using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using CarLane = Game.Net.CarLane;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Tollbooth pathfinding bias system:
    ///  • Applies massive cost penalties to incompatible vehicle-tollbooth combinations
    ///  • Uses behavior-time channel to influence routing without breaking pathfinding
    ///  • Delta-based updates prevent stacking on repeated runs
    /// 
    /// Per AGENTS.MD: Runs in Burst-compiled parallel jobs for performance
    /// </summary>
    public sealed partial class TollboothPathfindBiasSystem : GameSystemBase
    {
        // ----------------------- Queries -----------------------
        private EntityQuery m_TollLaneQuery;
        
        // ----------------------- Lookups -----------------------
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<PrefabRef> m_PrefabLookup;
        private ComponentLookup<CarLane> m_CarLaneLookup;
        
        // Tollbooth type lookups
        private ComponentLookup<TollRoadPrefabData> m_TollRoadPrefabLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;
        
        // ----------------------- Caches ------------------------
        private NativeParallelHashMap<Entity, float> m_PreviousTollPenaltySec;
        private NativeParallelHashMap<Entity, float> m_PreviousDensityAdd;
        
        // ----------------------- Config ------------------------
        private struct TollboothBiasConfig : IComponentData
        {
            // Massive penalties for incompatible vehicles
            public float IncompatibleVehiclePenaltySec;  // e.g., 1000s (makes route virtually impossible)
            public float IncompatibleDensityAdd;         // e.g., 10.0 (extreme perceived congestion)
            
            // Moderate bias for compatible vehicles during peak hours
            public float PeakHourBiasSec;                // e.g., 5s extra during peak
            public float PeakHourDensityAdd;             // e.g., 0.2 density add
            
            // Small incentive for express lanes
            public float ExpressLaneBonusSec;            // e.g., -2s (negative = faster)
            
            public bool EnableDebugLogging;
        }
        
        // ----------------------- Results -----------------------
        private struct TollPenaltyResult 
        { 
            public Entity LaneEntity; 
            public float PenaltySec;
            public float DensityAdd;
        }
        
        // ----------------------- Lifecycle ---------------------
        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_TollLaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Lane>(),
                    ComponentType.ReadOnly<CarLane>(),
                    ComponentType.ReadOnly<Owner>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Game.Tools.Temp>()
                }
            });
            
            m_PreviousTollPenaltySec = new NativeParallelHashMap<Entity, float>(1024, Allocator.Persistent);
            m_PreviousDensityAdd = new NativeParallelHashMap<Entity, float>(1024, Allocator.Persistent);
            
            RequireForUpdate(m_TollLaneQuery);
            RequireForUpdate<TollRoadPrefabData>();
            
            LogUtil.Info("TollboothPathfindBiasSystem: Created - manages tollbooth routing costs");
        }
        
        protected override void OnDestroy()
        {
            if (m_PreviousTollPenaltySec.IsCreated) m_PreviousTollPenaltySec.Dispose();
            if (m_PreviousDensityAdd.IsCreated) m_PreviousDensityAdd.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Update every ~8 seconds (same as reference system)
            return 262144 / 32;
        }
        
        // ----------------------- Update ------------------------
        protected override void OnUpdate()
        {
            // Refresh lookups using SystemAPI for Burst compatibility
            m_OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true);
            m_PrefabLookup = SystemAPI.GetComponentLookup<PrefabRef>(true);
            m_CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>(true);
            
            // Tollbooth lookups
            m_TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true);
            m_PrivateTransportLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            m_PublicTransportLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            m_ServiceVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            
            // Build config from settings
            var config = SystemAPI.HasSingleton<TollboothBiasConfig>()
                ? SystemAPI.GetSingleton<TollboothBiasConfig>()
                : new TollboothBiasConfig
                {
                    IncompatibleVehiclePenaltySec = 1000f,  // Massive penalty
                    IncompatibleDensityAdd = 10.0f,         // Extreme congestion
                    PeakHourBiasSec = 5f,
                    PeakHourDensityAdd = 0.2f,
                    ExpressLaneBonusSec = -2f,
                    EnableDebugLogging = ModSettings.Instance?.EnableGeneralLogging ?? false
                };
            
            // Get current time for peak hour calculation
            var simSystem = World.GetExistingSystemManaged<Game.Simulation.SimulationSystem>();
            uint currentFrame = simSystem?.frameIndex ?? 0;
            bool isPeakHours = IsPeakHours(currentFrame);
            
            int laneCount = m_TollLaneQuery.CalculateEntityCount();
            var tollPenaltyResults = new NativeList<TollPenaltyResult>(math.max(1, laneCount), Allocator.TempJob);
            
            // Schedule parallel job to calculate tollbooth penalties
            var calculateJob = new CalculateTollboothPenaltiesJob
            {
                OwnerLookup = m_OwnerLookup,
                CarLaneLookup = m_CarLaneLookup,
                
                TollRoadPrefabLookup = m_TollRoadPrefabLookup,
                PrivateTransportLookup = m_PrivateTransportLookup,
                TruckLookup = m_TruckLookup,
                PublicTransportLookup = m_PublicTransportLookup,
                ServiceVehiclesLookup = m_ServiceVehiclesLookup,
                
                IncompatiblePenaltySec = config.IncompatibleVehiclePenaltySec,
                IncompatibleDensityAdd = config.IncompatibleDensityAdd,
                PeakHourBiasSec = isPeakHours ? config.PeakHourBiasSec : 0f,
                PeakHourDensityAdd = isPeakHours ? config.PeakHourDensityAdd : 0f,
                ExpressLaneBonusSec = config.ExpressLaneBonusSec,
                
                Results = tollPenaltyResults.AsParallelWriter()
            };
            
            var jobHandle = calculateJob.ScheduleParallel(m_TollLaneQuery, default);
            jobHandle.Complete();
            
            // Apply penalties to pathfinding data (main thread)
            ApplyPenaltiesToPathfinding(tollPenaltyResults, config.EnableDebugLogging);
            
            // Dispose temp collections
            tollPenaltyResults.Dispose();
        }
        
        private void ApplyPenaltiesToPathfinding(NativeList<TollPenaltyResult> results, bool enableLogging)
        {
            var pathfindQueue = World.GetOrCreateSystemManaged<PathfindQueueSystem>();
            var pathfindData = pathfindQueue.GetDataContainer(out var dependency);
            dependency.Complete();
            
            int appliedCount = 0;
            
            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (!TryGetEdge(pathfindData, result.LaneEntity, out var edgeId)) 
                    continue;
                
                // Apply behavior-time penalty (delta-based to prevent stacking)
                float previousPenalty = m_PreviousTollPenaltySec.TryGetValue(result.LaneEntity, out var p) ? p : 0f;
                float deltaPenalty = result.PenaltySec - previousPenalty;
                
                if (math.abs(deltaPenalty) > 0.01f)
                {
                    ref var costs = ref pathfindData.SetCosts(edgeId);
                    costs.m_Value.y += deltaPenalty; // y = behavior-time channel
                    m_PreviousTollPenaltySec[result.LaneEntity] = result.PenaltySec;
                    appliedCount++;
                }
                
                // Apply density modifier (makes road appear congested)
                float previousDensity = m_PreviousDensityAdd.TryGetValue(result.LaneEntity, out var d) ? d : 0f;
                float deltaDensity = result.DensityAdd - previousDensity;
                
                if (math.abs(deltaDensity) > 0.001f)
                {
                    ref float density = ref pathfindData.SetDensity(edgeId);
                    density += deltaDensity;
                    m_PreviousDensityAdd[result.LaneEntity] = result.DensityAdd;
                }
            }
            
            if (appliedCount > 0 && enableLogging)
            {
                LogUtil.Info($"TollboothPathfindBiasSystem: Applied penalties to {appliedCount} toll lanes");
            }
            
            // Notify pathfinding system of changes
            pathfindQueue.AddDataReader(default);
        }
        
        private static bool TryGetEdge(NativePathfindData data, Entity owner, out EdgeID edgeId)
        {
            if (data.GetEdge(owner, out edgeId)) return true;
            if (data.GetSecondaryEdge(owner, out edgeId)) return true;
            edgeId = default;
            return false;
        }

        private bool IsPeakHours(uint currentFrame)
        {
            const float FRAMES_PER_DAY = 25920f;
            const float FRAMES_PER_HOUR = 1080f;
            float hourOfDay = (currentFrame % FRAMES_PER_DAY) / FRAMES_PER_HOUR;
            return (hourOfDay >= 7 && hourOfDay < 9) || (hourOfDay >= 17 && hourOfDay < 19);
        }
        
        // ----------------------- Jobs --------------------------
#if WITH_BURST
        [BurstCompile]
#endif
        private partial struct CalculateTollboothPenaltiesJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<CarLane> CarLaneLookup;
            
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicTransportLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceVehiclesLookup;
            
            public float IncompatiblePenaltySec;
            public float IncompatibleDensityAdd;
            public float PeakHourBiasSec;
            public float PeakHourDensityAdd;
            public float ExpressLaneBonusSec;
            
            [WriteOnly] public NativeList<TollPenaltyResult>.ParallelWriter Results;
            
            public void Execute(Entity laneEntity, in Lane lane, in Owner owner)
            {
                // Check if this lane belongs to a toll road
                var roadEntity = owner.m_Owner;
                if (!TollRoadPrefabLookup.HasComponent(roadEntity))
                    return;
                
                var tollRoadData = TollRoadPrefabLookup[roadEntity];
                
                // Get CarLane to check flags
                if (!CarLaneLookup.HasComponent(laneEntity))
                    return;
                
                var carLane = CarLaneLookup[laneEntity];
                
                // Calculate penalties based on tollbooth type and vehicle restrictions
                float penaltySec = 0f;
                float densityAdd = 0f;
                
                // Apply penalties based on tollbooth vehicle restrictions
                // These make incompatible routes virtually impossible
                if (PrivateTransportLookup.HasComponent(roadEntity))
                {
                    // Private transport only - penalize trucks and transit
                    if ((carLane.m_Flags & CarLaneFlags.ForbidHeavyTraffic) == 0)
                    {
                        // This lane allows heavy traffic but shouldn't on private tollbooth
                        penaltySec += IncompatiblePenaltySec * 0.5f; // Partial penalty for mixed lanes
                    }
                    densityAdd += PeakHourDensityAdd; // Add peak hour congestion
                }
                else if (TruckLookup.HasComponent(roadEntity))
                {
                    // Trucks only - heavily penalize private cars
                    if ((carLane.m_Flags & CarLaneFlags.ForbidTransitTraffic) == 0)
                    {
                        penaltySec += IncompatiblePenaltySec;
                        densityAdd += IncompatibleDensityAdd;
                    }
                    else
                    {
                        // Correct vehicle type, add peak hour bias
                        penaltySec += PeakHourBiasSec;
                        densityAdd += PeakHourDensityAdd;
                    }
                }
                else if (PublicTransportLookup.HasComponent(roadEntity))
                {
                    // Public transport only
                    if ((carLane.m_Flags & CarLaneFlags.PublicOnly) == 0)
                    {
                        penaltySec += IncompatiblePenaltySec;
                        densityAdd += IncompatibleDensityAdd;
                    }
                    else
                    {
                        // Public vehicles get slight bonus
                        penaltySec += ExpressLaneBonusSec;
                    }
                }
                else if (ServiceVehiclesLookup.HasComponent(roadEntity))
                {
                    // Service vehicles - apply moderate penalty to non-service
                    penaltySec += PeakHourBiasSec * 2f; // Double peak bias for non-service
                    densityAdd += PeakHourDensityAdd;
                }
                
                // Check tollbooth type for additional modifiers
                if (tollRoadData.TollboothType == (int)TollboothType.Automatic)
                {
                    // Express lanes get speed bonus
                    penaltySec += ExpressLaneBonusSec;
                }
                else if (tollRoadData.TollboothType == (int)TollboothType.Manual)
                {
                    // Manual tollbooths add delay
                    penaltySec += 10f; // 10 second delay for manual processing
                    densityAdd += 0.5f; // Appears more congested
                }
                
                // Only write results if there are penalties to apply
                if (math.abs(penaltySec) > 0.01f || math.abs(densityAdd) > 0.001f)
                {
                    Results.AddNoResize(new TollPenaltyResult
                    {
                        LaneEntity = laneEntity,
                        PenaltySec = penaltySec,
                        DensityAdd = densityAdd
                    });
                }
            }
        }
    }
}