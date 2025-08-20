using Game;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Entities;
using Unity.Mathematics;


namespace Systems
{
    public partial class TollRoadPrefabUpdateSystem : GameSystemBase
    {
        private PrefabSystem prefabSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Add the Toll Component to the two custom roads prefabs
            // in order to be used later in the entity query
            prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            AddTollComponentToRoad("RoadPrefab", "Highway Oneway - 1 lane (Toll 60kph)");
            AddTollComponentToRoad("RoadPrefab","Highway Oneway - 1 lane - Public Transport (Toll 60kph)");
            AddTollCabinComponentToRoad("StaticObjectPrefab", "TollBooth");

            LogUtil.Info("TollRoadPrefabUpdateSystem: System created and initialized");
        }

        protected override void OnUpdate()
        {
            return;
        }


        
        // This method is called to initialize the custom road prefab and add the TollRoadPrefabInfo component
        // Also add the component for toll booth prefab
        private void AddTollComponentToRoad(string typePrefab, string PrefabNameToSearch)
        {
            // Check if the prefab for the toll road exists
            if (prefabSystem.TryGetPrefab(new PrefabID(typePrefab, PrefabNameToSearch), out PrefabBase tollRoadPrefab))
            {
                // Check if the prefab already has the TollRoadPrefabInfo component
                if (tollRoadPrefab.GetComponent<TollRoadPrefabInfo>())
                {
                    // If the prefab already has the TollRoadPrefabInfo component, skip it
                    return;
                }
                else
                {
                    // If the prefab does not have the TollRoadPrefabInfo component, add it
                    tollRoadPrefab.AddComponent<TollRoadPrefabInfo>();
                    LogUtil.Info($"TollHighways::UpdateTollRoadsSystem::AddTollComponentToRoad() - Added TollRoadPrefabInfo to {tollRoadPrefab.name}");

                    // Update the prefab with the new component added to the prefab system in order to be used later in a entity query
                    prefabSystem.UpdatePrefab(tollRoadPrefab); 
                }
            }
        }

        private void AddTollCabinComponentToRoad(string typePrefab, string PrefabNameToSearch)
        {
            // Check if the prefab for the toll road exists
            if (prefabSystem.TryGetPrefab(new PrefabID(typePrefab, PrefabNameToSearch), out PrefabBase tollCabinPrefab))
            {
                // Check if the prefab already has the TollRoadPrefabInfo component
                if (tollCabinPrefab.GetComponent<TollBoothPrefabInfo>())
                {
                    // If the prefab already has the TollRoadPrefabInfo component, skip it
                    return;
                }
                else
                {
                    tollCabinPrefab.AddComponent<TollBoothPrefabInfo>();
                    LogUtil.Info($"TollHighways::UpdateTollRoadsSystem::AddTollComponentToRoad() - Added TollRoadPrefabInfo to {tollCabinPrefab.name}");

                    // Update the prefab with the new component added to the prefab system in order to be used later in a entity query
                    prefabSystem.UpdatePrefab(tollCabinPrefab);
                }
            }
        }

    }
}