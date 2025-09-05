using Game;
using Game.Net;
using Game.Prefabs;
using System;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using TollRoadHighways.Domain.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace Systems
{
    public partial class TollRoadPrefabUpdateSystem : GameSystemBase
    {
        private PrefabSystem prefabSystem;
        private bool isInitialized = false;
        private int initializationAttempts = 0;
        private const int MAX_INITIALIZATION_ATTEMPTS = 10;

        protected override void OnCreate()
        {
            base.OnCreate();
            prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            LogUtil.Info("TollRoadPrefabUpdateSystem: System created and initialized");
        }

        protected override void OnUpdate()
        {
            // Only run the initialization once, with retry logic
            if (!isInitialized && initializationAttempts < MAX_INITIALIZATION_ATTEMPTS)
            {
                initializationAttempts++;
                
                if (TryInitializeTollPrefabs())
                {
                    isInitialized = true;
                    // Stop this system from updating after successful initialization
                    Enabled = false;
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Prefab initialization completed successfully on attempt {initializationAttempts}, system disabled");
                }
                else
                {
                    LogUtil.Warn($"TollRoadPrefabUpdateSystem: Prefab initialization attempt {initializationAttempts} failed, will retry on next update");
                    
                    if (initializationAttempts >= MAX_INITIALIZATION_ATTEMPTS)
                    {
                        LogUtil.Error("TollRoadPrefabUpdateSystem: Maximum initialization attempts reached, disabling system");
                        Enabled = false;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to initialize all toll-related prefabs with their components
        /// </summary>
        /// <returns>True if initialization was successful, false otherwise</returns>
        private bool TryInitializeTollPrefabs()
        {
            LogUtil.Info("TollRoadPrefabUpdateSystem: Starting prefab initialization");

            try
            {
                int successCount = 0;
                int totalPrefabs = 10; // Total number of prefabs we're trying to initialize

                // Automatic Tollbooths Roads Prefab
                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Automatic - Private Transit)", false, VehicleGroup.PrivateTransport))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Automatic - Public Transport)", false, VehicleGroup.PublicTransport))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Automatic - Heavy Transport)", false, VehicleGroup.Trucks))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Automatic - Service Transport)", false, VehicleGroup.ServiceVehicles))
                    successCount++;

                // Manual Tollboooth Roads Prefab
                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Manual - Private Transit)", true, VehicleGroup.PrivateTransport))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Manual - Public Transport)", true, VehicleGroup.PublicTransport))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Manual - Heavy Transport)", true, VehicleGroup.PublicTransport))
                    successCount++;

                if (AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Manual - Service Transport)", true, VehicleGroup.PublicTransport))
                    successCount++;



                // Add toll cabin components
                if (AddTollCabinComponentToRoad("StaticObjectPrefab", "TollBooth", false))
                    successCount++;

                if (AddTollCabinComponentToRoad("StaticObjectPrefab", "TollBoothManual", true))
                    successCount++;

                bool allSuccessful = successCount == totalPrefabs;
                
                if (allSuccessful)
                {
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: All {successCount}/{totalPrefabs} prefabs initialized successfully");
                }
                else
                {
                    LogUtil.Warn($"TollRoadPrefabUpdateSystem: Only {successCount}/{totalPrefabs} prefabs initialized successfully");
                }

                return allSuccessful;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollRoadPrefabUpdateSystem: Exception during prefab initialization: {ex.Message}");
                LogUtil.Exception(ex);
                return false;
            }
        }

        /// <summary>
        /// Adds toll components to road prefabs
        /// </summary>
        /// <param name="typePrefab">The prefab type</param>
        /// <param name="prefabNameToSearch">The prefab name to search for</param>
        /// <param name="isManual">Whether this is a manual toll booth</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool AddTollComponentToRoad(string typePrefab, string prefabNameToSearch, bool isManual, VehicleGroup vehicleGroup)
        {
            try
            {
                // Check if the prefab for the toll road exists
                if (prefabSystem.TryGetPrefab(new PrefabID(typePrefab, prefabNameToSearch), out PrefabBase tollRoadPrefab))
                {
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Found prefab '{prefabNameToSearch}'");

                    // Check if the prefab already has the TollRoadPrefabInfo component
                    if (tollRoadPrefab.GetComponent<TollRoadPrefabInfo>())
                    {
                        LogUtil.Info($"TollRoadPrefabUpdateSystem: Prefab '{prefabNameToSearch}' already has TollRoadPrefabInfo, skipping");
                        return true; // Already initialized, consider this a success
                    }

                    // Add the TollRoadPrefabInfo component
                    var tollRoadInfo = tollRoadPrefab.AddComponent<TollRoadPrefabInfo>();
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Added TollRoadPrefabInfo to '{prefabNameToSearch}'");

                    // Add the component corresponding to the group type of vehicles allowed
                    SetGroupVehicleComponent(tollRoadPrefab, vehicleGroup);

                    // Add the TollRoadManualInfo component if this is a manual toll booth
                    if (isManual)
                    {   var manualInfo = tollRoadPrefab.AddComponent<TollBoothManualInfo>();
                        LogUtil.Info($"TollRoadPrefabUpdateSystem: Added TollRoadManualInfo to '{prefabNameToSearch}'");
                    }

                    // Update the prefab in the system
                    prefabSystem.UpdatePrefab(tollRoadPrefab);
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Successfully updated prefab '{prefabNameToSearch}' in PrefabSystem");
                    
                    return true;
                }
                else
                {
                    LogUtil.Warn($"TollRoadPrefabUpdateSystem: Could not find prefab '{prefabNameToSearch}' of type '{typePrefab}' - prefab may not be loaded yet");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollRoadPrefabUpdateSystem: Failed to add toll component to '{prefabNameToSearch}'. Error: {ex.Message}");
                LogUtil.Exception(ex);
                return false;
            }
        }

        /// <summary>
        /// Configures the specified vehicle group by adding the appropriate component to the given prefab.
        /// </summary>
        /// <remarks>This method adds a specific component to the provided prefab based on the vehicle
        /// group.  The component added corresponds to the type of vehicles in the group, such as private transport,
        /// public transport, trucks, or service vehicles.</remarks>
        /// <param name="Prefab">The prefab to which the vehicle group-specific component will be added. Cannot be null.</param>
        /// <param name="vehicleGroup">The vehicle group that determines the type of component to add.</param>
        private void SetGroupVehicleComponent(PrefabBase Prefab, VehicleGroup vehicleGroup)
        {
            switch(vehicleGroup)
            {
                case VehicleGroup.PrivateTransport:
                    Prefab.AddComponent<TollRoadPrivateTransportInfo>();
                    break;

                case VehicleGroup.PublicTransport:
                    Prefab.AddComponent<TollRoadPublicTransportInfo>();
                    break;

                case VehicleGroup.Trucks:
                    Prefab.AddComponent<TollRoadTruckInfo>();
                    break;

                case VehicleGroup.ServiceVehicles:
                    Prefab.AddComponent<TollRoadServiceVehiclesInfo>();
                    break;
            }

        }

        /// <summary>
        /// Adds toll cabin components to toll booth prefabs
        /// </summary>
        /// <param name="typePrefab">The prefab type</param>
        /// <param name="prefabNameToSearch">The prefab name to search for</param>
        /// <returns>True if successful, false otherwise</returns>
        private bool AddTollCabinComponentToRoad(string typePrefab, string prefabNameToSearch, bool isManual)
        {
            try
            {
                // Check if the prefab for the toll cabin exists
                if (prefabSystem.TryGetPrefab(new PrefabID(typePrefab, prefabNameToSearch), out PrefabBase tollCabinPrefab))
                {
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Found toll cabin prefab '{prefabNameToSearch}'");

                    // Check if the prefab already has the TollBoothPrefabInfo component
                    if (tollCabinPrefab.GetComponent<TollBoothPrefabInfo>())
                    {
                        LogUtil.Info($"TollRoadPrefabUpdateSystem: Toll cabin '{prefabNameToSearch}' already has TollBoothPrefabInfo, skipping");
                        return true; // Already initialized, consider this a success
                    }

                    // Add the TollBoothPrefabInfo component
                    var tollBoothInfo = tollCabinPrefab.AddComponent<TollBoothPrefabInfo>();
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Added TollBoothPrefabInfo to toll cabin '{prefabNameToSearch}'");

                    // Update the prefab in the system
                    prefabSystem.UpdatePrefab(tollCabinPrefab);
                    LogUtil.Info($"TollRoadPrefabUpdateSystem: Successfully updated toll cabin prefab '{prefabNameToSearch}' in PrefabSystem");

                    // If this is a manual toll road, add the TollBoothManualInfo component
                    if (isManual)
                    {
                        var manualInfo = tollCabinPrefab.AddComponent<TollBoothManualInfo>();
                        LogUtil.Info($"TollRoadPrefabUpdateSystem: Added TollBoothManualInfo to '{prefabNameToSearch}'");
                    }

                    return true;
                }
                else
                {
                    LogUtil.Warn($"TollRoadPrefabUpdateSystem: Could not find toll cabin prefab '{prefabNameToSearch}' of type '{typePrefab}' - prefab may not be loaded yet");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollRoadPrefabUpdateSystem: Failed to add toll cabin component to '{prefabNameToSearch}'. Error: {ex.Message}");
                LogUtil.Exception(ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the update interval for this system - must be power of 2
        /// </summary>
        /// <param name="phase">The system update phase</param>
        /// <returns>Update interval in ticks (power of 2)</returns>
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // For PrefabUpdate phase, use a small interval to retry quickly
            // 16 ticks = roughly 16/262144 of a day ≈ 0.000061 days ≈ 0.0015 hours ≈ 0.09 minutes ≈ 5.4 seconds at 60fps
            if (phase == SystemUpdatePhase.PrefabUpdate)
            {
                return isInitialized ? 0 : 16; // 16 is a power of 2, stop updating when initialized
            }

            // Should not be called for other phases, but return a safe value
            return 64; // 64 is a power of 2
        }

        /// <summary>
        /// Gets the update offset for this system to spread load
        /// </summary>
        /// <param name="phase">The system update phase</param>
        /// <returns>Update offset in ticks</returns>
        public override int GetUpdateOffset(SystemUpdatePhase phase)
        {
            // Offset by 2 ticks to spread the load across different frames
            return 2;
        }
    }
}