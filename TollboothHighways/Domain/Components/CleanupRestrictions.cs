using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Temporary component used to schedule cleanup of access restrictions after pathfinding completes.
    /// </summary>
    public struct CleanupRestrictions : IComponentData
    {
        /// <summary>
        /// Frame number when the cleanup should be processed
        /// </summary>
        public int ProcessingFrame;
    }
}