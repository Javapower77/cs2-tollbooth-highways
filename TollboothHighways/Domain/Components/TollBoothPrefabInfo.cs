using Game.Companies;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Domain.Components
{
    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class TollBoothPrefabInfo : ComponentBase
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollBoothPrefabData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollBoothPrefabData>());
        }

        public override void Initialize(EntityManager entityManager, Entity entity)
        {
            base.Initialize(entityManager, entity);

            // Set a default name that will be overridden by the spawn system
            var tollBoothData = new TollBoothPrefabData
            {
                BelongsToHighwayTollbooth = Entity.Null // Default to null, will be set by the spawn system
            };

            if (entityManager.HasComponent<TollBoothPrefabData>(entity))
            {
                entityManager.SetComponentData(entity, tollBoothData);
            }
        }
    }
}
