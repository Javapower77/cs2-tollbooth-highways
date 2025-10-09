using TollboothHighways.Domain.Enums;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    public struct LaneBlockedByVehicle : IComponentData
    {
        public Entity Vehicle;
        public Entity TollRoad;
        public VehicleType VehicleType;
        public ushort AttemptCount;
        public uint FrameBlocked;
    }

    public struct OriginalCarLaneFlags : IComponentData
    {
        public uint Value;
    }

    public struct OriginalConnectionLaneFlags : IComponentData
    {
        public uint Value;
    }
}