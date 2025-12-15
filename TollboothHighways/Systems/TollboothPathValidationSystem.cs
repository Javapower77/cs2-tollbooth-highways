using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Validates vehicle paths after pathfinding completes.
    /// Marks paths as obsolete if vehicle type doesn't match tollbooth restrictions.
    /// 
    /// This is the effective solution since CS2's pathfinding is Burst-compiled
    /// and cannot be patched with Harmony directly.
    /// 
    /// Per AGENTS.MD: Burst-compatible, parallel execution, uses SystemAPI.
    /// </summary>
    [UpdateAfter(typeof(PathfindSetupSystem))]
    public sealed partial class TollboothPathValidationSystem : GameSystemBase
    {
        private EntityQuery m_VehiclePathQuery;
        private EntityQuery m_TollRoadQuery;
        
        // Repath attempt tracking to prevent infinite loops
        private NativeParallelHashMap<Entity, int> m_RepathAttempts;
        private const int MaxRepathAttempts = 5;
        
        // Logging control
        private uint m_LastLogFrame;
        private const uint LOG_INTERVAL_FRAMES = 300;
        
        private uint m_LastUpdateFrame;
        private const uint UPDATE_INTERVAL_FRAMES = 8;

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
            
            // Query toll roads
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<TollRoadPrefabData>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>() }
            });
            
            RequireForUpdate(m_TollRoadQuery);
        }
        
        protected override void OnDestroy()
        {
            if (m_RepathAttempts.IsCreated) 
                m_RepathAttempts.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 8;
        }
        
        protected override void OnUpdate()
        {
            var simulationSystem = World.GetExistingSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem?.frameIndex ?? 0;
            
            // Throttle updates
            if (currentFrame - m_LastUpdateFrame < UPDATE_INTERVAL_FRAMES)
                return;
            m_LastUpdateFrame = currentFrame;
            
            // Get lookups using SystemAPI for Burst compatibility
            var pathOwnerLookup = SystemAPI.GetComponentLookup<PathOwner>(false);
            var pathElementLookup = SystemAPI.GetBufferLookup<PathElement>(true);
            var ownerLookup = SystemAPI.GetComponentLookup<Owner>(true);
            
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
            bool shouldLogSummary = (currentFrame - m_LastLogFrame > LOG_INTERVAL_FRAMES) && 
                                    ModSettings.Instance?.EnableGeneralLogging == true;
            if (shouldLogSummary)
            {
                LogTollRoadsSummary(tollRoadLookup, privateLookup, truckLookup, publicLookup, serviceLookup, allVehiclesLookup);
                m_LastLogFrame = currentFrame;
            }
            
            // Calculate vehicle count for capacity allocation
            int vehicleCount = m_VehiclePathQuery.CalculateEntityCount();
            if (vehicleCount == 0)
                return;
            
            // Results for path invalidation
            var invalidPaths = new NativeList<Entity>(vehicleCount / 10, Allocator.TempJob);
            
            // Schedule validation job
            var validateJob = new ValidateVehiclePathsJob
            {
                PathOwnerLookup = pathOwnerLookup,
                PathElementLookup = pathElementLookup,
                OwnerLookup = ownerLookup,
                
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
                
                InvalidPaths = invalidPaths.AsParallelWriter()
            };
            
            JobHandle jobHandle = validateJob.ScheduleParallel(m_VehiclePathQuery, Dependency);
            jobHandle.Complete();

            // Apply path invalidations on main thread
            ApplyPathInvalidations(invalidPaths, pathOwnerLookup, currentFrame);
            
            // Cleanup
            invalidPaths.Dispose();
            
            // Periodic cleanup of repath attempts map
            if (currentFrame % 1000 == 0)
            {
                CleanupRepathAttempts();
            }
            
            Dependency = jobHandle;
        }
        
        private void LogTollRoadsSummary(
            ComponentLookup<TollRoadPrefabData> tollRoadLookup,
            ComponentLookup<TollRoadPrivateTransportData> privateLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.Temp);
            
            int privateCount = 0, truckCount = 0, publicCount = 0, serviceCount = 0, allCount = 0;
            
            for (int i = 0; i < tollRoadEntities.Length; i++)
            {
                var entity = tollRoadEntities[i];
                if (privateLookup.HasComponent(entity)) privateCount++;
                if (truckLookup.HasComponent(entity)) truckCount++;
                if (publicLookup.HasComponent(entity)) publicCount++;
                if (serviceLookup.HasComponent(entity)) serviceCount++;
                if (allVehiclesLookup.HasComponent(entity)) allCount++;
            }
            
            LogUtil.Info($"TollboothPathValidationSystem: {tollRoadEntities.Length} toll roads - Private:{privateCount} Truck:{truckCount} Public:{publicCount} Service:{serviceCount} All:{allCount}");
            
            tollRoadEntities.Dispose();
        }
        
        private void ApplyPathInvalidations(NativeList<Entity> invalidPaths, ComponentLookup<PathOwner> pathOwnerLookup, uint currentFrame)
        {
            if (invalidPaths.Length == 0)
                return;
                
            bool enableLogging = ModSettings.Instance?.EnableVehicleLogging == true;
            
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
                    m_RepathAttempts.TryAdd(vehicleEntity, 1);
                }
                
                if (enableLogging)
                {
                    int currentAttempts = m_RepathAttempts.TryGetValue(vehicleEntity, out int a) ? a : 1;
                    LogUtil.Debug($"TollboothPathValidationSystem: Vehicle {vehicleEntity.Index} path invalidated (attempt {currentAttempts}/{MaxRepathAttempts})");
                }
            }
            
            if (enableLogging && invalidPaths.Length > 0)
            {
                LogUtil.Info($"TollboothPathValidationSystem: Invalidated {invalidPaths.Length} vehicle paths at frame {currentFrame}");
            }
        }
        
        private void CleanupRepathAttempts()
        {
            // Clear map if it gets too large (vehicles despawned but entries remain)
            if (m_RepathAttempts.Count() > 5000)
            {
                m_RepathAttempts.Clear();
            }
        }
        
        // ----------------------- Jobs --------------------------
        
        [BurstCompile]
        private partial struct ValidateVehiclePathsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PathOwner> PathOwnerLookup;
            [ReadOnly] public BufferLookup<PathElement> PathElementLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            
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
            
            [WriteOnly] public NativeList<Entity>.ParallelWriter InvalidPaths;
            
            public void Execute(Entity vehicleEntity, in Car car, in PathOwner pathOwner)
            {
                // Skip if path is already obsolete or pending
                if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Pending)) != 0)
                    return;
                
                // Skip if max repath attempts reached (avoid infinite loop)
                if (RepathAttempts.TryGetValue(vehicleEntity, out int attempts) && attempts >= MaxAttempts)
                    return;
                
                // Determine vehicle group
                VehicleGroup vehicleGroup = GetVehicleGroup(vehicleEntity);
                
                // Service vehicles always allowed (emergency access)
                if (vehicleGroup == VehicleGroup.ServiceVehicles)
                    return;
                
                if (!PathElementLookup.HasBuffer(vehicleEntity))
                    return;

                var pathElements = PathElementLookup[vehicleEntity];
                
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
                    
                    // Check if vehicle is allowed on this toll road
                    if (!IsVehicleAllowed(vehicleGroup, roadEntity))
                    {
                        // Vehicle not allowed - invalidate path
                        InvalidPaths.AddNoResize(vehicleEntity);
                        return; // Stop checking, path is already invalid
                    }
                }
            }
            
            private VehicleGroup GetVehicleGroup(Entity vehicleEntity)
            {
                // Check specific types first (order matters for classification)
                
                // Public transport
                if (PublicTransportLookup.HasComponent(vehicleEntity)) 
                    return VehicleGroup.PublicTransport;
                if (TaxiLookup.HasComponent(vehicleEntity)) 
                    return VehicleGroup.PublicTransport;
                
                // Trucks
                if (DeliveryTruckLookup.HasComponent(vehicleEntity)) 
                    return VehicleGroup.Trucks;
                
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
                
                // If no specific restriction, allow all (backward compatibility)
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
                        return true; // Always allow service/emergency vehicles
                        
                    default:
                        return true;
                }
            }
        }
    }
}