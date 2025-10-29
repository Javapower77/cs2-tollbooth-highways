using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;
using Game.City;
using Game.Simulation;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using TollboothHighways.Extensions;
using Game;
using Unity.Collections;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System to display toll income in the economy panel as a separate income source
    /// </summary>
    public partial class TollEconomyUISystem : UISystemBase
    {
        private SimulationSystem m_SimulationSystem;
        private CityStatisticsSystem m_CityStatisticsSystem;
        private EntityQuery m_CityQuery;
        private Entity m_TollStatisticsEntity = Entity.Null;
        
        // UI Bindings using ValueBindingHelper
        private ValueBindingHelper<float> m_TollIncomeDaily;
        private ValueBindingHelper<float> m_TollIncomeWeekly;
        private ValueBindingHelper<float> m_TollIncomeMonthly;
        private ValueBindingHelper<float> m_TollIncomeTotal;
        private ValueBindingHelper<int> m_TollVehiclesDaily;
        private ValueBindingHelper<int> m_TollVehiclesTotal;
        private ValueBindingHelper<bool> m_HasTollIncome;
        
        // Frame tracking
        private const uint FRAMES_PER_DAY = 25920;
        private const uint FRAMES_PER_WEEK = FRAMES_PER_DAY * 7;
        private const uint FRAMES_PER_MONTH = FRAMES_PER_DAY * 30;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            
            // Create city query - look for the city entity with PlayerMoney component
            m_CityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<City>(),
                    ComponentType.ReadOnly<PlayerMoney>()
                }
            });
            
            // Create UI bindings using ValueBindingHelper for cleaner API
            m_TollIncomeDaily = new ValueBindingHelper<float>(
                new ValueBinding<float>(Mod.Id, "tollIncomeDaily", 0f)
            );
            
            m_TollIncomeWeekly = new ValueBindingHelper<float>(
                new ValueBinding<float>(Mod.Id, "tollIncomeWeekly", 0f)
            );
            
            m_TollIncomeMonthly = new ValueBindingHelper<float>(
                new ValueBinding<float>(Mod.Id, "tollIncomeMonthly", 0f)
            );
            
            m_TollIncomeTotal = new ValueBindingHelper<float>(
                new ValueBinding<float>(Mod.Id, "tollIncomeTotal", 0f)
            );
            
            m_TollVehiclesDaily = new ValueBindingHelper<int>(
                new ValueBinding<int>(Mod.Id, "tollVehiclesDaily", 0)
            );
            
            m_TollVehiclesTotal = new ValueBindingHelper<int>(
                new ValueBinding<int>(Mod.Id, "tollVehiclesTotal", 0)
            );
            
            m_HasTollIncome = new ValueBindingHelper<bool>(
                new ValueBinding<bool>(Mod.Id, "hasTollIncome", false)
            );
            
            // Register bindings - access the underlying ValueBinding through the Binding property
            AddBinding(m_TollIncomeDaily.Binding);
            AddBinding(m_TollIncomeWeekly.Binding);
            AddBinding(m_TollIncomeMonthly.Binding);
            AddBinding(m_TollIncomeTotal.Binding);
            AddBinding(m_TollVehiclesDaily.Binding);
            AddBinding(m_TollVehiclesTotal.Binding);
            AddBinding(m_HasTollIncome.Binding);
            
            LogUtil.Info("TollEconomyUISystem: Created with toll income bindings using ValueBindingHelper");
        }

        protected override void OnUpdate()
        {
            // Get or create the toll statistics entity
            Entity tollStatsEntity = GetOrCreateTollStatisticsEntity();
            
            if (tollStatsEntity == Entity.Null)
            {
                LogUtil.Warn("TollEconomyUISystem: Unable to create toll statistics entity");
                return;
            }
            
            var tollStats = EntityManager.GetComponentData<TollIncomeStatistics>(tollStatsEntity);
            uint currentFrame = m_SimulationSystem.frameIndex;
            
            // Update period-based statistics
            UpdatePeriodStatistics(ref tollStats, currentFrame);
            
            // Update UI bindings using ValueBindingHelper's Value property
            m_TollIncomeDaily.Value = tollStats.DailyIncome;
            m_TollIncomeWeekly.Value = tollStats.WeeklyIncome;
            m_TollIncomeMonthly.Value = tollStats.MonthlyIncome;
            m_TollIncomeTotal.Value = tollStats.TotalIncome;
            m_TollVehiclesDaily.Value = tollStats.DailyVehicleCount;
            m_TollVehiclesTotal.Value = tollStats.TotalVehicleCount;
            m_HasTollIncome.Value = tollStats.TotalIncome > 0;
            
            // Save updated statistics
            EntityManager.SetComponentData(tollStatsEntity, tollStats);
        }
        
        private Entity GetOrCreateTollStatisticsEntity()
        {
            // Check if we already have a toll statistics entity
            if (m_TollStatisticsEntity != Entity.Null && EntityManager.Exists(m_TollStatisticsEntity))
            {
                return m_TollStatisticsEntity;
            }
            
            // Try to find an existing toll statistics entity
            var query = GetEntityQuery(ComponentType.ReadOnly<TollIncomeStatistics>());
            var entities = query.ToEntityArray(Allocator.Temp);
            
            if (entities.Length > 0)
            {
                m_TollStatisticsEntity = entities[0];
                entities.Dispose();
                return m_TollStatisticsEntity;
            }
            
            entities.Dispose();
            
            // Create a new entity for toll statistics
            m_TollStatisticsEntity = EntityManager.CreateEntity(
                ComponentType.ReadWrite<TollIncomeStatistics>()
            );
            
            // Initialize with default values
            EntityManager.SetComponentData(m_TollStatisticsEntity, new TollIncomeStatistics
            {
                LastDayUpdate = m_SimulationSystem.frameIndex,
                LastWeekUpdate = m_SimulationSystem.frameIndex,
                LastMonthUpdate = m_SimulationSystem.frameIndex
            });
            
            LogUtil.Info($"TollEconomyUISystem: Created toll statistics entity {m_TollStatisticsEntity.Index}");
            return m_TollStatisticsEntity;
        }
        
        private void UpdatePeriodStatistics(ref TollIncomeStatistics stats, uint currentFrame)
        {
            uint currentDay = currentFrame / FRAMES_PER_DAY;
            uint currentWeek = currentFrame / FRAMES_PER_WEEK;
            uint currentMonth = currentFrame / FRAMES_PER_MONTH;
            
            // Reset daily statistics
            if (currentDay > stats.LastDayUpdate / FRAMES_PER_DAY)
            {
                LogUtil.Info($"TollEconomyUISystem: New day - resetting daily stats (was: ${stats.DailyIncome:F2})");
                stats.DailyIncome = 0;
                stats.DailyVehicleCount = 0;
                stats.LastDayUpdate = currentFrame;
            }
            
            // Reset weekly statistics
            if (currentWeek > stats.LastWeekUpdate / FRAMES_PER_WEEK)
            {
                LogUtil.Info($"TollEconomyUISystem: New week - resetting weekly stats (was: ${stats.WeeklyIncome:F2})");
                stats.WeeklyIncome = 0;
                stats.WeeklyVehicleCount = 0;
                stats.LastWeekUpdate = currentFrame;
            }
            
            // Reset monthly statistics
            if (currentMonth > stats.LastMonthUpdate / FRAMES_PER_MONTH)
            {
                LogUtil.Info($"TollEconomyUISystem: New month - resetting monthly stats (was: ${stats.MonthlyIncome:F2})");
                stats.MonthlyIncome = 0;
                stats.MonthlyVehicleCount = 0;
                stats.LastMonthUpdate = currentFrame;
            }
        }
        
        /// <summary>
        /// Called by TollCollectionSystem to add toll income
        /// </summary>
        public void AddTollIncome(float amount, int vehicleCount = 1)
        {
            Entity tollStatsEntity = GetOrCreateTollStatisticsEntity();
            
            if (tollStatsEntity == Entity.Null)
            {
                LogUtil.Warn("TollEconomyUISystem: Unable to add toll income - no statistics entity");
                return;
            }
            
            var tollStats = EntityManager.GetComponentData<TollIncomeStatistics>(tollStatsEntity);
            
            // Update all statistics
            tollStats.DailyIncome += amount;
            tollStats.WeeklyIncome += amount;
            tollStats.MonthlyIncome += amount;
            tollStats.TotalIncome += amount;
            
            tollStats.DailyVehicleCount += vehicleCount;
            tollStats.WeeklyVehicleCount += vehicleCount;
            tollStats.MonthlyVehicleCount += vehicleCount;
            tollStats.TotalVehicleCount += vehicleCount;
            
            EntityManager.SetComponentData(tollStatsEntity, tollStats);
            
            if (ModSettings.Instance?.EnableGeneralLogging == true)
            {
                LogUtil.Info($"TollEconomyUISystem: Added ${amount:F2} toll income (Daily: ${tollStats.DailyIncome:F2}, Total: ${tollStats.TotalIncome:F2})");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // Clean up the toll statistics entity when the system is destroyed
            if (m_TollStatisticsEntity != Entity.Null && EntityManager.Exists(m_TollStatisticsEntity))
            {
                EntityManager.DestroyEntity(m_TollStatisticsEntity);
                m_TollStatisticsEntity = Entity.Null;
            }
        }
    }
}