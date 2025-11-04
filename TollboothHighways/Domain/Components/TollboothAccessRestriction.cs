using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Custom access restriction component for tollbooth roads.
    /// Controls which vehicle types are allowed on specific toll roads.
    /// </summary>
    public struct TollboothAccessRestriction : IComponentData
    {
        /// <summary>
        /// The toll road entity this restriction applies to.
        /// </summary>
        public Entity TollRoadEntity;
        
        /// <summary>
        /// Whether private cars are allowed on this toll road.
        /// </summary>
        public bool AllowPrivateCars;
        
        /// <summary>
        /// Whether trucks are allowed on this toll road.
        /// </summary>
        public bool AllowTrucks;
        
        /// <summary>
        /// Whether public transport is allowed on this toll road.
        /// </summary>
        public bool AllowPublicTransport;
        
        /// <summary>
        /// Whether service vehicles are allowed on this toll road.
        /// </summary>
        public bool AllowServiceVehicles;
    }
}