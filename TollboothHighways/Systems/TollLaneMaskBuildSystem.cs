using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollRoadHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Creates/updates TollLaneAllowedMask + CarLaneFlags on toll road car sub-lanes.
    /// (Replaces VehicleGroup-only logic with bitmask compatible with VehicleCategoryData.)
    /// Bit meaning must match VehicleCategoryData.
    /// </summary>
    public partial class TollLaneMaskBuildSystem : GameSystemBase
    {
        private EntityQuery m_TollRoads;
        private BufferLookup<SubLane> m_SubLaneLookup;
        private ComponentLookup<CarLane> m_CarLaneLookup;
        private ComponentLookup<TollLaneAllowedMask> m_LaneMaskLookup;

        private const CarLaneFlags ManagedMask =
            CarLaneFlags.ForbidHeavyTraffic |
            CarLaneFlags.ForbidTransitTraffic |
            CarLaneFlags.PublicOnly;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TollRoads = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Road>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<TollRoadPrivateTransportData>(),
                    ComponentType.ReadOnly<TollRoadPublicTransportData>(),
                    ComponentType.ReadOnly<TollRoadTruckData>(),
                    ComponentType.ReadOnly<TollRoadServiceVehiclesData>(),
                    ComponentType.ReadOnly<TollRoadAllVehiclesData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            m_SubLaneLookup = GetBufferLookup<SubLane>(true);
            m_CarLaneLookup = GetComponentLookup<CarLane>();
            m_LaneMaskLookup = GetComponentLookup<TollLaneAllowedMask>();
            RequireForUpdate(m_TollRoads);
        }

        protected override void OnUpdate()
        {
            m_SubLaneLookup.Update(this);
            m_CarLaneLookup.Update(this);
            m_LaneMaskLookup.Update(this);

            var roads = m_TollRoads.ToEntityArray(Allocator.Temp);
            foreach (var road in roads)
            {
                if (!m_SubLaneLookup.HasBuffer(road))
                    continue;

                var group = ResolveGroup(road);
                // Build lane allowed mask & CarLaneFlags
                GetMaskAndFlags(group, out byte allowedMask, out CarLaneFlags desiredFlagBits);

                var subLanes = m_SubLaneLookup[road];
                foreach (var s in subLanes)
                {
                    var lane = s.m_SubLane;
                    if (!m_CarLaneLookup.HasComponent(lane))
                        continue;

                    var carLane = m_CarLaneLookup[lane];
                    var currentManaged = carLane.m_Flags & ManagedMask;
                    if (currentManaged != desiredFlagBits)
                    {
                        carLane.m_Flags = (carLane.m_Flags & ~ManagedMask) | desiredFlagBits;
                        m_CarLaneLookup[lane] = carLane;
                    }

                    if (m_LaneMaskLookup.HasComponent(lane))
                    {
                        var lm = m_LaneMaskLookup[lane];
                        if (lm.Mask != allowedMask)
                        {
                            lm.Mask = allowedMask;
                            EntityManager.SetComponentData(lane, lm);
                        }
                    }
                    else
                    {
                        EntityManager.AddComponentData(lane, new TollLaneAllowedMask { Mask = allowedMask });
                    }
                }
            }
            roads.Dispose();
        }

        private VehicleGroup ResolveGroup(Entity road)
        {
            if (EntityManager.HasComponent<TollRoadPrivateTransportData>(road)) return VehicleGroup.PrivateTransport;
            if (EntityManager.HasComponent<TollRoadPublicTransportData>(road)) return VehicleGroup.PublicTransport;
            if (EntityManager.HasComponent<TollRoadTruckData>(road)) return VehicleGroup.Trucks;
            if (EntityManager.HasComponent<TollRoadServiceVehiclesData>(road)) return VehicleGroup.ServiceVehicles;
            if (EntityManager.HasComponent<TollRoadAllVehiclesData>(road)) return VehicleGroup.All;
            return VehicleGroup.All;
        }

        // Builds both masks:
        // allowedMask bits: 1 Private, 2 Transit, 4 Heavy, 8 Service
        private static void GetMaskAndFlags(VehicleGroup group, out byte allowedMask, out CarLaneFlags flags)
        {
            switch (group)
            {
                case VehicleGroup.PrivateTransport:
                    // allow Private + Service
                    allowedMask = 0b0001 | 0b1000;
                    flags = CarLaneFlags.ForbidHeavyTraffic | CarLaneFlags.ForbidTransitTraffic;
                    return;
                case VehicleGroup.PublicTransport:
                    // allow Transit + Service
                    allowedMask = 0b0010 | 0b1000;
                    flags = CarLaneFlags.ForbidHeavyTraffic | CarLaneFlags.PublicOnly;
                    return;
                case VehicleGroup.Trucks:
                    // allow Heavy + Private + Service (exclude transit)
                    allowedMask = 0b0100 | 0b0001 | 0b1000;
                    flags = CarLaneFlags.ForbidTransitTraffic;
                    return;
                case VehicleGroup.ServiceVehicles:
                    // allow Service only
                    allowedMask = 0b1000;
                    flags = CarLaneFlags.ForbidHeavyTraffic | CarLaneFlags.ForbidTransitTraffic | CarLaneFlags.PublicOnly;
                    return;
                case VehicleGroup.All:
                default:
                    allowedMask = 0b1111;
                    flags = 0;
                    return;
            }
        }
    }
}