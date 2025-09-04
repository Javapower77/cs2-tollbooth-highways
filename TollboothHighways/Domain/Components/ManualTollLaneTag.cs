using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Tag component placed on each sub-lane (lane entity) that belongs to a MANUAL tollbooth road.
    /// Used by pathfinding cost modifier logic.
    /// </summary>
    public struct ManualTollLaneTag : IComponentData { }

    /// <summary>
    /// Holds precomputed cost values for a manual toll lane. 
    /// (Optional) Lets you tune personal vs non-personal dynamically without re-scanning ownership.
    /// </summary>
    public struct LanePersonalCarCost : IComponentData
    {
        /// <summary>Raw additional cost applied for PersonalCar (normally small or zero).</summary>
        public int PersonalCarAdditionalCost;

        /// <summary>Raw additional cost applied for any non-personal vehicle (set huge to discourage usage).</summary>
        public int NonPersonalAdditionalCost;
    }
}