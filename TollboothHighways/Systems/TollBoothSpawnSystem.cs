using Colossal.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Colossal.IO.AssetDatabase.AtlasFrame;
using Random = System.Random;

namespace TollboothHighways.Systems
{
    public partial class TollBoothSpawnSystem : GameSystemBase
    {
        private EntityQuery m_UnprocessedTollBoothQuery;
        private PrefabSystem m_PrefabSystem;
        private SimulationSystem m_SimulationSystem;
        private BufferLookup<Game.Net.SubLane> SubLaneObjectData;
        private BufferLookup<Game.Objects.SubObject> SubObjectsObjectData;

        // Additional lookups for TrafficLights integration
        private ComponentLookup<TrafficLights> m_TrafficLightsData;
        private ComponentLookup<LaneSignal> m_LaneSignalData;
        private ComponentLookup<Game.Objects.TrafficLight> m_TrafficLightObjectData;

        // Predefined random names for toll booths
        private readonly string[] m_TollBoothNames = new string[]
        {
            "Gateway Plaza",
            "Golden Bridge Toll",
            "Sunrise Station",
            "Mountain View Plaza",
            "Riverside Checkpoint",
            "Valley Express",
            "Harbor Gate",
            "Summit Pass",
            "Metro Junction",
            "Central Plaza",
            "Pine Ridge Station",
            "Coastal Gateway",
            "Highland Passage",
            "Urban Express",
            "Parkway Plaza",
            "Commerce Gate",
            "Industrial Junction",
            "Liberty Station",
            "Eagle Pass",
            "Thunder Ridge",
            "Crystal Bay Plaza",
            "Meadowbrook Gate",
            "Silverstone Pass",
            "Woodland Station",
            "Lakeside Plaza"
        };

        private System.Random m_Random;

        // Event to notify when toll booth data changes
        public static event System.Action<Entity, string> TollBoothDataChanged;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_Random = new Random((int)DateTime.Now.Ticks);
            
            // Initialize buffer lookups
            SubLaneObjectData = GetBufferLookup<Game.Net.SubLane>(true);
            SubObjectsObjectData = GetBufferLookup<Game.Objects.SubObject>(true);
            
            // Initialize component lookups for TrafficLights integration
            m_TrafficLightsData = GetComponentLookup<TrafficLights>(false);
            m_LaneSignalData = GetComponentLookup<LaneSignal>(false);
            m_TrafficLightObjectData = GetComponentLookup<Game.Objects.TrafficLight>(false);

            // Query for toll booth entities that haven't been processed yet
            // This excludes entities that already have the TollBoothSpawned marker component
            m_UnprocessedTollBoothQuery = GetEntityQuery(
                ComponentType.ReadWrite<TollBoothPrefabData>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<TollBoothSpawned>()  // Only get entities without the spawned marker
            );


            LogUtil.Info("TollBoothSpawnSystem: System created and initialized");
        }

        protected override void OnUpdate()
        {
            // Update lookups
            SubLaneObjectData.Update(this);
            SubObjectsObjectData.Update(this);
            m_TrafficLightsData.Update(this);
            m_LaneSignalData.Update(this);
            m_TrafficLightObjectData.Update(this);

            // Early exit if no unprocessed entities
            if (m_UnprocessedTollBoothQuery.IsEmpty)
                return;

            LogUtil.Info($"TollBoothSpawnSystem: OnUpdate called, found {m_UnprocessedTollBoothQuery.CalculateEntityCount()} unprocessed toll booth entities");

            var entities = m_UnprocessedTollBoothQuery.ToEntityArray(Allocator.TempJob);
            var tollBoothDataArray = m_UnprocessedTollBoothQuery.ToComponentDataArray<TollBoothPrefabData>(Allocator.TempJob);

            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var tollBoothData = tollBoothDataArray[i];

                    LogUtil.Info($"TollBoothSpawnSystem: Processing entity {entity.Index}");

                    // Check if there is no asociated highway tollbooth yet
                    if (tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                    {
                        // Write owner entity information
                        WriteOwnerEntityInfo(entity, ref tollBoothData);

                        // Generate a unique random name
                        AssignRandomName(entity, ref tollBoothData);

                        // Initialize TollBoothInsight component for tracking vehicle statistics
                        InitializeTollBoothInsight(entity);

                        // Mark entity as processed by adding the TollBoothSpawned component
                        EntityManager.AddComponent<TollBoothSpawned>(entity);
                    }
                    else
                    {
                        LogUtil.Info($"TollBoothSpawnSystem: Entity {entity.Index} already has a road associated, marking as processed");
                    }
                }
            }
            finally
            {
                entities.Dispose();
                tollBoothDataArray.Dispose();
            }
        }

        /// <summary>
        /// Initializes the TollBoothInsight component for a tollbooth entity to start tracking vehicle statistics.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to initialize insights for</param>
        private void InitializeTollBoothInsight(Entity tollBoothEntity)
        {
            try
            {
                uint currentFrame = m_SimulationSystem.frameIndex;

                // Check if the entity already has TollBoothInsight component
                if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: TollBoothInsight already exists for entity {tollBoothEntity.Index}, skipping initialization");
                    return;
                }

                // Create new TollBoothInsight component with initial values
                var tollBoothInsight = new TollBoothInsight();
                tollBoothInsight.ResetStatistics(currentFrame);

                // Add the component to the tollbooth entity
                EntityManager.AddComponentData(tollBoothEntity, tollBoothInsight);

                LogUtil.Info($"TollBoothSpawnSystem: Successfully initialized TollBoothInsight for tollbooth entity {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to initialize TollBoothInsight for entity {tollBoothEntity.Index}. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a random toll booth name by selecting a base name from a predefined list and optionally appending
        /// a random number for uniqueness.
        /// </summary>
        /// <remarks>There is a 30% chance that the generated name will include a random number appended
        /// to the base name for additional uniqueness.</remarks>
        /// <returns>A string representing the generated toll booth name. The name will either be a base name from the predefined
        /// list or a combination of the base name and a random number.</returns>
        private string GenerateRandomTollBoothName()
        {
            // Choose a random base name
            string baseName = m_TollBoothNames[m_Random.Next(m_TollBoothNames.Length)];

            // Add a random number to make it more unique
            int randomNumber = m_Random.Next(1, 100);

            // Combine base name with number or use just the base name occasionally
            if (m_Random.Next(100) < 30) // 30% chance to add number
            {
                return $"{baseName} {randomNumber}";
            }
            else
            {
                return baseName;
            }
        }

        // Alternative method using more varied naming patterns
        private string GenerateRandomTollBoothNameAdvanced()
        {
            string[] prefixes = { "North", "South", "East", "West", "Central", "Upper", "Lower", "New", "Old" };
            string[] types = { "Plaza", "Station", "Gate", "Checkpoint", "Pass", "Junction", "Express", "Bridge" };
            string[] suffixes = { "A", "B", "C", "1", "2", "3", "Main", "Ext" };

            string baseName = m_TollBoothNames[m_Random.Next(m_TollBoothNames.Length)];

            // 40% chance to add prefix
            if (m_Random.Next(100) < 40)
            {
                string prefix = prefixes[m_Random.Next(prefixes.Length)];
                baseName = $"{prefix} {baseName}";
            }

            // 20% chance to add suffix
            if (m_Random.Next(100) < 20)
            {
                string suffix = suffixes[m_Random.Next(suffixes.Length)];
                baseName = $"{baseName}-{suffix}";
            }

            return baseName;
        }

        /// <summary>
        /// Updates the toll booth entity with ownership information and establishes a bidirectional relationship
        /// between the toll booth and its associated owner entity.
        /// </summary>
        /// <remarks>If the toll booth entity has an <see cref="Owner"/> component, this method logs the
        /// ownership information, updates the toll booth's data to reflect the owner, and establishes a bidirectional
        /// relationship between the toll booth and the owner entity. If the <see cref="Owner"/> component is not
        /// present, a warning is logged.</remarks>
        /// <param name="tollBoothEntity">The entity representing the toll booth to be updated.</param>
        /// <param name="tollBoothData">A reference to the <see cref="TollBoothPrefabData"/> structure that holds data about the toll booth. This
        /// data will be updated with ownership information.</param>
        private void WriteOwnerEntityInfo(Entity tollBoothEntity, ref TollBoothPrefabData tollBoothData)
        {
            if (EntityManager.TryGetComponent<Owner>(tollBoothEntity, out var ownerComponent))
            {
                // Log the owner entity information
                LogUtil.Info($"TollBoothSpawnSystem: Toll booth {tollBoothEntity.Index} belongs to owner {ownerComponent.m_Owner.Index}");

                // Set this information in the tollBoothData
                tollBoothData.BelongsToHighwayTollbooth = ownerComponent.m_Owner;

                // Update the component data
                EntityManager.SetComponentData(tollBoothEntity, tollBoothData);

                // Now establish the bidirectional relationship: associate tollbooth with the road
                AssociateTollboothWithRoad(tollBoothEntity, ownerComponent.m_Owner);
            }
            else
            {
                LogUtil.Warn($"TollBoothSpawnSystem: Toll booth {tollBoothEntity.Index} does not have an Owner component.");
            }
        }

        /// <summary>
        /// Associates a tollbooth entity with its parent road entity by updating the road's TollRoadPrefabData component.
        /// This creates a bidirectional relationship between the road and its tollbooth, and sets up manual barrier control if applicable.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to associate</param>
        /// <param name="roadEntity">The road entity that owns the tollbooth</param>
        private void AssociateTollboothWithRoad(Entity tollBoothEntity, Entity roadEntity)
        {
            try
            {
                // Check if the road entity already has a TollRoadPrefabData component
                if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
                {
                    // Get the existing TollRoadPrefabData
                    var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);

                    // Check if the road already has a different tollbooth associated
                    if (tollRoadData.HasActiveTollbooth && tollRoadData.AssociatedTollbooth != tollBoothEntity)
                    {
                        LogUtil.Warn($"TollBoothSpawnSystem: Road {roadEntity.Index} already has tollbooth {tollRoadData.AssociatedTollbooth.Index} associated. " +
                                   $"Replacing with new tollbooth {tollBoothEntity.Index}");
                    }

                    // Update the tollbooth association
                    tollRoadData.AssociatedTollbooth = tollBoothEntity;
                    tollRoadData.HasActiveTollbooth = true;

                    // Update the component on the road entity
                    EntityManager.SetComponentData(roadEntity, tollRoadData);

                    LogUtil.Info($"TollBoothSpawnSystem: Successfully updated association - tollbooth {tollBoothEntity.Index} with toll road {roadEntity.Index}");
                }
                else
                {
                    // Road doesn't have TollRoadPrefabData component, add it with the tollbooth association
                    var newTollRoadData = new TollRoadPrefabData
                    {
                        AssociatedTollbooth = tollBoothEntity,
                        HasActiveTollbooth = true
                    };

                    EntityManager.AddComponentData(roadEntity, newTollRoadData);

                    LogUtil.Info($"TollBoothSpawnSystem: Created new TollRoadPrefabData and associated tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}");
                }

                // Set up manual barrier control system if this is a manual tollbooth
                SetupManualBarrierControl(tollBoothEntity, roadEntity);
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to associate tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}. Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Sets up manual barrier control system for tollbooths with TollBoothManualData component.
        /// This configures the lane signals and traffic lights WITHOUT using TrafficLights component.
        /// IMPORTANT: Barrier is initialized as CLOSED by default.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        /// <param name="roadEntity">The road entity that owns the tollbooth</param>
        private void SetupManualBarrierControl(Entity tollBoothEntity, Entity roadEntity)
        {
            // Only proceed if this tollbooth has manual data (indicating it's a manual tollbooth)
            if (!EntityManager.HasComponent<TollBoothManualData>(tollBoothEntity))
            {
                return;
            }

            LogUtil.Info($"TollBoothSpawnSystem: Setting up manual barrier control for tollbooth {tollBoothEntity.Index} - BARRIER STARTS CLOSED");

            try
            {
                // DO NOT ADD TrafficLights component - it will be managed by TrafficLightSystem
                // Instead, directly control lane signals and traffic light objects

                // Set up lane signals for barrier control (CLOSED by default)
                SetupLaneSignalsForBarrier(roadEntity, tollBoothEntity);

                // Set up traffic light control for visual barrier indication (RED by default)
                SetupTrafficLightForBarrier(roadEntity, tollBoothEntity);

                // CRITICAL: Ensure barrier starts in CLOSED state (without TrafficLights)
                EnsureBarrierClosedStateDirectly(roadEntity);

                LogUtil.Info($"TollBoothSpawnSystem: Successfully set up manual barrier control for tollbooth {tollBoothEntity.Index} - BARRIER IS CLOSED (NO TRAFFICLIGHTS)");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to setup manual barrier control for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
            }
        }


        /// <summary>
        /// Ensures the barrier is in a closed state after setup WITHOUT using TrafficLights component.
        /// This is a safety measure to guarantee the default closed state.
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        private void EnsureBarrierClosedStateDirectly(Entity roadEntity)
        {
            try
            {
                // Do NOT add or modify TrafficLights component
                // Instead, directly control lane signals and traffic lights

                // Explicitly set lane signals to STOP
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out var sublaneObjects))
                {
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;

                            if (m_LaneSignalData.HasComponent(laneEntity))
                            {
                                var laneSignal = m_LaneSignalData[laneEntity];
                                laneSignal.m_Signal = LaneSignalType.Stop;
                                m_LaneSignalData[laneEntity] = laneSignal;
                            }
                            break;
                        }
                    }
                }

                // Explicitly set traffic light objects to RED
                if (SubObjectsObjectData.TryGetBuffer(roadEntity, out var subObjects))
                {
                    for (int i = 0; i < subObjects.Length; i++)
                    {
                        if (m_TrafficLightObjectData.HasComponent(subObjects[i].m_SubObject))
                        {
                            Entity trafficLightEntity = subObjects[i].m_SubObject;

                            var trafficLight = m_TrafficLightObjectData[trafficLightEntity];
                            trafficLight.m_State = Game.Objects.TrafficLightState.Red;
                            m_TrafficLightObjectData[trafficLightEntity] = trafficLight;
                            break;
                        }
                    }
                }

                LogUtil.Info($"TollBoothSpawnSystem: Barrier closed state enforced DIRECTLY for road {roadEntity.Index} (bypassing TrafficLights system)");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to ensure barrier closed state for road {roadEntity.Index}. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up lane signals that will control vehicle flow through the toll barrier.
        /// Uses the same pattern as TrafficLightSystem for lane signal management.
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        private void SetupLaneSignalsForBarrier(Entity roadEntity, Entity tollBoothEntity)
        {
            if (!SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
            {
                LogUtil.Warn($"TollBoothSpawnSystem: No sublanes found for road {roadEntity.Index}");
                return;
            }

            // Find the main road lane (used for vehicle traffic)
            int mainRoadLaneIndex = -1;
            for (int i = 0; i < sublaneObjects.Length; i++)
            {
                if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                {
                    mainRoadLaneIndex = i;
                    break;
                }
            }

            if (mainRoadLaneIndex == -1)
            {
                LogUtil.Warn($"TollBoothSpawnSystem: No road lane found in sublanes for road {roadEntity.Index}");
                return;
            }

            Entity laneEntity = sublaneObjects[mainRoadLaneIndex].m_SubLane;

            // Add or update LaneSignal component for barrier control
            if (!EntityManager.HasComponent<LaneSignal>(laneEntity))
            {
                EntityManager.AddComponent<LaneSignal>(laneEntity);
            }

            // Configure lane signal based on TrafficLightSystem patterns
            var laneSignal = new LaneSignal
            {
                m_Flags = LaneSignalFlags.CanExtend, // Allow signal extension for manual control
                m_Signal = LaneSignalType.Stop,      // Start with barrier closed (red signal)
                m_GroupMask = 1,                     // Assign to signal group 1
                m_Default = 0,                       // Default priority
                m_Priority = 0,                      // Current priority
                m_Petitioner = Entity.Null,         // No current petitioner
                m_Blocker = Entity.Null              // No current blocker
            };

            EntityManager.SetComponentData(laneEntity, laneSignal);

            LogUtil.Info($"TollBoothSpawnSystem: Added LaneSignal to sublane {laneEntity.Index} for manual barrier control");
        }

        /// <summary>
        /// Sets up traffic light visual indicators for the manual toll barrier.
        /// The traffic light will show red when barrier is closed, green when open.
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        private void SetupTrafficLightForBarrier(Entity roadEntity, Entity tollBoothEntity)
        {
            if (!SubObjectsObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Objects.SubObject> subObjects))
            {
                LogUtil.Warn($"TollBoothSpawnSystem: No subobjects found for road {roadEntity.Index}");
                return;
            }

            // Find the traffic light subobject
            Entity trafficLightEntity = Entity.Null;
            for (int i = 0; i < subObjects.Length; i++)
            {
                if (EntityManager.HasComponent<Game.Objects.TrafficLight>(subObjects[i].m_SubObject))
                {
                    trafficLightEntity = subObjects[i].m_SubObject;
                    break;
                }
            }

            if (trafficLightEntity == Entity.Null)
            {
                LogUtil.Warn($"TollBoothSpawnSystem: No traffic light subobject found for road {roadEntity.Index}");
                return;
            }

            // Configure traffic light for manual barrier control
            var trafficLight = new Game.Objects.TrafficLight
            {
                // Start with red state (barrier closed) - using pattern from TrafficLightSystem
                m_State = Game.Objects.TrafficLightState.Red,
                m_GroupMask0 = 1,  // Assign to signal group 1 (matches lane signal)
                m_GroupMask1 = 0   // No secondary group for simple barrier control
            };

            EntityManager.SetComponentData(trafficLightEntity, trafficLight);

            LogUtil.Info($"TollBoothSpawnSystem: Configured traffic light {trafficLightEntity.Index} for manual barrier control");
        }

       
        

        /// <summary>
        /// Closes the manual toll barrier by directly controlling lane signals and traffic lights.
        /// IMPORTANT: This is the DEFAULT state - barrier should always return to CLOSED.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        public void CloseBarrier(Entity tollBoothEntity)
        {
            try
            {
                // Get the associated road entity
                if (!EntityManager.TryGetComponent<TollBoothPrefabData>(tollBoothEntity, out var tollBoothData) ||
                    tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: Cannot close barrier - no associated road for tollbooth {tollBoothEntity.Index}");
                    return;
                }

                Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;

                // Directly control lane signals and traffic lights (bypass TrafficLights system)
                UpdateLaneSignalForBarrierState(roadEntity, LaneSignalType.Stop);
                UpdateTrafficLightForBarrierState(roadEntity, Game.Objects.TrafficLightState.Red);

                LogUtil.Info($"TollBoothSpawnSystem: CLOSED barrier DIRECTLY for tollbooth {tollBoothEntity.Index} - DEFAULT STATE RESTORED");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to close barrier for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the lane signal state for barrier control.
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        /// <param name="signalType">The signal type to set</param>
        private void UpdateLaneSignalForBarrierState(Entity roadEntity, LaneSignalType signalType)
        {
            if (!SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
            {
                return;
            }

            // Find the main road lane
            for (int i = 0; i < sublaneObjects.Length; i++)
            {
                if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                {
                    Entity laneEntity = sublaneObjects[i].m_SubLane;
                    
                    if (EntityManager.HasComponent<LaneSignal>(laneEntity))
                    {
                        var laneSignal = EntityManager.GetComponentData<LaneSignal>(laneEntity);
                        laneSignal.m_Signal = signalType;
                        EntityManager.SetComponentData(laneEntity, laneSignal);
                        
                        LogUtil.Info($"TollBoothSpawnSystem: Updated lane signal to {signalType} for lane {laneEntity.Index}");
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Updates the traffic light state for barrier control.
        /// </summary>
        /// <param name="roadEntity">The road entity</param>
        /// <param name="lightState">The light state to set</param>
        private void UpdateTrafficLightForBarrierState(Entity roadEntity, Game.Objects.TrafficLightState lightState)
        {
            if (!SubObjectsObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Objects.SubObject> subObjects))
            {
                return;
            }

            // Find the traffic light subobject
            for (int i = 0; i < subObjects.Length; i++)
            {
                if (EntityManager.HasComponent<Game.Objects.TrafficLight>(subObjects[i].m_SubObject))
                {
                    Entity trafficLightEntity = subObjects[i].m_SubObject;
                    
                    var trafficLight = EntityManager.GetComponentData<Game.Objects.TrafficLight>(trafficLightEntity);
                    trafficLight.m_State = lightState;
                    EntityManager.SetComponentData(trafficLightEntity, trafficLight);
                    
                    LogUtil.Info($"TollBoothSpawnSystem: Updated traffic light to {lightState} for light {trafficLightEntity.Index}");
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if a manual toll barrier is currently open by checking lane signals directly.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to check</param>
        /// <returns>True if barrier is open, false if closed or if not a manual tollbooth</returns>
        public bool IsBarrierOpen(Entity tollBoothEntity)
        {
            try
            {
                // Check if this is a manual tollbooth
                if (!EntityManager.HasComponent<TollBoothManualData>(tollBoothEntity))
                {
                    return false; // Not a manual tollbooth
                }

                // Get the associated road entity
                if (!EntityManager.TryGetComponent<TollBoothPrefabData>(tollBoothEntity, out var tollBoothData) ||
                    tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                {
                    return false;
                }

                Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;

                // Check lane signal state DIRECTLY (ignore TrafficLights)
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
                {
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;

                            if (EntityManager.HasComponent<LaneSignal>(laneEntity))
                            {
                                var laneSignal = EntityManager.GetComponentData<LaneSignal>(laneEntity);
                                return laneSignal.m_Signal == LaneSignalType.Go;
                            }
                            break;
                        }
                    }
                }

                return false; // Default to closed
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to check barrier state for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
                return false; // Default to closed on error
            }
        }

        /// <summary>
        /// Assigns a randomly generated name to the specified toll booth entity.
        /// </summary>
        /// <remarks>This method generates a random name for the toll booth entity and assigns it using
        /// the <see cref="Game.UI.NameSystem"/>. The assigned name is logged for debugging purposes.</remarks>
        /// <param name="entity">The entity to which the random name will be assigned.</param>
        /// <param name="tollBoothData">A reference to the toll booth data associated with the entity. This parameter is not modified by this
        /// method.</param>
        private void AssignRandomName(Entity entity, ref TollBoothPrefabData tollBoothData)
        {
            // Generate a random name for the toll booth
            string randomName = GenerateRandomTollBoothNameAdvanced();

            // Set the entity's custom name through the NameSystem
            var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            nameSystem.SetCustomName(entity, randomName);

            LogUtil.Info($"TollBoothSpawnSystem: Assigned random name '{randomName}' to toll booth entity {entity.Index}");
        }

        protected override void OnDestroy()
        {
            // No need to clean up HashSet anymore - ECS handles component cleanup automatically
            base.OnDestroy();
        }
    
        /// <summary>
        /// Gets the tollbooth entity associated with a specific road entity.
        /// </summary>
        /// <param name="roadEntity">The road entity to check</param>
        /// <returns>The associated tollbooth entity, or Entity.Null if none exists</returns>
        public Entity GetTollboothForRoad(Entity roadEntity)
        {
            if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
            {
                var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                if (tollRoadData.HasActiveTollbooth && EntityManager.Exists(tollRoadData.AssociatedTollbooth))
                {
                    return tollRoadData.AssociatedTollbooth;
                }
            }
            return Entity.Null;
        }

        /// <summary>
        /// Checks if a road entity has an active tollbooth associated with it.
        /// </summary>
        /// <param name="roadEntity">The road entity to check</param>
        /// <returns>True if the road has an active tollbooth, false otherwise</returns>
        public bool RoadHasTollbooth(Entity roadEntity)
        {
            if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
            {
                var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                return tollRoadData.HasActiveTollbooth && EntityManager.Exists(tollRoadData.AssociatedTollbooth);
            }
            return false;
        }

        /// <summary>
        /// Removes the tollbooth association from a road entity.
        /// This should be called when a tollbooth is deleted or deactivated.
        /// </summary>
        /// <param name="roadEntity">The road entity to update</param>
        public void RemoveTollboothFromRoad(Entity roadEntity)
        {
            if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
            {
                var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                tollRoadData.AssociatedTollbooth = Entity.Null;
                tollRoadData.HasActiveTollbooth = false;

                EntityManager.SetComponentData(roadEntity, tollRoadData);

                LogUtil.Info($"TollBoothSpawnSystem: Removed tollbooth association from road {roadEntity.Index}");
            }
        }

        /// <summary>
        /// Updates vehicle statistics for a specific tollbooth when a vehicle passes through.
        /// This method should be called by other systems when they detect a vehicle passing through a tollbooth.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to update</param>
        /// <param name="vehicleType">Type of vehicle that passed through</param>
        /// <param name="tollAmount">Amount of toll charged for this vehicle</param>
        public void UpdateVehicleStatistics(Entity tollBoothEntity, Domain.Enums.VehicleType vehicleType, float tollAmount)
        {
            try
            {
                if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
                {
                    var insight = EntityManager.GetComponentData<TollBoothInsight>(tollBoothEntity);
                    insight.AddVehiclePassage(vehicleType, tollAmount, m_SimulationSystem.frameIndex);
                    EntityManager.SetComponentData(tollBoothEntity, insight);

                    LogUtil.Info($"TollBoothSpawnSystem: Updated vehicle statistics for tollbooth {tollBoothEntity.Index} - Vehicle: {vehicleType}, Toll: ${tollAmount:F2}");
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: Cannot update vehicle statistics - TollBoothInsight component not found for entity {tollBoothEntity.Index}");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to update vehicle statistics for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current vehicle statistics for a specific tollbooth.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to get statistics for</param>
        /// <returns>TollBoothInsight component data, or default if not found</returns>
        public TollBoothInsight GetTollBoothStatistics(Entity tollBoothEntity)
        {
            if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
            {
                return EntityManager.GetComponentData<TollBoothInsight>(tollBoothEntity);
            }
            return default(TollBoothInsight);
        }

        /// <summary>
        /// Resets vehicle statistics for a specific tollbooth.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity to reset statistics for</param>
        public void ResetTollBoothStatistics(Entity tollBoothEntity)
        {
            try
            {
                if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
                {
                    var insight = EntityManager.GetComponentData<TollBoothInsight>(tollBoothEntity);
                    insight.ResetStatistics(m_SimulationSystem.frameIndex);
                    EntityManager.SetComponentData(tollBoothEntity, insight);

                    LogUtil.Info($"TollBoothSpawnSystem: Reset vehicle statistics for tollbooth {tollBoothEntity.Index}");
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: Cannot reset statistics - TollBoothInsight component not found for entity {tollBoothEntity.Index}");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: Failed to reset statistics for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
            }
        }
    }
}