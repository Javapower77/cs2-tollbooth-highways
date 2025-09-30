using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;
using Game.Net;

namespace TollboothHighways.Systems
{
    // Restores original lane flags after repath is completed (simple immediate restore next frame)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TollboothRepathSystem))]
    public partial class RestoreOriginalLaneFlagsSystem : SystemBase
    {
        private EntityQuery _modifiedLanesQuery;

        protected override void OnCreate()
        {
            _modifiedLanesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<OriginalCarLaneFlags>(),
                    ComponentType.ReadWrite<CarLane>()
                }
            });
            RequireForUpdate(_modifiedLanesQuery);
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            using var lanes = _modifiedLanesQuery.ToEntityArray(Allocator.Temp);
            foreach (var lane in lanes)
            {
                if (!em.HasComponent<CarLane>(lane) || !em.HasComponent<OriginalCarLaneFlags>(lane)) continue;
                LogUtil.Info($"Restoring original lane flags for lane entity {lane.Index}, {lane.Version}");
                var orig = em.GetComponentData<OriginalCarLaneFlags>(lane);
                var data = em.GetComponentData<CarLane>(lane);
                LogUtil.Info($"  Original flags: {orig.Value}, Current flags: {(uint)data.m_Flags}");
                data.m_Flags = (CarLaneFlags)orig.Value;
                data.m_BlockageEnd = 0; // restore cost
                data.m_BlockageStart = 0; // restore cost
                data.m_LaneCrossCount = 0; // restore cost
                em.SetComponentData(lane, data);
                em.RemoveComponent<OriginalCarLaneFlags>(lane);
                LogUtil.Info($"  Restored flags: {(uint)data.m_Flags}");
            }
        }
    }
}
