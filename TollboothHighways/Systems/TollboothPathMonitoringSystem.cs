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
using CarLaneFlags = Game.Net.CarLaneFlags;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors vehicle paths and blocks incompatible tollbooth lanes dynamically.
    /// Temporarily modifies CarLane flags to force pathfinder to choose alternative routes.
    /// </summary>
    public partial class TollboothPathMonitoringSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;
        private EntityQuery m_BlockedLaneQuery;
        private EndFrameBarrier m_EndFrameBarrier;
        private bool m_LogInitialized = false;

        // Track blocked lanes and when to unblock them
        private NativeHashMap<Entity, uint> m_BlockedLanes;
        private const uint BLOCK_DURATION_FRAMES = 60; // 1 second at 60fps

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

            // Query for blocked lanes that need to be restored
            m_BlockedLaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CarLane>(),
                    ComponentType.ReadOnly<VehicleBlockedLane>()
                }
            });

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_BlockedLanes = new NativeHashMap<Entity, uint>(100, Allocator.Persistent);

            RequireForUpdate(m_VehicleQuery);
            RequireForUpdate<TollRoadPrefabData>();

            LogUtil.Info("TollboothPathMonitoringSystem: Created with dynamic lane blocking");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_BlockedLanes.Dispose();
        }

        protected override void OnUpdate()
        {
            EnsureLogger();

            uint currentFrame = World.GetExistingSystemManaged<Game.Simulation.SimulationSystem>().frameIndex;

            // First, restore any lanes that have been blocked long enough
            var restoreJob = new RestoreBlockedLanesJob
            {
                m_CarLaneTypeHandle = SystemAPI.GetComponentTypeHandle<CarLane>(false),
                m_BlockedLaneTypeHandle = SystemAPI.GetComponentTypeHandle<VehicleBlockedLane>(true),
                m_CurrentFrame = currentFrame,
                m_Ecb = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
            };

            Dependency = restoreJob.ScheduleParallel(m_BlockedLaneQuery, Dependency);

            // Then monitor paths and block incompatible lanes
            var monitorJob = new MonitorPathForTollboothViolationsJob
            {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                m_PathOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<PathOwner>(false),
                m_PathElementTypeHandle = SystemAPI.GetBufferTypeHandle<PathElement>(true),
                m_RepathAttemptsTypeHandle = SystemAPI.GetComponentTypeHandle<TollboothRepathAttempts>(false),

                m_CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>(false), // Need write access
                m_OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                m_TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),
                m_VehicleBlockedLaneLookup = SystemAPI.GetComponentLookup<VehicleBlockedLane>(true),

                // Tollbooth type lookups
                m_PrivateTransportLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                m_TruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                m_PublicTransportLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                m_ServiceVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),

                // Vehicle type detection
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

                m_CurrentFrame = currentFrame,
                m_Ecb = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
            };

            Dependency = monitorJob.ScheduleParallel(m_VehicleQuery, Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct RestoreBlockedLanesJob : IJobChunk
        {
            public ComponentTypeHandle<CarLane> m_CarLaneTypeHandle;
            [ReadOnly] public ComponentTypeHandle<VehicleBlockedLane> m_BlockedLaneTypeHandle;
            [ReadOnly] public uint m_CurrentFrame;
            public EntityCommandBuffer.ParallelWriter m_Ecb;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var carLanes = chunk.GetNativeArray(ref m_CarLaneTypeHandle);
                var blockedLanes = chunk.GetNativeArray(ref m_BlockedLaneTypeHandle);
                //var entities = chunk.GetEntityDataPtrRO(m_CarLaneTypeHandle.GlobalSystemVersion);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var blockedLane = blockedLanes[i];
                    
                    // Check if it's time to restore this lane
                    if (m_CurrentFrame >= blockedLane.UnblockAtFrame)
                    {
                        var carLane = carLanes[i];
                        
                        // Restore original flags
                        carLane.m_Flags = blockedLane.OriginalFlags;
                        carLanes[i] = carLane;
                        
                        // Remove the blocked component
                        // Note: We need entity access - this is a limitation
                        // We'll handle this differently
                    }
                }
            }
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

            public ComponentLookup<CarLane> m_CarLaneLookup; // Write access for blocking
            [ReadOnly] public ComponentLookup<Owner> m_OwnerLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> m_TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<VehicleBlockedLane> m_VehicleBlockedLaneLookup;
            
            // Tollbooth types
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
            
            [ReadOnly] public uint m_CurrentFrame;
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

                    // Only validate completed paths
                    if ((pathOwner.m_State & PathFlags.Pending) != 0 ||
                        (pathOwner.m_State & PathFlags.Obsolete) != 0 ||
                        (pathOwner.m_State & PathFlags.Failed) != 0 ||
                        pathElements.Length == 0)
                    {
                        continue;
                    }

                    TollboothRepathAttempts attempts = default;
                    if (hasRepathAttempts)
                    {
                        attempts = repathAttempts[i];

                        if (attempts.HasReachedMaxAttempts)
                        {
                            VehicleDebugLogger.Log(entity,
                                $"TollboothPathMonitoring: Max {attempts.AttemptCount} attempts reached. Allowing current path.");
                            
                            attempts.AttemptCount = 0;
                            attempts.LastValidatedElementCount = 0;
                            repathAttempts[i] = attempts;
                            continue;
                        }
                        
                        if (attempts.LastValidatedElementCount == pathElements.Length && attempts.AttemptCount > 0)
                        {
                            continue;
                        }
                    }

                    VehicleGroup vehicleGroup = DetermineVehicleType(entity);

                    // Scan path for violations
                    bool hasViolation = false;
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

                        if (!m_TollRoadPrefabLookup.HasComponent(roadEntity))
                        {
                            continue;
                        }

                        bool isAllowed = CheckVehicleAllowedOnTollbooth(vehicleGroup, roadEntity, carLane);

                        if (!isAllowed)
                        {
                            hasViolation = true;
                            violationLaneEntity = laneEntity;
                            violationRoadEntity = roadEntity;

                            VehicleDebugLogger.Log(entity,
                                $"TollboothPathMonitoring: VIOLATION - {vehicleGroup} not allowed on lane {laneEntity.Index}");
                            break;
                        }
                    }

                    if (hasViolation)
                    {
                        attempts.AttemptCount++;
                        attempts.LastValidatedElementCount = pathElements.Length;

                        // CRITICAL: Block the violating lane temporarily
                        if (!m_VehicleBlockedLaneLookup.HasComponent(violationLaneEntity))
                        {
                            var carLane = m_CarLaneLookup[violationLaneEntity];
                            
                            // Store original flags and set blocking flags
                            var blockedComponent = new VehicleBlockedLane
                            {
                                OriginalFlags = carLane.m_Flags,
                                UnblockAtFrame = m_CurrentFrame + 120, // 2 seconds
                                BlockedForVehicle = entity,
                                VehicleGroup = vehicleGroup
                            };
                            
                            // Apply blocking flags based on vehicle type
                            Game.Net.CarLaneFlags blockingFlags = GetBlockingFlagsForVehicle(vehicleGroup);
                            carLane.m_Flags |= blockingFlags;
                            m_CarLaneLookup[violationLaneEntity] = carLane;
                            
                            // Add tracking component
                            m_Ecb.AddComponent(unfilteredChunkIndex, violationLaneEntity, blockedComponent);
                            
                            VehicleDebugLogger.Log(entity,
                                $"TollboothPathMonitoring: BLOCKING lane {violationLaneEntity.Index} with flags {blockingFlags}");
                        }

                        // Mark path obsolete
                        pathOwner.m_State |= PathFlags.Obsolete;
                        pathOwners[i] = pathOwner;

                        if (hasRepathAttempts)
                        {
                            repathAttempts[i] = attempts;
                        }
                        else
                        {
                            m_Ecb.AddComponent(unfilteredChunkIndex, entity, attempts);
                        }

                        VehicleDebugLogger.Log(entity,
                            $"TollboothPathMonitoring: INVALIDATING path (attempt {attempts.AttemptCount}/10)");
                    }
                    else if (hasRepathAttempts && attempts.AttemptCount > 0)
                    {
                        VehicleDebugLogger.Log(entity,
                            $"TollboothPathMonitoring: SUCCESS - Valid path found after {attempts.AttemptCount} attempts");
                        
                        attempts.AttemptCount = 0;
                        attempts.LastValidatedElementCount = 0;
                        repathAttempts[i] = attempts;
                    }
                }
            }

            private CarLaneFlags GetBlockingFlagsForVehicle(VehicleGroup vehicleGroup)
            {
                // Apply opposite flags to force pathfinder to avoid this lane
                switch (vehicleGroup)
                {
                    case VehicleGroup.PrivateTransport:
                        // Block private cars by making it unsafe or forbidden
                        return CarLaneFlags.Unsafe | CarLaneFlags.ForbidCombustionEngines;
                        
                    case VehicleGroup.Trucks:
                        // Block trucks
                        return CarLaneFlags.ForbidHeavyTraffic;
                        
                    case VehicleGroup.PublicTransport:
                        // Block public transport
                        return CarLaneFlags.ForbidTransitTraffic;
                        
                    case VehicleGroup.ServiceVehicles:
                        // Service vehicles are harder to block, use Unsafe
                        return CarLaneFlags.Unsafe;
                        
                    default:
                        return CarLaneFlags.Unsafe;
                }
            }

            private VehicleGroup DetermineVehicleType(Entity vehicleEntity)
            {
                // [Same implementation as before]
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

                if (m_PublicTransportVehicleLookup.HasComponent(vehicleEntity) ||
                    m_TaxiLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PublicTransport;
                }

                if (m_DeliveryTruckLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.Trucks;
                }

                if (m_PersonalCarLookup.HasComponent(vehicleEntity) ||
                    m_MotorbikeLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PrivateTransport;
                }

                return VehicleGroup.PrivateTransport;
            }

            private bool CheckVehicleAllowedOnTollbooth(VehicleGroup vehicleGroup, Entity roadEntity, CarLane carLane)
            {
                // [Same implementation as before]
                if (m_PrivateTransportLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.PrivateTransport;
                }

                if (m_TruckLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.Trucks;
                }

                if (m_PublicTransportLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.PublicTransport;
                }

                if (m_ServiceVehiclesLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.ServiceVehicles;
                }

                return true;
            }
        }

        private void EnsureLogger()
        {
            if (m_LogInitialized)
                return;

            try
            {
                VehicleDebugLogger.Init();
                VehicleDebugLogger.LogOnce("=== TollboothPathMonitoringSystem with lane blocking started ===");
            }
            catch { }

            m_LogInitialized = true;
        }
    }
}