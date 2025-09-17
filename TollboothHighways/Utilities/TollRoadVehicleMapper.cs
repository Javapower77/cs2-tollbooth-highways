using Game.Vehicles;
using System;
using System.Collections.Generic;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollRoadHighways.Domain.Components;
using Unity.Entities;

namespace TollboothHighways.Utilities
{
    public static class TollRoadVehicleMapper
    {
        /// <summary>
        /// Maps vehicle groups to their corresponding toll road component types
        /// </summary>
        public static readonly Dictionary<VehicleGroup, Type> VehicleGroupToComponentMap = new Dictionary<VehicleGroup, Type>
        {
            { VehicleGroup.PrivateTransport, typeof(TollRoadPrivateTransportData) },
            { VehicleGroup.Trucks, typeof(TollRoadTruckData) },
            { VehicleGroup.PublicTransport, typeof(TollRoadPublicTransportData) },
            { VehicleGroup.ServiceVehicles, typeof(TollRoadServiceVehiclesData) }
        };

        /// <summary>
        /// Gets the appropriate toll road component type for a given vehicle type
        /// </summary>
        public static Type GetTollRoadComponentType(VehicleType vehicleType)
        {
            var vehicleGroup = VehiclesUtil.vehicleTypeToGroupMap.TryGetValue(vehicleType, out var group) 
                ? group 
                : VehicleGroup.PrivateTransport; // Default fallback

            return VehicleGroupToComponentMap.TryGetValue(vehicleGroup, out var componentType) 
                ? componentType 
                : typeof(TollRoadAllVehiclesData); // Universal fallback
        }

        /// <summary>
        /// Gets the ComponentType for ECS queries based on vehicle type
        /// </summary>
        public static ComponentType GetTollRoadComponentTypeForQuery(VehicleType vehicleType)
        {
            var componentType = GetTollRoadComponentType(vehicleType);
            
            if (componentType == typeof(TollRoadPrivateTransportData))
                return ComponentType.ReadOnly<TollRoadPrivateTransportData>();
            else if (componentType == typeof(TollRoadTruckData))
                return ComponentType.ReadOnly<TollRoadTruckData>();
            else if (componentType == typeof(TollRoadPublicTransportData))
                return ComponentType.ReadOnly<TollRoadPublicTransportData>();
            else if (componentType == typeof(TollRoadServiceVehiclesData))
                return ComponentType.ReadOnly<TollRoadServiceVehiclesData>();
            else
                return ComponentType.ReadOnly<TollRoadAllVehiclesData>();
        }

        /// <summary>
        /// Checks if a tollbooth entity supports the given vehicle type
        /// </summary>
        public static bool TollboothSupportsVehicleType(EntityManager entityManager, Entity tollboothEntity, VehicleType vehicleType)
        {
            // Check if tollbooth supports all vehicles first
            if (entityManager.HasComponent<TollRoadAllVehiclesData>(tollboothEntity))
                return true;

            var componentType = GetTollRoadComponentType(vehicleType);
            
            if (componentType == typeof(TollRoadPrivateTransportData))
                return entityManager.HasComponent<TollRoadPrivateTransportData>(tollboothEntity);
            else if (componentType == typeof(TollRoadTruckData))
                return entityManager.HasComponent<TollRoadTruckData>(tollboothEntity);
            else if (componentType == typeof(TollRoadPublicTransportData))
                return entityManager.HasComponent<TollRoadPublicTransportData>(tollboothEntity);
            else if (componentType == typeof(TollRoadServiceVehiclesData))
                return entityManager.HasComponent<TollRoadServiceVehiclesData>(tollboothEntity);
            
            return false;
        }
    }
}