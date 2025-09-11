using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using TollRoadHighways.Domain.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using VehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System that modifies pathfinding costs for restricted toll roads.
    /// Makes unauthorized vehicle types avoid restricted toll roads during path calculation.
    /// </summary>
    public partial class TollRoadPathfindSystem : GameSystemBase
    {
        /// <summary>
        /// Very high cost to make restricted roads practically unusable for unauthorized vehicles
        /// </summary>
        private const float RestrictedRoadPenalty = 10000f;

        /// <summary>
        /// Small cost increase for authorized vehicles to prefer free roads when available
        /// </summary>
        private const float AuthorizedRoadCost = 10f;

        private EntityQuery m_PathfindQuery;
        private EntityQuery m_RestrictedRoadQuery;

        private ComponentLookup<Lane> m_LaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Road> m_RoadLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;
        private ComponentLookup<TollRoadAllVehiclesData> m_AllVehiclesLookup;
        private ComponentLookup<PathfindCosts> m_PathfindCostsLookup;
        private ComponentLookup<Game.Vehicles.Vehicle> m_VehicleLookup;

        private VehiclesUtil m_VehiclesUtil;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_VehiclesUtil = new VehiclesUtil();

            // Query for pathfind requests that need cost adjustment
            m_PathfindQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PathfindParameters>(),
                    ComponentType.ReadOnly<PathOwner>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            // Query for roads with vehicle restrictions
            m_RestrictedRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<TollRoadPrivateTransportData>(),
                    ComponentType.ReadOnly<TollRoadTruckData>(),
                    ComponentType.ReadOnly<TollRoadPublicTransportData>(),
                    ComponentType.ReadOnly<TollRoadServiceVehiclesData>()
                },
                All = new[]
                {
                    ComponentType.ReadOnly<Road>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<TollRoadAllVehiclesData>() // Exclude unrestricted roads
                }
            });

            RequireForUpdate(m_RestrictedRoadQuery);
        }

        protected override void OnUpdate()
        {
            // Update lookups
            m_LaneLookup = GetComponentLookup<Lane>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_RoadLookup = GetComponentLookup<Road>(true);
            m_PrivateTransportLookup = GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TruckLookup = GetComponentLookup<TollRoadTruckData>(true);
            m_PublicTransportLookup = GetComponentLookup<TollRoadPublicTransportData>(true);
            m_ServiceVehiclesLookup = GetComponentLookup<TollRoadServiceVehiclesData>(true);
            m_AllVehiclesLookup = GetComponentLookup<TollRoadAllVehiclesData>(true);
            m_PathfindCostsLookup = GetComponentLookup<PathfindCosts>(false);
            m_VehicleLookup = GetComponentLookup<Game.Vehicles.Vehicle>(true);

            // Apply pathfinding cost modifications
            ApplyPathfindCosts();
        }

        private void ApplyPathfindCosts()
        {
            var laneLookup = m_LaneLookup;
            var ownerLookup = m_OwnerLookup;
            var roadLookup = m_RoadLookup;
            var privateTransportLookup = m_PrivateTransportLookup;
            var truckLookup = m_TruckLookup;
            var publicTransportLookup = m_PublicTransportLookup;
            var serviceVehiclesLookup = m_ServiceVehiclesLookup;
            var allVehiclesLookup = m_AllVehiclesLookup;
            var pathfindCostsLookup = m_PathfindCostsLookup;
            var vehicleLookup = m_VehicleLookup;
            var vehiclesUtil = m_VehiclesUtil;
            var entityManager = EntityManager;

            Entities
                .WithName("ModifyPathfindCosts")
                .WithStoreEntityQueryInField(ref m_PathfindQuery)
                .WithReadOnly(laneLookup)
                .WithReadOnly(ownerLookup)
                .WithReadOnly(roadLookup)
                .WithReadOnly(privateTransportLookup)
                .WithReadOnly(truckLookup)
                .WithReadOnly(publicTransportLookup)
                .WithReadOnly(serviceVehiclesLookup)
                .WithReadOnly(allVehiclesLookup)
                .WithReadOnly(vehicleLookup)
                .WithCaptureLocal(vehiclesUtil)
                .WithCaptureLocal(entityManager)
                .ForEach((Entity pathEntity,
                          ref PathfindParameters pathParams,
                          in PathOwner pathOwner) =>
                {
                    // Get the vehicle entity that owns this pathfind request
                    Entity vehicleEntity = pathOwner.m_Owner;
                    if (vehicleEntity == Entity.Null || !vehicleLookup.HasComponent(vehicleEntity))
                        return;

                    // Determine vehicle type and group
                    VehicleType vehicleType = vehiclesUtil.GetVehicleType(vehicleEntity, entityManager);
                    if (vehicleType == VehicleType.None)
                        return;

                    VehicleGroup vehicleGroup = vehiclesUtil.GetVehicleGroup(vehicleType);

                    // Apply custom pathfind costs for this vehicle type
                    pathParams.m_PathfindCostModifier = CreateCostModifier(vehicleGroup,
                        privateTransportLookup, truckLookup, publicTransportLookup,
                        serviceVehiclesLookup, allVehiclesLookup);

                }).ScheduleParallel();
        }

        private static PathfindCostModifier CreateCostModifier(
            VehicleGroup vehicleGroup,
            ComponentLookup<TollRoadPrivateTransportData> privateTransportLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicTransportLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceVehiclesLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            return new PathfindCostModifier
            {
                VehicleGroup = vehicleGroup,
                RestrictedRoadPenalty = RestrictedRoadPenalty,
                AuthorizedRoadCost = AuthorizedRoadCost
            };
        }
    }

    /// <summary>
    /// Custom pathfind cost modifier for vehicle type restrictions
    /// </summary>
    public struct PathfindCostModifier : IComponentData
    {
        public VehicleGroup VehicleGroup;
        public float RestrictedRoadPenalty;
        public float AuthorizedRoadCost;
    }
}