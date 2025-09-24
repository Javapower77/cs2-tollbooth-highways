using Game.Pathfind;
using Game.Vehicles;
using TollboothHighways.Domain;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TollPathParameterInjectionSystem))]
    public partial struct TollLaneViolationSystem : ISystem
    {
        private ComponentLookup<TollAllowedMethod> _tollAllowed;

        public void OnCreate(ref SystemState state)
        {
            _tollAllowed = state.GetComponentLookup<TollAllowedMethod>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _tollAllowed.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            new ViolationJob
            {
                TollAllowedLookup = _tollAllowed,
                EntityManager = state.EntityManager,
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel();

            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private partial struct ViolationJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<TollAllowedMethod> TollAllowedLookup;
            [ReadOnly] public EntityManager EntityManager;
            public EntityCommandBuffer.ParallelWriter ECB;

            void Execute([EntityIndexInQuery] int sortKey,
                         Entity vehicle,
                         ref PathOwner pathOwner,
                         in CarCurrentLane currentLane,
                         in Car car)
            {
                if (currentLane.m_Lane == Entity.Null)
                    return;

                if (!TollAllowedLookup.HasComponent(currentLane.m_Lane))
                    return; // Not a toll lane

                var vType = VehiclesUtil.GetVehicleTypeStatic(vehicle, EntityManager);
                if (!VehiclesUtil.vehicleTypeToGroupMap.TryGetValue(vType, out var group))
                    group = VehicleGroup.PrivateTransport;

                var allowedMask = TollAllowedLookup[currentLane.m_Lane].Value;
                var haveMask    = TollPathMethods.FromVehicleGroup(group);

                if ((allowedMask & haveMask) == 0)
                {
                    if ((pathOwner.m_State & PathFlags.Pending) == 0)
                    {
                        pathOwner.m_State |= PathFlags.Obsolete | PathFlags.Divert;
                        ECB.SetComponent(sortKey, vehicle, pathOwner);
                    }
                }
            }
        }
    }
}