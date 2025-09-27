using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Component to mark vehicles that have had their paths recreated due to tollbooth restrictions.
    /// This prevents the system from repeatedly processing the same vehicle.
    /// </summary>
    public struct RepathCreated : IComponentData
    {
        /// <summary>
        /// Frame when the repath was created (for debugging purposes)
        /// </summary>
        public uint CreationFrame;
        
        /// <summary>
        /// Number of toll roads that caused the repath
        /// </summary>
        public int TollRoadCount;
    }
}