using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

namespace TollboothHighways.Utilities
{
    /// <summary>
    /// A thread-safe logger for use within Burst-compiled IJobParallelFor jobs.
    /// </summary>
    public struct JobLogger : IDisposable
    {
        private NativeList<FixedString4096Bytes> m_LogMessages;
        private bool m_IsEnabled;
        private bool m_IsInitialized;

        /// <summary>
        /// A thread-safe writer that adds log messages from a parallel job.
        /// Must be Burst-compatible - no managed types or string interpolation.
        /// </summary>
        [BurstCompile]
        public struct Writer
        {
            [NativeDisableParallelForRestriction]
            internal NativeList<FixedString4096Bytes>.ParallelWriter m_ParallelWriter;
            internal bool m_IsEnabled;

            /// <summary>
            /// Writes a log message, automatically prepending the current thread ID.
            /// Uses Burst-compatible FixedString formatting.
            /// </summary>
            /// <param name="message">The message to log.</param>
            public void Log(in FixedString512Bytes message)
            {
                if (!m_IsEnabled) return;

                FixedString4096Bytes formatted = default;
                formatted.Append('[');
                formatted.Append('T');
                formatted.Append(JobsUtility.ThreadIndex);
                formatted.Append(']');
                formatted.Append(' ');
                formatted.Append(message);
                m_ParallelWriter.AddNoResize(formatted);
            }

            /// <summary>
            /// Logs a message with an entity index for vehicle tracking.
            /// </summary>
            public void LogVehicle(int entityIndex, int entityVersion, in FixedString512Bytes message)
            {
                if (!m_IsEnabled) return;

                FixedString4096Bytes formatted = default;
                formatted.Append('[');
                formatted.Append('T');
                formatted.Append(JobsUtility.ThreadIndex);
                formatted.Append(']');
                formatted.Append(' ');
                formatted.Append('E');
                formatted.Append('(');
                formatted.Append(entityIndex);
                formatted.Append(':');
                formatted.Append(entityVersion);
                formatted.Append(')');
                formatted.Append(' ');
                formatted.Append(message);
                m_ParallelWriter.AddNoResize(formatted);
            }

            /// <summary>
            /// Logs a numeric value with a label.
            /// </summary>
            public void LogValue(in FixedString128Bytes label, int value)
            {
                if (!m_IsEnabled) return;

                FixedString4096Bytes formatted = default;
                formatted.Append('[');
                formatted.Append('T');
                formatted.Append(JobsUtility.ThreadIndex);
                formatted.Append(']');
                formatted.Append(' ');
                formatted.Append(label);
                formatted.Append(':');
                formatted.Append(' ');
                formatted.Append(value);
                m_ParallelWriter.AddNoResize(formatted);
            }
        }

        /// <summary>
        /// Returns true if the logger has been initialized.
        /// </summary>
        public bool IsInitialized => m_IsInitialized && m_LogMessages.IsCreated;

        /// <summary>
        /// Initializes the logger with a specified capacity.
        /// Call this on the main thread before scheduling the job.
        /// </summary>
        /// <param name="allocator">The memory allocator to use.</param>
        /// <param name="initialCapacity">Initial capacity for log messages.</param>
        /// <param name="isEnabled">Whether logging is enabled (use for debug builds).</param>
        public void Initialize(Allocator allocator, int initialCapacity = 256, bool isEnabled = true)
        {
            if (m_IsInitialized && m_LogMessages.IsCreated)
            {
                m_LogMessages.Dispose();
            }
            
            m_LogMessages = new NativeList<FixedString4096Bytes>(initialCapacity, allocator);
            m_IsEnabled = isEnabled;
            m_IsInitialized = true;
        }

        /// <summary>
        /// Updates the enabled state at runtime (e.g., from settings).
        /// </summary>
        public void SetEnabled(bool isEnabled)
        {
            m_IsEnabled = isEnabled;
        }

        /// <summary>
        /// Sets the capacity to handle expected message count.
        /// Call before scheduling if you know the approximate count.
        /// </summary>
        public void SetCapacity(int capacity)
        {
            if (!m_IsInitialized || !m_LogMessages.IsCreated) return;
            
            if (m_LogMessages.Capacity < capacity)
            {
                m_LogMessages.SetCapacity(capacity);
            }
        }

        /// <summary>
        /// Gets a writer to be used inside a Burst-compiled job.
        /// Returns a disabled writer if logger is not initialized.
        /// </summary>
        public Writer GetWriter()
        {
            if (!m_IsInitialized || !m_LogMessages.IsCreated)
            {
                return new Writer
                {
                    m_IsEnabled = false
                };
            }
            
            return new Writer
            {
                m_ParallelWriter = m_LogMessages.AsParallelWriter(),
                m_IsEnabled = m_IsEnabled
            };
        }

        /// <summary>
        /// Gets the number of logged messages.
        /// </summary>
        public int MessageCount => (m_IsInitialized && m_LogMessages.IsCreated) ? m_LogMessages.Length : 0;

        /// <summary>
        /// Flushes all collected log messages to the main game log.
        /// Call this on the main thread after the job has completed.
        /// </summary>
        public void Flush()
        {
            if (!m_IsInitialized || !m_LogMessages.IsCreated || m_LogMessages.Length == 0) 
                return;

            for (int i = 0; i < m_LogMessages.Length; i++)
            {
                var message = m_LogMessages[i];
                var messageStr = message.ToString();
                
                try
                {
                    if (Mod.Settings != null)
                    {
                        LogUtil.Info(messageStr, LogUtil.LogTarget.General);
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[TollboothHighways] {messageStr}");
                    }
                }
                catch (System.Exception)
                {
                    // Fallback to Unity's built-in logger if Colossal logger fails
                    try
                    {
                        UnityEngine.Debug.Log($"[TollboothHighways] {messageStr}");
                    }
                    catch
                    {
                        // Silently ignore if even Unity logging fails
                    }
                }
            }
            m_LogMessages.Clear();
        }

        /// <summary>
        /// Disposes the native collection. Must be called to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            if (m_LogMessages.IsCreated)
            {
                m_LogMessages.Dispose();
            }
            m_IsInitialized = false;
        }
    }
}