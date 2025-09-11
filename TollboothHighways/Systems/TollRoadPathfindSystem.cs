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

        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;
        private ComponentLookup<TollRoadAllVehiclesData> m_AllVehiclesLookup;
        private ComponentLookup<Game.Vehicles.Vehicle> m_VehicleLookup;
        private BufferLookup<PathElement> m_PathElementLookup;

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
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_PrivateTransportLookup = GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TruckLookup = GetComponentLookup<TollRoadTruckData>(true);
            m_PublicTransportLookup = GetComponentLookup<TollRoadPublicTransportData>(true);
            m_ServiceVehiclesLookup = GetComponentLookup<TollRoadServiceVehiclesData>(true);
            m_AllVehiclesLookup = GetComponentLookup<TollRoadAllVehiclesData>(true);
            m_VehicleLookup = GetComponentLookup<Game.Vehicles.Vehicle>(true);
            m_PathElementLookup = GetBufferLookup<PathElement>(true);

            // Apply pathfinding cost modifications
            ApplyPathfindCosts();
        }

        private void ApplyPathfindCosts()
        {
            var ownerLookup = m_OwnerLookup;
            var privateTransportLookup = m_PrivateTransportLookup;
            var truckLookup = m_TruckLookup;
            var publicTransportLookup = m_PublicTransportLookup;
            var serviceVehiclesLookup = m_ServiceVehiclesLookup;
            var allVehiclesLookup = m_AllVehiclesLookup;
            var vehicleLookup = m_VehicleLookup;
            var pathElementLookup = m_PathElementLookup;
            var vehiclesUtil = m_VehiclesUtil;

            Entities
                .WithName("ModifyPathfindCosts")
                .WithStoreEntityQueryInField(ref m_PathfindQuery)
                .WithReadOnly(ownerLookup)
                .WithReadOnly(privateTransportLookup)
                .WithReadOnly(truckLookup)
                .WithReadOnly(publicTransportLookup)
                .WithReadOnly(serviceVehiclesLookup)
                .WithReadOnly(allVehiclesLookup)
                .WithReadOnly(vehicleLookup)
                .WithReadOnly(pathElementLookup)
                .WithStructuralChanges()
                .ForEach((Entity pathEntity,
                          ref PathfindParameters pathParams,
                          in PathOwner pathOwner) =>
                {
                    Entity vehicleEntity = Entity.Null;

                    // Method 1: Check if pathfind entity has an Owner component pointing to vehicle
                    if (ownerLookup.TryGetComponent(pathEntity, out var pathOwnerComponent))
                    {
                        var potentialVehicle = pathOwnerComponent.m_Owner;
                        if (vehicleLookup.HasComponent(potentialVehicle))
                        {
                            vehicleEntity = potentialVehicle;
                        }
                    }

                    // Method 2: Search for vehicles that have this pathfind entity in their PathElement buffer
                    if (vehicleEntity == Entity.Null)
                    {
                        vehicleEntity = FindVehicleByPathReference(pathEntity, vehicleLookup, pathElementLookup);
                    }

                    if (vehicleEntity == Entity.Null || !vehicleLookup.HasComponent(vehicleEntity))
                        return;

                    // Determine vehicle type and group
                    VehicleType vehicleType = vehiclesUtil.GetVehicleType(vehicleEntity, EntityManager);
                    if (vehicleType == VehicleType.None)
                        return;

                    VehicleGroup vehicleGroup = vehiclesUtil.GetVehicleGroup(vehicleType);

                    // Create and add cost modifier component to the pathfind entity
                    var costModifier = CreateCostModifier(vehicleGroup,
                        privateTransportLookup, truckLookup, publicTransportLookup,
                        serviceVehiclesLookup, allVehiclesLookup);

                    if (EntityManager.HasComponent<PathfindCostModifier>(pathEntity))
                    {
                        EntityManager.SetComponentData(pathEntity, costModifier);
                    }
                    else
                    {
                        EntityManager.AddComponentData(pathEntity, costModifier);
                    }

                }).Run();
        }

        // Helper method to find vehicle by checking PathElement buffers
        private Entity FindVehicleByPathReference(Entity pathEntity,
            ComponentLookup<Game.Vehicles.Vehicle> vehicleLookup,
            BufferLookup<PathElement> pathElementLookup)
        {
            // Use entity query to iterate through all vehicles with PathElement buffers
            var vehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>(),
                    ComponentType.ReadOnly<PathElement>()
                }
            });

            var vehicleEntities = vehicleQuery.ToEntityArray(Allocator.Temp);

            try
            {
                for (int i = 0; i < vehicleEntities.Length; i++)
                {
                    var vehicle = vehicleEntities[i];

                    // Check if this vehicle has PathElement buffer that references our pathfind entity
                    if (pathElementLookup.TryGetBuffer(vehicle, out var pathElements))
                    {
                        for (int j = 0; j < pathElements.Length; j++)
                        {
                            // Check if any path element targets our pathfind entity
                            if (pathElements[j].m_Target == pathEntity)
                            {
                                return vehicle;
                            }
                        }
                    }
                }
            }
            finally
            {
                vehicleEntities.Dispose();
            }

            return Entity.Null;
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