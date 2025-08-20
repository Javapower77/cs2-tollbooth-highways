using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using TollboothHighways.Domain.Components;
using TollboothHighways.Extensions;
using TollboothHighways.Utilities;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    public partial class TollBoothInfoUISystem : ExtendedInfoSectionBase
    {
        private ToolSystem m_ToolSystem;
        private ValueBindingHelper<bool> m_IsPanelVisible;
        private ValueBindingHelper<string> m_TollAmount;
        private ValueBindingHelper<string> m_TotalIncome;
        private ValueBindingHelper<string> m_PanelIcon;
        private ValueBindingHelper<object> m_TollBoothInsight;

        protected override string group => Mod.Id;
        
        public new bool visible => selectedEntity != Entity.Null && EntityManager.HasComponent<TollBoothPrefabData>(selectedEntity);

        /// <inheritdoc/>
        public override void OnWriteProperties(IJsonWriter writer)
        {
        }

        /// <inheritdoc/>
        protected override void OnProcess()
        {
        }

        /// <inheritdoc/>
        protected override void Reset()
        {
        }

        
        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_IsPanelVisible = CreateBinding("m_IsPanelVisible", false);
            m_TollAmount = CreateBinding("m_TollAmount", "$0.00");
            m_TotalIncome = CreateBinding("m_TotalIncome", "$0.00");
            m_PanelIcon = CreateBinding("m_PanelIcon", "");                             
            m_TollBoothInsight = CreateBinding("m_TollBoothInsight", new object());

            m_InfoUISystem.AddMiddleSection(this);

            LogUtil.Info("ToolboothInfoUISystem created and bindings initialized.");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (!Enabled)
            {
                this.Enabled = true;
            }
                
            Entity selectedEntity = m_ToolSystem.selected;

            if (selectedEntity != Entity.Null && EntityManager.HasComponent<TollBoothPrefabData>(selectedEntity))
            {
                if (!m_IsPanelVisible.Value)
                {
                    LogUtil.Info("ToolboothInfoUISystem: Showing tollbooth panel");
                    m_IsPanelVisible.Value = true;
                    base.visible = true;
                 }
                UpdatePanelData(selectedEntity);
            }
            else
            {
                if (m_IsPanelVisible.Value)
                {
                    LogUtil.Info("ToolboothInfoUISystem: Hiding tollbooth panel");
                    m_IsPanelVisible.Value = false;
                    base.visible = false;
                }
            }            
        }

        private void UpdatePanelData(Entity entity)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
                return;

            if (EntityManager.TryGetComponent<TollBoothPrefabData>(entity, out var data))
            {                
                m_TollAmount.Value = "$234.00";
                m_TotalIncome.Value = "$1,234.56";
        
                // Add tollbooth insight data
                if (EntityManager.TryGetComponent<TollBoothInsight>(entity, out var insight))
                {
                    var insightData = new
                    {
                        totalVehiclesPassed = insight.TotalVehiclesPassed,
                        totalRevenue = insight.TotalRevenue,
                        personalCarCount = insight.PersonalCarCount,
                        personalCarWithTrailerCount = insight.PersonalCarWithTrailerCount,
                        truckCount = insight.TruckCount,
                        truckWithTrailerCount = insight.TruckWithTrailerCount,
                        busCount = insight.BusCount,
                        taxiCount = insight.TaxiCount,
                        parkMaintenanceCount = insight.ParkMaintenanceCount,
                        roadMaintenanceCount = insight.RoadMaintenanceCount,
                        ambulanceCount = insight.AmbulanceCount,
                        evacuatingTransportCount = insight.EvacuatingTransportCount,
                        fireEngineCount = insight.FireEngineCount,
                        garbageTruckCount = insight.GarbageTruckCount,
                        hearseCount = insight.HearseCount,
                        policeCarCount = insight.PoliceCarCount,
                        postVanCount = insight.PostVanCount,
                        prisonerTransportCount = insight.PrisonerTransportCount,
                        motorcycleCount = insight.MotorcycleCount
                    };
            
                    m_TollBoothInsight.Value = insightData;
                }
            }
        }
    }
}