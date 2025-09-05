using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Entities;
namespace TollRoadHighways.Domain.Components
{
    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class TollRoadPrivateTransportInfo : ComponentBase
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadPrivateTransportData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadPrivateTransportData>());
        }
    }
    public struct TollRoadPrivateTransportData : IComponentData
    {
        // This is a marker component - no data needed
        // Indicates the entity is for private transport vehicles
    }

    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class TollRoadTruckInfo : ComponentBase
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadTruckData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadTruckData>());
        }
    }

    public struct TollRoadTruckData : IComponentData
    {
        // This is a marker component - no data needed
        // Indicates the entity is for trucks
    }

    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class TollRoadPublicTransportInfo : ComponentBase
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadPublicTransportData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadPublicTransportData>());
        }
    }

    public struct TollRoadPublicTransportData : IComponentData
    {
        // This is a marker component - no data needed
        // Indicates the entity is for public transport vehicles
    }

    [ComponentMenu("TollHighways/", new Type[] { typeof(WithNoneAttribute) })]
    public class TollRoadServiceVehiclesInfo : ComponentBase
    {
        public override void GetArchetypeComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadServiceVehiclesData>());
        }

        public override void GetPrefabComponents(HashSet<ComponentType> components)
        {
            components.Add(ComponentType.ReadWrite<TollRoadServiceVehiclesData>());
        }
    }

    public struct TollRoadServiceVehiclesData : IComponentData
    {
        // This is a marker component - no data needed
        // Indicates the entity is for service vehicles
    }
}
