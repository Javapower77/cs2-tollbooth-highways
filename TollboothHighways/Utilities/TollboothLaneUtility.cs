using Game.Net;
using Game.Pathfind;
using TollboothHighways.Domain.Components;
using TollboothHighways.Domain.Enums;
using Unity.Entities;
using Unity.Mathematics;

namespace TollboothHighways.Utilities
{
    internal static class TollboothLaneUtility
    {
        public static void BlockTollRoadLanes(Entity roadEntity,
                                               VehicleType vehicleType,
                                               EntityCommandBuffer ecb,
                                               BufferLookup<Game.Net.SubLane> subLaneLookup,
                                               ComponentLookup<Game.Net.CarLane> carLaneLookup,
                                               ComponentLookup<Game.Net.ConnectionLane> connectionLaneLookup,
                                               EntityManager entityManager,
                                               Entity blockingVehicle,
                                               uint currentFrame)
        {
            if (!subLaneLookup.TryGetBuffer(roadEntity, out var subLanes))
            {
                return;
            }

            CarLaneFlags blockFlags = GetBlockingFlagsForVehicleType(vehicleType);
            ConnectionLaneFlags connectionBlockFlags = GetConnectionBlockingFlags(vehicleType);

            for (int i = 0; i < subLanes.Length; i++)
            {
                Entity subLaneEntity = subLanes[i].m_SubLane;

                if (subLanes[i].m_PathMethods == PathMethod.Road && carLaneLookup.TryGetComponent(subLaneEntity, out var carLane))
                {
                    bool originalStored = entityManager.HasComponent<OriginalCarLaneFlags>(subLaneEntity);
                    bool hasBlockInfo = entityManager.HasComponent<LaneBlockedByVehicle>(subLaneEntity);

                    if (!originalStored)
                    {
                        ecb.AddComponent(subLaneEntity, new OriginalCarLaneFlags
                        {
                            Value = (uint)carLane.m_Flags
                        });

                        VehicleDebugLogger.Log(blockingVehicle, $"Storing original flags {carLane.m_Flags} for lane {subLaneEntity.Index}");
                    }

                    LaneBlockedByVehicle blockedInfo;
                    if (!hasBlockInfo)
                    {
                        blockedInfo = new LaneBlockedByVehicle
                        {
                            Vehicle = blockingVehicle,
                            TollRoad = roadEntity,
                            VehicleType = vehicleType,
                            FrameBlocked = currentFrame,
                            AttemptCount = 1
                        };

                        ecb.AddComponent(subLaneEntity, blockedInfo);
                    }
                    else
                    {
                        blockedInfo = entityManager.GetComponentData<LaneBlockedByVehicle>(subLaneEntity);
                        blockedInfo.Vehicle = blockingVehicle;
                        blockedInfo.TollRoad = roadEntity;
                        blockedInfo.VehicleType = vehicleType;
                        blockedInfo.FrameBlocked = currentFrame;
                        blockedInfo.AttemptCount = (ushort)math.min(ushort.MaxValue, blockedInfo.AttemptCount + 1);

                        ecb.SetComponent(subLaneEntity, blockedInfo);
                    }

                    carLane.m_Flags |= blockFlags;
                    carLane.m_BlockageStart = 0;
                    carLane.m_BlockageEnd = 255;

                    ecb.SetComponent(subLaneEntity, carLane);

                    VehicleDebugLogger.Log(blockingVehicle, $"Blocked Tollbooth Road lane {subLaneEntity.Index} with flags: {blockFlags} (attempt {blockedInfo.AttemptCount})");
                }

                if (connectionLaneLookup.TryGetComponent(subLaneEntity, out var connectionLane))
                {
                    if (!entityManager.HasComponent<OriginalConnectionLaneFlags>(subLaneEntity))
                    {
                        ecb.AddComponent(subLaneEntity, new OriginalConnectionLaneFlags
                        {
                            Value = (uint)connectionLane.m_Flags
                        });
                    }

                    ConnectionLaneFlags newFlags = connectionLane.m_Flags | connectionBlockFlags;
                    if (newFlags != connectionLane.m_Flags)
                    {
                        connectionLane.m_Flags = newFlags;
                        ecb.SetComponent(subLaneEntity, connectionLane);

                        VehicleDebugLogger.Log(blockingVehicle, $"Blocked toll connection lane {subLaneEntity.Index} with flags: {connectionBlockFlags}");
                    }
                }

                RequestPathfindRebuild(subLaneEntity, roadEntity, ecb, entityManager);
            }
        }

        public static CarLaneFlags GetBlockingFlagsForVehicleType(VehicleType vehicleType)
        {
            VehicleGroup vehicleGroup = VehiclesUtil.GetVehicleGroupBurstCompatible(vehicleType);

            switch (vehicleGroup)
            {
                case VehicleGroup.PrivateTransport:
                    return CarLaneFlags.Forbidden | CarLaneFlags.Unsafe;

                case VehicleGroup.Trucks:
                    return CarLaneFlags.ForbidHeavyTraffic | CarLaneFlags.Forbidden;

                case VehicleGroup.PublicTransport:
                    return CarLaneFlags.Forbidden | CarLaneFlags.Unsafe;

                case VehicleGroup.ServiceVehicles:
                    return CarLaneFlags.Forbidden | CarLaneFlags.Unsafe | CarLaneFlags.ForbidTransitTraffic;

                default:
                    return CarLaneFlags.Forbidden | CarLaneFlags.Unsafe;
            }
        }

        public static ConnectionLaneFlags GetConnectionBlockingFlags(VehicleType vehicleType)
        {
            return ConnectionLaneFlags.Disabled;
        }

        public static void RequestPathfindRebuild(Entity laneEntity, Entity ownerEntity, EntityCommandBuffer ecb, EntityManager entityManager)
        {
            if (laneEntity == Entity.Null || !entityManager.Exists(laneEntity))
            {
                return;
            }

            // Intentionally left blank. The pathfinding queues triggered by the caller
            // and the blocked lane flags are sufficient to notify the simulation.
            // This helper exists to keep the call sites readable in case we need to
            // integrate with future signalling components.
            _ = laneEntity;
            _ = ownerEntity;
            _ = ecb;
            _ = entityManager;
        }
    }
}
