using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using System.Collections.Generic;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System responsible for detecting vehicles approaching manual toll barriers and controlling barrier operations.
    /// IMPORTANT: Barriers are CLOSED by default and only open for vehicle passage.
    /// This system monitors lane objects to detect vehicles and triggers barrier opening/closing based on vehicle presence and toll payment status.
    /// Now works with DIRECT lane signal control (no TrafficLights system integration).
    /// </summary>
    public partial class TollBoothBarrierControlSystem : GameSystemBase
    {
        private EntityQuery m_ManualTollBoothQuery;
        private EntityQuery m_VehicleQuery;
        private TollBoothSpawnSystem m_TollBoothSpawnSystem;
        private SimulationSystem m_SimulationSystem;

        // Lookups for component data
        private ComponentLookup<TollBoothPrefabData> m_TollBoothData;
        private ComponentLookup<TollBoothManualData> m_TollBoothManualData;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadData;
        private ComponentLookup<CarCurrentLane> m_CarCurrentLaneData;
        private ComponentLookup<Car> m_CarData;
        private ComponentLookup<Transform> m_TransformData;
        private ComponentLookup<PrefabRef> m_PrefabRefData;
        private ComponentLookup<LaneSignal> m_LaneSignalData;
        private ComponentLookup<Curve> m_CurveData;

        // REMOVED: TrafficLights integration lookups (no longer needed)
        // private ComponentLookup<TrafficLights> m_TrafficLightsData;
        private ComponentLookup<Game.Objects.TrafficLight> m_TrafficLightObjectData;

        private BufferLookup<Game.Net.SubLane> m_SubLaneData;
        private BufferLookup<LaneObject> m_LaneObjectData;
        private BufferLookup<Game.Objects.SubObject> m_SubObjectData;

        // Vehicle processing state tracking
        private NativeHashMap<Entity, VehicleProcessingState> m_VehicleStates;

        // Barrier state tracking to prevent unnecessary state changes
        private NativeHashMap<Entity, BarrierState> m_BarrierStates;

        /// <summary>
        /// Represents the processing state of a vehicle at a toll booth
        /// </summary>
        private struct VehicleProcessingState
        {
            public Entity TollBoothEntity;
            public Entity RoadEntity;
            public Entity LaneEntity;
            public uint DetectedFrame;
            public uint ProcessingStartFrame;
            public bool IsBeingProcessed;
            public bool PaymentCompleted;
            public float ProcessingTime;
            public float2 LastKnownPosition;
        }

        /// <summary>
        /// Represents the current barrier state to prevent unnecessary changes
        /// IMPORTANT: Default state is CLOSED (IsOpen = false)
        /// UPDATED: Removed TrafficLightState tracking since we're not using TrafficLights anymore
        /// </summary>
        private struct BarrierState
        {
            public bool IsOpen;                                    // Default: false (CLOSED)
            public uint LastUpdateFrame;                          // Frame of last update
            public bool HasVehicleWaiting;                        // Default: false (no vehicles)
            public bool ShouldBeClosed;                           // Default: true (should be closed)
            public uint LastVehiclePassedFrame;                   // Frame when last vehicle passed through
            public LaneSignalType LastLaneSignalState;            // Track actual lane signal state
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Update every 16 frames (roughly 3.75 times per second at 60 FPS) to ensure responsiveness
            return 16;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_TollBoothSpawnSystem = World.GetOrCreateSystemManaged<TollBoothSpawnSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            // Initialize lookups
            m_TollBoothData = GetComponentLookup<TollBoothPrefabData>(true);
            m_TollBoothManualData = GetComponentLookup<TollBoothManualData>(true);
            m_TollRoadData = GetComponentLookup<TollRoadPrefabData>(true);
            m_CarCurrentLaneData = GetComponentLookup<CarCurrentLane>(true);
            m_CarData = GetComponentLookup<Car>(true);
            m_TransformData = GetComponentLookup<Transform>(true);
            m_PrefabRefData = GetComponentLookup<PrefabRef>(true);
            m_LaneSignalData = GetComponentLookup<LaneSignal>(true);
            m_CurveData = GetComponentLookup<Curve>(true);

            // REMOVED: TrafficLights integration lookups (no longer needed)
            // m_TrafficLightsData = GetComponentLookup<TrafficLights>(true);
            m_TrafficLightObjectData = GetComponentLookup<Game.Objects.TrafficLight>(true);

            m_SubLaneData = GetBufferLookup<Game.Net.SubLane>(true);
            m_LaneObjectData = GetBufferLookup<LaneObject>(true);
            m_SubObjectData = GetBufferLookup<Game.Objects.SubObject>(true);

            // Initialize vehicle state tracking
            m_VehicleStates = new NativeHashMap<Entity, VehicleProcessingState>(100, Allocator.Persistent);
            m_BarrierStates = new NativeHashMap<Entity, BarrierState>(50, Allocator.Persistent);

            // Query for manual toll booths
            m_ManualTollBoothQuery = GetEntityQuery(
                ComponentType.ReadOnly<TollBoothPrefabData>(),
                ComponentType.ReadOnly<TollBoothManualData>(),
                ComponentType.ReadOnly<TollBoothSpawned>()
            );

            // Query for vehicles
            m_VehicleQuery = GetEntityQuery(
                ComponentType.ReadOnly<Car>(),
                ComponentType.ReadOnly<CarCurrentLane>(),
                ComponentType.ReadOnly<Transform>(),
                ComponentType.Exclude<Deleted>()
            );

            RequireForUpdate(m_ManualTollBoothQuery);

            LogUtil.Info("TollBoothBarrierControlSystem: System created and initialized - BARRIERS DEFAULT TO CLOSED (DIRECT CONTROL)");
        }

        protected override void OnUpdate()
        {
            // Update lookups (removed TrafficLights lookup)
            m_TollBoothData.Update(this);
            m_TollBoothManualData.Update(this);
            m_TollRoadData.Update(this);
            m_CarCurrentLaneData.Update(this);
            m_CarData.Update(this);
            m_TransformData.Update(this);
            m_PrefabRefData.Update(this);
            m_LaneSignalData.Update(this);
            m_CurveData.Update(this);
            // REMOVED: m_TrafficLightsData.Update(this);
            m_TrafficLightObjectData.Update(this);
            m_SubLaneData.Update(this);
            m_LaneObjectData.Update(this);
            m_SubObjectData.Update(this);

            uint currentFrame = m_SimulationSystem.frameIndex;

            // Process each manual toll booth
            var tollBoothEntities = m_ManualTollBoothQuery.ToEntityArray(Allocator.TempJob);

            try
            {
                for (int i = 0; i < tollBoothEntities.Length; i++)
                {
                    Entity tollBoothEntity = tollBoothEntities[i];
                    ProcessTollBoothBarrier(tollBoothEntity, currentFrame);
                }

                // Clean up old vehicle states
                CleanupOldVehicleStates(currentFrame);
            }
            finally
            {
                tollBoothEntities.Dispose();
            }
        }

        /// <summary>
        /// Processes barrier control for a specific toll booth with DIRECT lane signal control
        /// IMPORTANT: Ensures barrier is CLOSED by default unless vehicle is actively passing through
        /// </summary>
        /// <param name="tollBoothEntity">The toll booth entity to process</param>
        /// <param name="currentFrame">Current simulation frame</param>
        private void ProcessTollBoothBarrier(Entity tollBoothEntity, uint currentFrame)
        {
            // Get toll booth data
            if (!m_TollBoothData.TryGetComponent(tollBoothEntity, out var tollBoothData) ||
                !m_TollBoothManualData.TryGetComponent(tollBoothEntity, out var manualData))
            {
                return;
            }

            Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;
            if (roadEntity == Entity.Null)
            {
                return;
            }

            // Check if road has toll booth association
            if (!m_TollRoadData.TryGetComponent(roadEntity, out var tollRoadData) ||
                !tollRoadData.HasActiveTollbooth)
            {
                return;
            }

            // Get current barrier state (initialized as CLOSED by default)
            if (!m_BarrierStates.TryGetValue(tollBoothEntity, out var barrierState))
            {
                barrierState = new BarrierState
                {
                    IsOpen = false,                                    // BARRIER STARTS CLOSED
                    LastUpdateFrame = currentFrame,                   // Current frame
                    HasVehicleWaiting = false,                        // No vehicles waiting
                    ShouldBeClosed = true,                            // Should be closed by default
                    LastVehiclePassedFrame = 0,                       // No vehicles have passed yet
                    LastLaneSignalState = LaneSignalType.Stop         // Default to STOP signal
                };
            }

            // Get the lane with the barrier
            Entity barrierLaneEntity = GetBarrierLaneEntity(roadEntity);
            if (barrierLaneEntity == Entity.Null)
            {
                return;
            }

            // Check current lane signal state to understand barrier status
            LaneSignalType currentLaneSignal = GetCurrentLaneSignalState(barrierLaneEntity);
            bool shouldProcessBarrier = ShouldProcessBarrier(currentLaneSignal, barrierState, currentFrame);

            if (!shouldProcessBarrier)
            {
                // Skip processing if lane signal state hasn't changed significantly
                return;
            }

            // Check for vehicles in the barrier lane
            var vehiclesAtBarrier = GetVehiclesInLane(barrierLaneEntity);

            try
            {
                bool hasVehicles = vehiclesAtBarrier.Length > 0;
                barrierState.HasVehicleWaiting = hasVehicles;

                if (hasVehicles)
                {
                    // Process the first vehicle in the lane (closest to barrier)
                    Entity vehicleEntity = vehiclesAtBarrier[0].m_LaneObject;
                    float2 vehiclePosition = vehiclesAtBarrier[0].m_CurvePosition;

                    ProcessVehicleAtBarrier(tollBoothEntity, roadEntity, barrierLaneEntity, vehicleEntity, vehiclePosition, manualData, currentFrame, ref barrierState);
                }
                else
                {
                    // NO VEHICLES AT BARRIER - ENSURE IT'S CLOSED
                    bool hasPendingVehicles = HasPendingVehicles(tollBoothEntity);

                    // Close barrier if it's open and no vehicles are pending
                    if (barrierState.IsOpen && !hasPendingVehicles)
                    {
                        RequestBarrierClose(tollBoothEntity, ref barrierState, "no vehicles present");
                    }

                    // Aggressively ensure closure if no activity for too long
                    uint framesSinceLastVehicle = currentFrame - barrierState.LastVehiclePassedFrame;
                    if (framesSinceLastVehicle > 180 && barrierState.IsOpen) // 3 seconds with no activity
                    {
                        RequestBarrierClose(tollBoothEntity, ref barrierState, "timeout with no vehicle activity");
                    }

                    // Mark that barrier should be closed
                    barrierState.ShouldBeClosed = true;
                }

                // Update barrier state
                barrierState.LastLaneSignalState = currentLaneSignal;
                barrierState.LastUpdateFrame = currentFrame;
                m_BarrierStates[tollBoothEntity] = barrierState;
            }
            finally
            {
                vehiclesAtBarrier.Dispose();
            }
        }

        /// <summary>
        /// Gets the current lane signal state for a barrier lane entity
        /// UPDATED: Now directly checks lane signal instead of TrafficLights
        /// </summary>
        /// <param name="laneEntity">The barrier lane entity</param>
        /// <returns>Current LaneSignalType</returns>
        private LaneSignalType GetCurrentLaneSignalState(Entity laneEntity)
        {
            if (m_LaneSignalData.TryGetComponent(laneEntity, out var laneSignal))
            {
                return laneSignal.m_Signal;
            }
            return LaneSignalType.Stop; // Default to stopped
        }

        /// <summary>
        /// Determines if barrier processing should occur based on lane signal state changes
        /// UPDATED: Now uses lane signal state instead of TrafficLights state
        /// </summary>
        /// <param name="currentLaneSignal">Current lane signal state</param>
        /// <param name="barrierState">Current barrier state</param>
        /// <param name="currentFrame">Current frame</param>
        /// <returns>True if processing should occur</returns>
        private bool ShouldProcessBarrier(LaneSignalType currentLaneSignal, BarrierState barrierState, uint currentFrame)
        {
            // Always process if this is the first time or state has changed
            if (barrierState.LastLaneSignalState != currentLaneSignal)
            {
                return true;
            }

            // Process periodically if there's a vehicle waiting (every 2 seconds)
            if (barrierState.HasVehicleWaiting && (currentFrame - barrierState.LastUpdateFrame) > 120)
            {
                return true;
            }

            // Process less frequently if no vehicles (every 5 seconds)
            if (!barrierState.HasVehicleWaiting && (currentFrame - barrierState.LastUpdateFrame) > 300)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if there are pending vehicles being processed
        /// </summary>
        /// <param name="tollBoothEntity">The toll booth entity</param>
        /// <returns>True if there are pending vehicles</returns>
        private bool HasPendingVehicles(Entity tollBoothEntity)
        {
            var keys = m_VehicleStates.GetKeyArray(Allocator.TempJob);
            try
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    var vehicleState = m_VehicleStates[keys[i]];
                    if (vehicleState.TollBoothEntity.Equals(tollBoothEntity) &&
                        (vehicleState.IsBeingProcessed || vehicleState.PaymentCompleted))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                keys.Dispose();
            }
            return false;
        }

        /// <summary>
        /// Requests barrier to open (only if not already open and vehicle needs to pass)
        /// IMPORTANT: Only opens when explicitly needed for vehicle passage
        /// UPDATED: Now uses TollBoothSpawnSystem's direct control methods
        /// </summary>
        /// <param name="tollBoothEntity">The toll booth entity</param>
        /// <param name="barrierState">Reference to barrier state</param>
        /// <param name="reason">Reason for opening (for logging)</param>
        private void RequestBarrierOpen(Entity tollBoothEntity, ref BarrierState barrierState, string reason = "vehicle payment completed")
        {
            if (!barrierState.IsOpen)
            {
                m_TollBoothSpawnSystem.OpenBarrier(tollBoothEntity);
                barrierState.IsOpen = true;
                barrierState.ShouldBeClosed = false; // Temporarily should not be closed
                LogUtil.Info($"TollBoothBarrierControlSystem: OPENED barrier for tollbooth {tollBoothEntity.Index} - {reason}");
            }
        }

        /// <summary>
        /// Requests barrier to close (this is the DEFAULT behavior)
        /// IMPORTANT: This is the default state - barriers should always be closed unless actively needed
        /// UPDATED: Now uses TollBoothSpawnSystem's direct control methods
        /// </summary>
        /// <param name="tollBoothEntity">The toll booth entity</param>
        /// <param name="barrierState">Reference to barrier state</param>
        /// <param name="reason">Reason for closing (for logging)</param>
        private void RequestBarrierClose(Entity tollBoothEntity, ref BarrierState barrierState, string reason = "returning to default closed state")
        {
            if (barrierState.IsOpen)
            {
                m_TollBoothSpawnSystem.CloseBarrier(tollBoothEntity);
                barrierState.IsOpen = false;
                barrierState.ShouldBeClosed = true;
                LogUtil.Info($"TollBoothBarrierControlSystem: CLOSED barrier for tollbooth {tollBoothEntity.Index} - {reason}");
            }
        }

        /// <summary>
        /// Gets the barrier lane entity for a road
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        /// <returns>The lane entity with the barrier, or Entity.Null if not found</returns>
        private Entity GetBarrierLaneEntity(Entity roadEntity)
        {
            if (!m_SubLaneData.TryGetBuffer(roadEntity, out var subLanes))
            {
                return Entity.Null;
            }

            // Find the lane with a lane signal (indicating it has a barrier)
            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity laneEntity = subLanes[i].m_SubLane;
                if (subLanes[i].m_PathMethods == PathMethod.Road &&
                    m_LaneSignalData.HasComponent(laneEntity))
                {
                    return laneEntity;
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// Gets all vehicles currently in a specific lane, sorted by position
        /// </summary>
        /// <param name="laneEntity">The lane entity to check</param>
        /// <returns>Array of lane objects representing vehicles</returns>
        private NativeArray<LaneObject> GetVehiclesInLane(Entity laneEntity)
        {
            if (!m_LaneObjectData.TryGetBuffer(laneEntity, out var laneObjects))
            {
                return new NativeArray<LaneObject>(0, Allocator.TempJob);
            }

            // Filter for vehicles (entities with Car component) and check they're close to the barrier
            var vehicles = new NativeList<LaneObject>(laneObjects.Length, Allocator.TempJob);

            for (int i = 0; i < laneObjects.Length; i++)
            {
                var laneObject = laneObjects[i];
                if (m_CarData.HasComponent(laneObject.m_LaneObject))
                {
                    // Check if vehicle is close enough to the barrier (within detection range)
                    float detectionRange = 0.2f; // 20% of the lane length for better detection
                    if (laneObject.m_CurvePosition.x <= detectionRange)
                    {
                        vehicles.Add(laneObject);
                    }
                }
            }

            // Sort by position on the lane (closest to barrier first)
            vehicles.Sort(new LaneObjectPositionComparer());

            return vehicles.ToArray(Allocator.TempJob);
        }

        /// <summary>
        /// Processes a vehicle at the barrier with improved state management
        /// IMPORTANT: Only opens barrier when vehicle has completed payment
        /// </summary>
        /// <param name="tollBoothEntity">The toll booth entity</param>
        /// <param name="roadEntity">The road entity</param>
        /// <param name="laneEntity">The lane entity</param>
        /// <param name="vehicleEntity">The vehicle entity</param>
        /// <param name="vehiclePosition">The vehicle's position on the lane</param>
        /// <param name="manualData">Manual toll booth data</param>
        /// <param name="currentFrame">Current simulation frame</param>
        /// <param name="barrierState">Reference to barrier state</param>
        private void ProcessVehicleAtBarrier(Entity tollBoothEntity, Entity roadEntity, Entity laneEntity, Entity vehicleEntity,
            float2 vehiclePosition, TollBoothManualData manualData, uint currentFrame, ref BarrierState barrierState)
        {
            // Check if we're already tracking this vehicle
            if (!m_VehicleStates.TryGetValue(vehicleEntity, out var vehicleState))
            {
                // New vehicle detected at barrier
                vehicleState = new VehicleProcessingState
                {
                    TollBoothEntity = tollBoothEntity,
                    RoadEntity = roadEntity,
                    LaneEntity = laneEntity,
                    DetectedFrame = currentFrame,
                    ProcessingStartFrame = 0,
                    IsBeingProcessed = false,
                    PaymentCompleted = false,
                    ProcessingTime = manualData.ProcessingTime,
                    LastKnownPosition = vehiclePosition
                };

                m_VehicleStates[vehicleEntity] = vehicleState;

                LogUtil.Info($"TollBoothBarrierControlSystem: New vehicle {vehicleEntity.Index} detected at tollbooth {tollBoothEntity.Index} at position {vehiclePosition.x:F3} - BARRIER REMAINS CLOSED");
            }
            else
            {
                // Update last known position
                vehicleState.LastKnownPosition = vehiclePosition;
                m_VehicleStates[vehicleEntity] = vehicleState;
            }

            // Update vehicle state
            ProcessVehicleState(vehicleEntity, ref vehicleState, currentFrame, ref barrierState);
            m_VehicleStates[vehicleEntity] = vehicleState;
        }

        /// <summary>
        /// Processes the state machine for a vehicle at the toll booth with improved barrier control
        /// IMPORTANT: Barrier only opens AFTER payment is completed
        /// </summary>
        /// <param name="vehicleEntity">The vehicle entity</param>
        /// <param name="vehicleState">The vehicle's processing state</param>
        /// <param name="currentFrame">Current simulation frame</param>
        /// <param name="barrierState">Reference to barrier state</param>
        private void ProcessVehicleState(Entity vehicleEntity, ref VehicleProcessingState vehicleState, uint currentFrame, ref BarrierState barrierState)
        {
            const uint FRAMES_PER_SECOND = 60; // Assuming 60 FPS

            if (!vehicleState.IsBeingProcessed)
            {
                // Start processing the vehicle - BARRIER STAYS CLOSED during processing
                vehicleState.IsBeingProcessed = true;
                vehicleState.ProcessingStartFrame = currentFrame;

                LogUtil.Info($"TollBoothBarrierControlSystem: Started processing vehicle {vehicleEntity.Index} at tollbooth {vehicleState.TollBoothEntity.Index} - BARRIER REMAINS CLOSED FOR PAYMENT");
            }
            else if (!vehicleState.PaymentCompleted)
            {
                // Check if processing time has elapsed
                uint framesElapsed = currentFrame - vehicleState.ProcessingStartFrame;
                float secondsElapsed = (float)framesElapsed / FRAMES_PER_SECOND;

                if (secondsElapsed >= vehicleState.ProcessingTime)
                {
                    // Payment completed - NOW open barrier
                    vehicleState.PaymentCompleted = true;

                    // Update vehicle statistics
                    var vehicleType = DetermineVehicleType(vehicleEntity);
                    float tollAmount = CalculateTollAmount(vehicleEntity, vehicleType);

                    m_TollBoothSpawnSystem.UpdateVehicleStatistics(vehicleState.TollBoothEntity, vehicleType, tollAmount);

                    // ONLY NOW open the barrier for vehicle passage
                    RequestBarrierOpen(vehicleState.TollBoothEntity, ref barrierState, $"vehicle {vehicleEntity.Index} completed payment (${tollAmount:F2})");

                    // Mark when vehicle passed
                    barrierState.LastVehiclePassedFrame = currentFrame;

                    LogUtil.Info($"TollBoothBarrierControlSystem: Payment completed for vehicle {vehicleEntity.Index}, OPENED barrier for passage. Toll: ${tollAmount:F2}");
                }
            }
        }

        /// <summary>
        /// Determines the type of vehicle for toll calculation
        /// </summary>
        /// <param name="vehicleEntity">The vehicle entity</param>
        /// <returns>The vehicle type</returns>
        private Domain.Enums.VehicleType DetermineVehicleType(Entity vehicleEntity)
        {
            // Check vehicle prefab to determine type
            if (m_PrefabRefData.TryGetComponent(vehicleEntity, out var prefabRef))
            {
                // Check if it's a large vehicle (truck, bus, etc.)
                if (EntityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicleEntity))
                {
                    return Domain.Enums.VehicleType.Truck;
                }

                if (EntityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicleEntity))
                {
                    return Domain.Enums.VehicleType.Bus;
                }
            }

            // Default to passenger car
            return Domain.Enums.VehicleType.PersonalCar;
        }

        /// <summary>
        /// Calculates the toll amount for a vehicle based on settings
        /// </summary>
        /// <param name="vehicleEntity">The vehicle entity</param>
        /// <param name="vehicleType">The vehicle type</param>
        /// <returns>The toll amount</returns>
        private float CalculateTollAmount(Entity vehicleEntity, Domain.Enums.VehicleType vehicleType)
        {
            var settings = Mod.Settings;
            if (settings == null)
            {
                // Fallback values if settings not available
                return vehicleType switch
                {
                    Domain.Enums.VehicleType.PersonalCar => 2.50f,
                    Domain.Enums.VehicleType.Truck => 5.00f,
                    Domain.Enums.VehicleType.Bus => 4.00f,
                    _ => 2.50f
                };
            }

            // Use settings-based pricing (assuming non-peak for now)
            return vehicleType switch
            {
                Domain.Enums.VehicleType.PersonalCar => settings.PrivateCarPeakPrice,
                Domain.Enums.VehicleType.Truck => settings.TruckPeakPrice,
                Domain.Enums.VehicleType.Bus => settings.BusPeakPrice,
                _ => settings.PrivateCarPeakPrice
            };
        }

        /// <summary>
        /// Cleans up old vehicle states for vehicles that are no longer present
        /// IMPORTANT: Ensures barrier closes after vehicle passes through
        /// </summary>
        /// <param name="currentFrame">Current simulation frame</param>
        private void CleanupOldVehicleStates(uint currentFrame)
        {
            const uint CLEANUP_THRESHOLD_FRAMES = 600; // 10 seconds at 60 FPS
            const uint PASSAGE_CONFIRMATION_FRAMES = 120; // 2 seconds to confirm vehicle has passed

            var keysToRemove = new NativeList<Entity>(Allocator.TempJob);

            try
            {
                var keys = m_VehicleStates.GetKeyArray(Allocator.TempJob);

                try
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        Entity vehicleEntity = keys[i];
                        var vehicleState = m_VehicleStates[vehicleEntity];

                        // Check if vehicle still exists and is in a valid state
                        bool shouldCleanup = false;

                        if (!EntityManager.Exists(vehicleEntity))
                        {
                            shouldCleanup = true;
                        }
                        else if (currentFrame - vehicleState.DetectedFrame > CLEANUP_THRESHOLD_FRAMES)
                        {
                            // Vehicle has been tracked for too long, probably moved on
                            shouldCleanup = true;
                        }
                        else if (vehicleState.PaymentCompleted)
                        {
                            // Check if vehicle has moved significantly forward (passed through)
                            Entity barrierLane = vehicleState.LaneEntity;
                            if (barrierLane != Entity.Null)
                            {
                                var vehiclesInLane = GetVehiclesInLane(barrierLane);
                                try
                                {
                                    bool vehicleStillAtBarrier = false;
                                    for (int j = 0; j < vehiclesInLane.Length; j++)
                                    {
                                        if (vehiclesInLane[j].m_LaneObject == vehicleEntity)
                                        {
                                            vehicleStillAtBarrier = true;
                                            break;
                                        }
                                    }

                                    // If vehicle is no longer at barrier and enough time has passed, close barrier
                                    if (!vehicleStillAtBarrier)
                                    {
                                        // Wait a bit to ensure vehicle fully passed before closing
                                        uint framesSincePayment = currentFrame - vehicleState.ProcessingStartFrame;

                                        if (framesSincePayment > PASSAGE_CONFIRMATION_FRAMES)
                                        {
                                            // Vehicle has passed through, close the barrier
                                            if (m_BarrierStates.TryGetValue(vehicleState.TollBoothEntity, out var barrierState))
                                            {
                                                RequestBarrierClose(vehicleState.TollBoothEntity, ref barrierState, $"vehicle {vehicleEntity.Index} passed through");
                                                barrierState.LastVehiclePassedFrame = currentFrame;
                                                m_BarrierStates[vehicleState.TollBoothEntity] = barrierState;
                                            }

                                            shouldCleanup = true;

                                            LogUtil.Info($"TollBoothBarrierControlSystem: Vehicle {vehicleEntity.Index} passed through, CLOSED barrier for tollbooth {vehicleState.TollBoothEntity.Index}");
                                        }
                                    }
                                }
                                finally
                                {
                                    vehiclesInLane.Dispose();
                                }
                            }
                        }

                        if (shouldCleanup)
                        {
                            keysToRemove.Add(vehicleEntity);
                        }
                    }
                }
                finally
                {
                    keys.Dispose();
                }

                // Remove old entries
                for (int i = 0; i < keysToRemove.Length; i++)
                {
                    m_VehicleStates.Remove(keysToRemove[i]);
                }
            }
            finally
            {
                keysToRemove.Dispose();
            }
        }

        protected override void OnDestroy()
        {
            if (m_VehicleStates.IsCreated)
            {
                m_VehicleStates.Dispose();
            }
            if (m_BarrierStates.IsCreated)
            {
                m_BarrierStates.Dispose();
            }
            base.OnDestroy();
        }

        /// <summary>
        /// Comparer for sorting lane objects by their position on the lane
        /// </summary>
        private struct LaneObjectPositionComparer : IComparer<LaneObject>
        {
            public int Compare(LaneObject x, LaneObject y)
            {
                return x.m_CurvePosition.x.CompareTo(y.m_CurvePosition.x);
            }
        }
    }
}