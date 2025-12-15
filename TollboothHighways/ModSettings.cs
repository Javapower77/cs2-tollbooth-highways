using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Input;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Settings;
using Game.UI;
using Game.UI.InGame;
using Game.UI.Widgets;
using Unity.Entities;
using UnityEngine.Device;

namespace TollboothHighways
{
    [FileLocation("ModsSettings/" + nameof(TollboothHighways))]
    [SettingsUITabOrder(GeneralTab, NonPeakTab, PeakTab, AboutTab, CreditsTab)]
    [SettingsUIGroupOrder(AboutSection)]
    [SettingsUIShowGroupName(AboutSection, PublicTransportNonPeakSection, PublicTransportPeakSection, PrivateTransportPeakSection, PrivateTransportNonPeakSection,
        ServiceVehiclesPeakSection, ServiceVehicleNonPeakSection, TruckNonPeakSection, TruckPeakSection, CreditsSection)]
    public partial class ModSettings : ModSetting
    {
        internal const string SETTINGS_ASSET_NAME = "Tollbooth Highways General Settings";
        internal static ModSettings Instance { get; private set; }

        // TABs from the Settings UI
        internal const string AboutTab = "About";
        internal const string GeneralTab = "General";
        internal const string CreditsTab = "Credits";
        internal const string PeakTab = "Peak Hours";
        internal const string NonPeakTab = "Non-Peak Hours";

        internal const string CreditsSection = "Credits";
        [SettingsUISection(CreditsTab, CreditsSection)]
        [SettingsUIMultilineText("coui://javapower-tollbooth-highways/Happy.svg")]
        public string SpecialThanks => string.Empty;

       


        internal const string ChargeServicePublicSection = "General Settings";
        [SettingsUISection(GeneralTab, ChargeServicePublicSection)]
        public bool ChargeServiceVehicles { get; set; }

        [SettingsUISection(GeneralTab, ChargeServicePublicSection)]
        public bool ChargePublicVehicles { get; set; }

        internal const string LogSection = "Log Settings";
        [SettingsUISection(GeneralTab, LogSection)]
        public bool EnableGeneralLogging { get; set; }

        [SettingsUISection(GeneralTab, LogSection)]
        public bool EnableJobsLogging { get; set; }

        [SettingsUISection(GeneralTab, LogSection)]
        public bool EnableVehicleLogging { get; set; }

        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUISection(GeneralTab, ChargeServicePublicSection)]
        public bool ResetSettings
        {
            set
            {
                SetDefaults();
                ApplyAndSave();
            }
        }


        internal const string PrivateTransportPeakSection = "PrivateTransport";
        [SettingsUISection(PeakTab, PrivateTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float MotorcyclePeakPrice { get; set; }

        [SettingsUISection(PeakTab, PrivateTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarPeakPrice { get; set; }

        [SettingsUISection(PeakTab, PrivateTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarWithTrailerPeakPrice { get; set; }

        internal const string TruckPeakSection = "TruckPeakSection";
        [SettingsUISection(PeakTab, TruckPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckPeakPrice { get; set; }

        [SettingsUISection(PeakTab, TruckPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckWithTrailerPeakPrice { get; set; }

        internal const string PublicTransportPeakSection = "PublicTransportPeakSection";
        [SettingsUISection(PeakTab, PublicTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles), true)]
        public float BusPeakPrice { get; set; }

        [SettingsUISection(PeakTab, PublicTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles), true)]
        public float TaxiPeakPrice { get; set; }

        internal const string ServiceVehiclesPeakSection = "ServiceVehiclesPeakSection";
        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float ParkMaintenancePeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float RoadMaintenancePeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float AmbulancePeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float EvacuatingTransportPeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float FireEnginePeakPrice { get; set; }
        
        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float GarbageTruckPeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float HearsePeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float PoliceCarPeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float PostVanPeakPrice { get; set; }

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float PrisonerTransportPeakPrice { get; set; }
        




        internal const string PrivateTransportNonPeakSection = "PrivateTransportNonPeakSection";
        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float MotorcycleNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarNonPeakPrice { get; set; }
        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarWithTrailerNonPeakPrice { get; set; }

        internal const string TruckNonPeakSection = "TruckNonPeakSection";
        [SettingsUISection(NonPeakTab, TruckNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, TruckNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckWithTrailerNonPeakPrice { get; set; }

        internal const string PublicTransportNonPeakSection = "PublicTransportNonPeakSection";
        [SettingsUISection(NonPeakTab, PublicTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles), true)]
        public float BusNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, PublicTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles), true)]
        public float TaxiNonPeakPrice { get; set; }

        internal const string ServiceVehicleNonPeakSection = "ServiceVehicleNonPeakSection";
        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float ParkMaintenanceNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float RoadMaintenanceNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float AmbulanceNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float EvacuatingTransportNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float FireEngineNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float GarbageTruckNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float HearseNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float PoliceCarNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PostVanNonPeakPrice { get; set; }

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles), true)]
        public float PrisonerTransportNonPeakPrice { get; set; }

        // Sections from the Settings UI
        internal const string AboutSection = "About";

        [SettingsUISection(AboutTab, AboutSection)]
        public string ModVersion => Mod.Version;

        [SettingsUISection(AboutTab, AboutSection)]
        public string AuthorMod => Mod.Author;

        [SettingsUISection(AboutTab, AboutSection)]
        public string InformationalVersion => Mod.InformationalVersion;

        [SettingsUISection(AboutTab, AboutSection)]
        public bool OpenRepositoryAtVersion
        {
            set
            {
                try
                {
                    Application.OpenURL($"https://github.com/Javapower77/cs2-tollbooth-highways/commit/{Mod.InformationalVersion.Split('+')[1]}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutTab, AboutSection)]
        public bool OpenDocumentation
        {
            set
            {
                try
                {
                    Application.OpenURL($"https://github.com/Javapower77/cs2-tollbooth-highways/blob/master/README.md");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutTab, AboutSection)]
        [SettingsUIMultilineText("coui://javapower-tollbooth-highways/discord-icon-white.png")]
        public string DiscordServers => string.Empty;

        [SettingsUISection(AboutTab, AboutSection)]
        public bool OpenCS2ModdingDiscord
        {
            set
            {
                try
                {
                    Application.OpenURL($"https://discord.gg/HTav7ARPs2");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutTab, AboutSection)]
        public bool OpenAuthorDiscord
        {
            set
            {
                try
                {
                    Application.OpenURL($"https://discord.gg/VxDJTMzf");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public ModSettings(IMod mod) : base(mod)
        {
            Instance = this;
        }

        // Add this method to handle when settings are applied
        public override void Apply()
        {
            base.Apply();
            
            // Force update the instance reference
            Instance = this;
        }

        // Also override the OnSetDefaults to ensure Instance is updated
        public override void SetDefaults()
        {
            MotorcycleNonPeakPrice = 70f;
            MotorcyclePeakPrice = 100f; 
            PrivateCarNonPeakPrice = 140f;
            PrivateCarPeakPrice = 200f;
            PrivateCarWithTrailerNonPeakPrice = 190f;
            PrivateCarWithTrailerPeakPrice = 250f;
            TruckNonPeakPrice = 400f;
            TruckPeakPrice = 500f;
            TruckWithTrailerNonPeakPrice = 500f;
            TruckWithTrailerPeakPrice = 700f;
            BusNonPeakPrice = 100f;
            BusPeakPrice = 150f;
            TaxiNonPeakPrice = 50f;
            TaxiPeakPrice = 110f;
            ParkMaintenanceNonPeakPrice = 100f;
            ParkMaintenancePeakPrice = 200f;
            RoadMaintenanceNonPeakPrice = 100f;
            RoadMaintenancePeakPrice = 200f;
            AmbulanceNonPeakPrice = 100f;
            AmbulancePeakPrice = 200f;
            EvacuatingTransportNonPeakPrice = 100f;
            EvacuatingTransportPeakPrice = 200f;
            FireEngineNonPeakPrice = 100f;
            FireEnginePeakPrice = 200f;
            GarbageTruckNonPeakPrice = 100f;
            GarbageTruckPeakPrice = 200f;
            HearseNonPeakPrice = 100f;
            HearsePeakPrice = 200f;
            PoliceCarNonPeakPrice = 100f;
            PoliceCarPeakPrice = 200f;
            PostVanNonPeakPrice = 100f;
            PostVanPeakPrice = 200f;
            PrisonerTransportNonPeakPrice = 100f;
            PrisonerTransportPeakPrice = 200f;
            ChargeServiceVehicles = false;
            ChargePublicVehicles = true;
            EnableGeneralLogging = false;
            EnableVehicleLogging = false;
            EnableJobsLogging = false;
            
            // Update instance after setting defaults
            Instance = this;
        }
    }
}
