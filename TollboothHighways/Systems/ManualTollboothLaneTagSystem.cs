using Game.Net;
using Game.Objects;
using Game.Common;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Game;
using SubLane = Game.Net.SubLane;

/// <summary>
/// Discovers lanes that belong to roads which are associated to MANUAL tollbooths
/// (i.e. have TollBoothManualData or a TollRoadPrefabData whose AssociatedTollbooth has TollBoothManualData)
/// and tags those lanes so pathfinding can treat them differently.
/// 
/// Runs after TollBoothSpawnSystem so association & manual data exist.
/// </summary>
namespace TollboothHighways.Systems
{
    public partial class ManualTollboothLaneTagSystem : GameSystemBase
    {
        private EntityQuery _manualRoadQuery;
        private BufferLookup<SubLane> _subLaneLookup;
        private ComponentLookup<TollBoothManualData> _manualDataLookup;
        private ComponentLookup<TollRoadPrefabData> _tollRoadLookup;
        private ComponentLookup<Owner> _ownerLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            _manualRoadQuery = GetEntityQuery(
                ComponentType.ReadOnly<Road>(),
                ComponentType.ReadOnly<TollRoadPrefabData>()); // filter roads that have tollroad data (manual subset resolved inside)

            _subLaneLookup      = GetBufferLookup<SubLane>(true);
            _manualDataLookup   = GetComponentLookup<TollBoothManualData>(true);
            _tollRoadLookup     = GetComponentLookup<TollRoadPrefabData>(true);
            _ownerLookup        = GetComponentLookup<Owner>(true);

            // We only act when roads with toll data exist.
            RequireForUpdate(_manualRoadQuery);
        }

        protected override void OnUpdate()
        {
            _subLaneLookup.Update(this);
            _manualDataLookup.Update(this);
            _tollRoadLookup.Update(this);
            _ownerLookup.Update(this);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Iterate candidate roads
            using var roads = _manualRoadQuery.ToEntityArray(Allocator.Temp);
            foreach (var road in roads)
            {
                if (!_tollRoadLookup.TryGetComponent(road, out var tollRoadData))
                    continue;

                if (tollRoadData.AssociatedTollbooth == Entity.Null)
                    continue;

                // Only manual tollbooths
                if (!_manualDataLookup.HasComponent(tollRoadData.AssociatedTollbooth))
                    continue;

                // Tag each sub-lane entity
                if (_subLaneLookup.TryGetBuffer(road, out var subLanes))
                {
                    for (int i = 0; i < subLanes.Length; i++)
                    {
                        var laneEnt = subLanes[i].m_SubLane;
                        if (!EntityManager.Exists(laneEnt))
                            continue;

                        if (!EntityManager.HasComponent<ManualTollLaneTag>(laneEnt))
                        {
                            ecb.AddComponent<ManualTollLaneTag>(laneEnt);

                            // (Optional) Provide default cost struct – tune constants as desired.
                            if (!EntityManager.HasComponent<LanePersonalCarCost>(laneEnt))
                            {
                                ecb.AddComponent(laneEnt, new LanePersonalCarCost
                                {
                                    PersonalCarAdditionalCost = 5,      // modest extra cost (or 0)
                                    NonPersonalAdditionalCost = 50000   // huge to make pathfinder avoid
                                });
                            }
                        }
                    }
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}