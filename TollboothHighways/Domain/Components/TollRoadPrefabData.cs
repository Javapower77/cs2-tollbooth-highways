using Colossal.Serialization.Entities;
using Unity.Entities;

namespace Domain.Components
{
    // This component is used to mark a prefab as a toll road.
    // And also to serialize it when the game is saved.
    public struct TollRoadPrefabData : IComponentData, ISerializable
    {
        /// <summary>
        /// The tollbooth entity associated with this toll road segment.
        /// Each road segment can have only one tollbooth entity.
        /// </summary>
        public Entity AssociatedTollbooth;

        /// <summary>
        /// Flag indicating if this road segment has an active tollbooth.
        /// This provides quick lookup without checking the entity validity.
        /// </summary>
        public bool HasActiveTollbooth;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out Entity tollboothEntity);
            AssociatedTollbooth = tollboothEntity;
            reader.Read(out bool hasActiveTollbooth);
            HasActiveTollbooth = hasActiveTollbooth;    
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(AssociatedTollbooth);
            writer.Write(HasActiveTollbooth);
        }
    }

}