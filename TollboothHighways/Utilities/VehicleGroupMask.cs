using TollboothHighways.Domain.Enums;

namespace TollboothHighways.Utilities
{
    public static class VehicleGroupMask
    {
        public static uint FromGroup(VehicleGroup group)
        {
            return 1u << (int)group;
        }

        public static uint AllGroups()
        {
            // Assuming enum values are contiguous 0..5
            uint mask = 0;
            for (int i = 0; i <= (int)VehicleGroup.All; i++)
                mask |= 1u << i;
            return mask;
        }
    }
}