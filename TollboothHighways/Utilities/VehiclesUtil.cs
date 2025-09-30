using Colossal.Entities;
using Colossal.Win32;
using Game.Simulation;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Domain.Components;
using Unity.Entities;
using Unity.Burst;

namespace TollboothHighways.Utilities
{
    public class VehiclesUtil
    {
        /// <summary>
        /// Legacy Dictionary for backward compatibility - prefer GetVehicleGroupBurstCompatible for performance
        /// </summary>
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

        /// <summary>
        /// Burst-compatible method to map vehicle type to vehicle group.
        /// Use this instead of vehicleTypeToGroupMap.TryGetValue for better performance in Burst jobs.
        /// </summary>
        /// <param name="vehicleType">The vehicle type to map</param>
        /// <returns>The corresponding vehicle group</returns>
        [BurstCompile]
        public static VehicleGroup GetVehicleGroupBurstCompatible(VehicleType vehicleType)
        {
            return vehicleType switch
            {
                VehicleType.PersonalCar => VehicleGroup.PrivateTransport,
                VehicleType.PersonalCarWithTrailer => VehicleGroup.PrivateTransport,
                VehicleType.Motorcycle => VehicleGroup.PrivateTransport,
                VehicleType.Taxi => VehicleGroup.PublicTransport,
                VehicleType.Truck => VehicleGroup.Trucks,
                VehicleType.TruckWithTrailer => VehicleGroup.Trucks,
                VehicleType.Bus => VehicleGroup.PublicTransport,
                VehicleType.ParkMaintenance => VehicleGroup.ServiceVehicles,
                VehicleType.RoadMaintenance => VehicleGroup.ServiceVehicles,
                VehicleType.Ambulance => VehicleGroup.ServiceVehicles,
                VehicleType.EvacuatingTransport => VehicleGroup.ServiceVehicles,
                VehicleType.FireEngine => VehicleGroup.ServiceVehicles,
                VehicleType.GarbageTruck => VehicleGroup.ServiceVehicles,
                VehicleType.Hearse => VehicleGroup.ServiceVehicles,
                VehicleType.PoliceCar => VehicleGroup.ServiceVehicles,
                VehicleType.PostVan => VehicleGroup.ServiceVehicles,
                VehicleType.PrisonerTransport => VehicleGroup.ServiceVehicles,
                _ => VehicleGroup.PrivateTransport // Default fallback for None and unknown types
            };
        }

        public VehicleGroup GetVehicleGroup(VehicleType vehicleType)
       {
            // Use the new Burst-compatible method for better performance
            return GetVehicleGroupBurstCompatible(vehicleType);
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
            else if (entityManager.HasComponent<MotorbikePrefabInfo>(vehicleEntity))
            {
                return VehicleType.Motorcycle;
            }
            else if (entityManager.HasComponent<Game.Vehicles.PersonalCar>(vehicleEntity))
            {
                 return VehicleType.PersonalCar;
            }
            // If no specific type is found, return None
            return VehicleType.None;
        }

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
        /// Burst-compatible method to get toll road component type for a vehicle type.
        /// Use this instead of the Dictionary-based approach for better performance.
        /// </summary>
        /// <param name="vehicleType">The vehicle type</param>
        /// <returns>The corresponding toll road component type</returns>
        [BurstCompile]
        public static Type GetTollRoadComponentTypeBurstCompatible(VehicleType vehicleType)
        {
            var vehicleGroup = GetVehicleGroupBurstCompatible(vehicleType);
            
            return vehicleGroup switch
            {
                VehicleGroup.PrivateTransport => typeof(TollRoadPrivateTransportData),
                VehicleGroup.Trucks => typeof(TollRoadTruckData),
                VehicleGroup.PublicTransport => typeof(TollRoadPublicTransportData),
                VehicleGroup.ServiceVehicles => typeof(TollRoadServiceVehiclesData),
                _ => typeof(TollRoadAllVehiclesData) // Universal fallback
            };
        }

        [BurstCompile]
        public static string GetTollRoadType(VehicleType vehicleType)
        {
            var vehicleGroup = GetVehicleGroupBurstCompatible(vehicleType);

            return vehicleGroup switch
            {
                VehicleGroup.PrivateTransport => "TollRoadPrivateTransportData",
                VehicleGroup.Trucks => "TollRoadTruckData",
                VehicleGroup.PublicTransport => "TollRoadPublicTransportData",
                VehicleGroup.ServiceVehicles => "TollRoadServiceVehiclesData",
                _ => "NotFound" // Universal fallback
            };
        }

        /// <summary>
        /// Gets the appropriate toll road component type for a given vehicle type
        /// </summary>
        public static Type GetTollRoadComponentType(VehicleType vehicleType)
        {
            // Use the new Burst-compatible method
            return GetTollRoadComponentTypeBurstCompatible(vehicleType);
        }

        /// <summary>
        /// Gets the ComponentType for ECS queries based on vehicle type
        /// </summary>
        public static ComponentType GetTollRoadComponentTypeForQuery(VehicleType vehicleType)
        {
            var componentType = GetTollRoadComponentTypeBurstCompatible(vehicleType);

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
            var componentType = GetTollRoadComponentTypeBurstCompatible(vehicleType);

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

        public static string GetTollboothRoadType(EntityManager entityManager, Entity tollboothEntity)
        {
            if (entityManager.HasComponent<TollRoadPrivateTransportData>(tollboothEntity))
                return "TollRoadPrivateTransportData";
            else if (entityManager.HasComponent<TollRoadTruckData>(tollboothEntity))
                return "TollRoadTruckData";
            else if (entityManager.HasComponent<TollRoadPublicTransportData>(tollboothEntity))
                return "TollRoadPublicTransportData";
            else if (entityManager.HasComponent<TollRoadServiceVehiclesData>(tollboothEntity))
                return "TollRoadServiceVehiclesData";
            else if (entityManager.HasComponent<TollRoadAllVehiclesData>(tollboothEntity))
                return "TollRoadAllVehiclesData";
            else
                return "NotFound";
        }

        /// <summary>
        /// Determines whether a specified vehicle is allowed to pass through a given tollbooth.
        /// </summary>
        /// <remarks>This method evaluates the compatibility of a vehicle with a tollbooth based on the
        /// components associated with each entity. Tollbooths may be configured to accept specific types of vehicles
        /// (e.g., public transport, trucks, service vehicles, or private transport), or they may accept all vehicles if
        /// no specific configuration is present.</remarks>
        /// <param name="entityManager">The <see cref="EntityManager"/> instance used to query components of entities.</param>
        /// <param name="vehicle">The entity representing the vehicle to evaluate.</param>
        /// <param name="tollbooth">The entity representing the tollbooth to check against.</param>
        /// <returns><see langword="true"/> if the vehicle is supported by the tollbooth; otherwise, <see langword="false"/>.</returns>
        private bool IsVehicleSupported(EntityManager entityManager, Entity vehicle, Entity tollbooth)
        {
            if (!entityManager.Exists(tollbooth)) return false;
            bool hasSpecific = entityManager.HasComponent<TollRoadTruckData>(tollbooth) ||
                               entityManager.HasComponent<TollRoadPublicTransportData>(tollbooth) ||
                               entityManager.HasComponent<TollRoadServiceVehiclesData>(tollbooth) ||
                               entityManager.HasComponent<TollRoadPrivateTransportData>(tollbooth);
            if (!hasSpecific) return true; // booth accepts all

            bool isPublic = entityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicle) || entityManager.HasComponent<Game.Vehicles.Taxi>(vehicle);
            if (isPublic && (entityManager.HasComponent<TollRoadPublicTransportData>(tollbooth) || entityManager.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            bool isTruck = entityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicle);
            if (isTruck && (entityManager.HasComponent<TollRoadTruckData>(tollbooth) || entityManager.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            bool isService = entityManager.HasComponent<Game.Vehicles.Ambulance>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.FireEngine>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.GarbageTruck>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.PoliceCar>(vehicle) ||
                                entityManager.HasComponent<Game.Vehicles.Hearse>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.ParkMaintenanceVehicle>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.RoadMaintenanceVehicle>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.PrisonerTransport>(vehicle) ||
                              entityManager.HasComponent<Game.Vehicles.PostVan>(vehicle);
            if (isService && (entityManager.HasComponent<TollRoadServiceVehiclesData>(tollbooth) || entityManager.HasComponent<TollRoadAllVehiclesData>(tollbooth))) return true;

            // default private cars / motorcycles
            return entityManager.HasComponent<TollRoadPrivateTransportData>(tollbooth) || entityManager.HasComponent<TollRoadAllVehiclesData>(tollbooth);
        }

        internal static VehicleType GetVehicleTypeStatic(Entity e, EntityManager em)
        {
            VehiclesUtil util = new VehiclesUtil();
            return util.GetVehicleType(e, em);
        }
    }
}
