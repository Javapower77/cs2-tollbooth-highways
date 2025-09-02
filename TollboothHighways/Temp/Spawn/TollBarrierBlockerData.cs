using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    public struct TollBarrierBlockerData : IComponentData
    {
        public Entity TollBoothEntity { get; set; }
        public float ProcessingTime { get; set; }
    }
}