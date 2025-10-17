using System.Reflection;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities; 
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using CarLane = Game.Net.CarLane;
using DomainVehicleType = TollboothHighways.Domain.Enums.VehicleType;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors vehicle PathElements to detect and prevent invalid tollbooth road usage.
    /// This system enforces that vehicles only use tollbooths designated for their vehicle type
    /// by monitoring paths, adjusting pathfind costs, and marking invalid paths as obsolete.
    /// </summary>
    /// <remarks>
    /// Main thread execution for debugging support as per AGENTS.MD requirements.
    /// </remarks>
    public partial class VehicleTollboothPathMonitoringSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;
        private BufferLookup<PathElement> m_PathElementLookup;
        private ComponentLookup<PathOwner> m_PathOwnerLookup;
        private ComponentLookup<CarCurrentLane> m_CarCurrentLaneLookup;
        private ComponentLookup<Car> m_CarLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Game.Prefabs.PrefabRef> m_PrefabRefLookup;
        private ComponentLookup<CarLane> m_CarLaneLookup;
        private ComponentLookup<Curve> m_CurveLookup;
        private ComponentLookup<TollRoadPrefabData> m_TollRoadPrefabDataLookup;
        private ComponentLookup<TollBoothPrefabData> m_TollBoothPrefabDataLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_TollRoadPrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TollRoadTruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_TollRoadPublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_TollRoadServiceVehiclesLookup;
        private BufferLookup<Game.Net.SubLane> m_SubLaneLookup;
        
        private SimulationSystem m_SimulationSystem;
        private uint m_LastProcessedFrame;
        private const uint FRAMES_BETWEEN_CHECKS = 15; // Check every ~0.25 seconds at 60fps        
        private bool m_LogInitialized;
        
        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();

            // Initialize lookups
            m_PathElementLookup = GetBufferLookup<PathElement>(true);
            m_PathOwnerLookup = GetComponentLookup<PathOwner>(false);
            m_CarCurrentLaneLookup = GetComponentLookup<CarCurrentLane>(true);
            m_CarLookup = GetComponentLookup<Car>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_PrefabRefLookup = GetComponentLookup<PrefabRef>(true);
            m_CarLaneLookup = GetComponentLookup<CarLane>(true);
            m_CurveLookup = GetComponentLookup<Curve>(true);
            m_TollRoadPrefabDataLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_TollBoothPrefabDataLookup = GetComponentLookup<TollBoothPrefabData>(true);
            m_TollRoadPrivateTransportLookup = GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TollRoadTruckLookup = GetComponentLookup<TollRoadTruckData>(true);
            m_TollRoadPublicTransportLookup = GetComponentLookup<TollRoadPublicTransportData>(true);
            m_TollRoadServiceVehiclesLookup = GetComponentLookup<TollRoadServiceVehiclesData>(true);
            m_SubLaneLookup = GetBufferLookup<Game.Net.SubLane>(true);

            // Query for all vehicles with paths (cars, trucks, buses, service vehicles)
            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<PathOwner>(),
                    ComponentType.ReadWrite<PathElement>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<CarCurrentLane>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            m_LastProcessedFrame = 0;

            LogUtil.Info("VehicleTollboothPathMonitoringSystem: OnCreate() - System created successfully");
        }

        protected override void OnUpdate()
        {
            // Throttle updates to reduce performance impact
            uint currentFrame = m_SimulationSystem.frameIndex;
            if (currentFrame - m_LastProcessedFrame < FRAMES_BETWEEN_CHECKS)
            {
                return;
            }
            m_LastProcessedFrame = currentFrame;

            EnsureLogger();

            // Update all lookups
            UpdateLookups();

            if (m_VehicleQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            // Process vehicles on main thread for debugging (as per AGENTS.MD)
            var vehicles = m_VehicleQuery.ToEntityArray(Allocator.Temp);

            try
            {
                int checkedCount = 0;
                int invalidPathsFound = 0;

                foreach (var vehicleEntity in vehicles)
                {
                    try
                    {
                        checkedCount++;

                        // Check if vehicle's path contains invalid tollbooth usage
                        if (CheckAndHandleInvalidTollboothPath(vehicleEntity, currentFrame))
                        {
                            invalidPathsFound++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"VehicleTollboothPathMonitoringSystem: Exception processing vehicle {vehicleEntity.Index}: {ex.Message}");
                        VehicleDebugLogger.Log(vehicleEntity, $"Exception in path monitoring: {ex.Message}");
                    }
                }

                if (invalidPathsFound > 0)
                {
                    VehicleDebugLogger.LogOnce($"VehicleTollboothPathMonitoringSystem: Frame {currentFrame} - Checked {checkedCount} vehicles, found {invalidPathsFound} with invalid tollbooth paths");
                }
            }
            finally
            {
                vehicles.Dispose();
            }
        }

        private void UpdateLookups()
        {
            m_PathElementLookup.Update(this);
            m_PathOwnerLookup.Update(this);
            m_CarCurrentLaneLookup.Update(this);
            m_CarLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_PrefabRefLookup.Update(this);
            m_CarLaneLookup.Update(this);
            m_CurveLookup.Update(this);
            m_TollRoadPrefabDataLookup.Update(this);
            m_TollBoothPrefabDataLookup.Update(this);
            m_TollRoadPrivateTransportLookup.Update(this);
            m_TollRoadTruckLookup.Update(this);
            m_TollRoadPublicTransportLookup.Update(this);
            m_TollRoadServiceVehiclesLookup.Update(this);
            m_SubLaneLookup.Update(this);
        }

        /// <summary>
        /// Checks if a vehicle's path contains invalid tollbooth roads and handles it.
        /// </summary>
        /// <param name="vehicleEntity">The vehicle entity to check</param>
        /// <param name="currentFrame">Current simulation frame</param>
        /// <returns>True if invalid path was detected and handled</returns>
        private bool CheckAndHandleInvalidTollboothPath(Entity vehicleEntity, uint currentFrame)
        {
            // Get vehicle type using VehiclesUtil
            var entityManager = EntityManager;
            DomainVehicleType vehicleType = VehiclesUtil.GetVehicleTypeStatic(vehicleEntity, entityManager);
            if (vehicleType == DomainVehicleType.None)
            {
                return false;
            }

            // Get path elements
            if (!m_PathElementLookup.TryGetBuffer(vehicleEntity, out var pathElements) || pathElements.Length == 0)
            {
                return false;
            }

            // Get path owner
            if (!m_PathOwnerLookup.TryGetComponent(vehicleEntity, out var pathOwner))
            {
                return false;
            }

            // Skip if path is already obsolete or being calculated
            if ((pathOwner.m_State & (PathFlags.Obsolete | PathFlags.Pending | PathFlags.Failed)) != 0)
            {
                return false;
            }

            // Check each path element for invalid tollbooth roads
            for (int i = 0; i < pathElements.Length; i++)
            {
                var pathElement = pathElements[i];
                Entity laneEntity = pathElement.m_Target;

                if (m_OwnerLookup.TryGetComponent(laneEntity, out var owner))
                {
                    continue;
                }

                // Check if this lane belongs to a tollbooth road
                Entity roadEntity = owner.m_Owner;
                if (!m_TollRoadPrefabDataLookup.HasComponent(roadEntity))
                {
                    continue;
                }

                VehicleDebugLogger.Log(vehicleEntity, $"Vehicle path contains toll road {roadEntity.Index} at path element {i}");

                // Check if vehicle is allowed to use this tollbooth type
                bool isAllowed = VehiclesUtil.IsVehicleAllowedOnTollRoad(
                    vehicleType,
                    roadEntity,
                    m_TollRoadPrivateTransportLookup,
                    m_TollRoadTruckLookup,
                    m_TollRoadPublicTransportLookup,
                    m_TollRoadServiceVehiclesLookup,
                    EntityManager
                );

                if (!isAllowed)
                {
                    VehicleDebugLogger.Log(vehicleEntity, 
                        $"INVALID TOLLBOOTH USAGE DETECTED! Vehicle type {vehicleType} attempting to use incompatible toll road {roadEntity.Index} (Current Tollbooth Road: {VehiclesUtil.GetTollboothRoadType(entityManager, roadEntity)})");

                    // Handle the invalid path
                    HandleInvalidTollboothPath(vehicleEntity, ref pathOwner, roadEntity, vehicleType, i, pathElements.Length);
                    
                    return true;
                }
                else
                {
                    VehicleDebugLogger.Log(vehicleEntity, 
                        $"Valid tollbooth usage: Vehicle type {vehicleType} allowed on toll road {roadEntity.Index}");
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the road entity that owns a specific lane entity.
        /// </summary>
        private Entity GetRoadEntityFromLane(Entity laneEntity)
        {
            if (m_OwnerLookup.TryGetComponent(laneEntity, out var owner))
            {
                return owner.m_Owner;
            }
            return Entity.Null;
        }

        /// <summary>
        /// Handles a vehicle with an invalid tollbooth in its path by marking the path obsolete
        /// to force pathfinding recalculation.
        /// </summary>
        private void HandleInvalidTollboothPath(
            Entity vehicleEntity, 
            ref PathOwner pathOwner, 
            Entity invalidRoadEntity,
            Domain.Enums.VehicleType vehicleType,
            int pathElementIndex,
            int totalPathElements)
        {
            try
            {
                VehicleDebugLogger.Log(vehicleEntity, 
                    $"Marking path as OBSOLETE to force rerouting. Invalid toll road at element {pathElementIndex}/{totalPathElements}");

                // Mark the path as obsolete to force the pathfinding system to recalculate
                pathOwner.m_State |= PathFlags.Obsolete;
                
                // Update the PathOwner component
                m_PathOwnerLookup[vehicleEntity] = pathOwner;

                // Add Updated component to ensure systems process this change
                if (!EntityManager.HasComponent<Updated>(vehicleEntity))
                {
                    EntityManager.AddComponent<Updated>(vehicleEntity);
                }

                VehicleDebugLogger.LogOnce($"VehicleTollboothPathMonitoringSystem: Vehicle {vehicleEntity.Index} (Type: {vehicleType}) path marked obsolete due to invalid toll road {invalidRoadEntity.Index}");

                // Log detailed path information for debugging
                if (m_PathElementLookup.TryGetBuffer(vehicleEntity, out var pathElements))
                {
                    VehicleDebugLogger.Log(vehicleEntity, $"Current path has {pathElements.Length} elements");
                    
                    // Log first few and last few elements for context
                    int logCount = System.Math.Min(3, pathElements.Length);
                    for (int i = 0; i < logCount; i++)
                    {
                        var element = pathElements[i];
                        bool isTollRoad = m_TollRoadPrefabDataLookup.HasComponent(GetRoadEntityFromLane(element.m_Target));
                        VehicleDebugLogger.Log(vehicleEntity, 
                            $"  PathElement[{i}]: Lane {element.m_Target.Index} (TollRoad: {isTollRoad})");
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"VehicleTollboothPathMonitoringSystem: Failed to handle invalid path for vehicle {vehicleEntity.Index}: {ex.Message}");
                LogUtil.Error($"VehicleTollboothPathMonitoringSystem: Stack trace: {ex.StackTrace}");
                VehicleDebugLogger.Log(vehicleEntity, $"Failed to mark path obsolete: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            LogUtil.Info("VehicleTollboothPathMonitoringSystem: OnDestroy() - System destroyed");
        }

        private void EnsureLogger()
        {
            if (m_LogInitialized)
            {
                return;
            }

            try
            {
                VehicleDebugLogger.Init();
                VehicleDebugLogger.LogOnce("=== VehicleTollboothPathMonitoringSystem logging started ===");
            }
            catch
            {
                // best-effort logging initialisation
            }

            m_LogInitialized = true;
        }

    }
}