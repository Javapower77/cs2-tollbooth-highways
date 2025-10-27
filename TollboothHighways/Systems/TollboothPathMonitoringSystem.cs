using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Game.Common;
using Game.Vehicles;
using Game.Pathfind;
using Game.Net;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Game;
using Game.Objects;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors vehicle paths and invalidates those using incompatible tollbooth lanes.
    /// Per AGENTS.MD: Uses CarLaneFlags set by TollBoothSpawnSystem.
    /// Marks paths as obsolete (max 10 retries) when violations detected.
    /// Tracks total attempts until vehicle finds compatible path or reaches max attempts.
    /// </summary>
    public partial class TollboothPathMonitoringSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;
        private EndFrameBarrier m_EndFrameBarrier;
        private bool m_LogInitialized = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PathOwner>(),
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            RequireForUpdate(m_VehicleQuery);
            RequireForUpdate<TollRoadPrefabData>();

            LogUtil.Info("TollboothPathMonitoringSystem: Created (per AGENTS.MD - monitors CarLaneFlags compliance)");
        }

        protected override void OnUpdate()
        {
            EnsureLogger();

            var monitorJob = new MonitorPathForTollboothViolationsJob
            {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_PathOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<PathOwner>(false),
                m_PathElementTypeHandle = SystemAPI.GetBufferTypeHandle<PathElement>(true),
                m_RepathAttemptsTypeHandle = SystemAPI.GetComponentTypeHandle<TollboothRepathAttempts>(false),

                m_CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>(true),
                m_OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                m_TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),

                // Per AGENTS.MD: Check specific tollbooth types
                m_PrivateTransportLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                m_TruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                m_PublicTransportLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                m_ServiceVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),

                // Per AGENTS.MD: Use VehiclesUtil patterns for vehicle detection
                m_PublicTransportVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                m_DeliveryTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                m_GarbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                m_PoliceCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                m_AmbulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                m_FireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                m_HearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                m_MaintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),
                m_PostVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                m_TaxiLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                m_PersonalCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                m_MotorbikeLookup = SystemAPI.GetComponentLookup<MotorbikePrefabData>(true),
                m_EvacuatingTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.EvacuatingTransport>(true),
                m_PrisonerTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PrisonerTransport>(true),

                m_Ecb = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
            };

            Dependency = monitorJob.ScheduleParallel(m_VehicleQuery, Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct MonitorPathForTollboothViolationsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;
            public ComponentTypeHandle<PathOwner> m_PathOwnerTypeHandle;
            [ReadOnly] public BufferTypeHandle<PathElement> m_PathElementTypeHandle;
            public ComponentTypeHandle<TollboothRepathAttempts> m_RepathAttemptsTypeHandle;

            [ReadOnly] public ComponentLookup<CarLane> m_CarLaneLookup;
            [ReadOnly] public ComponentLookup<Owner> m_OwnerLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> m_TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> m_TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;

            // Vehicle type detection
            [ReadOnly] public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportVehicleLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTruckLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> m_GarbageTruckLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> m_PoliceCarLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> m_FireEngineLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> m_HearseLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.MaintenanceVehicle> m_MaintenanceVehicleLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> m_PostVanLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Taxi> m_TaxiLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarLookup;
            [ReadOnly] public ComponentLookup<MotorbikePrefabData> m_MotorbikeLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.EvacuatingTransport> m_EvacuatingTransportLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PrisonerTransport> m_PrisonerTransportLookup;
            public EntityCommandBuffer.ParallelWriter m_Ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(m_EntityTypeHandle);
                var pathOwners = chunk.GetNativeArray(ref m_PathOwnerTypeHandle);
                var pathElementBuffers = chunk.GetBufferAccessor(ref m_PathElementTypeHandle);

                bool hasRepathAttempts = chunk.Has(ref m_RepathAttemptsTypeHandle);
                NativeArray<TollboothRepathAttempts> repathAttempts = default;
                if (hasRepathAttempts)
                {
                    repathAttempts = chunk.GetNativeArray(ref m_RepathAttemptsTypeHandle);
                }

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var pathOwner = pathOwners[i];
                    var pathElements = pathElementBuffers[i];

                    // Only validate completed paths (not pending/obsolete/failed)
                    if ((pathOwner.m_State & PathFlags.Pending) != 0 ||
                        (pathOwner.m_State & PathFlags.Obsolete) != 0 ||
                        (pathOwner.m_State & PathFlags.Failed) != 0 ||
                        pathElements.Length == 0)
                    {
                        continue;
                    }

                    // Track attempts persistently until max reached or valid path found
                    TollboothRepathAttempts attempts = default;
                    if (hasRepathAttempts)
                    {
                        attempts = repathAttempts[i];

                        // Only stop trying when max attempts reached
                        if (attempts.HasReachedMaxAttempts)
                        {
                           // VehicleDebugLogger.Log(entity,
                           //     $"TollboothPathMonitoring: Max 10 attempts reached. Allowing current path.");
                            
                            // Reset counter so vehicle can pathfind normally going forward
                            attempts.AttemptCount = 0;
                            attempts.LastValidatedElementCount = 0;
                            repathAttempts[i] = attempts;
                            continue;
                        }
                    }

                    // Use VehiclesUtil patterns to determine vehicle type
                    VehicleGroup vehicleGroup = DetermineVehicleType(entity);

                    // Scan path for tollbooth violations
                    bool hasViolation = false;
                    bool hasTollboothInPath = false;
                    Entity violationLaneEntity = Entity.Null;
                    Entity violationRoadEntity = Entity.Null;

                    for (int j = 0; j < pathElements.Length; j++)
                    {
                        var laneEntity = pathElements[j].m_Target;

                        if (!m_CarLaneLookup.HasComponent(laneEntity) ||
                            !m_OwnerLookup.HasComponent(laneEntity))
                        {
                            continue;
                        }

                        var carLane = m_CarLaneLookup[laneEntity];
                        var owner = m_OwnerLookup[laneEntity];
                        var roadEntity = owner.m_Owner;

                        // Check if this is a tollbooth road
                        if (!m_TollRoadPrefabLookup.HasComponent(roadEntity))
                        {
                            continue;
                        }

                        hasTollboothInPath = true;

                        // Check vehicle group against tollbooth type
                        bool isAllowed = CheckVehicleAllowedOnTollbooth(vehicleGroup, roadEntity, carLane);

                        if (!isAllowed)
                        {
                            hasViolation = true;
                            violationLaneEntity = laneEntity;
                            violationRoadEntity = roadEntity;

                            //VehicleDebugLogger.Log(entity,
                            //    $"TollboothPathMonitoring: VIOLATION - {vehicleGroup} not allowed on lane {laneEntity.Index} " +
                            //    $"(road {roadEntity.Index}, flags: {carLane.m_Flags})");
                            break;
                        }
                    }

                    if (hasViolation)
                    {
                        // Increment total attempts (never reset until max or success)
                        attempts.AttemptCount++;
                        attempts.LastValidatedElementCount = pathElements.Length;

                        //VehicleDebugLogger.Log(entity,
                        //    $"TollboothPathMonitoring: INVALIDATING path (attempt {attempts.AttemptCount}/10 per AGENTS.MD). " +
                        //    $"Violation: lane {violationLaneEntity.Index}, road {violationRoadEntity.Index}. " +
                        //    $"Vehicle type: {vehicleGroup}");

                        // Mark path obsolete to trigger repath
                        pathOwner.m_State |= PathFlags.Obsolete;
                        pathOwners[i] = pathOwner;

                        // Update or add attempts component
                        if (hasRepathAttempts)
                        {
                            repathAttempts[i] = attempts;
                        }
                        else
                        {
                            m_Ecb.AddComponent(unfilteredChunkIndex, entity, attempts);
                        }
                    }
                    else if (hasTollboothInPath && hasRepathAttempts && attempts.AttemptCount > 0)
                    {
                        // Only reset if vehicle found VALID path through tollbooth
                        // This means they successfully found a compatible tollbooth
                        //VehicleDebugLogger.Log(entity,
                        //    $"TollboothPathMonitoring: SUCCESS - Valid compatible tollbooth path found after {attempts.AttemptCount} attempts. " +
                        //    $"Vehicle type: {vehicleGroup}. Resetting counter.");
                        
                        attempts.AttemptCount = 0;
                        attempts.LastValidatedElementCount = 0;
                        repathAttempts[i] = attempts;
                    }
                    else if (!hasTollboothInPath && hasRepathAttempts && attempts.AttemptCount > 0)
                    {
                        // Path doesn't go through any tollbooth - also reset counter
                        //VehicleDebugLogger.Log(entity,
                        //    $"TollboothPathMonitoring: Path avoids all tollbooths after {attempts.AttemptCount} attempts. " +
                        //    $"Vehicle type: {vehicleGroup}. Resetting counter.");
                        
                        attempts.AttemptCount = 0;
                        attempts.LastValidatedElementCount = 0;
                        repathAttempts[i] = attempts;
                    }
                }
            }

            private VehicleGroup DetermineVehicleType(Entity vehicleEntity)
            {
                // Service vehicles (highest priority)
                if (m_PoliceCarLookup.HasComponent(vehicleEntity) ||
                    m_GarbageTruckLookup.HasComponent(vehicleEntity) ||
                    m_AmbulanceLookup.HasComponent(vehicleEntity) ||
                    m_FireEngineLookup.HasComponent(vehicleEntity) ||
                    m_EvacuatingTransportLookup.HasComponent(vehicleEntity) ||
                    m_HearseLookup.HasComponent(vehicleEntity) ||
                    m_MaintenanceVehicleLookup.HasComponent(vehicleEntity) ||
                    m_PrisonerTransportLookup.HasComponent(vehicleEntity) ||
                    m_PostVanLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.ServiceVehicles;
                }

                // Public transport (buses, taxis)
                if (m_PublicTransportVehicleLookup.HasComponent(vehicleEntity) ||
                    m_TaxiLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PublicTransport;
                }

                // Trucks
                if (m_DeliveryTruckLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.Trucks;
                }

                // Private cars (default)
                if (m_PersonalCarLookup.HasComponent(vehicleEntity) ||
                    m_MotorbikeLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PrivateTransport;
                }

                return VehicleGroup.PrivateTransport; // Default fallback
            }

            private bool CheckVehicleAllowedOnTollbooth(VehicleGroup vehicleGroup, Entity roadEntity, CarLane carLane)
            {
                // Per AGENTS.MD: Match vehicle type to tollbooth type

                // Private Transport tollbooth (ForbidHeavyTraffic flag)
                if (m_PrivateTransportLookup.HasComponent(roadEntity))
                {
                    // Allow only PersonalCar (blocks trucks)
                    return vehicleGroup == VehicleGroup.PrivateTransport;
                }

                // Truck tollbooth (ForbidTransitTraffic flag)
                if (m_TruckLookup.HasComponent(roadEntity))
                {
                    // Allow only DeliveryTruck (blocks cars/buses)
                    return vehicleGroup == VehicleGroup.Trucks;
                }

                // Public Transport tollbooth (PublicOnly flag)
                if (m_PublicTransportLookup.HasComponent(roadEntity))
                {
                    // Allow only Buses and Taxis
                    return vehicleGroup == VehicleGroup.PublicTransport;
                }

                // Service Vehicle tollbooth
                if (m_ServiceVehiclesLookup.HasComponent(roadEntity))
                {
                    // Allow only service vehicles
                    return vehicleGroup == VehicleGroup.ServiceVehicles;
                }

                // Unknown tollbooth type - allow by default
                return true;
            }
        }

        private void EnsureLogger()
        {
            if (m_LogInitialized)
            {
                return;
            }

            try
            {
                VehicleDebugLogger.Init();
                VehicleDebugLogger.LogOnce("=== TollboothPathMonitoringSystem logging started ===");
            }
            catch
            {
                // best-effort logging initialization
            }

            m_LogInitialized = true;
        }
    }
}