using Game;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// (Optional) Safety net: kicks non-PersonalCar vehicles off manual toll lanes by forcing a re-path
/// if they are currently navigating on such a lane.
/// Replace 'CarNavigation' adjustments with the actual re-path trigger used by CS2 if different.
/// </summary>
namespace TollboothHighways.Systems
{
    public partial class EnforceManualTollLaneUsageSystem : GameSystemBase
    {
        private EntityQuery _carQuery;
        private ComponentLookup<CarCurrentLane> _laneLookup;
        private ComponentLookup<CarNavigation> _navLookup;
        private ComponentLookup<ManualTollLaneTag> _manualLaneLookup;
        private ComponentLookup<PersonalCar> _personalCarLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            _carQuery = GetEntityQuery(
                ComponentType.ReadOnly<Car>(),
                ComponentType.ReadOnly<CarCurrentLane>(),
                ComponentType.ReadWrite<CarNavigation>());

            _laneLookup        = GetComponentLookup<CarCurrentLane>(true);
            _navLookup         = GetComponentLookup<CarNavigation>(false);
            _manualLaneLookup  = GetComponentLookup<ManualTollLaneTag>(true);
            _personalCarLookup = GetComponentLookup<PersonalCar>(true);

            RequireForUpdate(_carQuery);
        }

        protected override void OnUpdate()
        {
            _laneLookup.Update(this);
            _navLookup.Update(this);
            _manualLaneLookup.Update(this);
            _personalCarLookup.Update(this);

            using var cars = _carQuery.ToEntityArray(Allocator.Temp);
            foreach (var car in cars)
            {
                // Skip personal cars
                if (_personalCarLookup.HasComponent(car))
                    continue;

                if (!_laneLookup.TryGetComponent(car, out var currentLane))
                    continue;

                if (currentLane.m_Lane == Entity.Null)
                    continue;

                // If lane tagged, force re-path by clearing target (or adjust as needed)
                if (_manualLaneLookup.HasComponent(currentLane.m_Lane))
                {
                    if (_navLookup.TryGetComponent(car, out var nav))
                    {
                        // Simplest: move target a little forward so the internal system picks new lane
                        nav.m_TargetPosition += new float3(2f, 0f, 2f);
                        // Reduce max speed momentarily to lessen jitter
                        nav.m_MaxSpeed = math.min(nav.m_MaxSpeed, 2f);
                        _navLookup[car] = nav;
                    }
                }
            }
        }
    }
}