using Colossal;
using Game.Modding;
using Game.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TollboothHighways
{
    public partial class ModSettings : ModSetting
    {
        public class LocaleEN : IDictionarySource
        {
            private readonly ModSettings _setting;
            private Dictionary<string, string> _translations;

            public LocaleEN(ModSettings setting)
            {
                _setting = setting;
                _translations = Load();
            }
            public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
            {
                return _translations;
            }

            public static string GetToolTooltipLocaleID(string tool, string value)
            {
                return $"{Mod.MOD_NAME}.Tooltip.Tools[{tool}][{value}]";
            }

            public static string GetLanguageNameLocaleID()
            {
                return $"{Mod.MOD_NAME}.Language.DisplayName";
            }

            public Dictionary<string, string> Load(bool dumpTranslations = false)
            {
                return new Dictionary<string, string>
                {
                    { _setting.GetSettingsLocaleID(), "Tollbooth Highways" },
                    { GetLanguageNameLocaleID(), "English"},
                    { _setting.GetOptionTabLocaleID(ModSettings.GeneralTab), "General" },
                    { _setting.GetOptionTabLocaleID(ModSettings.AboutTab), "About" },
                    { _setting.GetOptionTabLocaleID(ModSettings.CreditsTab), "Credits" },
                    { _setting.GetOptionTabLocaleID(ModSettings.PeakTab), "Peak Hours" },
                    { _setting.GetOptionTabLocaleID(ModSettings.NonPeakTab), "Non-Peak Hours" },
                    // Groups
                    { _setting.GetOptionGroupLocaleID(ModSettings.AboutSection), "Mod Info" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.CreditsSection), "Special Thanks" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.ChargeServicePublicSection), "General Settings" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.PrivateTransportNonPeakSection), "Private Transport" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.PrivateTransportPeakSection), "Private Transport" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.PublicTransportNonPeakSection), "Public Transport" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.PublicTransportPeakSection), "Public Transport" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.ServiceVehicleNonPeakSection), "Services Vehicles" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.ServiceVehiclesPeakSection), "Services Vehicles" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.TruckNonPeakSection), "Truck" },
                    { _setting.GetOptionGroupLocaleID(ModSettings.TruckPeakSection), "Truck" },
                    //Keybindings

                    //Labels
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenRepositoryAtVersion)), "Open GitHub mod repository" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.OpenRepositoryAtVersion)), "Open the github repository of this mod." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenRepositoryRoadmap)), "Open Roadmap" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.OpenRepositoryRoadmap)), "Open the status board of the tasks involved in the developing of this mod." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ModVersion)), "Version" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ModVersion)), "Mod current version." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.AuthorMod)), "Mod author" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.AuthorMod)), "Name of the author of this mod." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.InformationalVersion)), "Informational Version" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.InformationalVersion)), "Mod version with the commit ID from GitHub." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.DiscordServers)), "Discord Servers" },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenCS2ModdingDiscord)), "Cities: Skylines Modding Discord" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.OpenCS2ModdingDiscord)), "Open the official Cities Skyline 2 modding discord server." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.OpenAuthorDiscord)), "Mod Author Server" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.OpenAuthorDiscord)), "Open the author discord server." },

                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.MotorcyclePeakPrice)), "Motorcycle" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.MotorcyclePeakPrice)), "Toll price for motorcycles in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrivateCarPeakPrice)), "Personal Car" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrivateCarPeakPrice)), "Toll price for personal cars in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrivateCarWithTrailerPeakPrice)), "Personal Car with Trailer" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrivateCarWithTrailerPeakPrice)), "Toll price for personal cars with trailer in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TruckPeakPrice)), "Truck" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TruckPeakPrice)), "Toll price for trucks in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TruckWithTrailerPeakPrice)), "Truck with Trailer" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TruckWithTrailerPeakPrice)), "Toll price for trucks with trailer in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TaxiPeakPrice)), "Taxi" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TaxiPeakPrice)), "Toll price for taxis in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.BusPeakPrice)), "Bus" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.BusPeakPrice)), "Toll price for buses in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ParkMaintenancePeakPrice)), "Park Maintenance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ParkMaintenancePeakPrice)), "Toll price for park maintenance vehicles in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.RoadMaintenancePeakPrice)), "Road Maintenance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.RoadMaintenancePeakPrice)), "Toll price for road maintenance vehicles in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.AmbulancePeakPrice)), "Ambulance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.AmbulancePeakPrice)), "Toll price for ambulances in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.EvacuatingTransportPeakPrice)), "Evacuating Transport" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.EvacuatingTransportPeakPrice)), "Toll price for evacuating transport in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.FireEnginePeakPrice)), "Fire Engine" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.FireEnginePeakPrice)), "Toll price for fire engines in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.GarbageTruckPeakPrice)), "Garbage Truck" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.GarbageTruckPeakPrice)), "Toll price for garbage trucks in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.HearsePeakPrice)), "Hearse" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.HearsePeakPrice)), "Toll price for hearses in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PoliceCarPeakPrice)), "Police Car" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PoliceCarPeakPrice)), "Toll price for police cars in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PostVanPeakPrice)), "Post Van" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PostVanPeakPrice)), "Toll price for post vans in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.MotorcycleNonPeakPrice)), "Motorcycle" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.MotorcycleNonPeakPrice)), "Toll price for motorcycles in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrivateCarNonPeakPrice)), "Personal Car" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrivateCarNonPeakPrice)), "Toll price for personal cars in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrivateCarWithTrailerNonPeakPrice)), "Personal Car with Trailer" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrivateCarWithTrailerNonPeakPrice)), "Toll price for personal cars with trailer in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TruckNonPeakPrice)), "Truck" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TruckNonPeakPrice)), "Toll price for trucks in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TruckWithTrailerNonPeakPrice)), "Truck with Trailer" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TruckWithTrailerNonPeakPrice)), "Toll price for trucks with trailer in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.TaxiNonPeakPrice)), "Taxi" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.TaxiNonPeakPrice)), "Toll price for taxis in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.BusNonPeakPrice)), "Bus" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.BusNonPeakPrice)), "Toll price for buses in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ParkMaintenanceNonPeakPrice)), "Park Maintenance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ParkMaintenanceNonPeakPrice)), "Toll price for park maintenance vehicles in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.RoadMaintenanceNonPeakPrice)), "Road Maintenance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.RoadMaintenanceNonPeakPrice)), "Toll price for road maintenance vehicles in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.AmbulanceNonPeakPrice)), "Ambulance" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.AmbulanceNonPeakPrice)), "Toll price for ambulances in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.EvacuatingTransportNonPeakPrice)), "Evacuating Transport" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.EvacuatingTransportNonPeakPrice)), "Toll price for evacuating transport in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.FireEngineNonPeakPrice)), "Fire Engine" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.FireEngineNonPeakPrice)), "Toll price for fire engines in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.GarbageTruckNonPeakPrice)), "Garbage Truck" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.GarbageTruckNonPeakPrice)), "Toll price for garbage trucks in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.HearseNonPeakPrice)), "Hearse" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.HearseNonPeakPrice)), "Toll price for hearses in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PoliceCarNonPeakPrice)), "Police Car" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PoliceCarNonPeakPrice)), "Toll price for police cars in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PostVanNonPeakPrice)), "Post Van" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PostVanNonPeakPrice)), "Toll price for post vans in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrisonerTransportNonPeakPrice)), "Prisioner Transport" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrisonerTransportNonPeakPrice)), "Toll price for prisioner transport in non-peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.PrisonerTransportPeakPrice)), "Prisioner Transport" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.PrisonerTransportPeakPrice)), "Toll price for prisioner transport in peak hours." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.SpecialThanks)), "This mod was a long journey in learning modding in CS:2, a game that I loved for years form the first version. Thanks to the patiente and help on so many people on Discord Server this mod could never be possible. I hope you enjoy as much as I enjoyed building it.\r\n\r\nkrzychu124\r\nBruceyboy24804\r\nStarQ / Qoushik\r\nTDW\r\nyenyang\r\nTigon Ologdring\r\njk142\r\nrcav8tr\r\nNullpinter\r\nKonsi / Mimonsi\r\n...and many more! THANKS YOU!!!" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.SpecialThanks)), "krzychu124\r\nBruceyboy24804\r\nStarQ / Qoushik\r\nTDW\r\nyenyang\r\nTigon Ologdring\r\njk142\r\nrcav8tr\r\nNullpinter\r\nKonsi / Mimonsi\r\n...and many more! THANKS YOU!!!" },
                    // Buttons
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetSettings)), "Reset to default values" },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ResetSettings)), "Reset all settings to default values." },
                    { _setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetSettings)), "Are you sure to rollback all value to default?" },
                    // Checkboxs
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ChargePublicVehicles)), "Do not charge tolls to public transport." },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ChargePublicVehicles)), "No fee will be apply to taxis and buses passing a tollbooth." },
                    { _setting.GetOptionLabelLocaleID(nameof(ModSettings.ChargeServiceVehicles)), "Do not charge tolls to services vehicles." },
                    { _setting.GetOptionDescLocaleID(nameof(ModSettings.ChargeServiceVehicles)), "No fee will be apply to services vehicles passing a tollbooth." }
                };
            }

            public void Unload() { }

            public override string ToString()
            {
                return "HighwayTollbooth.Locale.en-US";
            }
        }
    }
}
