using Game;
using Game.Common;
using Game.Tools;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Cycles through vehicle categories (Private, Transit, Heavy, Service).
    /// For each frame's category, block toll lanes that do NOT allow that category via TollLaneTempBlocked.
    /// Pathfinding for that category then naturally excludes those lanes.
    /// </summary>
    public partial class TollLaneCategoryFilterSystem : GameSystemBase
    {
        private EntityQuery m_TollLanes;
        private int m_CategoryIndex; // 0..3

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TollLanes = GetEntityQuery(
                ComponentType.ReadOnly<TollLaneAllowedMask>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());

            RequireForUpdate(m_TollLanes);
        }

        protected override void OnUpdate()
        {
            // Remove previous frame temp blocked
            {
                var blockedQuery = GetEntityQuery(ComponentType.ReadOnly<TollLaneTempBlocked>());
                EntityManager.RemoveComponent<TollLaneTempBlocked>(blockedQuery);
            }

            byte bit = (byte)(1 << m_CategoryIndex); // 0->1,1->2,2->4,3->8

            using var lanes = m_TollLanes.ToEntityArray(Allocator.Temp);
            using var masks = m_TollLanes.ToComponentDataArray<TollLaneAllowedMask>(Allocator.Temp);

            for (int i = 0; i < lanes.Length; i++)
            {
                if ((masks[i].Mask & bit) == 0)
                {
                    // Lane does not allow current category => block
                    if (!EntityManager.HasComponent<TollLaneTempBlocked>(lanes[i]))
                        EntityManager.AddComponent<TollLaneTempBlocked>(lanes[i]);
                }
            }

            m_CategoryIndex = (m_CategoryIndex + 1) & 0x3; // cycle 0..3
        }
    }
}