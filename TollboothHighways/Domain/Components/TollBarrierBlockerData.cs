using Colossal.Serialization.Entities;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Data for a toll barrier blocker entity
    /// </summary>
    public struct TollBarrierBlockerData : IComponentData, ISerializable
    {
        /// <summary>
        /// The tollbooth entity this blocker is associated with
        /// </summary>
        public Entity TollBoothEntity;
        
        /// <summary>
        /// The processing time for vehicles at this barrier
        /// </summary>
        public float ProcessingTime;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out TollBoothEntity);
            reader.Read(out ProcessingTime);
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(TollBoothEntity);
            writer.Write(ProcessingTime);
        }
    }
}