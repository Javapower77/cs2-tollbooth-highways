using Game.Net;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Path;
using TollboothHighways.Domain.Enums;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CarLane = Game.Net.CarLane;

namespace TollboothHighways.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TollboothHighways.Systems.TollRoadPrefabUpdateSystem))]
    public partial struct TollLanePatchSystem : ISystem
    {
        private EntityQuery _laneQuery;

        private ComponentLookup<TollRoadPrivateTransportData> _priv;
        private ComponentLookup<TollRoadPublicTransportData>  _pub;
        private ComponentLookup<TollRoadTruckData>            _truck;
        private ComponentLookup<TollRoadServiceVehiclesData>  _service;
        private ComponentLookup<TollRoadAllVehiclesData>      _all;

        public void OnCreate(ref SystemState state)
        {
            _laneQuery = state.GetEntityQuery(
                // Network lane types you want to tag
                ComponentType.ReadOnly<CarLane>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<TollPatchedLane>()
            );
            state.RequireForUpdate(_laneQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            _priv    = state.GetComponentLookup<TollRoadPrivateTransportData>(true);
            _pub     = state.GetComponentLookup<TollRoadPublicTransportData>(true);
            _truck   = state.GetComponentLookup<TollRoadTruckData>(true);
            _service = state.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            _all     = state.GetComponentLookup<TollRoadAllVehiclesData>(true);

            var prefabRefType = state.GetComponentTypeHandle<PrefabRef>(true);
            var entityType    = state.GetEntityTypeHandle();
            var ecb           = new EntityCommandBuffer(Allocator.Temp);

            foreach (var chunk in _laneQuery.ToArchetypeChunkArray(Allocator.Temp))
            {
                var prefabs  = chunk.GetNativeArray(prefabRefType);
                var entities = chunk.GetNativeArray(entityType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var prefabRef = prefabs[i];
                    if (!state.EntityManager.Exists(prefabRef.m_Prefab))
                        continue;

                    var group = ResolveGroup(prefabRef.m_Prefab);
                    if (group == VehicleGroup.None)
                        continue;

                    var tollMask = TollPathMethods.FromVehicleGroup(group);

                    ecb.AddComponent(entities[i], new TollAllowedMethod { Value = tollMask });
                    ecb.AddComponent<TollPatchedLane>(entities[i]);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private VehicleGroup ResolveGroup(Entity prefab)
        {
            if (_all.HasComponent(prefab))     return VehicleGroup.All;
            if (_priv.HasComponent(prefab))    return VehicleGroup.PrivateTransport;
            if (_pub.HasComponent(prefab))     return VehicleGroup.PublicTransport;
            if (_truck.HasComponent(prefab))   return VehicleGroup.Trucks;
            if (_service.HasComponent(prefab)) return VehicleGroup.ServiceVehicles;
            return VehicleGroup.None;
        }
    }
}