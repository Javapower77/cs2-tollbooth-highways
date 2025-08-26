using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;

namespace TollboothHighways.Systems
{
    public partial class ManualTollBoothBarrierSystem : GameSystemBase
    {
        private SimulationSystem _simulation;
        private EntityQuery _tollBoothQuery;

        private ComponentLookup<TollBoothPrefabData> _tollBoothDataRO;
        private ComponentLookup<TollBoothManualData> _manualDataRO;
        private ComponentLookup<LaneSignal> _laneSignalRW;
        private ComponentLookup<Game.Objects.TrafficLight> _trafficLightRW;
        private ComponentLookup<Car> _carRO;
        private BufferLookup<Game.Net.SubLane> _subLaneRO;
        private BufferLookup<LaneObject> _laneObjectRO;
        private BufferLookup<Game.Objects.SubObject> _subObjectRO;

        private NativeHashMap<Entity, VehicleProcessingState> _vehicleStates;
        private NativeHashMap<Entity, BarrierState> _barrierStates;

        private const float STOP_ZONE_END = 0.15f;
        private const float PASS_THRESHOLD = 0.55f;
        private const float POSITION_DELTA_STOP = 0.0008f;
        private const uint CLOSE_DELAY_FRAMES = 30;
        private const uint CLEANUP_TIMEOUT_FRAMES = 1200;
        private const uint PERIODIC_PROCESS_FRAMES = 30; // faster refresh to keep petitioner accurate

        private struct VehicleProcessingState
        {
            public Entity TollBooth;
            public Entity Lane;
            public uint DetectedFrame;
            public uint ProcessingStartFrame;
            public bool HasStartedProcessing;
            public bool PaymentCompleted;
            public float ProcessingSeconds;
            public float2 LastCurvePos;
        }

        private struct BarrierState
        {
            public bool IsOpen;
            public uint LastUpdateFrame;
            public uint OpenedFrame;
            public uint LastVehiclePassedFrame;
            public Entity CurrentVehicle;
            public LaneSignalType LastLaneSignal;
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 8; // increase responsiveness

        protected override void OnCreate()
        {
            base.OnCreate();

            _simulation = World.GetOrCreateSystemManaged<SimulationSystem>();

            _tollBoothQuery = GetEntityQuery(
                ComponentType.ReadOnly<TollBoothPrefabData>(),
                ComponentType.ReadOnly<TollBoothManualData>(),
                ComponentType.ReadOnly<TollBoothSpawned>());

            RequireForUpdate(_tollBoothQuery);

            _tollBoothDataRO = GetComponentLookup<TollBoothPrefabData>(true);
            _manualDataRO = GetComponentLookup<TollBoothManualData>(true);
            _laneSignalRW = GetComponentLookup<LaneSignal>(false);
            _trafficLightRW = GetComponentLookup<Game.Objects.TrafficLight>(false);
            _carRO = GetComponentLookup<Car>(true);
            _subLaneRO = GetBufferLookup<Game.Net.SubLane>(true);
            _laneObjectRO = GetBufferLookup<LaneObject>(true);
            _subObjectRO = GetBufferLookup<Game.Objects.SubObject>(true);

            _vehicleStates = new NativeHashMap<Entity, VehicleProcessingState>(256, Allocator.Persistent);
            _barrierStates = new NativeHashMap<Entity, BarrierState>(128, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            _tollBoothDataRO.Update(this);
            _manualDataRO.Update(this);
            _laneSignalRW.Update(this);
            _trafficLightRW.Update(this);
            _carRO.Update(this);
            _subLaneRO.Update(this);
            _laneObjectRO.Update(this);
            _subObjectRO.Update(this);

            uint frame = _simulation.frameIndex;

            var booths = _tollBoothQuery.ToEntityArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < booths.Length; i++)
                {
                    ProcessBooth(booths[i], frame);
                }
            }
            finally
            {
                booths.Dispose();
            }

            CleanupVehicleStates(frame);
        }

        private void ProcessBooth(Entity booth, uint frame)
        {
            if (!_tollBoothDataRO.TryGetComponent(booth, out var boothData) ||
                !_manualDataRO.TryGetComponent(booth, out var manual))
                return;

            Entity road = boothData.BelongsToHighwayTollbooth;
            if (road == Entity.Null)
                return;

            if (!TryGetBarrierLane(road, out Entity lane) || !_laneSignalRW.HasComponent(lane))
                return;

            TryGetTrafficLight(road, out Entity trafficLight);

            if (!_barrierStates.TryGetValue(booth, out var barrier))
            {
                barrier = new BarrierState
                {
                    IsOpen = false,
                    LastUpdateFrame = frame - (PERIODIC_PROCESS_FRAMES + 1),
                    OpenedFrame = 0,
                    LastVehiclePassedFrame = 0,
                    CurrentVehicle = Entity.Null,
                    LastLaneSignal = LaneSignalType.Stop
                };
            }

            var laneSignal = _laneSignalRW[lane];

            bool needProcess = (frame - barrier.LastUpdateFrame) >= PERIODIC_PROCESS_FRAMES ||
                               laneSignal.m_Signal != barrier.LastLaneSignal;

            if (!needProcess)
                return;

            barrier.LastUpdateFrame = frame;
            barrier.LastLaneSignal = laneSignal.m_Signal;

            var vehicles = GetApproachingVehicles(lane);
            try
            {
                if (vehicles.Length == 0)
                {
                    // No vehicles: close if open for enough time
                    if (barrier.IsOpen && (frame - barrier.OpenedFrame) > CLOSE_DELAY_FRAMES)
                    {
                        CloseBarrier(ref barrier, booth, lane, trafficLight, ref laneSignal, "idle");
                    }
                    barrier.CurrentVehicle = Entity.Null;
                    _barrierStates[booth] = barrier;
                    return;
                }

                // First vehicle in queue
                var first = vehicles[0];
                Entity vehicle = first.m_LaneObject;
                float2 curvePos = first.m_CurvePosition;

                // Refresh petitioner EACH update if vehicle changed
                if (laneSignal.m_Petitioner != vehicle && !barrier.IsOpen)
                {
                    laneSignal.m_Petitioner = vehicle;
                }

                barrier.CurrentVehicle = vehicle;

                if (!_vehicleStates.TryGetValue(vehicle, out var vstate))
                {
                    vstate = new VehicleProcessingState
                    {
                        TollBooth = booth,
                        Lane = lane,
                        DetectedFrame = frame,
                        ProcessingStartFrame = 0,
                        HasStartedProcessing = false,
                        PaymentCompleted = false,
                        ProcessingSeconds = math.max(1f, manual.ProcessingTime),
                        LastCurvePos = curvePos
                    };

                    // Force CLOSED barrier state and ensure blocker
                    ForceClosedSetup(ref barrier, booth, lane, trafficLight, ref laneSignal, vehicle);
                    _vehicleStates[vehicle] = vstate;
                    _laneSignalRW[lane] = laneSignal;
                    _barrierStates[booth] = barrier;
                    return;
                }
                else
                {
                    bool stopped = math.distance(vstate.LastCurvePos, curvePos) < POSITION_DELTA_STOP ||
                                   curvePos.x <= STOP_ZONE_END;
                    vstate.LastCurvePos = curvePos;

                    if (!vstate.HasStartedProcessing && stopped)
                    {
                        vstate.HasStartedProcessing = true;
                        vstate.ProcessingStartFrame = frame;
                    }

                    if (vstate.HasStartedProcessing && !vstate.PaymentCompleted)
                    {
                        uint elapsed = frame - vstate.ProcessingStartFrame;
                        uint required = (uint)(vstate.ProcessingSeconds * GetTicksPerSecond());
                        if (elapsed >= required)
                        {
                            vstate.PaymentCompleted = true;
                            OpenBarrier(ref barrier, booth, lane, trafficLight, ref laneSignal, "payment complete");
                        }
                        else
                        {
                            // Keep closed and refresh blocker/petitioner
                            if (!barrier.IsOpen)
                                EnsurePetitionerAndBlocker(booth, lane, ref laneSignal, vehicle);
                        }
                    }

                    if (barrier.IsOpen && vstate.PaymentCompleted)
                    {
                        bool passed = curvePos.x > PASS_THRESHOLD || !VehicleStillInLane(lane, vehicle);
                        if (passed)
                        {
                            barrier.LastVehiclePassedFrame = frame;

                            // Clear petitioner early once vehicle moves beyond stop zone
                            if (laneSignal.m_Petitioner == vehicle)
                                laneSignal.m_Petitioner = Entity.Null;

                            if ((frame - barrier.LastVehiclePassedFrame) > CLOSE_DELAY_FRAMES)
                            {
                                CloseBarrier(ref barrier, booth, lane, trafficLight, ref laneSignal, "vehicle passed");
                            }
                        }
                    }

                    _vehicleStates[vehicle] = vstate;
                }

                _laneSignalRW[lane] = laneSignal;
                _barrierStates[booth] = barrier;
            }
            finally
            {
                vehicles.Dispose();
            }
        }

        private void ForceClosedSetup(ref BarrierState barrier, Entity booth, Entity lane, Entity trafficLight, ref LaneSignal laneSignal, Entity vehicle)
        {
            if (_tollBoothDataRO.TryGetComponent(booth, out var boothData))
            {
                laneSignal.m_Petitioner = vehicle;
                if (boothData.BarrierBlockerEntity != Entity.Null)
                    laneSignal.m_Blocker = boothData.BarrierBlockerEntity;
            }
            laneSignal.m_Signal = LaneSignalType.Stop;
            if (trafficLight != Entity.Null && _trafficLightRW.HasComponent(trafficLight))
            {
                var light = _trafficLightRW[trafficLight];
                light.m_State = Game.Objects.TrafficLightState.Red;
                _trafficLightRW[trafficLight] = light;
            }
            barrier.IsOpen = false;
        }

        private void EnsurePetitionerAndBlocker(Entity booth, Entity lane, ref LaneSignal laneSignal, Entity vehicle)
        {
            bool changed = false;

            if (laneSignal.m_Petitioner != vehicle)
            {
                laneSignal.m_Petitioner = vehicle;
                changed = true;
            }

            if (_tollBoothDataRO.TryGetComponent(booth, out var boothData))
            {
                if (boothData.BarrierBlockerEntity != Entity.Null && laneSignal.m_Blocker != boothData.BarrierBlockerEntity)
                {
                    laneSignal.m_Blocker = boothData.BarrierBlockerEntity;
                    changed = true;
                }
            }

            if (laneSignal.m_Signal != LaneSignalType.Stop)
            {
                laneSignal.m_Signal = LaneSignalType.Stop;
                changed = true;
            }

            if (changed)
                _laneSignalRW[lane] = laneSignal;
        }

        private void OpenBarrier(ref BarrierState barrier, Entity booth, Entity lane, Entity trafficLight, ref LaneSignal laneSignal, string reason)
        {
            laneSignal.m_Signal = LaneSignalType.Go;
            laneSignal.m_Blocker = Entity.Null; // REQUIREMENT: blocker cleared when open
            _laneSignalRW[lane] = laneSignal;

            if (trafficLight != Entity.Null && _trafficLightRW.HasComponent(trafficLight))
            {
                var light = _trafficLightRW[trafficLight];
                light.m_State = Game.Objects.TrafficLightState.Green;
                _trafficLightRW[trafficLight] = light;
            }

            barrier.IsOpen = true;
            barrier.OpenedFrame = _simulation.frameIndex;
            LogUtil.Info($"ManualTollBoothBarrierSystem: OPEN barrier booth {booth.Index} ({reason})");
        }

        private void CloseBarrier(ref BarrierState barrier, Entity booth, Entity lane, Entity trafficLight, ref LaneSignal laneSignal, string reason)
        {
            if (_tollBoothDataRO.TryGetComponent(booth, out var boothData) && boothData.BarrierBlockerEntity != Entity.Null)
                laneSignal.m_Blocker = boothData.BarrierBlockerEntity;

            laneSignal.m_Signal = LaneSignalType.Stop;
            laneSignal.m_Petitioner = Entity.Null; // reset petitioner when closed & no vehicle queued yet
            _laneSignalRW[lane] = laneSignal;

            if (trafficLight != Entity.Null && _trafficLightRW.HasComponent(trafficLight))
            {
                var light = _trafficLightRW[trafficLight];
                light.m_State = Game.Objects.TrafficLightState.Red;
                _trafficLightRW[trafficLight] = light;
            }

            barrier.IsOpen = false;
            LogUtil.Info($"ManualTollBoothBarrierSystem: CLOSE barrier booth {booth.Index} ({reason})");
        }

        private bool TryGetBarrierLane(Entity road, out Entity laneEntity)
        {
            laneEntity = Entity.Null;
            if (!_subLaneRO.TryGetBuffer(road, out var subLanes))
                return false;

            for (int i = 0; i < subLanes.Length; i++)
            {
                if (subLanes[i].m_PathMethods == PathMethod.Road)
                {
                    var l = subLanes[i].m_SubLane;
                    if (_laneSignalRW.HasComponent(l))
                    {
                        laneEntity = l;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryGetTrafficLight(Entity road, out Entity trafficLightEntity)
        {
            trafficLightEntity = Entity.Null;
            if (_subObjectRO.TryGetBuffer(road, out var subs))
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    var s = subs[i].m_SubObject;
                    if (_trafficLightRW.HasComponent(s))
                    {
                        trafficLightEntity = s;
                        return true;
                    }
                }
            }
            return false;
        }

        private NativeArray<LaneObject> GetApproachingVehicles(Entity lane)
        {
            if (!_laneObjectRO.TryGetBuffer(lane, out var buffer))
                return new NativeArray<LaneObject>(0, Allocator.Temp);

            var list = new NativeList<LaneObject>(Allocator.Temp);
            for (int i = 0; i < buffer.Length; i++)
            {
                var lo = buffer[i];
                if (_carRO.HasComponent(lo.m_LaneObject) && lo.m_CurvePosition.x <= 0.6f)
                    list.Add(lo);
            }
            list.Sort(new LaneObjectCurvePosComparer());
            return list.AsArray();
        }

        private bool VehicleStillInLane(Entity lane, Entity vehicle)
        {
            if (!_laneObjectRO.TryGetBuffer(lane, out var buffer))
                return false;
            for (int i = 0; i < buffer.Length; i++)
                if (buffer[i].m_LaneObject == vehicle)
                    return true;
            return false;
        }

        private void CleanupVehicleStates(uint frame)
        {
            var keys = _vehicleStates.GetKeyArray(Allocator.Temp);
            var toRemove = new NativeList<Entity>(Allocator.Temp);
            try
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    var veh = keys[i];
                    var vs = _vehicleStates[veh];
                    bool remove = false;

                    if (frame - vs.DetectedFrame > CLEANUP_TIMEOUT_FRAMES)
                        remove = true;
                    else if (!VehicleStillInLane(vs.Lane, veh) && (frame - vs.DetectedFrame) > 120)
                        remove = true;

                    if (remove)
                        toRemove.Add(veh);
                }
                for (int i = 0; i < toRemove.Length; i++)
                    _vehicleStates.Remove(toRemove[i]);
            }
            finally
            {
                keys.Dispose();
                toRemove.Dispose();
            }
        }

        private float GetTicksPerSecond()
        {
            var ft = _simulation.frameTime;
            return ft > 0 ? 1f / ft : 60f;
        }

        protected override void OnDestroy()
        {
            if (_vehicleStates.IsCreated) _vehicleStates.Dispose();
            if (_barrierStates.IsCreated) _barrierStates.Dispose();
            base.OnDestroy();
        }

        private struct LaneObjectCurvePosComparer : IComparer<LaneObject>
        {
            public int Compare(LaneObject a, LaneObject b) => a.m_CurvePosition.x.CompareTo(b.m_CurvePosition.x);
        }
    }
}