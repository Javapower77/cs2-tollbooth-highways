using Colossal.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using System;
using System.Collections.Generic;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System responsible for controlling the barrier at manual toll booths.
    /// Handles vehicles stopping at barriers, waiting for a designated time,
    /// and then allowing them to pass through.
    /// </summary>
    public partial class TollBoothBarrierSystem : GameSystemBase
    {
        private SimulationSystem m_SimulationSystem;
        private ComponentLookup<Blocker> m_BlockerData;
        private ComponentLookup<Game.Objects.TrafficLight> m_TrafficLightData;
        private ComponentLookup<LaneSignal> m_LaneSignalData;
        private ComponentLookup<TollBoothPrefabData> m_TollBoothData;
        private ComponentLookup<TollBoothManualData> m_TollBoothManualData;
        private ComponentLookup<CarCurrentLane> m_CarCurrentLaneData;
        private ComponentLookup<Controller> m_ControllerData;
        private ComponentLookup<Car> m_CarData;
        private BufferLookup<Game.Objects.SubObject> m_SubObjectsData;
        private BufferLookup<Game.Net.SubLane> m_SubLaneData;
        private BufferLookup<LaneObject> m_LaneObjectData;
        
        private Dictionary<Entity, ProcessingVehicle> m_ProcessingVehicles;
        
        // Structure to track vehicles being processed at toll booths
        private struct ProcessingVehicle
        {
            public Entity VehicleEntity;
            public Entity TollBoothEntity;
            public Entity RoadEntity;
            public Entity BarrierEntity;
            public Entity LaneEntity;
            public uint StartTime;
            public bool IsProcessing;
            public bool IsFinished;
            public bool HasPetitioned;
        }

        protected override void OnCreate()
        {
            LogUtil.Info("TollBoothBarrierSystem: OnCreate() - Starting system creation");

            try
            {
                base.OnCreate();

                m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
                m_ProcessingVehicles = new Dictionary<Entity, ProcessingVehicle>();

                // Initialize component lookups
                m_BlockerData = GetComponentLookup<Blocker>(false);
                m_TrafficLightData = GetComponentLookup<Game.Objects.TrafficLight>(false);
                m_LaneSignalData = GetComponentLookup<LaneSignal>(false);
                m_TollBoothData = GetComponentLookup<TollBoothPrefabData>(true);
                m_TollBoothManualData = GetComponentLookup<TollBoothManualData>(true);
                m_CarCurrentLaneData = GetComponentLookup<CarCurrentLane>(true);
                m_ControllerData = GetComponentLookup<Controller>(true);
                m_CarData = GetComponentLookup<Car>(true);
                m_SubObjectsData = GetBufferLookup<Game.Objects.SubObject>(true);
                m_SubLaneData = GetBufferLookup<Game.Net.SubLane>(true);
                m_LaneObjectData = GetBufferLookup<LaneObject>(true);

                LogUtil.Info("TollBoothBarrierSystem: OnCreate() - System created successfully");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: OnCreate() - ERROR during system creation: {ex.Message}");
                LogUtil.Error($"TollBoothBarrierSystem: OnCreate() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void OnUpdate()
        {
            try
            {
                // Update component lookups
                m_BlockerData.Update(this);
                m_TrafficLightData.Update(this);
                m_LaneSignalData.Update(this);
                m_TollBoothData.Update(this);
                m_TollBoothManualData.Update(this);
                m_CarCurrentLaneData.Update(this);
                m_ControllerData.Update(this);
                m_CarData.Update(this);
                m_SubObjectsData.Update(this);
                m_SubLaneData.Update(this);
                m_LaneObjectData.Update(this);

                uint currentFrame = m_SimulationSystem.frameIndex;

                // Check for vehicles approaching toll booth barriers
                CheckVehiclesApproachingBarriers();

                // Process vehicles that are currently at barriers
                ProcessBarrierVehicles(currentFrame);

                // Clean up finished vehicles
                CleanupFinishedVehicles();

                // Process delayed barrier closes
                if (m_BarrierCloseQueue != null && m_BarrierCloseQueue.Count > 0)
                {
                    for (int i = m_BarrierCloseQueue.Count - 1; i >= 0; i--)
                    {
                        var (roadEntity, closeFrame) = m_BarrierCloseQueue[i];
                        if (currentFrame >= closeFrame)
                        {
                            CloseBarrier(roadEntity);
                            m_BarrierCloseQueue.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: OnUpdate() - ERROR during update: {ex.Message}");
                LogUtil.Error($"TollBoothBarrierSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Checks for vehicles approaching toll booth barriers and sets up lane signal petitioning
        /// </summary>
        private void CheckVehiclesApproachingBarriers()
        {
            try
            {
                // Query all roads with toll booths
                var roadQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TollRoadPrefabData>());
                var roads = roadQuery.ToEntityArray(Allocator.Temp);

                try
                {
                    foreach (var roadEntity in roads)
                    {
                        var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                        
                        if (!tollRoadData.HasActiveTollbooth || 
                            !EntityManager.Exists(tollRoadData.AssociatedTollbooth) ||
                            !EntityManager.HasComponent<TollBoothManualData>(tollRoadData.AssociatedTollbooth))
                        {
                            continue;
                        }

                        CheckRoadForApproachingVehicles(roadEntity, tollRoadData.AssociatedTollbooth);
                    }
                }
                finally
                {
                    roads.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CheckVehiclesApproachingBarriers() - ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks a specific road for vehicles approaching the toll booth
        /// </summary>
        private void CheckRoadForApproachingVehicles(Entity roadEntity, Entity tollBoothEntity)
        {
            try
            {
                if (!m_SubLaneData.TryGetBuffer(roadEntity, out var sublanes))
                    return;

                var tollBoothData = m_TollBoothData[tollBoothEntity];

                // Check each lane for vehicles
                for (int i = 0; i < sublanes.Length; i++)
                {
                    if (sublanes[i].m_PathMethods != PathMethod.Road)
                        continue;

                    Entity laneEntity = sublanes[i].m_SubLane;

                    // Check if this lane has a lane signal (toll booth signal)
                    if (!m_LaneSignalData.HasComponent(laneEntity))
                        continue;

                    var laneSignal = m_LaneSignalData[laneEntity];

                    // Check if the lane signal is associated with our toll booth barrier
                    if (laneSignal.m_Blocker != tollBoothData.BarrierBlockerEntity)
                        continue;

                    // Check for vehicles on this lane
                    if (!m_LaneObjectData.TryGetBuffer(laneEntity, out var laneObjects))
                        continue;

                    CheckLaneForVehicles(laneEntity, laneObjects, tollBoothEntity, roadEntity, tollBoothData.BarrierBlockerEntity);
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CheckRoadForApproachingVehicles() - ERROR for road {roadEntity.Index}: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks a specific lane for vehicles and sets up petitioning
        /// </summary>
        private void CheckLaneForVehicles(Entity laneEntity, DynamicBuffer<LaneObject> laneObjects, Entity tollBoothEntity, Entity roadEntity, Entity barrierEntity)
        {
            try
            {
                for (int i = 0; i < laneObjects.Length; i++)
                {
                    Entity vehicleEntity = laneObjects[i].m_LaneObject;

                    // Check if this is a car
                    if (!m_CarData.HasComponent(vehicleEntity))
                        continue;

                    // Get the controller if it exists
                    Entity controllerEntity = vehicleEntity;
                    if (m_ControllerData.HasComponent(vehicleEntity))
                    {
                        controllerEntity = m_ControllerData[vehicleEntity].m_Controller;
                    }

                    // Skip if already processing this vehicle
                    if (m_ProcessingVehicles.ContainsKey(controllerEntity))
                        continue;

                    // Check if vehicle is on current lane and approaching the barrier
                    if (m_CarCurrentLaneData.HasComponent(controllerEntity))
                    {
                        var carCurrentLane = m_CarCurrentLaneData[controllerEntity];
                        
                        // Check if the vehicle is on the lane with the toll booth signal
                        if (carCurrentLane.m_Lane == laneEntity)
                        {
                            // Check proximity to barrier (simple distance check)
                            float curvePosition = laneObjects[i].m_CurvePosition.x;
                            
                            // If vehicle is close enough to the end of the lane (where barrier would be)
                            if (curvePosition > 0.7f) // Within 30% of lane end
                            {
                                SetupVehiclePetitioning(controllerEntity, laneEntity, tollBoothEntity, roadEntity, barrierEntity);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CheckLaneForVehicles() - ERROR for lane {laneEntity.Index}: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up lane signal petitioning for a vehicle approaching a toll booth
        /// </summary>
        private void SetupVehiclePetitioning(Entity vehicleEntity, Entity laneEntity, Entity tollBoothEntity, Entity roadEntity, Entity barrierEntity)
        {
            try
            {
                LogUtil.Info($"TollBoothBarrierSystem: SetupVehiclePetitioning() - Setting up petitioning for vehicle {vehicleEntity.Index} on lane {laneEntity.Index}");

                // Create processing record
                var processingVehicle = new ProcessingVehicle
                {
                    VehicleEntity = vehicleEntity,
                    TollBoothEntity = tollBoothEntity,
                    RoadEntity = roadEntity,
                    BarrierEntity = barrierEntity,
                    LaneEntity = laneEntity,
                    StartTime = 0,
                    IsProcessing = false,
                    IsFinished = false,
                    HasPetitioned = false
                };

                m_ProcessingVehicles.Add(vehicleEntity, processingVehicle);

                // Set up lane signal petitioning
                if (m_LaneSignalData.HasComponent(laneEntity))
                {
                    var laneSignal = m_LaneSignalData[laneEntity];
                    
                    // Set the vehicle as petitioner
                    laneSignal.m_Petitioner = vehicleEntity;
                    laneSignal.m_Priority = 100; // High priority for toll booth processing
                    
                    // Ensure the blocker is set to our barrier entity
                    laneSignal.m_Blocker = barrierEntity;
                    
                    m_LaneSignalData[laneEntity] = laneSignal; 
                    
                    processingVehicle.HasPetitioned = true;
                    m_ProcessingVehicles[vehicleEntity] = processingVehicle;
                    
                    LogUtil.Info($"TollBoothBarrierSystem: SetupVehiclePetitioning() - Vehicle {vehicleEntity.Index} set as petitioner for lane {laneEntity.Index}");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: SetupVehiclePetitioning() - ERROR for vehicle {vehicleEntity.Index}: {ex.Message}");
            }
        }

        private void ProcessBarrierVehicles(uint currentFrame)
        {
            List<Entity> finishedVehicles = new List<Entity>();
            List<Entity> vehiclesToProcess = new List<Entity>(m_ProcessingVehicles.Keys);

            foreach (var vehicleEntity in vehiclesToProcess)
            {
                // Skip if entity doesn't exist anymore
                if (!EntityManager.Exists(vehicleEntity))
                {
                    finishedVehicles.Add(vehicleEntity);
                    continue;
                }

                var processingVehicle = m_ProcessingVehicles[vehicleEntity];

                // Skip if already finished
                if (processingVehicle.IsFinished)
                {
                    continue;
                }

                // If not processing yet, set up barrier and vehicle
                if (!processingVehicle.IsProcessing)
                {
                    SetupBarrierStop(processingVehicle);
                    processingVehicle.IsProcessing = true;
                    processingVehicle.StartTime = currentFrame;
                    m_ProcessingVehicles[vehicleEntity] = processingVehicle;
                    continue;
                }

                // Check if processing time has elapsed (3 seconds, or ~180 frames at 60fps)
                uint processingTime = 180; // Default 3 seconds

                // If manual toll booth has custom processing time, use that instead
                if (m_TollBoothManualData.HasComponent(processingVehicle.TollBoothEntity))
                {
                    var manualData = m_TollBoothManualData[processingVehicle.TollBoothEntity];
                    processingTime = (uint)(manualData.ProcessingTime * 60); // Convert seconds to frames
                }

                if (currentFrame - processingVehicle.StartTime >= processingTime)
                {
                    ReleaseVehicleFromBarrier(processingVehicle);
                    processingVehicle.IsFinished = true;
                    m_ProcessingVehicles[vehicleEntity] = processingVehicle;

                    // Mark for cleanup after a delay to ensure vehicle passes through
                    finishedVehicles.Add(vehicleEntity);
                }
            }

            // Remove finished vehicles and clean up their petitioning
            foreach (var vehicle in finishedVehicles)
            {
                CleanupVehiclePetitioning(vehicle);
                m_ProcessingVehicles.Remove(vehicle);
            }
        }

        /// <summary>
        /// Cleans up lane signal petitioning for a vehicle
        /// </summary>
        private void CleanupVehiclePetitioning(Entity vehicleEntity)
        {
            try
            {
                if (m_ProcessingVehicles.TryGetValue(vehicleEntity, out var processingVehicle) && processingVehicle.HasPetitioned)
                {
                    if (m_LaneSignalData.HasComponent(processingVehicle.LaneEntity))
                    {
                        var laneSignal = m_LaneSignalData[processingVehicle.LaneEntity];
                        
                        // Clear the petitioner if it's our vehicle
                        if (laneSignal.m_Petitioner == vehicleEntity)
                        {
                            laneSignal.m_Petitioner = Entity.Null;
                            laneSignal.m_Priority = laneSignal.m_Default;
                            m_LaneSignalData[processingVehicle.LaneEntity] = laneSignal;
                            
                            LogUtil.Info($"TollBoothBarrierSystem: CleanupVehiclePetitioning() - Cleared petitioning for vehicle {vehicleEntity.Index}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CleanupVehiclePetitioning() - ERROR for vehicle {vehicleEntity.Index}: {ex.Message}");
            }
        }

        private void CleanupFinishedVehicles()
        {
            // Additional cleanup logic if needed
        }

        /// <summary>
        /// Sets up a vehicle to stop at a barrier
        /// </summary>
        private void SetupBarrierStop(ProcessingVehicle vehicle)
        {
            try
            {
                LogUtil.Info($"TollBoothBarrierSystem: SetupBarrierStop() - Setting up vehicle {vehicle.VehicleEntity.Index} to stop at barrier");

                // Ensure the vehicle has a blocker component
                if (!EntityManager.HasComponent<Blocker>(vehicle.VehicleEntity))
                {
                    EntityManager.AddComponent<Blocker>(vehicle.VehicleEntity);
                }

                // Configure the blocker to stop the vehicle
                var blocker = new Blocker
                {
                    m_Blocker = vehicle.BarrierEntity,
                    m_Type = BlockerType.Crossing,
                    m_MaxSpeed = 0
                };

                EntityManager.SetComponentData(vehicle.VehicleEntity, blocker);

                LogUtil.Info($"TollBoothBarrierSystem: SetupBarrierStop() - Vehicle {vehicle.VehicleEntity.Index} stopped at barrier");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: SetupBarrierStop() - ERROR stopping vehicle at barrier: {ex.Message}");
            }
        }

        /// <summary>
        /// Releases a vehicle from a barrier
        /// </summary>
        private void ReleaseVehicleFromBarrier(ProcessingVehicle vehicle)
        {
            try
            {
                LogUtil.Info($"TollBoothBarrierSystem: ReleaseVehicleFromBarrier() - Releasing vehicle {vehicle.VehicleEntity.Index} from barrier");

                // Update vehicle's blocker component to allow passage
                if (EntityManager.HasComponent<Blocker>(vehicle.VehicleEntity))
                {
                    var blocker = EntityManager.GetComponentData<Blocker>(vehicle.VehicleEntity);
                    blocker.m_Blocker = Entity.Null;
                    blocker.m_Type = BlockerType.None;
                    EntityManager.SetComponentData(vehicle.VehicleEntity, blocker);
                }

                // Open the barrier by updating traffic lights and lane signals
                OpenBarrier(vehicle.RoadEntity);

                LogUtil.Info($"TollBoothBarrierSystem: ReleaseVehicleFromBarrier() - Vehicle {vehicle.VehicleEntity.Index} released from barrier");

                // Schedule barrier to close after vehicle passes
                CloseBarrierAfterDelay(vehicle.RoadEntity);
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: ReleaseVehicleFromBarrier() - ERROR releasing vehicle from barrier: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the barrier by updating traffic lights and lane signals
        /// </summary>
        private void OpenBarrier(Entity roadEntity)
        {
            try
            {
                LogUtil.Info($"TollBoothBarrierSystem: OpenBarrier() - Opening barrier for road {roadEntity.Index}");

                // Update lane signals to allow passage
                if (m_SubLaneData.TryGetBuffer(roadEntity, out var sublanes))
                {
                    for (int i = 0; i < sublanes.Length; i++)
                    {
                        if (sublanes[i].m_PathMethods == PathMethod.Road)
                        {
                            Entity laneEntity = sublanes[i].m_SubLane;

                            if (m_LaneSignalData.HasComponent(laneEntity))
                            {
                                var laneSignal = m_LaneSignalData[laneEntity];
                                laneSignal.m_Signal = LaneSignalType.Go;
                                m_LaneSignalData[laneEntity] = laneSignal;
                                LogUtil.Info($"TollBoothBarrierSystem: OpenBarrier() - Set lane signal to GO for lane {laneEntity.Index}");
                            }
                        }
                    }
                }

                // Update traffic lights to green
                if (m_SubObjectsData.TryGetBuffer(roadEntity, out var subObjects))
                {
                    for (int i = 0; i < subObjects.Length; i++)
                    {
                        if (m_TrafficLightData.HasComponent(subObjects[i].m_SubObject))
                        {
                            Entity trafficLightEntity = subObjects[i].m_SubObject;
                            var trafficLight = m_TrafficLightData[trafficLightEntity];
                            trafficLight.m_State = Game.Objects.TrafficLightState.Green;
                            m_TrafficLightData[trafficLightEntity] = trafficLight;
                            LogUtil.Info($"TollBoothBarrierSystem: OpenBarrier() - Set traffic light to GREEN for light {trafficLightEntity.Index}");
                        }
                    }
                }

                LogUtil.Info($"TollBoothBarrierSystem: OpenBarrier() - Barrier opened for road {roadEntity.Index}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: OpenBarrier() - ERROR opening barrier: {ex.Message}");
            }
        }

        /// <summary>
        /// Closes the barrier by updating traffic lights and lane signals
        /// </summary>
        private void CloseBarrier(Entity roadEntity)
        {
            try
            {
                LogUtil.Info($"TollBoothBarrierSystem: CloseBarrier() - Closing barrier for road {roadEntity.Index}");

                // Update lane signals to stop passage
                if (m_SubLaneData.TryGetBuffer(roadEntity, out var sublanes))
                {
                    for (int i = 0; i < sublanes.Length; i++)
                    {
                        if (sublanes[i].m_PathMethods == PathMethod.Road)
                        {
                            Entity laneEntity = sublanes[i].m_SubLane;

                            if (m_LaneSignalData.HasComponent(laneEntity))
                            {
                                var laneSignal = m_LaneSignalData[laneEntity];
                                laneSignal.m_Signal = LaneSignalType.Stop;
                                m_LaneSignalData[laneEntity] = laneSignal;
                                LogUtil.Info($"TollBoothBarrierSystem: CloseBarrier() - Set lane signal to STOP for lane {laneEntity.Index}");
                            }
                        }
                    }
                }

                // Update traffic lights to red
                if (m_SubObjectsData.TryGetBuffer(roadEntity, out var subObjects))
                {
                    for (int i = 0; i < subObjects.Length; i++)
                    {
                        if (m_TrafficLightData.HasComponent(subObjects[i].m_SubObject))
                        {
                            Entity trafficLightEntity = subObjects[i].m_SubObject;
                            var trafficLight = m_TrafficLightData[trafficLightEntity];
                            trafficLight.m_State = Game.Objects.TrafficLightState.Red;
                            m_TrafficLightData[trafficLightEntity] = trafficLight;
                            LogUtil.Info($"TollBoothBarrierSystem: CloseBarrier() - Set traffic light to RED for light {trafficLightEntity.Index}");
                        }
                    }
                }

                LogUtil.Info($"TollBoothBarrierSystem: CloseBarrier() - Barrier closed for road {roadEntity.Index}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CloseBarrier() - ERROR closing barrier: {ex.Message}");
            }
        }
        protected override void OnDestroy()
        {
            m_ProcessingVehicles.Clear();
            base.OnDestroy();
        }

        /// <summary>
        /// Closes the barrier after a delay to allow the vehicle to pass through
        /// </summary>
        private void CloseBarrierAfterDelay(Entity roadEntity)
        {
            try
            {
                if (m_BarrierCloseQueue == null)
                    m_BarrierCloseQueue = new List<(Entity roadEntity, uint closeFrame)>();

                uint closeFrame = m_SimulationSystem.frameIndex + 60; // Close after ~1 second
                m_BarrierCloseQueue.Add((roadEntity, closeFrame));
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollBoothBarrierSystem: CloseBarrierAfterDelay() - ERROR scheduling barrier close: {ex.Message}");
            }
        }

        // Add this field to the class
        private List<(Entity roadEntity, uint closeFrame)> m_BarrierCloseQueue;
    }
}