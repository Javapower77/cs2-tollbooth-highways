using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors toll road usage and logs vehicle type validations.
    /// 
    /// Per AGENTS.MD: Burst-compatible, parallel execution, uses SystemAPI.
    /// Uses ComponentTypeHandle for chunk-based iteration.
    /// </summary>
    public sealed partial class TollboothPathValidationSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private EntityQuery m_CarOnLaneQuery;
        
        // Logging control
        private uint m_LastLogFrame;
        private const uint LOG_INTERVAL_FRAMES = 300;
        
        // Burst-compatible job logger
        private JobLogger m_JobLogger;
        private JobLogger m_SummaryLogger;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_JobLogger = new JobLogger();
            m_SummaryLogger = new JobLogger();
            
            m_JobLogger.Initialize(Allocator.Persistent, initialCapacity: 256, isEnabled: false);
            m_SummaryLogger.Initialize(Allocator.Persistent, initialCapacity: 64, isEnabled: false);
            
            // Query toll roads (edges with TollRoadPrefabData)
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
            
            // Query cars with current lane info
            m_CarOnLaneQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadOnly<CarCurrentLane>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });
        }
        
        protected override void OnDestroy()
        {
            m_JobLogger.Dispose();
            m_SummaryLogger.Dispose();
            base.OnDestroy();
        }
        
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return 16; // Check frequently for responsive validation
        }
        
        protected override void OnUpdate()
        {
            bool isJobLoggingEnabled = ModSettings.Instance?.EnableJobsLogging == true;
            m_JobLogger.SetEnabled(isJobLoggingEnabled);
            m_SummaryLogger.SetEnabled(isJobLoggingEnabled);
            
            var simulationSystem = World.GetExistingSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem?.frameIndex ?? 0;
            
            // Log summary occasionally
            if (isJobLoggingEnabled && currentFrame - m_LastLogFrame > LOG_INTERVAL_FRAMES)
            {
                LogTollRoadSummary();
                m_LastLogFrame = currentFrame;
            }
            
            // Build toll road lookup set
            var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.TempJob);
            var tollRoadSet = new NativeParallelHashSet<Entity>(tollRoadEntities.Length * 2, Allocator.TempJob);
            
            for (int i = 0; i < tollRoadEntities.Length; i++)
            {
                tollRoadSet.Add(tollRoadEntities[i]);
            }
            
            // Get type handles per AGENTS.MD best practices
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
            var carTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.Car>(true);
            var carCurrentLaneTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.CarCurrentLane>(true);
            var personalCarTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PersonalCar>(true);
            var deliveryTruckTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.DeliveryTruck>(true);
            var publicTransportTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PublicTransport>(true);
            var taxiTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.Taxi>(true);
            var policeCarTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PoliceCar>(true);
            var ambulanceTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.Ambulance>(true);
            var fireEngineTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.FireEngine>(true);
            var garbageTruckTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.GarbageTruck>(true);
            var hearseTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.Hearse>(true);
            var maintenanceTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.MaintenanceVehicle>(true);
            var postVanTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PostVan>(true);
            var prisonerTransportTypeHandle = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PrisonerTransport>(true);
            
            // Get component lookups for toll road checks
            var ownerLookup = SystemAPI.GetComponentLookup<Owner>(true);
            var edgeLookup = SystemAPI.GetComponentLookup<Edge>(true);
            var tollRoadLookup = SystemAPI.GetComponentLookup<TollRoadPrefabData>(true);
            var privateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            var truckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            var publicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            var serviceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            var allVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true);
            
            // Schedule validation job
            var validateJob = new ValidateVehiclesOnTollRoadsJob
            {
                EntityTypeHandle = entityTypeHandle,
                CarTypeHandle = carTypeHandle,
                CarCurrentLaneTypeHandle = carCurrentLaneTypeHandle,
                PersonalCarTypeHandle = personalCarTypeHandle,
                DeliveryTruckTypeHandle = deliveryTruckTypeHandle,
                PublicTransportTypeHandle = publicTransportTypeHandle,
                TaxiTypeHandle = taxiTypeHandle,
                PoliceCarTypeHandle = policeCarTypeHandle,
                AmbulanceTypeHandle = ambulanceTypeHandle,
                FireEngineTypeHandle = fireEngineTypeHandle,
                GarbageTruckTypeHandle = garbageTruckTypeHandle,
                HearseTypeHandle = hearseTypeHandle,
                MaintenanceTypeHandle = maintenanceTypeHandle,
                PostVanTypeHandle = postVanTypeHandle,
                PrisonerTransportTypeHandle = prisonerTransportTypeHandle,
                
                OwnerLookup = ownerLookup,
                EdgeLookup = edgeLookup,
                TollRoadLookup = tollRoadLookup,
                PrivateLookup = privateLookup,
                TruckLookup = truckLookup,
                PublicLookup = publicLookup,
                ServiceLookup = serviceLookup,
                AllVehiclesLookup = allVehiclesLookup,
                
                TollRoadSet = tollRoadSet,
                Logger = m_JobLogger.GetWriter()
            };
            
            Dependency = validateJob.ScheduleParallel(m_CarOnLaneQuery, Dependency);
            Dependency.Complete();
            
            // Flush logs
            if (isJobLoggingEnabled && m_JobLogger.MessageCount > 0)
            {
                m_JobLogger.Flush();
            }
            
            // Cleanup
            tollRoadEntities.Dispose();
            tollRoadSet.Dispose();
        }
        
        private void LogTollRoadSummary()
        {
            var tollRoadEntities = m_TollRoadQuery.ToEntityArray(Allocator.TempJob);
            
            var privateLookup = SystemAPI.GetComponentLookup<TollRoadPrivateTransportData>(true);
            var truckLookup = SystemAPI.GetComponentLookup<TollRoadTruckData>(true);
            var publicLookup = SystemAPI.GetComponentLookup<TollRoadPublicTransportData>(true);
            var serviceLookup = SystemAPI.GetComponentLookup<TollRoadServiceVehiclesData>(true);
            var allVehiclesLookup = SystemAPI.GetComponentLookup<TollRoadAllVehiclesData>(true);
            
            var summaryJob = new LogTollRoadsSummaryJob
            {
                TollRoadEntities = tollRoadEntities,
                PrivateLookup = privateLookup,
                TruckLookup = truckLookup,
                PublicLookup = publicLookup,
                ServiceLookup = serviceLookup,
                AllVehiclesLookup = allVehiclesLookup,
                Logger = m_SummaryLogger.GetWriter()
            };
            
            summaryJob.Run();
            m_SummaryLogger.Flush();
            tollRoadEntities.Dispose();
        }
        
        // ----------------------- Jobs --------------------------
        
        [BurstCompile]
        private struct ValidateVehiclesOnTollRoadsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Car> CarTypeHandle;
            [ReadOnly] public ComponentTypeHandle<CarCurrentLane> CarCurrentLaneTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.PersonalCar> PersonalCarTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.DeliveryTruck> DeliveryTruckTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.PublicTransport> PublicTransportTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.Taxi> TaxiTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.PoliceCar> PoliceCarTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.Ambulance> AmbulanceTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.FireEngine> FireEngineTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.GarbageTruck> GarbageTruckTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.Hearse> HearseTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.MaintenanceVehicle> MaintenanceTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.PostVan> PostVanTypeHandle;
            [ReadOnly] public ComponentTypeHandle<Game.Vehicles.PrisonerTransport> PrisonerTransportTypeHandle;
            
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [ReadOnly] public ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrefabData> TollRoadLookup;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceLookup;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> AllVehiclesLookup;
            
            [ReadOnly] public NativeParallelHashSet<Entity> TollRoadSet;
            
            public JobLogger.Writer Logger;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var currentLanes = chunk.GetNativeArray(ref CarCurrentLaneTypeHandle);
                
                // Determine vehicle type for this chunk using Has()
                bool hasPersonalCar = chunk.Has(ref PersonalCarTypeHandle);
                bool hasDeliveryTruck = chunk.Has(ref DeliveryTruckTypeHandle);
                bool hasPublicTransport = chunk.Has(ref PublicTransportTypeHandle);
                bool hasTaxi = chunk.Has(ref TaxiTypeHandle);
                bool hasPoliceCar = chunk.Has(ref PoliceCarTypeHandle);
                bool hasAmbulance = chunk.Has(ref AmbulanceTypeHandle);
                bool hasFireEngine = chunk.Has(ref FireEngineTypeHandle);
                bool hasGarbageTruck = chunk.Has(ref GarbageTruckTypeHandle);
                bool hasHearse = chunk.Has(ref HearseTypeHandle);
                bool hasMaintenance = chunk.Has(ref MaintenanceTypeHandle);
                bool hasPostVan = chunk.Has(ref PostVanTypeHandle);
                bool hasPrisonerTransport = chunk.Has(ref PrisonerTransportTypeHandle);
                
                VehicleGroup vehicleGroup = DetermineVehicleGroup(
                    hasPersonalCar, hasDeliveryTruck, hasPublicTransport, hasTaxi,
                    hasPoliceCar, hasAmbulance, hasFireEngine, hasGarbageTruck,
                    hasHearse, hasMaintenance, hasPostVan, hasPrisonerTransport);
                
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var currentLane = currentLanes[i];
                    var laneEntity = currentLane.m_Lane;
                    
                    if (laneEntity == Entity.Null)
                        continue;
                    
                    // Get the road (owner) of this lane
                    if (!OwnerLookup.HasComponent(laneEntity))
                        continue;
                    
                    var roadEntity = OwnerLookup[laneEntity].m_Owner;
                    
                    // Check if this is a toll road
                    if (!TollRoadSet.Contains(roadEntity))
                        continue;
                    
                    // Check if vehicle is allowed on this toll road
                    bool isAllowed = IsVehicleGroupAllowed(vehicleGroup, roadEntity);
                    
                    // Log the validation result
                    FixedString512Bytes msg = default;
                    msg.Append((FixedString64Bytes)"Vehicle E:");
                    msg.Append(entity.Index);
                    msg.Append((FixedString64Bytes)" Group:");
                    AppendVehicleGroupName(ref msg, vehicleGroup);
                    msg.Append((FixedString64Bytes)" TollRoad:");
                    msg.Append(roadEntity.Index);
                    msg.Append((FixedString64Bytes)" Allowed:");
                    msg.Append(isAllowed ? (FixedString32Bytes)"YES" : (FixedString32Bytes)"NO");
                    
                    Logger.Log(msg);
                }
            }
            
            private VehicleGroup DetermineVehicleGroup(
                bool hasPersonalCar, bool hasDeliveryTruck, bool hasPublicTransport, bool hasTaxi,
                bool hasPoliceCar, bool hasAmbulance, bool hasFireEngine, bool hasGarbageTruck,
                bool hasHearse, bool hasMaintenance, bool hasPostVan, bool hasPrisonerTransport)
            {
                // Public transport
                if (hasPublicTransport || hasTaxi)
                    return VehicleGroup.PublicTransport;
                
                // Trucks
                if (hasDeliveryTruck)
                    return VehicleGroup.Trucks;
                
                // Service vehicles
                if (hasPoliceCar || hasAmbulance || hasFireEngine || hasGarbageTruck ||
                    hasHearse || hasMaintenance || hasPostVan || hasPrisonerTransport)
                    return VehicleGroup.ServiceVehicles;
                
                // Default: Private transport
                return VehicleGroup.PrivateTransport;
            }
            
            private bool IsVehicleGroupAllowed(VehicleGroup vehicleGroup, Entity tollRoadEntity)
            {
                // All vehicles allowed
                if (AllVehiclesLookup.HasComponent(tollRoadEntity))
                    return true;

                // Check specific permissions
                bool hasPrivate = PrivateLookup.HasComponent(tollRoadEntity);
                bool hasTruck = TruckLookup.HasComponent(tollRoadEntity);
                bool hasPublic = PublicLookup.HasComponent(tollRoadEntity);
                bool hasService = ServiceLookup.HasComponent(tollRoadEntity);
                
                // If no specific restriction, allow all
                if (!hasPrivate && !hasTruck && !hasPublic && !hasService)
                    return true;

                // Match vehicle group to road type
                switch (vehicleGroup)
                {
                    case VehicleGroup.PrivateTransport:
                        return hasPrivate;
                        
                    case VehicleGroup.Trucks:
                        return hasTruck;
                        
                    case VehicleGroup.PublicTransport:
                        return hasPublic;
                        
                    case VehicleGroup.ServiceVehicles:
                        // Service vehicles always allowed (emergency access)
                        return true;
                        
                    default:
                        return true;
                }
            }
            
            private void AppendVehicleGroupName(ref FixedString512Bytes str, VehicleGroup group)
            {
                switch (group)
                {
                    case VehicleGroup.PrivateTransport:
                        str.Append((FixedString32Bytes)"Private");
                        break;
                    case VehicleGroup.Trucks:
                        str.Append((FixedString32Bytes)"Truck");
                        break;
                    case VehicleGroup.PublicTransport:
                        str.Append((FixedString32Bytes)"Public");
                        break;
                    case VehicleGroup.ServiceVehicles:
                        str.Append((FixedString32Bytes)"Service");
                        break;
                    default:
                        str.Append((FixedString32Bytes)"Unknown");
                        break;
                }
            }
        }
        
        [BurstCompile]
        private struct LogTollRoadsSummaryJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> TollRoadEntities;
            [ReadOnly] public ComponentLookup<TollRoadPrivateTransportData> PrivateLookup;
            [ReadOnly] public ComponentLookup<TollRoadTruckData> TruckLookup;
            [ReadOnly] public ComponentLookup<TollRoadPublicTransportData> PublicLookup;
            [ReadOnly] public ComponentLookup<TollRoadServiceVehiclesData> ServiceLookup;
            [ReadOnly] public ComponentLookup<TollRoadAllVehiclesData> AllVehiclesLookup;
            
            public JobLogger.Writer Logger;
            
            public void Execute()
            {
                int privateCount = 0;
                int truckCount = 0;
                int publicCount = 0;
                int serviceCount = 0;
                int allCount = 0;
                
                for (int i = 0; i < TollRoadEntities.Length; i++)
                {
                    var entity = TollRoadEntities[i];
                    if (PrivateLookup.HasComponent(entity)) privateCount++;
                    if (TruckLookup.HasComponent(entity)) truckCount++;
                    if (PublicLookup.HasComponent(entity)) publicCount++;
                    if (ServiceLookup.HasComponent(entity)) serviceCount++;
                    if (AllVehiclesLookup.HasComponent(entity)) allCount++;
                }
                
                FixedString512Bytes header = "TollboothPathValidation: Active Toll Roads Summary";
                Logger.Log(header);
                
                Logger.LogValue("Total toll roads", TollRoadEntities.Length);
                Logger.LogValue("Private Transport", privateCount);
                Logger.LogValue("Trucks", truckCount);
                Logger.LogValue("Public Transport", publicCount);
                Logger.LogValue("Service Vehicles", serviceCount);
                Logger.LogValue("All Vehicles", allCount);
            }
        }
    }
}