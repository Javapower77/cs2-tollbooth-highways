using Game.Vehicles;
using Game.Pathfind;
using TollboothHighways.Path;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Entities;
using Game;
using TollboothHighways.Domain.Enums;
using Game.Simulation;

namespace TollboothHighways.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PersonalCarAISystem))]
    [UpdateBefore(typeof(AmbulanceAISystem))]
    [UpdateBefore(typeof(DeliveryTruckAISystem))]
    [UpdateBefore(typeof(FireEngineAISystem))]
    [UpdateBefore(typeof(GarbageTruckAISystem))]
    [UpdateBefore(typeof(HearseAISystem))]
    [UpdateBefore(typeof(MaintenanceVehicleAISystem))]
    [UpdateBefore(typeof(PoliceCarAISystem))]
    [UpdateBefore(typeof(PostVanAISystem))]
    [UpdateBefore(typeof(TaxiAISystem))]
    [UpdateBefore(typeof(TransportCarAISystem))]
    public partial class TollPathParameterInjectionSystem : GameSystemBase
    {
        protected override void OnUpdate()
        {
            new InjectJob
            {
                EntityManager = EntityManager
            }.Run();
        }

        [BurstCompile]
        private partial struct InjectJob : IJobEntity
        {
            public EntityManager EntityManager;

            void Execute(Entity e,
                         ref PathfindParameters parameters,
                         in Car car)
            {
                // Only add if not already pending? Usually safe to always OR.
                var vType = VehiclesUtil.GetVehicleTypeStatic(e, EntityManager);
                if (vType == Domain.Enums.VehicleType.None)
                    return;

                if (!VehiclesUtil.vehicleTypeToGroupMap.TryGetValue(vType, out var group))
                    group = VehicleGroup.PrivateTransport;

                parameters.m_Methods |= TollPathMethods.FromVehicleGroup(group);
            }
        }
    }
}