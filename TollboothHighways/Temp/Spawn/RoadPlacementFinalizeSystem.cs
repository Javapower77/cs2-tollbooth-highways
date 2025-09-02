using Colossal.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Stage 2: Finalize roads tagged with TollNewRoadTag once required data (SubLane buffer or geometry) exists.
    /// Adds TollRoadPrefabData if the prefab qualifies; removes TollNewRoadTag and adds TollProcessedRoadTag.
    /// </summary>
    public partial class RoadPlacementFinalizeSystem : GameSystemBase
    {
        private EntityQuery m_PendingFinalize;
        private ComponentLookup<PrefabRef> m_PrefabRefData;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadInfoData;
        private BufferLookup<Game.Net.SubLane> m_SubLanes;
        private ComponentLookup<EndNodeGeometry> m_EndNodeGeometry;
        private ComponentLookup<Edge> m_Edge;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_PrefabRefData = GetComponentLookup<PrefabRef>(true);
            m_TollRoadInfoData = GetComponentLookup<TollRoadPrefabData>(true);
            m_SubLanes = GetBufferLookup<Game.Net.SubLane>(true);
            m_EndNodeGeometry = GetComponentLookup<EndNodeGeometry>(true);
            m_Edge = GetComponentLookup<Edge>(true);

            m_PendingFinalize = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<TollNewRoadTag>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            LogUtil.Info("RoadPlacementFinalizeSystem: Created");
        }

        protected override void OnUpdate()
        {
            // Pseudocode:
            // 1. Update required lookups.
            // 2. Early exit if no pending edges.
            // 3. Gather entities & create ECB.
            // 4. Loop edges:
            //    a. Skip if no PrefabRef.
            //    b. If prefab not toll road -> remove TollNewRoadTag, add processed tag, continue.
            //    c. Wait until either SubLane buffer has data OR EndNodeGeometry exists.
            //    d. If TollRoadPrefabData missing -> add it.
            //    e. Call TollBoothSpawnSystem.AddManualBarrierControlToEdgeEnd(edge) to create barrier related components.
            //    f. Mark processed.
            // 5. Playback ECB & dispose.

            m_PrefabRefData.Update(this);
            m_TollRoadInfoData.Update(this);
            m_SubLanes.Update(this);
            m_EndNodeGeometry.Update(this);

            if (m_PendingFinalize.IsEmpty) return;

            var entities = m_PendingFinalize.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get (or create) the TollBoothSpawnSystem once per update
            var tollBoothSpawnSystem = World.GetOrCreateSystemManaged<TollBoothSpawnSystem>();

            try
            {
                foreach (var edge in entities)
                {
                    if (!m_PrefabRefData.HasComponent(edge))
                        continue;

                    var prefabRef = m_PrefabRefData[edge];



                    // Only finalize if prefab has TollRoadPrefabInfo
                    if (!m_TollRoadInfoData.HasComponent(prefabRef.m_Prefab))
                    {
                        // Not a toll road prefab; just mark processed so we don't re-check
                        ecb.RemoveComponent<TollNewRoadTag>(edge);
                        ecb.AddComponent<TollProcessedRoadTag>(edge);
                        continue;
                    }

                    // Wait until lanes or geometry exist for stable manipulation
                    bool hasSubLaneData = m_SubLanes.TryGetBuffer(edge, out var buffer) && buffer.Length > 0;
                    bool hasGeometry = m_EndNodeGeometry.HasComponent(edge);

                    if (!hasSubLaneData && !hasGeometry)
                    {
                        // Defer – keep TollNewRoadTag for next frame
                        continue;
                    }

                    bool newlyAddedTollRoadData = false;
                    if (!EntityManager.HasComponent<TollRoadPrefabData>(edge))
                    {
                        ecb.AddComponent(edge, new TollRoadPrefabData
                        {
                            AssociatedTollbooth = Entity.Null,
                            HasActiveTollbooth = false
                        });
                        newlyAddedTollRoadData = true;
                        LogUtil.Info($"RoadPlacementFinalizeSystem: Added TollRoadPrefabData to edge {edge.Index}");
                    }

                    // Invoke barrier control setup immediately (outside ECB as it performs direct structural changes)
                    // Safe because AddManualBarrierControlToEdgeEnd operates directly with EntityManager.
                    try
                    {
                        if (EntityManager.TryGetComponent<Game.Net.Edge>(edge, out var edgeEnd))
                        {
                            tollBoothSpawnSystem.AddManualBarrierControlToEdgeEnd(edgeEnd.m_End, edge);
                            LogUtil.Info($"RoadPlacementFinalizeSystem: Added manual barrier control components for edge end {edgeEnd.m_End.Index}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Warn($"RoadPlacementFinalizeSystem: Failed to add manual barrier control for edge end: {ex.Message}");
                    }

                    ecb.RemoveComponent<TollNewRoadTag>(edge);
                    ecb.AddComponent<TollProcessedRoadTag>(edge);
                }
            }
            finally
            {
                ecb.Playback(EntityManager);
                ecb.Dispose();
                entities.Dispose();
            }
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 4;
    }
}