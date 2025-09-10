using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    // Mirrors bit layout used by TollLaneAllowedMask (1=Private, 2=Transit, 4=Heavy, 8=Service)
    public struct VehicleCategoryMask : IComponentData
    {
        public byte Mask;
    }
}