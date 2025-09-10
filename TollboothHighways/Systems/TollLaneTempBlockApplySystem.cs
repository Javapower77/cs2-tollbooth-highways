using Game;
using Game.Net;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Mirrors TollLaneTempBlocked -> Blocked (if engine respects Blocked).
    /// </summary>
    public partial class TollLaneTempBlockApplySystem : GameSystemBase
    {
        private EntityQuery m_Query;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Query = GetEntityQuery(
                ComponentType.ReadOnly<TollLaneTempBlocked>(),
                ComponentType.Exclude<Blocked>());
        }

        protected override void OnUpdate()
        {
            if (m_Query.IsEmpty) return;
            using var lanes = m_Query.ToEntityArray(Allocator.Temp);
            foreach (var lane in lanes)
            {
                EntityManager.AddComponent<Blocked>(lane);
            }

            // Cleanup: remove Blocked from lanes no longer temp-blocked
            var unblockQuery = GetEntityQuery(
                ComponentType.ReadOnly<Blocked>(),
                ComponentType.Exclude<TollLaneTempBlocked>());
            if (!unblockQuery.IsEmpty)
            {
                using var ub = unblockQuery.ToEntityArray(Allocator.Temp);
                foreach (var l in ub)
                    EntityManager.RemoveComponent<Blocked>(l);
            }
        }
    }
}