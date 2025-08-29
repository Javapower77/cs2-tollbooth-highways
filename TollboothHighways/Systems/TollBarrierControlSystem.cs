using Game.Net;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TollboothHighways.Systems
{
    [UpdateAfter(typeof(Game.Simulation.TrafficLightSystem))] // ensure post light updates (even if not used)
    public partial class TollBarrierControlSystem : SystemBase
    {
        private BufferLookup<LaneObject> _laneObjects;
        private ComponentLookup<LaneSignal> _laneSignals;
        private ComponentLookup<Game.Net.CarLane> _carLaneData;

        protected override void OnCreate()
        {
            _laneObjects = GetBufferLookup<LaneObject>(true);
            _laneSignals = GetComponentLookup<LaneSignal>(false);
            _carLaneData = GetComponentLookup<Game.Net.CarLane>(false);

            RequireForUpdate(GetEntityQuery(
                ComponentType.ReadOnly<TollBoothPrefabData>(),
                ComponentType.ReadWrite<TollBarrierState>()));
        }

        protected override void OnUpdate()
        {
            _laneObjects.Update(this);
            _laneSignals.Update(this);
            _carLaneData.Update(this);

            var laneObjects = _laneObjects;
            var laneSignals = _laneSignals;
            var carLaneData = _carLaneData;

            Entities
                .WithName("ManualTollBarrierControl")
                .WithBurst(FloatMode.Default, FloatPrecision.Standard, true)
                .ForEach((ref TollBarrierState barrier, in TollBoothPrefabData tollData) =>
                {
                    if (barrier.Lane == Entity.Null)
                        return;

                    if (!laneSignals.HasComponent(barrier.Lane))
                        return;

                    // Defensive: ensure flags remain
                    if (carLaneData.HasComponent(barrier.Lane))
                    {
                        var cl = carLaneData[barrier.Lane];
                        var needed = Game.Net.CarLaneFlags.LevelCrossing | Game.Net.CarLaneFlags.Stop;
                        if ((cl.m_Flags & needed) != needed)
                        {
                            cl.m_Flags |= needed;
                            carLaneData[barrier.Lane] = cl;
                        }
                    }

                    DynamicBuffer<LaneObject> buf;
                    bool has = laneObjects.TryGetBuffer(barrier.Lane, out buf) && buf.Length > 0;

                    Entity firstVehicle = Entity.Null;
                    if (has)
                    {
                        for (int i = 0; i < buf.Length; i++)
                        {
                            var obj = buf[i].m_LaneObject;
                            if (obj != barrier.Blocker)
                            {
                                firstVehicle = obj;
                                break;
                            }
                        }
                        has = firstVehicle != Entity.Null;
                    }

                    var ls = laneSignals[barrier.Lane];
                    if (ls.m_GroupMask == 0)
                        ls.m_GroupMask = 1;

                    if (barrier.Phase == 0) // Closed
                    {
                        // Enforce STOP
                        if (ls.m_Signal != LaneSignalType.Stop ||
                            (ls.m_Flags & LaneSignalFlags.Physical) == 0)
                        {
                            ls.m_Signal = LaneSignalType.Stop;
                            ls.m_Flags |= LaneSignalFlags.Physical;
                            ls.m_Blocker = barrier.Blocker;
                        }

                        if (has)
                        {
                            barrier.Phase = 1;
                            barrier.CurrentVehicle = firstVehicle;
                            barrier.OpenFramesRemaining = math.max(5, barrier.OpenFrameDuration); // safety min

                            ls.m_Signal = LaneSignalType.Go;
                            ls.m_Flags &= ~LaneSignalFlags.Physical;
                            ls.m_Blocker = Entity.Null;
                        }
                    }
                    else // Open
                    {
                        barrier.OpenFramesRemaining--;

                        bool stillPresent = false;
                        if (barrier.CurrentVehicle != Entity.Null && has)
                        {
                            for (int i = 0; i < buf.Length; i++)
                            {
                                if (buf[i].m_LaneObject == barrier.CurrentVehicle)
                                {
                                    stillPresent = true;
                                    break;
                                }
                            }
                        }

                        if (barrier.OpenFramesRemaining <= 0 || !stillPresent)
                        {
                            barrier.Phase = 0;
                            barrier.CurrentVehicle = Entity.Null;
                            ls.m_Signal = LaneSignalType.Stop;
                            ls.m_Flags |= LaneSignalFlags.Physical;
                            ls.m_Blocker = barrier.Blocker;
                        }
                    }

                    laneSignals[barrier.Lane] = ls;
                }).Schedule();
        }
    }
}