using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using TollboothHighways.Domain.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace TollboothHighways.Systems
{
    /// <summary>
    /// System that manages toll payment logic for road vehicles (MANUAL toll booths only):
    /// <list type="number">
    /// <item><description>Detects vehicles entering a toll trigger zone and starts a payment process.</description></item>
    /// <item><description>Stops vehicles at a precise stop position (traffic light subobject preferred, tollbooth fallback).</description></item>
    /// <item><description>Maintains a processing (payment) duration while the vehicle is halted.</description></item>
    /// <item><description>Controls a barrier (represented via a <see cref="Game.Objects.TrafficLight"/> state) using deferred queued updates
    /// to remain Burst/job safe (open on entry, close on completion or abort).</description></item>
    /// <item><description>Releases vehicles after payment and applies a cooldown so they are not immediately re-captured.</description></item>
    /// </list>
    /// The system uses parallel jobs for detection and state updates, then applies visual barrier
    /// changes in a final single-thread job via <see cref="ApplyBarrierChangesJob"/>.
    /// </summary>
    public partial class StopVehiclesOnRoadSystem : GameSystemBase
    {
        /// <summary>
        /// Maximum distance (meters) at which a vehicle will qualify to start the toll stopping sequence.
        /// </summary>
        private const float StopTriggerDistance = 6f;

        /// <summary>
        /// Distance (meters) beyond which a vehicle already in processing is considered to have aborted (moved away).
        /// </summary>
        private const float CancelFarDistance = 40f;

        /// <summary>
        /// Minimum velocity threshold used when deciding if a vehicle is moving towards the stop point.
        /// Below this speed the direction test is skipped to avoid jitter.
        /// </summary>
        private const float MinApproachSpeed = 0.1f;

        /// <summary>
        /// Duration in seconds that a vehicle remains stopped to simulate toll payment.
        /// Converted to frames each update based on simulation frame rate.
        /// </summary>
        private const float ProcessingSeconds = 2.0f;

        /// <summary>
        /// Cooldown in seconds after release during which the vehicle cannot be captured again.
        /// Prevents repeated triggering while still near the toll area.
        /// </summary>
        private const float CooldownSeconds = 2.0f;

        /// <summary>
        /// Minimum speed restored to the vehicle after payment (ensures it does not remain at zero if original speed was very low).
        /// </summary>
        private const float MinResumeSpeed = 1f;

        /// <summary>
        /// Query containing all candidate car entities that can enter a toll process.
        /// </summary>
        private EntityQuery m_VehicleQuery;

        /// <summary>
        /// Query containing vehicles currently undergoing payment processing.
        /// </summary>
        private EntityQuery m_ProcessingQuery;

        /// <summary>
        /// Query containing vehicles that are in cooldown after completing toll payment.
        /// </summary>
        private EntityQuery m_CooldownQuery;

        /// <summary>
        /// Lookup for lane data (read-only).
        /// </summary>
        private ComponentLookup<Lane> m_LaneLookup;

        /// <summary>
        /// Lookup for owner data linking lanes/objects to road entities (read-only).
        /// </summary>
        private ComponentLookup<Owner> m_OwnerLookup;

        /// <summary>
        /// Lookup for road component data (read-only).
        /// </summary>
        private ComponentLookup<Road> m_RoadLookup;

        /// <summary>
        /// Lookup for toll road metadata component (read-only).
        /// </summary>
        private ComponentLookup<TollRoadPrefabData> m_TollRoadLookup;

        /// <summary>
        /// Lookup for toll booth manual payment (read-only).
        /// </summary>
        private ComponentLookup<TollBoothManualData> m_TollBoothManualLookup;

        /// <summary>
        /// Lookup for transforms (read-only).
        /// </summary>
        private ComponentLookup<Transform> m_TransformLookup;

        /// <summary>
        /// Lookup for sub-object buffers (read-only) used to locate traffic light subobjects on roads.
        /// </summary>
        private BufferLookup<Game.Objects.SubObject> m_SubObjectsLookup;

        /// <summary>
        /// Lookup for traffic light components (write-access required in apply job).
        /// </summary>
        private ComponentLookup<Game.Objects.TrafficLight> m_TrafficLightLookup;

        /// <summary>
        /// Simulation system reference for frame index access.
        /// </summary>
        private SimulationSystem m_SimulationSystem;

        /// <summary>
        /// End frame barrier used to safely batch structural Entity Command Buffer (ECB) modifications.
        /// </summary>
        private EndFrameBarrier m_EndFrameBarrier;

        /// <summary>
        /// Queue of barrier "open" (green) events accumulated in parallel jobs.
        /// </summary>
        private NativeQueue<BarrierChange> m_OpenBarrierQueue;

        /// <summary>
        /// Queue of barrier "close" (red) events accumulated in parallel jobs.
        /// </summary>
        private NativeQueue<BarrierChange> m_CloseBarrierQueue;

        /// <summary>
        /// Lightweight value describing a requested barrier state change for a specific road.
        /// Processed later by <see cref="ApplyBarrierChangesJob"/>.
        /// </summary>
        private struct BarrierChange
        {
            /// <summary>
            /// Road entity owning the barrier/traffic light.
            /// </summary>
            public Entity Road;

            /// <summary>
            /// Desired traffic light state to apply (e.g. Green / Red).
            /// </summary>
            public Game.Objects.TrafficLightState State;

            /// <summary>
            /// Constructs a barrier change request.
            /// </summary>
            /// <param name="road">Road entity.</param>
            /// <param name="state">Desired traffic light state.</param>
            public BarrierChange(Entity road, Game.Objects.TrafficLightState state)
            {
                Road = road;
                State = state;
            }
        }

        /// <summary>
        /// Final single-thread job that applies all queued barrier (traffic light) changes.
        /// Processes all "open" requests first, then all "close" requests so that
        /// a close in the same frame overrides an open.
        /// </summary>
        [BurstCompile]
        private struct ApplyBarrierChangesJob : IJob
        {
            /// <summary>
            /// Queue of open (green) requests.
            /// </summary>
            public NativeQueue<BarrierChange> OpenQueue;

            /// <summary>
            /// Queue of close (red) requests.
            /// </summary>
            public NativeQueue<BarrierChange> CloseQueue;

            /// <summary>
            /// Read-only lookup for sub-object buffers on roads.
            /// </summary>
            [ReadOnly] public BufferLookup<Game.Objects.SubObject> SubObjects;

            /// <summary>
            /// Traffic light lookup used for writing updated states.
            /// </summary>
            public ComponentLookup<Game.Objects.TrafficLight> TrafficLights;

            /// <inheritdoc />
            public void Execute()
            {
                while (OpenQueue.TryDequeue(out var change))
                {
                    Apply(change);
                }
                while (CloseQueue.TryDequeue(out var change))
                {
                    Apply(change);
                }
            }

            /// <summary>
            /// Applies a single barrier change if a traffic light subobject exists.
            /// </summary>
            /// <param name="change">Barrier change instruction.</param>
            private void Apply(BarrierChange change)
            {
                if (!SubObjects.HasBuffer(change.Road))
                    return;

                var buffer = SubObjects[change.Road];
                for (int i = 0; i < buffer.Length; i++)
                {
                    var sub = buffer[i].m_SubObject;
                    if (TrafficLights.HasComponent(sub))
                    {
                        var tl = TrafficLights[sub];
                        tl.m_State = change.State;
                        tl.m_GroupMask0 = 1;
                        tl.m_GroupMask1 = 0;
                        TrafficLights[sub] = tl;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes queries, queues, and required system references.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();

            m_OpenBarrierQueue = new NativeQueue<BarrierChange>(Allocator.Persistent);
            m_CloseBarrierQueue = new NativeQueue<BarrierChange>(Allocator.Persistent);

            m_VehicleQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Car>(),
                    ComponentType.ReadWrite<CarNavigation>(),
                    ComponentType.ReadWrite<Moving>(),
                    ComponentType.ReadOnly<CarCurrentLane>(),
                    ComponentType.ReadOnly<Transform>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<OutOfControl>()
                }
            });

            m_ProcessingQuery = GetEntityQuery(
                ComponentType.ReadOnly<TollPaymentProcessing>(),
                ComponentType.ReadWrite<CarNavigation>(),
                ComponentType.ReadWrite<Moving>(),
                ComponentType.ReadOnly<Transform>());

            m_CooldownQuery = GetEntityQuery(ComponentType.ReadOnly<TollPaymentCooldown>());

            RequireForUpdate(m_VehicleQuery);
        }

        /// <summary>
        /// Disposes native queues on system destruction.
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_OpenBarrierQueue.IsCreated) m_OpenBarrierQueue.Dispose();
            if (m_CloseBarrierQueue.IsCreated) m_CloseBarrierQueue.Dispose();
            base.OnDestroy();
        }

        /// <summary>
        /// Main update loop:
        /// <list type="number">
        /// <item><description>Re-fetches component lookups.</description></item>
        /// <item><description>Cleans expired cooldowns.</description></item>
        /// <item><description>Advances or terminates active processing vehicles (enqueue close if done/aborted).</description></item>
        /// <item><description>Detects new candidate vehicles (enqueue open and add processing component).</description></item>
        /// <item><description>Applies queued barrier changes in a final job.</description></item>
        /// </list>
        /// </summary>
        protected override void OnUpdate()
        {
            uint frame = m_SimulationSystem.frameIndex;
            float frameRate = 60f;
            uint processingFrames = (uint)math.max(1, math.round(ProcessingSeconds * frameRate));
            uint cooldownFrames = (uint)math.max(1, math.round(CooldownSeconds * frameRate));

            // Refresh lookups (snapshots)
            m_LaneLookup = GetComponentLookup<Lane>(true);
            m_OwnerLookup = GetComponentLookup<Owner>(true);
            m_RoadLookup = GetComponentLookup<Road>(true);
            m_TollRoadLookup = GetComponentLookup<TollRoadPrefabData>(true);
            m_TransformLookup = GetComponentLookup<Transform>(true);
            m_SubObjectsLookup = GetBufferLookup<Game.Objects.SubObject>(true);
            m_TrafficLightLookup = GetComponentLookup<Game.Objects.TrafficLight>(false); // write in apply job
            m_TollBoothManualLookup = GetComponentLookup<TollBoothManualData>(true);

            var ecb = m_EndFrameBarrier.CreateCommandBuffer();
            var ecbParallel = ecb.AsParallelWriter();
            var em = EntityManager;

            // 1. Cleanup cooldowns
            {
                uint now = frame;
                var ecbLocal = ecbParallel;
                Entities
                    .WithName("CleanupTollCooldowns")
                    .WithStoreEntityQueryInField(ref m_CooldownQuery)
                    .ForEach((Entity e, int entityInQueryIndex, in TollPaymentCooldown cd) =>
                    {
                        if (now >= cd.ExpireFrame || !em.Exists(cd.TollBooth))
                        {
                            ecbLocal.RemoveComponent<TollPaymentCooldown>(entityInQueryIndex, e);
                        }
                    }).ScheduleParallel();
            }

            // 2. Update processing vehicles
            {
                uint now = frame;
                var transformL = m_TransformLookup;
                var manualL = m_TollBoothManualLookup;
                var closeWriter = m_CloseBarrierQueue.AsParallelWriter();
                var ecbLocal = ecbParallel;

                Entities
                    .WithName("UpdateTollProcessing_BarrierQueue")
                    .WithStoreEntityQueryInField(ref m_ProcessingQuery)
                    .WithReadOnly(transformL)
                    .WithReadOnly(manualL)
                    .ForEach((Entity e, int entityInQueryIndex,
                              ref CarNavigation navigation,
                              ref Moving moving,
                              in Transform vehicleTransform,
                              in TollPaymentProcessing processing) =>
                    {
                        // Abort if tollbooth is not manual payment
                        if (processing.TollBooth == Entity.Null || !transformL.HasComponent(processing.TollBooth) || !manualL.HasComponent(processing.TollBooth))
                        {
                            closeWriter.Enqueue(new BarrierChange(processing.RoadEntity, Game.Objects.TrafficLightState.Red));
                            ecbLocal.RemoveComponent<TollPaymentProcessing>(entityInQueryIndex, e);
                            return;
                        }

                        float dist = math.distance(vehicleTransform.m_Position, processing.StopPosition);
                        if (dist > CancelFarDistance)
                        {
                            navigation.m_MaxSpeed = math.max(processing.OriginalMaxSpeed, MinResumeSpeed);
                            closeWriter.Enqueue(new BarrierChange(processing.RoadEntity, Game.Objects.TrafficLightState.Red));
                            ecbLocal.RemoveComponent<TollPaymentProcessing>(entityInQueryIndex, e);
                            return;
                        }

                        uint elapsed = now - processing.StartFrame;
                        if (elapsed >= processing.DurationFrames)
                        {
                            // Reset target so CarNavigationSystem can naturally choose a forward point next frame
                            navigation.m_TargetPosition = vehicleTransform.m_Position;
                            navigation.m_MaxSpeed = math.max(processing.OriginalMaxSpeed, MinResumeSpeed);
                            closeWriter.Enqueue(new BarrierChange(processing.RoadEntity, Game.Objects.TrafficLightState.Red));
                            ecbLocal.RemoveComponent<TollPaymentProcessing>(entityInQueryIndex, e);
                            ecbLocal.AddComponent(entityInQueryIndex, e, new TollPaymentCooldown
                            {
                                TollBooth = processing.TollBooth,
                                ExpireFrame = now + cooldownFrames
                            });
                        }
                        else
                        {
                            navigation.m_TargetPosition = processing.StopPosition;
                            navigation.m_MaxSpeed = 0f;
                            moving.m_Velocity = float3.zero;
                        }
                    }).ScheduleParallel();
            }

            // 3. Detect new candidates
            {
                uint now = frame;
                var laneL = m_LaneLookup;
                var ownerL = m_OwnerLookup;
                var roadL = m_RoadLookup;
                var tollRoadL = m_TollRoadLookup;
                var manualL = m_TollBoothManualLookup;
                var transformL = m_TransformLookup;
                var subObjsL = m_SubObjectsLookup;
                var tlLookupRO = GetComponentLookup<Game.Objects.TrafficLight>(true); // detection only
                var openWriter = m_OpenBarrierQueue.AsParallelWriter();
                var ecbLocal = ecbParallel;

                Entities
                    .WithName("DetectTollCandidates_BarrierQueue")
                    .WithStoreEntityQueryInField(ref m_VehicleQuery)
                    .WithReadOnly(laneL)
                    .WithReadOnly(ownerL)
                    .WithReadOnly(roadL)
                    .WithReadOnly(tollRoadL)
                    .WithReadOnly(manualL)
                    .WithReadOnly(transformL)
                    .WithReadOnly(subObjsL)
                    .WithReadOnly(tlLookupRO)
                    .ForEach((Entity e, int entityInQueryIndex,
                              ref CarNavigation navigation,
                              ref Moving moving,
                              in CarCurrentLane currentLane,
                              in Transform vehicleTransform) =>
                    {
                        if (em.HasComponent<TollPaymentProcessing>(e) ||
                            em.HasComponent<TollPaymentCooldown>(e))
                            return;

                        Entity laneEntity = currentLane.m_Lane;
                        if (laneEntity == Entity.Null || !laneL.HasComponent(laneEntity))
                            return;

                        if (!ownerL.TryGetComponent(laneEntity, out var owner))
                            return;
                        Entity roadEntity = owner.m_Owner;
                        if (roadEntity == Entity.Null || !roadL.HasComponent(roadEntity))
                            return;

                        if (!tollRoadL.TryGetComponent(roadEntity, out var tollRoadData) || !tollRoadData.HasActiveTollbooth)
                            return;
                                               
                        Entity tollBooth = tollRoadData.AssociatedTollbooth;
                        if (tollBooth == Entity.Null || !transformL.HasComponent(tollBooth) || !manualL.HasComponent(tollBooth))
                            return;  // Only manual toll booths supported

                        float3 stopPos;
                        if (!TryGetTrafficLightStopPosition(roadEntity, subObjsL, tlLookupRO, transformL, out stopPos))
                            stopPos = transformL[tollBooth].m_Position;

                        float3 toStop = stopPos - vehicleTransform.m_Position;
                        float dist = math.length(toStop);
                        if (dist > StopTriggerDistance) return;

                        float speed = math.length(moving.m_Velocity);
                        if (speed > MinApproachSpeed)
                        {
                            float3 dir = moving.m_Velocity / speed;
                            if (math.dot(dir, math.normalizesafe(toStop)) < 0f)
                                return;
                        }

                        ecbLocal.AddComponent(entityInQueryIndex, e, new TollPaymentProcessing
                        {
                            TollBooth = tollBooth,
                            StartFrame = now,
                            DurationFrames = processingFrames,
                            OriginalMaxSpeed = navigation.m_MaxSpeed,
                            StopPosition = stopPos,
                            RoadEntity = roadEntity
                        });

                        openWriter.Enqueue(new BarrierChange(roadEntity, Game.Objects.TrafficLightState.Green));

                        navigation.m_TargetPosition = stopPos;
                        navigation.m_MaxSpeed = 0f;
                        moving.m_Velocity = float3.zero;
                    }).ScheduleParallel();
            }

            // 4. Apply queued barrier changes after all entity jobs
            var applyJob = new ApplyBarrierChangesJob
            {
                OpenQueue = m_OpenBarrierQueue,
                CloseQueue = m_CloseBarrierQueue,
                SubObjects = m_SubObjectsLookup,
                TrafficLights = m_TrafficLightLookup
            };
            Dependency = applyJob.Schedule(Dependency);

            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        /// <summary>
        /// Attempts to resolve the precise stop position from a traffic light subobject.
        /// If no traffic light is found, the method returns <c>false</c> and position is default.
        /// </summary>
        /// <param name="roadEntity">Road entity to inspect for subobjects.</param>
        /// <param name="subObjectsLookup">Lookup to fetch subobject buffers.</param>
        /// <param name="trafficLightLookup">Lookup for traffic light components.</param>
        /// <param name="transformLookup">Lookup for transforms.</param>
        /// <param name="position">Resolved world position (output).</param>
        /// <returns><c>true</c> if a traffic light subobject was found; otherwise <c>false</c>.</returns>
        private static bool TryGetTrafficLightStopPosition(
            Entity roadEntity,
            BufferLookup<Game.Objects.SubObject> subObjectsLookup,
            ComponentLookup<Game.Objects.TrafficLight> trafficLightLookup,
            ComponentLookup<Transform> transformLookup,
            out float3 position)
        {
            position = default;
            if (!subObjectsLookup.HasBuffer(roadEntity))
                return false;

            var buffer = subObjectsLookup[roadEntity];
            for (int i = 0; i < buffer.Length; i++)
            {
                Entity subObj = buffer[i].m_SubObject;
                if (trafficLightLookup.HasComponent(subObj) && transformLookup.HasComponent(subObj))
                {
                    position = transformLookup[subObj].m_Position;
                    return true;
                }
            }
            return false;
        }
    }
}