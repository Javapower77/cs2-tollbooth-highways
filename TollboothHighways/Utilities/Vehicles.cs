using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TollboothHighways.Utilities
{
    public class Vehicles
    {
        private VehicleType m_typeVehicle;
        private VehicleGroup m_groupVehicle;

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
    }
}
