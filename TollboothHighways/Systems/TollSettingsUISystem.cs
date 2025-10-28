using Colossal.UI.Binding;
using Game.UI;
using Unity.Entities;
using Game.Simulation;
using TollboothHighways.Utilities;

namespace TollboothHighways.Systems
{
    public partial class TollSettingsUISystem : UISystemBase
    {
        private SimulationSystem m_SimulationSystem;
        private ValueBinding<object> m_ModSettingsBinding;
        private ValueBinding<bool> m_IsPeakHoursBinding;
        private ValueBinding<float> m_CurrentHourBinding;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            
            // Create bindings for mod settings
            m_ModSettingsBinding = new ValueBinding<object>(Mod.Id, "modSettings", GetModSettingsObject());
            m_IsPeakHoursBinding = new ValueBinding<bool>(Mod.Id, "isPeakHours", false);
            m_CurrentHourBinding = new ValueBinding<float>(Mod.Id, "currentHour", 0f);
            
            AddBinding(m_ModSettingsBinding);
            AddBinding(m_IsPeakHoursBinding);
            AddBinding(m_CurrentHourBinding);
        }

        protected override void OnUpdate()
        {
            // Update peak hours status
            bool isPeakHours = IsPeakHours(m_SimulationSystem.frameIndex);
            float currentHour = GetCurrentHour(m_SimulationSystem.frameIndex);
            
            m_IsPeakHoursBinding.Update(isPeakHours);
            m_CurrentHourBinding.Update(currentHour);
            
            // Update mod settings if they changed
            if (ModSettings.Instance != null)
            {
                m_ModSettingsBinding.Update(GetModSettingsObject());
            }
        }
        
        private object GetModSettingsObject()
        {
            var settings = ModSettings.Instance;
            if (settings == null) return null;
            
            // Create anonymous object with camelCase properties for JavaScript
            return new
            {
                // Private Transport
                motorcyclePeakPrice = settings.MotorcyclePeakPrice,
                motorcycleNonPeakPrice = settings.MotorcycleNonPeakPrice,
                privateCarPeakPrice = settings.PrivateCarPeakPrice,
                privateCarNonPeakPrice = settings.PrivateCarNonPeakPrice,
                privateCarWithTrailerPeakPrice = settings.PrivateCarWithTrailerPeakPrice,
                privateCarWithTrailerNonPeakPrice = settings.PrivateCarWithTrailerNonPeakPrice,
                
                // Trucks
                truckPeakPrice = settings.TruckPeakPrice,
                truckNonPeakPrice = settings.TruckNonPeakPrice,
                truckWithTrailerPeakPrice = settings.TruckWithTrailerPeakPrice,
                truckWithTrailerNonPeakPrice = settings.TruckWithTrailerNonPeakPrice,
                
                // Public Transport
                busPeakPrice = settings.BusPeakPrice,
                busNonPeakPrice = settings.BusNonPeakPrice,
                taxiPeakPrice = settings.TaxiPeakPrice,
                taxiNonPeakPrice = settings.TaxiNonPeakPrice,
                
                // Service Vehicles
                parkMaintenancePeakPrice = settings.ParkMaintenancePeakPrice,
                parkMaintenanceNonPeakPrice = settings.ParkMaintenanceNonPeakPrice,
                roadMaintenancePeakPrice = settings.RoadMaintenancePeakPrice,
                roadMaintenanceNonPeakPrice = settings.RoadMaintenanceNonPeakPrice,
                ambulancePeakPrice = settings.AmbulancePeakPrice,
                ambulanceNonPeakPrice = settings.AmbulanceNonPeakPrice,
                evacuatingTransportPeakPrice = settings.EvacuatingTransportPeakPrice,
                evacuatingTransportNonPeakPrice = settings.EvacuatingTransportNonPeakPrice,
                fireEnginePeakPrice = settings.FireEnginePeakPrice,
                fireEngineNonPeakPrice = settings.FireEngineNonPeakPrice,
                garbageTruckPeakPrice = settings.GarbageTruckPeakPrice,
                garbageTruckNonPeakPrice = settings.GarbageTruckNonPeakPrice,
                hearsePeakPrice = settings.HearsePeakPrice,
                hearseNonPeakPrice = settings.HearseNonPeakPrice,
                policeCarPeakPrice = settings.PoliceCarPeakPrice,
                policeCarNonPeakPrice = settings.PoliceCarNonPeakPrice,
                postVanPeakPrice = settings.PostVanPeakPrice,
                postVanNonPeakPrice = settings.PostVanNonPeakPrice,
                prisonerTransportPeakPrice = settings.PrisonerTransportPeakPrice,
                prisonerTransportNonPeakPrice = settings.PrisonerTransportNonPeakPrice,
                
                // Charge settings
                chargeServiceVehicles = settings.ChargeServiceVehicles,
                chargePublicVehicles = settings.ChargePublicVehicles
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
    }
}