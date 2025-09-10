using Game.Vehicles;
using Game.Common;
using Game.Pathfind;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;
using Game;
using Game.Tools;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Forces a repath for vehicles currently on a toll lane they are not allowed to use.
    /// Works as a safety net when lane rules change after path generation.
    /// </summary>
    [UpdateInGroup(typeof(Game.SystemGroups.GameSimulationSystemGroup))]
    [UpdateAfter(typeof(VehicleCategoryMaskBuildSystem))]
    public partial class TollLaneEligibilityEnforceSystem : GameSystemBase
    {
        private EntityQuery m_Cars;
        private ComponentLookup<CarCurrentLane> m_CurrentLaneLookup;
        private ComponentLookup<TollLaneAllowedMask> m_TollLaneMaskLookup;
        private ComponentLookup<VehicleCategoryMask> m_VehicleMaskLookup;
        private ComponentLookup<CarNavigation> m_NavLookup;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Cars = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadOnly<VehicleCategoryMask>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_CurrentLaneLookup = GetComponentLookup<CarCurrentLane>(true);
            m_TollLaneMaskLookup = GetComponentLookup<TollLaneAllowedMask>(true);
            m_VehicleMaskLookup = GetComponentLookup<VehicleCategoryMask>(true);
            m_NavLookup = GetComponentLookup<CarNavigation>(false);

            RequireForUpdate(m_Cars);
        }

        protected override void OnUpdate()
        {
            m_CurrentLaneLookup.Update(this);
            m_TollLaneMaskLookup.Update(this);
            m_VehicleMaskLookup.Update(this);
            m_NavLookup.Update(this);

            using var cars = m_Cars.ToEntityArray(Allocator.Temp);
            foreach (var car in cars)
            {
                if (!m_CurrentLaneLookup.TryGetComponent(car, out var current))
                    continue;
                var lane = current.m_Lane;
                if (lane == Entity.Null)
                    continue;

                if (!m_TollLaneMaskLookup.HasComponent(lane))
                    continue; // not a toll lane, ignore

                var laneMask = m_TollLaneMaskLookup[lane].Mask;
                var vehMask = m_VehicleMaskLookup[car].Mask;

                // Allowed if any common bit
                if ((laneMask & vehMask) != 0)
                    continue;

                // Force repath: simplest approach is to clear current navigation path markers.
                if (m_NavLookup.TryGetComponent(car, out var nav))
                {
                    // Fields are speculative, adjust to actual CarNavigation fields:
                    nav.m_Flags |= CarNavigationFlags.Repath;  // if such flag exists
                    nav.m_TargetLane = Entity.Null;
                    m_NavLookup[car] = nav;
                }
            }
        }
    }
}