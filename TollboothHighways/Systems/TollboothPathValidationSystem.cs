using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;

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
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
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
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });
            
            // Query toll roads for validation
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<TollRoadPrefabData>() },
                None = new[] { ComponentType.ReadOnly<Deleted>() }
            });
            
            m_RepathAttempts = new NativeParallelHashMap<Entity, int>(1024, Allocator.Persistent);
            
            RequireForUpdate(m_VehiclePathQuery);
            RequireForUpdate(m_TollRoadQuery);
            
            LogUtil.Info("TollboothPathValidationSystem: Created - validates vehicle paths against tollbooth restrictions");
        }
        
        protected override void OnDestroy()
        {
            if (m_RepathAttempts.IsCreated) m_RepathAttempts.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Check every ~0.5 seconds for responsive validation
            return 262144 / 512;
        }
        
        protected override void OnUpdate()
        {
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
            
            // Build toll road set for fast lookup
            var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.TempJob);
            var tollRoadSet = new NativeParallelHashSet<Entity>(tollRoadEntities.Length, Allocator.TempJob);
            for (int i = 0; i < tollRoadEntities.Length; i++)
            {
                tollRoadSet.Add(tollRoadEntities[i]);
            }
            
            // Results for path invalidation
            var invalidPaths = new NativeList<Entity>(Allocator.TempJob);
            
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
                
                TollRoadSet = tollRoadSet,
                RepathAttempts = m_RepathAttempts,
                MaxAttempts = MaxRepathAttempts,
                
                InvalidPaths = invalidPaths.AsParallelWriter()
            };
            
            var jobHandle = validateJob.ScheduleParallel(m_VehiclePathQuery, default);
            jobHandle.Complete();
            
            // Apply path invalidations on main thread
            ApplyPathInvalidations(invalidPaths, pathOwnerLookup);
            
            // Cleanup
            tollRoadEntities.Dispose();
            tollRoadSet.Dispose();
            invalidPaths.Dispose();
        }
        
        private void ApplyPathInvalidations(NativeList<Entity> invalidPaths, ComponentLookup<PathOwner> pathOwnerLookup)
        {
            int invalidatedCount = 0;
            
            for (int i = 0; i < invalidPaths.Length; i++)
            {
                var vehicleEntity = invalidPaths[i];
                
                if (!pathOwnerLookup.HasComponent(vehicleEntity))
                    continue;
                
                var pathOwner = pathOwnerLookup[vehicleEntity];
                
                // Mark path as obsolete - vanilla pathfinding will recalculate
                pathOwner.m_State |= PathFlags.Obsolete;
                pathOwnerLookup[vehicleEntity] = pathOwner;
                
                // Track repath attempts
                if (m_RepathAttempts.ContainsKey(vehicleEntity))
                {
                    m_RepathAttempts[vehicleEntity]++;
                }
                else
                {
                    m_RepathAttempts[vehicleEntity] = 1;
                }
                
                invalidatedCount++;
            }
            
            if (invalidatedCount > 0 && ModSettings.Instance?.EnableGeneralLogging == true)
            {
                LogUtil.Info($"TollboothPathValidationSystem: Invalidated {invalidatedCount} vehicle paths due to tollbooth restrictions");
            }
            
            // Cleanup old entries (vehicles that completed their journey)
            CleanupRepathAttempts();
        }
        
        private void CleanupRepathAttempts()
        {
            // Remove entries for entities that no longer exist or have valid paths
            var keysToRemove = new NativeList<Entity>(Allocator.Temp);
            
            foreach (var kvp in m_RepathAttempts)
            {
                if (!EntityManager.Exists(kvp.Key) || kvp.Value >= MaxRepathAttempts)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                m_RepathAttempts.Remove(keysToRemove[i]);
            }
            
            keysToRemove.Dispose();
        }
        
        // ----------------------- Jobs --------------------------
#if WITH_BURST
        [BurstCompile]
#endif
        private partial struct ValidateVehiclePathsJob : IJobEntity
        {
            public ComponentLookup<PathOwner> PathOwnerLookup;
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
            
            [ReadOnly] public NativeParallelHashSet<Entity> TollRoadSet;
            [ReadOnly] public NativeParallelHashMap<Entity, int> RepathAttempts;
            public int MaxAttempts;
            
            [WriteOnly] public NativeList<Entity>.ParallelWriter InvalidPaths;
            
            public void Execute(Entity vehicleEntity, in Car car, in PathOwner pathOwner)
            {
                // Skip if path is already obsolete or pending
                if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Pending)) != 0)
                    return;
                
                // Skip if max repath attempts reached
                if (RepathAttempts.TryGetValue(vehicleEntity, out int attempts) && attempts >= MaxAttempts)
                    return;
                
                // Get vehicle group
                VehicleGroup vehicleGroup = GetVehicleGroup(vehicleEntity);
                
                // Check path elements for toll roads
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
                        return;
                    }
                }
            }
            
            private VehicleGroup GetVehicleGroup(Entity vehicleEntity)
            {
                // Service vehicles (highest priority - emergency services)
                if (PoliceCarLookup.HasComponent(vehicleEntity) ||
                    AmbulanceLookup.HasComponent(vehicleEntity) ||
                    FireEngineLookup.HasComponent(vehicleEntity) ||
                    GarbageTruckLookup.HasComponent(vehicleEntity) ||
                    HearseLookup.HasComponent(vehicleEntity) ||
                    MaintenanceLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.ServiceVehicles;
                }
                
                // Public transport
                if (PublicTransportLookup.HasComponent(vehicleEntity) ||
                    TaxiLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PublicTransport;
                }
                
                // Trucks
                if (DeliveryTruckLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.Trucks;
                }
                
                // Default: Private transport
                return VehicleGroup.PrivateTransport;
            }
            
            private bool IsVehicleAllowed(VehicleGroup vehicleGroup, Entity roadEntity)
            {
                // All vehicles allowed
                if (AllVehiclesLookup.HasComponent(roadEntity))
                    return true;
                
                // Check specific restrictions
                bool hasPrivate = PrivateLookup.HasComponent(roadEntity);
                bool hasTruck = TruckLookup.HasComponent(roadEntity);
                bool hasPublic = PublicLookup.HasComponent(roadEntity);
                bool hasService = ServiceLookup.HasComponent(roadEntity);
                
                // If no specific restriction, allow all
                if (!hasPrivate && !hasTruck && !hasPublic && !hasService)
                    return true;
                
                // Match vehicle group to road type
                return vehicleGroup switch
                {
                    VehicleGroup.PrivateTransport => hasPrivate,
                    VehicleGroup.Trucks => hasTruck,
                    VehicleGroup.PublicTransport => hasPublic,
                    VehicleGroup.ServiceVehicles => hasService || true, // Service vehicles always allowed
                    _ => true
                };
            }
        }
    }
}