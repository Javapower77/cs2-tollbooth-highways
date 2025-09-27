using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using System.Collections.Generic;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using CarLane = Game.Net.CarLane;
using VehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System responsible for recreating vehicle paths when new tollbooths are spawned.
    /// Ensures only allowed vehicle types can pass through their corresponding tollbooth roads.
    /// </summary>
    public partial class VehicleRepathSystem : GameSystemBase
    {
        private EndFrameBarrier m_EndFrameBarrier;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private EntityQuery m_VehicleQuery;
        private EntityQuery m_TollRoadQuery;
        
        // Temporary entity used to restrict access
        private Entity m_TemporaryRestrictionEntity;

        /// <summary>
        /// Burst-compatible method to map vehicle type to vehicle group.
        /// Replaces Dictionary.TryGetValue which is not supported in Burst compilation.
        /// </summary>
        /// <param name="vehicleType">The vehicle type to map</param>
        /// <returns>The corresponding vehicle group</returns>
#if WITH_BURST
        [BurstCompile]
#endif
        public static VehicleGroup GetVehicleGroup(VehicleType vehicleType)
        {
            return vehicleType switch
            {
                VehicleType.PersonalCar => VehicleGroup.PrivateTransport,
                VehicleType.PersonalCarWithTrailer => VehicleGroup.PrivateTransport,
                VehicleType.Motorcycle => VehicleGroup.PrivateTransport,
                VehicleType.Taxi => VehicleGroup.PublicTransport,
                VehicleType.Truck => VehicleGroup.Trucks,
                VehicleType.TruckWithTrailer => VehicleGroup.Trucks,
                VehicleType.Bus => VehicleGroup.PublicTransport,
                VehicleType.ParkMaintenance => VehicleGroup.ServiceVehicles,
                VehicleType.RoadMaintenance => VehicleGroup.ServiceVehicles,
                VehicleType.Ambulance => VehicleGroup.ServiceVehicles,
                VehicleType.EvacuatingTransport => VehicleGroup.ServiceVehicles,
                VehicleType.FireEngine => VehicleGroup.ServiceVehicles,
                VehicleType.GarbageTruck => VehicleGroup.ServiceVehicles,
                VehicleType.Hearse => VehicleGroup.ServiceVehicles,
                VehicleType.PoliceCar => VehicleGroup.ServiceVehicles,
                VehicleType.PostVan => VehicleGroup.ServiceVehicles,
                VehicleType.PrisonerTransport => VehicleGroup.ServiceVehicles,
                _ => VehicleGroup.PrivateTransport // Default fallback for None and unknown types
            };
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            
            // Query for vehicles that need repathing
            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Vehicle>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<Target>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<RepathCreated>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Destroyed>()
                }
            });

            // Query for toll roads
            m_TollRoadQuery = GetEntityQuery(ComponentType.ReadOnly<TollRoadPrefabData>());
            
            // Create temporary restriction entity
            m_TemporaryRestrictionEntity = EntityManager.CreateEntity();
            EntityManager.SetName(m_TemporaryRestrictionEntity, "TempTollRestriction");
        }

        protected override void OnUpdate()
        {
            if (m_VehicleQuery.IsEmpty || m_TollRoadQuery.IsEmpty)
                return;

            var job = new VehicleRepathJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                CarCurrentLaneType = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(true),
                PathOwnerType = SystemAPI.GetComponentTypeHandle<PathOwner>(true),
                TargetType = SystemAPI.GetComponentTypeHandle<Target>(true),
                
                PathElements = SystemAPI.GetBufferLookup<PathElement>(true),
                CarLaneData = SystemAPI.GetComponentLookup<CarLane>(),
                TollRoadPrefabData = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),
                PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                
                // Vehicle identification components
                PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                DeliveryTruckData = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true),
                PublicTransportData = SystemAPI.GetComponentLookup<Game.Vehicles.PublicTransport>(true),
                TaxiData = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(true),
                AmbulanceData = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                FireEngineData = SystemAPI.GetComponentLookup<Game.Vehicles.FireEngine>(true),
                GarbageTruckData = SystemAPI.GetComponentLookup<Game.Vehicles.GarbageTruck>(true),
                PoliceCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PoliceCar>(true),
                HearseData = SystemAPI.GetComponentLookup<Game.Vehicles.Hearse>(true),
                PostVanData = SystemAPI.GetComponentLookup<Game.Vehicles.PostVan>(true),
                PrisonerTransportData = SystemAPI.GetComponentLookup<Game.Vehicles.PrisonerTransport>(true),
                ParkMaintenanceData = SystemAPI.GetComponentLookup<Game.Vehicles.ParkMaintenanceVehicle>(true),
                RoadMaintenanceData = SystemAPI.GetComponentLookup<Game.Vehicles.RoadMaintenanceVehicle>(true),
                EvacuatingTransportData = SystemAPI.GetComponentLookup<Game.Vehicles.EvacuatingTransport>(true),
                LayoutElementData = SystemAPI.GetBufferLookup<LayoutElement>(true),
                MotorbikeData = SystemAPI.GetComponentLookup<MotorbikePrefabData>(true),
                
                // Tollbooth components
                TollRoadPrivateTransportData = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                TollRoadTruckData = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                TollRoadPublicTransportData = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                TollRoadServiceVehiclesData = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),
                TollRoadAllVehiclesData = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true),
                
                CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
                TemporaryRestrictionEntity = m_TemporaryRestrictionEntity,
                
                RestrictedLanes = new NativeParallelHashMap<Entity, Entity>(1000, Allocator.TempJob)
            };

            var jobHandle = job.ScheduleParallel(m_VehicleQuery, Dependency);
            job.RestrictedLanes.Dispose(jobHandle);
            
            m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
            m_PathfindSetupSystem.AddQueueWriter(jobHandle);
            Dependency = jobHandle;
        }

        protected override void OnDestroy()
        {
            if (EntityManager.Exists(m_TemporaryRestrictionEntity))
            {
                EntityManager.DestroyEntity(m_TemporaryRestrictionEntity);
            }
            base.OnDestroy();
        }
#if WITH_BURST
        [BurstCompile]
#endif
        private struct VehicleRepathJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CarCurrentLane> CarCurrentLaneType;
            [ReadOnly] public ComponentTypeHandle<PathOwner> PathOwnerType;
            [ReadOnly] public ComponentTypeHandle<Target> TargetType;
            
            [ReadOnly] public BufferLookup<PathElement> PathElements;
            public ComponentLookup<CarLane> CarLaneData;
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadPrefabData;
            [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefData;
            
            // Vehicle identification components
            [ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> PersonalCarData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> DeliveryTruckData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PublicTransport> PublicTransportData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Taxi> TaxiData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> AmbulanceData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> FireEngineData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> GarbageTruckData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> PoliceCarData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> HearseData;
            [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> PostVanData;
            [ReadOnly] public ComponentLookup<PrisonerTransport> PrisonerTransportData;
            [ReadOnly] public ComponentLookup<ParkMaintenanceVehicle> ParkMaintenanceData;
            [ReadOnly] public ComponentLookup<RoadMaintenanceVehicle> RoadMaintenanceData;
            [ReadOnly] public ComponentLookup<EvacuatingTransport> EvacuatingTransportData;
            [ReadOnly] public BufferLookup<LayoutElement> LayoutElementData;
            [ReadOnly] public ComponentLookup<MotorbikePrefabData> MotorbikeData;
            
            // Tollbooth components
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> TollRoadPrivateTransportData;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TollRoadTruckData;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> TollRoadPublicTransportData;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> TollRoadServiceVehiclesData;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> TollRoadAllVehiclesData;
            
            public EntityCommandBuffer.ParallelWriter CommandBuffer;
            public NativeQueue<SetupQueueItem>.ParallelWriter PathfindQueue;
            [ReadOnly] public Entity TemporaryRestrictionEntity;
            
            [NativeDisableParallelForRestriction]
            public NativeParallelHashMap<Entity, Entity> RestrictedLanes;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var currentLanes = chunk.GetNativeArray(ref CarCurrentLaneType);
                var pathOwners = chunk.GetNativeArray(ref PathOwnerType);
                var targets = chunk.GetNativeArray(ref TargetType);

                for (int i = 0; i < entities.Length; i++)
                {
                    var vehicleEntity = entities[i];
                    var currentLane = currentLanes[i];
                    var pathOwner = pathOwners[i];
                    var target = targets[i];

                    if (!PathElements.HasBuffer(vehicleEntity))
                        continue;

                    var pathElements = PathElements[vehicleEntity];
                    if (pathElements.Length == 0)
                        continue;

                    // Determine vehicle type
                    var vehicleType = GetVehicleType(vehicleEntity);
                    if (vehicleType == VehicleType.None)
                        continue;

                    // Check for toll roads in path and handle restrictions
                    var needsRepath = ProcessTollRoadsInPath(pathElements, vehicleType, unfilteredChunkIndex);

                    if (needsRepath)
                    {
                        // Set up pathfinding to recreate the path
                        SetupVehicleRepath(vehicleEntity, currentLane, pathOwner, target, unfilteredChunkIndex);
                        
                        // Mark vehicle as repathed
                        CommandBuffer.AddComponent<RepathCreated>(unfilteredChunkIndex, vehicleEntity);
                    }
                }
            }

            private VehicleType GetVehicleType(Entity vehicleEntity)
            {
                // Check for trailers first
                if (LayoutElementData.HasBuffer(vehicleEntity))
                {
                    var layout = LayoutElementData[vehicleEntity];
                    if (layout.Length > 1)
                    {
                        // Check if it's a car with trailer
                        if (PersonalCarData.HasComponent(layout[0].m_Vehicle) || 
                            PersonalCarData.HasComponent(layout[1].m_Vehicle))
                        {
                            return VehicleType.PersonalCarWithTrailer;
                        }
                        else
                        {
                            return VehicleType.TruckWithTrailer;
                        }
                    }
                }

                // Check specific vehicle types
                if (PublicTransportData.HasComponent(vehicleEntity))
                    return VehicleType.Bus;
                if (DeliveryTruckData.HasComponent(vehicleEntity))
                    return VehicleType.Truck;
                if (PoliceCarData.HasComponent(vehicleEntity))
                    return VehicleType.PoliceCar;
                if (GarbageTruckData.HasComponent(vehicleEntity))
                    return VehicleType.GarbageTruck;
                if (TaxiData.HasComponent(vehicleEntity))
                    return VehicleType.Taxi;
                if (AmbulanceData.HasComponent(vehicleEntity))
                    return VehicleType.Ambulance;
                if (FireEngineData.HasComponent(vehicleEntity))
                    return VehicleType.FireEngine;
                if (EvacuatingTransportData.HasComponent(vehicleEntity))
                    return VehicleType.EvacuatingTransport;
                if (ParkMaintenanceData.HasComponent(vehicleEntity))
                    return VehicleType.ParkMaintenance;
                if (RoadMaintenanceData.HasComponent(vehicleEntity))
                    return VehicleType.RoadMaintenance;
                if (HearseData.HasComponent(vehicleEntity))
                    return VehicleType.Hearse;
                if (PrisonerTransportData.HasComponent(vehicleEntity))
                    return VehicleType.PrisonerTransport;
                if (PostVanData.HasComponent(vehicleEntity))
                    return VehicleType.PostVan;
                if (MotorbikeData.HasComponent(vehicleEntity))
                    return VehicleType.Motorcycle;
                if (PersonalCarData.HasComponent(vehicleEntity))
                    return VehicleType.PersonalCar;

                return VehicleType.None;
            }

            private bool ProcessTollRoadsInPath(DynamicBuffer<PathElement> pathElements, VehicleType vehicleType, int jobIndex)
            {
                var needsRepath = false;
                var restrictedLanesInThisPath = new NativeList<Entity>(Allocator.Temp);

                for (int i = 0; i < pathElements.Length; i++)
                {
                    var pathElement = pathElements[i];
                    
                    // Check if this path element has a toll road prefab
                    if (!PrefabRefData.HasComponent(pathElement.m_Target))
                        continue;

                    var prefabRef = PrefabRefData[pathElement.m_Target];
                    if (!TollRoadPrefabData.HasComponent(prefabRef.m_Prefab))
                        continue;

                    var tollRoadData = TollRoadPrefabData[prefabRef.m_Prefab];
                    if (!tollRoadData.HasActiveTollbooth)
                        continue;

                    // Check if vehicle is allowed on this toll road
                    if (!IsVehicleAllowedOnTollRoad(tollRoadData.AssociatedTollbooth, vehicleType))
                    {
                        // Restrict access to this lane
                        if (CarLaneData.HasComponent(pathElement.m_Target))
                        {
                            var carLane = CarLaneData[pathElement.m_Target];
                            var originalRestriction = carLane.m_AccessRestriction;
                            
                            // Set temporary restriction
                            carLane.m_AccessRestriction = TemporaryRestrictionEntity;
                            CarLaneData[pathElement.m_Target] = carLane;
                            
                            // Store for cleanup later
                            restrictedLanesInThisPath.Add(pathElement.m_Target);
                            RestrictedLanes.TryAdd(pathElement.m_Target, originalRestriction);
                            
                            needsRepath = true;
                        }
                    }
                }

                restrictedLanesInThisPath.Dispose();
                return needsRepath;
            }

            private bool IsVehicleAllowedOnTollRoad(Entity tollboothEntity, VehicleType vehicleType)
            {
                // Check if tollbooth has universal access
                if (TollRoadAllVehiclesData.HasComponent(tollboothEntity))
                    return true;

                // Map vehicle type to vehicle group using Burst-compatible method
                var vehicleGroup = VehicleRepathSystem.GetVehicleGroup(vehicleType);

                return vehicleGroup switch
                {
                    VehicleGroup.PrivateTransport => TollRoadPrivateTransportData.HasComponent(tollboothEntity),
                    VehicleGroup.Trucks => TollRoadTruckData.HasComponent(tollboothEntity),
                    VehicleGroup.PublicTransport => TollRoadPublicTransportData.HasComponent(tollboothEntity),
                    VehicleGroup.ServiceVehicles => TollRoadServiceVehiclesData.HasComponent(tollboothEntity),
                    _ => false
                };
            }

            private void SetupVehicleRepath(Entity vehicleEntity, CarCurrentLane currentLane, PathOwner pathOwner, Target target, int jobIndex)
            {
                // Create pathfinding setup item with same origin and destination
                var parameters = new PathfindParameters
                {
                    m_MaxSpeed = new float2(111.111115f, 277.77777f), // MAX_CAR_SPEED from VehicleUtils
                    m_WalkSpeed = 5.555556f,
                    m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                    m_Methods = PathMethod.Road | PathMethod.Parking,
                    m_MaxCost = 16777215f, // Large value to ensure path finding
                    m_PathfindFlags = PathfindFlags.Stable
                };

                var origin = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Road,
                    m_RoadTypes = RoadTypes.Car
                };

                var destination = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Road,
                    m_RoadTypes = RoadTypes.Car,
                    m_Entity = target.m_Target
                };

                var setupItem = new SetupQueueItem(vehicleEntity, parameters, origin, destination);
                
                // Schedule pathfinding
                PathfindQueue.Enqueue(setupItem);
                
                // Schedule cleanup of restrictions (after a delay to allow pathfinding to complete)
                CommandBuffer.AddComponent(jobIndex, vehicleEntity, new CleanupRestrictions 
                { 
                    ProcessingFrame = UnityEngine.Time.frameCount + 5 // Delay cleanup by 5 frames
                });
            }
        }
    }
}