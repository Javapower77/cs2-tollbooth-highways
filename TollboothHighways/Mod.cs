using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Input;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.UI.InGame;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Systems;
using TollboothHighways.Domain.Components;
using TollboothHighways.Systems;
using TollboothHighways.Utilities;
using Unity.Entities;
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
        public static readonly string harmonyID = "TollboothHighways";

        public string ModPath { get; set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            LogUtil.Info($"{nameof(Mod)}.{nameof(OnLoad)}, version:{InformationalVersion}");

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

                // 2. Spawn tollbooth / toll road entities (early in simulation)
                updateSystem.UpdateAt<TollBoothSpawnSystem>(SystemUpdatePhase.GameSimulation);

                // 3. Enforce lane flags after tollbooth spawning
                updateSystem.UpdateAfter<TollBoothLaneFlagEnforcementSystem, TollBoothSpawnSystem>(SystemUpdatePhase.GameSimulation);

                // 4. Monitor vehicle paths for invalid tollbooth usage (NEW - runs after lane flag enforcement)
                updateSystem.UpdateAfter<VehicleTollboothPathMonitoringSystem, TollBoothLaneFlagEnforcementSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<VehicleTollboothPathMonitoringSystem, Game.Simulation.PathfindSetupSystem>(SystemUpdatePhase.GameSimulation);

                // 5. Selection system for toll booths
                updateSystem.UpdateAt<TollboothSelectionSystem>(SystemUpdatePhase.GameSimulation);

                // 6. StopVehiclesOnRoadSystem ordering:
                updateSystem.UpdateAfter<StopVehiclesOnRoadSystem, Game.Simulation.CarNavigationSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<StopVehiclesOnRoadSystem, Game.Simulation.CarMoveSystem>(SystemUpdatePhase.GameSimulation);

                // 7. UI systems (separate phases)
                updateSystem.UpdateAt<TollBoothInfoUISystem>(SystemUpdatePhase.UIUpdate);
                updateSystem.UpdateAt<TollBoothTooltipUISystem>(SystemUpdatePhase.UITooltip);

                LogUtil.Info("All systems registered successfully (including VehicleTollboothPathMonitoringSystem).");

                //Harmony
                var harmony = new Harmony(harmonyID);
                //Harmony.DEBUG = true;
                harmony.PatchAll(typeof(Mod).Assembly);
                var patchedMethods = harmony.GetPatchedMethods().ToArray();
                LogUtil.Info($"Plugin {harmonyID} made patches! Patched methods: " + patchedMethods);
                foreach (var patchedMethod in patchedMethods)
                {
                    LogUtil.Info($"Patched: {patchedMethod.DeclaringType?.FullName}.{patchedMethod.Name}");
                }
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
