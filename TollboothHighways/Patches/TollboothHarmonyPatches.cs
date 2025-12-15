using Game.Pathfind;
using Game.Simulation;
using HarmonyLib;
using System;
using System.Reflection;
using TollboothHighways.Utilities;
using Unity.Entities;

namespace TollboothHighways.Patches
{
    /// <summary>
    /// Manages all Harmony patches for the Tollbooth Highways mod.
    /// </summary>
    public static class TollboothHarmonyPatches
    {
        private static Harmony _harmony;
        private static bool _isPatched;
        
        public const string HARMONY_ID = "com.tollboothhighways.patches";
        
        /// <summary>
        /// Applies all Harmony patches.
        /// </summary>
        public static void ApplyPatches(EntityManager entityManager)
        {
            if (_isPatched)
            {
                LogUtil.Info("TollboothHarmonyPatches: Already patched, skipping");
                return;
            }
            
            try
            {
                // Initialize the pathfinding patch with entity manager
                TollRoadPathfindingPatch.Initialize(entityManager);
                
                _harmony = new Harmony(HARMONY_ID);
                
                // Apply manual patches
                ApplyPathfindingPatches();
                
                _isPatched = true;
                LogUtil.Info("TollboothHarmonyPatches: All patches applied successfully");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollboothHarmonyPatches: Failed to apply patches - {ex.Message}");
                LogUtil.Error(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// Removes all Harmony patches.
        /// </summary>
        public static void RemovePatches()
        {
            if (!_isPatched || _harmony == null)
                return;
            
            try
            {
                _harmony.UnpatchAll(HARMONY_ID);
                _isPatched = false;
                LogUtil.Info("TollboothHarmonyPatches: All patches removed");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"TollboothHarmonyPatches: Failed to remove patches - {ex.Message}");
            }
        }
        
        private static void ApplyPathfindingPatches()
        {
            // Try to patch PathfindSetupSystem or related pathfinding methods
            // The exact method depends on the game's internals
            
            try
            {
                // Attempt to find and patch the cost calculation method
                var pathfindSetupType = typeof(PathfindSetupSystem);
                
                // Log available methods for debugging
                if (ModSettings.Instance?.EnableGeneralLogging == true)
                {
                    LogUtil.Info($"TollboothHarmonyPatches: Analyzing {pathfindSetupType.Name}");
                    
                    foreach (var method in pathfindSetupType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        LogUtil.Info($"  Method: {method.Name}");
                    }
                }
                
                // Apply attribute-based patches
                _harmony.PatchAll(typeof(PathfindSetupSystemPatches).Assembly);
                
                LogUtil.Info("TollboothHarmonyPatches: Pathfinding patches applied");
            }
            catch (Exception ex)
            {
                LogUtil.Warn($"TollboothHarmonyPatches: Could not patch PathfindSetupSystem - {ex.Message}");
                LogUtil.Info("TollboothHarmonyPatches: Falling back to alternative approach");
                
                // Try alternative patches
                try
                {
                    _harmony.PatchAll(typeof(CarNavigationPatches).Assembly);
                    LogUtil.Info("TollboothHarmonyPatches: Car navigation patches applied");
                }
                catch (Exception ex2)
                {
                    LogUtil.Warn($"TollboothHarmonyPatches: Alternative patches also failed - {ex2.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Patches for PathfindSetupSystem to modify lane costs.
    /// </summary>
    [HarmonyPatch]
    public static class PathfindSetupSystemPatches
    {
        /// <summary>
        /// Attempts to patch the OnUpdate method to intercept pathfinding setup.
        /// </summary>
        [HarmonyPatch(typeof(PathfindSetupSystem), "OnUpdate")]
        [HarmonyPrefix]
        public static void OnUpdate_Prefix(PathfindSetupSystem __instance)
        {
            // This prefix runs before pathfinding is set up
            // We can use this to log or prepare data
            if (ModSettings.Instance?.EnableJobsLogging == true)
            {
                // Minimal logging to avoid performance impact
            }
        }
    }
    
    /// <summary>
    /// Alternative patches for car navigation if PathfindSetupSystem patches fail.
    /// </summary>
    [HarmonyPatch]
    public static class CarNavigationPatches
    {
        // This will be populated based on what methods are available to patch
    }
}