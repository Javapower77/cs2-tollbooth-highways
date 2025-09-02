# StopVehiclesOnRoadSystem (Tollbooth Highways)

## Purpose
Controls tollbooth behavior:
- Detects vehicles approaching a toll barrier.
- Stops them at a precise stop point (traffic-light subobject or tollbooth position).
- Simulates payment (processing time).
- Re-opens/closes a visual barrier (traffic light) using deferred queued updates.
- Releases vehicles and applies a cooldown to prevent immediate retrigger.

## High-Level Flow
1. Query candidate vehicles (cars) on toll-enabled roads.
2. If close enough and moving toward the stop point: enqueue:
   - Add TollPaymentProcessing
   - Open barrier (green light)
3. While processing:
   - Freeze movement (max speed = 0, target = stop position)
   - Count frames
4. When finished or aborted:
   - Restore speed
   - Close barrier (red light)
   - Add TollPaymentCooldown (prevents re-trigger)
5. At the end of the frame:
   - Apply all queued barrier (traffic light) changes.

## Components
### TollRoadPrefabData (road)
- AssociatedTollbooth (Entity)
- HasActiveTollbooth (bool)

### TollPaymentProcessing (vehicle)
- TollBooth (Entity)
- StartFrame (uint)
- DurationFrames (uint)
- OriginalMaxSpeed (float)
- StopPosition (float3)
- RoadEntity (Entity)

### TollPaymentCooldown (vehicle)
- TollBooth (Entity)
- ExpireFrame (uint)

## Key Queries
- m_VehicleQuery: All eligible moving cars.
- m_ProcessingQuery: Cars with TollPaymentProcessing.
- m_CooldownQuery: Cars in cooldown window.

## Tunable Constants
| Constant | Meaning | Typical Impact |
|----------|---------|----------------|
| StopTriggerDistance | Max distance to begin stop sequence | Increase to start earlier |
| CancelFarDistance | Distance at which a stalled departure is aborted | Bigger = more tolerant |
| ProcessingSeconds | Payment duration | Scales vehicle throughput |
| CooldownSeconds | Delay before retrigger | Prevents flicker loops |
| MinApproachSpeed | Direction test threshold | Lower for slow traffic |
| MinResumeSpeed | Minimum restored speed | Guards against 0 speed restart |

## Barrier (Traffic Light) Control
Barrier control is deferred:
- Two NativeQueues: Open (green) and Close (red)
- Queuing happens in parallel jobs (no structural write hazards)
- An `ApplyBarrierChangesJob` runs after scheduling to:
  - Open queue first
  - Close queue second (close wins if both in same frame)
- SubObject buffer is scanned; first `Game.Objects.TrafficLight` updated.

### Why Queued?
Direct writes inside ScheduleParallel jobs are unsafe. Queues allow aggregation then single-thread application.

## Stop Position Resolution
`TryGetTrafficLightStopPosition(roadEntity, ...)`
1. Iterate road SubObjects for first entity with `Game.Objects.TrafficLight`.
2. If found, its transform position is used.
3. Fallback: tollbooth entity position.

## Vehicle Control Logic
- Target position forced: `navigation.m_TargetPosition = StopPosition`
- Movement halted: `navigation.m_MaxSpeed = 0` and `moving.m_Velocity = 0`
- On release: `navigation.m_TargetPosition = current position` so the downstream `CarNavigationSystem` recalculates forward target without “orbiting” stop point.

## Common Issue: Vehicle Swerving After Release
Symptoms: Upon barrier reopening, vehicle curves sideways.
Causes:
- Target was left at historical stop point (fixed by resetting target).
- Lane change progress mid-stop produced lateral alignment mismatch.
Mitigation already applied: resetting target and restoring original max speed (clamped by MinResumeSpeed). If still present:
- Consider re-validating current lane (call a helper to re-locate CarCurrentLane if drift occurred).
- Optionally add a small forward offset along lane curve on release.

## Performance Notes
- All heavy work is per-frame but bounded to active candidates.
- No per-vehicle allocations.
- Queues scale linearly with events (typically low).
- Lookups refreshed once per frame (amortized cost).

## Safety / Structural Considerations
- Component adds/removes done through `EndFrameBarrier` ECB (ParallelWriter variant).
- Barrier visuals require write access to TrafficLight components—handled only in the final single job.

## Extension Points
| Use Case | Approach |
|----------|----------|
| Variable pricing / time | Add fields to TollPaymentProcessing (e.g. Charge, DynamicDuration) |
| Multi-lane gating | Track lane entity; store lane->queue counts |
| Animated barrier arm | Add a custom component (e.g. BarrierAnimator) and toggle state in ApplyBarrierChangesJob |
| Vehicle class filtering | Add checks on `Car` flags, or prefab categories |
| Logging / analytics | Inject a NativeQueue<Event> and process via a separate job |

## Edge Cases
| Case | Handling |
|------|----------|
| TollBooth destroyed mid-process | Abort, close barrier |
| Vehicle teleports or desync distance | Exceeds CancelFarDistance -> release |
| Multiple vehicles at same barrier | Each opens barrier; closes when each finishes (last frame close wins) — consider ref counting if needed |
| Cooldown overlaps new approach | Cooldown prevents immediate re-trigger until expiry |

## Potential Improvements
- Replace simple “stop & hold” with deceleration ramp.
- Add ref-count to avoid flicker if N vehicles overlap in one frame.
- Integrate with lane signals (e.g. LaneSignal petitioners).
- Add Debug Gizmos (conditional compilation symbol) to visualize stop spheres.

## Pseudocode Summary
```csharp
OnUpdate: Refresh lookups Clean cooldowns 
For each processing vehicle: 
If invalid/too far -> close barrier + remove 
Else if done -> restore speed + close barrier + cooldown 
Else -> hold stopped 
For each candidate vehicle: 
If qualifies -> add processing + open barrier + freeze motion 
ApplyBarrierChangesJob: 
Dequeue open (green) events 
Dequeue close (red) events
```


## Troubleshooting
| Symptom | Fix |
|---------|-----|
| Vehicles never stop | Verify TollRoadPrefabData.HasActiveTollbooth true & distance < StopTriggerDistance |
| Barrier never turns red | Ensure completion path enqueues close; check DurationFrames |
| Flickering open/close same frame | Implement ref counting or batch merge (current close overwrites) |
| Path misalignment post-release | Confirm `navigation.m_TargetPosition` reset; optionally add slight forward offset |

## Code References
- Component creation: section “DetectTollCandidates_BarrierQueue”
- Release logic: section “UpdateTollProcessing_BarrierQueue”
- Barrier application: `ApplyBarrierChangesJob`
- Stop position selection: `TryGetTrafficLightStopPosition`

## Minimal Forward Offset (Optional)
To push vehicle forward slightly after release:
```csharp
// After payment completion 
var forward = math.normalizesafe(processing.StopPosition - vehicleTransform.m_Position); 
navigation.m_TargetPosition = vehicleTransform.m_Position + forward * 2f;
```


(Ensure forward is valid; else fall back to current position.)

## Versioning
Document corresponds to current in-repo implementation (StopVehiclesOnRoadSystem.cs).

---

Happy CS:2 modding!
Documented by *Javas77*
*Disclaimer:* This documentation is community-contributed and may not reflect official game design. Use at your own risk.