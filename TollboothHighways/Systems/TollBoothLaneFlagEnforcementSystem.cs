using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Enforces CarLane flags on tollbooth roads to ensure vehicle type restrictions.
    /// </summary>
    public partial class TollBoothLaneFlagEnforcementSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private EndSimulationEntityCommandBufferSystem m_CommandBufferSystem;
        private EntityTypeHandle m_EntityTypeHandle;
        private BufferLookup<Game.Net.SubLane> m_SubLaneData;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Get command buffer system for adding components
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            // Initialize type handle
            m_EntityTypeHandle = GetEntityTypeHandle();

            // Query for toll roads that haven't been processed yet
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Net.Edge>()
                },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<TollRoadPrivateTransportData>(),
                    ComponentType.ReadOnly<TollRoadTruckData>(),
                    ComponentType.ReadOnly<TollRoadPublicTransportData>(),
                    ComponentType.ReadOnly<TollRoadServiceVehiclesData>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<TollRoadCarLaneApplied>()
                }
            });

            RequireForUpdate(m_TollRoadQuery);
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Created successfully");
        }

        protected override void OnUpdate()
        {
            // Early exit if no roads to process
            if (m_TollRoadQuery.IsEmptyIgnoreFilter)
                return;

            // Update all lookups and handles
            m_EntityTypeHandle.Update(this);
            m_SubLaneData.Update(this);
            m_CarLaneLookup.Update(this);
            m_PrivateTransportLookup.Update(this);
            m_TruckLookup.Update(this);
            m_PublicTransportLookup.Update(this);
            m_ServiceVehiclesLookup.Update(this);

            // Create command buffer
            var ecb = m_CommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            // Schedule Burst job using IJobChunk for better control
            var applyFlagsJob = new ApplyTollLaneFlagsJob
            {
                EntityTypeHandle = m_EntityTypeHandle,
                SubLaneData = m_SubLaneData,
                CarLaneLookup = m_CarLaneLookup,
                PrivateTransportLookup = m_PrivateTransportLookup,
                TruckLookup = m_TruckLookup,
                PublicTransportLookup = m_PublicTransportLookup,
                ServiceVehiclesLookup = m_ServiceVehiclesLookup,
                ECB = ecb
            };

            var jobHandle = applyFlagsJob.ScheduleParallel(m_TollRoadQuery, Dependency);
            m_CommandBufferSystem.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Destroyed");
        }

        /// <summary>
        /// Burst-compiled job to apply CarLane flags to toll road lanes.
        /// Uses IJobChunk for explicit chunk iteration and better ECB handling.
        /// </summary>
#if WITH_BURST
        [BurstCompile]
#endif
        private struct ApplyTollLaneFlagsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferLookup<Game.Net.SubLane> SubLaneData;
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Net.CarLane> CarLaneLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceVehiclesLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity roadEntity = entities[i];
                    CarLaneFlags flagsToApply;
                    
                    // Determine toll type and corresponding flags
                    if (PrivateTransportLookup.HasComponent(roadEntity))
                    {
                        flagsToApply = CarLaneFlags.ForbidHeavyTraffic;
                    }
                    else if (TruckLookup.HasComponent(roadEntity))
                    {
                        flagsToApply = CarLaneFlags.ForbidTransitTraffic;
                    }
                    else if (PublicTransportLookup.HasComponent(roadEntity))
                    {
                        flagsToApply = CarLaneFlags.PublicOnly;
                    }
                    else if (ServiceVehiclesLookup.HasComponent(roadEntity))
                    {
                        flagsToApply = CarLaneFlags.ForbidTransitTraffic | CarLaneFlags.ForbidHeavyTraffic;
                    }
                    else
                    {
                        // No recognized toll type - should not happen due to query
                        continue;
                    }

                    // Apply flags to all road sublanes
                    if (!SubLaneData.TryGetBuffer(roadEntity, out var subLanes))
                        continue;

                    bool appliedAnyFlags = false;

                    for (int j = 0; j < subLanes.Length; j++)
                    {
                        var subLane = subLanes[j];
                        
                        // Only process road lanes
                        if ((subLane.m_PathMethods & PathMethod.Road) == 0)
                            continue;

                        Entity laneEntity = subLane.m_SubLane;

                        // Check if lane has CarLane component
                        if (!CarLaneLookup.TryGetComponent(laneEntity, out var carLane))
                            continue;

                        // Apply flags (using |= to preserve existing flags)
                        var originalFlags = carLane.m_Flags;
                        carLane.m_Flags |= flagsToApply;
                        
                        // Only write if flags actually changed
                        if (carLane.m_Flags != originalFlags)
                        {
                            CarLaneLookup[laneEntity] = carLane;
                            appliedAnyFlags = true;
                        }
                    }

                    // Mark road as processed using ECB
                    if (appliedAnyFlags)
                    {
                        ECB.AddComponent<TollRoadCarLaneApplied>(unfilteredChunkIndex, roadEntity);
                    }
                }
            }
        }
    }
}