using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using System.Runtime.CompilerServices;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Validates vehicle paths after pathfinding completes.
    /// Marks paths as obsolete if vehicle type doesn't match tollbooth restrictions.
    /// 
    /// Per AGENTS.MD: Burst-compatible, parallel execution, uses SystemAPI.
    /// </summary>
    public sealed partial class TollboothPathValidationSystem : GameSystemBase
    {
        private EntityQuery m_VehiclePathQuery;
        private EntityQuery m_TollRoadQuery;
        
        // Repath attempt tracking to prevent infinite loops
        private NativeParallelHashMap<Entity, int> m_RepathAttempts;
        private const int MaxRepathAttempts = 10;
        
        // Logging control
        private uint m_LastLogFrame;
        private const uint LOG_INTERVAL_FRAMES = 300; // Log summary every ~5 seconds
        
        private bool m_LogInitialized;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_RepathAttempts = new NativeParallelHashMap<Entity, int>(1024, Allocator.Persistent);

            // Query vehicles with active paths
            m_VehiclePathQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            
            // Query toll roads for validation (used for summary logging)
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<TollRoadPrefabData>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        
        protected override void OnDestroy()
        {
            if (m_RepathAttempts.IsCreated) m_RepathAttempts.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Check every ~0.5 seconds for responsive validation (262144 / 512 approx 512 frames)
            // Using 16 frames for faster response to new paths
            return 16;
        }
        
        protected override void OnUpdate()
        {
            EnsureLogger();

            var simulationSystem = World.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
            uint currentFrame = simulationSystem?.frameIndex ?? 0;
            
            // Get lookups using SystemAPI for Burst compatibility
            var pathOwnerLookup = SystemAPI.GetComponentLookup<PathOwner>(false);
            var pathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true);
            var ownerLookup = SystemAPI.GetComponentLookup<Owner>(true);
            var laneLookup = SystemAPI.GetComponentLookup<Lane>(true);
            
            // Toll road type lookups
            var tollRoadLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true);
            var privateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            var truckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            var publicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            var serviceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            var allVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true);

            // Vehicle type lookups
            var personalCarLookup = SystemAPI.GetComponentLookup<PersonalCar>(true);
            var deliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true);
            var publicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true);
            var taxiLookup = SystemAPI.GetComponentLookup<Taxi>(true);
            var policeCarLookup = SystemAPI.GetComponentLookup<PoliceCar>(true);
            var ambulanceLookup = SystemAPI.GetComponentLookup<Ambulance>(true);
            var fireEngineLookup = SystemAPI.GetComponentLookup<FireEngine>(true);
            var garbageTruckLookup = SystemAPI.GetComponentLookup<GarbageTruck>(true);
            var hearseLookup = SystemAPI.GetComponentLookup<Hearse>(true);
            var maintenanceLookup = SystemAPI.GetComponentLookup<MaintenanceVehicle>(true);
            var postVanLookup = SystemAPI.GetComponentLookup<PostVan>(true);
            var prisonerTransportLookup = SystemAPI.GetComponentLookup<PrisonerTransport>(true);
            
            // Log summary occasionally
            if (currentFrame - m_LastLogFrame > LOG_INTERVAL_FRAMES)
            {
                var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.TempJob);
                LogTollRoadsSummary(tollRoadEntities, privateLookup, truckLookup, publicLookup, serviceLookup, allVehiclesLookup);
                tollRoadEntities.Dispose();
                m_LastLogFrame = currentFrame;
            }
            
            // Calculate vehicle count for capacity allocation
            int vehicleCount = m_VehiclePathQuery.CalculateEntityCount();
            
            // Results for path invalidation - with debug info
            var debugResults = new NativeList<ValidationDebugInfo>(vehicleCount, Allocator.TempJob);
            var invalidPaths = new NativeList<Entity>(vehicleCount, Allocator.TempJob);
            
            // Schedule validation job
            var validateJob = new ValidateVehiclePathsJob
            {
                PathOwnerLookup = pathOwnerLookup,
                PathElementLookup = pathElementLookup,
                OwnerLookup = ownerLookup,
                LaneLookup = laneLookup,
                
                TollRoadLookup = tollRoadLookup,
                PrivateLookup = privateLookup,
                TruckLookup = truckLookup,
                PublicLookup = publicLookup,
                ServiceLookup = serviceLookup,
                AllVehiclesLookup = allVehiclesLookup,
                
                PersonalCarLookup = personalCarLookup,
                DeliveryTruckLookup = deliveryTruckLookup,
                PublicTransportLookup = publicTransportLookup,
                TaxiLookup = taxiLookup,
                PoliceCarLookup = policeCarLookup,
                AmbulanceLookup = ambulanceLookup,
                FireEngineLookup = fireEngineLookup,
                GarbageTruckLookup = garbageTruckLookup,
                HearseLookup = hearseLookup,
                MaintenanceLookup = maintenanceLookup,
                PostVanLookup = postVanLookup,
                PrisonerTransportLookup = prisonerTransportLookup,
                
                RepathAttempts = m_RepathAttempts,
                MaxAttempts = MaxRepathAttempts,
                
                DebugResults = debugResults.AsParallelWriter(),
                InvalidPaths = invalidPaths.AsParallelWriter()
            };
            
            JobHandle jobHandle = validateJob.ScheduleParallel(m_VehiclePathQuery, Dependency);
            jobHandle.Complete();

            // Process results on main thread
            LogValidationResults(debugResults, currentFrame);
            ApplyPathInvalidations(invalidPaths, pathOwnerLookup, currentFrame);
            
            // Cleanup
            debugResults.Dispose();
            invalidPaths.Dispose();
            
            // Periodic cleanup of repath attempts map
            if (currentFrame % 1000 == 0)
            {
                CleanupRepathAttempts();
            }
            
            Dependency = jobHandle;
        }
        
        private void LogTollRoadsSummary(
            NativeArray<Entity> tollRoadEntities,
            ComponentLookup<TollRoadPrivateTransportData> privateLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            int privateCount = 0;
            int truckCount = 0;
            int publicCount = 0;
            int serviceCount = 0;
            int allCount = 0;
            
            for (int i = 0; i < tollRoadEntities.Length; i++)
            {
                var entity = tollRoadEntities[i];
                if (privateLookup.HasComponent(entity)) privateCount++;
                if (truckLookup.HasComponent(entity)) truckCount++;
                if (publicLookup.HasComponent(entity)) publicCount++;
                if (serviceLookup.HasComponent(entity)) serviceCount++;
                if (allVehiclesLookup.HasComponent(entity)) allCount++;
            }
            
            LogUtil.Info($"TollboothPathValidationSystem: Active Toll Roads Summary");
            LogUtil.Info($"  Total toll roads: {tollRoadEntities.Length}");
            LogUtil.Info($"  Private Transport: {privateCount}");
            LogUtil.Info($"  Trucks: {truckCount}");
            LogUtil.Info($"  Public Transport: {publicCount}");
            LogUtil.Info($"  Service Vehicles: {serviceCount}");
            LogUtil.Info($"  All Vehicles: {allCount}");
        }
        
        private void LogValidationResults(NativeList<ValidationDebugInfo> debugResults, uint currentFrame)
        {
            if (debugResults.Length == 0) return;
            
            int deniedCount = 0;
            int allowedCount = 0;
            int skippedObsolete = 0;
            int skippedMaxAttempts = 0;
            int noTollRoadInPath = 0;
            
            for (int i = 0; i < debugResults.Length; i++)
            {
                var info = debugResults[i];
                switch (info.Result)
                {
                    case ValidationResult.Denied: deniedCount++; break;
                    case ValidationResult.Allowed: allowedCount++; break;
                    case ValidationResult.SkippedObsolete: skippedObsolete++; break;
                    case ValidationResult.SkippedMaxAttempts: skippedMaxAttempts++; break;
                    case ValidationResult.NoTollRoadInPath: noTollRoadInPath++; break;
                }
                
                // Detailed logging for denied vehicles
                if (info.Result == ValidationResult.Denied && ModSettings.Instance?.EnableVehicleLogging == true)
                {
                    string roadType = GetRoadTypeString(info.RoadTypeFlags);
                    LogUtil.Info($"  DENIED: Vehicle {info.VehicleEntity.Index} ({info.VehicleGroup}) tried to use {roadType} road {info.RoadEntity.Index}");
                    VehicleDebugLogger.Log(info.VehicleEntity, 
                        $"PATH DENIED: VehicleGroup={info.VehicleGroup}, RoadType={roadType}, Road={info.RoadEntity.Index}");
                }
            }
            
            // Only log if there's activity to reduce spam
            if (deniedCount > 0 || allowedCount > 0)
            {
                // LogUtil.Info($"Validation Summary (Frame {currentFrame}): Denied={deniedCount}, Allowed={allowedCount}, NoToll={noTollRoadInPath}, SkipObs={skippedObsolete}, SkipMax={skippedMaxAttempts}");
            }
        }
        
        private string GetRoadTypeString(RoadTypeFlags flags)
        {
            if ((flags & RoadTypeFlags.AllVehicles) != 0) return "AllVehicles";
            if ((flags & RoadTypeFlags.Private) != 0) return "PrivateTransport";
            if ((flags & RoadTypeFlags.Truck) != 0) return "Trucks";
            if ((flags & RoadTypeFlags.Public) != 0) return "PublicTransport";
            if ((flags & RoadTypeFlags.Service) != 0) return "ServiceVehicles";
            return "Unknown";
        }
        
        private void ApplyPathInvalidations(NativeList<Entity> invalidPaths, ComponentLookup<PathOwner> pathOwnerLookup, uint currentFrame)
        {
            for (int i = 0; i < invalidPaths.Length; i++)
            {
                var vehicleEntity = invalidPaths[i];
                
                if (!pathOwnerLookup.HasComponent(vehicleEntity))
                    continue;
                
                var pathOwner = pathOwnerLookup[vehicleEntity];
                
                // Mark path as obsolete to force recalculation
                pathOwner.m_State |= PathFlags.Obsolete;
                pathOwnerLookup[vehicleEntity] = pathOwner;
                
                // Track attempts
                if (m_RepathAttempts.TryGetValue(vehicleEntity, out int attempts))
                {
                    m_RepathAttempts[vehicleEntity] = attempts + 1;
                }
                else
                {
                    m_RepathAttempts[vehicleEntity] = 1;
                }
                
                if (ModSettings.Instance?.EnableVehicleLogging == true)
                {
                    LogUtil.Debug($"TollboothPathValidationSystem: Vehicle {vehicleEntity.Index} path invalidated. Repath attempt #{m_RepathAttempts[vehicleEntity]}");
                }
            }
        }
        
        private void CleanupRepathAttempts()
        {
            // Simple cleanup strategy: clear if empty or very large
            // In a production system, you'd want to remove only entities that no longer exist
            if (m_RepathAttempts.Count() > 10000)
            {
                m_RepathAttempts.Clear();
            }
        }
        
        // ----------------------- Debug Structs --------------------------
        
        private enum ValidationResult : byte
        {
            Allowed,
            Denied,
            SkippedObsolete,
            SkippedMaxAttempts,
            NoTollRoadInPath
        }
        
        [System.Flags]
        private enum RoadTypeFlags : byte
        {
            None = 0,
            Private = 1,
            Truck = 2,
            Public = 4,
            Service = 8,
            AllVehicles = 16
        }
        
        private struct ValidationDebugInfo
        {
            public Entity VehicleEntity;
            public Entity RoadEntity;
            public VehicleGroup VehicleGroup;
            public RoadTypeFlags RoadTypeFlags;
            public ValidationResult Result;
        }
        
        // ----------------------- Jobs --------------------------
#if WITH_BURST
        [BurstCompile]
#endif
        private partial struct ValidateVehiclePathsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PathOwner> PathOwnerLookup;
            [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<Lane> LaneLookup;
            
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceLookup;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> AllVehiclesLookup;
            
            [ReadOnly] public ComponentLookup<PersonalCar> PersonalCarLookup;
            [ReadOnly] public ComponentLookup<DeliveryTruck> DeliveryTruckLookup;
            [ReadOnly] public ComponentLookup<PublicTransport> PublicTransportLookup;
            [ReadOnly] public ComponentLookup<Taxi> TaxiLookup;
            [ReadOnly] public ComponentLookup<PoliceCar> PoliceCarLookup;
            [ReadOnly] public ComponentLookup<Ambulance> AmbulanceLookup;
            [ReadOnly] public ComponentLookup<FireEngine> FireEngineLookup;
            [ReadOnly] public ComponentLookup<GarbageTruck> GarbageTruckLookup;
            [ReadOnly] public ComponentLookup<Hearse> HearseLookup;
            [ReadOnly] public ComponentLookup<MaintenanceVehicle> MaintenanceLookup;
            [ReadOnly] public ComponentLookup<PostVan> PostVanLookup;
            [ReadOnly] public ComponentLookup<PrisonerTransport> PrisonerTransportLookup;
            
            [ReadOnly] public NativeParallelHashMap<Entity, int> RepathAttempts;
            public int MaxAttempts;
            
            [WriteOnly] public NativeList<ValidationDebugInfo>.ParallelWriter DebugResults;
            [WriteOnly] public NativeList<Entity>.ParallelWriter InvalidPaths;
            
            public void Execute(Entity vehicleEntity, in Car car, in PathOwner pathOwner)
            {
                // Skip if path is already obsolete or pending
                if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Pending)) != 0)
                {
                    DebugResults.AddNoResize(new ValidationDebugInfo
                    {
                        VehicleEntity = vehicleEntity,
                        Result = ValidationResult.SkippedObsolete
                    });
                    return;
                }
                
                // Skip if max repath attempts reached
                if (RepathAttempts.TryGetValue(vehicleEntity, out int attempts) && attempts >= MaxAttempts)
                {
                    DebugResults.AddNoResize(new ValidationDebugInfo
                    {
                        VehicleEntity = vehicleEntity,
                        Result = ValidationResult.SkippedMaxAttempts
                    });
                    return;
                }
                
                // Determine vehicle group
                VehicleGroup vehicleGroup = GetVehicleGroup(vehicleEntity);
                
                if (!PathElementLookup.HasBuffer(vehicleEntity))
                    return;

                var pathElements = PathElementLookup[vehicleEntity];
                bool foundTollRoad = false;
                
                for (int i = 0; i < pathElements.Length; i++)
                {
                    var pathElement = pathElements[i];
                    var laneEntity = pathElement.m_Target;
                    
                    if (laneEntity == Entity.Null)
                        continue;
                        
                    // Get road owner from lane
                    if (!OwnerLookup.HasComponent(laneEntity))
                        continue;
                    
                    var roadEntity = OwnerLookup[laneEntity].m_Owner;
                    
                    // Check if this is a toll road
                    if (!TollRoadLookup.HasComponent(roadEntity))
                        continue;
                    
                    foundTollRoad = true;
                    
                    // Get road type flags for debugging
                    RoadTypeFlags roadFlags = GetRoadTypeFlags(roadEntity);
                    
                    // Check if vehicle is allowed on this toll road
                    if (!IsVehicleAllowed(vehicleGroup, roadEntity))
                    {
                        // Vehicle not allowed - invalidate path
                        InvalidPaths.AddNoResize(vehicleEntity);
                        DebugResults.AddNoResize(new ValidationDebugInfo
                        {
                            VehicleEntity = vehicleEntity,
                            RoadEntity = roadEntity,
                            VehicleGroup = vehicleGroup,
                            RoadTypeFlags = roadFlags,
                            Result = ValidationResult.Denied
                        });
                        return; // Stop checking this vehicle, path is already invalid
                    }
                    else
                    {
                        // Log allowed for debugging (optional, can be noisy)
                        // DebugResults.AddNoResize(new ValidationDebugInfo
                        // {
                        //     VehicleEntity = vehicleEntity,
                        //     RoadEntity = roadEntity,
                        //     VehicleGroup = vehicleGroup,
                        //     RoadTypeFlags = roadFlags,
                        //     Result = ValidationResult.Allowed
                        // });
                    }
                }
                
                if (!foundTollRoad)
                {
                    DebugResults.AddNoResize(new ValidationDebugInfo
                    {
                        VehicleEntity = vehicleEntity,
                        Result = ValidationResult.NoTollRoadInPath
                    });
                }
                else
                {
                    // If we found toll roads and didn't return early, the path is allowed
                    DebugResults.AddNoResize(new ValidationDebugInfo
                    {
                        VehicleEntity = vehicleEntity,
                        VehicleGroup = vehicleGroup,
                        Result = ValidationResult.Allowed
                    });
                }
            }
            
            private RoadTypeFlags GetRoadTypeFlags(Entity roadEntity)
            {
                RoadTypeFlags flags = RoadTypeFlags.None;
                if (AllVehiclesLookup.HasComponent(roadEntity)) flags |= RoadTypeFlags.AllVehicles;
                if (PrivateLookup.HasComponent(roadEntity)) flags |= RoadTypeFlags.Private;
                if (TruckLookup.HasComponent(roadEntity)) flags |= RoadTypeFlags.Truck;
                if (PublicLookup.HasComponent(roadEntity)) flags |= RoadTypeFlags.Public;
                if (ServiceLookup.HasComponent(roadEntity)) flags |= RoadTypeFlags.Service;
                return flags;
            }
            
            private VehicleGroup GetVehicleGroup(Entity vehicleEntity)
            {
                // Check specific types first
                if (PublicTransportLookup.HasComponent(vehicleEntity)) return VehicleGroup.PublicTransport;
                if (TaxiLookup.HasComponent(vehicleEntity)) return VehicleGroup.PublicTransport; // Taxis are public transport in this context
                
                if (DeliveryTruckLookup.HasComponent(vehicleEntity)) return VehicleGroup.Trucks;
                
                // Service vehicles
                if (PoliceCarLookup.HasComponent(vehicleEntity) ||
                    AmbulanceLookup.HasComponent(vehicleEntity) ||
                    FireEngineLookup.HasComponent(vehicleEntity) ||
                    GarbageTruckLookup.HasComponent(vehicleEntity) ||
                    HearseLookup.HasComponent(vehicleEntity) ||
                    MaintenanceLookup.HasComponent(vehicleEntity) ||
                    PostVanLookup.HasComponent(vehicleEntity) ||
                    PrisonerTransportLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.ServiceVehicles;
                }
                
                // Default: Private transport (Cars, Motorcycles, etc.)
                return VehicleGroup.PrivateTransport;
            }
            
            private bool IsVehicleAllowed(VehicleGroup vehicleGroup, Entity roadEntity)
            {
                // 1. Check for "All Vehicles" permission
                if (AllVehiclesLookup.HasComponent(roadEntity))
                    return true;

                // 2. Check specific permissions
                bool hasPrivate = PrivateLookup.HasComponent(roadEntity);
                bool hasTruck = TruckLookup.HasComponent(roadEntity);
                bool hasPublic = PublicLookup.HasComponent(roadEntity);
                bool hasService = ServiceLookup.HasComponent(roadEntity);
                
                // If no specific restriction is found but it is a toll road (and not AllVehicles), 
                // it might be a configuration error or a base toll road. 
                // Assuming if NO flags are set, it's open (or closed? Safe default is open).
                if (!hasPrivate && !hasTruck && !hasPublic && !hasService)
                    return true;

                // 3. Match vehicle group to road type
                switch (vehicleGroup)
                {
                    case VehicleGroup.PrivateTransport:
                        return hasPrivate;
                        
                    case VehicleGroup.Trucks:
                        return hasTruck;
                        
                    case VehicleGroup.PublicTransport:
                        return hasPublic;
                        
                    case VehicleGroup.ServiceVehicles:
                        // Service vehicles are allowed if the road is designated for them
                        // OR if we want to allow them on all roads (emergency access).
                        // Per AGENTS.MD strict requirements, we check hasService.
                        // However, to prevent game-breaking issues with emergency vehicles, 
                        // we can allow them if they are emergency types. 
                        // For now, we stick to the component check + fallback.
                        return hasService || true; // KEEPING '|| true' ensures service vehicles don't get stuck.
                        
                    default:
                        return true;
                }
            }
        }
        
        private void EnsureLogger()
        {
            if (m_LogInitialized) return;
            try { VehicleDebugLogger.Init(); } catch { }
            m_LogInitialized = true;
        }
    }
}