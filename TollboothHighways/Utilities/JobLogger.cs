using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace TollboothHighways.Utilities
{
    /// <summary>
    /// A thread-safe logger for use within IJobParallelFor jobs.
    /// </summary>
    public struct JobLogger : IDisposable
    {
        private NativeList<FixedString512Bytes> m_LogMessages;

        /// <summary>
        /// A thread-safe writer that adds log messages from a parallel job.
        /// </summary>
        public struct Writer
        {
            internal NativeList<FixedString512Bytes>.ParallelWriter m_ParallelWriter;

            /// <summary>
            /// Writes a log message, automatically prepending the current thread ID.
            /// </summary>
            /// <param name="message">The message to log.</param>
            public void Log(in FixedString512Bytes message)
            {
                // Get the current worker thread index and prepend it to the message.
                m_ParallelWriter.AddNoResize($"[Thread {JobsUtility.ThreadIndex}] {message}");
            }
        }

        /// <summary>
        /// Initializes the logger. Call this before scheduling the job.
        /// </summary>
        /// <param name="allocator">The memory allocator to use.</param>
        public void Initialize(Allocator allocator)
        {
            m_LogMessages = new NativeList<FixedString512Bytes>(allocator);
        }

        /// <summary>
        /// Gets a writer to be used inside a job.
        /// </summary>
        public Writer GetWriter()
        {
            return new Writer { m_ParallelWriter = m_LogMessages.AsParallelWriter() };
        }

        /// <summary>
        /// Flushes all collected log messages to the main game log. Call this after the job has completed.
        /// </summary>
        public void Flush()
        {
            foreach (var message in m_LogMessages)
            {
                LogUtil.Info(message.ToString());
            }
            m_LogMessages.Clear();
        }

        /// <summary>
        /// Disposes the native collection.
        /// </summary>
        public void Dispose()
        {
            if (m_LogMessages.IsCreated)
            {
                m_LogMessages.Dispose();
            }
        }
    }

}
