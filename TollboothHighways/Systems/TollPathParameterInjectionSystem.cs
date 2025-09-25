using Game.Vehicles;
using Game.Pathfind;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Entities;
using Game;
using TollboothHighways.Domain.Enums;
using Game.Simulation;
using TollboothHighways.Domain;
using Unity.Collections;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System that modifies vehicles' pathfinding setup to include toll methods
    /// by working with PathOwner components that manage pathfinding state.
    /// </summary>
    public partial class TollPathParameterInjectionSystem : GameSystemBase
    {
        private EntityQuery _vehicleQuery;

        protected override void OnCreate()
        {
            // Query for vehicles that have PathOwner (pathfinding capability) and Car component
            _vehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(PathOwner),
                    typeof(Car)
                }
            });

            RequireForUpdate(_vehicleQuery);
        }

        protected override void OnUpdate()
        {
            new InjectJob
            {
                EntityManager = EntityManager
            }.ScheduleParallel(_vehicleQuery);
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private partial struct InjectJob : IJobEntity
        {
            [ReadOnly] public EntityManager EntityManager;

            void Execute(Entity e, ref PathOwner pathOwner, in Car car)
            {
                // Only process if pathfinding is needed (path is obsolete or doesn't exist)
                if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Pending)) == 0)
                    return;

                // Get vehicle type
                var vType = VehiclesUtil.GetVehicleTypeStatic(e, EntityManager);
                if (vType == Domain.Enums.VehicleType.None)
                    return;

                // Map to vehicle group
                if (!VehiclesUtil.vehicleTypeToGroupMap.TryGetValue(vType, out var group))
                    group = VehicleGroup.PrivateTransport;

                // Get toll methods for this vehicle group
                var tollMethods = TollPathMethods.FromVehicleGroup(group);

                // Store the toll methods in the PathOwner for the pathfinding system to use
                // This assumes PathOwner has a methods field - you may need to adjust based on actual structure
                pathOwner.m_ElementIndex |= (int)tollMethods; // or however toll methods are stored
            }
        }
    }
}