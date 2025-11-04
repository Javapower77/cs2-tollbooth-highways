using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst.Intrinsics;
using Game;
using Game.Common;
using Game.Net;
using Game.Vehicles;
using Game.Pathfind;
using Game.Simulation;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using CarLaneFlags = Game.Net.CarLaneFlags;
using Game.Tools;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Dynamically blocks tollbooth lanes for incompatible vehicles during pathfinding.
    /// Per AGENTS.MD: Runs before pathfinding to set blockages, then clears them after.
    /// Uses Burst-compiled jobs for performance.
    /// </summary>
    [UpdateBefore(typeof(PathfindSetupSystem))]
    public partial class TollboothDynamicLaneBlockingSystem : GameSystemBase
    {
        private EntityQuery m_PathfindingVehiclesQuery;
        private EntityQuery m_TollLanesQuery;
        private SimulationSystem m_SimulationSystem;
        
        // Native collections for frame-based blocking
        private NativeHashMap<Entity, CarLaneFlags> m_OriginalLaneFlags;
        private NativeList<Entity> m_ModifiedLanes;
        private NativeParallelHashMap<Entity, VehicleGroup> m_VehicleTypesCache;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            
            // Query for vehicles that are about to pathfind
            m_PathfindingVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>(),
                    ComponentType.ReadOnly<CarCurrentLane>()
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<PathInformation>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            
            // Query for toll road lanes
            m_TollLanesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CarLane>(),
                    ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<TollRoadPrefabData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            
            // Initialize native collections
            m_OriginalLaneFlags = new NativeHashMap<Entity, CarLaneFlags>(1000, Allocator.Persistent);
            m_ModifiedLanes = new NativeList<Entity>(100, Allocator.Persistent);
            m_VehicleTypesCache = new NativeParallelHashMap<Entity, VehicleGroup>(500, Allocator.Persistent);
            
            RequireForUpdate(m_PathfindingVehiclesQuery);
            RequireForUpdate(m_TollLanesQuery);
            
            LogUtil.Info("TollboothDynamicLaneBlockingSystem: Created - manages per-frame lane blocking");
        }
        
        protected override void OnUpdate()
        {
            uint currentFrame = m_SimulationSystem.frameIndex;
            
            // Clear previous frame's modifications first
            RestoreLaneFlags();
            
            // Get vehicles that need pathfinding this frame
            var pathfindingVehicles = m_PathfindingVehiclesQuery.ToEntityArray(Allocator.TempJob);
            
            if (pathfindingVehicles.Length == 0)
            {
                pathfindingVehicles.Dispose();
                return;
            }
            
            // Job 1: Identify vehicle types
            var identifyVehiclesJob = new IdentifyVehicleTypesJob
            {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                Vehicles = pathfindingVehicles,
                VehicleTypesCache = m_VehicleTypesCache.AsParallelWriter(),
                m_PublicTransportVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                m_DeliveryTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                m_GarbageTruckLookup = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                m_PoliceCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                m_AmbulanceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                m_FireEngineLookup = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                m_HearseLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                m_MaintenanceVehicleLookup = SystemAPI.GetComponentLookup<Game.Vehicles.MaintenanceVehicle>(true),
                m_PostVanLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                m_TaxiLookup = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                m_PersonalCarLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                m_MotorbikeLookup = SystemAPI.GetComponentLookup<MotorbikePrefabData>(true),
                m_EvacuatingTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.EvacuatingTransport>(true),
                m_PrisonerTransportLookup = SystemAPI.GetComponentLookup<Game.Vehicles.PrisonerTransport>(true),
            };
            
            var identifyHandle = identifyVehiclesJob.Schedule(pathfindingVehicles.Length, 32);
            
            // Job 2: Block incompatible lanes for current vehicles
            var blockLanesJob = new BlockIncompatibleLanesJob
            {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                CarLaneHandle = SystemAPI.GetComponentTypeHandle<CarLane>(),
                OwnerHandle = SystemAPI.GetComponentTypeHandle<Owner>(true),
                
                VehicleTypesCache = m_VehicleTypesCache,
                OriginalLaneFlags = m_OriginalLaneFlags,
                ModifiedLanes = m_ModifiedLanes,
                
                TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),
                TollRoadPrivateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                TollRoadTruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                TollRoadPublicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                TollRoadServiceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),
                
                CurrentFrame = currentFrame
            };
            
            var blockHandle = blockLanesJob.ScheduleParallel(m_TollLanesQuery, identifyHandle);
            
            // Complete blocking before pathfinding runs
            blockHandle.Complete();
            
            // Schedule restoration after pathfinding (will happen next frame)
            // This ensures pathfinding sees the blocked lanes
            pathfindingVehicles.Dispose();
        }

        private void RestoreLaneFlags()
        {
            if (m_ModifiedLanes.Length == 0)
                return;

            // Restore original flags for modified lanes
            var restoreJob = new RestoreLaneFlagsJob
            {
                m_EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ModifiedLanes = m_ModifiedLanes.AsArray(),
                OriginalLaneFlags = m_OriginalLaneFlags,
                CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>()
            };

            restoreJob.Schedule(m_ModifiedLanes.Length, 32).Complete();

            // Clear tracking collections
            m_ModifiedLanes.Clear();
            m_OriginalLaneFlags.Clear();
            m_VehicleTypesCache.Clear();
        }
#if WITH_BURST        
        [BurstCompile]
#endif
        private struct IdentifyVehicleTypesJob : IJobParallelFor
        {
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;
            [ReadOnly] public NativeArray<Entity> Vehicles;
            [WriteOnly] public NativeParallelHashMap<Entity, VehicleGroup>.ParallelWriter VehicleTypesCache;

            [ReadOnly] public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportVehicleLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTruckLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> m_GarbageTruckLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> m_PoliceCarLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> m_FireEngineLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> m_HearseLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.MaintenanceVehicle> m_MaintenanceVehicleLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> m_PostVanLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Taxi> m_TaxiLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarLookup;
            [ReadOnly] public ComponentLookup<MotorbikePrefabData> m_MotorbikeLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.EvacuatingTransport> m_EvacuatingTransportLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PrisonerTransport> m_PrisonerTransportLookup;
            public void Execute(int index)
            {
                var vehicleEntity = Vehicles[index];
                var vehicleType = DetermineVehicleType(vehicleEntity);
                VehicleTypesCache.TryAdd(vehicleEntity, vehicleType);
            }

            private VehicleGroup DetermineVehicleType(Entity vehicleEntity)
            {
                // Service vehicles (highest priority)
                if (m_PoliceCarLookup.HasComponent(vehicleEntity) ||
                    m_GarbageTruckLookup.HasComponent(vehicleEntity) ||
                    m_AmbulanceLookup.HasComponent(vehicleEntity) ||
                    m_FireEngineLookup.HasComponent(vehicleEntity) ||
                    m_EvacuatingTransportLookup.HasComponent(vehicleEntity) ||
                    m_HearseLookup.HasComponent(vehicleEntity) ||
                    m_MaintenanceVehicleLookup.HasComponent(vehicleEntity) ||
                    m_PrisonerTransportLookup.HasComponent(vehicleEntity) ||
                    m_PostVanLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.ServiceVehicles;
                }

                // Public transport (buses, taxis)
                if (m_PublicTransportVehicleLookup.HasComponent(vehicleEntity) ||
                    m_TaxiLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PublicTransport;
                }

                // Trucks
                if (m_DeliveryTruckLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.Trucks;
                }

                // Private cars (default)
                if (m_PersonalCarLookup.HasComponent(vehicleEntity) ||
                    m_MotorbikeLookup.HasComponent(vehicleEntity))
                {
                    return VehicleGroup.PrivateTransport;
                }

                return VehicleGroup.PrivateTransport; // Default fallback
            }
        }
 
 #if WITH_BURST       
        [BurstCompile]
#endif
        private struct BlockIncompatibleLanesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;
            public ComponentTypeHandle<CarLane> CarLaneHandle;
            [ReadOnly] public ComponentTypeHandle<Owner> OwnerHandle;
            
            [ReadOnly] public NativeParallelHashMap<Entity, VehicleGroup> VehicleTypesCache;
            public NativeHashMap<Entity, CarLaneFlags> OriginalLaneFlags;
            public NativeList<Entity> ModifiedLanes;
            
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> TollRoadPrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TollRoadTruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> TollRoadPublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> TollRoadServiceLookup;
            
            [ReadOnly] public uint CurrentFrame;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.Has(ref OwnerHandle))
                    return;
                
                var carLanes = chunk.GetNativeArray(ref CarLaneHandle);
                var owners = chunk.GetNativeArray(ref OwnerHandle);
                var entities = chunk.GetNativeArray(m_EntityTypeHandle);
                
                for (int i = 0; i < chunk.Count; i++)
                {
                    var laneEntity = entities[i];
                    var owner = owners[i];
                    var roadEntity = owner.m_Owner;
                    
                    // Check if this is a toll road
                    if (!TollRoadPrefabLookup.HasComponent(roadEntity))
                        continue;
                    
                    var carLane = carLanes[i];
                    var originalFlags = carLane.m_Flags;
                    var shouldBlock = false;
                    
                    // Check each active vehicle type against this toll road
                    foreach (var kvp in VehicleTypesCache)
                    {
                        var vehicleType = kvp.Value;
                        
                        if (!IsVehicleAllowedOnTollRoad(roadEntity, vehicleType))
                        {
                            shouldBlock = true;
                            break;
                        }
                    }
                    
                    if (shouldBlock)
                    {
                        // Store original flags if not already stored
                        if (!OriginalLaneFlags.ContainsKey(laneEntity))
                        {
                            OriginalLaneFlags.TryAdd(laneEntity, originalFlags);
                            ModifiedLanes.Add(laneEntity);
                        }
                        
                        // Apply maximum blocking to force rerouting
                        carLane.m_Flags |= CarLaneFlags.Forbidden;
                        carLane.m_BlockageStart = 0;
                        carLane.m_BlockageEnd = 255;
                        
                        carLanes[i] = carLane;
                        
                        VehicleDebugLogger.LogOnce($"Frame {CurrentFrame}: Blocked lane {laneEntity.Index} on toll road {roadEntity.Index}");
                    }
                }
            }

            private bool IsVehicleAllowedOnTollRoad(Entity roadEntity, VehicleGroup vehicleGroup)
            {
                // Private Transport Only
                if (TollRoadPrivateLookup.HasComponent(roadEntity))
                {
                     return vehicleGroup == VehicleGroup.PrivateTransport;
                }
                
                // Trucks Only
                if (TollRoadTruckLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.Trucks;
                }
                
                // Public Transport Only
                if (TollRoadPublicLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.PublicTransport;
                }
                
                // Service Vehicles Only
                if (TollRoadServiceLookup.HasComponent(roadEntity))
                {
                    return vehicleGroup == VehicleGroup.ServiceVehicles;
                }
                
                // Default: allow all
                return true;
            }
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct RestoreLaneFlagsJob : IJobParallelFor
        {
            [ReadOnly] public EntityTypeHandle m_EntityTypeHandle;
            [ReadOnly] public NativeArray<Entity> ModifiedLanes;
            [ReadOnly] public NativeHashMap<Entity, CarLaneFlags> OriginalLaneFlags;
            public ComponentLookup<CarLane> CarLaneLookup;
            
            public void Execute(int index)
            {
                var laneEntity = ModifiedLanes[index];
                
                if (OriginalLaneFlags.TryGetValue(laneEntity, out var originalFlags))
                {
                    if (CarLaneLookup.TryGetComponent(laneEntity, out var carLane))
                    {
                        carLane.m_Flags = originalFlags;
                        carLane.m_BlockageStart = 255;
                        carLane.m_BlockageEnd = 0;
                        CarLaneLookup[laneEntity] = carLane;
                    }
                }
            }
        }
        
        protected override void OnDestroy()
        {
            // Restore all modified lanes before destroying
            RestoreLaneFlags();
            
            // Dispose native collections
            if (m_OriginalLaneFlags.IsCreated)
                m_OriginalLaneFlags.Dispose();
            if (m_ModifiedLanes.IsCreated)
                m_ModifiedLanes.Dispose();
            if (m_VehicleTypesCache.IsCreated)
                m_VehicleTypesCache.Dispose();
            
            base.OnDestroy();
        }
    }
}