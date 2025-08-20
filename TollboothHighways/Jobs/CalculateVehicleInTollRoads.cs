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
        [WriteOnly] public NativeList<(Entity tollRoad, Entity vehicle)>.ParallelWriter Results;
        public JobLogger.Writer Logger;

        public void Execute(int index)
        {
            //Logger.Log($"BEGIN -> TollHighways.Jobs.CalculateVehicleInTollRoads.Execute({index})");
            Entity e = tollRoadEntities[index];
            
            // Variable to store the index position of the Sublane object that represents the road
            int subLaneTypeRoad = 0;

            // Check if the entity has the Edge component, which is used to represent the road
            // and the point of check where the vehicles pass through
            //Logger.Log($"| Checking if entity {e.Index}:{index} has Edge component");
            if (EdgeObjectData.TryGetComponent(e, out Edge edgeComponent))
            {
                // get the Sublane objects from the Stard Edge component of the road
                if (SubLaneObjectData.TryGetBuffer(edgeComponent.m_Start, out DynamicBuffer<SubLane> sublaneObjects))
                {
                    for (int x = 0; x < sublaneObjects.Length; x++)
                    {
                        // Check if the Sublane object is a road, if so, store the index position
                        // to use it later to get the LaneObjects from the Sublane object
                        if (sublaneObjects[x].m_PathMethods == Game.Pathfind.PathMethod.Road)
                        {
                            subLaneTypeRoad = x;
                            break;
                        }
                    }                   

                    // Get the LaneObjects from the second Sublane of the road that represent the location
                    // where vehicles passthrough. This is only for this custom made road
                    if (LaneObjectData.TryGetBuffer(sublaneObjects[subLaneTypeRoad].m_SubLane, out DynamicBuffer<LaneObject> laneObjects))
                    {                       
                        // Loop through the LaneObjects to check if one Object has the VehicleTrailerData component,
                        // if so, it means that the vehicle is a trailer and we don't want to count it as a vehicle
                        for (int i = 0; i < laneObjects.Length; i++)
                        {
                            Entity vehicleEntity = laneObjects[i].m_LaneObject;
                            if (!VehicleTrailerData.TryGetComponent(vehicleEntity, out _))
                            {
                                Results.AddNoResize((e, vehicleEntity));
                            }
                        }                        
                    }
                }
            }
            //Logger.Log($"END -> TollHighways.Jobs.CalculateVehicleInTollRoads.Execute({index})");
        }
    }
}