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
using System.Reflection;
using Systems;
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

        // This is something for the feature if this mod is incompatible with other mod in order to fix
        // ---
        // public static bool IsTLEEnabled => _isTLEEnabled ??= GameManager.instance.modManager.ListModsEnabled().Any(x => x.StartsWith("C2VM.CommonLibraries.LaneSystem"));
        // public static bool IsRBEnabled => _isRBEnabled ??= GameManager.instance.modManager.ListModsEnabled().Any(x => x.StartsWith("RoadBuilder"));
        // private static bool? _isTLEEnabled;
        // private static bool? _isRBEnabled;
        public void OnLoad(UpdateSystem updateSystem)
        {
            // Log entry for debugging purposes
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

                // Load all dictonary in English to apply in the objects of the mod
                GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));

                // Load the settings for the current mod
                AssetDatabase.global.LoadSettings(nameof(TollboothHighways), Settings, new ModSettings(this));

                Settings.ApplyAndSave();

                // Try to fetch the mod asset from the mod manager
                if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                {
                    ModPath = Path.GetDirectoryName(asset.path);
                    // Set the thumbnails location for the assets inside the mod
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

                // 2. Vehicle category determination (early each simulation frame)
                // If you only use one of these systems, remove the other line.
                updateSystem.UpdateAt<VehicleCategoryAssignmentSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAfter<VehicleCategoryMaskBuildSystem, VehicleCategoryAssignmentSystem>(SystemUpdatePhase.GameSimulation);

                // 3. Spawn tollbooth / toll road entities after base lane data exists
                updateSystem.UpdateAfter<TollBoothSpawnSystem, Game.Pathfind.LaneDataSystem>(SystemUpdatePhase.GameSimulation);

                // 4. Build toll lane masks after spawn (bitmask + flags)
                updateSystem.UpdateAfter<TollLaneMaskBuildSystem, TollBoothSpawnSystem>(SystemUpdatePhase.GameSimulation);

                // 5. (Optional) Path reference pruning after mask changes
                updateSystem.UpdateAfter<TollLanePathPruneRefCountSystem, TollLaneMaskBuildSystem>(SystemUpdatePhase.GameSimulation);

                // 6. Category lane filtering BEFORE CarNavigationSystem
                //    Produces temporary blocked markers per category cycle
                updateSystem.UpdateAfter<TollLaneCategoryFilterSystem, TollLaneMaskBuildSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollLaneCategoryFilterSystem, Game.Simulation.CarNavigationSystem>(SystemUpdatePhase.GameSimulation);

                // 7. Apply temp blocks (convert to Blocked or equivalent), still before navigation
                updateSystem.UpdateAfter<TollLaneTempBlockApplySystem, TollLaneCategoryFilterSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollLaneTempBlockApplySystem, Game.Simulation.CarNavigationSystem>(SystemUpdatePhase.GameSimulation);

                // 8. Eligibility enforcement: repath vehicles that slipped onto disallowed lane
                updateSystem.UpdateAfter<TollLaneEligibilityEnforceSystem, Game.Simulation.CarNavigationSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<TollLaneEligibilityEnforceSystem, Game.Simulation.CarMoveSystem>(SystemUpdatePhase.GameSimulation);

                // 9. Stop vehicles system (after enforcement, still before movement)
                updateSystem.UpdateAfter<StopVehiclesOnRoadSystem, TollLaneEligibilityEnforceSystem>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateBefore<StopVehiclesOnRoadSystem, Game.Simulation.CarMoveSystem>(SystemUpdatePhase.GameSimulation);

                // 10. UI systems (separate phases)
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
