# Copilot Coding Agent Instructions for Tollbooth Highways

## Project Overview
- Cities: Skylines II mod built on Unity DOTS/ECS (`TollboothHighways/`).
- Core goal: map vehicle groups to tollbooth prefabs to enforce lane restrictions and toll pricing.
- Gameplay loop spans ECS systems (`Systems/`), domain models (`Domain/`), utilities (`Utilities/`), and React-based UI (`UI/`, `Temp/`).

## Architecture & Patterns
- **Systems on main thread**: Critical vehicle monitors (e.g., `TollboothCarNavigationMonitorSystem.cs`) must run in `Game.UpdateSystem` main phase for deterministic logging.
- **Vehicle/Nav data**: Vehicles expose `CarNavigationLane` buffers (capacity 8); use `CarLaneFlags` to detect `Forbidden`, `AllowEnter`, etc. `CarLane` components hold `m_AccessRestriction` entity references for blocking logic.
- **Vehicle group mapping**: `Utilities/VehiclesUtil.cs` maps `VehicleType` to domain-specific `VehicleGroup` via burst-compatible helper; use `VehicleGroupToComponentMap` to fetch prefab data components such as `TollRoadPublicTransportData`.
- **Pathfinding interactions**: Use `Game.Vehicles.VehicleUtils.SetupPathfind` after adjusting lanes to enqueue new routes; refer to base-game `PathfindSetupSystem`/`PathfindQueueSystem` if deeper integration is needed.
- **Logging**: `Utilities/VehicleDebugLogger.cs` writes per-vehicle logs (UTF-8, UTC timestamps); initialize once via `VehicleDebugLogger.Init(modPath)` and guard log calls when `_root` is null.
- **Access restriction flow**: When a vehicle violates toll mapping, set lane `m_Flags` to include `CarLaneFlags.Forbidden`/`IsBlocked`, update `m_AccessRestriction` with the vehicle entity, and propagate to sublanes before repathing.

## Developer Workflows
- **Build**: `dotnet build TollboothHighways.sln` or build via Visual Studio; ensure game assemblies are referenced.
- **Debug**: Attach to CS2 process, enable verbose logging via `VehicleDebugLogger.LogOnce` to confirm system activation.
- **In-game validation**: No automated tests; verify behaviour in sandbox save, inspecting generated `TollboothVehicleLogs` folder.
- **System registration**: Add new systems in `Mod.cs` via `updateSystem` with proper ordering relative to pathfinding systems.

## Key Integration Details
- `Domain/Components/TollBoothPrefabData.cs` ties prefab authoring components to tollbooth metadata (pricing, allowed groups).
- `Systems/TollboothCarNavigationMonitorSystem.cs` exemplifies vehicle filtering (`Unspawned`, `Deleted`, `CarNavigation`) and lane buffer inspection.
- `Utilities/VehiclesUtil.cs` provides helper methods for route checks and vehicle group detection; prefer burst-compatible APIs in jobs.
- `Utilities/VehicleDebugLogger.cs` swallows IO errors silently (debug use); avoid relying on exceptions for control flow.
- UI builds through `UI/package.json` (Webpack/TypeScript) for mod panels; coordinate data via ECS queries exposed to UI systems.

## Implementation Checklist Example
1. Query vehicles lacking `Unspawned`/`Deleted`; iterate `CarNavigationLane` buffer (max 8 entries).
2. Resolve each lane's owner via `Game.Common.Owner` to find tollbooth entity; check for `TollBoothPrefabData` and matching `VehicleGroup`.
3. If restricted, mutate `CarLane.m_AccessRestriction`/`m_Flags`, update sublane buffers, then call `VehicleUtils.SetupPathfind` to enqueue a new route.
4. Log each decision with `VehicleDebugLogger.Log(vehicle, message)` to aid debugging.

Refer to `AGENTS.MD` for high-level requirements and `Docs/StopVehiclesOnRoadSystem.md` for concrete ECS system patterns.
