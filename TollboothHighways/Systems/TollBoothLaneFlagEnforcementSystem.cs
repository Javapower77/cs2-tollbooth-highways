using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Enforces CarLane flags on tollbooth roads to ensure vehicle type restrictions.
    /// Runs once per road when spawned, uses IJobChunk for performance.
    /// </summary>
    public partial class TollBoothLaneFlagEnforcementSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private BufferLookup<Game.Net.SubLane> m_SubLaneData;
        private ComponentLookup<Game.Net.CarLane> m_CarLaneLookup;
        private ComponentLookup<TollRoadPrivateTransportData> m_PrivateTransportLookup;
        private ComponentLookup<TollRoadTruckData> m_TruckLookup;
        private ComponentLookup<TollRoadPublicTransportData> m_PublicTransportLookup;
        private ComponentLookup<TollRoadServiceVehiclesData> m_ServiceVehiclesLookup;
        private EntityCommandBufferSystem m_CommandBufferSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Initialize lookups
            m_SubLaneData = GetBufferLookup<Game.Net.SubLane>(true);
            m_CarLaneLookup = GetComponentLookup<Game.Net.CarLane>(false);
            m_PrivateTransportLookup = GetComponentLookup<TollRoadPrivateTransportData>(true);
            m_TruckLookup = GetComponentLookup<TollRoadTruckData>(true);
            m_PublicTransportLookup = GetComponentLookup<TollRoadPublicTransportData>(true);
            m_ServiceVehiclesLookup = GetComponentLookup<TollRoadServiceVehiclesData>(true);

            // Get command buffer system for adding components
            m_CommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();

            // Query for toll roads that haven't been processed yet
            m_TollRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Net.Edge>()
                },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<TollRoadPrivateTransportData>(),
                    ComponentType.ReadOnly<TollRoadTruckData>(),
                    ComponentType.ReadOnly<TollRoadPublicTransportData>(),
                    ComponentType.ReadOnly<TollRoadServiceVehiclesData>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<TollRoadCarLaneApplied>()
                }
            });

            RequireForUpdate(m_TollRoadQuery);
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Created successfully");
        }

        protected override void OnUpdate()
        {
            // Early exit if no roads to process
            if (m_TollRoadQuery.IsEmptyIgnoreFilter)
                return;

            // Update lookups
            m_SubLaneData.Update(this);
            m_CarLaneLookup.Update(this);
            m_PrivateTransportLookup.Update(this);
            m_TruckLookup.Update(this);
            m_PublicTransportLookup.Update(this);
            m_ServiceVehiclesLookup.Update(this);

            var ecb = m_CommandBufferSystem.CreateCommandBuffer();
            int processedCount = 0;

            // Process job - cannot use IJobChunk with EntityManager operations
            // So we keep it simple and fast on main thread
            var entities = m_TollRoadQuery.ToEntityArray(Allocator.Temp);

            foreach (var roadEntity in entities)
            {
                if (ProcessTollRoad(roadEntity, ecb))
                {
                    processedCount++;
                }
            }

            entities.Dispose();

            // Log only if enabled and roads were processed
            if (processedCount > 0)
            {
                LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Processed {processedCount} toll roads");
            }
        }

        private bool ProcessTollRoad(Entity roadEntity, EntityCommandBuffer ecb)
        {
            // Determine toll type and corresponding flags
            CarLaneFlags flagsToApply;

            if (m_PrivateTransportLookup.HasComponent(roadEntity))
            {
                flagsToApply = CarLaneFlags.ForbidHeavyTraffic;
            }
            else if (m_TruckLookup.HasComponent(roadEntity))
            {
                flagsToApply = CarLaneFlags.ForbidTransitTraffic;
            }
            else if (m_PublicTransportLookup.HasComponent(roadEntity))
            {
                flagsToApply = CarLaneFlags.PublicOnly;
            }
            else if (m_ServiceVehiclesLookup.HasComponent(roadEntity))
            {
                flagsToApply = CarLaneFlags.ForbidTransitTraffic | CarLaneFlags.ForbidHeavyTraffic;
            }
            else
            {
                // No recognized toll type
                return false;
            }

            // Apply flags to all road sublanes
            if (!m_SubLaneData.TryGetBuffer(roadEntity, out var subLanes))
                return false;

            bool appliedAnyFlags = false;

            for (int i = 0; i < subLanes.Length; i++)
            {
                var subLane = subLanes[i];
                
                // Only process road lanes
                if ((subLane.m_PathMethods & PathMethod.Road) == 0)
                    continue;

                Entity laneEntity = subLane.m_SubLane;

                // Check if lane has CarLane component
                if (!m_CarLaneLookup.TryGetComponent(laneEntity, out var carLane))
                    continue;

                // Apply flags (using |= to preserve existing flags)
                LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Current Car Lane Flags: " + carLane.m_Flags + " for lane entity: " + laneEntity.Index);
                carLane.m_Flags |= flagsToApply;
                m_CarLaneLookup[laneEntity] = carLane;
                appliedAnyFlags = true;
                LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Applied Car Lane Flags: " + carLane.m_Flags);
            }

            // Mark road as processed
            if (appliedAnyFlags)
            {
                ecb.AddComponent<TollRoadCarLaneApplied>(roadEntity);
            }

            return appliedAnyFlags;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: Destroyed");
        }
    }
}