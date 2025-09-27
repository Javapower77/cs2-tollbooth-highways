using Game;
using Game.Common;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System to clean up RepathCreated components from vehicles that are no longer active or have completed their journey.
    /// </summary>
    public partial class RepathCreatedCleanupSystem : GameSystemBase
    {
        private EndFrameBarrier m_EndFrameBarrier;
        private EntityQuery m_RepathCreatedQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            
            // Query for vehicles with RepathCreated that should be cleaned up
            m_RepathCreatedQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<RepathCreated>()
                },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (m_RepathCreatedQuery.IsEmpty)
                return;

            var job = new CleanupRepathCreatedJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
            };

            var jobHandle = job.ScheduleParallel(m_RepathCreatedQuery, Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle;
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct CleanupRepathCreatedJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);

                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    CommandBuffer.RemoveComponent<RepathCreated>(unfilteredChunkIndex, entity);
                }
            }
        }
    }
}