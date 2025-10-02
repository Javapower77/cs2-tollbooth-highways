using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using System;
using System.Reflection;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CarLaneFlags = Game.Vehicles.CarLaneFlags;
using ConnectionLaneFlags = Game.Net.ConnectionLaneFlags;
using DomainVehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Main-thread system that evaluates vehicles currently travelling through toll road segments
    /// and triggers a repath when the assigned tollbooth no longer supports the vehicle type.
    /// Vehicles are tagged with <see cref="RepathCreated"/> or <see cref="NoRepathNeeded"/>
    /// to avoid redundant checks. Tags are cleared if toll configuration changes so that
    /// entities are re-evaluated against the latest rules.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    public partial class TollboothVehicleRepathSystem : GameSystemBase
    {
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;
        private ComponentLookup<CarData> m_CarDataLookup;
        private ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneLookup;
        private ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneLookup;

        private EntityQuery m_VehicleQuery;
        private EntityQuery m_TollRoadQuery;
        private EntityQuery m_RepathedVehiclesQuery;
        private EntityQuery m_NoRepathVehiclesQuery;

        private EndFrameBarrier m_EndFrameBarrier;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private bool m_LogInitialized;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();

            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_CarDataLookup = GetComponentLookup<CarData>(true);
            m_ParkingLaneLookup = GetComponentLookup<Game.Net.ParkingLane>(true);
            m_ConnectionLaneLookup = GetComponentLookup<Game.Net.ConnectionLane>(true);

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadWrite<CarCurrentLane>(),
                    ComponentType.ReadWrite<PathOwner>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Target>(),
                    ComponentType.ReadOnly<PathElement>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<RepathCreated>(),
                    ComponentType.ReadOnly<NoRepathNeeded>()
                }
            });

            m_TollRoadQuery = GetEntityQuery(ComponentType.ReadOnly<TollRoadPrefabData>());
            m_RepathedVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<RepathCreated>()
                }
            });
            m_NoRepathVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<NoRepathNeeded>()
                }
            });
        }

        protected override void OnUpdate()
        {
            if (!m_LogInitialized)
            {
                try
                {
                    var modPathProp = typeof(Mod).GetProperty("ModPath", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    string modPath = null;
                    if (modPathProp != null)
                    {
                        var modInstances = AppDomain.CurrentDomain.GetAssemblies();
                        // simpler: static path
                        modPath = Application.persistentDataPath + "/Logs";
                    }
                    VehicleDebugLogger.Init(modPath);
                    VehicleDebugLogger.LogOnce("=== TollboothRepathSystem logging started ===");
                }
                catch { }
                m_LogInitialized = true;
            }

            m_OwnerLookup.Update(this);
            m_TollRoadLookup.Update(this);
            m_CarDataLookup.Update(this);
            m_ParkingLaneLookup.Update(this);
            m_ConnectionLaneLookup.Update(this);

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
            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var pathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64);
            var pathfindQueueWriter = pathfindQueue.AsParallelWriter();
            var carDataLookup = m_CarDataLookup;
            var parkingLaneLookup = m_ParkingLaneLookup;
            var connectionLaneLookup = m_ConnectionLaneLookup;

            Entities
                .WithName("EvaluateVehicleTollPaths")
                .WithNone<RepathCreated, NoRepathNeeded>()
                .ForEach((Entity vehicle,
                          ref CarCurrentLane currentLane,
                          ref PathOwner pathOwner,
                          in PrefabRef prefabRef,
                          in Target target,
                          in DynamicBuffer<PathElement> pathElements) =>
                {
                    int elementIndex = pathOwner.m_ElementIndex;
                    if (!pathElements.IsCreated || pathElements.Length == 0 || elementIndex >= pathElements.Length)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "No path data; skipping repath");
                        return;
                    }

                    if (target.m_Target == Entity.Null)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Vehicle has no target; skipping repath");
                        return;
                    }

                    DomainVehicleType vehicleType = VehiclesUtil.GetVehicleTypeStatic(vehicle, entityManager);
                    if (vehicleType == DomainVehicleType.None)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Unable to resolve vehicle type");
                        return;
                    }

                    bool seenActiveToll = false;
                    bool requiresRepath = false;
                    bool unsupportedBooth = false;
                    bool missingBooth = false;

                    VehicleDebugLogger.Log(vehicle, "Path Elements Length: " + pathElements.Length + ", Starting at index: " + elementIndex );
                    for (int i = elementIndex; i < pathElements.Length; i++)
                    {
                        Entity laneEntity = pathElements[i].m_Target;
                        if (!ownerLookup.TryGetComponent(laneEntity, out var laneOwner) || laneOwner.m_Owner == Entity.Null)
                        {
                            continue;
                        }

                        if (!tollRoadLookup.TryGetComponent(laneOwner.m_Owner, out var tollData) || !tollData.HasActiveTollbooth)
                        {
                            continue;
                        }

                        seenActiveToll = true;

                        if (tollData.AssociatedTollbooth == Entity.Null || !entityManager.Exists(tollData.AssociatedTollbooth))
                        {
                            missingBooth = true;
                            requiresRepath = true;
                            break;
                        }

                        if (!VehiclesUtil.TollboothSupportsVehicleType(entityManager, laneOwner.m_Owner, vehicleType))
                        {
                            unsupportedBooth = true;
                            requiresRepath = true;
                            VehicleDebugLogger.Log(vehicle, "Tollbooth does not support vehicle type: " + vehicleType + ", found in Path Element Index: " + i);
                            VehicleDebugLogger.Log(vehicle, "Tollbooth Road Type found: " + VehiclesUtil.GetTollboothRoadType(entityManager, laneOwner.m_Owner));
                            break;
                        }
                    }

                    if (!seenActiveToll)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Path contains no toll segment; marked as compliant");
                        return;
                    }

                    if (!requiresRepath)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Toll path already valid for vehicle type");
                        return;
                    }

                    VehicleDebugLogger.Log(vehicle, "Path Owner State: " + pathOwner.m_State.ToString());
                    if ((pathOwner.m_State & PathFlags.Pending) != 0)
                    {
                        //ecb.AddComponent<RepathCreated>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Repath already pending; tagging vehicle");
                        return;
                    }

                    if (!carDataLookup.HasComponent(prefabRef.m_Prefab))
                    {
                        pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Stuck);
                        pathOwner.m_State |= PathFlags.Obsolete;
                        ecb.AddComponent<RepathCreated>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "CarData missing; marked obsolete for owning AI repath");
                        return;
                    }

                    var carData = carDataLookup[prefabRef.m_Prefab];

                    Entity parkingSource = vehicle;
                    if (parkingLaneLookup.HasComponent(currentLane.m_Lane))
                    {
                        parkingSource = currentLane.m_Lane;
                    }
                    else if (connectionLaneLookup.TryGetComponent(currentLane.m_Lane, out var connectionLane) && (connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
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

                    if (!entityManager.HasComponent<OriginalCarLaneFlags>(currentLane.m_Lane))
                    {
                        ecb.AddComponent(currentLane.m_Lane, new OriginalCarLaneFlags
                        {
                            Value = (uint)currentLane.m_LaneFlags
                        });
                    }
                    VehicleDebugLogger.Log(vehicle, $"Original lane flags recorded: {(uint)currentLane.m_LaneFlags}, lane index: {currentLane.m_Lane.Index}");

                    if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Divert)) == (PathFlags.Obsolete | PathFlags.Divert))
                    {
                        pathOwner.m_State |= PathFlags.CachedObsolete;
                    }

                    pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete | PathFlags.Stuck);
                    pathOwner.m_State |= PathFlags.Pending;
                    currentLane.m_LaneFlags &= ~CarLaneFlags.EndOfPath;
                    currentLane.m_LaneFlags |= CarLaneFlags.FixedLane;
                    
                    VehicleDebugLogger.Log(vehicle, $"Modified current lane flags to: {(uint)currentLane.m_LaneFlags}");

                    pathfindQueueWriter.Enqueue(queueItem);

                    ecb.AddComponent<RepathCreated>(vehicle);

                    if (unsupportedBooth)
                    {
                        VehicleDebugLogger.Log(vehicle, "Enqueued new path due to incompatible tollbooth");
                    }
                    else if (missingBooth)
                    {
                        VehicleDebugLogger.Log(vehicle, "Enqueued new path due to missing tollbooth entity");
                    }
                    else
                    {
                        VehicleDebugLogger.Log(vehicle, "Enqueued new path for toll constraint change");
                    }
                })
                .WithoutBurst()
                .Run();

            m_PathfindSetupSystem.AddQueueWriter(Dependency);
        }
    }
}
