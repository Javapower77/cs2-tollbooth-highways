using TollboothHighways.Domain.Enums;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Component attached to a lane that has been blocked because of an incompatible vehicle.
    /// Tracks which vehicle caused the block and when.
    /// </summary>
    public struct LaneBlockedByVehicle : IComponentData
    {
        public Entity Vehicle;
        public Entity TollRoad;
        public VehicleType VehicleType;
        public ushort AttemptCount;
        public uint FrameBlocked;
    }

    /// <summary>
    /// Stores the original connection lane flags so they can be restored after temporary blocking.
    /// </summary>
    public struct OriginalConnectionLaneFlags : IComponentData
    {
        public uint Value;
    }
}
