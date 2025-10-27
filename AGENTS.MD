# AGENTS.MD

## Context

This project is a mod for Cities Skylines 2 in a context of a ECS environment with the DOTS architecture in Unity.

## Principal Objetive

Having specific type of Vehicles go through only a specific Tollbooth Road using a mapping to get the relationship between both of them.

## It must meet the following conditions

- For the logic of having specific vehicle type with a specific tollbooth road type take this in consideration
  - Set CarLaneFlags in the tollbooth road lane acording the following
    - CarLaneFlags.ForbidHeavyTraffic -> Private Transport: Block heavy vehicles (trucks) for allowing only PersonalCar vehicles.
    - CarLaneFlags.ForbidTransitTraffic -> Trucks Only: Block transit traffic (private cars, buses) for allowing only DeliveryTruck vehicles.
    - CarLaneFlags.PublicOnly -> Public Transport Only: Allow only public transport vehicles for allowing only Buses and Taxis vehicles.

## Best Practices to follow
  
- By default all system should be in Burst-compatible code to follow the ECS best practices.
- System should inherit from GameSystemBase (not ISYSTEM).
- The systems should run in parallel threads in order to improve at best the performance.
- Always use SystemAPI when getting lookups or typehandles for a Jobs in Burst-compatible code.
- Use BufferTypeHandle instead of BufferLookup for buffered entities lookups.
- Use ComponentTypeHandle instead of ComponentLookup for not buffered component lookups.
- "Update(this)" should be not necessary for lookups as it is handled internally.
- Use ComponentTypeHandle with chunk.Has() for component checks.
- Use VehiclesUtil class to check vehicles entities with tollbooth roads entities.
- Use VehicleDebugLogger to log what is the code doing for each vehicle.
- Register the system in Mod.cs to proper update the phases using updateSystem.

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
