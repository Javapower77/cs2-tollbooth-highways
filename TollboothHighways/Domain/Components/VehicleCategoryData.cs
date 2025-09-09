using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Bitmask describing capabilities of a spawned vehicle.
    /// Bits (LSB):
    ///  1 = Private (personal car / motorcycle / taxi)
    ///  2 = Transit (bus / public transport)
    ///  4 = Heavy  (delivery / cargo / truck + trailer)
    ///  8 = Service (police, ambulance, fire, maintenance, hearse, prisoner, post, evacuation)
    /// A vehicle may have multiple bits (e.g. a taxi counts as Private; leave Transit out unless you want it to use bus lanes).
    /// </summary>
    public struct VehicleCategoryData : IComponentData
    {
        public byte Mask;
        public bool Has(byte bit) => (Mask & bit) != 0;
    }
}