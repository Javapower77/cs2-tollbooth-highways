using Game;
using Game.Common;
using Game.Pathfind;
using Game.Vehicles;
using Game.Net;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using TollRoadHighways.Domain.Components;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Adjusts vehicle PathElement buffer so that if multiple toll road alternatives appear in its path,
    /// it swaps to a tollbooth that is compatible with the concrete vehicle (service, public transport, truck, or default private).
    /// No payment logic.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TollboothPathSelectionSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;
        private ComponentLookup<PathOwner> m_PathOwnerLookup;
        private BufferLookup<PathElement> m_PathElementLookup;
        private ComponentLookup<Lane> m_LaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Road> m_RoadLookup;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;
        private ComponentLookup<Game.Net.Edge> m_EdgeLookup;
        private EntityManager m_Em;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Em = EntityManager;
            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>()
                }
            });
            RequireForUpdate(m_VehicleQuery);
        }

        protected override void OnUpdate()
        {
            m_PathOwnerLookup = GetComponentLookup<PathOwner>(true);
            m_PathElementLookup = GetBufferLookup<PathElement>(false);
            m_LaneLookup = GetComponentLookup<Lane>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_RoadLookup = GetComponentLookup<Road>(true);
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_EdgeLookup = GetComponentLookup<Game.Net.Edge>(true);

            var vehicles = m_VehicleQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var vehicle in vehicles)
            {
                if (!m_PathOwnerLookup.HasComponent(vehicle) || !m_PathElementLookup.HasBuffer(vehicle))
                    continue;
                var path = m_PathElementLookup[vehicle];
                if (path.Length == 0) continue;

                for (int i = 0; i < path.Length; i++)
                {
                    var pe = path[i];
                    var road = ResolveRoadFromTarget(pe.m_Target);
                    if (road == Entity.Null) continue;
                    if (!m_TollRoadLookup.HasComponent(road)) continue;
                    var toll = m_TollRoadLookup[road];
                    if (!toll.HasActiveTollbooth || toll.AssociatedTollbooth == Entity.Null) continue;

                    if (!IsVehicleSupported(vehicle, road))
                    {
                        int alt = FindCompatible(vehicle, path, i + 1);
                        if (alt >= 0)
                        {
                            path[i] = path[alt];
                        }
                    }
                }
            }
            vehicles.Dispose();
        }

        private Entity ResolveRoadFromTarget(Entity target)
        {
            if (m_LaneLookup.HasComponent(target) && m_OwnerLookup.TryGetComponent(target, out var owner))
            {
                if (owner.m_Owner != Entity.Null && m_RoadLookup.HasComponent(owner.m_Owner))
                    return owner.m_Owner;
            }
            else if (m_OwnerLookup.TryGetComponent(target, out var owner2))
            {
                if (owner2.m_Owner != Entity.Null && m_RoadLookup.HasComponent(owner2.m_Owner))
                    return owner2.m_Owner;
            }
            return Entity.Null;
        }

        // Compatibility logic: if tollbooth has any specific marker component we check those, otherwise allow all.
        private bool IsVehicleSupported(Entity vehicle, Entity tollbooth)
        {
            if (!m_Em.Exists(tollbooth)) return false;
            bool hasSpecific = m_Em.HasComponent<TollRoadTruckData>(tollbooth) ||
                               m_Em.HasComponent<TollRoadPublicTransportData>(tollbooth) ||
                               m_Em.HasComponent<TollRoadServiceVehiclesData>(tollbooth) ||
                               m_Em.HasComponent<TollRoadPrivateTransportData>(tollbooth);
            if (!hasSpecific) return true; // booth accepts all

            bool isPublic = m_Em.HasComponent<Game.Vehicles.PublicTransport>(vehicle) || m_Em.HasComponent<Game.Vehicles.Taxi>(vehicle);
            if (isPublic && (m_Em.HasComponent<TollRoadPublicTransportData>(tollbooth) || m_Em.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            bool isTruck = m_Em.HasComponent<Game.Vehicles.DeliveryTruck>(vehicle);
            if (isTruck && (m_Em.HasComponent<TollRoadTruckData>(tollbooth) || m_Em.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            bool isService = m_Em.HasComponent<Game.Vehicles.Ambulance>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.FireEngine>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.GarbageTruck>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.PoliceCar>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.Hearse>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.PrisonerTransport>(vehicle) ||
                              m_Em.HasComponent<Game.Vehicles.PostVan>(vehicle);
            if (isService && (m_Em.HasComponent<TollRoadServiceVehiclesData>(tollbooth) || m_Em.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            // default private cars / motorcycles
            return m_Em.HasComponent<TollRoadPrivateTransportData>(tollbooth) || m_Em.HasComponent<TollRoadAllVehiclesData>(tollbooth);
        }

        private int FindCompatible(Entity vehicle, DynamicBuffer<PathElement> path, int start, int maxLookAhead = 8)
        {
            int end = math.min(path.Length, start + maxLookAhead);
            for (int i = start; i < end; i++)
            {
                var pe = path[i];
                var road = ResolveRoadFromTarget(pe.m_Target);
                if (road == Entity.Null) continue;
                if (!m_TollRoadLookup.HasComponent(road)) continue;              
                if (!m_EdgeLookup.HasComponent(road)) continue;

                if (IsVehicleSupported(vehicle, road)) return i;
            }
            return -1;
        }
    }

}
