using Game.Pathfind;
using TollboothHighways.Domain.Enums;

namespace TollboothHighways.Domain
{
    public static class TollPathMethods
    {
        // Use valid bit positions within ushort range (0-15) that don't conflict with existing PathMethod values
        // Looking at existing values: highest used is MediumRoad = 0x2000 (bit 13)
        // So we can only use bits 14 and 15 safely, need to compress to 2 bits or find another approach
        // Alternative: use available lower bits by combining with existing values
        public static readonly PathMethod TollPrivate = (PathMethod)0x4000;    // Bit 14
        public static readonly PathMethod TollPublic = (PathMethod)0x8000;     // Bit 15  
        public static readonly PathMethod TollTruck = (PathMethod)0xC000;      // Bits 14+15 combined
        public static readonly PathMethod TollService = (PathMethod)0x4000;    // Same as private for now
        public static readonly PathMethod TollAllMask = TollPrivate | TollPublic | TollTruck;

        public static PathMethod FromVehicleGroup(VehicleGroup group)
        {
            var result = group switch
            {
                VehicleGroup.PrivateTransport => TollPrivate,
                VehicleGroup.PublicTransport => TollPublic,
                VehicleGroup.Trucks => TollTruck,
                VehicleGroup.ServiceVehicles => TollService,
                VehicleGroup.All => TollAllMask,
                VehicleGroup.None => (PathMethod)0,
                _ => TollPrivate
            };

            // Debug logging to verify the values
#if DEBUG
            UnityEngine.Debug.Log($"FromVehicleGroup({group}) = {result} (int: {(int)result})");
#endif

            return result;
        }

        // Helper method to check if a PathMethod contains toll flags
        public static bool HasTollMethod(PathMethod method)
        {
            return (method & TollAllMask) != 0;
        }

        // Helper method to get the vehicle group from PathMethod
        public static VehicleGroup GetVehicleGroupFromMethod(PathMethod method)
        {
            if ((method & TollAllMask) == TollAllMask) return VehicleGroup.All;
            if ((method & TollTruck) == TollTruck) return VehicleGroup.Trucks;  // Check this first as it uses both bits
            if ((method & TollPrivate) != 0) return VehicleGroup.PrivateTransport;
            if ((method & TollPublic) != 0) return VehicleGroup.PublicTransport;
            return VehicleGroup.None;
        }
    }
}