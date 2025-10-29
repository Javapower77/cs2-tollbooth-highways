using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Game.Common;
using Game.Net;
using Game.Vehicles;
using Game.Economy;
using Game.Simulation;
using Game.City;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using System.Collections.Generic;
using Game;
using Game.Tools;
using UnityEngine;
using Game.Objects;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors vehicles exiting toll roads (at Edge.m_End) and collects tolls.
    /// Runs on main thread for debugging and logging capabilities.
    /// Per AGENTS.MD: Updates TollBoothInsight and adds money to game economy.
    /// </summary>
    public partial class TollCollectionSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private EntityQuery m_VehicleQuery;
        private EntityQuery m_CityQuery;
        private SimulationSystem m_SimulationSystem;
        private VehiclesUtil m_VehiclesUtil;
        private HashSet<Entity> m_ProcessedVehicles;
        private Dictionary<Entity, uint> m_VehicleLastProcessedFrame;
        
        // Component lookups
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;
        private ComponentLookup<TollBoothPrefabData> m_TollBoothLookup;
        private ComponentLookup<TollBoothInsight> m_TollBoothInsightLookup;
        private ComponentLookup<Edge> m_EdgeLookup;
        private ComponentLookup<CarCurrentLane> m_CarCurrentLaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Lane> m_LaneLookup;
        private BufferLookup<LaneObject> m_LaneObjectLookup;
        private BufferLookup<SubLane> m_SubLaneLookup;
        
        // City system components - Fixed to use proper types
        private BufferLookup<CityStatistic> m_CityStatisticsLookup;
        private ComponentLookup<PlayerMoney> m_PlayerMoneyLookup;
        
        // Economy system references
        private CityStatisticsSystem m_CityStatisticsSystem;
        
        // Frame cooldown to prevent double-charging
        private const uint COOLDOWN_FRAMES = 300; // ~5 seconds at 60fps
        
        // Track daily toll income for statistics
        private float m_DailyTollIncome;
        private uint m_LastDayFrame;

        // Add reference to the toll economy UI system
        private TollEconomyUISystem m_TollEconomyUISystem;
        
        private bool m_LogInitialized = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();

            m_VehiclesUtil = new VehiclesUtil();
            m_ProcessedVehicles = new HashSet<Entity>();
            m_VehicleLastProcessedFrame = new Dictionary<Entity, uint>();

            m_DailyTollIncome = 0f;
            m_LastDayFrame = 0;

            // Create city query - properly for the city entity
            m_CityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    //ComponentType.ReadWrite<PlayerMoney>(),
                    ComponentType.ReadWrite<CityStatistic>()
                }
            });

            // Create queries
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TollRoadPrefabData>(),
                    ComponentType.ReadOnly<Edge>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Vehicle>(),
                    ComponentType.ReadOnly<CarCurrentLane>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Unspawned>()
                }
            });

            RequireForUpdate(m_TollRoadQuery);
            RequireForUpdate(m_VehicleQuery);

            // Get the toll economy UI system
            m_TollEconomyUISystem = World.GetOrCreateSystemManaged<TollEconomyUISystem>();

            LogUtil.Info("TollCollectionSystem: Created - monitors vehicles at road exits per AGENTS.MD");
        }

        protected override void OnUpdate()
        {
            // Update lookups
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_TollBoothLookup = GetComponentLookup<TollBoothPrefabData>(true);
            m_TollBoothInsightLookup = GetComponentLookup<TollBoothInsight>(false);
            m_EdgeLookup = GetComponentLookup<Edge>(true);
            m_CarCurrentLaneLookup = GetComponentLookup<CarCurrentLane>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_LaneLookup = GetComponentLookup<Lane>(true);
            m_LaneObjectLookup = GetBufferLookup<LaneObject>(true);
            m_SubLaneLookup = GetBufferLookup<SubLane>(true);
            
            // Fixed: Use BufferLookup for CityStatistic since it's IBufferElementData
            m_CityStatisticsLookup = GetBufferLookup<CityStatistic>(false);
            m_PlayerMoneyLookup = GetComponentLookup<PlayerMoney>(false);
            
            uint currentFrame = m_SimulationSystem.frameIndex;
            
            // Check for new day and update statistics
            UpdateDailyStatistics(currentFrame);
            
            // Clean up old processed vehicles
            CleanupProcessedVehicles(currentFrame);
            
            // Get all toll roads
            var tollRoads = m_TollRoadQuery.ToEntityArray(Allocator.Temp);
            
            // Process each toll road
            foreach (var roadEntity in tollRoads)
            {
                ProcessTollRoad(roadEntity, currentFrame);
            }
            
            tollRoads.Dispose();
        }

        private void ProcessTollRoad(Entity roadEntity, uint currentFrame)
        {
            if (!m_TollRoadLookup.TryGetComponent(roadEntity, out var tollRoadData))
                return;
                
            if (!tollRoadData.HasActiveTollbooth || tollRoadData.AssociatedTollbooth == Entity.Null)
                return;
                
            if (!m_EdgeLookup.TryGetComponent(roadEntity, out var edge))
                return;
            
            // Monitor vehicles at the m_End of the road (per requirement)
            MonitorVehiclesAtEndNode(roadEntity, edge.m_End, tollRoadData.AssociatedTollbooth, currentFrame);
        }

        private void MonitorVehiclesAtEndNode(Entity roadEntity, Entity endNode, Entity tollboothEntity, uint currentFrame)
        {
            // Get lanes connected to the end node
            if (!m_SubLaneLookup.TryGetBuffer(endNode, out var subLanes))
                return;
            
            // Check each lane for vehicles
            for (int i = 0; i < subLanes.Length; i++)
            {
                var laneEntity = subLanes[i].m_SubLane;
                
                if (!m_LaneObjectLookup.TryGetBuffer(laneEntity, out var laneObjects))
                    continue;
                    
                // Process vehicles in this lane
                for (int j = 0; j < laneObjects.Length; j++)
                {
                    var vehicleEntity = laneObjects[j].m_LaneObject;
                    
                    if (EntityManager.HasComponent<Vehicle>(vehicleEntity))
                    {
                        ProcessVehicleForToll(vehicleEntity, roadEntity, tollboothEntity, currentFrame);
                    }
                }
            }
        }

        private void ProcessVehicleForToll(Entity vehicleEntity, Entity roadEntity, Entity tollboothEntity, uint currentFrame)
        {
            // Check if vehicle was recently processed (cooldown)
            if (m_VehicleLastProcessedFrame.TryGetValue(vehicleEntity, out uint lastFrame))
            {
                if (currentFrame - lastFrame < COOLDOWN_FRAMES)
                    return;
            }
            
            // Get vehicle type
            var vehicleType = m_VehiclesUtil.GetVehicleType(vehicleEntity, EntityManager);
            
            if (vehicleType == VehicleType.None)
            {
                if (ModSettings.Instance?.EnableVehicleLogging == true)
                    VehicleDebugLogger.Log(vehicleEntity, $"TollCollection: Unable to determine vehicle type");
                return;
            }
            
            // Check if this vehicle type should be charged
            if (!ShouldChargeVehicle(vehicleType))
            {
                if (ModSettings.Instance?.EnableVehicleLogging == true)
                    VehicleDebugLogger.Log(vehicleEntity, $"TollCollection: {vehicleType} exempt from toll charges");
                return;
            }
            
            // Calculate toll amount based on settings and time
            float tollAmount = CalculateTollAmount(vehicleType, currentFrame);
            
            if (tollAmount <= 0)
            {
                if (ModSettings.Instance?.EnableVehicleLogging == true)
                    VehicleDebugLogger.Log(vehicleEntity, $"TollCollection: {vehicleType} toll amount is 0");
                return;
            }
            
            // Collect the toll
            CollectToll(vehicleEntity, tollboothEntity, vehicleType, tollAmount, currentFrame);
            
            // Update last processed frame
            m_VehicleLastProcessedFrame[vehicleEntity] = currentFrame;
        }

        private bool ShouldChargeVehicle(VehicleType vehicleType)
        {
            var settings = ModSettings.Instance;
            if (settings == null) return false;
            
            var vehicleGroup = VehiclesUtil.GetVehicleGroupBurstCompatible(vehicleType);
            
            switch (vehicleGroup)
            {
                case VehicleGroup.ServiceVehicles:
                    return settings.ChargeServiceVehicles;
                case VehicleGroup.PublicTransport:
                    return settings.ChargePublicVehicles;
                default:
                    return true; // Always charge private transport and trucks
            }
        }

        private float CalculateTollAmount(VehicleType vehicleType, uint currentFrame)
        {
            var settings = ModSettings.Instance;
            if (settings == null) return 0f;
            
            // Determine if peak hours (simplified - you may want to use TollboothTimeManager)
            bool isPeakHours = IsPeakHours(currentFrame);
            
            // Get toll amount based on vehicle type and time
            return vehicleType switch
            {
                VehicleType.Motorcycle => isPeakHours ? settings.MotorcyclePeakPrice : settings.MotorcycleNonPeakPrice,
                VehicleType.PersonalCar => isPeakHours ? settings.PrivateCarPeakPrice : settings.PrivateCarNonPeakPrice,
                VehicleType.PersonalCarWithTrailer => isPeakHours ? settings.PrivateCarWithTrailerPeakPrice : settings.PrivateCarWithTrailerNonPeakPrice,
                VehicleType.Truck => isPeakHours ? settings.TruckPeakPrice : settings.TruckNonPeakPrice,
                VehicleType.TruckWithTrailer => isPeakHours ? settings.TruckWithTrailerPeakPrice : settings.TruckWithTrailerNonPeakPrice,
                VehicleType.Bus => isPeakHours ? settings.BusPeakPrice : settings.BusNonPeakPrice,
                VehicleType.Taxi => isPeakHours ? settings.TaxiPeakPrice : settings.TaxiNonPeakPrice,
                VehicleType.ParkMaintenance => isPeakHours ? settings.ParkMaintenancePeakPrice : settings.ParkMaintenanceNonPeakPrice,
                VehicleType.RoadMaintenance => isPeakHours ? settings.RoadMaintenancePeakPrice : settings.RoadMaintenanceNonPeakPrice,
                VehicleType.Ambulance => isPeakHours ? settings.AmbulancePeakPrice : settings.AmbulanceNonPeakPrice,
                VehicleType.EvacuatingTransport => isPeakHours ? settings.EvacuatingTransportPeakPrice : settings.EvacuatingTransportNonPeakPrice,
                VehicleType.FireEngine => isPeakHours ? settings.FireEnginePeakPrice : settings.FireEngineNonPeakPrice,
                VehicleType.GarbageTruck => isPeakHours ? settings.GarbageTruckPeakPrice : settings.GarbageTruckNonPeakPrice,
                VehicleType.Hearse => isPeakHours ? settings.HearsePeakPrice : settings.HearseNonPeakPrice,
                VehicleType.PoliceCar => isPeakHours ? settings.PoliceCarPeakPrice : settings.PoliceCarNonPeakPrice,
                VehicleType.PostVan => isPeakHours ? settings.PostVanPeakPrice : settings.PostVanNonPeakPrice,
                VehicleType.PrisonerTransport => isPeakHours ? settings.PrisonerTransportPeakPrice : settings.PrisonerTransportNonPeakPrice,
                _ => 0f
            };
        }

        private bool IsPeakHours(uint currentFrame)
        {
            // Simple peak hours calculation (7-9 AM and 5-7 PM)
            // You may want to use TollboothTimeManager for more accurate time calculation
            float hourOfDay = (currentFrame % 25920f) / 1080f; // 25920 frames per day, 1080 frames per hour
            return (hourOfDay >= 7 && hourOfDay < 9) || (hourOfDay >= 17 && hourOfDay < 19);
        }

        private void CollectToll(Entity vehicleEntity, Entity tollboothEntity, VehicleType vehicleType, float tollAmount, uint currentFrame)
        {
            // Add money to the city's treasury properly
            AddMoneyToCity(tollAmount);
            
            // Track toll income in the dedicated economy UI system
            m_TollEconomyUISystem?.AddTollIncome(tollAmount, 1);
            
            // Track daily income
            m_DailyTollIncome += tollAmount;
            
            // Update TollBoothInsight
            if (m_TollBoothInsightLookup.TryGetComponent(tollboothEntity, out var insight))
            {
                insight.AddVehiclePassage(vehicleType, tollAmount, currentFrame);
                m_TollBoothInsightLookup[tollboothEntity] = insight;
                
                if (ModSettings.Instance?.EnableGeneralLogging == true)
                {
                    LogUtil.Info($"TollCollection: Collected ${tollAmount:F2} from {vehicleType} at tollbooth {tollboothEntity.Index}");
                    LogUtil.Info($"TollCollection: Total revenue: ${insight.TotalRevenue:F2}, Total vehicles: {insight.TotalVehiclesPassed}");
                }
                
                if (ModSettings.Instance?.EnableVehicleLogging == true)
                {
                    VehicleDebugLogger.Log(vehicleEntity, 
                        $"TollCollection: Paid ${tollAmount:F2} toll as {vehicleType} at tollbooth {tollboothEntity.Index}");
                }
            }
            else
            {
                LogUtil.Warn($"TollCollection: TollBoothInsight component not found for tollbooth {tollboothEntity.Index}");
            }
        }
        
        private void AddMoneyToCity(float amount)
        {
            // Get the city entity from the query
            var cityEntities = m_CityQuery.ToEntityArray(Allocator.Temp);
            if (cityEntities.Length == 0)
            {
                LogUtil.Warn("TollCollection: Unable to find city entity for adding money");
                cityEntities.Dispose();
                return;
            }
            
            Entity cityEntity = cityEntities[0];
            cityEntities.Dispose();
            
            // Add money to PlayerMoney component
            if (m_PlayerMoneyLookup.TryGetComponent(cityEntity, out var playerMoney))
            {
                int moneyAmount = Mathf.RoundToInt(amount);
                playerMoney.Add(moneyAmount);
                m_PlayerMoneyLookup[cityEntity] = playerMoney;
                
                // Update city statistics buffer for proper UI display
                if (m_CityStatisticsLookup.TryGetBuffer(cityEntity, out var cityStatsBuffer))
                {
                    // The CityStatistic buffer contains various statistics
                    // We need to find the income statistic index (typically index 12 for income)
                    // This corresponds to StatisticType.Income in the game's enum
                    const int INCOME_STAT_INDEX = 12;
                    
                    if (cityStatsBuffer.Length > INCOME_STAT_INDEX)
                    {
                        var incomeStat = cityStatsBuffer[INCOME_STAT_INDEX];
                        incomeStat.m_Value += moneyAmount;
                        incomeStat.m_TotalValue += moneyAmount;
                        cityStatsBuffer[INCOME_STAT_INDEX] = incomeStat;
                    }
                }
                
                if (ModSettings.Instance?.EnableGeneralLogging == true)
                {
                    LogUtil.Info($"TollCollection: Added ${moneyAmount} to city treasury (Total: ${playerMoney.money})");
                }
            }
        }
        
        private void UpdateDailyStatistics(uint currentFrame)
        {
            // Check if a day has passed (25920 frames per day at default speed)
            const uint FRAMES_PER_DAY = 25920;
            uint currentDay = currentFrame / FRAMES_PER_DAY;
            uint lastDay = m_LastDayFrame / FRAMES_PER_DAY;
            
            if (currentDay > lastDay)
            {
                // Report daily income to statistics
                if (m_DailyTollIncome > 0)
                {
                    LogUtil.Info($"TollCollection: Daily toll income: ${m_DailyTollIncome:F2}");
                    
                    // You can add custom statistics tracking here if needed
                    // For now, the income is already added to the city's general income
                }
                
                // Reset daily counter
                m_DailyTollIncome = 0f;
                m_LastDayFrame = currentFrame;
            }
        }

        private void CleanupProcessedVehicles(uint currentFrame)
        {
            // Remove vehicles that have been in the dictionary for more than cooldown period
            var vehiclesToRemove = new List<Entity>();

            foreach (var kvp in m_VehicleLastProcessedFrame)
            {
                if (currentFrame - kvp.Value > COOLDOWN_FRAMES * 2 || !EntityManager.Exists(kvp.Key))
                {
                    vehiclesToRemove.Add(kvp.Key);
                }
            }

            foreach (var vehicle in vehiclesToRemove)
            {
                m_VehicleLastProcessedFrame.Remove(vehicle);
            }
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
                VehicleDebugLogger.LogOnce("=== TollboothPathMonitoringSystem logging started ===");
            }
            catch
            {
                // best-effort logging initialization
            }

            m_LogInitialized = true;
        }
    }
}