using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CarLaneFlags = Game.Net.CarLaneFlags;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Ref-count version (replaces simple toggle) to avoid flicker when mixed vehicle categories
    /// share a toll lane. Each frame:
    ///  1. Count disallowed vehicles per lane (based on TollLaneAllowedMask & VehicleCategoryData).
    ///  2. Apply CarLaneFlags.Forbidden only if count > 0.
    ///  3. Clear flag when count returns to 0.
    /// Uses a per-lane TollLaneRestrictionState component to remember last applied count.
    /// </summary>
    [BurstCompile]
    public partial class TollLanePathPruneRefCountSystem : GameSystemBase
    {
        private EntityQuery _vehicleQuery;
        private EntityQuery _laneQuery;

        private ComponentLookup<CarLane> _carLaneLookup;
        private ComponentLookup<TollLaneAllowedMask> _allowedMaskLookup;
        private ComponentLookup<TollLaneRestrictionState> _restrictionStateLookup;
        private ComponentLookup<CarCurrentLane> _currentLaneLookup;
        private ComponentLookup<VehicleCategoryData> _vehicleCategoryLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            _vehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadOnly<VehicleCategoryData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            _laneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CarLane>(),
                    ComponentType.ReadOnly<TollLaneAllowedMask>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            _carLaneLookup          = GetComponentLookup<CarLane>();
            _allowedMaskLookup      = GetComponentLookup<TollLaneAllowedMask>(true);
            _restrictionStateLookup = GetComponentLookup<TollLaneRestrictionState>();
            _currentLaneLookup      = GetComponentLookup<CarCurrentLane>(true);
            _vehicleCategoryLookup  = GetComponentLookup<VehicleCategoryData>(true);

            RequireForUpdate(_vehicleQuery);
            RequireForUpdate(_laneQuery);
        }

        protected override void OnUpdate()
        {
            _carLaneLookup.Update(this);
            _allowedMaskLookup.Update(this);
            _restrictionStateLookup.Update(this);
            _currentLaneLookup.Update(this);
            _vehicleCategoryLookup.Update(this);

            var em = EntityManager;

            // Prepare counting structure sized to (#lanes or #vehicles) whichever is smaller heuristic
            int estimated = math.max(16, math.min(_laneQuery.CalculateEntityCount(), _vehicleQuery.CalculateEntityCount()));
            var disallowedCounts = new NativeParallelHashMap<Entity, int>(estimated, Allocator.Temp);

            // Pass 1: Count disallowed vehicles per lane
            var vehicles = _vehicleQuery.ToEntityArray(Allocator.Temp);
            foreach (var vehicle in vehicles)
            {
                var laneEntity = _currentLaneLookup[vehicle].m_Lane;
                if (laneEntity == Entity.Null)
                    continue;
                if (!_allowedMaskLookup.HasComponent(laneEntity))
                    continue; // Not a toll lane
                if (!_vehicleCategoryLookup.HasComponent(vehicle))
                    continue;

                byte laneAllowed = _allowedMaskLookup[laneEntity].Mask;
                byte vehicleMask = _vehicleCategoryLookup[vehicle].Mask;

                bool permitted = (laneAllowed & vehicleMask) != 0;
                if (permitted)
                    continue;

                if (disallowedCounts.TryGetValue(laneEntity, out int current))
                {
                    disallowedCounts[laneEntity] = current + 1;
                }
                else
                {
                    disallowedCounts.Add(laneEntity, 1);
                }
            }
            vehicles.Dispose();

            // Pass 2: Apply flags & update state (iterate all toll lanes to clear those with zero)
            var lanes = _laneQuery.ToEntityArray(Allocator.Temp);
            foreach (var laneEntity in lanes)
            {
                if (!_carLaneLookup.HasComponent(laneEntity))
                    continue;

                int newCount = disallowedCounts.TryGetValue(laneEntity, out int c) ? c : 0;

                // Ensure state component exists
                TollLaneRestrictionState state;
                bool hasState = _restrictionStateLookup.HasComponent(laneEntity);
                if (hasState)
                {
                    state = _restrictionStateLookup[laneEntity];
                }
                else
                {
                    state = new TollLaneRestrictionState { DisallowedCount = -1 }; // force initial apply
                }

                if (state.DisallowedCount != newCount)
                {
                    // Update CarLane forbidden flag
                    var carLane = _carLaneLookup[laneEntity];
                    bool currentlyForbidden = (carLane.m_Flags & CarLaneFlags.Forbidden) != 0;

                    if (newCount > 0 && !currentlyForbidden)
                    {
                        carLane.m_Flags |= CarLaneFlags.Forbidden;
                        _carLaneLookup[laneEntity] = carLane;
                    }
                    else if (newCount == 0 && currentlyForbidden)
                    {
                        carLane.m_Flags &= ~CarLaneFlags.Forbidden;
                        _carLaneLookup[laneEntity] = carLane;
                    }

                    state.DisallowedCount = newCount;
                    if (hasState)
                    {
                        _restrictionStateLookup[laneEntity] = state;
                    }
                    else
                    {
                        em.AddComponentData(laneEntity, state);
                    }
                }
            }
            lanes.Dispose();
            disallowedCounts.Dispose();
        }
    }
}