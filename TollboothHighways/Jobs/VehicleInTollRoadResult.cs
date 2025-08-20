using Game.Prefabs;
using System;
using TollHighways.Domain;
using Unity.Entities;

namespace TollHighways.Jobs
{
    // Result structure for job communication
    public struct VehicleInTollRoadResult
    {
        public Entity TollRoadEntity;
        public Entity VehiclePrefabRef;
    }

}