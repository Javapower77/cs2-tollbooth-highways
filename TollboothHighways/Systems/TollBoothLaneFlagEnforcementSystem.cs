using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.PlayerLoop;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Monitors and enforces CarLane flags on tollbooth roads to ensure vehicle type restrictions persist.
    /// This system runs after TollBoothSpawnSystem and continuously applies lane restrictions based on tollbooth types.
    /// </summary>
    public partial class TollBoothLaneFlagEnforcementSystem : GameSystemBase
    {
        private EntityQuery m_TollRoadQuery;
        private BufferLookup<Game.Net.SubLane> m_SubLaneData;
        private ComponentLookup<Game.Common.Owner> m_OwnerLookup;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_SubLaneData = GetBufferLookup<Game.Net.SubLane>(false);
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
                    ComponentType.ReadOnly<TollRoadCarLaneApplied>()
                }
            });

            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: OnCreate() - System created successfully");
        }

        protected override void OnUpdate()
        {
            // Update lookups
            m_SubLaneData.Update(this);
            m_OwnerLookup.Update(this);

            // Check if there are any toll roads to process
            if (m_TollRoadQuery.IsEmptyIgnoreFilter)
                return;

            // Get all toll road entities
            var tollRoads = m_TollRoadQuery.ToEntityArray(Allocator.Temp);
            try
                {
                    LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: OnUpdate() - Processing {tollRoads.Length} toll roads");

                    foreach (var roadEntity in tollRoads)
                    {
                        try
                        {
                            LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Processing toll road {roadEntity.Index}");

                            // Apply lane flags for this toll road
                            SetCarLaneFlags(roadEntity);
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollBoothLaneFlagEnforcementSystem: OnUpdate() - EXCEPTION processing road entity {roadEntity.Index}: {ex.Message}");
                            LogUtil.Error($"TollBoothLaneFlagEnforcementSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
                        }
                    }
                }
                finally
                {
                    tollRoads.Dispose();
                }
        }

        private void SetCarLaneFlags(Entity roadEntity)
        {
            try
            {
                if (m_SubLaneData.TryGetBuffer(roadEntity, out var subLanes))
                {
                    for (int i = 0; i < subLanes.Length; i++)
                    {
                        if (subLanes[i].m_PathMethods == PathMethod.Road)
                        {
                            Entity laneEntity = subLanes[i].m_SubLane;
                            var carLaneFlags = EntityManager.GetComponentData<Game.Net.CarLane>(laneEntity);
                            LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: SetCarLaneFlags() - Original lane flags for SubLane {laneEntity.Index}: {carLaneFlags.m_Flags}");

                            // Apply correct flags based on tollbooth type
                            if (EntityManager.HasComponent<TollRoadPrivateTransportData>(roadEntity))
                            {
                                // Private transport only - block heavy traffic (trucks)
                                carLaneFlags.m_Flags |= Game.Net.CarLaneFlags.ForbidHeavyTraffic;
                                LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Applied ForbidHeavyTraffic for Private Transport tollbooth");
                            }
                            else if (EntityManager.HasComponent<TollRoadTruckData>(roadEntity))
                            {
                                // Trucks only - block transit traffic (cars/buses)
                                carLaneFlags.m_Flags |= Game.Net.CarLaneFlags.ForbidTransitTraffic;
                                LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Applied ForbidTransitTraffic for Truck tollbooth");
                            }
                            else if (EntityManager.HasComponent<TollRoadPublicTransportData>(roadEntity))
                            {
                                // Public transport only
                                carLaneFlags.m_Flags |= Game.Net.CarLaneFlags.PublicOnly;
                                LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Applied PublicOnly for Public Transport tollbooth");
                            }
                            else if (EntityManager.HasComponent<TollRoadServiceVehiclesData>(roadEntity))
                            {
                                // Service vehicles only - block both transit and heavy traffic
                                carLaneFlags.m_Flags |= Game.Net.CarLaneFlags.ForbidTransitTraffic | Game.Net.CarLaneFlags.ForbidHeavyTraffic;
                                LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: Applied ForbidTransitTraffic | ForbidHeavyTraffic for Service Vehicle tollbooth");
                            }

                            EntityManager.SetComponentData(laneEntity, carLaneFlags);
                            EntityManager.AddComponent<TollRoadCarLaneApplied>(roadEntity);
                            LogUtil.Info($"TollBoothLaneFlagEnforcementSystem: SetCarLaneFlags() - Updated lane flags for SubLane {laneEntity.Index}: m_Flags={carLaneFlags.m_Flags}");
                            break;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollBoothLaneFlagEnforcementSystem: SetCarLaneFlags() - FAILED to set car lane flags for road {roadEntity.Index}. Error: {ex.Message}");
                LogUtil.Error($"TollBoothLaneFlagEnforcementSystem: Stack trace: {ex.StackTrace}");
                throw;
            }

        }
       
        protected override void OnDestroy()
        {
            base.OnDestroy();
            LogUtil.Info("TollBoothLaneFlagEnforcementSystem: OnDestroy() - System destroyed");
        }
    }


}