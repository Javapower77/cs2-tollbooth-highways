using System;

namespace TollboothHighways.Domain.Enums
{
    /// <summary>
    /// Defines the main vehicle groups for tollbooth access control.
    /// Maps to the game's vehicle type components.
    /// </summary>
    public enum VehicleGroup : byte
    {
        /// <summary>
        /// Private cars, personal vehicles, taxis (default category).
        /// </summary>
        PrivateTransport = 0,

        /// <summary>
        /// Delivery trucks, cargo vehicles, industrial transport.
        /// </summary>
        Trucks = 1,

        /// <summary>
        /// Buses, trams, trains, public transportation.
        /// </summary>
        PublicTransport = 2,

        /// <summary>
        /// Police, ambulance, fire trucks, hearses, maintenance vehicles, garbage trucks, postal vans.
        /// </summary>
        ServiceVehicles = 3,

        /// <summary>
        /// All vehicle types.
        /// </summary>
        All = 4
    }
}
