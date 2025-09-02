using Unity.Entities;
using Unity.Mathematics;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Vehicle is currently stopped / paying at a tollbooth.
    /// </summary>
    public struct TollPaymentProcessing : IComponentData
    {
        public Entity TollBooth;
        public uint   StartFrame;
        public uint   DurationFrames;
        public float  OriginalMaxSpeed;
        public float3 StopPosition;
        public Entity RoadEntity;          // NEW: owning road (for barrier control)
    }

    public struct TollPaymentCooldown : IComponentData
    {
        public Entity TollBooth;
        public uint   ExpireFrame;
    }
}