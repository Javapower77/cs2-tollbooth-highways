using Colossal.Serialization.Entities;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    // This component is used to mark a prefab as a toll road.
    // And also to serialize it when the game is saved.
    public struct TollBoothPrefabData : IComponentData, ISerializable
    {
        public Entity BelongsToHighwayTollbooth;
        public Entity BarrierBlockerEntity;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out BelongsToHighwayTollbooth);
            reader.Read(out BarrierBlockerEntity);
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(BelongsToHighwayTollbooth);
            writer.Write(BarrierBlockerEntity);
        }
    }

}