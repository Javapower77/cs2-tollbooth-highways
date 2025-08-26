using Colossal.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using System;
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
            LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Starting system creation");
            
            try
            {
                base.OnCreate();
                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Base.OnCreate() completed successfully");

                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Getting managed systems");
                m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
                LogUtil.Info($"TollBoothSpawnSystem: OnCreate() - PrefabSystem acquired: {(m_PrefabSystem != null ? "SUCCESS" : "FAILED")}");
                
                m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
                LogUtil.Info($"TollBoothSpawnSystem: OnCreate() - SimulationSystem acquired: {(m_SimulationSystem != null ? "SUCCESS" : "FAILED")}");
                
                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Initializing random number generator");
                m_Random = new Random((int)DateTime.Now.Ticks);
                LogUtil.Info($"TollBoothSpawnSystem: OnCreate() - Random generator initialized with seed: {(int)DateTime.Now.Ticks}");
                
                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Initializing buffer lookups");
                try
                {
                    SubLaneObjectData = GetBufferLookup<Game.Net.SubLane>(true);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - SubLaneObjectData lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize SubLaneObjectData: {ex.Message}");
                    throw;
                }

                try
                {
                    SubObjectsObjectData = GetBufferLookup<Game.Objects.SubObject>(true);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - SubObjectsObjectData lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize SubObjectsObjectData: {ex.Message}");
                    throw;
                }
                
                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Initializing component lookups for TrafficLights integration");
                try
                {
                    m_TrafficLightsData = GetComponentLookup<TrafficLights>(false);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - TrafficLights lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize TrafficLights lookup: {ex.Message}");
                    throw;
                }

                try
                {
                    m_LaneSignalData = GetComponentLookup<LaneSignal>(false);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - LaneSignal lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize LaneSignal lookup: {ex.Message}");
                    throw;
                }

                try
                {
                    m_TrafficLightObjectData = GetComponentLookup<Game.Objects.TrafficLight>(false);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - TrafficLight object lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize TrafficLight object lookup: {ex.Message}");
                    throw;
                }

                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Initializing component lookups for Lane/Node transforms");
                try
                {
                    m_LaneData = GetComponentLookup<Lane>(true);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Lane data lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize Lane data lookup: {ex.Message}");
                    throw;
                }

                try
                {
                    m_NodeData = GetComponentLookup<Node>(true);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Node data lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize Node data lookup: {ex.Message}");
                    throw;
                }

                try
                {
                    m_TransformData = GetComponentLookup<Transform>(true);
                    LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Transform data lookup initialized");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to initialize Transform data lookup: {ex.Message}");
                    throw;
                }

                LogUtil.Info("TollBoothSpawnSystem: OnCreate() - Creating entity query for unprocessed toll booths");
                try
                {
                    m_UnprocessedTollBoothQuery = GetEntityQuery(
                        ComponentType.ReadWrite<TollBoothPrefabData>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.Exclude<TollBoothSpawned>()
                    );
                    LogUtil.Info($"TollBoothSpawnSystem: OnCreate() - Entity query created successfully. IsEmpty: {m_UnprocessedTollBoothQuery.IsEmpty}");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnCreate() - Failed to create entity query: {ex.Message}");
                    throw;
                }

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
          //  LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Starting update cycle");
            
            try
            {
          //      LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Updating lookups");

          //      LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Checking for unprocessed entities");
                if (m_UnprocessedTollBoothQuery.IsEmpty)
                {
                    //LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - No unprocessed toll booth entities found, early exit");
                    return;
                }

                try
                {
                    SubLaneObjectData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - SubLaneObjectData updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update SubLaneObjectData: {ex.Message}");
                    return;
                }

                try
                {
                    SubObjectsObjectData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - SubObjectsObjectData updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update SubObjectsObjectData: {ex.Message}");
                    return;
                }

                try
                {
                    m_TrafficLightsData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - TrafficLights data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update TrafficLights data: {ex.Message}");
                    return;
                }

                try
                {
                    m_LaneSignalData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - LaneSignal data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update LaneSignal data: {ex.Message}");
                    return;
                }

                try
                {
                    m_TrafficLightObjectData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - TrafficLight object data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update TrafficLight object data: {ex.Message}");
                    return;
                }                

                try
                {
                    m_LaneData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Lane data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update Lane data: {ex.Message}");
                    return;
                }

                try
                {
                    m_NodeData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Node data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update Node data: {ex.Message}");
                    return;
                }

                try
                {
                    m_TransformData.Update(this);
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Transform data updated");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Failed to update Transform data: {ex.Message}");
                    return;
                }                

                int entityCount = m_UnprocessedTollBoothQuery.CalculateEntityCount();
                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Found {entityCount} unprocessed toll booth entities");

                LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Allocating arrays for entity processing");
                var entities = m_UnprocessedTollBoothQuery.ToEntityArray(Allocator.TempJob);
                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Entities array allocated with {entities.Length} elements");
                
                var tollBoothDataArray = m_UnprocessedTollBoothQuery.ToComponentDataArray<TollBoothPrefabData>(Allocator.TempJob);
                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - TollBoothData array allocated with {tollBoothDataArray.Length} elements");

                try
                {
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Starting entity processing loop");
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        var tollBoothData = tollBoothDataArray[i];

                        LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Processing entity {i + 1}/{entities.Length}: {entity.Index}");

                        try
                        {
                            if (tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Entity {entity.Index} has no highway association, processing...");

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Writing owner entity info for {entity.Index}");
                                WriteOwnerEntityInfo(entity, ref tollBoothData);
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Owner entity info written for {entity.Index}");

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Assigning random name for {entity.Index}");
                                AssignRandomName(entity, ref tollBoothData);
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Random name assigned for {entity.Index}");

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Initializing TollBoothInsight for {entity.Index}");
                                InitializeTollBoothInsight(entity);
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - TollBoothInsight initialized for {entity.Index}");

                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Adding TollBoothSpawned component to {entity.Index}");
                                EntityManager.AddComponent<TollBoothSpawned>(entity);
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - TollBoothSpawned component added to {entity.Index}");
                            }
                            else
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: OnUpdate() - Entity {entity.Index} already has highway association ({tollBoothData.BelongsToHighwayTollbooth.Index}), marking as processed");
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
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Entity processing loop completed");
                }
                finally
                {
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Disposing allocated arrays");
                    entities.Dispose();
                    tollBoothDataArray.Dispose();
                    LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Arrays disposed successfully");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - CRITICAL ERROR in update cycle: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
            }
            
            LogUtil.Info("TollBoothSpawnSystem: OnUpdate() - Update cycle completed");
        }

        private void InitializeTollBoothInsight(Entity tollBoothEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Starting initialization for entity {tollBoothEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Getting frame index for entity {tollBoothEntity.Index}");
                uint currentFrame = 0;
                
                if (m_SimulationSystem != null)
                {
                    LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - SimulationSystem is available for entity {tollBoothEntity.Index}");
                    try
                    {
                        currentFrame = m_SimulationSystem.frameIndex;
                        LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Got frame index {currentFrame} for entity {tollBoothEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Warn($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Could not get frameIndex for entity {tollBoothEntity.Index}, using 0. Error: {ex.Message}");
                        currentFrame = 0;
                    }
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: InitializeTollBoothInsight() - SimulationSystem is null for entity {tollBoothEntity.Index}, using frame 0");
                }

                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Checking if entity {tollBoothEntity.Index} already has TollBoothInsight component");
                if (EntityManager.HasComponent<TollBoothInsight>(tollBoothEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Entity {tollBoothEntity.Index} already has TollBoothInsight, skipping initialization");
                    return;
                }

                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Creating new TollBoothInsight for entity {tollBoothEntity.Index}");
                var tollBoothInsight = new TollBoothInsight();
                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - TollBoothInsight instance created for entity {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Resetting statistics with frame {currentFrame} for entity {tollBoothEntity.Index}");
                tollBoothInsight.ResetStatistics(currentFrame);
                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Statistics reset completed for entity {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Adding component to entity {tollBoothEntity.Index}");
                EntityManager.AddComponentData(tollBoothEntity, tollBoothInsight);
                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Component added successfully to entity {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Successfully initialized TollBoothInsight for entity {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: InitializeTollBoothInsight() - FAILED to initialize TollBoothInsight for entity {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: InitializeTollBoothInsight() - Stack trace for entity {tollBoothEntity.Index}: {ex.StackTrace}");
            }
        }

        private string GenerateRandomTollBoothNameAdvanced()
        {
            LogUtil.Info("TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Starting name generation");
            
            try
            {
                string[] prefixes = { "North", "South", "East", "West", "Central", "Upper", "Lower", "New", "Old" };
                string[] types = { "Plaza", "Station", "Gate", "Checkpoint", "Pass", "Junction", "Express", "Bridge" };
                string[] suffixes = { "A", "B", "C", "1", "2", "3", "Main", "Ext" };

                LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Selecting base name from {m_TollBoothNames.Length} options");
                string baseName = m_TollBoothNames[m_Random.Next(m_TollBoothNames.Length)];
                LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Selected base name: '{baseName}'");

                int prefixChance = m_Random.Next(100);
                LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Prefix chance: {prefixChance}% (40% needed)");
                
                if (prefixChance < 40)
                {
                    string prefix = prefixes[m_Random.Next(prefixes.Length)];
                    baseName = $"{prefix} {baseName}";
                    LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Added prefix: '{prefix}', result: '{baseName}'");
                }

                int suffixChance = m_Random.Next(100);
                LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Suffix chance: {suffixChance}% (20% needed)");
                
                if (suffixChance < 20)
                {
                    string suffix = suffixes[m_Random.Next(suffixes.Length)];
                    baseName = $"{baseName}-{suffix}";
                    LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Added suffix: '{suffix}', result: '{baseName}'");
                }

                LogUtil.Info($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - Final generated name: '{baseName}'");
                return baseName;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: GenerateRandomTollBoothNameAdvanced() - ERROR generating name: {ex.Message}");
                return "Default Toll Plaza";
            }
        }

        private void WriteOwnerEntityInfo(Entity tollBoothEntity, ref TollBoothPrefabData tollBoothData)
        {
            LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Processing entity {tollBoothEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Checking for Owner component on entity {tollBoothEntity.Index}");
                
                if (EntityManager.TryGetComponent<Owner>(tollBoothEntity, out var ownerComponent))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Owner component found for entity {tollBoothEntity.Index}");
                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Toll booth {tollBoothEntity.Index} belongs to owner {ownerComponent.m_Owner.Index}");

                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Setting owner info in tollBoothData for entity {tollBoothEntity.Index}");
                    tollBoothData.BelongsToHighwayTollbooth = ownerComponent.m_Owner;
                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Owner set to {ownerComponent.m_Owner.Index} for entity {tollBoothEntity.Index}");

                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Updating component data for entity {tollBoothEntity.Index}");
                    try
                    {
                        EntityManager.SetComponentData(tollBoothEntity, tollBoothData);
                        LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Component data updated for entity {tollBoothEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - FAILED to set component data for entity {tollBoothEntity.Index}: {ex.Message}");
                        throw;
                    }

                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Associating tollbooth {tollBoothEntity.Index} with road {ownerComponent.m_Owner.Index}");
                    AssociateTollboothWithRoad(tollBoothEntity, ownerComponent.m_Owner);
                    LogUtil.Info($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Association completed for entity {tollBoothEntity.Index}");
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Entity {tollBoothEntity.Index} does not have an Owner component");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - EXCEPTION processing entity {tollBoothEntity.Index}: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: WriteOwnerEntityInfo() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void AssociateTollboothWithRoad(Entity tollBoothEntity, Entity roadEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Associating tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Checking if road {roadEntity.Index} has TollRoadPrefabData component");
                
                if (EntityManager.HasComponent<TollRoadPrefabData>(roadEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Road {roadEntity.Index} has existing TollRoadPrefabData");
                    
                    try
                    {
                        var tollRoadData = EntityManager.GetComponentData<TollRoadPrefabData>(roadEntity);
                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Retrieved existing TollRoadPrefabData for road {roadEntity.Index}");

                        if (tollRoadData.HasActiveTollbooth && tollRoadData.AssociatedTollbooth != tollBoothEntity)
                        {
                            LogUtil.Warn($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Road {roadEntity.Index} already has tollbooth {tollRoadData.AssociatedTollbooth.Index} associated. Replacing with new tollbooth {tollBoothEntity.Index}");
                        }

                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Updating tollbooth association for road {roadEntity.Index}");
                        tollRoadData.AssociatedTollbooth = tollBoothEntity;
                        tollRoadData.HasActiveTollbooth = true;

                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Setting updated component data for road {roadEntity.Index}");
                        EntityManager.SetComponentData(roadEntity, tollRoadData);
                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Successfully updated association - tollbooth {tollBoothEntity.Index} with toll road {roadEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - FAILED to update existing TollRoadPrefabData for road {roadEntity.Index}: {ex.Message}");
                        throw;
                    }
                }
                else
                {
                    LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Road {roadEntity.Index} does not have TollRoadPrefabData, creating new one");
                    
                    try
                    {
                        var newTollRoadData = new TollRoadPrefabData
                        {
                            AssociatedTollbooth = tollBoothEntity,
                            HasActiveTollbooth = true
                        };
                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Created new TollRoadPrefabData for road {roadEntity.Index}");

                        EntityManager.AddComponentData(roadEntity, newTollRoadData);
                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Added new TollRoadPrefabData to road {roadEntity.Index}");

                        LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Created new TollRoadPrefabData and associated tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - FAILED to create new TollRoadPrefabData for road {roadEntity.Index}: {ex.Message}");
                        throw;
                    }
                }

                LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Setting up manual barrier control for tollbooth {tollBoothEntity.Index}");
                SetupManualBarrierControl(tollBoothEntity, roadEntity);
                LogUtil.Info($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Manual barrier control setup completed for tollbooth {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - FAILED to associate tollbooth {tollBoothEntity.Index} with road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: AssociateTollboothWithRoad() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void SetupManualBarrierControl(Entity tollBoothEntity, Entity roadEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Starting setup for tollbooth {tollBoothEntity.Index} and road {roadEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Checking if tollbooth {tollBoothEntity.Index} has TollBoothManualData component");
                
                if (!EntityManager.HasComponent<TollBoothManualData>(tollBoothEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Tollbooth {tollBoothEntity.Index} is not a manual tollbooth, skipping barrier setup");
                    return;
                }

                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Tollbooth {tollBoothEntity.Index} is manual, setting up barrier control - BARRIER STARTS CLOSED");

                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Setting up lane signals for tollbooth {tollBoothEntity.Index}");
                SetupLaneSignalsForBarrier(tollBoothEntity, roadEntity);
                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Lane signals setup completed for tollbooth {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Setting up traffic light for tollbooth {tollBoothEntity.Index}");
                SetupTrafficLightForBarrier(roadEntity, tollBoothEntity);
                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Traffic light setup completed for tollbooth {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Ensuring barrier closed state for tollbooth {tollBoothEntity.Index}");
                EnsureBarrierClosedStateDirectly(roadEntity);
                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Barrier closed state ensured for tollbooth {tollBoothEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: SetupManualBarrierControl() - Successfully set up manual barrier control for tollbooth {tollBoothEntity.Index} - BARRIER IS CLOSED");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: SetupManualBarrierControl() - FAILED to setup manual barrier control for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: SetupManualBarrierControl() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void EnsureBarrierClosedStateDirectly(Entity roadEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Ensuring closed state for road {roadEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Setting lane signals to STOP for road {roadEntity.Index}");
                
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out var sublaneObjects))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Found {sublaneObjects.Length} sublanes for road {roadEntity.Index}");
                    
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Checking sublane {i} for road {roadEntity.Index}");
                        
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;
                            LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Found road lane {laneEntity.Index} for road {roadEntity.Index}");

                            if (m_LaneSignalData.HasComponent(laneEntity))
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Setting lane signal to STOP for lane {laneEntity.Index}");
                                var laneSignal = m_LaneSignalData[laneEntity];
                                laneSignal.m_Signal = LaneSignalType.Stop;
                                m_LaneSignalData[laneEntity] = laneSignal;
                                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Lane signal set to STOP for lane {laneEntity.Index}");
                            }
                            else
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Lane {laneEntity.Index} has no lane signal component");
                            }
                            break;
                        }
                    }
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - No sublanes buffer found for road {roadEntity.Index}");
                }

                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Setting traffic light objects to RED for road {roadEntity.Index}");
                
                if (SubObjectsObjectData.TryGetBuffer(roadEntity, out var subObjects))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Found {subObjects.Length} subobjects for road {roadEntity.Index}");
                    
                    for (int i = 0; i < subObjects.Length; i++)
                    {
                        LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Checking subobject {i} for road {roadEntity.Index}");
                        
                        if (m_TrafficLightObjectData.HasComponent(subObjects[i].m_SubObject))
                        {
                            Entity trafficLightEntity = subObjects[i].m_SubObject;
                            LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Found traffic light {trafficLightEntity.Index} for road {roadEntity.Index}");

                            LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Setting traffic light to RED for light {trafficLightEntity.Index}");
                            var trafficLight = m_TrafficLightObjectData[trafficLightEntity];
                            trafficLight.m_State = Game.Objects.TrafficLightState.Red;
                            m_TrafficLightObjectData[trafficLightEntity] = trafficLight;
                            LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Traffic light set to RED for light {trafficLightEntity.Index}");
                            break;
                        }
                    }
                }
                else
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - No subobjects buffer found for road {roadEntity.Index}");
                }

                LogUtil.Info($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Barrier closed state enforced DIRECTLY for road {roadEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - FAILED to ensure barrier closed state for road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: EnsureBarrierClosedStateDirectly() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void SetupLaneSignalsForBarrier(Entity tollBoothEntity, Entity roadEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Setting up lane signals for road {roadEntity.Index} / tollbooth {tollBoothEntity.Index}");

            try
            {
                if (!SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - No sublanes on road {roadEntity.Index}");
                    return;
                }

                // Find first ROAD lane (can be improved to select specific direction later)
                Entity laneEntity = Entity.Null;
                for (int i = 0; i < sublaneObjects.Length; i++)
                {
                    if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                    {
                        laneEntity = sublaneObjects[i].m_SubLane;
                        break;
                    }
                }

                if (laneEntity == Entity.Null)
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - No road lane found for road {roadEntity.Index}");
                    return;
                }

                // Ensure LaneSignal
                if (!EntityManager.HasComponent<LaneSignal>(laneEntity))
                {
                    EntityManager.AddComponent<LaneSignal>(laneEntity);
                    LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Added LaneSignal to lane {laneEntity.Index}");
                }

                // Create blocker entity (dummy) for STOP state
                Entity blockerEntity = EntityManager.CreateEntity();
                EntityManager.AddComponent<TollBarrierBlocker>(blockerEntity);

                if (EntityManager.TryGetComponent<TollBoothManualData>(tollBoothEntity, out var manualData))
                {
                    EntityManager.AddComponentData(blockerEntity, new TollBarrierBlockerData
                    {
                        TollBoothEntity = tollBoothEntity,
                        ProcessingTime = manualData.ProcessingTime
                    });
                }

                // Closed by default
                var laneSignal = new LaneSignal
                {
                    m_Flags = LaneSignalFlags.CanExtend,
                    m_Signal = LaneSignalType.Stop,
                    m_GroupMask = 1,
                    m_Default = 0,
                    m_Priority = 0,
                    m_Petitioner = Entity.Null,
                    m_Blocker = blockerEntity
                };
                EntityManager.SetComponentData(laneEntity, laneSignal);

                // Store blocker reference
                if (EntityManager.HasComponent<TollBoothPrefabData>(tollBoothEntity))
                {
                    var tbData = EntityManager.GetComponentData<TollBoothPrefabData>(tollBoothEntity);
                    tbData.BarrierBlockerEntity = blockerEntity;
                    EntityManager.SetComponentData(tollBoothEntity, tbData);
                }

                // ---------------------------------------------------------------------------------
                // TRAFFIC LIGHTS PLACEMENT (FIX):
                // Previous implementation incorrectly attached Game.Net.TrafficLights to the lane.
                // The TrafficLightInitializationSystem expects the component on an entity that owns
                // the SubLane buffer (the road/edge entity) to build signal groups.
                // We therefore attach TrafficLights to the ROAD ENTITY (edge) and mark it Updated.
                // ---------------------------------------------------------------------------------
                if (!EntityManager.HasComponent<Game.Net.TrafficLights>(roadEntity))
                {
                    EntityManager.AddComponentData(roadEntity, new Game.Net.TrafficLights
                    {
                        m_State = Game.Net. TrafficLightState.None,
                        m_Flags = 0,
                        m_SignalGroupCount = 0,
                        m_CurrentSignalGroup = 0,
                        m_NextSignalGroup = 0,
                        m_Timer = 0
                    });
                    LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Added TrafficLights component to road(edge) {roadEntity.Index}");
                }
                else
                {
                    // Reset to ensure clean initialization pass
                    var tl = EntityManager.GetComponentData<Game.Net.TrafficLights>(roadEntity);
                    tl.m_State = Game.Net.TrafficLightState.None;
                    tl.m_CurrentSignalGroup = 0;
                    tl.m_NextSignalGroup = 0;
                    tl.m_Timer = 0;
                    EntityManager.SetComponentData(roadEntity, tl);
                    LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Reset existing TrafficLights on road {roadEntity.Index}");
                }

                // Ensure Updated so TrafficLightInitializationSystem processes this edge this frame
                if (!EntityManager.HasComponent<Updated>(roadEntity))
                {
                    EntityManager.AddComponent<Updated>(roadEntity);
                    LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Added Updated to road {roadEntity.Index}");
                }

                // OPTIONAL: Try to choose the node (start/end) closest to the tollbooth for future refinement.
                // (Currently not required because TrafficLightInitializationSystem works off SubLane buffer
                // on the road entity. This is here for future directional control.)
                if (EntityManager.HasComponent<Lane>(laneEntity) &&
                    EntityManager.HasComponent<Transform>(tollBoothEntity))
                {
                    var laneData = EntityManager.GetComponentData<Lane>(laneEntity);
                    PathNode startNode = default;
                    PathNode endNode = laneData.m_EndNode; // Lane struct excerpt only exposes m_EndNode in provided signature.

                    // If later you expose start node, compute distance to tollbooth transform and decide orientation.
                    // Placeholder comment to mark where logic would go.
                }

                LogUtil.Info($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - Completed (lane {laneEntity.Index}, road {roadEntity.Index})");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: SetupLaneSignalsForBarrier() - FAILED for road {roadEntity.Index}. Error: {ex.Message}");
                throw;
            }
        }

        private void SetupTrafficLightForBarrier(Entity roadEntity, Entity tollBoothEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Setting up traffic light for road {roadEntity.Index} and tollbooth {tollBoothEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Getting subobjects buffer for road {roadEntity.Index}");
                
                if (!SubObjectsObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Objects.SubObject> subObjects))
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - No subobjects found for road {roadEntity.Index}");
                    return;
                }

                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Found {subObjects.Length} subobjects for road {roadEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Finding traffic light subobject for road {roadEntity.Index}");
                Entity trafficLightEntity = Entity.Null;
                for (int i = 0; i < subObjects.Length; i++)
                {
                    LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Checking subobject {i}: {subObjects[i].m_SubObject.Index}");
                    
                    if (EntityManager.HasComponent<Game.Objects.TrafficLight>(subObjects[i].m_SubObject))
                    {
                        trafficLightEntity = subObjects[i].m_SubObject;
                        LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Found traffic light entity: {trafficLightEntity.Index}");
                        break;
                    }
                }

                if (trafficLightEntity == Entity.Null)
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - No traffic light subobject found for road {roadEntity.Index}");
                    return;
                }

                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Configuring traffic light {trafficLightEntity.Index} for manual barrier control");
                var trafficLight = new Game.Objects.TrafficLight
                {
                    m_State = Game.Objects.TrafficLightState.Red,
                    m_GroupMask0 = 1,
                    m_GroupMask1 = 0
                };

                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Setting traffic light data for entity {trafficLightEntity.Index}");
                EntityManager.SetComponentData(trafficLightEntity, trafficLight);
                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Traffic light {trafficLightEntity.Index} configured for manual barrier control");

                LogUtil.Info($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Successfully configured traffic light {trafficLightEntity.Index} for tollbooth {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - FAILED to setup traffic light for road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: SetupTrafficLightForBarrier() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Open the manual toll barrier by directly controlling lane signals and traffic lights.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        public void OpenBarrier(Entity tollBoothEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Opening barrier for tollbooth {tollBoothEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Getting associated road for tollbooth {tollBoothEntity.Index}");
                
                if (!EntityManager.TryGetComponent<TollBoothPrefabData>(tollBoothEntity, out var tollBoothData) ||
                    tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: OpenBarrier() - Cannot open barrier - no associated road for tollbooth {tollBoothEntity.Index}");
                    return;
                }

                Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;
                Entity blockerEntity = tollBoothData.BarrierBlockerEntity;
                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Road: {roadEntity.Index}, Blocker: {blockerEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Finding and updating lane signal for road {roadEntity.Index}");
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out var sublaneObjects))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Found {sublaneObjects.Length} sublanes");
                    
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;
                            LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Processing lane {laneEntity.Index}");
                            
                            if (EntityManager.HasComponent<LaneSignal>(laneEntity))
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Updating lane signal for lane {laneEntity.Index}");
                                var laneSignal = EntityManager.GetComponentData<LaneSignal>(laneEntity);
                                
                                laneSignal.m_Blocker = Entity.Null;
                                laneSignal.m_Signal = LaneSignalType.Go;
                                
                                EntityManager.SetComponentData(laneEntity, laneSignal);
                                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - OPENED barrier for tollbooth {tollBoothEntity.Index} (blocker removed)");
                            }
                            break;
                        }
                    }
                }

                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Updating traffic light visuals for road {roadEntity.Index}");
                UpdateTrafficLightForBarrierState(roadEntity, Game.Objects.TrafficLightState.Green);
                LogUtil.Info($"TollBoothSpawnSystem: OpenBarrier() - Barrier opening completed for tollbooth {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: OpenBarrier() - FAILED to open barrier for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: OpenBarrier() - Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Closes the manual toll barrier by directly controlling lane signals and traffic lights.
        /// IMPORTANT: This is the DEFAULT state - barrier should always return to CLOSED.
        /// </summary>
        /// <param name="tollBoothEntity">The tollbooth entity</param>
        public void CloseBarrier(Entity tollBoothEntity)
        {
            LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Closing barrier for tollbooth {tollBoothEntity.Index}");
            
            try
            {
                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Getting associated road for tollbooth {tollBoothEntity.Index}");
                
                if (!EntityManager.TryGetComponent<TollBoothPrefabData>(tollBoothEntity, out var tollBoothData) ||
                    tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: CloseBarrier() - Cannot close barrier - no associated road for tollbooth {tollBoothEntity.Index}");
                    return;
                }

                Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;
                Entity blockerEntity = tollBoothData.BarrierBlockerEntity;
                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Road: {roadEntity.Index}, Blocker: {blockerEntity.Index}");

                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Finding and updating lane signal for road {roadEntity.Index}");
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out var sublaneObjects))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Found {sublaneObjects.Length} sublanes");
                    
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;
                            LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Processing lane {laneEntity.Index}");
                            
                            if (EntityManager.HasComponent<LaneSignal>(laneEntity))
                            {
                                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Updating lane signal for lane {laneEntity.Index}");
                                var laneSignal = EntityManager.GetComponentData<LaneSignal>(laneEntity);
                                
                                laneSignal.m_Blocker = blockerEntity;
                                laneSignal.m_Petitioner = Entity.Null;
                                laneSignal.m_Signal = LaneSignalType.Stop;
                                
                                EntityManager.SetComponentData(laneEntity, laneSignal);
                                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - CLOSED barrier for tollbooth {tollBoothEntity.Index} (blocker restored)");
                            }
                            break;
                        }
                    }
                }

                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Updating traffic light visuals for road {roadEntity.Index}");
                UpdateTrafficLightForBarrierState(roadEntity, Game.Objects.TrafficLightState.Red);
                LogUtil.Info($"TollBoothSpawnSystem: CloseBarrier() - Barrier closing completed for tollbooth {tollBoothEntity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: CloseBarrier() - FAILED to close barrier for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: CloseBarrier() - Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateLaneSignalForBarrierState(Entity roadEntity, LaneSignalType signalType)
        {
            LogUtil.Info($"TollBoothSpawnSystem: UpdateLaneSignalForBarrierState() - Updating signal to {signalType} for road {roadEntity.Index}");
            
            try
            {
                if (!SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: UpdateLaneSignalForBarrierState() - No sublanes buffer for road {roadEntity.Index}");
                    return;
                }

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
                            
                            LogUtil.Info($"TollBoothSpawnSystem: UpdateLaneSignalForBarrierState() - Updated lane signal to {signalType} for lane {laneEntity.Index}");
                        }
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: UpdateLaneSignalForBarrierState() - FAILED to update lane signal for road {roadEntity.Index}. Error: {ex.Message}");
            }
        }

        private void UpdateTrafficLightForBarrierState(Entity roadEntity, Game.Objects.TrafficLightState lightState)
        {
            LogUtil.Info($"TollBoothSpawnSystem: UpdateTrafficLightForBarrierState() - Updating light to {lightState} for road {roadEntity.Index}");
            
            try
            {
                if (!SubObjectsObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Objects.SubObject> subObjects))
                {
                    LogUtil.Warn($"TollBoothSpawnSystem: UpdateTrafficLightForBarrierState() - No subobjects buffer for road {roadEntity.Index}");
                    return;
                }

                for (int i = 0; i < subObjects.Length; i++)
                {
                    if (EntityManager.HasComponent<Game.Objects.TrafficLight>(subObjects[i].m_SubObject))
                    {
                        Entity trafficLightEntity = subObjects[i].m_SubObject;
                        
                        var trafficLight = EntityManager.GetComponentData<Game.Objects.TrafficLight>(trafficLightEntity);
                        trafficLight.m_State = lightState;
                        EntityManager.SetComponentData(trafficLightEntity, trafficLight);
                        
                        LogUtil.Info($"TollBoothSpawnSystem: UpdateTrafficLightForBarrierState() - Updated traffic light to {lightState} for light {trafficLightEntity.Index}");
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: UpdateTrafficLightForBarrierState() - FAILED to update traffic light for road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothSpawnSystem: UpdateTrafficLightForBarrierState() - Stack trace: {ex.StackTrace}");
                throw;
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
                LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Checking barrier state for tollbooth {tollBoothEntity.Index}");
                
                // Check if this is a manual tollbooth
                if (!EntityManager.HasComponent<TollBoothManualData>(tollBoothEntity))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Entity {tollBoothEntity.Index} is not a manual tollbooth");
                    return false; // Not a manual tollbooth
                }

                LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Getting associated road for tollbooth {tollBoothEntity.Index}");
                
                if (!EntityManager.TryGetComponent<TollBoothPrefabData>(tollBoothEntity, out var tollBoothData) ||
                    tollBoothData.BelongsToHighwayTollbooth == Entity.Null)
                {
                    LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Entity {tollBoothEntity.Index} has no associated road");
                    return false;
                }

                Entity roadEntity = tollBoothData.BelongsToHighwayTollbooth;

                LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Checking lane signal state for road {roadEntity.Index}");
                // Check lane signal state DIRECTLY (ignore TrafficLights)
                if (SubLaneObjectData.TryGetBuffer(roadEntity, out DynamicBuffer<Game.Net.SubLane> sublaneObjects))
                {
                    LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Found {sublaneObjects.Length} sublanes for road {roadEntity.Index}");
                    
                    for (int i = 0; i < sublaneObjects.Length; i++)
                    {
                        LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Checking sublane {i} for road {roadEntity.Index}");
                        
                        if (sublaneObjects[i].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            Entity laneEntity = sublaneObjects[i].m_SubLane;

                            if (EntityManager.HasComponent<LaneSignal>(laneEntity))
                            {
                                var laneSignal = EntityManager.GetComponentData<LaneSignal>(laneEntity);
                                bool isOpen = laneSignal.m_Signal == LaneSignalType.Go;
                                LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Barrier is currently {(isOpen ? "OPEN" : "CLOSED")} for tollbooth {tollBoothEntity.Index}");
                                return isOpen;
                            }
                            break;
                        }
                    }
                }

                LogUtil.Info($"TollBoothSpawnSystem: IsBarrierOpen() - Barrier state could not be determined, defaulting to CLOSED for tollbooth {tollBoothEntity.Index}");
                return false; // Default to closed
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothSpawnSystem: IsBarrierOpen() - Failed to check barrier state for tollbooth {tollBoothEntity.Index}. Error: {ex.Message}");
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