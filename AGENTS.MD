# AGENTS.MD

## Context

This project is a mod for Cities Skylines 2 in a context of a ECS environment with the DOTS architecture in Unity.

## Principal Objetive

Having specific type of Vehicles go through only a specific Tollbooth Road using a mapping to get the relationship between both of them.

## It must meet the following conditions

    - Monitor the component PathElements for all vehicles entities to detect new or deleted tollbooth roads.
    - Set PathfindCosts to a higher value so vanilla PathfindJob avoid vehicles going through invalid type of tollbooth roads.
    - Mark path as obsolete in the Vehicle CarCurrentLane to force trigger pathfinding using pathOwner.m_State |= PathFlags.Obsolete.

## Conditions

- By default all system should be in Burst-compatible code to follow the ECS best practices.
- The systems should run in main thread in order to enable debugging only if it is needed.
- Always use SystemAPI when getting lookups or typehandles for a Jobs in Burst-compatible code.
- Use BufferTypeHandle instead of BufferLookup for buffered entities lookups.
- Use ComponentTypeHandle with chunk.Has() for component checks
- Use VehiclesUtil class to check vehicles entities with tollbooth roads entities.
- Use VehicleDebugLogger to log what is the code doing for each vehicle.
- Register the system in Mod.cs to proper update the phases using updateSystem

## Resource that could help to build the solution

- Game.Net.CarLaneFlags
- Game.Vehicles.CarNavigationLane
- Game.Pathfind.PathUtils
- Game.Vehicles.VehicleUtils
- Game.Pathfind.PathfindCosts
- Game.Simulation.PathfindSetupSystem
- Game.Pathfind.PathfindParameters
- TollboothHighways.Utilities.VehiclesUtil
- TollboothHighways.Domain.Components.TollBoothPrefabData
- Game.Common.Owner
- TollboothHighways.Utilities.VehicleDebugLogger
- Game.UpdateSystem
