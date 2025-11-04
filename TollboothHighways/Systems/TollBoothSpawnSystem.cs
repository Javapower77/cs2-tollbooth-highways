using Colossal.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.Vehicles;
using System;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using static Colossal.IO.AssetDatabase.AtlasFrame;
using CarLaneFlags = Game.Net.CarLaneFlags;
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
        private ComponentLookup<Game.Net.Edge> m_EdgeObjectData;
        private ComponentLookup<Game.Net.EndNodeGeometry> m_EndNodeGeometryData;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneData;
        private bool m_LogInitialized = false;

        // Additional lookups for TrafficLights integration
        private ComponentLookup<TrafficLights> m_TrafficLightsData;
        private ComponentLookup<LaneSignal> m_LaneSignalData;
        private ComponentLookup<Game.Objects.TrafficLight> m_TrafficLightObjectData;

        // New lookups for Lane/Node transforms
        private ComponentLookup<Lane> m_LaneData;
        private ComponentLookup<Node> m_NodeData;
        private ComponentLookup<Transform> m_TransformData;

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

        protected override void OnCreate()
        {
            try
            {
                base.OnCreate();
                m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
                m_Random = new Random((int)DateTime.Now.Ticks);
                m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

                SubLaneObjectData = GetBufferLookup<Game.Net.SubLane>(false);
                SubObjectsObjectData = GetBufferLookup<Game.Objects.SubObject>(false);
                m_TrafficLightsData = GetComponentLookup<TrafficLights>(false);
                m_LaneSignalData = GetComponentLookup<LaneSignal>(false);
                m_EdgeObjectData = GetComponentLookup<Game.Net.Edge>(true);
                m_EndNodeGeometryData = GetComponentLookup<Game.Net.EndNodeGeometry>(true);
                m_TrafficLightObjectData = GetComponentLookup<Game.Objects.TrafficLight>(false);
                m_LaneData = GetComponentLookup<Lane>(true);
                m_NodeData = GetComponentLookup<Node>(true);
                m_TransformData = GetComponentLookup<Transform>(true);
                m_CarLaneData = GetComponentLookup<Game.Net.CarLane>(false);
                
                m_UnprocessedTollBoothQuery = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadWrite<TollBoothPrefabData>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<TollBoothSpawned>(),
                        ComponentType.ReadOnly<TollRoadCarLaneApplied>()
                    }
                });

                RequireForUpdate(m_UnprocessedTollBoothQuery);

                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - System created and initialized successfully");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - CRITICAL ERROR during system creation: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void OnUpdate()
        {
            try
            {
                if (m_UnprocessedTollBoothQuery.IsEmpty)
                {
                    return;
                }

                SubLaneObjectData.Update(this);
                SubObjectsObjectData.Update(this);
                m_TrafficLightsData.Update(this);
                m_LaneSignalData.Update(this);
                m_TrafficLightObjectData.Update(this);
                m_LaneData.Update(this);
                m_NodeData.Update(this);
                m_TransformData.Update(this);
                m_EdgeObjectData.Update(this);
                m_EndNodeGeometryData.Update(this);
                m_CarLaneData.Update(this);

                int entityCount = m_UnprocessedTollBoothQuery.CalculateEntityCount();
                var entities = m_UnprocessedTollBoothQuery.ToEntityArray(Allocator.TempJob);              
                var tollBoothDataArray = m_UnprocessedTollBoothQuery.ToComponentDataArray<TollBoothPrefabData>(Allocator.TempJob);

                try
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        var tollBoothData = tollBoothDataArray[i];

                        try
                        {
                            if (tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - [STEP 1] - Configure custom components for entity{entity.Index}");
                                SetTollboothComponents(entity, ref tollBoothData);

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - [STEP 2] - Assigning random name for {entity.Index}");
                                AssignRandomName(entity, ref tollBoothData);

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - [STEP 3] - Initializing TollBoothInsight for {entity.Index}");
                                InitializeTollBoothInsight(entity);

                                // If this is a manual tollbooth, set up barrier control to start closed
                                if (EntityManager.HasComponent<TollBoothManualData>(entity))
                                {
                                    LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - [STEP 4] - Initializing traffic light in Red for manual tollbooth {entity.Index}");
                                    InitializeTollRoadTrafficLight(entity);
                                }

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - [STEP 5] - Adding TollBoothSpawned component to {entity.Index}");
                                EntityManager.AddComponent<TollBoothSpawned>(entity);
                            }
                            else
                            {
                                EntityManager.AddComponent<TollBoothSpawned>(entity);
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Entity {entity.Index} marked as processed");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - EXCEPTION processing entity {entity.Index}: {ex.Message}");
                            LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Stack trace for entity {entity.Index}: {ex.StackTrace}");
                        }
                    }
                }
                finally
                {
                    entities.Dispose();
                    tollBoothDataArray.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - CRITICAL ERROR in update cycle: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
            }
        }

        private void InitializeTollBoothInsight(Entity tollBoothEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: \tInitializeTollBoothInsight() - Starting initialization for entity {tollBoothEntity.Index}");
            
            try
            {
                uint currentFrame = 0;
                
                if (m_SimulationSystem != null)
                {
                    try
                    {
                        currentFrame = m_SimulationSystem.frameIndex;
                    }
                    catch (System.Exception ex) 
                    {
                        currentFrame = 0;
                    }
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: \tInitializeTollBoothInsight() - SimulationSystem is null for entity {tollBoothEntity.Index}, using frame 0");
                }

                if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: \tInitializeTollBoothInsight() - Entity {tollBoothEntity.Index} already has TollBoothInsight, skipping initialization");
                    return;
                }

                var tollBoothInsight = new TollBoothInsight();
                tollBoothInsight.ResetStatistics(currentFrame);
                EntityManager.AddComponentData(tollBoothEntity, tollBoothInsight);
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \tInitializeTollBoothInsight() - FAILED to initialize TollBoothInsight for entity {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: \tInitializeTollBoothInsight() - Stack trace for entity {tollBoothEntity.Index}: {ex.StackTrace}");
            }
        }

        private string GenerateRandomTollBoothNameAdvanced()
        {            
            try
            {
                string[] prefixes = { "North", "South", "East", "West", "Central", "Upper", "Lower", "New", "Old" };
                string[] types = { "Plaza", "Station", "Gate", "Checkpoint", "Pass", "Junction", "Express", "Bridge" };
                string[] suffixes = { "A", "B", "C", "1", "2", "3", "Main", "Ext" };

                string baseName = m_TollBoothNames[m_Random.Next(m_TollBoothNames.Length)];
                int prefixChance = m_Random.Next(100);
                
                if (prefixChance < 40)
                {
                    string prefix = prefixes[m_Random.Next(prefixes.Length)];
                    baseName = $"{prefix} {baseName}";
                }

                int suffixChance = m_Random.Next(100);
                
                if (suffixChance < 20)
                {
                    string suffix = suffixes[m_Random.Next(suffixes.Length)];
                    baseName = $"{baseName}-{suffix}";
                }

                LogUtil.Info($"TollBoothSpawnSystem: \tGenerateRandomTollBoothNameAdvanced() - Final generated name: '{baseName}'");
                return baseName;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \tGenerateRandomTollBoothNameAdvanced() - ERROR generating name: {ex.Message}");
                return "Default Toll Plaza";
            }
        }

        private void SetTollboothComponents(Entity tollBoothEntity, ref TollBoothPrefabData tollBoothData)
        {
            try
            {
                if (EntityManager.TryGetComponent<Owner>(tollBoothEntity, out var ownerComponent))
                {
                    tollBoothData.BelongsToHighwayTollbooth = ownerComponent.m_Owner;
                    try
                    {
                        EntityManager.SetComponentData(tollBoothEntity, tollBoothData);
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: \tSetTollboothComponents() - FAILED to set component data for entity {tollBoothEntity.Index}: {ex.Message}");
                        throw;
                    }

                    LogUtil.Info($"TollBoothSpawnSystem: \t\tSetTollboothComponents() - [STEP 1a] - Associating tollbooth {tollBoothEntity.Index} with road {ownerComponent.m_Owner.Index}");
                    AssociateTollboothWithRoad(tollBoothEntity, ownerComponent.m_Owner);

                    LogUtil.Info($"TollBoothSpawnSystem: \t\tSetTollboothComponents() - [STEP 1b] - Assigning car lane flags to tollbooth road {ownerComponent.m_Owner.Index}");
                    AssignCarLaneFlagsToTollboothRoad(ownerComponent.m_Owner);
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: \tSetTollboothComponents() - Entity {tollBoothEntity.Index} does not have an Owner component");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \tSetTollboothComponents() - EXCEPTION processing entity {tollBoothEntity.Index}: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: \tSetTollboothComponents() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void AssignCarLaneFlagsToTollboothRoad(Entity roadEntity)
        {
            try
            {
                // Determine toll type for this chunk using chunk.Has()
                bool hasValidTollType = false;
                bool IsPublicTollRoad = false;
                CarLaneFlags flagsToApply;

                if (EntityManager.HasComponent<TollRoadPrivateTransportData>(roadEntity))
                {
                    // Private Transport: Block heavy vehicles (trucks)
                    flagsToApply = CarLaneFlags.ForbidHeavyTraffic;
                    hasValidTollType = true;
                }
                else if (EntityManager.HasComponent<TollRoadTruckData>(roadEntity))
                {
                    // Trucks Only: Block transit traffic (private cars, buses)
                    flagsToApply = CarLaneFlags.ForbidTransitTraffic;
                    hasValidTollType = true;
                }
                else if (EntityManager.HasComponent<TollRoadPublicTransportData>(roadEntity))
                {
                    // Public Transport Only: Allow only public transport vehicles
                    flagsToApply = CarLaneFlags.PublicOnly;
                    IsPublicTollRoad = true;
                    hasValidTollType = true;
                }
                else if (EntityManager.HasComponent<TollRoadServiceVehiclesData>(roadEntity))
                {
                    // Service Vehicles Only: Block both transit and heavy traffic
                    flagsToApply = CarLaneFlags.ForbidTransitTraffic | CarLaneFlags.ForbidHeavyTraffic;
                    hasValidTollType = true;
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - No valid toll type component found for road {roadEntity.Index}");
                    hasValidTollType = false;
                    return;
                }

                if (!hasValidTollType)
                    return;

                if (m_EdgeObjectData.TryGetComponent(roadEntity, out var edgeData))
                {
                    if (SubLaneObjectData.TryGetBuffer(edgeData.m_Start, out DynamicBuffer<Game.Net.SubLane> subLanesBufferEdgeStart))
                    {
                        for (int i = 0; i < subLanesBufferEdgeStart.Length; i++)
                        {
                            var subLane = subLanesBufferEdgeStart[i];

                            // Only process road lanes (skip pedestrian, track, etc.)
                            if ((subLane.m_PathMethods & PathMethod.Road) == 0)
                                continue;

                            Entity laneEntity = subLane.m_SubLane;

                            // Check if lane has CarLane component
                            if (!m_CarLaneData.TryGetComponent(laneEntity, out var carLane))
                                continue;

                            // Store original flags for comparison
                            var originalFlags = carLane.m_Flags;

                            // Apply restriction flags (preserve existing flags using |=)
                            carLane.m_Flags |= flagsToApply;

                            // Only write if flags actually changed
                            if (carLane.m_Flags != originalFlags)
                            {
                                m_CarLaneData[laneEntity] = carLane;
                                LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - Current CarLane flags: {originalFlags}, CarLane flags to apply: {flagsToApply}");
                                LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - Applied flags {m_CarLaneData[laneEntity].m_Flags} to EDGE START lane {laneEntity.Index} on tollbooth road {roadEntity.Index}");
                            }
                        }
                    }
                    else
                    {
                        LogUtil.Warn($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - No SubLanes found for road {roadEntity.Index}");
                    }
                }


                if (SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> subLanesBuffer))
                {
                    for (int i = 0; i < subLanesBuffer.Length; i++)
                    {
                        var subLane = subLanesBuffer[i];
                        bool appliedAnyFlags = false;

                        // Only process road lanes (skip pedestrian, track, etc.)
                        if ((subLane.m_PathMethods & PathMethod.Road) == 0)
                            continue;

                        Entity laneEntity = subLane.m_SubLane;

                        // Check if lane has CarLane component
                        if (!m_CarLaneData.TryGetComponent(laneEntity, out var carLane))
                            continue;

                        // Store original flags for comparison
                        var originalFlags = carLane.m_Flags;

                        // Apply restriction flags (preserve existing flags using |=)
                        carLane.m_Flags |= flagsToApply;

                        // Only write if flags actually changed
                        if (carLane.m_Flags != originalFlags)
                        {
                            m_CarLaneData[laneEntity] = carLane;
                            LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - Current CarLane flags: {originalFlags}, CarLane flags to apply: {flagsToApply}");
                            LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - Applied flags {m_CarLaneData[laneEntity].m_Flags} to lane {laneEntity.Index} on tollbooth road {roadEntity.Index}");
                            appliedAnyFlags = true;
                        }

                        // Mark road as processed to prevent reprocessing
                        // If it is a public toll road, we add the component anyway to avoid reprocessing
                        if (appliedAnyFlags || IsPublicTollRoad)
                        {
                            if (!EntityManager.HasComponent<TollRoadCarLaneApplied>(roadEntity))
                                EntityManager.AddComponent<TollRoadCarLaneApplied>(roadEntity);
                        }
                    }
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - No SubLanes found for road {roadEntity.Index}");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - FAILED to assign CarLane flags for road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssignCarLaneFlagsToTollboothRoad() - Stack trace: {ex.StackTrace}");
            }
        }

        private void AssociateTollboothWithRoad(Entity tollBoothEntity, Entity roadEntity)
        {
            try
            {
                if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
                {
                    try
                    {
                        var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                        if (tollRoadData.HasActiveTollbooth && tollRoadData.AssociatedTollbooth != tollBoothEntity)
                        {
                            LogUtil.Warn($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - Road {roadEntity.Index} already has tollbooth {tollRoadData.AssociatedTollbooth.Index} associated. Replacing with new tollbooth {tollBoothEntity.Index}");
                        }
                        tollRoadData.AssociatedTollbooth = tollBoothEntity;
                        tollRoadData.HasActiveTollbooth = true;
                        EntityManager.SetComponentData(roadEntity, tollRoadData);
                        LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - Successfully updated association - tollbooth {tollBoothEntity.Index} with toll road {roadEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - FAILED to update existing TollRoadPrefabData for road {roadEntity.Index}: {ex.Message}");
                        throw;
                    }
                }
                else
                {
                    try
                    {
                        var newTollRoadData = new TollRoadPrefabData
                        {
                            AssociatedTollbooth = tollBoothEntity,
                            HasActiveTollbooth = true
                        };
                        EntityManager.AddComponentData(roadEntity, newTollRoadData);
                        LogUtil.Info($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - Created new TollRoadPrefabData and associated tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - FAILED to create new TollRoadPrefabData for road {roadEntity.Index}: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - FAILED to associate tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: \t\t\tAssociateTollboothWithRoad() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void InitializeTollRoadTrafficLight(Entity tollBoothEntity)
        {
            try
            {
               // Get the road entity from the toll booth's owner
                if (!EntityManager.TryGetComponent<Owner>(tollBoothEntity, out var ownerComponent))
                {
                    return;
                }

                Entity roadEntity = ownerComponent.m_Owner;

                // Try to find an existing TrafficLight sub-object and set its state to Red
                if (SubObjectsObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Objects.SubObject> subObjectsBuffer))
                {
                    for (int i = 0; i < subObjectsBuffer.Length; i++)
                    {
                        Entity subObject = subObjectsBuffer[i].m_SubObject;
                        if (EntityManager.HasComponent<Game.Objects.TrafficLight>(subObject))
                        {
                            var trafficLight = EntityManager.GetComponentData<Game.Objects.TrafficLight>(subObject);
                            trafficLight.m_State = Game.Objects.TrafficLightState.Red;
                            trafficLight.m_GroupMask0 = 1;
                            trafficLight.m_GroupMask1 = 0;
                            EntityManager.SetComponentData(subObject, trafficLight);
                            LogUtil.Info($"TollBoothSpawnSystem: \tInitializeTollRoadTrafficLight() - Set existing traffic light to Red for road {roadEntity.Index}");
                            return;
                        }
                        else
                        {
                            LogUtil.Info($"TollBoothSpawnSystem: \tInitializeTollRoadTrafficLight() - No subobject with Traffic Lights was found");
                            return;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: \tInitializeTollRoadTrafficLight() - FAILED for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: \tInitializeTollRoadTrafficLight() - Stack trace: {ex.StackTrace}");
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

            LogUtil.Info($"TollBoothSpawnSystem: \tAssigned random name '{randomName}' to toll booth entity {entity.Index}");
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

        private void EnsureLogger()
        {
            if (m_LogInitialized)
            {
                return;
            }

            try
            {
                VehicleDebugLogger.Init();
                VehicleDebugLogger.LogOnce("=== TollboothPathMonitoringSystem logging started ===");
            }
            catch
            {
                // best-effort logging initialization
            }

            m_LogInitialized = true;
        }
    }
}