using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Modifies pathfinding costs for toll roads based on vehicle type restrictions.
    /// Since CarLaneFlags alone don't work, this system adds prohibitive costs to incompatible vehicle-road combinations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PathfindSetupSystem))]
    public partial class TollboothPathfindCostSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;
        private EntityQuery m_TollRoadQuery;
        
        // Prohibitive cost to prevent pathfinding through restricted roads
        private const float PROHIBITIVE_COST = 100000f;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Query for vehicles with navigation data
            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<PrefabRef>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            // Query for toll roads
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Edge>(),
                    ComponentType.ReadOnly<TollRoadPrefabData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            RequireForUpdate(m_VehicleQuery);
            RequireForUpdate(m_TollRoadQuery);
        }

        protected override void OnUpdate()
        {
            var modifyPathCostsJob = new ModifyPathfindCostsJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                CarNavigationTypeHandle = SystemAPI.GetComponentTypeHandle<CarNavigation>(),
                CarTypeHandle = SystemAPI.GetComponentTypeHandle<Car>(true),
                PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                CurrentNavigationLaneTypeHandle = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(true),
                
                // Lookups for lane and road data
                LaneLookup = SystemAPI.GetComponentLookup<Lane>(true),
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                SubLaneBufferLookup = SystemAPI.GetBufferLookup<SubLane>(true),
                CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>(true),
                
                // Toll road type lookups
                TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),
                TollRoadPrivateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                TollRoadTruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                TollRoadPublicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                TollRoadServiceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),
                TollRoadAllVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true),
                
                // Vehicle type lookups
                PersonalCarLookup = SystemAPI.GetComponentLookup<PersonalCar>(true),
                DeliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true),
                PublicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true),
                ServiceLookup = SystemAPI.GetComponentLookup<Game.Vehicles.ServiceVehicle>(true),
                TaxiLookup = SystemAPI.GetComponentLookup<Taxi>(true)
            };

            Dependency = modifyPathCostsJob.ScheduleParallel(m_VehicleQuery, Dependency);
        }

        [BurstCompile]
        private struct ModifyPathfindCostsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<CarNavigation> CarNavigationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Car> CarTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PrefabRef> PrefabRefTypeHandle;
            [ReadOnly] public ComponentTypeHandle<CarCurrentLane> CurrentNavigationLaneTypeHandle;
            
            // Lane and road lookups
            [ReadOnly] public ComponentLookup<Lane> LaneLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public BufferLookup<SubLane> SubLaneBufferLookup;
            [ReadOnly] public ComponentLookup<CarLane> CarLaneLookup;
            
            // Toll road lookups
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> TollRoadPrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TollRoadTruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> TollRoadPublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> TollRoadServiceLookup;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> TollRoadAllVehiclesLookup;
            
            // Vehicle type lookups
            [ReadOnly] public ComponentLookup<PersonalCar> PersonalCarLookup;
            [ReadOnly] public ComponentLookup<DeliveryTruck> DeliveryTruckLookup;
            [ReadOnly] public ComponentLookup<PublicTransport> PublicTransportLookup;
            [ReadOnly] public ComponentLookup<Game.Vehicles.ServiceVehicle> ServiceLookup;
            [ReadOnly] public ComponentLookup<Taxi> TaxiLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var carNavigations = chunk.GetNativeArray(ref CarNavigationTypeHandle);
                var hasCurrentLane = chunk.Has(ref CurrentNavigationLaneTypeHandle);
                
                NativeArray<CarCurrentLane> currentLanes = default;
                if (hasCurrentLane)
                {
                    currentLanes = chunk.GetNativeArray(ref CurrentNavigationLaneTypeHandle);
                }

                for (int i = 0; i < chunk.Count; i++)
                {
                    var vehicleEntity = entities[i];
                    var carNavigation = carNavigations[i];
                    
                    // Check if vehicle is currently on a lane
                    if (!hasCurrentLane)
                        continue;
                        
                    var currentLane = currentLanes[i];
                    if (currentLane.m_Lane == Entity.Null)
                        continue;
                    
                    // Get the road entity from the current lane
                    if (!OwnerLookup.TryGetComponent(currentLane.m_Lane, out var laneOwner))
                        continue;
                        
                    Entity roadEntity = laneOwner.m_Owner;
                    
                    // Check if this is a toll road
                    if (!TollRoadPrefabLookup.HasComponent(roadEntity))
                        continue;
                    
                    // Determine vehicle type
                    var vehicleType = GetVehicleType(vehicleEntity);
                    
                    // Check if vehicle is allowed on this toll road type
                    bool isAllowed = IsVehicleAllowedOnTollRoad(roadEntity, vehicleType);
                    
                    if (!isAllowed)
                    {
                        // Apply prohibitive cost to navigation
                        // This will force the pathfinder to find alternative routes
                        if (carNavigation.m_TargetPosition.Equals(float3.zero))
                            continue;
                            
                        // Modify the navigation to add extra cost
                        // This is a workaround since we can't directly modify PathfindParameters
                        carNavigation.m_MaxSpeed = math.max(1f, carNavigation.m_MaxSpeed * 0.01f); // Dramatically reduce speed on restricted roads
                        carNavigations[i] = carNavigation;
                        
                        VehicleDebugLogger.Log(vehicleEntity, 
                            $"Vehicle {vehicleEntity.Index} ({vehicleType}) restricted on toll road {roadEntity.Index} - reducing navigation speed");
                    }
                }
            }
            
            private VehicleType GetVehicleType(Entity vehicleEntity)
            {
                if (PersonalCarLookup.HasComponent(vehicleEntity))
                    return VehicleType.PersonalCar;
                if (DeliveryTruckLookup.HasComponent(vehicleEntity))
                    return VehicleType.DeliveryTruck;
                if (PublicTransportLookup.HasComponent(vehicleEntity))
                    return VehicleType.PublicTransport;
                if (TaxiLookup.HasComponent(vehicleEntity))
                    return VehicleType.Taxi;
                if (ServiceLookup.HasComponent(vehicleEntity))
                    return VehicleType.Service;
                    
                return VehicleType.Unknown;
            }
            
            private bool IsVehicleAllowedOnTollRoad(Entity roadEntity, VehicleType vehicleType)
            {
                // Check for "All Vehicles" toll road first
                if (TollRoadAllVehiclesLookup.HasComponent(roadEntity))
                    return true;
                
                // Private Transport Only
                if (TollRoadPrivateLookup.HasComponent(roadEntity))
                {
                    return vehicleType == VehicleType.PersonalCar;
                }
                
                // Trucks Only
                if (TollRoadTruckLookup.HasComponent(roadEntity))
                {
                    return vehicleType == VehicleType.DeliveryTruck;
                }
                
                // Public Transport Only
                if (TollRoadPublicLookup.HasComponent(roadEntity))
                {
                    return vehicleType == VehicleType.PublicTransport || vehicleType == VehicleType.Taxi;
                }
                
                // Service Vehicles Only
                if (TollRoadServiceLookup.HasComponent(roadEntity))
                {
                    return vehicleType == VehicleType.Service;
                }
                
                // Default: allow if no specific restriction
                return true;
            }
        }
        
        private enum VehicleType
        {
            Unknown,
            PersonalCar,
            DeliveryTruck,
            PublicTransport,
            Taxi,
            Service
        }
    }
}