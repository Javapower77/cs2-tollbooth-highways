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
        internal const string SETTINGS_ASSET_NAME = "Toll Highways General Settings";
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
        public float MotorcyclePeakPrice { get; set; } = 100f;

        [SettingsUISection(PeakTab, PrivateTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarPeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, PrivateTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarWithTrailerPeakPrice { get; set; } = 250f;

        internal const string TruckPeakSection = "TruckPeakSection";
        [SettingsUISection(PeakTab, TruckPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckPeakPrice { get; set; } = 500f;

        [SettingsUISection(PeakTab, TruckPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckWithTrailerPeakPrice { get; set; } = 700f;

        internal const string PublicTransportPeakSection = "PublicTransportPeakSection";
        [SettingsUISection(PeakTab, PublicTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles))]
        public float BusPeakPrice { get; set; } = 150f;

        [SettingsUISection(PeakTab, PublicTransportPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles))]
        public float TaxiPeakPrice { get; set; } = 110f;

        internal const string ServiceVehiclesPeakSection = "ServiceVehiclesPeakSection";
        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float ParkMaintenancePeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float RoadMaintenancePeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float AmbulancePeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float EvacuatingTransportPeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float FireEnginePeakPrice { get; set; } = 200f;
        
        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float GarbageTruckPeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float HearsePeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PoliceCarPeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PostVanPeakPrice { get; set; } = 200f;

        [SettingsUISection(PeakTab, ServiceVehiclesPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PrisonerTransportPeakPrice { get; set; } = 200f;
        




        internal const string PrivateTransportNonPeakSection = "PrivateTransportNonPeakSection";
        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float MotorcycleNonPeakPrice { get; set; } = 70f;

        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarNonPeakPrice { get; set; } = 140f;

        [SettingsUISection(NonPeakTab, PrivateTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float PrivateCarWithTrailerNonPeakPrice { get; set; } = 190f;

        internal const string TruckNonPeakSection = "TruckNonPeakSection";
        [SettingsUISection(NonPeakTab, TruckNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckNonPeakPrice { get; set; } = 400f;

        [SettingsUISection(NonPeakTab, TruckNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        public float TruckWithTrailerNonPeakPrice { get; set; } = 500f;

        internal const string PublicTransportNonPeakSection = "PublicTransportNonPeakSection";
        [SettingsUISection(NonPeakTab, PublicTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles))]
        public float BusNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, PublicTransportNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargePublicVehicles))]
        public float TaxiNonPeakPrice { get; set; } = 50f;

        internal const string ServiceVehicleNonPeakSection = "ServiceVehicleNonPeakSection";
        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float ParkMaintenanceNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float RoadMaintenanceNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float AmbulanceNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float EvacuatingTransportNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float FireEngineNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float GarbageTruckNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float HearseNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PoliceCarNonPeakPrice { get; set; } =  100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PostVanNonPeakPrice { get; set; } = 100f;

        [SettingsUISection(NonPeakTab, ServiceVehicleNonPeakSection)]
        [SettingsUISlider(min = 0, max = 1000, step = 10, unit = Unit.kMoney)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(ChargeServiceVehicles))]
        public float PrisonerTransportNonPeakPrice { get; set; } = 100f;




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
                    Application.OpenURL($"https://github.com/Javapower77/cs2-toll-highways/commit/{Mod.InformationalVersion.Split('+')[1]}");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        [SettingsUISection(AboutTab, AboutSection)]
        public bool OpenRepositoryRoadmap
        {
            set
            {
                try
                {
                    Application.OpenURL($"https://github.com/users/Javapower77/projects/2/views/5");
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
            SetDefaults();
        }

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
            ChargeServiceVehicles = true;
            ChargePublicVehicles = true;

        }
    }
}
