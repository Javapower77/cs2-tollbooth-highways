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
        private BufferLookup<Game.Net.SubLane> m_SubLaneLookup;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneLookup;

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
            m_SubLaneLookup = GetBufferLookup<Game.Net.SubLane>(true);
            m_CarLaneLookup = GetComponentLookup<Game.Net.CarLane>();

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Vehicle>(),
                    ComponentType.ReadWrite<CarCurrentLane>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<RepathCreated>(),
                    ComponentType.ReadOnly<NoRepathNeeded>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            m_TollRoadQuery = GetEntityQuery(ComponentType.ReadOnly<TollRoadPrefabData>());
            m_RepathedVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<RepathCreated>()
                }
            });
            m_NoRepathVehiclesQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
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
            m_SubLaneLookup.Update(this);
            m_CarLaneLookup.Update(this);

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
            var subLaneLookup = m_SubLaneLookup;
            var carLaneLookup = m_CarLaneLookup;

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

                    int wrongTollBoothCount = 0;
                    int okTollBoothCount = 0;
                    int subLaneRoad = 0;

                    VehicleDebugLogger.Log(vehicle, "Path Elements Length: " + pathElements.Length + ", Starting at index: " + elementIndex );
                    for (int i = elementIndex; i < pathElements.Length; i++)
                    {
                        Entity laneEntity = pathElements[i].m_Target;
                        if (!ownerLookup.TryGetComponent(laneEntity, out var laneOwner) || laneOwner.m_Owner == Entity.Null)
                        {
                            continue;
                        }

                        // If the current index of the path elements contains a toll road segment, 
                        // check if it supports the vehicle type
                        if (tollRoadLookup.HasComponent(laneOwner.m_Owner))
                        {
                            VehicleDebugLogger.Log(vehicle, "Tollbooth road found in Path Element Index: " + i);

                            if (!VehiclesUtil.TollboothSupportsVehicleType(entityManager, laneOwner.m_Owner, vehicleType))
                            {
                                VehicleDebugLogger.Log(vehicle, "Tollbooth does not support the current vehicle type: " + vehicleType.ToString());
                                VehicleDebugLogger.Log(vehicle, "Type of the Tollbooth Road found: " + VehiclesUtil.GetTollboothRoadType(entityManager, laneOwner.m_Owner));
                                wrongTollBoothCount++;
                                //BlockTempTollboothRoadLane(ref laneOwner.m_Owner);

                                if (subLaneLookup.TryGetBuffer(laneEntity, out var subLanes))
                                {
                                    for (int sl = 0; sl < subLanes.Length; sl++)
                                    {
                                        if (subLanes[sl].m_PathMethods == PathMethod.Road)
                                        {
                                            subLaneRoad = sl;
                                            break;
                                        }
                                    }

                                    if (carLaneLookup.TryGetComponent(subLanes[subLaneRoad].m_SubLane, out var carLane))
                                    {
                                        if (!entityManager.HasComponent<OriginalCarLaneFlags>(subLanes[subLaneRoad].m_SubLane))
                                        {
                                            VehicleDebugLogger.Log(vehicle, "Storing original CarLane flags: " + carLane.m_Flags);
                                            ecb.AddComponent(subLanes[subLaneRoad].m_SubLane, new OriginalCarLaneFlags
                                            {
                                                Value = (uint)carLane.m_Flags
                                            });
                                        }
                                        carLane.m_Flags = Game.Net.CarLaneFlags.Unsafe;
                                        carLane.m_BlockageEnd = 255;
                                        carLane.m_BlockageStart = 0;
                                        VehicleDebugLogger.Log(vehicle, "Modified CarLane flags to: " + carLane.m_Flags + ", BlockageStart: " + carLane.m_BlockageStart + ", and BlockageEnd: " + carLane.m_BlockageEnd);
                                        VehicleDebugLogger.Log(vehicle, "Added component OriginalCarLaneFlags to lane entity: " + subLanes[subLaneRoad].m_SubLane + " for later restoration");
                                    }
                                }
                            }
                            else
                            {
                                okTollBoothCount++;
                                VehicleDebugLogger.Log(vehicle, "Tollbooth Road match with the vehicle typein Path Element Index: " + i);
                            }
                        }
                    }

                    if (okTollBoothCount == 0 && wrongTollBoothCount == 0)
                    {
                        ecb.AddComponent<NoRepathNeeded>(vehicle);
                        VehicleDebugLogger.Log(vehicle, "Tollbooth roads were not found in the path; skipping repath");
                        return;
                    }

                    VehicleDebugLogger.Log(vehicle, "Path Owner State: " + pathOwner.m_State.ToString());
                    if ((pathOwner.m_State & PathFlags.Pending) != 0)
                    {
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

                    VehicleDebugLogger.Log(vehicle, "Repath triggered; vehicle tagged, WrongTollBoothCount: " + wrongTollBoothCount + ", OkTollBoothCount: " + okTollBoothCount);
                })
                .WithoutBurst()
                .Run();

            m_PathfindSetupSystem.AddQueueWriter(Dependency);
        }
    }
}
