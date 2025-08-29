using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    // Marks a toll booth prefab instance and keeps references needed for barrier logic.
    public struct TollBoothPrefabData : IComponentData, ISerializable
    {
        // Owning road edge (set during spawn)
        public Entity BelongsToHighwayTollbooth;

        // Barrier blocker pseudo vehicle entity
        public Entity BarrierBlockerEntity;

        // The lane we control (road vehicular sub-lane)
        public Entity ControlledLane;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out BelongsToHighwayTollbooth);
            reader.Read(out BarrierBlockerEntity);
            // Fix: Use CompareTo for Version vs int comparison
            if (reader.context.version.CompareTo(1) >= 0) // simple forward-friendly pattern
            {
                reader.Read(out ControlledLane);
            }
            else
            {
                ControlledLane = Entity.Null;
            }
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(BelongsToHighwayTollbooth);
            writer.Write(BarrierBlockerEntity);
            writer.Write(ControlledLane);
        }
    }

}