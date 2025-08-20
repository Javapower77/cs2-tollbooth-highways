using Unity.Entities;

namespace Domain.Components
{
    /// <summary>
    /// Marker component to indicate that a toll booth entity has been spawned and processed.
    /// This component is added after the initial setup (name assignment, owner linking, etc.) is complete.
    /// </summary>
    public struct TollBoothSpawned : IComponentData
    {
        // This is a marker component - no data needed
        // The presence of this component indicates the entity has been processed
    }
}