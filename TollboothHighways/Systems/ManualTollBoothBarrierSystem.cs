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
        private ComponentLookup<Game.Net.CarLane> _carLaneRW;
        private ComponentLookup<Game.Objects.TrafficLight> _trafficLightRW;
        private ComponentLookup<Car> _carRO;
        private ComponentLookup<Blocker> _vehicleBlockerRW;
        private ComponentLookup<LaneReservation> _laneReservationRW;
        private ComponentLookup<Transform> _transformRO;
        private BufferLookup<Game.Net.SubLane> _subLaneRO;
        private BufferLookup<LaneObject> _laneObjectRO;
        private BufferLookup<Game.Objects.SubObject> _subObjectRO;
        private BufferLookup<BlockedLane> _blockedLaneRW;

        // CRITICAL: Add this lookup to remove TrafficLights components
        private ComponentLookup<TrafficLights> _trafficLightsRW;

        private NativeHashMap<Entity, VehicleProcessingState> _vehicleStates;
        private NativeHashMap<Entity, BarrierState> _barrierStates;
        private NativeHashMap<Entity, Entity> _createdBlockers; // Track created blocker entities

        private const float STOP_ZONE_END = 0.15f;
        private const float PASS_THRESHOLD = 0.55f;
        private const float POSITION_DELTA_STOP = 0.0008f;
        private const uint CLOSE_DELAY_FRAMES = 30;
        private const uint CLEANUP_TIMEOUT_FRAMES = 1200;
        private const uint PERIODIC_PROCESS_FRAMES = 30;

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
            public bool TrafficLightsRemoved;
            public Entity CreatedBlocker; // Track blocker entity for this barrier
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase) => 8;

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
            _carLaneRW = GetComponentLookup<Game.Net.CarLane>(false);
            _vehicleBlockerRW = GetComponentLookup<Blocker>(false);
            _laneReservationRW = GetComponentLookup<LaneReservation>(false);
            _transformRO = GetComponentLookup<Transform>(true);
            _blockedLaneRW = GetBufferLookup<BlockedLane>(false);

            // CRITICAL: Add TrafficLights lookup for removal
            _trafficLightsRW = GetComponentLookup<TrafficLights>(false);

            _vehicleStates = new NativeHashMap<Entity, VehicleProcessingState>(256, Allocator.Persistent);
            _barrierStates = new NativeHashMap<Entity, BarrierState>(128, Allocator.Persistent);
            _createdBlockers = new NativeHashMap<Entity, Entity>(128, Allocator.Persistent);
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
            _trafficLightsRW.Update(this);
            _carLaneRW.Update(this);
            _laneReservationRW.Update(this);
            _transformRO.Update(this);
            _blockedLaneRW.Update(this);

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
                    LastLaneSignal = LaneSignalType.Stop,
                    TrafficLightsRemoved = false,
                    CreatedBlocker = Entity.Null
                };
            }

            // CRITICAL: Remove TrafficLights component to prevent interference
            if (!barrier.TrafficLightsRemoved && _trafficLightsRW.HasComponent(road))
            {
                EntityManager.RemoveComponent<TrafficLights>(road);
                barrier.TrafficLightsRemoved = true;
                LogUtil.Info($"ManualTollBoothBarrierSystem: Removed TrafficLights component from road {road.Index} to prevent interference");
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

                // CRITICAL: Set proper priority and blocking to ensure vehicles stop
                EnsureVehicleStopsAtBarrier(booth, lane, ref laneSignal, vehicle, barrier.IsOpen, ref barrier);

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

                    // Force CLOSED barrier state and ensure proper blocking
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
                            // Keep closed and ensure proper blocking
                            if (!barrier.IsOpen)
                                EnsureVehicleStopsAtBarrier(booth, lane, ref laneSignal, vehicle, false, ref barrier);
                        }
                    }

                    if (barrier.IsOpen && vstate.PaymentCompleted)
                    {
                        bool passed = curvePos.x > PASS_THRESHOLD || !VehicleStillInLane(lane, vehicle);
                        if (passed)
                        {
                            barrier.LastVehiclePassedFrame = frame;

                            // Clear petitioner once vehicle passes
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

        /// <summary>
        /// CRITICAL: Creates a proper blocker entity that vehicles will recognize and stop for
        /// </summary>
        private Entity CreateProperBlocker(Entity lane)
        {
            // Create blocker entity with proper components
            var blocker = EntityManager.CreateEntity();

            // Add Blocker component with appropriate type
            EntityManager.AddComponentData(blocker, new Blocker
            {
                m_Type = BlockerType.Signal,
                m_Blocker = blocker
            });

            // Add Transform component at the barrier position
            if (_transformRO.TryGetComponent(lane, out var laneTransform))
            {
                EntityManager.AddComponentData(blocker, new Transform
                {
                    m_Position = laneTransform.m_Position,
                    m_Rotation = laneTransform.m_Rotation
                });
            }

            // Add to created blockers for tracking
            _createdBlockers[lane] = blocker;

            LogUtil.Info($"ManualTollBoothBarrierSystem: Created proper blocker entity {blocker.Index} for lane {lane.Index}");
            return blocker;
        }

        /// <summary>
        /// CRITICAL: Ensures vehicles stop at the barrier by setting proper lane signal properties AND creating lane reservations
        /// </summary>
        private void EnsureVehicleStopsAtBarrier(Entity booth, Entity lane, ref LaneSignal laneSignal, Entity vehicle, bool isOpen, ref BarrierState barrier)
        {
            if (isOpen)
            {
                // Barrier is open - allow passage
                laneSignal.m_Signal = LaneSignalType.Go;
                laneSignal.m_Blocker = Entity.Null;
                laneSignal.m_Petitioner = Entity.Null;
                laneSignal.m_Priority = 0;

                // Remove lane reservation to allow passage
                if (_laneReservationRW.HasComponent(lane))
                {
                    EntityManager.RemoveComponent<LaneReservation>(lane);
                }
            }
            else
            {
                // Barrier is closed - force stop
                laneSignal.m_Signal = LaneSignalType.Stop;
                laneSignal.m_Petitioner = vehicle;
                laneSignal.m_Priority = 100; // High priority to ensure stopping

                // Get or create a proper blocker
                Entity blocker = Entity.Null;

                if (_tollBoothDataRO.TryGetComponent(booth, out var boothData) &&
                    boothData.BarrierBlockerEntity != Entity.Null &&
                    EntityManager.Exists(boothData.BarrierBlockerEntity))
                {
                    blocker = boothData.BarrierBlockerEntity;
                }
                else if (barrier.CreatedBlocker != Entity.Null && EntityManager.Exists(barrier.CreatedBlocker))
                {
                    blocker = barrier.CreatedBlocker;
                }
                else
                {
                    // Create a new proper blocker
                    blocker = CreateProperBlocker(lane);
                    barrier.CreatedBlocker = blocker;
                }

                laneSignal.m_Blocker = blocker;

                // CRITICAL: Add lane reservation to physically block the lane
                if (!_laneReservationRW.HasComponent(lane))
                {
                    EntityManager.AddComponentData(lane, new LaneReservation
                    {
                        m_Blocker = blocker
                    });
                }
                else
                {
                    // Update existing reservation
                    var reservation = _laneReservationRW[lane];
                    reservation.m_Blocker = blocker;
                    //reservation.m_Priority = 200;
                    //reservation.m_CurvePos = new float2(0.1f, 0.0f);
                    _laneReservationRW[lane] = reservation;
                }

                // ADDITIONAL: Add blocked lane buffer to ensure complete blocking
                if (!_blockedLaneRW.HasBuffer(blocker))
                {
                    EntityManager.AddBuffer<BlockedLane>(blocker);
                }

                var blockedLanes = _blockedLaneRW[blocker];
                blockedLanes.Clear();
                blockedLanes.Add(new BlockedLane
                {
                    m_Lane = lane,
                    m_CurvePosition = new float2(0.1f, 0.0f)
                });
            }

            // Ensure lane signal flags support manual control
            laneSignal.m_Flags |= LaneSignalFlags.CanExtend;
            laneSignal.m_GroupMask = 1; // Assign to group 1
            laneSignal.m_Default = 0;   // Default priority when reset
        }

        private void ForceClosedSetup(ref BarrierState barrier, Entity booth, Entity lane, Entity trafficLight, ref LaneSignal laneSignal, Entity vehicle)
        {
            EnsureVehicleStopsAtBarrier(booth, lane, ref laneSignal, vehicle, false, ref barrier);

            if (trafficLight != Entity.Null && _trafficLightRW.HasComponent(trafficLight))
            {
                var light = _trafficLightRW[trafficLight];
                light.m_State = Game.Objects.TrafficLightState.Red;
                _trafficLightRW[trafficLight] = light;
            }
            barrier.IsOpen = false;
        }

        private void OpenBarrier(ref BarrierState barrier, Entity booth, Entity lane, Entity trafficLight, ref LaneSignal laneSignal, string reason)
        {
            // Get current vehicle for proper petitioner handling
            Entity currentVehicle = barrier.CurrentVehicle;
            EnsureVehicleStopsAtBarrier(booth, lane, ref laneSignal, currentVehicle, true, ref barrier);

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
            // Close barrier - no current vehicle, so use Entity.Null
            EnsureVehicleStopsAtBarrier(booth, lane, ref laneSignal, Entity.Null, false, ref barrier);

            if (trafficLight != Entity.Null && _trafficLightRW.HasComponent(trafficLight))
            {
                var light = _trafficLightRW[trafficLight];
                light.m_State = Game.Objects.TrafficLightState.Red;
                _trafficLightRW[trafficLight] = light;
            }

            barrier.IsOpen = false;
            LogUtil.Info($"ManualTollBoothBarrierSystem: CLOSE barrier booth {booth.Index} ({reason})");
        }

        // ... rest of the methods remain the same ...

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
            // Clean up created blocker entities
            var blockerKeys = _createdBlockers.GetKeyArray(Allocator.Temp);
            try
            {
                for (int i = 0; i < blockerKeys.Length; i++)
                {
                    var blocker = _createdBlockers[blockerKeys[i]];
                    if (EntityManager.Exists(blocker))
                    {
                        EntityManager.DestroyEntity(blocker);
                    }
                }
            }
            finally
            {
                blockerKeys.Dispose();
            }

            if (_vehicleStates.IsCreated) _vehicleStates.Dispose();
            if (_barrierStates.IsCreated) _barrierStates.Dispose();
            if (_createdBlockers.IsCreated) _createdBlockers.Dispose();
            base.OnDestroy();
        }

        private struct LaneObjectCurvePosComparer : IComparer<LaneObject>
        {
            public int Compare(LaneObject a, LaneObject b) => a.m_CurvePosition.x.CompareTo(b.m_CurvePosition.x);
        }
    }
}