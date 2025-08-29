using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    // Phase: 0=Closed, 1=Open
    public struct TollBarrierState : IComponentData
    {
        public byte Phase;
        public int OpenFramesRemaining;
        public int OpenFrameDuration;  // cached duration
        public Entity Lane;
        public Entity Blocker;
        public Entity CurrentVehicle;  // vehicle being processed
    }
}