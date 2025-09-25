using Game.Net;
using Game.Prefabs;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using CarLane = Game.Net.CarLane;
using TollboothHighways.Domain;
using Game;
using Game.Common;

namespace TollboothHighways.Systems
{
    public partial class TollLanePatchSystem : GameSystemBase
    {
        private EntityQuery _laneQuery;

        private ComponentLookup<TollRoadPrivateTransportData> _priv;
        private ComponentLookup<TollRoadPublicTransportData>  _pub;
        private ComponentLookup<TollRoadTruckData>            _truck;
        private ComponentLookup<TollRoadServiceVehiclesData>  _service;
        private ComponentLookup<TollRoadAllVehiclesData>      _all;

        protected override void OnCreate()
        {
            _laneQuery = GetEntityQuery(
                // Network lane types you want to tag
                ComponentType.ReadOnly<CarLane>(),
                ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Owner>(),
                ComponentType.Exclude<TollPatchedLane>()
            );
            RequireForUpdate(_laneQuery);
        }

        protected override void OnUpdate()
        {
            _priv    = GetComponentLookup<TollRoadPrivateTransportData>(true);
            _pub     = GetComponentLookup<TollRoadPublicTransportData>(true);
            _truck   = GetComponentLookup<TollRoadTruckData>(true);
            _service = GetComponentLookup<TollRoadServiceVehiclesData>(true);
            _all     = GetComponentLookup<TollRoadAllVehiclesData>(true);

            //var prefabRefType = GetComponentTypeHandle<PrefabRef>(true);
            var entityType    = GetEntityTypeHandle();
            var ecb           = new EntityCommandBuffer(Allocator.Temp);

            foreach (var chunk in _laneQuery.ToArchetypeChunkArray(Allocator.Temp))
            {
                //var prefabs  = chunk.GetNativeArray(ref prefabRefType);
                var entities = chunk.GetNativeArray(entityType);

                for (int i = 0; i < chunk.Count; i++)
                {
                    //var prefabRef = prefabs[i];
                    //if (!EntityManager.Exists(prefabRef.m_Prefab))
                    //    continue;

                    var group = ResolveGroup(entities[i]);
                    if (group == VehicleGroup.None)
                        continue;

                    var tollMask = TollPathMethods.FromVehicleGroup(group);

                    ecb.AddComponent(entities[i], new TollAllowedMethod { Value = tollMask });
                    ecb.AddComponent<TollPatchedLane>(entities[i]);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private VehicleGroup ResolveGroup(Entity prefab)
        {
            if (EntityManager.HasComponent<Owner>(prefab))
            {
                var owner = EntityManager.GetComponentData<Owner>(prefab);
                Entity tollRoadEntity = owner.m_Owner;

                // Check toll road components on the owner entity
                if (_all.HasComponent(tollRoadEntity)) return VehicleGroup.All;
                if (_priv.HasComponent(tollRoadEntity)) return VehicleGroup.PrivateTransport;
                if (_pub.HasComponent(tollRoadEntity)) return VehicleGroup.PublicTransport;
                if (_truck.HasComponent(tollRoadEntity)) return VehicleGroup.Trucks;
                if (_service.HasComponent(tollRoadEntity)) return VehicleGroup.ServiceVehicles;
            }
            return VehicleGroup.None;
        }
    }
}