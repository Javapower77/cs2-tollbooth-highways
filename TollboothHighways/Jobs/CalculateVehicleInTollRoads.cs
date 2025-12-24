using Colossal.Entities;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using System.Collections.Generic;
using TollboothHighways.Utilities;
using TollboothHighways.Domain;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using UnityEngine;
using SubLane = Game.Net.SubLane;
using System.Runtime.InteropServices;

namespace TollboothHighways.Jobs
{
    // Job to calculate the vehicles passing through a toll road
    // thanks to krzychu124 to pointing me to disable burst compilation at build level of the mod
#if WITH_BURST
    [BurstCompile]
#endif
    public struct CalculateVehicleInTollRoads : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> tollRoadEntities;
        [ReadOnly] public BufferLookup<SubLane> SubLaneObjectData;
        [ReadOnly] public BufferLookup<LaneObject> LaneObjectData;
        [ReadOnly] public ComponentLookup<PrefabRef> PrefabRefData;
        [ReadOnly] public ComponentLookup<Edge> EdgeObjectData;
        [ReadOnly] public ComponentLookup<CarTrailerLane> VehicleTrailerData;
        [WriteOnly] public NativeList<TollRoadVehicle>.ParallelWriter Results;

        public void Execute(int index)
        {
            Entity e = tollRoadEntities[index];
            int subLaneTypeRoad = 0;

            if (EdgeObjectData.TryGetComponent(e, out Edge edgeComponent))
            {
                if (SubLaneObjectData.TryGetBuffer(edgeComponent.m_Start, out DynamicBuffer<SubLane> sublaneObjects))
                {
                    for (int x = 0; x < sublaneObjects.Length; x++)
                    {
                        if (sublaneObjects[x].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            subLaneTypeRoad = x;
                            break;
                        }
                    }

                    if (LaneObjectData.TryGetBuffer(sublaneObjects[subLaneTypeRoad].m_SubLane, out DynamicBuffer<LaneObject> laneObjects))
                    {
                        for (int i = 0; i < laneObjects.Length; i++)
                        {
                            Entity vehicleEntity = laneObjects[i].m_LaneObject;
                            if (!VehicleTrailerData.TryGetComponent(vehicleEntity, out _))
                            {
                                Results.AddNoResize(new TollRoadVehicle { TollRoad = e, Vehicle = vehicleEntity });
                            }
                        }
                    }
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TollRoadVehicle
    {
        public Entity TollRoad;
        public Entity Vehicle;
    }
}