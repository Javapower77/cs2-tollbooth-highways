using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Input;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI.InGame;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using Systems;
using TollboothHighways.Domain.Components;
using TollboothHighways.Systems;
using TollboothHighways.Utilities;
using static TollboothHighways.ModSettings;

namespace TollboothHighways
{
    public class Mod : IMod
    {
        public const string MOD_NAME = "TollboothHighways";
        public static string uiHostName = "javapower-tollbooth-highways";
        public static readonly string Id = "TollboothHighways";
        public static string Author = "Javapower";
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString(4);
        public static string InformationalVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        internal static ModSettings Settings { get; private set; }

        public string ModPath { get; set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            LogUtil.Info($"{nameof(Mod)}.{nameof(OnLoad)}, version:{InformationalVersion}");

            // Apply Harmony Patches
            var harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Mod).Assembly);
            LogUtil.Info("Harmony patches applied.");

            try
            {
                LogUtil.Info("Registering settings + key bindings");
                Settings = new ModSettings(this);
                Settings.RegisterKeyBindings();
                Settings.RegisterInOptionsUI();

                GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
                AssetDatabase.global.LoadSettings(nameof(TollboothHighways), Settings, new ModSettings(this));
                Settings.ApplyAndSave();

                if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                {
                    ModPath = Path.GetDirectoryName(asset.path);
                    UIManager.defaultUISystem.AddHostLocation(uiHostName, Path.Combine(Path.GetDirectoryName(asset.path), "thumbs"), false);
                    LogUtil.Info($"Current mod asset at {asset.path}");
                    LogUtil.Info($"Current mod asset at {Path.GetDirectoryName(asset.path)}");
                    LogUtil.Info($"Current mod asset at {Path.Combine(Path.GetDirectoryName(asset.path), "thumbs")}");
                    LogUtil.Info($"Current mod asset at {uiHostName}");
                }
                else
                {
                    LogUtil.Error("Unable to get mod executable asset.");
                    return;
                }

                // ---------------------------
                // SYSTEM REGISTRATION ORDER
                // ---------------------------

                // 1. Prefab processing (runs only until successful)
                updateSystem.UpdateAt<TollRoadPrefabUpdateSystem>(SystemUpdatePhase.PrefabUpdate);

                // 2. Toll lane patching (after prefabs, BEFORE spawn & path building)
                //    Place before TollBoothSpawnSystem and before path parameter injection.
                updateSystem.UpdateBefore<TollLanePatchSystem, TollBoothSpawnSystem>(SystemUpdatePhase.GameSimulation);

                // 3. Spawn tollbooth / toll road entities (early in simulation)
                updateSystem.UpdateAt<TollBoothSpawnSystem>(SystemUpdatePhase.GameSimulation);

                // 4. Selection system for toll booths
                updateSystem.UpdateAt<TollboothSelectionSystem>(SystemUpdatePhase.GameSimulation);

                // 5. Path parameter injection
                //    Must run:
                //      - After lane patch (so lane data is ready)
                //      - Before navigation & any per-vehicle AI systems that might read path params
                updateSystem.UpdateAfter<TollPathParameterInjectionSystem, TollLanePatchSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, CarNavigationSystem>(SystemUpdatePhase.GameSimulation);

                // Preserve original attribute intent: run before every vehicle AI system previously listed.
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, PersonalCarAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, AmbulanceAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, DeliveryTruckAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, FireEngineAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, GarbageTruckAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, HearseAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, MaintenanceVehicleAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, PoliceCarAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, PostVanAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, TaxiAISystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollPathParameterInjectionSystem, TransportCarAISystem>(SystemUpdatePhase.GameSimulation);

                // 6. Violation validation (AFTER CarNavigation so lane positions are updated)
                updateSystem.UpdateAfter<TollLaneViolationSystem, CarNavigationSystem>(SystemUpdatePhase.GameSimulation);

                // 7. StopVehiclesOnRoadSystem ordering:
                // After core CarNavigationSystem (so navigation complete)
                updateSystem.UpdateAfter<StopVehiclesOnRoadSystem, CarNavigationSystem>(SystemUpdatePhase.GameSimulation);
                // Before CarMoveSystem (so movement uses zeroed speed)
                updateSystem.UpdateBefore<StopVehiclesOnRoadSystem, CarMoveSystem>(SystemUpdatePhase.GameSimulation);

                // 8. UI systems (separate phases)
                updateSystem.UpdateAt<TollBoothInfoUISystem>(SystemUpdatePhase.UIUpdate);
                updateSystem.UpdateAt<TollBoothTooltipUISystem>(SystemUpdatePhase.UITooltip);

                LogUtil.Info("All systems registered (validated order, no duplicates).");
            }
            catch (Exception ex)
            {
                LogUtil.Exception(ex);
            }
        }

        public void OnDispose()
        {
            UIManager.defaultUISystem.RemoveHostLocation(uiHostName);
            UIManager.defaultUISystem.RemoveHostLocation("netsubobject-info");
            UIManager.defaultUISystem.RemoveHostLocation("mouse-position");
            LogUtil.Info($"{nameof(Mod)}.{nameof(OnDispose)}");
            Settings?.UnregisterInOptionsUI();
            Settings = null;
        }
    }
}
