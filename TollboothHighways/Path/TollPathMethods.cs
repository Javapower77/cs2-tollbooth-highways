using Game.Pathfind;
using TollboothHighways.Domain.Enums;

namespace TollboothHighways.Path
{
    public static class TollPathMethods
    {
        // High unused bits (verify against your build if future patches add values).
        public static readonly PathMethod TollPrivate  = unchecked((PathMethod)(1 << 20));
        public static readonly PathMethod TollPublic   = unchecked((PathMethod)(1 << 21));
        public static readonly PathMethod TollTruck    = unchecked((PathMethod)(1 << 22));
        public static readonly PathMethod TollService  = unchecked((PathMethod)(1 << 23));
        public static readonly PathMethod TollAllMask  = TollPrivate | TollPublic | TollTruck | TollService;

        public static PathMethod FromVehicleGroup(VehicleGroup group)
        {
            return group switch
            {
                VehicleGroup.PrivateTransport => TollPrivate,
                VehicleGroup.PublicTransport  => TollPublic,
                VehicleGroup.Trucks           => TollTruck,
                VehicleGroup.ServiceVehicles  => TollService,
                VehicleGroup.All              => TollAllMask,
                _                             => TollPrivate
            };
        }
    }
}