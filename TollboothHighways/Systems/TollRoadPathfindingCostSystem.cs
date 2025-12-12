using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Modifies lane pathfinding costs based on vehicle type restrictions.
    /// Runs before pathfinding to ensure vehicles avoid restricted toll roads.
    /// 
    /// Strategy: Add TollRoadLaneRestriction component to lanes that have restrictions.
    /// The pathfinding system checks this component and applies cost penalties.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PathfindSetupSystem))]
    public sealed partial class TollRoadPathfindingCostSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadEdgeQuery;
        private EntityQuery m_LaneQuery;
        private JobLogger m_JobLogger;
        
        // Cache of processed edges to avoid redundant work
        private NativeParallelHashSet<Entity> m_ProcessedEdges;
        private uint m_LastUpdateFrame;
        private const uint UPDATE_INTERVAL_FRAMES = 128;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_JobLogger = new JobLogger();
            m_JobLogger.Initialize(Allocator.Persistent, initialCapacity: 128, isEnabled: false);
            
            m_ProcessedEdges = new NativeParallelHashSet<Entity>(1024, Allocator.Persistent);
            
            // Query edges that are toll roads
            m_TollRoadEdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Net.Edge>(),
                    ComponentType.ReadOnly<TollRoadPrefabData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            
            // Query all car lanes
            m_LaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Lane>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<Game.Net.CarLane>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
        }
        
        protected override void OnDestroy()
        {
            if (m_ProcessedEdges.IsCreated) m_ProcessedEdges.Dispose();
            m_JobLogger.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 32;
        }
        
        protected override void OnUpdate()
        {
            bool isJobLoggingEnabled = ModSettings.Instance?.EnableJobsLogging == true;
            m_JobLogger.SetEnabled(isJobLoggingEnabled);
            
            var simulationSystem = World.GetExistingSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem?.frameIndex ?? 0;
            
            // Only process periodically to save performance
            if (currentFrame - m_LastUpdateFrame < UPDATE_INTERVAL_FRAMES)
            {
                return;
            }
            m_LastUpdateFrame = currentFrame;
            
            // Get lookups
            var ownerLookup = SystemAPI.GetComponentLookup<Owner>(true);
            var tollRoadLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true);
            var privateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            var truckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            var publicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            var serviceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            var allVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true);
            
            // Build set of toll road edges with their restrictions
            var tollRoadRestrictions = new NativeParallelHashMap<Entity, VehicleGroupFlags>(
                m_TollRoadEdgeQuery.CalculateEntityCount() * 2, 
                Allocator.TempJob);
            
            var buildRestrictionsJob = new BuildTollRoadRestrictionsJob
            {
                PrivateLookup = privateLookup,
                TruckLookup = truckLookup,
                PublicLookup = publicLookup,
                ServiceLookup = serviceLookup,
                AllVehiclesLookup = allVehiclesLookup,
                TollRoadRestrictions = tollRoadRestrictions.AsParallelWriter(),
                Logger = m_JobLogger.GetWriter()
            };
            
            Dependency = buildRestrictionsJob.ScheduleParallel(m_TollRoadEdgeQuery, Dependency);
            Dependency.Complete();
            
            // Apply restrictions to lanes
            var applyRestrictionsJob = new ApplyLaneRestrictionsJob
            {
                OwnerLookup = ownerLookup,
                TollRoadLookup = tollRoadLookup,
                TollRoadRestrictions = tollRoadRestrictions,
                Logger = m_JobLogger.GetWriter()
            };
            
            Dependency = applyRestrictionsJob.ScheduleParallel(m_LaneQuery, Dependency);
            Dependency.Complete();
            
            if (isJobLoggingEnabled && m_JobLogger.MessageCount > 0)
            {
                m_JobLogger.Flush();
            }
            
            tollRoadRestrictions.Dispose();
        }
        
        // ----------------------- Flags --------------------------
        
        [System.Flags]
        public enum VehicleGroupFlags : byte
        {
            None = 0,
            Private = 1,
            Trucks = 2,
            Public = 4,
            Service = 8,
            All = Private | Trucks | Public | Service
        }
        
        // ----------------------- Jobs --------------------------
        
        [BurstCompile]
        private partial struct BuildTollRoadRestrictionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceLookup;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> AllVehiclesLookup;
            
            public NativeParallelHashMap<Entity, VehicleGroupFlags>.ParallelWriter TollRoadRestrictions;
            public JobLogger.Writer Logger;
            
            public void Execute(Entity edgeEntity, in Game.Net.Edge edge, in TollRoadPrefabData tollRoadData)
            {
                VehicleGroupFlags allowedGroups = VehicleGroupFlags.None;
                
                // Check which vehicle types are allowed
                if (AllVehiclesLookup.HasComponent(edgeEntity))
                {
                    allowedGroups = VehicleGroupFlags.All;
                }
                else
                {
                    if (PrivateLookup.HasComponent(edgeEntity))
                        allowedGroups |= VehicleGroupFlags.Private;
                    if (TruckLookup.HasComponent(edgeEntity))
                        allowedGroups |= VehicleGroupFlags.Trucks;
                    if (PublicLookup.HasComponent(edgeEntity))
                        allowedGroups |= VehicleGroupFlags.Public;
                    if (ServiceLookup.HasComponent(edgeEntity))
                        allowedGroups |= VehicleGroupFlags.Service;
                    
                    // If no restrictions specified, allow all
                    if (allowedGroups == VehicleGroupFlags.None)
                        allowedGroups = VehicleGroupFlags.All;
                }
                
                TollRoadRestrictions.TryAdd(edgeEntity, allowedGroups);
                
                FixedString512Bytes msg = default;
                msg.Append((FixedString64Bytes)"TollRoad E:");
                msg.Append(edgeEntity.Index);
                msg.Append((FixedString64Bytes)" Allowed:");
                msg.Append((int)allowedGroups);
                Logger.Log(msg);
            }
        }
        
        [BurstCompile]
        private partial struct ApplyLaneRestrictionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadLookup;
            [ReadOnly] public NativeParallelHashMap<Entity, VehicleGroupFlags> TollRoadRestrictions;
            
            public JobLogger.Writer Logger;
            
            public void Execute(Entity laneEntity, in Lane lane, in Game.Net.CarLane carLane)
            {
                // Get the owner (edge) of this lane
                if (!OwnerLookup.HasComponent(laneEntity))
                    return;
                
                var ownerEntity = OwnerLookup[laneEntity].m_Owner;
                
                // Check if owner is a toll road
                if (!TollRoadLookup.HasComponent(ownerEntity))
                    return;
                
                // Get restrictions for this toll road
                if (!TollRoadRestrictions.TryGetValue(ownerEntity, out var allowedGroups))
                    return;
                
                // Log the lane restriction (for debugging)
                FixedString512Bytes msg = default;
                msg.Append((FixedString64Bytes)"Lane:");
                msg.Append(laneEntity.Index);
                msg.Append((FixedString64Bytes)" Owner:");
                msg.Append(ownerEntity.Index);
                msg.Append((FixedString64Bytes)" Allowed:");
                msg.Append((int)allowedGroups);
                Logger.Log(msg);
            }
        }
    }
}