using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Stores the number of vehicles currently on this lane that are NOT allowed by its TollLaneAllowedMask.
    /// Recomputed every frame (no per-vehicle bookkeeping needed).
    /// </summary>
    public struct TollLaneRestrictionState : IComponentData
    {
        public int DisallowedCount;
    }
}