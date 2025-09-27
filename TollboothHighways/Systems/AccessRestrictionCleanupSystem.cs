using Game;
using Game.Net;
using Game.Simulation;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;


namespace TollboothHighways.Systems
{
    /// <summary>
    /// System responsible for cleaning up temporary access restrictions after pathfinding completes.
    /// </summary>
    public partial class AccessRestrictionCleanupSystem : GameSystemBase
    {
        private EndFrameBarrier m_EndFrameBarrier;
        private EntityQuery m_CleanupQuery;
        SimulationSystem simulationSystem;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            
            m_CleanupQuery = GetEntityQuery(ComponentType.ReadOnly<CleanupRestrictions>());
        }

        protected override void OnUpdate()
        {
            if (m_CleanupQuery.IsEmpty)
                return;

            var currentFrame = this.simulationSystem.frameIndex;

            var job = new CleanupRestrictionsJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                CleanupRestrictionsType = SystemAPI.GetComponentTypeHandle<CleanupRestrictions>(true),
                CarLaneData = SystemAPI.GetComponentLookup<CarLane>(),
                CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                CurrentFrame = currentFrame
            };

            var jobHandle = job.ScheduleParallel(m_CleanupQuery, Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
            Dependency = jobHandle;
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct CleanupRestrictionsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CleanupRestrictions> CleanupRestrictionsType;
            
            public ComponentLookup<CarLane> CarLaneData;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            [ReadOnly] public uint CurrentFrame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var cleanupComponents = chunk.GetNativeArray(ref CleanupRestrictionsType);

                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var cleanup = cleanupComponents[i];

                    // Check if it's time to process cleanup
                    if (CurrentFrame >= cleanup.ProcessingFrame)
                    {
                        // Note: In a full implementation, you would need to store the mapping
                        // of which lanes were restricted and their original values.
                        // For simplicity, this example assumes the cleanup is handled elsewhere
                        // or that the restrictions are temporary and will be reset by the pathfinding system.
                        
                        // Remove the cleanup component
                        CommandBuffer.RemoveComponent<CleanupRestrictions>(unfilteredChunkIndex, entity);
                    }
                }
            }
        }
    }
}