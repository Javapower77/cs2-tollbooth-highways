using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollRoadHighways.Domain.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System that modifies individual lane costs based on vehicle restrictions by adding cost modifier components
    /// </summary>
    public partial class LaneCostModificationSystem : GameSystemBase
    {
        private EntityQuery m_LaneQuery;

        private ComponentLookup<Lane> m_LaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Road> m_RoadLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;
        private ComponentLookup<TollRoadAllVehiclesData> m_AllVehiclesLookup;
        private ComponentLookup<LaneCostModifier> m_LaneCostModifierLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Query for lanes that belong to roads (have Owner component pointing to road)
            m_LaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Lane>(),
                    ComponentType.ReadOnly<Owner>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            RequireForUpdate(m_LaneQuery);
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
            m_LaneCostModifierLookup = GetComponentLookup<LaneCostModifier>(false);

            var roadLookup = m_RoadLookup;
            var privateTransportLookup = m_PrivateTransportLookup;
            var truckLookup = m_TruckLookup;
            var publicTransportLookup = m_PublicTransportLookup;
            var serviceVehiclesLookup = m_ServiceVehiclesLookup;
            var allVehiclesLookup = m_AllVehiclesLookup;
            var laneCostModifierLookup = m_LaneCostModifierLookup;

            // Process lanes and add/update cost modifier components
            Entities
                .WithName("ModifyLaneCosts")
                .WithStoreEntityQueryInField(ref m_LaneQuery)
                .WithReadOnly(roadLookup)
                .WithReadOnly(privateTransportLookup)
                .WithReadOnly(truckLookup)
                .WithReadOnly(publicTransportLookup)
                .WithReadOnly(serviceVehiclesLookup)
                .WithReadOnly(allVehiclesLookup)
                .WithStructuralChanges()
                .ForEach((Entity laneEntity,
                          in Lane lane,
                          in Owner owner) =>
                {
                    Entity roadEntity = owner.m_Owner;
                    if (roadEntity == Entity.Null || !roadLookup.HasComponent(roadEntity))
                        return;

                    // Check if this road has vehicle restrictions
                    bool hasRestrictions = HasVehicleRestrictions(roadEntity, privateTransportLookup, truckLookup,
                                              publicTransportLookup, serviceVehiclesLookup, allVehiclesLookup);

                    if (hasRestrictions)
                    {
                        // Get the allowed vehicle group for this lane
                        var allowedGroup = GetLaneRestrictions(roadEntity, privateTransportLookup,
                            truckLookup, publicTransportLookup, serviceVehiclesLookup);

                        var costModifier = new LaneCostModifier
                        {
                            AllowedVehicleGroup = allowedGroup,
                            RestrictedCostMultiplier = 100f, // High cost for unauthorized vehicles
                            AuthorizedCostMultiplier = 1.1f  // Slight increase for authorized vehicles
                        };

                        // Add or update the cost modifier component
                        if (EntityManager.HasComponent<LaneCostModifier>(laneEntity))
                        {
                            EntityManager.SetComponentData(laneEntity, costModifier);
                        }
                        else
                        {
                            EntityManager.AddComponentData(laneEntity, costModifier);
                        }
                    }
                    else
                    {
                        // Remove cost modifier if road no longer has restrictions
                        if (EntityManager.HasComponent<LaneCostModifier>(laneEntity))
                        {
                            EntityManager.RemoveComponent<LaneCostModifier>(laneEntity);
                        }
                    }

                }).Run();
        }

        private static bool HasVehicleRestrictions(
            Entity roadEntity,
            ComponentLookup<TollRoadPrivateTransportData> privateTransportLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicTransportLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceVehiclesLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            return !allVehiclesLookup.HasComponent(roadEntity) && (
                   privateTransportLookup.HasComponent(roadEntity) ||
                   truckLookup.HasComponent(roadEntity) ||
                   publicTransportLookup.HasComponent(roadEntity) ||
                   serviceVehiclesLookup.HasComponent(roadEntity));
        }

        private static VehicleGroup GetLaneRestrictions(
            Entity roadEntity,
            ComponentLookup<TollRoadPrivateTransportData> privateTransportLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicTransportLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceVehiclesLookup)
        {
            if (privateTransportLookup.HasComponent(roadEntity))
                return VehicleGroup.PrivateTransport;
            if (truckLookup.HasComponent(roadEntity))
                return VehicleGroup.Trucks;
            if (publicTransportLookup.HasComponent(roadEntity))
                return VehicleGroup.PublicTransport;
            if (serviceVehiclesLookup.HasComponent(roadEntity))
                return VehicleGroup.ServiceVehicles;

            return VehicleGroup.PrivateTransport; // Default
        }
    }

    /// <summary>
    /// Component that stores cost modification data for restricted lanes
    /// </summary>
    public struct LaneCostModifier : IComponentData
    {
        /// <summary>
        /// Which vehicle group is allowed to use this lane at normal cost
        /// </summary>
        public VehicleGroup AllowedVehicleGroup;

        /// <summary>
        /// Cost multiplier for vehicles NOT in the allowed group
        /// </summary>
        public float RestrictedCostMultiplier;

        /// <summary>
        /// Cost multiplier for vehicles in the allowed group
        /// </summary>
        public float AuthorizedCostMultiplier;
    }
}