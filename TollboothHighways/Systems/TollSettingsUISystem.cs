using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;
using Game.Simulation;
using TollboothHighways.Utilities;
using TollboothHighways.Extensions;
using System;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// UI System for managing tollbooth mod settings display and updates
    /// </summary>
    public partial class TollSettingsUISystem : UISystemBase
    {
        private SimulationSystem m_SimulationSystem;
        
        // Use ValueBindingHelper instead of GetterValueBinding
        private ValueBindingHelper<TollSettingsData> m_ModSettingsBinding;
        private ValueBindingHelper<bool> m_IsPeakHoursBinding;
        private ValueBindingHelper<float> m_CurrentHourBinding;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            
            // Create bindings using ValueBindingHelper
            m_ModSettingsBinding = new ValueBindingHelper<TollSettingsData>(
                new ValueBinding<TollSettingsData>(
                    Mod.Id, 
                    "modSettings", 
                    GetModSettingsData(),
                    new GenericUIWriter<TollSettingsData>()
                )
            );
            
            m_IsPeakHoursBinding = new ValueBindingHelper<bool>(
                new ValueBinding<bool>(Mod.Id, "isPeakHours", false)
            );
            
            m_CurrentHourBinding = new ValueBindingHelper<float>(
                new ValueBinding<float>(Mod.Id, "currentHour", 0f)
            );
            
            // Add the bindings
            AddBinding(m_ModSettingsBinding.Binding);
            AddBinding(m_IsPeakHoursBinding.Binding);
            AddBinding(m_CurrentHourBinding.Binding);

            LogUtil.Info("TollSettingsUISystem: OnCreate() - Created with toll settings bindings");
            LogUtil.Info($"TollSettingsUISystem: OnCreate() - isPeakHours: {m_IsPeakHoursBinding.Value}, currentHour: {m_CurrentHourBinding.Value}");
            LogUtil.Info($"TollSettingsUISystem: OnCreate() - m_ModSettingsBinding: {m_ModSettingsBinding.Value}");            
        }

        protected override void OnUpdate()
        {
            // Calculate current values
            bool isPeakHours = IsPeakHours(m_SimulationSystem.frameIndex);
            float currentHour = GetCurrentHour(m_SimulationSystem.frameIndex);
            
            // Update bindings using ValueBindingHelper
            m_IsPeakHoursBinding.Value = isPeakHours;
            m_CurrentHourBinding.Value = currentHour;

            // Update mod settings data
            m_ModSettingsBinding.Value = GetModSettingsData();

            //LogUtil.Info($"TollSettingsUISystem: OnUpdate() - isPeakHours: {isPeakHours}, currentHour: {currentHour}");
            //LogUtil.Info($"TollSettingsUISystem: OnUpdate() - m_ModSettingsBinding: {m_ModSettingsBinding.Value}");
        }
        
        private TollSettingsData GetModSettingsData()
        {
            var settings = Mod.Settings;
            if (settings == null) 
            {
                LogUtil.Info("TollSettingsUISystem: GetModSettingsData() - ModSettings is null, returning default values");
                return new TollSettingsData();
            }
            
            // Return strongly typed data structure
            return new TollSettingsData
            {
                // Private Transport
                MotorcyclePeakPrice = settings.MotorcyclePeakPrice,
                MotorcycleNonPeakPrice = settings.MotorcycleNonPeakPrice,
                PrivateCarPeakPrice = settings.PrivateCarPeakPrice,
                PrivateCarNonPeakPrice = settings.PrivateCarNonPeakPrice,
                PrivateCarWithTrailerPeakPrice = settings.PrivateCarWithTrailerPeakPrice,
                PrivateCarWithTrailerNonPeakPrice = settings.PrivateCarWithTrailerNonPeakPrice,
                
                // Trucks
                TruckPeakPrice = settings.TruckPeakPrice,
                TruckNonPeakPrice = settings.TruckNonPeakPrice,
                TruckWithTrailerPeakPrice = settings.TruckWithTrailerPeakPrice,
                TruckWithTrailerNonPeakPrice = settings.TruckWithTrailerNonPeakPrice,
                
                // Public Transport
                BusPeakPrice = settings.BusPeakPrice,
                BusNonPeakPrice = settings.BusNonPeakPrice,
                TaxiPeakPrice = settings.TaxiPeakPrice,
                TaxiNonPeakPrice = settings.TaxiNonPeakPrice,
                
                // Service Vehicles
                ParkMaintenancePeakPrice = settings.ParkMaintenancePeakPrice,
                ParkMaintenanceNonPeakPrice = settings.ParkMaintenanceNonPeakPrice,
                RoadMaintenancePeakPrice = settings.RoadMaintenancePeakPrice,
                RoadMaintenanceNonPeakPrice = settings.RoadMaintenanceNonPeakPrice,
                AmbulancePeakPrice = settings.AmbulancePeakPrice,
                AmbulanceNonPeakPrice = settings.AmbulanceNonPeakPrice,
                EvacuatingTransportPeakPrice = settings.EvacuatingTransportPeakPrice,
                EvacuatingTransportNonPeakPrice = settings.EvacuatingTransportNonPeakPrice,
                FireEnginePeakPrice = settings.FireEnginePeakPrice,
                FireEngineNonPeakPrice = settings.FireEngineNonPeakPrice,
                GarbageTruckPeakPrice = settings.GarbageTruckPeakPrice,
                GarbageTruckNonPeakPrice = settings.GarbageTruckNonPeakPrice,
                HearsePeakPrice = settings.HearsePeakPrice,
                HearseNonPeakPrice = settings.HearseNonPeakPrice,
                PoliceCarPeakPrice = settings.PoliceCarPeakPrice,
                PoliceCarNonPeakPrice = settings.PoliceCarNonPeakPrice,
                PostVanPeakPrice = settings.PostVanPeakPrice,
                PostVanNonPeakPrice = settings.PostVanNonPeakPrice,
                PrisonerTransportPeakPrice = settings.PrisonerTransportPeakPrice,
                PrisonerTransportNonPeakPrice = settings.PrisonerTransportNonPeakPrice,
                
                // Charge settings
                ChargeServiceVehicles = settings.ChargeServiceVehicles,
                ChargePublicVehicles = settings.ChargePublicVehicles
            };
        }
        
        private bool IsPeakHours(uint currentFrame)
        {
            float hourOfDay = GetCurrentHour(currentFrame);
            return (hourOfDay >= 7 && hourOfDay < 9) || (hourOfDay >= 17 && hourOfDay < 19);
        }
        
        private float GetCurrentHour(uint currentFrame)
        {
            const float FRAMES_PER_DAY = 25920f;
            const float FRAMES_PER_HOUR = 1080f;
            return (currentFrame % FRAMES_PER_DAY) / FRAMES_PER_HOUR;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            LogUtil.Info("TollSettingsUISystem: OnDestroy() - Destroyed");
        }
    }
    
    /// <summary>
    /// Strongly typed data structure for toll settings to be sent to UI
    /// Public properties with JSON-friendly naming
    /// </summary>
    [Serializable]
    public class TollSettingsData
    {
        // Private Transport
        public float MotorcyclePeakPrice { get; set; }
        public float MotorcycleNonPeakPrice { get; set; }
        public float PrivateCarPeakPrice { get; set; }
        public float PrivateCarNonPeakPrice { get; set; }
        public float PrivateCarWithTrailerPeakPrice { get; set; }
        public float PrivateCarWithTrailerNonPeakPrice { get; set; }
        
        // Trucks
        public float TruckPeakPrice { get; set; }
        public float TruckNonPeakPrice { get; set; }
        public float TruckWithTrailerPeakPrice { get; set; }
        public float TruckWithTrailerNonPeakPrice { get; set; }
        
        // Public Transport
        public float BusPeakPrice { get; set; }
        public float BusNonPeakPrice { get; set; }
        public float TaxiPeakPrice { get; set; }
        public float TaxiNonPeakPrice { get; set; }
        
        // Service Vehicles
        public float ParkMaintenancePeakPrice { get; set; }
        public float ParkMaintenanceNonPeakPrice { get; set; }
        public float RoadMaintenancePeakPrice { get; set; }
        public float RoadMaintenanceNonPeakPrice { get; set; }
        public float AmbulancePeakPrice { get; set; }
        public float AmbulanceNonPeakPrice { get; set; }
        public float EvacuatingTransportPeakPrice { get; set; }
        public float EvacuatingTransportNonPeakPrice { get; set; }
        public float FireEnginePeakPrice { get; set; }
        public float FireEngineNonPeakPrice { get; set; }
        public float GarbageTruckPeakPrice { get; set; }
        public float GarbageTruckNonPeakPrice { get; set; }
        public float HearsePeakPrice { get; set; }
        public float HearseNonPeakPrice { get; set; }
        public float PoliceCarPeakPrice { get; set; }
        public float PoliceCarNonPeakPrice { get; set; }
        public float PostVanPeakPrice { get; set; }
        public float PostVanNonPeakPrice { get; set; }
        public float PrisonerTransportPeakPrice { get; set; }
        public float PrisonerTransportNonPeakPrice { get; set; }
        
        // Charge settings
        public bool ChargeServiceVehicles { get; set; }
        public bool ChargePublicVehicles { get; set; }
        
        // Constructor with default values
        public TollSettingsData()
        {
            // Set default values matching your ModSettings defaults
            MotorcyclePeakPrice = 1.0f;
            MotorcycleNonPeakPrice = 0.5f;
            PrivateCarPeakPrice = 2.0f;
            PrivateCarNonPeakPrice = 1.0f;
            PrivateCarWithTrailerPeakPrice = 3.0f;
            PrivateCarWithTrailerNonPeakPrice = 1.5f;
            
            TruckPeakPrice = 5.0f;
            TruckNonPeakPrice = 3.0f;
            TruckWithTrailerPeakPrice = 7.0f;
            TruckWithTrailerNonPeakPrice = 4.0f;
            
            BusPeakPrice = 3.0f;
            BusNonPeakPrice = 2.0f;
            TaxiPeakPrice = 2.0f;
            TaxiNonPeakPrice = 1.0f;
            
            // Service vehicles default to 0 (free)
            ParkMaintenancePeakPrice = 0.0f;
            ParkMaintenanceNonPeakPrice = 0.0f;
            RoadMaintenancePeakPrice = 0.0f;
            RoadMaintenanceNonPeakPrice = 0.0f;
            AmbulancePeakPrice = 0.0f;
            AmbulanceNonPeakPrice = 0.0f;
            EvacuatingTransportPeakPrice = 0.0f;
            EvacuatingTransportNonPeakPrice = 0.0f;
            FireEnginePeakPrice = 0.0f;
            FireEngineNonPeakPrice = 0.0f;
            GarbageTruckPeakPrice = 0.0f;
            GarbageTruckNonPeakPrice = 0.0f;
            HearsePeakPrice = 0.0f;
            HearseNonPeakPrice = 0.0f;
            PoliceCarPeakPrice = 0.0f;
            PoliceCarNonPeakPrice = 0.0f;
            PostVanPeakPrice = 0.0f;
            PostVanNonPeakPrice = 0.0f;
            PrisonerTransportPeakPrice = 0.0f;
            PrisonerTransportNonPeakPrice = 0.0f;
            
            ChargeServiceVehicles = false;
            ChargePublicVehicles = true;
        }
    }
}