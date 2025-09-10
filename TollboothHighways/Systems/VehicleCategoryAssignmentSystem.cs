using Game;
using Game.Common;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Classifies newly spawned vehicles and assigns VehicleCategoryData.
    /// Runs continuously but only touches vehicles missing the component.
    /// Pure ECS (no Harmony).
    /// </summary>
    public partial class VehicleCategoryAssignmentSystem : GameSystemBase
    {
        private EntityQuery m_NewCars;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_NewCars = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Vehicle>() },
                None = new[]
                {
                    ComponentType.ReadOnly<VehicleCategoryData>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
            RequireForUpdate(m_NewCars);
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var toClassify = m_NewCars.ToEntityArray(Allocator.Temp);
            foreach (var v in toClassify)
            {
                byte mask = 0;

                // Private
                if (em.HasComponent<PersonalCar>(v))
                    mask = 0b0001;

                // Transit (public transport bus vehicles and taxi)
                if (em.HasComponent<PublicTransport>(v) ||
                    em.HasComponent<Taxi>(v))
                    mask = 0b0010;

                // Heavy
                if (em.HasComponent<DeliveryTruck>(v))
                    mask = 0b0100;

                // Service
                if (em.HasComponent<PoliceCar>(v) ||
                    em.HasComponent<Ambulance>(v) ||
                    em.HasComponent<FireEngine>(v) ||
                    em.HasComponent<RoadMaintenanceVehicle>(v) ||
                    em.HasComponent<ParkMaintenanceVehicle>(v) ||
                    em.HasComponent<GarbageTruck>(v) ||
                    em.HasComponent<Hearse>(v) ||
                    em.HasComponent<PrisonerTransport>(v) ||
                    em.HasComponent<PostVan>(v) ||
                    em.HasComponent<EvacuatingTransport>(v))
                    mask = 0b1000;

                // Fallback: if nothing matched but it’s still a Car, treat as private.
                if (mask == 0)
                    mask = 0b0001;

                em.AddComponentData(v, new VehicleCategoryData { Mask = mask });
            }
            toClassify.Dispose();
        }

        private static bool HasPassengers(EntityManager em, Entity v)
            => em.HasBuffer<Passenger>(v);
    }
}