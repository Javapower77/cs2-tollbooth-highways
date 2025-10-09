using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using System;
using System.IO;
using System.Reflection;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CarLaneFlags = Game.Net.CarLaneFlags;
using ConnectionLaneFlags = Game.Net.ConnectionLaneFlags;
using DomainVehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Main-thread system that evaluates vehicles currently travelling through toll road segments
    /// and triggers a repath when the assigned tollbooth no longer supports the vehicle type.
    /// Temporarily blocks incompatible toll lanes by setting appropriate flags.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class TollboothVehicleRepathSystem : GameSystemBase
    {
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;
        private ComponentLookup<TollBoothPrefabData> m_TollBoothLookup;
        private ComponentLookup<CarData> m_CarDataLookup;
        private ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneLookup;
        private ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneLookup;
        private BufferLookup<Game.Net.SubLane> m_SubLaneLookup;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneLookup;
        private ComponentLookup<PathOwner> m_PathOwnerLookup;
        private ComponentLookup<Deleted> m_DeletedLookup;
        private BufferLookup<PathElement> m_PathElementLookup;

        private EntityQuery m_VehicleQuery;
        private EntityQuery m_TollRoadQuery;
        private EntityQuery m_TollBoothQuery;
        private EntityQuery m_BlockedLanesQuery;
        private EntityQuery m_RepathedVehiclesQuery;
        private EntityQuery m_NoRepathVehiclesQuery;

        private EndFrameBarrier m_EndFrameBarrier;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private SimulationSystem m_SimulationSystem;
        private bool m_LogInitialized;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_TollBoothLookup = GetComponentLookup<TollBoothPrefabData>(true);
            m_CarDataLookup = GetComponentLookup<CarData>(true);
            m_ParkingLaneLookup = GetComponentLookup<Game.Net.ParkingLane>(true);
            m_ConnectionLaneLookup = GetComponentLookup<Game.Net.ConnectionLane>(true);
            m_SubLaneLookup = GetBufferLookup<Game.Net.SubLane>(true);
            m_CarLaneLookup = GetComponentLookup<Game.Net.CarLane>();
            m_PathOwnerLookup = GetComponentLookup<PathOwner>(true);
            m_DeletedLookup = GetComponentLookup<Deleted>(true);
            m_PathElementLookup = GetBufferLookup<PathElement>(true);

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Vehicle>(),
                    ComponentType.ReadWrite<CarCurrentLane>(),
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Target>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            m_TollRoadQuery = GetEntityQuery(ComponentType.ReadOnly<TollRoadPrefabData>());
            m_TollBoothQuery = GetEntityQuery(ComponentType.ReadOnly<TollBoothPrefabData>());
            m_BlockedLanesQuery = GetEntityQuery(
                ComponentType.ReadOnly<OriginalCarLaneFlags>(),
                ComponentType.ReadOnly<LaneBlockedByVehicle>());
            m_RepathedVehiclesQuery = GetEntityQuery(ComponentType.ReadOnly<RepathCreated>());
            m_NoRepathVehiclesQuery = GetEntityQuery(ComponentType.ReadOnly<NoRepathNeeded>());
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
                    VehicleDebugLogger.LogOnce("=== TollboothRepathSystem logging started ===");
                }
                catch
                {
                    // ignore logging initialization failures
                }

                m_LogInitialized = true;
            }

            m_OwnerLookup.Update(this);
            m_TollRoadLookup.Update(this);
            m_TollBoothLookup.Update(this);
            m_CarDataLookup.Update(this);
            m_ParkingLaneLookup.Update(this);
            m_ConnectionLaneLookup.Update(this);
            m_SubLaneLookup.Update(this);
            m_CarLaneLookup.Update(this);
            m_PathOwnerLookup.Update(this);
            m_DeletedLookup.Update(this);
            m_PathElementLookup.Update(this);

            uint currentFrame = m_SimulationSystem.frameIndex;

            bool tollNetworkChanged = false;
            if (!m_TollRoadQuery.IsEmptyIgnoreFilter)
            {
                Entities
                    .WithName("DetectTollNetworkChanges")
                    .WithChangeFilter<TollRoadPrefabData>()
                    .ForEach((in TollRoadPrefabData data) =>
                    {
                        tollNetworkChanged = true;
                    })
                    .WithoutBurst()
                    .Run();
            }

            if (tollNetworkChanged)
            {
                if (!m_RepathedVehiclesQuery.IsEmptyIgnoreFilter)
                {
                    EntityManager.RemoveComponent<RepathCreated>(m_RepathedVehiclesQuery);
                }

                if (!m_NoRepathVehiclesQuery.IsEmptyIgnoreFilter)
                {
                    EntityManager.RemoveComponent<NoRepathNeeded>(m_NoRepathVehiclesQuery);
                }

                VehicleDebugLogger.LogOnce("Toll network changed; cleared repath markers");
            }

            if (m_VehicleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entityManager = EntityManager;
            var ownerLookup = m_OwnerLookup;
            var tollRoadLookup = m_TollRoadLookup;
            var tollBoothLookup = m_TollBoothLookup;
            var carLaneLookup = m_CarLaneLookup;
            var pathElementLookup = m_PathElementLookup;
            var carDataLookup = m_CarDataLookup;
            var parkingLaneLookup = m_ParkingLaneLookup;
            var connectionLaneLookup = m_ConnectionLaneLookup;
            var subLaneLookup = m_SubLaneLookup;

            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var pathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64);

            var tollBoothEntities = m_TollBoothQuery.ToEntityArray(Allocator.Temp);
            NativeParallelMultiHashMap<Entity, Entity> tollBoothsByRoad = default;

            try
            {
                if (tollBoothEntities.Length > 0)
                {
                    tollBoothsByRoad = new NativeParallelMultiHashMap<Entity, Entity>(tollBoothEntities.Length, Allocator.Temp);
                    for (int i = 0; i < tollBoothEntities.Length; i++)
                    {
                        Entity booth = tollBoothEntities[i];
                        if (!tollBoothLookup.TryGetComponent(booth, out var boothData))
                        {
                            continue;
                        }

                        if (boothData.BelongsToHighwayTollbooth == Entity.Null)
                        {
                            continue;
                        }

                        tollBoothsByRoad.Add(boothData.BelongsToHighwayTollbooth, booth);
                    }
                }

                Entities
                    .WithName("EvaluateVehicleTollPaths")
                    .WithNone<RepathCreated, NoRepathNeeded>()
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

                        if (!pathElementLookup.HasBuffer(vehicle))
                        {
                            VehicleDebugLogger.Log(vehicle, "No path buffer available - skipping current frame");
                            return;
                        }

                        var pathElements = pathElementLookup[vehicle];
                        if (!pathElements.IsCreated || pathElements.Length == 0 || target.m_Target == Entity.Null)
                        {
                            VehicleDebugLogger.Log(vehicle, "No path elements created yet or invalid target - skipping current frame");
                            return;
                        }

                        if (!carDataLookup.HasComponent(prefabRef.m_Prefab))
                        {
                            VehicleDebugLogger.Log(vehicle, "No CarData found for vehicle prefab - skipping current frame");
                            return;
                        }

                        DomainVehicleType vehicleType = VehiclesUtil.GetVehicleTypeStatic(vehicle, entityManager);
                        if (vehicleType == DomainVehicleType.None)
                        {
                            return;
                        }

                        int wrongTollBoothCount = 0;
                        int okTollBoothCount = 0;
                        int forcedTollBoothCount = 0;

                        int elementIndex = math.clamp(pathOwner.m_ElementIndex, 0, math.max(0, pathElements.Length - 1));

                        for (int i = elementIndex; i < pathElements.Length; i++)
                        {
                            Entity laneEntity = pathElements[i].m_Target;
                            if (!ownerLookup.TryGetComponent(laneEntity, out var laneOwner) || laneOwner.m_Owner == Entity.Null)
                            {
                                continue;
                            }

                            if (!tollRoadLookup.HasComponent(laneOwner.m_Owner))
                            {
                                continue;
                            }

                            VehicleDebugLogger.Log(vehicle, $"Tollbooth road found in Path Element Index: {i}");

                            if (VehiclesUtil.TollboothSupportsVehicleType(entityManager, laneOwner.m_Owner, vehicleType))
                            {
                                okTollBoothCount++;
                                VehicleDebugLogger.Log(vehicle, $"Tollbooth road of type [{VehiclesUtil.GetTollRoadType(vehicleType)}] matches vehicle type [{vehicleType}] at path element {i}");
                                continue;
                            }

                            if (TrySwapToCompatibleTollLane(laneOwner.m_Owner, vehicleType, vehicle, pathElements, i, tollBoothsByRoad,
                                    tollRoadLookup, tollBoothLookup, ownerLookup, carLaneLookup, entityManager))
                            {
                                forcedTollBoothCount++;
                                VehicleDebugLogger.Log(vehicle, $"Forced toll lane swap succeeded at element {i}; continuing without repath.");
                                continue;
                            }

                            wrongTollBoothCount++;
                            VehicleDebugLogger.Log(vehicle, $"Incompatible tollbooth at path element {i} - blocking lanes");
                            VehicleDebugLogger.Log(vehicle, $"Tollbooth {laneOwner.m_Owner.Index} does not support vehicle type {vehicleType}");
                            VehicleDebugLogger.Log(vehicle, $"Tollbooth Road Found: {VehiclesUtil.GetTollboothRoadType(entityManager, laneOwner.m_Owner)}, Tollbooth Road Expected: {VehiclesUtil.GetTollRoadType(vehicleType)}");

                            TollboothLaneUtility.BlockTollRoadLanes(laneOwner.m_Owner, vehicleType, ecb, subLaneLookup,
                                carLaneLookup, connectionLaneLookup, entityManager, vehicle, currentFrame);
                        }

                        if (wrongTollBoothCount == 0 && okTollBoothCount == 0)
                        {
                            ecb.AddComponent<NoRepathNeeded>(vehicle);
                            VehicleDebugLogger.Log(vehicle, "No tollbooths found on the paths of the vehicle - no action needed");
                            return;
                        }

                        if (wrongTollBoothCount == 0)
                        {
                            ecb.AddComponent<NoRepathNeeded>(vehicle);
                            VehicleDebugLogger.Log(vehicle, forcedTollBoothCount > 0
                                ? $"All tollbooths on the path are compatible after forcing {forcedTollBoothCount} lane adjustments - no repath needed"
                                : "All tollbooths on the path are compatible - no repath needed");
                            return;
                        }

                        var carData = carDataLookup[prefabRef.m_Prefab];

                        Entity parkingSource = vehicle;
                        if (parkingLaneLookup.HasComponent(currentLane.m_Lane))
                        {
                            parkingSource = currentLane.m_Lane;
                        }
                        else if (connectionLaneLookup.TryGetComponent(currentLane.m_Lane, out var connectionLane)
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
                        VehicleDebugLogger.Log(vehicle, $"Path Owner State updated to {pathOwner.m_State}");
                        VehicleDebugLogger.Log(vehicle, $"**REPATH TRIGGERED** Frame:{currentFrame} -- Incompatible:{wrongTollBoothCount}, Compatible:{okTollBoothCount}");
                    })
                    .WithoutBurst()
                    .Run();

                RestoreBlockedLanes(currentFrame);
                m_PathfindSetupSystem.AddQueueWriter(Dependency);
            }
            finally
            {
                if (tollBoothsByRoad.IsCreated)
                {
                    tollBoothsByRoad.Dispose();
                }

                tollBoothEntities.Dispose();
            }
        }

        private bool TrySwapToCompatibleTollLane(Entity roadEntity,
                                                 DomainVehicleType vehicleType,
                                                 Entity vehicle,
                                                 DynamicBuffer<PathElement> pathElements,
                                                 int pathIndex,
                                                 NativeParallelMultiHashMap<Entity, Entity> tollBoothsByRoad,
                                                 ComponentLookup<TollRoadPrefabData> tollRoadLookup,
                                                 ComponentLookup<TollBoothPrefabData> tollBoothLookup,
                                                 ComponentLookup<Owner> ownerLookup,
                                                 ComponentLookup<Game.Net.CarLane> carLaneLookup,
                                                 EntityManager entityManager)
        {
            if (roadEntity == Entity.Null)
            {
                return false;
            }

            Entity targetBooth = Entity.Null;

            if (tollRoadLookup.HasComponent(roadEntity))
            {
                var tollRoadData = tollRoadLookup[roadEntity];
                if (tollRoadData.HasActiveTollbooth &&
                    tollRoadData.AssociatedTollbooth != Entity.Null &&
                    entityManager.Exists(tollRoadData.AssociatedTollbooth) &&
                    VehiclesUtil.TollboothSupportsVehicleType(entityManager, tollRoadData.AssociatedTollbooth, vehicleType))
                {
                    targetBooth = tollRoadData.AssociatedTollbooth;
                }
            }

            if (targetBooth == Entity.Null && tollBoothsByRoad.IsCreated)
            {
                if (tollBoothsByRoad.TryGetFirstValue(roadEntity, out var booth, out var iterator))
                {
                    do
                    {
                        if (!entityManager.Exists(booth))
                        {
                            continue;
                        }

                        if (VehiclesUtil.TollboothSupportsVehicleType(entityManager, booth, vehicleType))
                        {
                            targetBooth = booth;
                            break;
                        }
                    }
                    while (tollBoothsByRoad.TryGetNextValue(out booth, ref iterator));
                }
            }

            if (targetBooth == Entity.Null)
            {
                return false;
            }

            if (!tollBoothLookup.TryGetComponent(targetBooth, out var boothData))
            {
                return false;
            }

            Entity newLane = boothData.ControlledLane;
            if (newLane == Entity.Null || !entityManager.Exists(newLane))
            {
                return false;
            }

            if (!carLaneLookup.HasComponent(newLane))
            {
                return false;
            }

            if (!ownerLookup.TryGetComponent(newLane, out var newLaneOwner) || newLaneOwner.m_Owner != roadEntity)
            {
                return false;
            }

            var element = pathElements[pathIndex];
            if (element.m_Target == newLane)
            {
                return false;
            }

            element.m_Target = newLane;
            pathElements[pathIndex] = element;

            VehicleDebugLogger.Log(vehicle, $"Reassigned path element {pathIndex} to toll lane {newLane.Index} (booth {targetBooth.Index}).");
            return true;
        }

        private void RestoreBlockedLanes(uint currentFrame)
        {
            if (m_BlockedLanesQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var carLaneLookup = m_CarLaneLookup;
            var pathOwnerLookup = m_PathOwnerLookup;
            var deletedLookup = m_DeletedLookup;
            var connectionLaneLookup = m_ConnectionLaneLookup;
            var ownerLookup = m_OwnerLookup;
            var pathElementLookup = m_PathElementLookup;
            var entityManager = EntityManager;

            const uint MAX_BLOCK_FRAMES = 300;

            Entities
                .WithName("RestoreBlockedLanes")
                .WithAll<OriginalCarLaneFlags, LaneBlockedByVehicle>()
                .ForEach((Entity laneEntity, in OriginalCarLaneFlags originalFlags, in LaneBlockedByVehicle blockedBy) =>
                {
                    bool shouldRestore = false;
                    bool keepBlockedForValidation = false;
                    string reason = "";

                    if (deletedLookup.HasComponent(blockedBy.Vehicle))
                    {
                        shouldRestore = true;
                        reason = "vehicle deleted";
                    }
                    else if (pathOwnerLookup.TryGetComponent(blockedBy.Vehicle, out var pathOwner))
                    {
                        if ((pathOwner.m_State & PathFlags.Pending) == 0)
                        {
                            if (blockedBy.TollRoad != Entity.Null && pathElementLookup.TryGetBuffer(blockedBy.Vehicle, out var pathElements))
                            {
                                for (int i = pathOwner.m_ElementIndex; i < pathElements.Length; i++)
                                {
                                    Entity pathLane = pathElements[i].m_Target;
                                    if (pathLane == Entity.Null)
                                    {
                                        continue;
                                    }

                                    if (!ownerLookup.TryGetComponent(pathLane, out var laneOwner) || laneOwner.m_Owner == Entity.Null)
                                    {
                                        continue;
                                    }

                                    if (laneOwner.m_Owner == blockedBy.TollRoad &&
                                        !VehiclesUtil.TollboothSupportsVehicleType(entityManager, laneOwner.m_Owner, blockedBy.VehicleType))
                                    {
                                        keepBlockedForValidation = true;
                                        break;
                                    }
                                }
                            }

                            if (!keepBlockedForValidation)
                            {
                                shouldRestore = true;
                                reason = "pathfinding completed";
                            }
                        }
                    }
                    else
                    {
                        shouldRestore = true;
                        reason = "vehicle entity missing";
                    }

                    if (!shouldRestore && !keepBlockedForValidation && (currentFrame - blockedBy.FrameBlocked) > MAX_BLOCK_FRAMES)
                    {
                        shouldRestore = true;
                        reason = $"timeout ({currentFrame - blockedBy.FrameBlocked} frames)";
                    }

                    if (keepBlockedForValidation)
                    {
                        var refreshed = blockedBy;
                        refreshed.FrameBlocked = currentFrame;
                        refreshed.AttemptCount = (ushort)math.min(ushort.MaxValue, blockedBy.AttemptCount + 1);
                        ecb.SetComponent(laneEntity, refreshed);

                        int tollIndex = blockedBy.TollRoad == Entity.Null ? -1 : blockedBy.TollRoad.Index;
                        VehicleDebugLogger.Log(blockedBy.Vehicle, $"Keeping lane {laneEntity.Index} blocked; forbidden toll {tollIndex} still on path (attempt {refreshed.AttemptCount}).");
                        return;
                    }

                    if (shouldRestore && carLaneLookup.TryGetComponent(laneEntity, out var carLane))
                    {
                        carLane.m_Flags = (CarLaneFlags)originalFlags.Value;
                        carLane.m_BlockageStart = 255;
                        carLane.m_BlockageEnd = 0;

                        ecb.SetComponent(laneEntity, carLane);
                        ecb.RemoveComponent<OriginalCarLaneFlags>(laneEntity);
                        ecb.RemoveComponent<LaneBlockedByVehicle>(laneEntity);

                        VehicleDebugLogger.Log(blockedBy.Vehicle, $"Restored lane {laneEntity.Index} - {reason}");

                        if (entityManager.HasComponent<OriginalConnectionLaneFlags>(laneEntity) &&
                            connectionLaneLookup.TryGetComponent(laneEntity, out var connectionLane))
                        {
                            var originalConnectionFlags = entityManager.GetComponentData<OriginalConnectionLaneFlags>(laneEntity);
                            connectionLane.m_Flags = (ConnectionLaneFlags)originalConnectionFlags.Value;
                            ecb.SetComponent(laneEntity, connectionLane);
                            ecb.RemoveComponent<OriginalConnectionLaneFlags>(laneEntity);
                        }

                        TollboothLaneUtility.RequestPathfindRebuild(laneEntity, blockedBy.TollRoad, ecb, entityManager);
                    }
                })
                .WithoutBurst()
                .Run();
        }
    }
}