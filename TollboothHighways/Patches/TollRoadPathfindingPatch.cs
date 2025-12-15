using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Patches
{
    /// <summary>
    /// Harmony patches to modify pathfinding costs for toll road restrictions.
    /// Vehicles will avoid toll roads they're not allowed to use by applying
    /// a very high cost penalty during pathfinding.
    /// </summary>
    public static class TollRoadPathfindingPatch
    {
        private static EntityManager _entityManager;
        private static bool _isInitialized;
        
        // Cost multiplier for restricted lanes (very high = effectively blocked)
        private const float RESTRICTED_COST_MULTIPLIER = 100000f;
        
        /// <summary>
        /// Initializes the patch system with the entity manager.
        /// </summary>
        public static void Initialize(EntityManager entityManager)
        {
            _entityManager = entityManager;
            _isInitialized = true;
            
            if (ModSettings.Instance?.EnableGeneralLogging == true)
            {
                LogUtil.Info("TollRoadPathfindingPatch initialized");
            }
        }
        
        /// <summary>
        /// Checks if a vehicle group is allowed on a toll road entity.
        /// </summary>
        public static bool IsVehicleGroupAllowed(VehicleGroup vehicleGroup, Entity tollRoadEntity)
        {
            if (!_isInitialized || !_entityManager.Exists(tollRoadEntity))
                return true;
            
            try
            {
                // All vehicles allowed
                if (_entityManager.HasComponent<TollRoadAllVehiclesData>(tollRoadEntity))
                    return true;

                // Check specific permissions
                bool hasPrivate = _entityManager.HasComponent<TollRoadPrivateTransportData>(tollRoadEntity);
                bool hasTruck = _entityManager.HasComponent<TollRoadTruckData>(tollRoadEntity);
                bool hasPublic = _entityManager.HasComponent<TollRoadPublicTransportData>(tollRoadEntity);
                bool hasService = _entityManager.HasComponent<TollRoadServiceVehiclesData>(tollRoadEntity);
                
                // If no specific restriction, allow all
                if (!hasPrivate && !hasTruck && !hasPublic && !hasService)
                    return true;

                // Match vehicle group to road type
                switch (vehicleGroup)
                {
                    case VehicleGroup.PrivateTransport:
                        return hasPrivate;
                        
                    case VehicleGroup.Trucks:
                        return hasTruck;
                        
                    case VehicleGroup.PublicTransport:
                        return hasPublic;
                        
                    case VehicleGroup.ServiceVehicles:
                        // Service vehicles always allowed (emergency access)
                        return true;
                        
                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                if (ModSettings.Instance?.EnableGeneralLogging == true)
                {
                    LogUtil.Error($"Error checking vehicle permissions: {ex.Message}");
                }
                return true;
            }
        }
        
        /// <summary>
        /// Gets the vehicle group for an entity.
        /// </summary>
        public static VehicleGroup GetVehicleGroup(Entity vehicleEntity)
        {
            if (!_isInitialized || !_entityManager.Exists(vehicleEntity))
                return VehicleGroup.PrivateTransport;
            
            try
            {
                // Public transport
                if (_entityManager.HasComponent<Game.Vehicles.PublicTransport>(vehicleEntity))
                    return VehicleGroup.PublicTransport;
                if (_entityManager.HasComponent<Game.Vehicles.Taxi>(vehicleEntity))
                    return VehicleGroup.PublicTransport;
                
                // Trucks
                if (_entityManager.HasComponent<Game.Vehicles.DeliveryTruck>(vehicleEntity))
                    return VehicleGroup.Trucks;
                
                // Service vehicles
                if (_entityManager.HasComponent<Game.Vehicles.PoliceCar>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.Ambulance>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.FireEngine>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.GarbageTruck>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.Hearse>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.MaintenanceVehicle>(vehicleEntity) ||
                    _entityManager.HasComponent<Game.Vehicles.PostVan>(vehicleEntity) ||
                    _entityManager.HasComponent<PrisonerTransport>(vehicleEntity))
                {
                    return VehicleGroup.ServiceVehicles;
                }
                
                // Default: Private transport
                return VehicleGroup.PrivateTransport;
            }
            catch
            {
                return VehicleGroup.PrivateTransport;
            }
        }
        
        /// <summary>
        /// Checks if a lane belongs to a restricted toll road for the given vehicle.
        /// </summary>
        public static bool IsLaneRestricted(Entity laneEntity, Entity vehicleEntity)
        {
            if (!_isInitialized)
                return false;
            
            try
            {
                // Get the owner (road edge) of the lane
                if (!_entityManager.HasComponent<Owner>(laneEntity))
                    return false;
                
                var owner = _entityManager.GetComponentData<Owner>(laneEntity);
                var roadEntity = owner.m_Owner;
                
                // Check if this is a toll road
                if (!_entityManager.HasComponent<TollRoadPrefabData>(roadEntity))
                    return false;
                
                // Get vehicle group and check permissions
                var vehicleGroup = GetVehicleGroup(vehicleEntity);
                return !IsVehicleGroupAllowed(vehicleGroup, roadEntity);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets the cost multiplier for a lane based on toll road restrictions.
        /// </summary>
        public static float GetLaneCostMultiplier(Entity laneEntity, VehicleGroup vehicleGroup)
        {
            if (!_isInitialized)
                return 1f;
            
            try
            {
                // Get the owner (road edge) of the lane
                if (!_entityManager.HasComponent<Owner>(laneEntity))
                    return 1f;
                
                var owner = _entityManager.GetComponentData<Owner>(laneEntity);
                var roadEntity = owner.m_Owner;
                
                // Check if this is a toll road
                if (!_entityManager.HasComponent<TollRoadPrefabData>(roadEntity))
                    return 1f;
                
                // Check if vehicle group is allowed
                if (IsVehicleGroupAllowed(vehicleGroup, roadEntity))
                    return 1f;
                
                // Vehicle not allowed - return high cost multiplier
                return RESTRICTED_COST_MULTIPLIER;
            }
            catch
            {
                return 1f;
            }
        }
    }
}