using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game;
using Game.Common;
using Game.Net;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Game.Pathfind;
using Game.Tools;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Actively prevents incompatible vehicles from entering toll roads by redirecting them.
    /// This is a fallback system when CarLaneFlags don't work as expected.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TollboothPathfindCostSystem))]
    public partial class TollboothVehicleRestrictionSystem : GameSystemBase
    {
        private EntityQuery m_VehicleQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadWrite<PathOwner>(),
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<CarCurrentLane>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            RequireForUpdate(m_VehicleQuery);
        }

        protected override void OnUpdate()
        {
            var restrictVehiclesJob = new RestrictVehiclesJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                CarNavigationTypeHandle = SystemAPI.GetComponentTypeHandle<CarNavigation>(),
                PathOwnerTypeHandle = SystemAPI.GetComponentTypeHandle<PathOwner>(),
                CarCurrentLaneTypeHandle = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(true),
                
                // Lookups
                OwnerLookup = SystemAPI.GetComponentLookup<Owner>(true),
                CarLaneLookup = SystemAPI.GetComponentLookup<CarLane>(true),
                PathElementBufferLookup = SystemAPI.GetBufferLookup<PathElement>(true),
                
                // Toll road lookups
                TollRoadPrefabLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true),
                TollRoadPrivateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true),
                TollRoadTruckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true),
                TollRoadPublicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true),
                TollRoadServiceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true),
                
                // Vehicle type lookups from VehiclesUtil
                PersonalCarLookup = SystemAPI.GetComponentLookup<PersonalCar>(true),
                DeliveryTruckLookup = SystemAPI.GetComponentLookup<DeliveryTruck>(true),
                PublicTransportLookup = SystemAPI.GetComponentLookup<PublicTransport>(true),
                TaxiLookup = SystemAPI.GetComponentLookup<Taxi>(true)
            };

            Dependency = restrictVehiclesJob.ScheduleParallel(m_VehicleQuery, Dependency);
        }

        [BurstCompile]
        private struct RestrictVehiclesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<CarNavigation> CarNavigationTypeHandle;
            public ComponentTypeHandle<PathOwner> PathOwnerTypeHandle;
            [ReadOnly] public ComponentTypeHandle<CarCurrentLane> CarCurrentLaneTypeHandle;
            
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<CarLane> CarLaneLookup;
            [ReadOnly] public BufferLookup<PathElement> PathElementBufferLookup;
            
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadPrefabLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> TollRoadPrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TollRoadTruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> TollRoadPublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> TollRoadServiceLookup;
            
            [ReadOnly] public ComponentLookup<PersonalCar> PersonalCarLookup;
            [ReadOnly] public ComponentLookup<DeliveryTruck> DeliveryTruckLookup;
            [ReadOnly] public ComponentLookup<PublicTransport> PublicTransportLookup;
            [ReadOnly] public ComponentLookup<Taxi> TaxiLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var carNavigations = chunk.GetNativeArray(ref CarNavigationTypeHandle);
                var pathOwners = chunk.GetNativeArray(ref PathOwnerTypeHandle);
                var currentLanes = chunk.GetNativeArray(ref CarCurrentLaneTypeHandle);

                for (int i = 0; i < chunk.Count; i++)
                {
                    var vehicleEntity = entities[i];
                    var currentLane = currentLanes[i];
                    var pathOwner = pathOwners[i];
                    var carNavigation = carNavigations[i];
                    
                    // Check if vehicle has a valid current lane
                    if (currentLane.m_Lane == Entity.Null)
                        continue;
                    
                    // Get road entity from lane
                    if (!OwnerLookup.TryGetComponent(currentLane.m_Lane, out var laneOwner))
                        continue;
                        
                    Entity roadEntity = laneOwner.m_Owner;
                    
                    // Check if approaching or on a toll road
                    if (!TollRoadPrefabLookup.HasComponent(roadEntity))
                    {
                        // Check next path element for upcoming toll road
                        if (!CheckUpcomingTollRoad(ref pathOwner, vehicleEntity, out roadEntity))
                            continue;
                    }
                    
                    // Check vehicle compatibility with toll road
                    if (!VehiclesUtil.IsVehicleAllowedOnTollRoad(
                        vehicleEntity, 
                        roadEntity, 
                        PersonalCarLookup, 
                        DeliveryTruckLookup, 
                        PublicTransportLookup, 
                        TaxiLookup,
                        TollRoadPrivateLookup, 
                        TollRoadTruckLookup, 
                        TollRoadPublicLookup, 
                        TollRoadServiceLookup))
                    {
                        // Force recalculation of path
                        pathOwner.m_State |= PathFlags.Obsolete;
                        pathOwners[i] = pathOwner;
                        
                        // Set flag to force vehicle to stop and reroute
                        carNavigation.m_TargetPosition = carNavigation.m_PreviousPosition;
                        carNavigations[i] = carNavigation;
                        
                        VehicleDebugLogger.Log(vehicleEntity, 
                            $"Blocking vehicle {vehicleEntity.Index} from toll road {roadEntity.Index} - forcing reroute");
                    }
                }
            }
            
            private bool CheckUpcomingTollRoad(ref PathOwner pathOwner, Entity vehicleEntity, out Entity tollRoadEntity)
            {
                tollRoadEntity = Entity.Null;
                
                // Check next few path elements for toll roads
                if (!PathElementBufferLookup.TryGetBuffer(vehicleEntity, out var pathElements))
                    return false;
                    
                int currentElement = pathOwner.m_ElementIndex;
                int lookahead = math.min(3, pathElements.Length - currentElement);
                
                for (int j = 0; j < lookahead; j++)
                {
                    int idx = currentElement + j;
                    if (idx >= pathElements.Length)
                        break;
                        
                    var element = pathElements[idx];
                    if (element.m_Target == Entity.Null)
                        continue;
                        
                    // Check if this element is a lane with toll road owner
                    if (OwnerLookup.TryGetComponent(element.m_Target, out var owner))
                    {
                        if (TollRoadPrefabLookup.HasComponent(owner.m_Owner))
                        {
                            tollRoadEntity = owner.m_Owner;
                            return true;
                        }
                    }
                }
                
                return false;
            }
        }
    }
}