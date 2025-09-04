using Game.Net;
using Game.Simulation;
using HarmonyLib;
using TollboothHighways.Domain.Components;
using Unity.Entities;

namespace TollboothHighways.Patches
{
    /// <summary>
    /// Harmony patch to modify the pathfinding cost for car lanes.
    /// This patch makes toll booth lanes with the 'Forbidden' flag accessible to personal cars
    /// by removing the high cost, effectively creating an exclusive lane.
    /// </summary>
    [HarmonyPatch(typeof(CarLaneSelectIterator), "GetLaneDriveCost")]
    internal static class CarLaneSelectIterator_GetLaneDriveCostPatch
    {
        /// <summary>
        /// A postfix that runs after the original GetLaneDriveCost method.
        /// </summary>
        /// <param name="__instance">The instance of CarLaneSelectIterator.</param>
        /// <param name="flags">The flags of the lane being evaluated.</param>
        /// <param name="__result">The original calculated cost, which we can modify.</param>
        [HarmonyPostfix]
        private static void Postfix(in CarLaneSelectIterator __instance, CarLaneFlags flags, ref float __result)
        {
            // Check if the original method determined the lane was forbidden.
            if (__result > 4.0f && (flags & CarLaneFlags.Forbidden) != 0)
            {
                // This is a toll lane marked as 'Forbidden'.
                // Now, check the vehicle type. If it's a personal car, we override the cost to allow access.
                // Personal cars do not have any of these 'Forbid' flags.
                bool isRestrictedVehicle = (__instance.m_ForbidLaneFlags & (CarLaneFlags.ForbidHeavyTraffic | CarLaneFlags.ForbidTransitTraffic)) != 0;

                // If it's NOT a restricted vehicle (i.e., it's a personal car), reset the cost to 0.
                if (!isRestrictedVehicle)
                {
                    __result = 0f;
                }
            }
        }
    }
}