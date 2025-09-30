using Game.Pathfind;
using Game.Vehicles;
using Game.Net;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using System.Reflection;
using System;
using Game.Objects;
using Game.Common;
using Game.Simulation;
using Unity.Mathematics;
using Game;
using System.IO;
using UnityEngine;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Scans moving vehicles; if their current path contains toll road segments that do NOT
    /// allow their vehicle type, temporarily restrict those lanes (Only ForbidPassing flag +
    /// inflated lane crossing cost) and request a new pathfind.
    /// Runs on main thread (no burst) for easier debugging.
    /// </summary>    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PathfindSetupSystem))]
    public partial class TollboothRepathSystem : GameSystemBase
    {
        private EntityQuery _vehicleQuery;
        private ComponentLookup<CarCurrentLane> _carCurrentLaneLookup;
        private ComponentLookup<PathOwner> _pathOwnerLookup;
        private BufferLookup<PathElement> _pathBufferLookup;
        private ComponentLookup<Game.Net.CarLane> _carLaneLookup;
        private ComponentLookup<TollRoadPrefabData> _tollRoadLookup;
        private ComponentLookup<TollBoothPrefabData> _tollBoothLookup;
        private ComponentLookup<PrefabRef> _prefabRefLookup;
        private ComponentLookup<Owner> _ownerLookup;

        private ComponentLookup<CarData> _carDataLookup;
        private ComponentLookup<Target> _targetLookup;
        private ComponentLookup<Game.Net.ParkingLane> _parkingLaneLookup;
        private ComponentLookup<Game.Net.ConnectionLane> _connectionLaneLookup;
        private ComponentLookup<ObjectGeometryData> _objectGeomLookup;

        private PathfindSetupSystem _pathfindSetupSystem;
        private SimulationSystem _simulationSystem;

        private NativeQueue<SetupQueueItem> _activeSetupQueue;
        private uint _queueFrame;

        private bool _logInit;

        protected override void OnCreate()
        {
            base.OnCreate();
            _vehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>(),
                    ComponentType.ReadWrite<CarCurrentLane>(),
                    ComponentType.ReadWrite<PathOwner>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            _carCurrentLaneLookup = GetComponentLookup<CarCurrentLane>(false);
            _pathOwnerLookup = GetComponentLookup<PathOwner>(false);
            _pathBufferLookup = GetBufferLookup<PathElement>(true);
            _carLaneLookup = GetComponentLookup<Game.Net.CarLane>(false);
            _tollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            _tollBoothLookup = GetComponentLookup<TollBoothPrefabData>(true);
            _prefabRefLookup = GetComponentLookup<PrefabRef>(true);
            _ownerLookup = GetComponentLookup<Owner>(true);

            _carDataLookup = GetComponentLookup<CarData>(true);
            _targetLookup = GetComponentLookup<Target>(true);
            _parkingLaneLookup = GetComponentLookup<Game.Net.ParkingLane>(true);
            _connectionLaneLookup = GetComponentLookup<Game.Net.ConnectionLane>(true);
            _objectGeomLookup = GetComponentLookup<ObjectGeometryData>(true);

            _pathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            _simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            _queueFrame = uint.MaxValue;

            RequireForUpdate(_vehicleQuery);
        }

        protected override void OnUpdate()
        {
            // Initialize vehicle debug logging root once
            if (!_logInit)
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
                _logInit = true;
            }

            if (_simulationSystem.frameIndex % 300 == 0)
                VehicleDebugLogger.LogOnce($"Tick frame={_simulationSystem.frameIndex}");

            EnsureSetupQueueForThisFrame();

            _carCurrentLaneLookup.Update(this);
            _pathOwnerLookup.Update(this);
            _pathBufferLookup.Update(this);
            _carLaneLookup.Update(this);
            _tollRoadLookup.Update(this);
            _tollBoothLookup.Update(this);
            _prefabRefLookup.Update(this);
            _carDataLookup.Update(this);
            _targetLookup.Update(this);
            _parkingLaneLookup.Update(this);
            _connectionLaneLookup.Update(this);
            _objectGeomLookup.Update(this);

            var em = EntityManager;
            using var vehicles = _vehicleQuery.ToEntityArray(Allocator.Temp);

            foreach (var vehicle in vehicles)
            {
                try
                {
                    VehicleDebugLogger.Log(vehicle, $"START vehicle frame={_simulationSystem.frameIndex}");

                    if (!_carCurrentLaneLookup.HasComponent(vehicle) || !_pathOwnerLookup.HasComponent(vehicle))
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP missing CarCurrentLane or PathOwner");
                        continue;
                    }

                    var pathOwner = _pathOwnerLookup[vehicle];
                    VehicleDebugLogger.Log(vehicle, $"PathOwner state=0x{((uint)pathOwner.m_State):X}");

                    if (!_pathBufferLookup.HasBuffer(vehicle))
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP no PathElement buffer yet");
                        continue;
                    }

                    var path = _pathBufferLookup[vehicle];
                    VehicleDebugLogger.Log(vehicle, $"Path length={path.Length}");

                    if (path.Length == 0)
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP empty path");
                        continue;
                    }

                    if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Scheduled)) != 0)
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP path pending/scheduled");
                        continue;
                    }

                    if ((pathOwner.m_State & PathFlags.Failed) != 0)
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP path failed (vanilla recovery expected)");
                        continue;
                    }

                    bool tollFound = false;
                    bool incompatible = false;

                    for (int i = 0; i < path.Length; i++)
                    {
                        var laneEntity = path[i].m_Target;
                        if (!_ownerLookup.HasComponent(laneEntity))
                            continue;

                        var roadEntity = _ownerLookup[laneEntity];
                        if (_tollRoadLookup.HasComponent(roadEntity.m_Owner))
                        {
                            tollFound = true;
                            var vehicleType = VehiclesUtil.GetVehicleTypeStatic(vehicle, em);
                            bool supported = VehiclesUtil.TollboothSupportsVehicleType(em, roadEntity.m_Owner, vehicleType);                            
                            VehicleDebugLogger.Log(vehicle, $"TOLL lane idx={i} lane={laneEntity.Index} road={roadEntity.m_Owner.Index} supported={supported}");
                            VehicleDebugLogger.Log(vehicle, $"Vehicle Type={vehicleType} - Tollbooth Road Type Found={VehiclesUtil.GetTollboothRoadType(em, roadEntity.m_Owner)}");
                            if (!supported)
                            {
                                incompatible = true;
                                if (_carLaneLookup.HasComponent(laneEntity) && !em.HasComponent<OriginalCarLaneFlags>(laneEntity))
                                {
                                    var laneData = _carLaneLookup[laneEntity];
                                    em.AddComponentData(laneEntity, new OriginalCarLaneFlags { Value = (uint)laneData.m_Flags });
                                    laneData.m_BlockageStart = 0;
                                    laneData.m_BlockageEnd = 255;
                                    laneData.m_Flags = Game.Net.CarLaneFlags.IsSecured | Game.Net.CarLaneFlags.ForbidCombustionEngines | Game.Net.CarLaneFlags.ForbidTransitTraffic | Game.Net.CarLaneFlags.ForbidHeavyTraffic | Game.Net.CarLaneFlags.AllowEnter;
                                    laneData.m_LaneCrossCount = 255;
                                    _carLaneLookup[laneEntity] = laneData;
                                    VehicleDebugLogger.Log(vehicle, $"MODIFIED lane flags, blockage and set laneCross=255");
                                }
                            }
                        }
                    }

                    VehicleDebugLogger.Log(vehicle, $"Scan result tollFound={tollFound} incompatible={incompatible}");

                    if (!tollFound || !incompatible)
                    {
                        VehicleDebugLogger.Log(vehicle, "NO REPTH needed this frame");
                        continue;
                    }

                    /*
                    if (em.HasComponent<RepathCreated>(vehicle))
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP already repathed (RepathCreated present)");
                        continue;
                    }
                    */

                    var currentLane = _carCurrentLaneLookup[vehicle];

                    if (!_prefabRefLookup.HasComponent(vehicle) ||
                        !_carDataLookup.HasComponent(_prefabRefLookup[vehicle].m_Prefab))
                    {
                        VehicleDebugLogger.Log(vehicle, "SKIP missing PrefabRef or CarData");
                        continue;
                    }

                    var prefabRef = _prefabRefLookup[vehicle];
                    CarData carData = _carDataLookup[prefabRef.m_Prefab];
                    VehicleDebugLogger.Log(vehicle, $"CarData maxSpeed={carData.m_MaxSpeed}");

                    Entity destEntity = Entity.Null;
                    if (_targetLookup.HasComponent(vehicle))
                    {
                        destEntity = _targetLookup[vehicle].m_Target;
                        VehicleDebugLogger.Log(vehicle, $"Destination target={destEntity.Index}");
                    }
                    else
                    {
                        VehicleDebugLogger.Log(vehicle, "Destination target MISSING");
                    }

                    Entity parkingSource = VehicleUtils.GetParkingSource(
                        vehicle,
                        currentLane,
                        ref _parkingLaneLookup,
                        ref _connectionLaneLookup);

                    float2 parkingSize = VehicleUtils.GetParkingSize(
                        vehicle,
                        ref _prefabRefLookup,
                        ref _objectGeomLookup);

                    VehicleDebugLogger.Log(vehicle, $"Parking source={parkingSource.Index} size={parkingSize}");

                    var parameters = new PathfindParameters
                    {
                        m_MaxSpeed = carData.m_MaxSpeed,
                        m_WalkSpeed = 5.555556f,
                        m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                        m_Methods = VehicleUtils.GetPathMethods(carData),
                        m_ParkingTarget = parkingSource,
                        m_ParkingDelta = currentLane.m_CurvePosition.z,
                        m_ParkingSize = parkingSize,
                        m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData),
                    };

                    VehicleDebugLogger.Log(vehicle, $"Parameters methods=0x{(uint)parameters.m_Methods:X} ignoredRules=0x{(uint)parameters.m_IgnoredRules:X}");

                    var origin = new SetupQueueTarget
                    {
                        m_Type = SetupTargetType.CurrentLocation,
                        m_Methods = VehicleUtils.GetPathMethods(carData) | PathMethod.Parking,
                        m_RoadTypes = RoadTypes.Car
                    };

                    var destination = new SetupQueueTarget
                    {
                        m_Type = SetupTargetType.CurrentLocation,
                        m_Methods = VehicleUtils.GetPathMethods(carData),
                        m_RoadTypes = RoadTypes.Car,
                        m_Entity = destEntity
                    };

                    var setupItem = new SetupQueueItem(vehicle, parameters, origin, destination);
                    VehicleDebugLogger.Log(vehicle, $"ENQUEUE repath (origin.methods=0x{(uint)origin.m_Methods:X} dest.methods=0x{(uint)destination.m_Methods:X})");

                    VehicleUtils.SetupPathfind(ref currentLane, ref pathOwner, _activeSetupQueue.AsParallelWriter(), setupItem);

                    _carCurrentLaneLookup[vehicle] = currentLane;
                    _pathOwnerLookup[vehicle] = pathOwner;
                    em.AddComponent<RepathCreated>(vehicle);

                    VehicleDebugLogger.Log(vehicle, $"RepathCreated added. PathOwner new state=0x{((uint)pathOwner.m_State):X}");
                }
                catch (Exception ex)
                {
                    VehicleDebugLogger.Log(vehicle, "EXCEPTION " + ex.Message);
                }
            }
        }

        private void EnsureSetupQueueForThisFrame()
        {
            uint frame = _simulationSystem.frameIndex;
            if (frame == _queueFrame && _activeSetupQueue.IsCreated)
                return;

            _activeSetupQueue = _pathfindSetupSystem.GetQueue(this, maxDelayFrames: 30, spreadFrames: 0);
            _queueFrame = frame;
            VehicleDebugLogger.LogOnce($"Acquired new setup queue (frame={frame})");
        }

        static void DumpAllFields(object obj)
        {
            var t = obj.GetType();
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var cur = t; cur != null; cur = cur.BaseType)
            {
                foreach (var f in cur.GetFields(all))
                {
                    LogUtil.Info($"{cur.FullName}.{f.Name} : {f.FieldType.FullName} (static={f.IsStatic}, vis={(f.IsPublic ? "public" : f.IsFamily ? "protected" : f.IsPrivate ? "private" : "internal")})");
                }
            }
        }

        private void TryFlushToVanillaPathfindQueue()
        {
            try
            {
                foreach (var sys in World.Systems)
                {
                    var t = sys.GetType();
                    if (!t.Name.Contains("Pathfind") && !t.Name.Contains("PathFind")) continue;
                    var field = t.GetField("m_SetupQueue", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (field == null) continue;
                    if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(NativeQueue<>))
                    {
                        var elementType = field.FieldType.GetGenericArguments()[0];
                        if (elementType != typeof(SetupQueueItem)) continue;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollboothRepathSystem: Failed reflection flush: {ex.Message}");
            }
        }
    }
}