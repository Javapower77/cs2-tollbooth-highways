using Game;
using Game.Common;
using Game.Net;
using Game.Tools;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using CarLane = Game.Net.CarLane;
using SubLane = Game.Net.SubLane;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// Adds Forbidden to all car sub-lanes of roads that are (or own) manual tollbooths.
    /// A Harmony patch then selectively lets private cars ignore the penalty.
    /// </summary>
    public partial class TollboothVehicleRestrictionSystem : GameSystemBase
    {
        // Two queries:
        //  1) Roads that directly have TollBoothManualData
        //  2) Manual toll booth objects (cabins) that have an Owner pointing to the road
        private EntityQuery m_DirectRoadQuery;
        private EntityQuery m_TollBoothObjectQuery;

        private BufferLookup<SubLane> m_SubLaneLookup;
        private ComponentLookup<CarLane> m_CarLaneLookup;
        private ComponentLookup<Owner> m_OwnerLookup;

        private EntityTypeHandle m_EntityTypeHandle_Roads;
        private ComponentTypeHandle<Owner> m_OwnerTypeHandle;

        private bool m_LoggedActive;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Roads already tagged
            m_DirectRoadQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Road>(),
                    ComponentType.ReadOnly<TollBoothManualData>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            // Toll booth objects (cabins) that carry TollBoothManualData and have an Owner referencing the road
            m_TollBoothObjectQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TollBoothManualData>(),
                    ComponentType.ReadOnly<Owner>() // owner should be the road (or an edge)
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Road>(), // exclude direct roads from this second query to avoid duplication here
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            m_SubLaneLookup = GetBufferLookup<SubLane>(true);
            m_CarLaneLookup = GetComponentLookup<CarLane>();
            m_OwnerLookup = GetComponentLookup<Owner>(true);

            m_EntityTypeHandle_Roads = GetEntityTypeHandle();
            m_OwnerTypeHandle = GetComponentTypeHandle<Owner>(true);

            // We want to run when EITHER path is valid.
            RequireForUpdate(GetEntityQuery(new EntityQueryDesc
            {
                Any = new[]
                {
                    ComponentType.ReadOnly<TollBoothManualData>() // either on roads or objects
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            }));
        }

        // Ensure we run every simulation tick for consistently overriding lane flags post LaneDataSystem
        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation)
                return 1;
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            // Refresh lookups & handles
            m_SubLaneLookup.Update(this);
            m_CarLaneLookup.Update(this);
            m_OwnerLookup.Update(this);
            m_EntityTypeHandle_Roads.Update(this);
            m_OwnerTypeHandle.Update(this);

            int directRoadCount = m_DirectRoadQuery.CalculateEntityCount();
            int boothCount = m_TollBoothObjectQuery.CalculateEntityCount();

            if (directRoadCount == 0 && boothCount == 0)
                return;

            if (!m_LoggedActive)
            {
                LogUtil.Info($"TollboothVehicleRestrictionSystem active. DirectRoads={directRoadCount}, BoothObjects={boothCount}");
                m_LoggedActive = true;
            }

            // 1) Process direct road tagging
            if (directRoadCount > 0)
            {
                var jobDirect = new ForbiddenLaneJob
                {
                    EntityTypeHandle = m_EntityTypeHandle_Roads,
                    SubLaneLookup = m_SubLaneLookup,
                    CarLaneLookup = m_CarLaneLookup
                };
                Dependency = jobDirect.ScheduleParallel(m_DirectRoadQuery, Dependency);
            }

            // 2) Process booth objects -> resolve owner -> road
            if (boothCount > 0)
            {
                var jobBooths = new BoothOwnerResolveJob
                {
                    OwnerTypeHandle = m_OwnerTypeHandle,
                    SubLaneLookup = m_SubLaneLookup,
                    CarLaneLookup = m_CarLaneLookup,
                    OwnerLookup = m_OwnerLookup
                };
                Dependency = jobBooths.ScheduleParallel(m_TollBoothObjectQuery, Dependency);
            }
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct ForbiddenLaneJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferLookup<SubLane> SubLaneLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<CarLane> CarLaneLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var roadEntities = chunk.GetNativeArray(EntityTypeHandle);
                for (int i = 0; i < roadEntities.Length; i++)
                {
                    var road = roadEntities[i];
                    if (!SubLaneLookup.HasBuffer(road))
                        continue;

                    var subLanes = SubLaneLookup[road];
                    for (int j = 0; j < subLanes.Length; j++)
                    {
                        var subLane = subLanes[j].m_SubLane;
                        if (!CarLaneLookup.HasComponent(subLane))
                            continue;

                        var lane = CarLaneLookup[subLane];
                        if ((lane.m_Flags & CarLaneFlags.Forbidden) != 0)
                            continue; // already set

                        lane.m_Flags |= CarLaneFlags.Forbidden;
                        CarLaneLookup[subLane] = lane;
                    }
                }
            }
        }

#if WITH_BURST
        [BurstCompile]
#endif
        private struct BoothOwnerResolveJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Owner> OwnerTypeHandle;
            [ReadOnly] public BufferLookup<SubLane> SubLaneLookup;
            [ReadOnly] public ComponentLookup<Owner> OwnerLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<CarLane> CarLaneLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(ref OwnerTypeHandle);
                for (int i = 0; i < owners.Length; i++)
                {
                    var road = owners[i].m_Owner;
                    if (road == Entity.Null || !SubLaneLookup.HasBuffer(road))
                        continue;

                    var subLanes = SubLaneLookup[road];
                    for (int j = 0; j < subLanes.Length; j++)
                    {
                        var subLane = subLanes[j].m_SubLane;
                        if (!CarLaneLookup.HasComponent(subLane))
                            continue;

                        var lane = CarLaneLookup[subLane];
                        if ((lane.m_Flags & CarLaneFlags.Forbidden) != 0)
                            continue;

                        lane.m_Flags |= CarLaneFlags.Forbidden;
                        CarLaneLookup[subLane] = lane;
                    }
                }
            }
        }
    }
}