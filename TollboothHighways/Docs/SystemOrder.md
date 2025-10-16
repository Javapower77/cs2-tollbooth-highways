# System Order

## Understanding System Update Phases in Cities: Skylines 2

`SystemUpdatePhase` enum defines the execution timing for various systems in your Cities Skylines 2 mod. This is a crucial concept in the DOTS (Data-Oriented Technology Stack) architecture—it determines **when** each system runs during the game loop.

## What is SystemUpdatePhase?

`SystemUpdatePhase` is an enumeration that represents different execution windows during a single frame. Think of it like a schedule—each phase is a time slot when certain systems are allowed to run. The enum contains 32 different phases, each serving a specific purpose in the game's simulation pipeline. The phases are ordered numerically, and systems execute in this sequence: `MainLoop` (0) → `LateUpdate` (1) → `Modification1` through `Modification5` → `GameSimulation` (12) → and so on, eventually reaching `Cleanup` (31).

## The Registration Pattern

The `UpdateAt<SystemType>(phase)` method schedules your system to run in a specific phase. The `UpdateAfter` and `UpdateBefore` methods provide fine-grained ordering control when you need to run a system relative to another system within the same phase. This prevents hard-coding specific frame indices and instead creates dependencies—a more maintainable approach as the game engine evolves.

## A Subtle but Important Detail

Notice the `GetUpdateInterval()` method returns 16 for `PrefabUpdate` until initialization completes, then 0. An interval of 0 means "run every frame," while 16 means "run every 16th frame." This is a performance optimization—prefab loading doesn't need to happen every single frame, so the system throttles itself until ready, then stops entirely once initialized.

## Understanding SystemUpdatePhase

`SystemUpdatePhase` is an enumeration that defines 32 distinct execution windows during each frame of the game loop. Each phase represents a specific moment when certain systems are allowed to run, creating a predictable order for all simulation logic. This is fundamental to the DOTS architecture—it ensures that dependencies between systems are honored and prevents race conditions where one system might read stale data from another.

## Registration and Execution Order

The `UpdateSystem` uses three primary registration methods: `UpdateAt<SystemType>(phase)` schedules a system to run in a specific phase, `UpdateBefore<SystemType>(phase)` runs a system before others in that phase (by subtracting 1,000,000 from the sort index), and `UpdateAfter<SystemType>(phase)` runs it after (by adding 1,000,000). This sorting mechanism allows fine-grained control—when you call `UpdateAfter<TollBoothLaneFlagEnforcementSystem, VehicleTollboothPathMonitoringSystem>(GameSimulation)`, you're telling the engine that lane flags must be enforced before path monitoring checks occur, ensuring vehicles have valid lane assignments before recalculating routes.

## Burst Compatibility and Interval Management

The `GetUpdateInterval()` method on your systems returns either 1 (run every frame) or a power-of-2 value (run every Nth frame for throttling). The `Update()` method in `UpdateSystem` uses bitwise operations to check `(updateIndex & (interval - 1)) == offset`, which efficiently determines if the current frame matches the system's execution schedule. This is why your instructions emphasize keeping systems Burst-compatible—it allows the engine to schedule and run many systems without garbage collection overhead, critical for a city simulation that spawns and destroys thousands of entities every frame.

## Purpose and Structure

`SystemOrder` is a static class that initializes the execution schedule for all game systems in Cities: Skylines 2. The `Initialize` method receives an `UpdateSystem` instance and registers every system with a specific execution phase. This centralized registration point ensures predictable system ordering across the entire game loop, which is essential for correctness in an ECS architecture where systems read and write shared data.

## How System Registration Works

The class uses three primary registration methods: `UpdateAt<T>()` schedules a system to run during a specific phase, `UpdateBefore<T>()` runs a system before another system or barrier, and `UpdateAfter<T>()` runs it after. Each phase represents a distinct execution window within a single frame. For example, `Modification5` is where entity initialization and data collection occur, while `GameSimulation` handles active game logic like vehicle navigation and AI behavior. By organizing systems into phases, the engine prevents common issues like a system reading data that hasn't been written yet or two systems conflicting over the same component.

## Phase Organization and Dependencies

The phases follow a logical progression through the frame: `MainLoop` handles initialization and framework setup, followed by `Modification1` through `Modification5` which progressively build up world state through entity creation and reference resolution. `GameSimulation` executes all active game logic—pathfinding, vehicle movement, economic systems—while `ModificationEnd` finalizes data structures and notifies dependent systems of changes. Phases like `Rendering`, `UIUpdate`, and `PrefabUpdate` operate in isolation from core simulation, preventing UI or asset loading from interfering with game logic.

## Performance and Debugging Implications

The massive registration list demonstrates how complex modern game simulations are. By centralizing all scheduling here, developers can see the complete execution order at a glance. When debugging performance issues or unexpected behavior, you can trace exactly when each system runs relative to others. The `RegisterGPUSystem<WaterSystem>()` call at the start shows that some systems run on the GPU entirely separately from the CPU-based ECS loop, highlighting how the engine partitions work across different hardware.

## Cities: Skylines 2 System Execution Order by Phase

## **MainLoop** (Phase 0)

Primary framework initialization and core engine systems.

- `RaycastSystem`
- `PrefabSystem`
- `CityConfigurationSystem`
- `ToolSystem`
- `LoadGameSystem`
- `ModificationSystem`
- `UnlockSystem`
- `PathfindSetupSystem`
- `PathfindResultSystem`
- `AchievementTriggerSystem`
- `UIUpdateSystem`
- `RenderingSystem`
- `SaveGameSystem`

---

## **LateUpdate** (Phase 1)

Post-frame cleanup and final updates.

- `DebugSystem`
- `SimulationSystem`
- `CompleteRenderingSystem`
- `GizmosSystem`
- `AutoSaveSystem`

---

## **Modification1** (Phase 2)

Initial entity generation and route setup.

- `RoutePathReadySystem`
- `GenerateObjectsSystem`
- `GenerateNodesSystem`
- `GenerateZonesSystem`
- `GenerateAreasSystem`
- `GenerateWaypointsSystem`
- `GenerateNotificationsSystem`
- `GenerateBrushesSystem`
- `GenerateAggregatesSystem`
- `GenerateWaterSourcesSystem`
- `LoanSystem`
- `CitySystem`
- `UnlockAllSystem`
- `AddMeetingSystem`
- `ElectricityGraphDeleteSystem`
- `WaterPipeGraphDeleteSystem`

---

## **Modification2** (Phase 3)

Edge generation, route creation, and audio setup.

- `Game.Events.InitializeSystem`
- `GenerateEdgesSystem`
- `GenerateRoutesSystem`
- `TrafficRoutesSystem`
- `AudioGroupingSystem`
- `WeatherAudioSystem`
- `HouseholdAndCitizenRemoveSystem`
- `GroundHeightSystem`
- `Game.Buildings.InitializeSystem`
- `OutsideConnectionInitializeSystem`
- `DamageSystem`
- `DestroySystem`

---

## **Modification2B** (Phase 3.5)

Reference resolution and graph system initialization.

- `FindOwnersSystem2`
- `Game.Routes.ReferencesSystem`
- `Game.Areas.GeometrySystem`
- `Game.Net.ReferencesSystem`
- `ServiceUpgradeReferencesSystem`
- `SubAreaReferencesSystem`
- `NodeReductionSystem`
- `NodeAlignSystem`
- `AggregateSystem`
- `Game.Objects.SubObjectSystem`
- `ElementSystem`
- `ElectricityEdgeGraphSystem`
- `WaterPipeEdgeGraphSystem`

---

## **Modification3** (Phase 4)

Policies, composition, and connection setup.

- `DefaultPoliciesSystem`
- `SubObjectReferencesSystem`
- `FindOwnersSystem`
- `AttachSystem`
- `CompositionSelectSystem`
- `TutorialDeactivationSystem`
- `ElectricityOutsideConnectionGraphSystem`
- `WaterPipeOutsideConnectionGraphSystem`

---

## **Modification4** (Phase 5)

Primary network and entity initialization.

- `DistrictModifierInitializeSystem`
- `BuildingModifierInitializeSystem`
- `RouteModifierInitializeSystem`
- `ModifiedSystem`
- `SubNetReferencesSystem`
- `NetCompositionSystem`
- `NetCompositionMeshRefSystem`
- `Game.Net.GeometrySystem`
- `NetComponentsSystem`
- `AttachPositionSystem`
- `AlignSystem`
- `OutsideConnectionSystem`
- `LaneSystem`
- `BlockSystem`
- `HouseholdInitializeSystem`
- `Game.Routes.InitializeSystem`
- `ComponentsSystem`
- `HouseholdPetRemoveSystem`
- `ImpactSystem`
- `IgniteSystem`
- `FaceWeatherSystem`
- `AddHealthProblemSystem`
- `SpectateSystem`
- `EndangerSystem`
- `AddAccidentSiteSystem`
- `AddCriminalSystem`
- `SubmergeSystem`
- `ServiceUpgradeSystem`
- `TripResetSystem`
- `EventJournalInitializeSystem`
- `TutorialActivationSystem`

---

## **Modification4B** (Phase 5.5)

Lane and building references, vehicle systems.

- `ObjectEmergeSystem`
- `LaneReferencesSystem`
- `Game.Buildings.ReferencesSystem`
- `BuildingPoliciesSystem`
- `LaneOverlapSystem`
- `AreaConnectionSystem`
- `RoadConnectionSystem`
- `ParkedVehiclesSystem`
- `TrafficLightInitializationSystem`
- `SecondaryObjectSystem`
- `SecondaryLaneSystem`
- `ElectricityBuildingGraphSystem`
- `WaterPipeBuildingGraphSystem`
- `BuildingStateEfficiencySystem`

---

## **Modification5** (Phase 6)

Vehicle initialization, lane enforcement, and search systems.

- `RemovedSystem`
- `Game.Creatures.InitializeSystem`
- `NetObjectInitializeSystem`
- `Game.Objects.UpdateCollectSystem`
- `Game.Net.UpdateCollectSystem`
- `Game.Zones.UpdateCollectSystem`
- `Game.Areas.UpdateCollectSystem`
- `ZoneBuiltRequirementSystem`
- `SegmentCurveSystem`
- `Game.Objects.SearchSystem`
- `Game.Net.SearchSystem`
- `Game.Zones.SearchSystem`
- `Game.Areas.SearchSystem`
- `Game.Routes.SearchSystem`
- `LocalEffectSystem`
- `Game.Creatures.ReferencesSystem`
- `GroupSystem`
- `BlockReferencesSystem`
- `SecondaryObjectReferencesSystem`
- `SecondaryLaneReferencesSystem`
- `SurfaceUpdateSystem`
- `WaypointConnectionSystem`
- `SpawnLocationConnectionSystem`
- `LaneConnectionSystem`
- `Game.Vehicles.InitializeSystem`
- `Game.Vehicles.ReferencesSystem`
- `UpdateGroupSystem`
- `CellCheckSystem`
- `LotHeightSystem`
- `Game.Objects.OverrideSystem`
- `LaneBlockSystem`
- `FixParkingLocationSystem`
- `Game.Net.OverrideSystem`
- `FixLaneObjectsSystem`
- `CurrentDistrictSystem`
- `ServiceDistrictSystem`
- `Game.Net.InitializeSystem`
- `EdgeMappingSystem`
- `CargoTransportStationInitializeSystem`
- `ResourcesInitializeSystem`
- `BatteryInitializeSystem`
- `ParkInitializeSystem`
- `CitizenInitializeSystem`
- `HouseholdPetInitializeSystem`
- `MeetingInitializeSystem`
- `Game.Citizens.CompanyInitializeSystem`
- `StorageInitializeSystem`
- `CityServiceWorkplaceInitializeSystem`
- `LaneHiddenSystem`
- `SubObjectHiddenSystem`
- `LanePoliciesSystem`
- `LaneDataUnknownEscalateSystem`
- `IconDeletedSystem`
- `IconAnimationSystem`
- `InitializeSchoolSystem`
- `CostSystem`
- `EventJournalSystem`
- `TutorialTriggerSystem`
- `ElectricityRoadConnectionGraphSystem`
- `WaterPipeRoadConnectionGraphSystem`

---

## **ModificationEnd** (Phase 7)

Final modifications, validation, and pathfinding preparation.

- `InstanceCountSystem`
- `LaneDataSystem`
- `RouteDataSystem`
- `ParkingLaneDataSystem`
- `LanesModifiedSystem`
- `RoutesModifiedSystem`
- `RoutePathSystem`
- `BoardingVehicleSystem`
- `MarkerCreateSystem`
- `PathOwnerTargetMovedSystem`
- `ZoneCheckSystem`
- `AreaResourceSystem`
- `SurfaceExpandSystem`
- `ConnectionWarningSystem`
- `CoveragePreviewSystem`
- `HeatmapPreviewSystem`
- `ValidationSystem`
- `AnimationUpdateSystem`
- `AnimationSystem`
- `QuantityUpdateSystem`
- `UnspawnedSystem`
- `ServiceRequestSystem`
- `IconCommandSystem`
- `XPBuiltSystem`
- `NetXPSystem`
- `XPSystem`
- `MilestoneSystem`
- `WaterSourceInitializeSystem`
- `ResetOverriddenSystem`
- `TriggerSystem`
- `LifePathEventSystem`
- `CreateChirpSystem`
- `RadioTagSystem`
- `SchoolUpdatedSystem`
- `WaterPoweredInitializeSystem`
- `TutorialSystem`
- `EventAchievementTriggerSystem`
- `StrictObjectBuiltRequirementSystem`
- `ObjectBuiltRequirementSystem`
- `PrefabUnlockedRequirementSystem`
- `ElectricityGraphReferencesSystem`
- `WaterPipeGraphReferencesSystem`
- `CityServiceEfficiencySystem`

---

## **GameSimulation** (Phase 12)

**Main simulation phase** - Vehicle AI, navigation, and game logic.

### Navigation & Movement

- `CarNavigationSystem`
- `TrainNavigationSystem`
- `WatercraftNavigationSystem`
- `AircraftNavigationSystem`
- `HumanNavigationSystem`
- `AnimalNavigationSystem`
- `CarMoveSystem`
- `CarTrailerMoveSystem`
- `TrainMoveSystem`
- `WatercraftMoveSystem`
- `AircraftMoveSystem`
- `HumanMoveSystem`
- `AnimalMoveSystem`

### Vehicle AI Systems

- `AmbulanceAISystem`
- `TransportCarAISystem`
- `GarbageTruckAISystem`
- `TransportTrainAISystem`
- `FireEngineAISystem`
- `PoliceCarAISystem`
- `TaxiAISystem`
- `MaintenanceVehicleAISystem`
- `TransportWatercraftAISystem`
- `WorkWatercraftAISystem`
- `PostVanAISystem`
- `TransportAircraftAISystem`
- `FireAircraftAISystem`
- `PoliceAircraftAISystem`
- `MedicalAircraftAISystem`
- `HearseAISystem`
- `WorkCarAISystem`
- `DeliveryTruckAISystem`
- `PersonalCarAISystem`
- `VehicleOutOfControlSystem`
- `PetAISystem`
- `WildlifeAISystem`
- `DomesticatedAISystem`

### Citizen & Household Systems

- `ResidentAISystem`
- `HouseholdBehaviorSystem`
- `TouristHouseholdBehaviorSystem`
- `HouseholdPetBehaviorSystem`
- `CitizenBehaviorSystem`
- `CitizenHappinessSystem`
- `LeisureSystem`

### Economic & Resource Systems

- `IndustrialSpawnSystem`
- `CommercialSpawnSystem`
- `BuyingCompanySystem`
- `ResourceExporterSystem`
- `StorageCompanySystem`
- `StorageTransferSystem`
- `WorkProviderSystem`
- `Game.Simulation.StudentSystem`
- `ProcessingCompanySystem`
- `ExtractorCompanySystem`
- `ResourceFlowSystem`
- `ServiceCompanySystem`
- `TradeSystem`
- `ResourceBuyerSystem`
- `ResourceProducerSystem`

### Population & Demographics

- `HouseholdSpawnSystem`
- `TouristSpawnSystem`
- `CommuterSpawnSystem`
- `HouseholdFindPropertySystem`
- `HouseholdMoveAwaySystem`
- `IndustrialFindPropertySystem`
- `CommercialFindPropertySystem`
- `PropertyProcessingSystem`
- `FindSchoolSystem`
- `FindJobSystem`
- `TouristFindTargetSystem`
- `BirthSystem`
- `DivorceSystem`
- `LookForPartnerSystem`
- `PartnerSystem`
- `LeaveHouseholdSystem`
- `DeathCheckSystem`
- `SicknessCheckSystem`
- `AgingSystem`
- `GraduationSystem`
- `CrimeCheckSystem`

### Environmental & Utility Systems

- `Game.Simulation.ServiceCoverageSystem`
- `ResourceAvailabilitySystem`
- `NetEdgeDensitySystem`
- `GroundWaterSystem`
- `GroundWaterPollutionSystem`
- `NaturalResourceSystem`
- `TerrainAttractivenessSystem`
- `PopulationToGridSystem`
- `AvailabilityInfoToGridSystem`
- `GroundPollutionSystem`
- `BuildingPollutionAddSystem`
- `NoisePollutionSystem`
- `AirPollutionSystem`
- `NetPollutionSystem`
- `WaterPipePollutionSystem`
- `TelecomCoverageSystem`
- `TelecomEfficiencySystem`
- `ElectricityFlowSystem`
- `WaterPipeFlowSystem`
- `DispatchElectricitySystem`
- `DispatchWaterSystem`
- `ElectricityTradeSystem`
- `WaterTradeSystem`
- `ElectricityStatusSystem`
- `ElectricityStatisticsSystem`
- `WaterStatisticsSystem`

### Building & Facility AI

- `PowerPlantAISystem`
- `BatteryAISystem`
- `TransformerAISystem`
- `TransportStationAISystem`
- `HospitalAISystem`
- `TransportDepotAISystem`
- `TrafficSpawnerAISystem`
- `SewageOutletAISystem`
- `WaterPumpingStationAISystem`
- `GarbageFacilityAISystem`
- `FireStationAISystem`
- `PoliceStationAISystem`
- `MaintenanceDepotAISystem`
- `PostFacilityAISystem`
- `ExtractorFacilityAISystem`
- `EmergencyShelterAISystem`
- `DeathcareFacilityAISystem`
- `PrisonAISystem`
- `TelecomFacilityAISystem`
- `FirewatchTowerAISystem`
- `ParkingFacilityAISystem`
- `SchoolAISystem`
- `ParkAISystem`

### Dispatch Systems

- `MailTransferDispatchSystem`
- `GarbageCollectorDispatchSystem`
- `TransportVehicleDispatchSystem`
- `RandomTrafficDispatchSystem`
- `FireRescueDispatchSystem`
- `PolicePatrolDispatchSystem`
- `TaxiDispatchSystem`
- `HealthcareDispatchSystem`
- `MaintenanceVehicleDispatchSystem`
- `PostVanDispatchSystem`
- `PoliceEmergencyDispatchSystem`
- `EvacuationDispatchSystem`
- `PrisonerTransportDispatchSystem`
- `GarbageTransferDispatchSystem`

### Safety & Health Systems

- `FireSimulationSystem`
- `CrimeAccumulationSystem`
- `CrimeEffectSystem`
- `FireHazardSystem`
- `RoadSafetySystem`
- `WeatherHazardSystem`
- `DestroyAbandonedSystem`
- `PollutionTriggerSystem`
- `ObjectPolluteSystem`

### Traffic & Transportation

- `TrafficAmbienceSystem`
- `TrafficFlowSystem`
- `OutsideConnectionDelaySystem`
- `NetLaneReservationSystem`
- `TrafficLightSystem`
- `StreetLightSystem`
- `TrafficBottleneckSystem`
- `StuckMovingObjectSystem`
- `ObjectCollisionSystem`

### Time, Weather & Events

- `TimeSystem`
- `PlanetarySystem`
- `ClimateSystem`
- `SnowSystem`
- `WindSimulationSystem`
- `WindSystem`
- `CalendarEventLaunchSystem`
- `WeatherPhenomenonSystem`
- `WeatherDamageSystem`
- `WaterLevelChangeSystem`

### Statistics & Economy

- `CityDangerLevelSystem`
- `ServiceFeeSystem`
- `XPAccumulationSystem`
- `CityStatisticsSystem`
- `LandValueSystem`
- `LoanUpdateSystem`
- `BrandPopularitySystem`
- `MailAccumulationSystem`
- `MailBoxSystem`
- `CountStudyPositionsSystem`
- `CountWorkplacesSystem`
- `CountCompanyDataSystem`
- `CountHouseholdDataSystem`
- `CountResidentialPropertySystem`
- `CompanyStatisticsSystem`
- `CityServiceStatisticsSystem`
- `CrimeStatisticsSystem`
- `WorkProviderStatisticsSystem`
- `WealthStatisticsSystem`
- `PayWageSystem`
- `UtilityFeeSystem`
- `NetUpkeepSystem`
- `BudgetSystem`
- `TaxSystem`
- `CountConsumptionSystem`
- `GameModeWealthSupportSystem`
- `GameModeGovernmentSubsidiesSystem`
- `GameModeNaturalResourcesAdjustSystem`
- `BuildingUpkeepSystem`
- `CityServiceUpkeepSystem`
- `RentAdjustSystem`
- `CompanyDividendSystem`
- `CompanyProfitabilitySystem`
- `AdjustElectricityConsumptionSystem`
- `AdjustWaterConsumptionSystem`

### Behavior & Mechanics

- `PersonalCarOwnerSystem`
- `ApplyToSchoolSystem`
- `HouseholdPetSpawnSystem`
- `CreatureSpawnSystem`
- `AttractionSystem`
- `CitizenEvacuateSystem`
- `CriminalSystem`
- `ZoneSpawnSystem`
- `VehicleSpawnSystem`
- `HealthProblemSystem`
- `FloodCheckSystem`
- `BuildingEfficiencySystem`
- `FindEventAttendantsSystem`
- `CitizenFindJobSystem`
- `TreeGrowthSystem`
- `WetnessSystem`
- `DirtynessSystem`
- `InDangerSystem`
- `WaterDangerSystem`
- `WaterDamageSystem`
- `AreaSpawnSystem`
- `AccidentCreatureSystem`
- `CitizenPresenceSystem`
- `CondemnedBuildingSystem`
- `BuildingConstructionSystem`
- `BudgetApplySystem`
- `TransportLineSystem`
- `EventTickSystem`
- `TaxiStandSystem`
- `CityModifierUpdateSystem`
- `LocalEffectUpdateSystem`
- `RideNeederSystem`
- `TransportStopSystem`
- `WaitingPassengersSystem`
- `DamagedVehicleSystem`
- `AreaLotSimulationSystem`
- `CreatureSpawnerSystem`
- `ResidentPurposeCounterSystem`
- `TourismSystem`
- `ZoneAmbienceSystem`
- `EffectFlagSystem`
- `AccidentSiteSystem`
- `AccidentVehicleSystem`
- `SpectatorSiteSystem`
- `VehicleLaunchSystem`
- `CollapsedBuildingSystem`
- `CommercialAISystem`
- `IndustrialAISystem`
- `GarbageAccumulationSystem`
- `NetDeteriorationSystem`
- `CompanyMoveAwaySystem`
- `PropertyRenterRemoveSystem`
- `LodgingProviderSystem`
- `TouristLeaveSystem`
- `ExtractorAISystem`

---

## **EditorSimulation** (Phase 13)

Editor-specific simulation (time, weather, climate).

- `TimeSystem`
- `PlanetarySystem`
- `ClimateSystem`
- `SnowSystem`
- `WindSimulationSystem`
- `WindSystem`

---

## **LoadSimulation** (Phase 14)

Navigation and AI during game load.

- `CarNavigationSystem`
- `TrainNavigationSystem`
- `WatercraftNavigationSystem`
- `AircraftNavigationSystem`
- `TransportCarAISystem`
- `TransportTrainAISystem`
- `TransportWatercraftAISystem`
- `TransportAircraftAISystem`
- `DeliveryTruckAISystem`
- `PersonalCarAISystem`
- `CarMoveSystem`
- `CarTrailerMoveSystem`
- `TrainMoveSystem`
- `WatercraftMoveSystem`
- `AircraftMoveSystem`
- `TrafficSpawnerAISystem`
- `RandomTrafficDispatchSystem`

---

## **PostSimulation** (Phase 15)

Post-game simulation systems.

- `WaterSystem`

---

## **PreCulling** (Phase 16)

Visibility culling and rendering preparation.

- `UndergroundViewSystem`
- `PreCullingSystem`
- `OverlayInfomodeSystem`
- `AreaBatchSystem`
- `EffectControlSystem`
- `AggregateMeshSystem`
- `BatchMeshSystem`
- `TerrainMaterialSystem`
- `TerrainRenderSystem`
- `WaterRenderSystem`
- `VegetationRenderSystem`
- `LightingSystem`
- `AreaBufferSystem`
- `CityBoundaryMeshSystem`
- `RouteBufferSystem`
- `MeshColorSystem`
- `WindTextureSystem`

---

## **Rendering** (Phase 17)

Object rendering and visual updates.

- `BatchInstanceSystem`
- `InitializeAnimatedSystem`
- `InitializeBonesSystem`
- `InitializeBoneHistoriesSystem`
- `InitializeLightsSystem`
- `ManagedBatchSystem`
- `BatchManagerSystem`
- `ObjectInterpolateSystem`
- `EventInterpolateSystem`
- `RelativeObjectSystem`
- `AnimatedSystem.Prepare`
- `EffectTransformSystem`
- `SFXCullingSystem`
- `LightCullingSystem`
- `ObjectColorSystem`
- `NetColorSystem`
- `AreaColorSystem`
- `PhotoModeRenderSystem`
- `ClimateRenderSystem`
- `NotificationIconLocationSystem`
- `MarkerIconSystem`
- `NotificationIconBufferSystem`
- `ProceduralSkeletonSystem`
- `ProceduralEmissiveSystem`
- `ProceduralUploadSystem.Prepare`
- `AggregateRenderSystem`
- `RouteRenderSystem`
- `BuildingLotRenderSystem`
- `AreaBorderRenderSystem`
- `GuideLinesSystem`
- `BrushRenderSystem`
- `EditorGizmoSystem`
- `EffectRangeRenderSystem`
- `BatchDataSystem`
- `BatchRendererSystem`
- `AreaRenderSystem`
- `OverlayRenderSystem`
- `VFXSystem`
- `ProceduralUploadSystem`

---

## **CompleteRendering** (Phase 18)

Final rendering completion.

- `NotificationIconRenderSystem`

---

## **UIUpdate** (Phase 22)

User interface updates.

- `RichPresenceUpdateSystem`
- `UIHighlightSystem`
- `DebugUISystem`
- `MenuUISystem`
- `WhatsNewPanelUISystem`
- `NotificationUISystem`
- `ToolUISystem`
- `OptionsUISystem`
- `InputRebindingUISystem`
- `AssetUploadPanelUISystem`
- `StandaloneAssetUploadPanelUISystem`
- `TooltipUISystem`
- *(60+ additional UI systems)*

---

## **UITooltip** (Phase 23)

Tooltip rendering and display.

- `TempCostTooltipSystem`
- `TempXPTooltipSystem`
- `TempRenewableElectricityProductionTooltipSystem`
- `TempWaterPumpingTooltipSystem`
- `TempExtractorTooltipSystem`
- *(20+ additional tooltip systems)*

---

## **PrefabUpdate** (Phase 24)

Prefab loading and asset initialization.

- `TextureStreamingSystem`
- `GeometryAssetLoadingSystem`
- `PrefabInitializeSystem`
- `MeshSystem`
- `AnimatedPrefabSystem`
- `UIInitializeSystem`
- `TerrainInitializeSystem`
- `NetInitializeSystem`
- `ObjectInitializeSystem`
- `ZoneSystem`
- `AreaInitializeSystem`
- `Game.Prefabs.CompanyInitializeSystem`
- `ResourceSystem`
- `ZonePrefabInitializeSystem`
- `BuildingInitializeSystem`
- `LotInitializeSystem`
- `InfoviewInitializeSystem`
- `VehicleInitializeSystem`
- `RouteInitializeSystem`
- `EffectInitializeSystem`
- `VehicleCapacitySystem`
- `NotificationIconPrefabSystem`
- `TriggerPrefabSystem`

---

## **DebugGizmos** (Phase 25)

Debug visualization (gizmos).

- `WaterPipeDebugSystem`
- `ObjectDebugSystem`
- `NetDebugSystem`
- `LaneDebugSystem`
- `ZoneDebugSystem`
- *(25+ additional debug systems)*

---

## **Raycast** (Phase 31)

Raycasting for tool interaction.

- `ToolRaycastSystem`
