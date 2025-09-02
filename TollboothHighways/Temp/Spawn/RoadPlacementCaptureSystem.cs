using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Captures unprocessed road edges (independent of 'Created' timing)
    /// and tags them for later toll processing.
    /// </summary>
    public partial class RoadPlacementCaptureSystem : GameSystemBase
    {
        private EntityQuery m_UnprocessedEdges;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Any edge with a prefab, not deleted/temp, not yet processed or queued.
            m_UnprocessedEdges = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<TollNewRoadTag>(),
                    ComponentType.ReadOnly<TollProcessedRoadTag>()
                }
            });

            LogUtil.Info("RoadPlacementCaptureSystem: Created (no longer depends on 'Created' tag).");
        }

        protected override void OnUpdate()
        {
            if (m_UnprocessedEdges.IsEmpty)
                return;

            var entities = m_UnprocessedEdges.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                int tagged = 0;
                foreach (var e in entities)
                {
                    ecb.AddComponent<TollNewRoadTag>(e);
                    tagged++;
                }

                if (tagged > 0)
                {
                    LogUtil.Info($"RoadPlacementCaptureSystem: Tagged {tagged} road edge(s) with TollNewRoadTag.");
                }

                ecb.Playback(EntityManager);
            }
            finally
            {
                ecb.Dispose();
                entities.Dispose();
            }
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 8; // scan every 8 frames (tune as desired)
    }
}