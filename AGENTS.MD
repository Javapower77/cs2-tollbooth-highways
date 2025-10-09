# AGENTS.MD

## Context

This project is a mod for Cities Skylines 2 in a context of a ECS environment with the DOTS architecture in Unity.

## Principal Objetive

Having specific type of Vehicles go through only a specific Tollbooth Road using a mapping to related both of them.

## It must meet the following conditions

- Monitor every vehicle in the game that doesnt have Unspawned and Deleted component.
- Check the CarLaneNavigation component that has 8 buffered entities representing the next 8 roads where the vehicle will go through.
- If a tollbooth road is found in any of the array element validate if is a valid tollbooth road for the current vehicle. m_Lane property contains the road entity. Having this entity it must get the tollbooth road specified in the Game.Common.Owner. Then check for the component TollBoothPrefabData.
- If it is valid, do nothing.
- If it is not valid, must set the m_Flags property in IsBlocked.
- Then get the sublanes of the tollbooth road to set the m_AccessRestriction property with the entity of the current car.
- Finally force the repath using VehicleUtils.SetupPathfind.

## Conditions

- The system must run in main thread in order to enable debugging.
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
