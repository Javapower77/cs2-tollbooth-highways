using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Game.Pathfind;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using TollRoadHighways.Domain.Components;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System that enforces vehicle type restrictions on specialized tollbooth roads.
    /// Prevents unauthorized vehicle types from entering restricted toll roads and reroutes them.
    /// </summary>
    public partial class VehicleAccessControlSystem : GameSystemBase
    {
        /// <summary>
        /// Distance threshold for detecting vehicles approaching restricted roads
        /// </summary>
        private const float DetectionDistance = 15f;
        
        /// <summary>
        /// Time in seconds before giving up on rerouting and allowing passage
        /// </summary>
        private const float RerouteTimeoutSeconds = 10f;
        
        /// <summary>
        /// Distance to push vehicles back when access is denied
        /// </summary>
        private const float PushbackDistance = 5f;

        private EntityQuery m_VehicleQuery;
        private EntityQuery m_AccessControlQuery;
        private EntityQuery m_RerouteQuery;
        
        private ComponentLookup<Lane> m_LaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;
        private ComponentLookup<Road> m_RoadLookup;
        private ComponentLookup<Transform> m_TransformLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_TollRoadPrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TollRoadTruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_TollRoadPublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_TollRoadServiceVehiclesLookup;
        private ComponentLookup<TollRoadAllVehiclesData> m_TollRoadAllVehiclesLookup;
        
        // Vehicle component lookups for classification
        private ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarLookup;
        private ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportLookup;
        private ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTruckLookup;
        private ComponentLookup<Game.Vehicles.PoliceCar> m_PoliceCarLookup;
        private ComponentLookup<Game.Vehicles.GarbageTruck> m_GarbageTruckLookup;
        private ComponentLookup<Game.Vehicles.Taxi> m_TaxiLookup;
        private ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceLookup;
        private ComponentLookup<Game.Vehicles.FireEngine> m_FireEngineLookup;
        private ComponentLookup<Game.Vehicles.EvacuatingTransport> m_EvacuatingTransportLookup;
        private ComponentLookup<Game.Vehicles.ParkMaintenanceVehicle> m_ParkMaintenanceLookup;
        private ComponentLookup<Game.Vehicles.RoadMaintenanceVehicle> m_RoadMaintenanceLookup;
        private ComponentLookup<Game.Vehicles.Hearse> m_HearseLookup;
        private ComponentLookup<Game.Vehicles.PrisonerTransport> m_PrisonerTransportLookup;
        private ComponentLookup<Game.Vehicles.PostVan> m_PostVanLookup;
        
        private BufferLookup<Game.Vehicles.LayoutElement> m_LayoutElementLookup;
        private BufferLookup<Game.Vehicles.Passenger> m_PassengerLookup;
        
        private SimulationSystem m_SimulationSystem;
        private EndFrameBarrier m_EndFrameBarrier;

        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            // Query for vehicles that could potentially access restricted roads
            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Game.Vehicles.Car>(),
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadOnly<Transform>(),
                    ComponentType.ReadOnly<Game.Vehicles.Vehicle>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<OutOfControl>(),
                    ComponentType.ReadOnly<VehicleAccessControl>()
                }
            });

            // Query for vehicles currently under access control
            m_AccessControlQuery = GetEntityQuery(
                ComponentType.ReadOnly<VehicleAccessControl>(),
                ComponentType.ReadWrite<CarNavigation>(),
                ComponentType.ReadOnly<Transform>());

            // Query for vehicles that need rerouting
            m_RerouteQuery = GetEntityQuery(
                ComponentType.ReadOnly<VehicleRerouteRequest>(),
                ComponentType.ReadWrite<CarNavigation>(),
                ComponentType.ReadOnly<Transform>());

            RequireForUpdate(m_VehicleQuery);
        }

        protected override void OnUpdate()
        {
            uint currentFrame = m_SimulationSystem.frameIndex;
            float frameRate = 60f;
            uint timeoutFrames = (uint)math.max(1, math.round(RerouteTimeoutSeconds * frameRate));

            // Update component lookups
            UpdateLookups();

            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var ecbParallel = ecb.AsParallelWriter();

            // 1. Clean up expired access control and reroute components
            CleanupExpiredComponents(currentFrame, timeoutFrames, ecbParallel);

            // 2. Handle vehicles currently under access control
            HandleAccessControlledVehicles(currentFrame, timeoutFrames, ecbParallel);

            // 3. Handle vehicles that need rerouting
            HandleRerouteRequests(ecbParallel);

            // 4. Detect new vehicles approaching restricted roads
            DetectRestrictedAccess(currentFrame, ecbParallel);
            DetectRestrictedAccessFallback(currentFrame, ecbParallel);

            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        private void UpdateLookups()
        {
            m_LaneLookup = GetComponentLookup<Lane>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_RoadLookup = GetComponentLookup<Road>(true);
            m_TransformLookup = GetComponentLookup<Transform>(true);
            m_TollRoadPrivateTransportLookup = GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TollRoadTruckLookup = GetComponentLookup<TollRoadTruckData>(true);
            m_TollRoadPublicTransportLookup = GetComponentLookup<TollRoadPublicTransportData>(true);
            m_TollRoadServiceVehiclesLookup = GetComponentLookup<TollRoadServiceVehiclesData>(true);
            m_TollRoadAllVehiclesLookup = GetComponentLookup<TollRoadAllVehiclesData>(true);
            
            // Vehicle component lookups
            m_PersonalCarLookup = GetComponentLookup<Game.Vehicles.PersonalCar>(true);
            m_PublicTransportLookup = GetComponentLookup<Game.Vehicles.PublicTransport>(true);
            m_DeliveryTruckLookup = GetComponentLookup<Game.Vehicles.DeliveryTruck>(true);
            m_PoliceCarLookup = GetComponentLookup<Game.Vehicles.PoliceCar>(true);
            m_GarbageTruckLookup = GetComponentLookup<Game.Vehicles.GarbageTruck>(true);
            m_TaxiLookup = GetComponentLookup<Game.Vehicles.Taxi>(true);
            m_AmbulanceLookup = GetComponentLookup<Game.Vehicles.Ambulance>(true);
            m_FireEngineLookup = GetComponentLookup<Game.Vehicles.FireEngine>(true);
            m_EvacuatingTransportLookup = GetComponentLookup<Game.Vehicles.EvacuatingTransport>(true);
            m_ParkMaintenanceLookup = GetComponentLookup<Game.Vehicles.ParkMaintenanceVehicle>(true);
            m_RoadMaintenanceLookup = GetComponentLookup<Game.Vehicles.RoadMaintenanceVehicle>(true);
            m_HearseLookup = GetComponentLookup<Game.Vehicles.Hearse>(true);
            m_PrisonerTransportLookup = GetComponentLookup<Game.Vehicles.PrisonerTransport>(true);
            m_PostVanLookup = GetComponentLookup<Game.Vehicles.PostVan>(true);
            
            m_LayoutElementLookup = GetBufferLookup<Game.Vehicles.LayoutElement>(true);
            m_PassengerLookup = GetBufferLookup<Game.Vehicles.Passenger>(true);
        }

        private void CleanupExpiredComponents(uint currentFrame, uint timeoutFrames, EntityCommandBuffer.ParallelWriter ecb)
        {
            // Clean up expired access control components
            Entities
                .WithName("CleanupExpiredAccessControl")
                .WithStoreEntityQueryInField(ref m_AccessControlQuery)
                .ForEach((Entity entity, int entityInQueryIndex, in VehicleAccessControl accessControl) =>
                {
                    if (accessControl.AccessDenied && 
                        currentFrame >= accessControl.DeniedStartFrame + timeoutFrames)
                    {
                        ecb.RemoveComponent<VehicleAccessControl>(entityInQueryIndex, entity);
                    }
                }).ScheduleParallel();

            // Clean up reroute requests that have exceeded max attempts
            Entities
                .WithName("CleanupRerouteRequests")
                .WithStoreEntityQueryInField(ref m_RerouteQuery)
                .ForEach((Entity entity, int entityInQueryIndex, in VehicleRerouteRequest rerouteRequest) =>
                {
                    if (rerouteRequest.RerouteAttempts >= VehicleRerouteRequest.MaxRerouteAttempts)
                    {
                        ecb.RemoveComponent<VehicleRerouteRequest>(entityInQueryIndex, entity);
                    }
                }).ScheduleParallel();
        }

        private void HandleAccessControlledVehicles(uint currentFrame, uint timeoutFrames, EntityCommandBuffer.ParallelWriter ecb)
        {
            Entities
                .WithName("HandleAccessControlledVehicles")
                .WithStoreEntityQueryInField(ref m_AccessControlQuery)
                .ForEach((Entity entity, int entityInQueryIndex,
                          ref CarNavigation navigation,
                          in Transform vehicleTransform,
                          in VehicleAccessControl accessControl) =>
                {
                    if (accessControl.AccessDenied)
                    {
                        // Check if timeout has been reached
                        if (currentFrame >= accessControl.DeniedStartFrame + timeoutFrames)
                        {
                            // Timeout reached, allow passage and remove component
                            ecb.RemoveComponent<VehicleAccessControl>(entityInQueryIndex, entity);
                            return;
                        }

                        // Calculate pushback position (away from the restricted road)
                        float3 currentPos = vehicleTransform.m_Position;
                        float3 originalTarget = accessControl.OriginalTargetPosition;
                        float3 directionAway = math.normalizesafe(currentPos - originalTarget);
                        float3 pushbackPosition = currentPos + directionAway * PushbackDistance;

                        // Set navigation to pushback position
                        navigation.m_TargetPosition = pushbackPosition;
                        
                        // Add reroute request to find alternative path
                        ecb.AddComponent(entityInQueryIndex, entity, new VehicleRerouteRequest
                        {
                            RestrictedRoad = accessControl.RestrictedRoad,
                            RerouteAttempts = 0
                        });
                    }
                }).ScheduleParallel();
        }

        private void HandleRerouteRequests(EntityCommandBuffer.ParallelWriter ecb)
        {
            Entities
                .WithName("HandleRerouteRequests")
                .WithStoreEntityQueryInField(ref m_RerouteQuery)
                .ForEach((Entity entity, int entityInQueryIndex,
                          ref CarNavigation navigation,
                          ref VehicleRerouteRequest rerouteRequest,
                          in Transform vehicleTransform) =>
                {
                    // Increment reroute attempts
                    rerouteRequest.RerouteAttempts++;

                    if (rerouteRequest.RerouteAttempts >= VehicleRerouteRequest.MaxRerouteAttempts)
                    {
                        // Max attempts reached, remove reroute request
                        ecb.RemoveComponent<VehicleRerouteRequest>(entityInQueryIndex, entity);
                        return;
                    }

                    // Request pathfinding to recalculate route avoiding the restricted road
                    // This would integrate with the game's pathfinding system
                    // For now, we'll clear the current path to force recalculation
                    navigation.m_TargetPosition = vehicleTransform.m_Position;
                }).ScheduleParallel();
        }

        private void DetectRestrictedAccess(uint currentFrame, EntityCommandBuffer.ParallelWriter ecb)
        {
            var laneLookup = m_LaneLookup;
            var ownerLookup = m_OwnerLookup;
            var roadLookup = m_RoadLookup;

            // Road component lookups for restrictions
            var tollRoadPrivateTransportLookup = m_TollRoadPrivateTransportLookup;
            var tollRoadTruckLookup = m_TollRoadTruckLookup;
            var tollRoadPublicTransportLookup = m_TollRoadPublicTransportLookup;
            var tollRoadServiceVehiclesLookup = m_TollRoadServiceVehiclesLookup;
            var tollRoadAllVehiclesLookup = m_TollRoadAllVehiclesLookup;

            // Vehicle component lookups
            var personalCarLookup = m_PersonalCarLookup;
            var publicTransportVehicleLookup = m_PublicTransportLookup;
            var deliveryTruckLookup = m_DeliveryTruckLookup;
            var policeCarLookup = m_PoliceCarLookup;
            var garbageTruckLookup = m_GarbageTruckLookup;
            var taxiLookup = m_TaxiLookup;
            var ambulanceLookup = m_AmbulanceLookup;
            var fireEngineLookup = m_FireEngineLookup;
            var evacuatingTransportLookup = m_EvacuatingTransportLookup;
            var parkMaintenanceLookup = m_ParkMaintenanceLookup;
            var roadMaintenanceLookup = m_RoadMaintenanceLookup;
            var hearseLookup = m_HearseLookup;
            var prisonerTransportLookup = m_PrisonerTransportLookup;
            var postVanLookup = m_PostVanLookup;
            var layoutElementLookup = m_LayoutElementLookup;
            var passengerLookup = m_PassengerLookup;

            Entities
                .WithName("DetectRestrictedAccess")
                .WithStoreEntityQueryInField(ref m_VehicleQuery)
                .WithReadOnly(laneLookup)
                .WithReadOnly(ownerLookup)
                .WithReadOnly(roadLookup)
                .WithReadOnly(tollRoadPrivateTransportLookup)
                .WithReadOnly(tollRoadTruckLookup)
                .WithReadOnly(tollRoadPublicTransportLookup)
                .WithReadOnly(tollRoadServiceVehiclesLookup)
                .WithReadOnly(tollRoadAllVehiclesLookup)
                .WithReadOnly(personalCarLookup)
                .WithReadOnly(publicTransportVehicleLookup)
                .WithReadOnly(deliveryTruckLookup)
                .WithReadOnly(policeCarLookup)
                .WithReadOnly(garbageTruckLookup)
                .WithReadOnly(taxiLookup)
                .WithReadOnly(ambulanceLookup)
                .WithReadOnly(fireEngineLookup)
                .WithReadOnly(evacuatingTransportLookup)
                .WithReadOnly(parkMaintenanceLookup)
                .WithReadOnly(roadMaintenanceLookup)
                .WithReadOnly(hearseLookup)
                .WithReadOnly(prisonerTransportLookup)
                .WithReadOnly(postVanLookup)
                .WithReadOnly(layoutElementLookup)
                .WithReadOnly(passengerLookup)
                .ForEach((Entity entity, int entityInQueryIndex,
                          ref CarNavigation navigation,
                          in CarCurrentLane currentLane,
                          in Transform vehicleTransform,
                          in Game.Vehicles.Vehicle vehicle) =>
                {
                    Entity laneEntity = currentLane.m_Lane;
                    if (laneEntity == Entity.Null || !laneLookup.HasComponent(laneEntity))
                        return;

                    if (!ownerLookup.TryGetComponent(laneEntity, out var owner))
                        return;

                    Entity roadEntity = owner.m_Owner;
                    if (roadEntity == Entity.Null || !roadLookup.HasComponent(roadEntity))
                        return;

                    // Check if this road has any vehicle restrictions
                    if (!HasVehicleRestrictions(roadEntity, tollRoadPrivateTransportLookup, tollRoadTruckLookup, 
                                              tollRoadPublicTransportLookup, tollRoadServiceVehiclesLookup, tollRoadAllVehiclesLookup))
                        return;

                    // Get vehicle type using Burst-compatible logic
                    VehicleType vehicleType = GetVehicleTypeBurstCompatible(entity, 
                        personalCarLookup, publicTransportVehicleLookup, deliveryTruckLookup, 
                        policeCarLookup, garbageTruckLookup, taxiLookup, ambulanceLookup, 
                        fireEngineLookup, evacuatingTransportLookup, parkMaintenanceLookup, 
                        roadMaintenanceLookup, hearseLookup, prisonerTransportLookup, postVanLookup,
                        layoutElementLookup, passengerLookup);

                    if (vehicleType == VehicleType.None)
                        return;

                    VehicleGroup vehicleGroup = GetVehicleGroup(vehicleType);

                    // Check if vehicle is allowed on this road
                    bool accessAllowed = IsVehicleAllowed(roadEntity, vehicleGroup, 
                                                        tollRoadPrivateTransportLookup, tollRoadTruckLookup,
                                                        tollRoadPublicTransportLookup, tollRoadServiceVehiclesLookup, 
                                                        tollRoadAllVehiclesLookup);

                    if (!accessAllowed)
                    {
                        // Access denied - add access control component
                        ecb.AddComponent(entityInQueryIndex, entity, new VehicleAccessControl
                        {
                            RestrictedRoad = roadEntity,
                            DetectedVehicleType = vehicleType,
                            VehicleGroup = vehicleGroup,
                            AccessDenied = true,
                            DeniedStartFrame = currentFrame,
                            OriginalTargetPosition = navigation.m_TargetPosition
                        });
                    }
                }).ScheduleParallel();
        }

        /// <summary>
        /// Fallback detection - should rarely trigger if pathfinding works correctly
        /// </summary>
        private void DetectRestrictedAccessFallback(uint currentFrame, EntityCommandBuffer.ParallelWriter ecb)
        {
            Entities
                .WithName("DetectRestrictedAccess_Fallback")
                .WithStoreEntityQueryInField(ref m_VehicleQuery)
                .ForEach((Entity entity, int entityInQueryIndex,
                          ref CarNavigation navigation,
                          in CarCurrentLane currentLane,
                          in Transform vehicleTransform,
                          in Game.Vehicles.Vehicle vehicle) =>
                {
                    Entity laneEntity = currentLane.m_Lane;
                    if (laneEntity == Entity.Null || !m_LaneLookup.HasComponent(laneEntity))
                        return;

                    if (!m_OwnerLookup.TryGetComponent(laneEntity, out var owner))
                        return;

                    Entity roadEntity = owner.m_Owner;
                    if (roadEntity == Entity.Null || !m_RoadLookup.HasComponent(roadEntity))
                        return;

                    // Check if this road has any vehicle restrictions
                    if (!HasVehicleRestrictions(roadEntity, m_TollRoadPrivateTransportLookup, m_TollRoadTruckLookup, 
                                              m_TollRoadPublicTransportLookup, m_TollRoadServiceVehiclesLookup, m_TollRoadAllVehiclesLookup))
                        return;

                    // Get vehicle type using Burst-compatible logic
                    VehicleType vehicleType = GetVehicleTypeBurstCompatible(entity, 
                        m_PersonalCarLookup, m_PublicTransportLookup, m_DeliveryTruckLookup, 
                        m_PoliceCarLookup, m_GarbageTruckLookup, m_TaxiLookup, m_AmbulanceLookup, 
                        m_FireEngineLookup, m_EvacuatingTransportLookup, m_ParkMaintenanceLookup, 
                        m_RoadMaintenanceLookup, m_HearseLookup, m_PrisonerTransportLookup, m_PostVanLookup,
                        m_LayoutElementLookup, m_PassengerLookup);

                    if (vehicleType == VehicleType.None)
                        return;

                    VehicleGroup vehicleGroup = GetVehicleGroup(vehicleType);

                    // Check if vehicle is allowed on this road
                    bool accessAllowed = IsVehicleAllowed(roadEntity, vehicleGroup, 
                                                        m_TollRoadPrivateTransportLookup, m_TollRoadTruckLookup,
                                                        m_TollRoadPublicTransportLookup, m_TollRoadServiceVehiclesLookup, 
                                                        m_TollRoadAllVehiclesLookup);

                    if (!accessAllowed)
                    {
                        // Log this as an unexpected case since pathfinding should have prevented this
                        LogUtil.Warn($"VehicleAccessControlSystem: Fallback restriction triggered for vehicle {entity.Index} " +
                                    $"of type {vehicleType} on restricted road {roadEntity.Index}. " +
                                    $"Pathfinding should have prevented this.");

                        // Try to find an alternative route by forcing a repath
                        ecb.AddComponent(entityInQueryIndex, entity, new PathfindUpdated());
                        
                        // Temporarily stop the vehicle
                        navigation.m_MaxSpeed = 0f;
                        
                        // Add a temporary restriction marker
                        ecb.AddComponent(entityInQueryIndex, entity, new VehicleAccessControl
                        {
                            RestrictedRoad = roadEntity,
                            DetectedVehicleType = vehicleType,
                            VehicleGroup = vehicleGroup,
                            AccessDenied = true,
                            DeniedStartFrame = currentFrame,
                            OriginalTargetPosition = navigation.m_TargetPosition
                        });
                    }
                }).ScheduleParallel();
        }

        private static VehicleType GetVehicleTypeBurstCompatible(
            Entity vehicleEntity,
            ComponentLookup<Game.Vehicles.PersonalCar> personalCarLookup,
            ComponentLookup<Game.Vehicles.PublicTransport> publicTransportLookup,
            ComponentLookup<Game.Vehicles.DeliveryTruck> deliveryTruckLookup,
            ComponentLookup<Game.Vehicles.PoliceCar> policeCarLookup,
            ComponentLookup<Game.Vehicles.GarbageTruck> garbageTruckLookup,
            ComponentLookup<Game.Vehicles.Taxi> taxiLookup,
            ComponentLookup<Game.Vehicles.Ambulance> ambulanceLookup,
            ComponentLookup<Game.Vehicles.FireEngine> fireEngineLookup,
            ComponentLookup<Game.Vehicles.EvacuatingTransport> evacuatingTransportLookup,
            ComponentLookup<Game.Vehicles.ParkMaintenanceVehicle> parkMaintenanceLookup,
            ComponentLookup<Game.Vehicles.RoadMaintenanceVehicle> roadMaintenanceLookup,
            ComponentLookup<Game.Vehicles.Hearse> hearseLookup,
            ComponentLookup<Game.Vehicles.PrisonerTransport> prisonerTransportLookup,
            ComponentLookup<Game.Vehicles.PostVan> postVanLookup,
            BufferLookup<Game.Vehicles.LayoutElement> layoutElementLookup,
            BufferLookup<Game.Vehicles.Passenger> passengerLookup)
        {
            // Check if the vehicle has a trailer (could be car or truck)
            if (layoutElementLookup.HasBuffer(vehicleEntity))
            {
                var vehicleLayout = layoutElementLookup[vehicleEntity];
                if (vehicleLayout.Length > 1)
                {
                    // Check if any element in the layout is a personal car
                    for (int i = 0; i < math.min(vehicleLayout.Length, 2); i++)
                    {
                        if (personalCarLookup.HasComponent(vehicleLayout[i].m_Vehicle))
                        {
                            return VehicleType.PersonalCarWithTrailer;
                        }
                    }
                    return VehicleType.TruckWithTrailer;
                }
            }

            // Check specific vehicle types
            if (publicTransportLookup.HasComponent(vehicleEntity))
                return VehicleType.Bus;
            if (deliveryTruckLookup.HasComponent(vehicleEntity))
                return VehicleType.Truck;
            if (policeCarLookup.HasComponent(vehicleEntity))
                return VehicleType.PoliceCar;
            if (garbageTruckLookup.HasComponent(vehicleEntity))
                return VehicleType.GarbageTruck;
            if (taxiLookup.HasComponent(vehicleEntity))
                return VehicleType.Taxi;
            if (ambulanceLookup.HasComponent(vehicleEntity))
                return VehicleType.Ambulance;
            if (fireEngineLookup.HasComponent(vehicleEntity))
                return VehicleType.FireEngine;
            if (evacuatingTransportLookup.HasComponent(vehicleEntity))
                return VehicleType.EvacuatingTransport;
            if (parkMaintenanceLookup.HasComponent(vehicleEntity))
                return VehicleType.ParkMaintenance;
            if (roadMaintenanceLookup.HasComponent(vehicleEntity))
                return VehicleType.RoadMaintenance;
            if (hearseLookup.HasComponent(vehicleEntity))
                return VehicleType.Hearse;
            if (prisonerTransportLookup.HasComponent(vehicleEntity))
                return VehicleType.PrisonerTransport;
            if (postVanLookup.HasComponent(vehicleEntity))
                return VehicleType.PostVan;

            // Check passenger vehicles (personal car or motorcycle)
            if (passengerLookup.HasBuffer(vehicleEntity))
            {
                var passengers = passengerLookup[vehicleEntity];
                if (passengers.Length == 1)
                {
                    return VehicleType.Motorcycle;
                }
                if (passengers.Length == 0)
                {
                    return VehicleType.PersonalCar;
                }
            }

            return VehicleType.None;
        }

        private static VehicleGroup GetVehicleGroup(VehicleType vehicleType)
        {
            switch (vehicleType)
            {
                case VehicleType.PersonalCar:
                case VehicleType.PersonalCarWithTrailer:
                case VehicleType.Motorcycle:
                    return VehicleGroup.PrivateTransport;

                case VehicleType.Truck:
                case VehicleType.TruckWithTrailer:
                    return VehicleGroup.Trucks;

                case VehicleType.Bus:
                case VehicleType.Taxi:
                    return VehicleGroup.PublicTransport;

                case VehicleType.ParkMaintenance:
                case VehicleType.RoadMaintenance:
                case VehicleType.Ambulance:
                case VehicleType.EvacuatingTransport:
                case VehicleType.FireEngine:
                case VehicleType.GarbageTruck:
                case VehicleType.Hearse:
                case VehicleType.PoliceCar:
                case VehicleType.PostVan:
                case VehicleType.PrisonerTransport:
                    return VehicleGroup.ServiceVehicles;

                default:
                    return VehicleGroup.PrivateTransport; // Default fallback
            }
        }

        private static bool HasVehicleRestrictions(
            Entity roadEntity,
            ComponentLookup<TollRoadPrivateTransportData> privateTransportLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicTransportLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceVehiclesLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            return privateTransportLookup.HasComponent(roadEntity) ||
                   truckLookup.HasComponent(roadEntity) ||
                   publicTransportLookup.HasComponent(roadEntity) ||
                   serviceVehiclesLookup.HasComponent(roadEntity) ||
                   allVehiclesLookup.HasComponent(roadEntity);
        }

        private static bool IsVehicleAllowed(
            Entity roadEntity,
            VehicleGroup vehicleGroup,
            ComponentLookup<TollRoadPrivateTransportData> privateTransportLookup,
            ComponentLookup<TollRoadTruckData> truckLookup,
            ComponentLookup<TollRoadPublicTransportData> publicTransportLookup,
            ComponentLookup<TollRoadServiceVehiclesData> serviceVehiclesLookup,
            ComponentLookup<TollRoadAllVehiclesData> allVehiclesLookup)
        {
            // AllVehicles roads allow everyone
            if (allVehiclesLookup.HasComponent(roadEntity))
                return true;

            // Check specific vehicle group restrictions
            switch (vehicleGroup)
            {
                case VehicleGroup.PrivateTransport:
                    return privateTransportLookup.HasComponent(roadEntity);
                
                case VehicleGroup.Trucks:
                    return truckLookup.HasComponent(roadEntity);
                
                case VehicleGroup.PublicTransport:
                    return publicTransportLookup.HasComponent(roadEntity);
                
                case VehicleGroup.ServiceVehicles:
                    return serviceVehiclesLookup.HasComponent(roadEntity);
                
                default:
                    return false;
            }
        }
    }
}