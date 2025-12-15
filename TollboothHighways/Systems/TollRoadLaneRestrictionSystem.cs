using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Adds restriction components to toll road lanes for pathfinding integration.
    /// This system marks lanes with their allowed vehicle types so the pathfinding
    /// system can apply appropriate costs.
    /// 
    /// Per AGENTS.MD: Burst-compatible, parallel execution.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PathfindSetupSystem))]
    public sealed partial class TollRoadLaneRestrictionSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadLaneQuery;
        private EntityQuery m_TollRoadQuery;
        private JobLogger m_JobLogger;
        
        private uint m_LastUpdateFrame;
        private const uint UPDATE_INTERVAL_FRAMES = 64;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_JobLogger = new JobLogger();
            m_JobLogger.Initialize(Allocator.Persistent, initialCapacity: 256, isEnabled: false);
            
            // Query lanes owned by roads
            m_TollRoadLaneQuery = GetEntityQuery(new EntityQueryDesc
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
            
            // Query toll roads
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<TollRoadPrefabData>()
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
            m_JobLogger.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16;
        }
        
        protected override void OnUpdate()
        {
            bool isJobLoggingEnabled = ModSettings.Instance?.EnableJobsLogging == true;
            m_JobLogger.SetEnabled(isJobLoggingEnabled);
            
            var simulationSystem = World.GetExistingSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem?.frameIndex ?? 0;
            
            // Only process periodically
            if (currentFrame - m_LastUpdateFrame < UPDATE_INTERVAL_FRAMES)
            {
                return;
            }
            m_LastUpdateFrame = currentFrame;
            
            // Build toll road set
            var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.TempJob);
            var tollRoadSet = new NativeParallelHashSet<Entity>(tollRoadEntities.Length * 2, Allocator.TempJob);
            
            // Build restriction map
            var restrictionMap = new NativeParallelHashMap<Entity, TollRoadRestrictionFlags>(
                tollRoadEntities.Length * 2, 
                Allocator.TempJob);
            
            var privateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            var truckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            var publicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            var serviceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            var allVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true);
            
            // Build restriction flags for each toll road
            for (int i = 0; i < tollRoadEntities.Length; i++)
            {
                var entity = tollRoadEntities[i];
                tollRoadSet.Add(entity);
                
                var flags = TollRoadRestrictionFlags.None;
                
                if (allVehiclesLookup.HasComponent(entity))
                {
                    flags = TollRoadRestrictionFlags.All;
                }
                else
                {
                    if (privateLookup.HasComponent(entity))
                        flags |= TollRoadRestrictionFlags.Private;
                    if (truckLookup.HasComponent(entity))
                        flags |= TollRoadRestrictionFlags.Trucks;
                    if (publicLookup.HasComponent(entity))
                        flags |= TollRoadRestrictionFlags.Public;
                    if (serviceLookup.HasComponent(entity))
                        flags |= TollRoadRestrictionFlags.Service;
                    
                    // If no restrictions specified, allow all
                    if (flags == TollRoadRestrictionFlags.None)
                        flags = TollRoadRestrictionFlags.All;
                }
                
                restrictionMap.TryAdd(entity, flags);
            }
            
            // Get type handles
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
            var ownerTypeHandle = SystemAPI.GetComponentTypeHandle<Owner>(true);
            var carLaneTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Net.CarLane>(true);
            
            // Build lane restriction data
            var laneRestrictions = new NativeParallelHashMap<Entity, TollRoadRestrictionFlags>(
                m_TollRoadLaneQuery.CalculateEntityCount(),
                Allocator.TempJob);
            
            var buildLaneRestrictionsJob = new BuildLaneRestrictionsJob
            {
                EntityTypeHandle = entityTypeHandle,
                OwnerTypeHandle = ownerTypeHandle,
                CarLaneTypeHandle = carLaneTypeHandle,
                TollRoadSet = tollRoadSet,
                RestrictionMap = restrictionMap,
                LaneRestrictions = laneRestrictions.AsParallelWriter(),
                Logger = m_JobLogger.GetWriter()
            };
            
            Dependency = buildLaneRestrictionsJob.ScheduleParallel(m_TollRoadLaneQuery, Dependency);
            Dependency.Complete();
            
            // Store lane restrictions for use by pathfinding patches
            TollRoadLaneRestrictionData.UpdateRestrictions(laneRestrictions);
            
            // Flush logs
            if (isJobLoggingEnabled && m_JobLogger.MessageCount > 0)
            {
                m_JobLogger.Flush();
            }
            
            // Cleanup
            tollRoadEntities.Dispose();
            tollRoadSet.Dispose();
            restrictionMap.Dispose();
            laneRestrictions.Dispose();
        }
        
        // ----------------------- Flags --------------------------
        
        [System.Flags]
        public enum TollRoadRestrictionFlags : byte
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
        private struct BuildLaneRestrictionsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Owner> OwnerTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Net.CarLane> CarLaneTypeHandle;
            
            [ReadOnly] public NativeParallelHashSet<Entity> TollRoadSet;
            [ReadOnly] public NativeParallelHashMap<Entity, TollRoadRestrictionFlags> RestrictionMap;
            
            public NativeParallelHashMap<Entity, TollRoadRestrictionFlags>.ParallelWriter LaneRestrictions;
            public JobLogger.Writer Logger;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var owners = chunk.GetNativeArray(ref OwnerTypeHandle);
                
                for (int i = 0; i < entities.Length; i++)
                {
                    var laneEntity = entities[i];
                    var owner = owners[i];
                    var roadEntity = owner.m_Owner;
                    
                    // Check if owner is a toll road
                    if (!TollRoadSet.Contains(roadEntity))
                        continue;
                    
                    // Get restrictions for this road
                    if (!RestrictionMap.TryGetValue(roadEntity, out var flags))
                        continue;
                    
                    // Store lane restriction
                    LaneRestrictions.TryAdd(laneEntity, flags);
                    
                    FixedString512Bytes msg = default;
                    msg.Append((FixedString64Bytes)"Lane:");
                    msg.Append(laneEntity.Index);
                    msg.Append((FixedString64Bytes)" Road:");
                    msg.Append(roadEntity.Index);
                    msg.Append((FixedString64Bytes)" Flags:");
                    msg.Append((int)flags);
                    Logger.Log(msg);
                }
            }
        }
    }
    
    /// <summary>
    /// Static storage for lane restriction data accessible by Harmony patches.
    /// </summary>
    public static class TollRoadLaneRestrictionData
    {
        private static NativeParallelHashMap<Entity, TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags> _laneRestrictions;
        private static readonly object _lock = new object();
        
        public static void UpdateRestrictions(NativeParallelHashMap<Entity, TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags> restrictions)
        {
            lock (_lock)
            {
                if (_laneRestrictions.IsCreated)
                {
                    _laneRestrictions.Dispose();
                }
                
                _laneRestrictions = new NativeParallelHashMap<Entity, TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags>(
                    restrictions.Count(),
                    Allocator.Persistent);
                
                var keys = restrictions.GetKeyArray(Allocator.Temp);
                var values = restrictions.GetValueArray(Allocator.Temp);
                
                for (int i = 0; i < keys.Length; i++)
                {
                    _laneRestrictions.TryAdd(keys[i], values[i]);
                }
                
                keys.Dispose();
                values.Dispose();
            }
        }
        
        public static bool TryGetRestriction(Entity laneEntity, out TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags flags)
        {
            flags = TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags.All;
            
            lock (_lock)
            {
                if (!_laneRestrictions.IsCreated)
                    return false;
                
                return _laneRestrictions.TryGetValue(laneEntity, out flags);
            }
        }
        
        public static bool IsVehicleGroupAllowed(Entity laneEntity, Domain.Enums.VehicleGroup vehicleGroup)
        {
            if (!TryGetRestriction(laneEntity, out var flags))
                return true;
            
            if (flags == TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags.All)
                return true;
            
            switch (vehicleGroup)
            {
                case Domain.Enums.VehicleGroup.PrivateTransport:
                    return (flags & TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags.Private) != 0;
                case Domain.Enums.VehicleGroup.Trucks:
                    return (flags & TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags.Trucks) != 0;
                case Domain.Enums.VehicleGroup.PublicTransport:
                    return (flags & TollRoadLaneRestrictionSystem.TollRoadRestrictionFlags.Public) != 0;
                case Domain.Enums.VehicleGroup.ServiceVehicles:
                    return true; // Always allow service vehicles
                default:
                    return true;
            }
        }
        
        public static void Dispose()
        {
            lock (_lock)
            {
                if (_laneRestrictions.IsCreated)
                {
                    _laneRestrictions.Dispose();
                }
            }
        }
    }
}