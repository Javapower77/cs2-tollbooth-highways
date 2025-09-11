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
    /// System that modifies individual lane costs based on vehicle restrictions
    /// </summary>
    [BurstCompile]
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

        protected override void OnCreate()
        {
            base.OnCreate();

            m_LaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<PathfindCosts>(),
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

            var laneLookup = m_LaneLookup;
            var ownerLookup = m_OwnerLookup;
            var roadLookup = m_RoadLookup;
            var privateTransportLookup = m_PrivateTransportLookup;
            var truckLookup = m_TruckLookup;
            var publicTransportLookup = m_PublicTransportLookup;
            var serviceVehiclesLookup = m_ServiceVehiclesLookup;
            var allVehiclesLookup = m_AllVehiclesLookup;

            Entities
                .WithName("ModifyLaneCosts")
                .WithStoreEntityQueryInField(ref m_LaneQuery)
                .WithReadOnly(laneLookup)
                .WithReadOnly(ownerLookup)
                .WithReadOnly(roadLookup)
                .WithReadOnly(privateTransportLookup)
                .WithReadOnly(truckLookup)
                .WithReadOnly(publicTransportLookup)
                .WithReadOnly(serviceVehiclesLookup)
                .WithReadOnly(allVehiclesLookup)
                .ForEach((Entity entity,
                          ref PathfindCosts costs,
                          in Lane lane,
                          in Owner owner) =>
                {
                    Entity roadEntity = owner.m_Owner;
                    if (roadEntity == Entity.Null || !roadLookup.HasComponent(roadEntity))
                        return;

                    // Check if this road has vehicle restrictions
                    if (!HasVehicleRestrictions(roadEntity, privateTransportLookup, truckLookup,
                                              publicTransportLookup, serviceVehiclesLookup, allVehiclesLookup))
                        return;

                    // Store original costs if not already stored
                    if (costs.m_Value.x == 0) // Assuming 0 means unmodified
                    {
                        // Set base cost for restricted lanes
                        costs.m_Value.x = 1f; // Base cost
                    }

                    // Add restriction information to the lane for pathfinding
                    var restrictionData = GetLaneRestrictions(roadEntity, privateTransportLookup,
                        truckLookup, publicTransportLookup, serviceVehiclesLookup);

                    // Store restriction info in unused cost components
                    costs.m_Value.y = (float)restrictionData;

                }).ScheduleParallel();
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
}