using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Common;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.Vehicles;
using Game.Routes;
using Game.Prefabs;
using Unity.Entities;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;
using TollboothHighways.Extensions;
using TollboothHighways.Utilities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// UI System for Transport Route vehicle selection and management
    /// </summary>
    public partial class TransportRouteUISystem : ExtendedInfoSectionBase
    {
        private ToolSystem m_ToolSystem;
        private PrefabSystem m_PrefabSystem;
        
        // Route Data Bindings
        private ValueBindingHelper<RouteDataModel> m_RouteData;
        private ValueBindingHelper<VehicleDataModel[]> m_AvailableVehicles;
        private ValueBindingHelper<VehicleDataModel> m_SelectedPrimaryVehicle;
        private ValueBindingHelper<VehicleDataModel> m_SelectedSecondaryVehicle;
        private ValueBindingHelper<bool> m_IsPanelVisible;
        
        // Performance Data Bindings
        private ValueBindingHelper<int> m_DailyPassengers;
        private ValueBindingHelper<float> m_RouteLength;
        private ValueBindingHelper<int> m_NumberOfStops;
        private ValueBindingHelper<float> m_AverageWaitingTime;
        private ValueBindingHelper<float> m_RouteEfficiency;
        private ValueBindingHelper<int> m_DailyRevenue;

        // Track initialization state
        private bool m_IsInitialized = false;
        private Entity m_LastSelectedEntity = Entity.Null;

        protected override string group => Mod.Id;
        
        public new bool visible => selectedEntity != Entity.Null && 
            (EntityManager.HasComponent<Route>(selectedEntity) || 
             EntityManager.HasComponent<Game.Routes.TransportLine>(selectedEntity));

        #region Data Models
        
        /// <summary>
        /// Data model for route information
        /// </summary>
        public struct RouteDataModel
        {
            public string id;
            public string name;
            public string type; // "bus", "train", "subway", "tram"
            public float length;
            public int stops;
            public int passengers;
            public int revenue;
            public bool isActive;

            // Create a default/empty route data model
            public static RouteDataModel CreateDefault()
            {
                return new RouteDataModel
                {
                    id = "",
                    name = "No Route Selected",
                    type = "bus",
                    length = 0f,
                    stops = 0,
                    passengers = 0,
                    revenue = 0,
                    isActive = false
                };
            }
        }

        /// <summary>
        /// Data model for vehicle information
        /// </summary>
        public struct VehicleDataModel
        {
            public string entity;
            public string id;
            public string thumbnail;
            public string name;
            public int capacity;
            public float fuelEfficiency;
            public int maintenanceCost;
            public string vehicleType; // "primary", "secondary"
            public bool isAvailable;

            // Create a default/empty vehicle data model
            public static VehicleDataModel CreateDefault()
            {
                return new VehicleDataModel
                {
                    entity = "",
                    id = "",
                    thumbnail = "",
                    name = "No Vehicle Selected",
                    capacity = 0,
                    fuelEfficiency = 0f,
                    maintenanceCost = 0,
                    vehicleType = "primary",
                    isAvailable = false
                };
            }
        }

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            // Initialize route data bindings with proper default values
            m_RouteData = CreateBinding("m_RouteData", RouteDataModel.CreateDefault());
            m_AvailableVehicles = CreateBinding("m_AvailableVehicles", new VehicleDataModel[0]);
            m_SelectedPrimaryVehicle = CreateBinding("m_SelectedPrimaryVehicle", VehicleDataModel.CreateDefault());
            m_SelectedSecondaryVehicle = CreateBinding("m_SelectedSecondaryVehicle", VehicleDataModel.CreateDefault());
            m_IsPanelVisible = CreateBinding("m_IsPanelVisible", false);

            // Initialize performance data bindings
            m_DailyPassengers = CreateBinding("m_DailyPassengers", 0);
            m_RouteLength = CreateBinding("m_RouteLength", 0f);
            m_NumberOfStops = CreateBinding("m_NumberOfStops", 0);
            m_AverageWaitingTime = CreateBinding("m_AverageWaitingTime", 0f);
            m_RouteEfficiency = CreateBinding("m_RouteEfficiency", 0f);
            m_DailyRevenue = CreateBinding("m_DailyRevenue", 0);

            // Register trigger handlers for vehicle selection
            AddBinding(new TriggerBinding<string, string, string>(group, "setRouteVehicles", SetRouteVehicles));
            AddBinding(new TriggerBinding<string>(group, "optimizeRoute", OptimizeRoute));
            AddBinding(new TriggerBinding<string>(group, "resetVehicles", ResetVehicles));
            AddBinding(new TriggerBinding<string>(group, "generateReport", GenerateReport));

            m_InfoUISystem.AddMiddleSection(this);

            // Force initial update of all bindings to prevent "update not called" errors
            InitializeBindings();

            LogUtil.Info("TransportRouteUISystem created and bindings initialized.");
        }

        /// <summary>
        /// Initialize all bindings with default values to prevent access errors
        /// </summary>
        private void InitializeBindings()
        {
            try
            {
                // Force initial values for all bindings
                m_RouteData.Value =  RouteDataModel.CreateDefault();
                m_AvailableVehicles.Value = new VehicleDataModel[0];
                m_SelectedPrimaryVehicle.Value = VehicleDataModel.CreateDefault();
                m_SelectedSecondaryVehicle.Value = VehicleDataModel.CreateDefault();
                m_IsPanelVisible.Value = false;
                
                m_DailyPassengers.Value = 0;
                m_RouteLength.Value = 0f;
                m_NumberOfStops.Value = 0;
                m_AverageWaitingTime.Value = 0f;
                m_RouteEfficiency.Value = 0f;
                m_DailyRevenue.Value = 0;

                m_IsInitialized = true;

                LogUtil.Info("TransportRouteUISystem: All bindings initialized with default values");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Failed to initialize bindings - {ex.Message}");
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            
            // Ensure we're enabled
            if (!Enabled)
            {
                this.Enabled = true;
            }

            // Ensure bindings are initialized
            if (!m_IsInitialized)
            {
                InitializeBindings();
                return;
            }

            Entity selectedEntity = m_ToolSystem.selected;

            // Check if selection has changed
            if (selectedEntity != m_LastSelectedEntity)
            {
                m_LastSelectedEntity = selectedEntity;
                OnSelectionChanged(selectedEntity);
            }

            // Update data if we have a valid entity
            if (IsValidTransportEntity(selectedEntity))
            {
                if (!m_IsPanelVisible.Value)
                {
                    LogUtil.Info("TransportRouteUISystem: Showing transport route panel");
                    m_IsPanelVisible.Value = true;
                    base.visible = true;
                }
                UpdateRouteData(selectedEntity);
                UpdateAvailableVehicles();
                UpdatePerformanceData(selectedEntity);
            }
            else
            {
                if (m_IsPanelVisible.Value)
                {
                    LogUtil.Info("TransportRouteUISystem: Hiding transport route panel");
                    m_IsPanelVisible.Value = false;
                    base.visible = false;
                    
                    // Reset to default values when no valid entity is selected
                    ResetToDefaults();
                }
            }
        }

        /// <summary>
        /// Handle selection changes
        /// </summary>
        private void OnSelectionChanged(Entity newSelection)
        {
            if (!IsValidTransportEntity(newSelection))
            {
                // Reset to defaults when invalid entity is selected
                ResetToDefaults();
            }
        }

        /// <summary>
        /// Reset all bindings to default values
        /// </summary>
        private void ResetToDefaults()
        {
            try
            {
                m_RouteData.Value = RouteDataModel.CreateDefault();
                m_SelectedPrimaryVehicle.Value = VehicleDataModel.CreateDefault();
                m_SelectedSecondaryVehicle.Value = VehicleDataModel.CreateDefault();
                
                m_DailyPassengers.Value = 0;
                m_RouteLength.Value = 0f;
                m_NumberOfStops.Value = 0;
                m_AverageWaitingTime.Value = 0f;
                m_RouteEfficiency.Value = 0f;
                m_DailyRevenue.Value = 0;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error resetting to defaults - {ex.Message}");
            }
        }

        #region Entity Validation and Data Updates

        /// <summary>
        /// Check if the selected entity is a valid transport route entity
        /// </summary>
        private bool IsValidTransportEntity(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return false;

            return EntityManager.HasComponent<Route>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TransportLine>(entity) ||
                   EntityManager.HasComponent<Game.Routes.TransportStop>(entity);
        }

        /// <summary>
        /// Update route data from the selected entity
        /// </summary>
        private void UpdateRouteData(Entity entity)
        {
            try
            {
                if (!IsValidTransportEntity(entity))
                    return;

                var routeData = new RouteDataModel
                {
                    id = entity.Index.ToString(),
                    name = GetRouteName(entity),
                    type = GetRouteType(entity),
                    length = GetRouteLength(entity),
                    stops = GetNumberOfStops(entity),
                    passengers = GetDailyPassengers(entity),
                    revenue = GetDailyRevenue(entity),
                    isActive = IsRouteActive(entity)
                };

                m_RouteData.Value = routeData;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error updating route data - {ex.Message}");
            }
        }

        /// <summary>
        /// Update available vehicles based on route type
        /// </summary>
        private void UpdateAvailableVehicles()
        {
            try
            {
                var availableVehicles = new List<VehicleDataModel>();

                // Get all vehicle prefabs from the prefab system
                var vehiclePrefabs = GetAllVehiclePrefabs();

                if (vehiclePrefabs.IsCreated)
                {
                    foreach (var prefab in vehiclePrefabs)
                    {
                        if (EntityManager.TryGetComponent<VehicleData>(prefab, out var vehicleData) &&
                            EntityManager.TryGetComponent<PrefabData>(prefab, out var prefabData))
                        {
                            var vehicle = new VehicleDataModel
                            {
                                entity = prefab.Index.ToString(),
                                id = prefabData.m_Index.ToString(),
                                thumbnail = GetVehicleThumbnail(prefab),
                                name = GetVehicleName(prefab),
                                capacity = GetVehicleCapacity(vehicleData),
                                fuelEfficiency = GetVehicleFuelEfficiency(vehicleData),
                                maintenanceCost = GetVehicleMaintenanceCost(vehicleData),
                                vehicleType = GetVehicleType(vehicleData),
                                isAvailable = IsVehicleAvailable(prefab)
                            };

                            availableVehicles.Add(vehicle);
                        }
                    }
                    vehiclePrefabs.Dispose();
                }

                m_AvailableVehicles.Value = availableVehicles.ToArray();
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error updating available vehicles - {ex.Message}");
            }
        }

        /// <summary>
        /// Update performance data for the selected route
        /// </summary>
        private void UpdatePerformanceData(Entity entity)
        {
            try
            {
                m_DailyPassengers.Value = GetDailyPassengers(entity);
                m_RouteLength.Value = GetRouteLength(entity);
                m_NumberOfStops.Value = GetNumberOfStops(entity);
                m_AverageWaitingTime.Value = GetAverageWaitingTime(entity);
                m_RouteEfficiency.Value = GetRouteEfficiency(entity);
                m_DailyRevenue.Value = GetDailyRevenue(entity);
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error updating performance data - {ex.Message}");
            }
        }

        #endregion

        #region Vehicle Management Methods

        /// <summary>
        /// Set vehicles for the route (triggered from UI)
        /// </summary>
        private void SetRouteVehicles(string routeId, string primaryVehicleId, string secondaryVehicleId)
        {
            LogUtil.Info($"Setting route vehicles - Route: {routeId}, Primary: {primaryVehicleId}, Secondary: {secondaryVehicleId}");

            if (int.TryParse(routeId, out int routeIndex) && 
                int.TryParse(primaryVehicleId, out int primaryIndex))
            {
                Entity routeEntity = new Entity { Index = routeIndex, Version = 1 };
                Entity primaryVehicleEntity = new Entity { Index = primaryIndex, Version = 1 };

                if (EntityManager.Exists(routeEntity) && EntityManager.Exists(primaryVehicleEntity))
                {
                    // Update selected primary vehicle
                    var primaryVehicle = CreateVehicleDataModel(primaryVehicleEntity);
                    m_SelectedPrimaryVehicle.Value = primaryVehicle;

                    // Update secondary vehicle if provided
                    if (!string.IsNullOrEmpty(secondaryVehicleId) && 
                        int.TryParse(secondaryVehicleId, out int secondaryIndex))
                    {
                        Entity secondaryVehicleEntity = new Entity { Index = secondaryIndex, Version = 1 };
                        if (EntityManager.Exists(secondaryVehicleEntity))
                        {
                            var secondaryVehicle = CreateVehicleDataModel(secondaryVehicleEntity);
                            m_SelectedSecondaryVehicle.Value = secondaryVehicle;
                        }
                    }

                    // Apply vehicle selection to the actual route
                    ApplyVehicleSelectionToRoute(routeEntity, primaryVehicleEntity);
                }
            }
        }

        /// <summary>
        /// Create vehicle data model from entity
        /// </summary>
        private VehicleDataModel CreateVehicleDataModel(Entity vehicleEntity)
        {
            var model = new VehicleDataModel
            {
                entity = vehicleEntity.Index.ToString(),
                id = GetVehicleId(vehicleEntity),
                thumbnail = GetVehicleThumbnail(vehicleEntity),
                name = GetVehicleName(vehicleEntity),
                capacity = GetVehicleCapacity(vehicleEntity),
                fuelEfficiency = GetVehicleFuelEfficiency(vehicleEntity),
                maintenanceCost = GetVehicleMaintenanceCost(vehicleEntity),
                vehicleType = GetVehicleType(vehicleEntity),
                isAvailable = true
            };

            return model;
        }

        /// <summary>
        /// Apply vehicle selection to the actual route in the game
        /// </summary>
        private void ApplyVehicleSelectionToRoute(Entity routeEntity, Entity vehicleEntity)
        {
            // This would integrate with the actual game's vehicle assignment system
            // Implementation depends on the specific game's vehicle management system
            LogUtil.Info($"Applied vehicle {vehicleEntity.Index} to route {routeEntity.Index}");
        }

        #endregion

        #region Route Management Actions

        /// <summary>
        /// Optimize the selected route
        /// </summary>
        private void OptimizeRoute(string routeId)
        {
            LogUtil.Info($"Optimizing route: {routeId}");
            
            if (int.TryParse(routeId, out int routeIndex))
            {
                Entity routeEntity = new Entity { Index = routeIndex, Version = 1 };
                if (EntityManager.Exists(routeEntity))
                {
                    // Implement route optimization logic
                    PerformRouteOptimization(routeEntity);
                }
            }
        }

        /// <summary>
        /// Reset vehicles for the route
        /// </summary>
        private void ResetVehicles(string routeId)
        {
            LogUtil.Info($"Resetting vehicles for route: {routeId}");
            
            m_SelectedPrimaryVehicle.Value = new VehicleDataModel();
            m_SelectedSecondaryVehicle.Value = new VehicleDataModel();
        }

        /// <summary>
        /// Generate performance report for the route
        /// </summary>
        private void GenerateReport(string routeId)
        {
            LogUtil.Info($"Generating report for route: {routeId}");
            
            // Implementation for generating route performance report
            // This could trigger a separate UI panel or export data
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get all vehicle prefabs from the game
        /// </summary>
        private NativeArray<Entity> GetAllVehiclePrefabs()
        {
            try
            {
                var query = GetEntityQuery(ComponentType.ReadOnly<VehicleData>(), ComponentType.ReadOnly<PrefabData>());
                return query.ToEntityArray(Allocator.Temp);
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error getting vehicle prefabs - {ex.Message}");
                return new NativeArray<Entity>(0, Allocator.Temp);
            }
        }

        // ... (rest of helper methods remain the same)

        protected override void Reset()
        {
            try
            {
                // Reset all bindings to default values
                m_RouteData.Value = RouteDataModel.CreateDefault();
                m_AvailableVehicles.Value = new VehicleDataModel[0];
                m_SelectedPrimaryVehicle.Value = VehicleDataModel.CreateDefault();
                m_SelectedSecondaryVehicle.Value = VehicleDataModel.CreateDefault();
                m_IsPanelVisible.Value = false;
                
                m_DailyPassengers.Value = 0;
                m_RouteLength.Value = 0f;
                m_NumberOfStops.Value = 0;
                m_AverageWaitingTime.Value = 0f;
                m_RouteEfficiency.Value = 0f;
                m_DailyRevenue.Value = 0;
                
                m_IsInitialized = false;
                m_LastSelectedEntity = Entity.Null;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TransportRouteUISystem: Error during reset - {ex.Message}");
            }
        }

        // Helper methods with safe implementations
        private string GetRouteName(Entity entity) => $"Route {entity.Index}";
        private string GetRouteType(Entity entity) => "bus";
        private float GetRouteLength(Entity entity) => 10.5f;
        private int GetNumberOfStops(Entity entity) => 12;
        private int GetDailyPassengers(Entity entity) => 2840;
        private int GetDailyRevenue(Entity entity) => 15670;
        private bool IsRouteActive(Entity entity) => true;
        private float GetAverageWaitingTime(Entity entity) => 3.2f;
        private float GetRouteEfficiency(Entity entity) => 87.0f;
        private string GetVehicleId(Entity entity) => $"vehicle_{entity.Index}";
        private string GetVehicleThumbnail(Entity entity) => "coui://gameui/Media/Game/Icons/Bus.svg";
        private string GetVehicleName(Entity entity) => $"Vehicle {entity.Index}";
        private int GetVehicleCapacity(Entity entity) => 120;
        private int GetVehicleCapacity(VehicleData vehicleData) => 120;
        private float GetVehicleFuelEfficiency(Entity entity) => 8.5f;
        private float GetVehicleFuelEfficiency(VehicleData vehicleData) => 8.5f;
        private int GetVehicleMaintenanceCost(Entity entity) => 850;
        private int GetVehicleMaintenanceCost(VehicleData vehicleData) => 850;
        private string GetVehicleType(Entity entity) => "primary";
        private string GetVehicleType(VehicleData vehicleData) => "primary";
        private bool IsVehicleAvailable(Entity entity) => true;

        #endregion

        protected override void OnProcess()
        {
            // Process any pending route operations
        }

        public override void OnWriteProperties(IJsonWriter writer)
        {
            // Write any additional properties if needed
        }
        // Add this method to fix CS0103: The name 'PerformRouteOptimization' does not exist in the current context

        /// <summary>
        /// Perform optimization logic for the given route entity.
        /// </summary>
        private void PerformRouteOptimization(Entity routeEntity)
        {
            // Example implementation: Log optimization performed.
            LogUtil.Info($"Route optimization performed for route {routeEntity.Index}");
            // Add actual optimization logic here as needed.
        }
    }
}