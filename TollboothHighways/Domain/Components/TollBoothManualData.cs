using Colossal.Serialization.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Represents a manual toll booth with a specified vehicle processing time.
    /// </summary>
    /// <remarks>This struct is used to define the behavior of a manual toll booth, including the time
    /// required to process a vehicle. It supports serialization and deserialization for data persistence or
    /// transfer.</remarks>
    public struct TollBoothManualData : IComponentData, ISerializable
    {        
        /// <summary>
        /// The time it takes to process a vehicle at this manual toll booth.
        /// </summary>
        public float ProcessingTime;

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out ProcessingTime);
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(ProcessingTime);
        }
    }
}