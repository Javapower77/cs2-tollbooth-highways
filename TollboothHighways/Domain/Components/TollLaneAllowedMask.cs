using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Stored per Car sub-lane of toll roads. Mask uses same bit schema as VehicleCategoryData.
    /// </summary>
    public struct TollLaneAllowedMask : IComponentData
    {
        public byte Mask;
    }
}