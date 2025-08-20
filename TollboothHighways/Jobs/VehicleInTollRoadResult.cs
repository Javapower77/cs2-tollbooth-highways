using Unity.Entities;

namespace TollboothHighways.Jobs
{
    // Result structure for job communication
    public struct VehicleInTollRoadResult
    {
        public Entity TollRoadEntity;
        public Entity VehiclePrefabRef;
    }

}