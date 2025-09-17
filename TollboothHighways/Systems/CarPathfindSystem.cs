using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Pathfind;
using Game.Prefabs;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    public partial class CarPathfindSystem : GameSystemBase
    {
        // We can remove the GetUpdateInterval and OnUpdate methods
        // if they are empty, as the base class handles them.

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            // We only want this logic to run when loading a game, not in the editor
            if (mode != GameMode.Game)
            {
                return;
            }

            LogUtil.Info("Applying custom car pathfinding costs...");

            // This is a more direct way to get the single entity with pedestrian pathfinding data
            if (SystemAPI.TryGetSingletonRW<PathfindCarData>(out var componentRef))
            {
                // By using "ref", we get a direct reference to the game's data, not a copy
                ref PathfindCarData carData = ref componentRef.ValueRW;

                // Log the original value before we change it
                LogUtil.Info($"Original ForbiddenCost 'behaviour' value: {carData.m_ForbiddenCost.m_Value.x}");

                // The PathfindCosts are stored in a vector (behaviour, comfort, time, money).
                // We are changing the 'x' component, which corresponds to the 'behaviour' cost.
                carData.m_ForbiddenCost.m_Value.x = 5000f;
                
                LogUtil.Info($"New ForbiddenCost 'behaviour' value set to: {carData.m_ForbiddenCost.m_Value.x}");
            }
            else
            {
                LogUtil.Warn("Could not find PathfindCarData singleton to modify.");
            }
        }

        protected override void OnUpdate()
        {
        }
    }
}