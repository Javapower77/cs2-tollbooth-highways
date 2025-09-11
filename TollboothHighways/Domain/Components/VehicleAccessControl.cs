using Unity.Entities;
using TollboothHighways.Domain.Enums;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Component to track vehicle access control state for restricted tollbooth roads
    /// </summary>
    public struct VehicleAccessControl : IComponentData
    {
        /// <summary>
        /// The road entity this vehicle is attempting to access
        /// </summary>
        public Entity RestrictedRoad;
        
        /// <summary>
        /// Type of vehicle detected
        /// </summary>
        public VehicleType DetectedVehicleType;
        
        /// <summary>
        /// Group this vehicle belongs to
        /// </summary>
        public VehicleGroup VehicleGroup;
        
        /// <summary>
        /// Whether access is currently denied
        /// </summary>
        public bool AccessDenied;
        
        /// <summary>
        /// Frame when access was first denied (for timeout purposes)
        /// </summary>
        public uint DeniedStartFrame;
        
        /// <summary>
        /// Original target position before rerouting
        /// </summary>
        public Unity.Mathematics.float3 OriginalTargetPosition;
    }

    /// <summary>
    /// Temporary component for vehicles that need to be rerouted away from restricted roads
    /// </summary>
    public struct VehicleRerouteRequest : IComponentData
    {
        /// <summary>
        /// The restricted road to avoid
        /// </summary>
        public Entity RestrictedRoad;
        
        /// <summary>
        /// Reroute attempt counter
        /// </summary>
        public int RerouteAttempts;
        
        /// <summary>
        /// Maximum reroute attempts before giving up
        /// </summary>
        public const int MaxRerouteAttempts = 3;
    }
}