using Unity.Entities;
using Game.Net;
using TollboothHighways.Domain.Enums;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Tracks lanes temporarily blocked for specific vehicles to force rerouting
    /// </summary>
    public struct VehicleBlockedLane : IComponentData
    {
        public CarLaneFlags OriginalFlags;
        public uint UnblockAtFrame;
        public Entity BlockedForVehicle;
        public VehicleGroup VehicleGroup;
    }
}