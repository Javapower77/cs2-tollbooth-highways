using System.ComponentModel;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
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
using ReadOnlyAttribute = Unity.Collections.ReadOnlyAttribute;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Enforces CarLane flags on tollbooth roads to ensure vehicle type restrictions.
    /// Uses SystemAPI for lookup management following ECS best practices.
    /// </summary>
    public partial class TollBoothLaneFlagEnforcementSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private JobLogger m_JobLogger;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Initialize the JobLogger with TempJob allocator
            // TempJob is appropriate for data that lives only during job execution
            if (Mod.Settings?.EnableJobsLogging == true)
                m_JobLogger.Initialize(Allocator.TempJob);

            // Query for toll roads that haven't been processed yet
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Net.Edge>(),
                    ComponentType.ReadOnly<Game.Net.SubLane>(),
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
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Created successfully with SystemAPI lookup management");
        }

        protected override void OnUpdate()
        {
            // Early exit if no roads to process
            if (m_TollRoadQuery.IsEmptyIgnoreFilter)
                return;

            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: OnUpdate started - m_TollRoadQuery is empty: " + m_TollRoadQuery.IsEmptyIgnoreFilter);

            // Command-buffer-free pattern by collecting the roads that need the TollRoadCarLaneApplied
            var roadsToMark = new NativeQueue<Entity>(Allocator.TempJob);

            // Create job using SystemAPI for all lookups and type handles
            var applyFlagsJob = new ApplyTollLaneFlagsJob
            {
                // Use SystemAPI to get all required type handles and lookups
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                SubLaneBufferHandle = SystemAPI.GetBufferTypeHandle<Game.Net.SubLane>(true),

                // ComponentTypeHandles for chunk.Has() checks (read-only)
                PrivateTransportTypeHandle = SystemAPI.GetComponentTypeHandle<TollRoadPrivateTransportData>(true),
                TruckTypeHandle = SystemAPI.GetComponentTypeHandle<TollRoadTruckData>(true),
                PublicTransportTypeHandle = SystemAPI.GetComponentTypeHandle<TollRoadPublicTransportData>(true),
                ServiceVehiclesTypeHandle = SystemAPI.GetComponentTypeHandle<TollRoadServiceVehiclesData>(true),

                // ComponentLookup for lane modifications
                CarLaneLookup = SystemAPI.GetComponentLookup<Game.Net.CarLane>(false),

                // Roads that need the TollRoadCarLaneApplied component
                RoadsToMark = roadsToMark.AsParallelWriter(),

                // Pass the JobLogger writer to the job
                Logger = Mod.Settings?.EnableJobsLogging == true ? m_JobLogger.GetWriter() : default
            };

            // Schedule the Burst-compiled job
            Dependency = applyFlagsJob.ScheduleParallel(m_TollRoadQuery, Dependency);
            Dependency.Complete();

            // Tag in a thread-safe queue and applying the component on the main thread after the job completes
            while (roadsToMark.TryDequeue(out var road))
            {
                if (!EntityManager.HasComponent<TollRoadCarLaneApplied>(road))
                    EntityManager.AddComponent<TollRoadCarLaneApplied>(road);
            }
            roadsToMark.Dispose();

            // Flush all collected log messages to LogUtil
            if (Mod.Settings?.EnableJobsLogging == true)
               m_JobLogger.Flush();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Mod.Settings?.EnableJobsLogging == true)
                m_JobLogger.Dispose();
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Destroyed");
        }

        /// <summary>
        /// Burst-compiled job to apply CarLane flags to toll road lanes.
        /// Uses BufferTypeHandle and ComponentTypeHandle with chunk.Has() for optimal performance.
        /// </summary>
#if WITH_BURST
        [BurstCompile]
#endif
        private struct ApplyTollLaneFlagsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<Game.Net.SubLane> SubLaneBufferHandle;

            // ComponentTypeHandles for chunk.Has() checks
            [ReadOnly] public ComponentTypeHandle<TollRoadPrivateTransportData> PrivateTransportTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TollRoadTruckData> TruckTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TollRoadPublicTransportData> PublicTransportTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TollRoadServiceVehiclesData> ServiceVehiclesTypeHandle;

            public JobLogger.Writer Logger;
            public NativeQueue<Entity>.ParallelWriter RoadsToMark;

            // ComponentLookup for modifying lane data
            public ComponentLookup<Game.Net.CarLane> CarLaneLookup;

            // Command buffer for adding components
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // Determine toll type for this chunk using chunk.Has()
                bool hasValidTollType = false;
                bool IsPublicTollRoad = false;
                CarLaneFlags flagsToApply;

                if (chunk.Has(ref PrivateTransportTypeHandle))
                {
                    // Private Transport: Block heavy vehicles (trucks)
                    flagsToApply = CarLaneFlags.ForbidHeavyTraffic;
                    hasValidTollType = true;
                }
                else if (chunk.Has(ref TruckTypeHandle))
                {
                    // Trucks Only: Block transit traffic (private cars, buses)
                    flagsToApply = CarLaneFlags.ForbidTransitTraffic;
                    hasValidTollType = true;
                }
                else if (chunk.Has(ref PublicTransportTypeHandle))
                {
                    // Public Transport Only: Allow only public transport vehicles
                    flagsToApply = CarLaneFlags.PublicOnly;
                    IsPublicTollRoad = true;
                    hasValidTollType = true;
                }
                else if (chunk.Has(ref ServiceVehiclesTypeHandle))
                {
                    // Service Vehicles Only: Block both transit and heavy traffic
                    flagsToApply = CarLaneFlags.ForbidTransitTraffic | CarLaneFlags.ForbidHeavyTraffic;
                    hasValidTollType = true;
                }
                else
                {
                    // No recognized toll type - should not happen due to query but handle gracefully
                    return;
                }

                if (!hasValidTollType)
                    return;

                // Get entities and sublane buffers for this chunk
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var subLaneAccessor = chunk.GetBufferAccessor(ref SubLaneBufferHandle);

                if (Mod.Settings?.EnableJobsLogging == true)
                {
                    FixedString4096Bytes message = $"Processing chunk {unfilteredChunkIndex} with {entities.Length} roads";
                    Logger.Log(message);
                }
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity roadEntity = entities[i];
                    DynamicBuffer<Game.Net.SubLane> subLanes = subLaneAccessor[i];

                    bool appliedAnyFlags = false;

                    // Iterate through all sublanes
                    for (int j = 0; j < subLanes.Length; j++)
                    {
                        var subLane = subLanes[j];

                        // Only process road lanes (skip pedestrian, track, etc.)
                        if ((subLane.m_PathMethods & PathMethod.Road) == 0)
                            continue;

                        Entity laneEntity = subLane.m_SubLane;

                        // Check if lane has CarLane component
                        if (!CarLaneLookup.TryGetComponent(laneEntity, out var carLane))
                            continue;

                        // Store original flags for comparison
                        var originalFlags = carLane.m_Flags;

                        // Apply restriction flags (preserve existing flags using |=)
                        carLane.m_Flags |= flagsToApply;

                        // Only write if flags actually changed
                        if (carLane.m_Flags != originalFlags)
                        {
                            CarLaneLookup[laneEntity] = carLane;
                            if (Mod.Settings?.EnableJobsLogging == true)
                            {
                                FixedString4096Bytes message = $"Road {roadEntity.Index} (Lane {laneEntity.Index}): Applied flags {flagsToApply}. OriginalFlags={originalFlags}, NewFlags={carLane.m_Flags}";
                                Logger.Log(message);
                            }
                            appliedAnyFlags = true;
                        }
                    }

                    // Mark road as processed to prevent reprocessing
                    // If it is a public toll road, we add the component anyway to avoid reprocessing
                    if (appliedAnyFlags || IsPublicTollRoad)
                    {
                        RoadsToMark.Enqueue(roadEntity);
                    }

                    if (Mod.Settings?.EnableJobsLogging == true)
                    {
                        FixedString4096Bytes message = $"Completed chunk {unfilteredChunkIndex}";
                        Logger.Log(message);
                    }
                }
            }
        }
    }
}