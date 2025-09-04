using Game;
using Game.Pathfind;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Provides pathfinding lane cost penalties so that MANUAL toll lanes are basically usable only by PersonalCar.
/// - PersonalCar: small (or zero) extra cost (neutral path choice)
/// - Non Personal: massive penalty so pathfinder avoids them unless absolutely no alternative.
/// 
/// Integration Notes:
/// 1. If the base game exposes an extension point / interface for lane cost modifiers,
///    adapt the #if HAS_OFFICIAL_COST_INTERFACE region.
/// 2. Otherwise, a Harmony patch can read the exported static NativeHashMap from this system
///    and add the penalty into the lane traversal cost (see comment).
/// </summary>
namespace TollboothHighways.Systems
{
    public partial class PersonalCarTollPathfindCostSystem : GameSystemBase
    {
        // Public (static) so a Harmony patch can read it if needed.
        internal static NativeParallelHashMap<Entity, LanePersonalCarCost> LaneCostMap;

        private EntityQuery _taggedLaneQuery;
        private EntityQuery _personalCarQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            _taggedLaneQuery = GetEntityQuery(
                ComponentType.ReadOnly<ManualTollLaneTag>(),
                ComponentType.ReadOnly<LanePersonalCarCost>());

            // Vehicles with PersonalCar component
            _personalCarQuery = GetEntityQuery(
                ComponentType.ReadOnly<PersonalCar>(),
                ComponentType.ReadOnly<Car>());

            LaneCostMap = new NativeParallelHashMap<Entity, LanePersonalCarCost>(128, Allocator.Persistent);

            RequireForUpdate(_taggedLaneQuery);
        }

        protected override void OnDestroy()
        {
            if (LaneCostMap.IsCreated)
                LaneCostMap.Dispose();

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // Refresh native map with current lane costs (cheap if stable).
            var lanes = _taggedLaneQuery.ToEntityArray(Allocator.Temp);
            var costs = _taggedLaneQuery.ToComponentDataArray<LanePersonalCarCost>(Allocator.Temp);

            // Rebuild map each frame (can be optimized with change filters if needed)
            LaneCostMap.Clear();

            for (int i = 0; i < lanes.Length; i++)
            {
                if (!LaneCostMap.ContainsKey(lanes[i]))
                {
                    LaneCostMap.TryAdd(lanes[i], costs[i]);
                }
            }

            lanes.Dispose();
            costs.Dispose();

#if HAS_OFFICIAL_COST_INTERFACE
            // If CS2 exposes something like:
            //   public interface IPathfindLaneCostModifier { int ModifyCost(Entity lane, Entity vehicle, int baseCost); }
            // you would implement that interface on this system and inside ModifyCost do:
            //   if (LaneCostMap.TryGetValue(lane, out var c)) {
            //       bool isPersonal = EntityManager.HasComponent<PersonalCar>(vehicle);
            //       return baseCost + (isPersonal ? c.PersonalCarAdditionalCost : c.NonPersonalAdditionalCost);
            //   }
            //   return baseCost;
#endif
        }

        // OPTIONAL: Helper for external (Harmony) patch
        public static bool TryGetAdjustedCost(Entity lane, bool isPersonalCar, int baseCost, out int newCost)
        {
            if (LaneCostMap.IsCreated && LaneCostMap.TryGetValue(lane, out var c))
            {
                newCost = baseCost + (isPersonalCar ? c.PersonalCarAdditionalCost : c.NonPersonalAdditionalCost);
                return true;
            }
            newCost = baseCost;
            return false;
        }
    }
}

/*
Harmony Patch Example (Pseudo):

[HarmonyPatch(typeof(SomeInternalLaneCostJobOrMethod), "SampleLaneCost")]
static class TollManualLaneCostPatch
{
    static void Postfix(Entity lane, Entity vehicle, ref int __result)
    {
        bool isPersonal = World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<PersonalCar>(vehicle);

        if (PersonalCarTollPathfindCostSystem.TryGetAdjustedCost(lane, isPersonal, __result, out var adjusted))
        {
            __result = adjusted;
        }
    }
}
*/