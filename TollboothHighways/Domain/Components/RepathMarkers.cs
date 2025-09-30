using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Added to a vehicle once we have forced a repath after tollbooth restrictions.
    /// </summary>
    public struct RepathCreated : IComponentData { }

    /// <summary>
    /// Added to a vehicle to indicate it does not need repathing (path already compliant or unaffected).
    /// </summary>
    public struct NoRepathNeeded : IComponentData { }

    /// <summary>
    /// Stores original lane flag so we can restore later if needed.
    /// One per modified lane (attach to the lane entity itself).
    /// </summary>
    public struct OriginalCarLaneFlags : IComponentData
    {
        public uint Value;
    }
}
