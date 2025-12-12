using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Utilities
{
    /// <summary>
    /// Burst-compatible utility for checking vehicle permissions on toll roads.
    /// </summary>
    [BurstCompile]
    public struct TollRoadPermissionChecker
    {
        [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateLookup;
        [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
        [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicLookup;
        [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceLookup;
        [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> AllVehiclesLookup;
        
        [ReadOnly] public ComponentLookup<PersonalCar> PersonalCarLookup;
        [ReadOnly] public ComponentLookup<DeliveryTruck> DeliveryTruckLookup;
        [ReadOnly] public ComponentLookup<PublicTransport> PublicTransportLookup;
        [ReadOnly] public ComponentLookup<Taxi> TaxiLookup;
        [ReadOnly] public ComponentLookup<PoliceCar> PoliceCarLookup;
        [ReadOnly] public ComponentLookup<Ambulance> AmbulanceLookup;
        [ReadOnly] public ComponentLookup<FireEngine> FireEngineLookup;
        [ReadOnly] public ComponentLookup<GarbageTruck> GarbageTruckLookup;
        [ReadOnly] public ComponentLookup<Hearse> HearseLookup;
        [ReadOnly] public ComponentLookup<MaintenanceVehicle> MaintenanceLookup;
        [ReadOnly] public ComponentLookup<PostVan> PostVanLookup;
        [ReadOnly] public ComponentLookup<PrisonerTransport> PrisonerTransportLookup;
        
        /// <summary>
        /// Determines the vehicle group for an entity.
        /// </summary>
        public VehicleGroup GetVehicleGroup(Entity vehicleEntity)
        {
            // Public transport (check first as they may also have other components)
            if (PublicTransportLookup.HasComponent(vehicleEntity)) 
                return VehicleGroup.PublicTransport;
            if (TaxiLookup.HasComponent(vehicleEntity)) 
                return VehicleGroup.PublicTransport;
            
            // Trucks
            if (DeliveryTruckLookup.HasComponent(vehicleEntity)) 
                return VehicleGroup.Trucks;
            
            // Service vehicles
            if (PoliceCarLookup.HasComponent(vehicleEntity) ||
                AmbulanceLookup.HasComponent(vehicleEntity) ||
                FireEngineLookup.HasComponent(vehicleEntity) ||
                GarbageTruckLookup.HasComponent(vehicleEntity) ||
                HearseLookup.HasComponent(vehicleEntity) ||
                MaintenanceLookup.HasComponent(vehicleEntity) ||
                PostVanLookup.HasComponent(vehicleEntity) ||
                PrisonerTransportLookup.HasComponent(vehicleEntity))
            {
                return VehicleGroup.ServiceVehicles;
            }
            
            // Default: Private transport (personal cars, motorcycles)
            return VehicleGroup.PrivateTransport;
        }
        
        /// <summary>
        /// Checks if a vehicle is allowed on a specific toll road.
        /// </summary>
        public bool IsVehicleAllowed(Entity vehicleEntity, Entity tollRoadEntity)
        {
            var vehicleGroup = GetVehicleGroup(vehicleEntity);
            return IsVehicleGroupAllowed(vehicleGroup, tollRoadEntity);
        }
        
        /// <summary>
        /// Checks if a vehicle group is allowed on a specific toll road.
        /// </summary>
        public bool IsVehicleGroupAllowed(VehicleGroup vehicleGroup, Entity tollRoadEntity)
        {
            // All vehicles allowed
            if (AllVehiclesLookup.HasComponent(tollRoadEntity))
                return true;

            // Check specific permissions
            bool hasPrivate = PrivateLookup.HasComponent(tollRoadEntity);
            bool hasTruck = TruckLookup.HasComponent(tollRoadEntity);
            bool hasPublic = PublicLookup.HasComponent(tollRoadEntity);
            bool hasService = ServiceLookup.HasComponent(tollRoadEntity);
            
            // If no specific restriction, allow all
            if (!hasPrivate && !hasTruck && !hasPublic && !hasService)
                return true;

            // Match vehicle group to road type
            switch (vehicleGroup)
            {
                case VehicleGroup.PrivateTransport:
                    return hasPrivate;
                    
                case VehicleGroup.Trucks:
                    return hasTruck;
                    
                case VehicleGroup.PublicTransport:
                    return hasPublic;
                    
                case VehicleGroup.ServiceVehicles:
                    // Service vehicles always allowed (emergency access)
                    return true;
                    
                default:
                    return true;
            }
        }
        
        /// <summary>
        /// Gets the pathfinding cost multiplier for a vehicle on a toll road.
        /// Returns 1.0 if allowed, high value if not allowed.
        /// </summary>
        public float GetPathfindingCostMultiplier(Entity vehicleEntity, Entity tollRoadEntity)
        {
            if (IsVehicleAllowed(vehicleEntity, tollRoadEntity))
                return 1.0f;
            
            // Very high cost makes pathfinder avoid this road
            return 10000f;
        }
        
        /// <summary>
        /// Gets the pathfinding cost multiplier for a vehicle group on a toll road.
        /// </summary>
        public float GetPathfindingCostMultiplier(VehicleGroup vehicleGroup, Entity tollRoadEntity)
        {
            if (IsVehicleGroupAllowed(vehicleGroup, tollRoadEntity))
                return 1.0f;
            
            return 10000f;
        }
    }
}