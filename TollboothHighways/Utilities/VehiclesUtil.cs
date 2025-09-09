using Colossal.Entities;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TollboothHighways.Domain.Enums;
using Unity.Entities;

namespace TollboothHighways.Utilities
{
    public class VehiclesUtil
    {
        public static readonly Dictionary<VehicleType, VehicleGroup> vehicleTypeToGroupMap = new Dictionary<VehicleType, VehicleGroup>
        {
            { VehicleType.PersonalCar, VehicleGroup.PrivateTransport },
            { VehicleType.PersonalCarWithTrailer, VehicleGroup.PrivateTransport },
            { VehicleType.Motorcycle, VehicleGroup.PrivateTransport },
            { VehicleType.Taxi, VehicleGroup.PublicTransport },
            { VehicleType.Truck, VehicleGroup.Trucks },
            { VehicleType.TruckWithTrailer, VehicleGroup.Trucks },
            { VehicleType.Bus, VehicleGroup.PublicTransport },
            { VehicleType.ParkMaintenance, VehicleGroup.ServiceVehicles },
            { VehicleType.RoadMaintenance, VehicleGroup.ServiceVehicles },
            { VehicleType.Ambulance, VehicleGroup.ServiceVehicles },
            { VehicleType.EvacuatingTransport, VehicleGroup.ServiceVehicles },
            { VehicleType.FireEngine, VehicleGroup.ServiceVehicles },
            { VehicleType.GarbageTruck, VehicleGroup.ServiceVehicles },
            { VehicleType.Hearse, VehicleGroup.ServiceVehicles },
            { VehicleType.PoliceCar, VehicleGroup.ServiceVehicles },
            { VehicleType.PostVan, VehicleGroup.ServiceVehicles },
            { VehicleType.PrisonerTransport, VehicleGroup.ServiceVehicles }
        };

       public VehicleGroup GetVehicleGroup(VehicleType vehicleType)
       {
            if (vehicleTypeToGroupMap.TryGetValue(vehicleType, out VehicleGroup group))
            {
                return group;
            }
            else
            {
                throw new ArgumentException($"Vehicle type {vehicleType} is not recognized.");
            }
       }

        public Domain.Enums.VehicleType GetVehicleType(Entity vehicleEntity, EntityManager entityManager)
        {
            // Check if the vehicle has a trailer. Can be a car or a truck.
            if (entityManager.HasBuffer<Game.Vehicles.LayoutElement>(vehicleEntity))
            {
                // Get the vehicle layout elements to determine if it is a car or a truck
                if (entityManager.TryGetBuffer<Game.Vehicles.LayoutElement>(vehicleEntity, true, out DynamicBuffer<LayoutElement> vehicleLayout))
                {
                    // The component LayoutElement is used to represent the vehicle layout,
                    // which can be used to determine if the trailer are attached to a car or a truck.
                    // If Index in position 0 or 1 of the vehicle layout is a PersonalCar, then it is a PersonalCarWithTrailer,
                    if ((entityManager.HasComponent<Game.Vehicles.PersonalCar>(vehicleLayout[0].m_Vehicle)) || (entityManager.HasComponent<Game.Vehicles.PersonalCar>(vehicleLayout[1].m_Vehicle)))
                    {
                        return VehicleType.PersonalCarWithTrailer;
                    }
                    else
                    {
                        return VehicleType.TruckWithTrailer;
                    }
                }
            }
            // Check if the PublicTransport component is present, which indicates the vehicle type is a bus.
            else if (entityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicleEntity))
            {
                return VehicleType.Bus;
            }
            // Check if DeliveryTruck component is present, which indicates the vehicle type is a truck.
            else if (entityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicleEntity))
            {
                return VehicleType.Truck;
            }
            // Check if PoliceCar component is present, which indicates the vehicle type is a police car.
            else if (entityManager.HasComponent<Game.Vehicles.PoliceCar>(vehicleEntity))
            {
                return VehicleType.PoliceCar;
            }
            // Check if GarbageTruck component is present, which indicates the vehicle type is a garbage truck.
            else if (entityManager.HasComponent<Game.Vehicles.GarbageTruck>(vehicleEntity))
            {
                return VehicleType.GarbageTruck;
            }
            // Check if Taxi component is present, which indicates the vehicle type is a taxi.
            else if (entityManager.HasComponent<Game.Vehicles.Taxi>(vehicleEntity))
            {
                return VehicleType.Taxi;
            }
            // Check if Ambulance component is present, which indicates the vehicle type is an ambulance.
            else if (entityManager.HasComponent<Game.Vehicles.Ambulance>(vehicleEntity))
            {
                return VehicleType.Ambulance;
            }
            // Check if FireEngine component is present, which indicates the vehicle type is a fire engine.
            else if (entityManager.HasComponent<Game.Vehicles.FireEngine>(vehicleEntity))
            {
                return VehicleType.FireEngine;
            }
            //
            else if (entityManager.HasComponent<Game.Vehicles.EvacuatingTransport>(vehicleEntity))
            {
                return VehicleType.EvacuatingTransport;
            }
            // Check if ParkMaintenanceVehicle component is present, which indicates the vehicle type is a park maintenance vehicle.
            else if (entityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicleEntity))
            {
                return VehicleType.ParkMaintenance;
            }
            // Check if RoadMaintenanceVehicle component is present, which indicates the vehicle type is a road maintenance vehicle.
            else if (entityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicleEntity))
            {
                return VehicleType.RoadMaintenance;
            }
            // Check if Hearse component is present, which indicates the vehicle type is a hearse.
            else if (entityManager.HasComponent<Game.Vehicles.Hearse>(vehicleEntity))
            {
                return VehicleType.Hearse;
            }
            // Check if PrisonerTransport component is present, which indicates the vehicle type is a prisoner transport.
            else if (entityManager.HasComponent<Game.Vehicles.PrisonerTransport>(vehicleEntity))
            {
                return VehicleType.PrisonerTransport;
            }
            // Check if PostVan component is present, which indicates the vehicle type is a post van.
            else if (entityManager.HasComponent<Game.Vehicles.PostVan>(vehicleEntity))
            {
                return VehicleType.PostVan;
            }
            // At the end check for the Passenger component, which indicates the vehicle type is a personal car or a motorcycle.            
            else if (entityManager.HasBuffer<Game.Vehicles.Passenger>(vehicleEntity))
            {
                if (entityManager.TryGetBuffer<Game.Vehicles.Passenger>(vehicleEntity, true, out DynamicBuffer<Game.Vehicles.Passenger> passengers))
                {
                    // If the vehicle has only one passenger, it is a motorcycle
                    if (passengers.Length == 1)
                    {
                        return VehicleType.Motorcycle;
                    }
                    // If the vehicle has no passengers, it is a personal car. Passenger objects are used in other way
                    if (passengers.Length == 0)
                    {
                        return VehicleType.PersonalCar;
                    }
                }
            }
            // If no specific type is found, return None
            return VehicleType.None;
        }
    }
}
