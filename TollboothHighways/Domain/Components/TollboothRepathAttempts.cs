using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Tracks repath attempts for vehicles encountering incompatible tollbooth restrictions.
    /// Prevents infinite repath loops by limiting to MaxAttempts.
    /// </summary>
    public struct TollboothRepathAttempts : IComponentData
    {
        /// <summary>
        /// Maximum number of repath attempts before allowing the vehicle through.
        /// After 10 attempts, we assume there's no alternative path available.
        /// </summary>
        public const int MaxAttempts = 40;

        /// <summary>
        /// Number of times this vehicle has attempted to repath around tollbooth restrictions.
        /// </summary>
        public int AttemptCount;

        /// <summary>
        /// Number of path elements in the last validated path.
        /// Used to detect if the path has actually changed between validations.
        /// Prevents re-validating the exact same invalid path multiple times per frame.
        /// </summary>
        public int LastValidatedElementCount;

        /// <summary>
        /// True if the vehicle has reached maximum repath attempts and should be allowed through.
        /// </summary>
        public readonly bool HasReachedMaxAttempts => AttemptCount >= MaxAttempts;
    }
}