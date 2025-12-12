# JobLogger Documentation

A thread-safe logging utility designed for Burst-compiled parallel jobs in Unity's DOTS/ECS environment.

## Overview

`JobLogger` provides a way to collect log messages from multiple worker threads during parallel job execution and flush them to the main thread safely. This is essential because Unity's `Debug.Log` and similar methods are not Burst-compatible.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Main Thread                              │
│  ┌────────────────────────────────────────────────────┐     │
│  │  JobLogger                                          │     │
│  │  ├── Initialize(Allocator, capacity, isEnabled)    │     │
│  │  ├── GetWriter() → Writer                          │     │
│  │  ├── Flush() → LogUtil.Info()                      │     │
│  │  └── Dispose()                                     │     │
│  └────────────────────────────────────────────────────┘     │
│                          │                                   │
│                          ▼                                   │
│  ┌────────────────────────────────────────────────────┐     │
│  │  NativeList<FixedString4096Bytes>                  │     │
│  │  (Thread-safe storage via ParallelWriter)          │     │
│  └────────────────────────────────────────────────────┘     │
│                          ▲                                   │
└──────────────────────────┼──────────────────────────────────┘
                           │
┌──────────────────────────┼──────────────────────────────────┐
│               Worker Threads (Burst Jobs)                    │
│  ┌────────────────────────────────────────────────────┐     │
│  │  Writer                                             │     │
│  │  ├── Log(message)                                  │     │
│  │  ├── LogVehicle(entityIndex, entityVersion, msg)   │     │
│  │  └── LogValue(label, value)                        │     │
│  └────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

## API Reference

### JobLogger (Main Thread)

| Method | Description |
|--------|-------------|
| `Initialize(Allocator, int, bool)` | Allocates the native list. Call before scheduling jobs. |
| `SetCapacity(int)` | Pre-allocates capacity if expected count is known. |
| `GetWriter()` | Returns a `Writer` struct to pass into jobs. |
| `MessageCount` | Gets the number of pending log messages. |
| `Flush()` | Outputs all messages via `LogUtil.Info()`. Call after job completion. |
| `Dispose()` | Frees native memory. Must be called to prevent leaks. |

### Writer (Worker Threads - Burst Compatible)

| Method | Description |
|--------|-------------|
| `Log(in FixedString512Bytes)` | Logs a general message with thread ID prefix. |
| `LogVehicle(int, int, in FixedString512Bytes)` | Logs with entity info `E(index:version)`. |
| `LogValue(in FixedString128Bytes, int)` | Logs a labeled numeric value. |

## Usage Examples

### Example 1: Basic Usage in a Parallel Job

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Game.Common;
using Game.Simulation;
using TollboothHighways.Utilities;

public partial class ExampleLoggingSystem : GameSystemBase
{
    private EntityQuery m_VehicleQuery;
    private JobLogger m_Logger;

    protected override void OnCreate()
    {
        base.OnCreate();
        m_VehicleQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Vehicles.Car>());
        
        // Initialize logger once - use Persistent for long-lived systems
        m_Logger = new JobLogger();
        m_Logger.Initialize(Allocator.Persistent, initialCapacity: 512, isEnabled: true);
    }

    protected override void OnDestroy()
    {
        m_Logger.Dispose();
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        var job = new LogVehiclesJob
        {
            EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
            Logger = m_Logger.GetWriter()
        };

        Dependency = job.ScheduleParallel(m_VehicleQuery, Dependency);
        Dependency.Complete();

        // Flush logs on main thread after job completes
        m_Logger.Flush();
    }

    [BurstCompile]
    private struct LogVehiclesJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityTypeHandle;
        public JobLogger.Writer Logger;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(EntityTypeHandle);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                Logger.LogVehicle(entity.Index, entity.Version, "Processing vehicle");
            }
        }
    }
}
```

### Example 2: Tollbooth Vehicle Tracking

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Game.Vehicles;
using Game.Net;
using Game.Simulation;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;

public partial class TollboothLoggingSystem : GameSystemBase
{
    private EntityQuery m_CarQuery;
    private JobLogger m_Logger;

    protected override void OnCreate()
    {
        base.OnCreate();
        
        m_CarQuery = GetEntityQuery(
            ComponentType.ReadOnly<Car>(),
            ComponentType.ReadOnly<CarCurrentLane>()
        );

        m_Logger = new JobLogger();
        
        // Enable only in debug builds
        #if DEBUG
        m_Logger.Initialize(Allocator.Persistent, initialCapacity: 1024, isEnabled: true);
        #else
        m_Logger.Initialize(Allocator.Persistent, initialCapacity: 64, isEnabled: false);
        #endif
    }

    protected override void OnDestroy()
    {
        m_Logger.Dispose();
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        // Set capacity based on expected vehicle count
        m_Logger.SetCapacity(m_CarQuery.CalculateEntityCount());

        var job = new TollboothTrackingJob
        {
            EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
            CarTypeHandle = SystemAPI.GetComponentTypeHandle<Car>(true),
            CarCurrentLaneTypeHandle = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(true),
            TollBoothDataLookup = SystemAPI.GetComponentLookup<TollBoothPrefabData>(true),
            EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
            Logger = m_Logger.GetWriter()
        };

        Dependency = job.ScheduleParallel(m_CarQuery, Dependency);
        Dependency.Complete();

        if (m_Logger.MessageCount > 0)
        {
            m_Logger.Flush();
        }
    }

    [BurstCompile]
    private struct TollboothTrackingJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<Car> CarTypeHandle;
        [ReadOnly] public ComponentTypeHandle<CarCurrentLane> CarCurrentLaneTypeHandle;
        [ReadOnly] public ComponentLookup<TollBoothPrefabData> TollBoothDataLookup;
        [ReadOnly] public ComponentLookup<Edge> EdgeLookup;
        
        public JobLogger.Writer Logger;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(EntityTypeHandle);
            var cars = chunk.GetNativeArray(ref CarTypeHandle);
            var currentLanes = chunk.GetNativeArray(ref CarCurrentLaneTypeHandle);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var currentLane = currentLanes[i];
                var laneEntity = currentLane.m_Lane;

                // Check if lane belongs to a tollbooth road
                if (!EdgeLookup.TryGetComponent(laneEntity, out var edge))
                    continue;

                if (!TollBoothDataLookup.HasComponent(edge.m_End))
                    continue;

                // Log vehicle passing through tollbooth
                FixedString512Bytes message = "Passing through tollbooth at edge ";
                message.Append(edge.m_End.Index);
                
                Logger.LogVehicle(entity.Index, entity.Version, message);
                
                // Log toll amount
                Logger.LogValue("TollAmount", 100); // Replace with actual calculation
            }
        }
    }
}
```

### Example 3: Conditional Logging with Vehicle Type

```csharp
[BurstCompile]
private struct VehicleTypeLoggingJob : IJobChunk
{
    [ReadOnly] public EntityTypeHandle EntityTypeHandle;
    [ReadOnly] public ComponentTypeHandle<Car> CarTypeHandle;
    [ReadOnly] public ComponentTypeHandle<Taxi> TaxiTypeHandle;
    [ReadOnly] public ComponentTypeHandle<PublicTransport> PublicTransportTypeHandle;
    
    public JobLogger.Writer Logger;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var entities = chunk.GetNativeArray(EntityTypeHandle);
        
        bool hasTaxi = chunk.Has(ref TaxiTypeHandle);
        bool hasPublicTransport = chunk.Has(ref PublicTransportTypeHandle);

        for (int i = 0; i < entities.Length; i++)
        {
            var entity = entities[i];
            
            FixedString512Bytes vehicleType = default;
            
            if (hasTaxi)
            {
                vehicleType.Append("Taxi");
            }
            else if (hasPublicTransport)
            {
                vehicleType.Append("PublicTransport");
            }
            else
            {
                vehicleType.Append("PersonalCar");
            }
            
            Logger.LogVehicle(entity.Index, entity.Version, vehicleType);
        }
    }
}
```

## Best Practices

### ✅ Do

1. **Initialize once, reuse**: Create `JobLogger` in `OnCreate()` with `Allocator.Persistent`
2. **Dispose properly**: Always dispose in `OnDestroy()`
3. **Use conditional compilation**: Disable in release builds for performance
4. **Pre-allocate capacity**: Use `SetCapacity()` when vehicle count is known
5. **Check `MessageCount`**: Skip `Flush()` if no messages were logged
6. **Use `in` parameter**: Pass `FixedString` by reference to avoid copies

### ❌ Don't

1. **Don't use string interpolation**: `$"..."` is not Burst-compatible
2. **Don't call `Flush()` inside jobs**: Only call on main thread
3. **Don't exceed capacity**: `AddNoResize` silently fails if full
4. **Don't share Writer between jobs**: Get a new `Writer` for each job

## Thread Safety

| Operation | Thread Safety |
|-----------|---------------|
| `Initialize()` | Main thread only |
| `GetWriter()` | Main thread only |
| `Writer.Log*()` | Thread-safe (parallel) |
| `Flush()` | Main thread only |
| `Dispose()` | Main thread only |

## Memory Considerations

- Each `FixedString4096Bytes` uses 4KB of memory
- Default capacity of 256 messages = ~1MB
- Adjust `initialCapacity` based on expected log volume
- Call `Flush()` regularly to prevent memory growth