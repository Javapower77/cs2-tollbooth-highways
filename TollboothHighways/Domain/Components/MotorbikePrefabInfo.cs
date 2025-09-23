using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class MotorbikePrefabInfo : ComponentBase, IQueryTypeParameter
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<MotorbikePrefabData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<MotorbikePrefabData>());
        }
    }
}
