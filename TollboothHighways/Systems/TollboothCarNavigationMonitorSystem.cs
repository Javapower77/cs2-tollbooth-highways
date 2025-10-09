using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using System.Reflection;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ConnectionLaneFlags = Game.Net.ConnectionLaneFlags;
using DomainVehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors the CarNavigationLane buffer of active vehicles and ensures they do not enter toll lanes
    /// incompatible with their vehicle type. If an incompatible toll segment is detected the lane is blocked
    /// and a repath is forced for the vehicle.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class TollboothCarNavigationMonitorSystem : GameSystemBase
    {
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;
        private ComponentLookup<CarData> m_CarDataLookup;
        private ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneLookup;
        private ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneLookup;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneLookup;
        private ComponentLookup<Deleted> m_DeletedLookup;
        private BufferLookup<Game.Net.SubLane> m_SubLaneLookup;
        private BufferLookup<CarNavigationLane> m_CarNavigationLaneLookup;

        private EndFrameBarrier m_EndFrameBarrier;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private SimulationSystem m_SimulationSystem;

        private EntityQuery m_VehicleNavigationQuery;
        private bool m_LogInitialized;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_CarDataLookup = GetComponentLookup<CarData>(true);
            m_ParkingLaneLookup = GetComponentLookup<Game.Net.ParkingLane>(true);
            m_ConnectionLaneLookup = GetComponentLookup<Game.Net.ConnectionLane>(true);
            m_CarLaneLookup = GetComponentLookup<Game.Net.CarLane>();
            m_DeletedLookup = GetComponentLookup<Deleted>(true);
            m_SubLaneLookup = GetBufferLookup<Game.Net.SubLane>(true);
            m_CarNavigationLaneLookup = GetBufferLookup<CarNavigationLane>();

            m_VehicleNavigationQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Vehicle>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Target>(),
                    ComponentType.ReadWrite<CarCurrentLane>(),
                    ComponentType.ReadOnly<PathOwner>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_LogInitialized)
            {
                try
                {
                    string modPath = null;
                    var modPathProp = typeof(Mod).GetProperty("ModPath", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    if (modPathProp != null)
                    {
                        modPath = Application.persistentDataPath + "/Logs";
                    }

                    VehicleDebugLogger.Init(modPath);
                    VehicleDebugLogger.LogOnce("=== TollboothCarNavigationMonitorSystem logging started ===");
                }
                catch
                {
                    // ignore logging initialization failures
                }

                m_LogInitialized = true;
            }

            m_OwnerLookup.Update(this);
            m_TollRoadLookup.Update(this);
            m_CarDataLookup.Update(this);
            m_ParkingLaneLookup.Update(this);
            m_ConnectionLaneLookup.Update(this);
            m_CarLaneLookup.Update(this);
            m_DeletedLookup.Update(this);
            m_SubLaneLookup.Update(this);
            m_CarNavigationLaneLookup.Update(this);

            if (m_VehicleNavigationQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            uint currentFrame = m_SimulationSystem.frameIndex;
            var entityManager = EntityManager;
            var ownerLookup = m_OwnerLookup;
            var tollRoadLookup = m_TollRoadLookup;
            var subLaneLookup = m_SubLaneLookup;
            var carLaneLookup = m_CarLaneLookup;
            var connectionLaneLookup = m_ConnectionLaneLookup;
            var carNavigationLaneLookup = m_CarNavigationLaneLookup;
            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var pathfindQueue = m_PathfindSetupSystem.GetQueue(this, 32);

            Entities
                .WithName("MonitorCarNavigationLanes")
                .WithNone<Deleted, Unspawned>()
                .ForEach((Entity vehicle,
                          ref CarCurrentLane currentLane,
                          ref PathOwner pathOwner,
                          in PrefabRef prefabRef,
                          in Target target) =>
                {
                    VehicleDebugLogger.Log(vehicle, $"Current frame: {currentFrame}");
                    VehicleDebugLogger.Log(vehicle, $"Evaluating vehicle at pathOwner element index {pathOwner.m_ElementIndex}, state: {pathOwner.m_State}");

                    if ((pathOwner.m_State & PathFlags.Pending) != 0)
                    {
                        VehicleDebugLogger.Log(vehicle, "Repath already pending, waiting for completion");
                        return;
                    }

                    if (target.m_Target == Entity.Null)
                    {
                        return;
                    }

                    if (!carNavigationLaneLookup.HasBuffer(vehicle))
                    {
                        return;
                    }

                    var navigationLanes = carNavigationLaneLookup[vehicle];
                    if (!navigationLanes.IsCreated || navigationLanes.Length == 0)
                    {
                        VehicleDebugLogger.Log(vehicle, "CarNavigationLane buffer is still empty, cannot monitor yet.");
                        return;
                    }

                    if (!m_CarDataLookup.HasComponent(prefabRef.m_Prefab))
                    {
                        return;
                    }

                    DomainVehicleType vehicleType = VehiclesUtil.GetVehicleTypeStatic(vehicle, entityManager);
                    if (vehicleType == DomainVehicleType.None)
                    {
                        return;
                    }

                    for (int i = 0; i < navigationLanes.Length; i++)
                    {
                        CarNavigationLane navLane = navigationLanes[i];
                        Entity laneEntity = navLane.m_Lane;

                        if (laneEntity == Entity.Null)
                        {
                            continue;
                        }

                        if (!ownerLookup.TryGetComponent(laneEntity, out var laneOwner) || laneOwner.m_Owner == Entity.Null)
                        {
                            continue;
                        }

                        if (!tollRoadLookup.HasComponent(laneOwner.m_Owner))
                        {
                            continue;
                        }

                        if (VehiclesUtil.TollboothSupportsVehicleType(entityManager, laneOwner.m_Owner, vehicleType))
                        {
                            VehicleDebugLogger.Log(vehicle, $"Tollbooth road of type [{VehiclesUtil.GetTollRoadType(vehicleType)}] matches vehicle type [{vehicleType}] at CarNavigationLane Buffer Index: {i}");
                            continue;
                        }
                        VehicleDebugLogger.Log(vehicle, $"Tollbooth Road Found at current CarNavigationLane Buffer Index [{i}]: {VehiclesUtil.GetTollboothRoadType(entityManager, laneOwner.m_Owner)}, Tollbooth Road Expected: {VehiclesUtil.GetTollRoadType(vehicleType)} ");
                        VehicleDebugLogger.Log(vehicle, $"CarNavigationLane [{i}] {laneEntity.Index} belongs to incompatible toll road {laneOwner.m_Owner.Index} for vehicle type {vehicleType}");
                        VehicleDebugLogger.Log(vehicle, $"Current CarNavigationLane Flags: {navLane.m_Flags}");
                        //TollboothLaneUtility.BlockTollRoadLanes(laneOwner.m_Owner, vehicleType, ecb, subLaneLookup, carLaneLookup, connectionLaneLookup, entityManager, vehicle, currentFrame);
                        //TollboothLaneUtility.RequestPathfindRebuild(laneEntity, laneOwner.m_Owner, ecb, entityManager);

                        navLane.m_Flags = Game.Vehicles.CarLaneFlags.IsBlocked;
                        VehicleDebugLogger.Log(vehicle, $"Changed CarNavigationLane Flags to IsBlocked. New Flags: {navLane.m_Flags}");


                        ForceVehicleRepath(vehicle, vehicleType, laneOwner.m_Owner, laneEntity, ref currentLane, ref pathOwner, prefabRef, target, ecb, pathfindQueue, currentFrame);
                        break;
                    }
                })
                .WithoutBurst()
                .Run();

            m_PathfindSetupSystem.AddQueueWriter(Dependency);
        }

        private void ForceVehicleRepath(Entity vehicle,
                                         DomainVehicleType vehicleType,
                                         Entity tollRoad,
                                         Entity laneEntity,
                                         ref CarCurrentLane currentLane,
                                         ref PathOwner pathOwner,
                                         PrefabRef prefabRef,
                                         Target target,
                                         EntityCommandBuffer ecb,
                                         NativeQueue<SetupQueueItem> pathfindQueue,
                                         uint currentFrame)
        {
            var entityManager = EntityManager;

            if (!m_CarDataLookup.HasComponent(prefabRef.m_Prefab))
            {
                return;
            }

            var carData = m_CarDataLookup[prefabRef.m_Prefab];

            Entity parkingSource = vehicle;
            if (m_ParkingLaneLookup.HasComponent(currentLane.m_Lane))
            {
                parkingSource = currentLane.m_Lane;
            }
            else if (m_ConnectionLaneLookup.TryGetComponent(currentLane.m_Lane, out var connectionLane)
                     && (connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
            {
                parkingSource = currentLane.m_Lane;
            }

            PathfindParameters parameters = new PathfindParameters
            {
                m_MaxSpeed = new float2(carData.m_MaxSpeed, VehicleUtils.MAX_VEHICLE_SPEED),
                m_WalkSpeed = 5.555556f,
                m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                m_Methods = VehicleUtils.GetPathMethods(carData),
                m_ParkingTarget = parkingSource,
                m_ParkingDelta = currentLane.m_CurvePosition.z,
                m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData)
            };

            SetupQueueTarget origin = new SetupQueueTarget
            {
                m_Type = SetupTargetType.CurrentLocation,
                m_Methods = VehicleUtils.GetPathMethods(carData) | PathMethod.Parking,
                m_RoadTypes = RoadTypes.Car
            };

            SetupQueueTarget destination = new SetupQueueTarget
            {
                m_Type = SetupTargetType.CurrentLocation,
                m_Methods = VehicleUtils.GetPathMethods(carData),
                m_RoadTypes = RoadTypes.Car,
                m_Entity = target.m_Target
            };

            var queueItem = new SetupQueueItem(vehicle, parameters, origin, destination);

            if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Divert)) == (PathFlags.Obsolete | PathFlags.Divert))
            {
                pathOwner.m_State |= PathFlags.CachedObsolete;
            }

            pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete | PathFlags.Stuck);
            pathOwner.m_State |= PathFlags.Pending;

            currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.EndOfPath;
            currentLane.m_LaneFlags |= Game.Vehicles.CarLaneFlags.FixedLane;
            pathfindQueue.Enqueue(queueItem);

            VehicleDebugLogger.Log(vehicle, $"## RE-PATH ## Forced repath due to incompatible toll lane {laneEntity.Index} on toll road {tollRoad.Index}");
        }
    }
}
