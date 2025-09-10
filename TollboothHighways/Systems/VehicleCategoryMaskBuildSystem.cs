using Game;
using Game.Common;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Assigns VehicleCategoryMask to vehicles so toll filtering is fast.
    /// Adjust detection logic to real component types (TransitVehicle, ServiceVehicle, HeavyTruck etc).
    /// </summary>
     public partial class VehicleCategoryMaskBuildSystem : GameSystemBase
    {
        private EntityQuery m_Vehicles;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Vehicles = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>() // anchor; extend for other vehicle base archetypes
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            RequireForUpdate(m_Vehicles);
        }

        protected override void OnUpdate()
        {
            using var entities = m_Vehicles.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                byte mask = 0;

                // Private
                if (EntityManager.HasComponent<PersonalCar>(e))
                    mask = 0b0001;

                // Transit (public transport bus vehicles and taxi)
                if (EntityManager.HasComponent<PublicTransport>(e) ||
                    EntityManager.HasComponent<Taxi>(e))
                    mask = 0b0010;

                // Heavy
                if (EntityManager.HasComponent<DeliveryTruck>(e))
                    mask = 0b0100;

                // Service
                if (EntityManager.HasComponent<PoliceCar>(e) ||
                    EntityManager.HasComponent<Ambulance>(e) ||
                    EntityManager.HasComponent<FireEngine>(e) ||
                    EntityManager.HasComponent<RoadMaintenanceVehicle>(e) ||
                    EntityManager.HasComponent<ParkMaintenanceVehicle>(e) ||
                    EntityManager.HasComponent<GarbageTruck>(e) ||
                    EntityManager.HasComponent<Hearse>(e) ||
                    EntityManager.HasComponent<PrisonerTransport>(e) ||
                    EntityManager.HasComponent<PostVan>(e) ||
                    EntityManager.HasComponent<EvacuatingTransport>(e))
                    mask = 0b1000;

                // Fallback: if nothing matched but it’s still a Car, treat as private.
                if (mask == 0)
                    mask = 0b0001;

                if (EntityManager.HasComponent<VehicleCategoryMask>(e))
                {
                    var current = EntityManager.GetComponentData<VehicleCategoryMask>(e);
                    if (current.Mask != mask)
                    {
                        current.Mask = mask;
                        EntityManager.SetComponentData(e, current);
                    }
                }
                else
                {
                    EntityManager.AddComponentData(e, new VehicleCategoryMask { Mask = mask });
                }
            }
        }
    }
}