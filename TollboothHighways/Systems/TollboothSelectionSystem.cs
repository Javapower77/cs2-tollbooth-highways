using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Game.UI.Editor;
using System;
using System.Collections.Generic;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Game.Input.UIBaseInputAction;

namespace TollboothHighways.Systems
{
    public partial class TollboothSelectionSystem : UISystemBase
    {
        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultToolSystem;
        private EntityQuery m_TollBoothQuery;
        private Entity m_HoveredEntity = Entity.Null;
        private Entity m_LastKnownSelected = Entity.Null;

        public Action<Entity> HoveredEntityChanged { get; set; }

        public Entity GetCurrentHoveredEntity()
        {
            try
            {
                //LogUtil.Info($"TollboothSelectionSystem: GetCurrentHoveredEntity() - Returning hovered entity: {m_HoveredEntity.Index}");
                return m_HoveredEntity;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: GetCurrentHoveredEntity() - EXCEPTION getting hovered entity: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: GetCurrentHoveredEntity() - Stack trace: {ex.StackTrace}");
                return Entity.Null;
            }
        }

        protected override void OnCreate()
        {
            LogUtil.Info("TollboothSelectionSystem: OnCreate() - Starting system creation");

            try
            {
                base.OnCreate();
                LogUtil.Info("TollboothSelectionSystem: OnCreate() - Base.OnCreate() completed successfully");

                LogUtil.Info("TollboothSelectionSystem: OnCreate() - Getting managed systems");
                try
                {
                    m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
                    LogUtil.Info($"TollboothSelectionSystem: OnCreate() - ToolSystem acquired: {(m_ToolSystem != null ? "SUCCESS" : "FAILED")}");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollboothSelectionSystem: OnCreate() - Failed to get ToolSystem: {ex.Message}");
                    throw;
                }

                try
                {
                    m_DefaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
                    LogUtil.Info($"TollboothSelectionSystem: OnCreate() - DefaultToolSystem acquired: {(m_DefaultToolSystem != null ? "SUCCESS" : "FAILED")}");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollboothSelectionSystem: OnCreate() - Failed to get DefaultToolSystem: {ex.Message}");
                    throw;
                }

                LogUtil.Info("TollboothSelectionSystem: OnCreate() - Creating entity query for toll booths");
                try
                {
                    m_TollBoothQuery = GetEntityQuery(
                        ComponentType.ReadOnly<Domain.Components.TollBoothPrefabData>(),
                        ComponentType.ReadOnly<PrefabRef>()
                    );
                    LogUtil.Info($"TollboothSelectionSystem: OnCreate() - Entity query created successfully. IsEmpty: {m_TollBoothQuery.IsEmpty}");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollboothSelectionSystem: OnCreate() - Failed to create entity query: {ex.Message}");
                    throw;
                }

                LogUtil.Info("TollboothSelectionSystem: OnCreate() - Setting RequireForUpdate");
                try
                {
                    RequireForUpdate(m_TollBoothQuery);
                    LogUtil.Info("TollboothSelectionSystem: OnCreate() - RequireForUpdate set successfully");
                }
                catch (System.Exception ex)
                {
                    LogUtil.Error($"TollboothSelectionSystem: OnCreate() - Failed to set RequireForUpdate: {ex.Message}");
                    throw;
                }

                LogUtil.Info("TollboothSelectionSystem: OnCreate() - System created successfully with active selection handling");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: OnCreate() - CRITICAL ERROR during system creation: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: OnCreate() - Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        protected override void OnUpdate()
        {
            LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Starting update cycle");

            try
            {
                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Handling mouse hover");
                HandleMouseHover();
                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Mouse hover handled successfully");

                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Handling mouse selection");
                HandleMouseSelection();
                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Mouse selection handled successfully");

                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Monitoring selection changes");
                MonitorSelectionChanges();
                LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Selection changes monitored successfully");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: OnUpdate() - CRITICAL ERROR in update cycle: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: OnUpdate() - Update cycle completed");
        }

        private void HandleMouseSelection()
        {
            LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Starting mouse selection handling");

            try
            {
                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Checking tool system state");
                if (m_ToolSystem.activeTool != m_DefaultToolSystem)
                {
                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Not using default tool (current: {m_ToolSystem.activeTool?.GetType().Name}), skipping selection");
                    return;
                }

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Checking mouse input");
                if (UnityEngine.InputSystem.Mouse.current == null)
                {
                    LogUtil.Warn("TollboothSelectionSystem: HandleMouseSelection() - Mouse.current is null, skipping selection");
                    return;
                }

                if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                {
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Left mouse button not pressed this frame, skipping selection");
                    return;
                }

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Left mouse button pressed, processing selection");

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Getting main camera");
                var camera = Camera.main;
                if (camera == null)
                {
                    LogUtil.Warn("TollboothSelectionSystem: HandleMouseSelection() - Camera.main is null, cannot process selection");
                    return;
                }

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Getting mouse position and creating ray");
                var mousePosition = InputManager.instance.mousePosition;
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Mouse position: {mousePosition}");

                var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Ray created: Origin={ray.origin}, Direction={ray.direction}");

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Creating instance entity query");
                var instanceQuery = GetEntityQuery(
                    ComponentType.ReadOnly<TollBoothPrefabData>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>(),
                    ComponentType.Exclude<Deleted>()
                );
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Instance query created. IsEmpty: {instanceQuery.IsEmpty}");

                LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Getting toll booth entities array");
                var tollBoothEntities = instanceQuery.ToEntityArray(Allocator.TempJob);
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Found {tollBoothEntities.Length} toll booth instance entities");

                Entity selectedTollbooth = Entity.Null;
                float closestDistance = float.MaxValue;

                try
                {
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Starting entity iteration");
                    for (int i = 0; i < tollBoothEntities.Length; i++)
                    {
                        var entity = tollBoothEntities[i];
                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Processing entity {i + 1}/{tollBoothEntities.Length}: {entity.Index}");

                        try
                        {
                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Checking PrefabRef for entity {entity.Index}");
                            if (!EntityManager.HasComponent<PrefabRef>(entity))
                            {
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} has no PrefabRef, skipping");
                                continue;
                            }

                            var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} has PrefabRef to {prefabRef.m_Prefab.Index}");

                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Checking if prefab {prefabRef.m_Prefab.Index} has TollBoothPrefabData");
                            if (!EntityManager.HasComponent<TollBoothPrefabData>(prefabRef.m_Prefab))
                            {
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Prefab {prefabRef.m_Prefab.Index} has no TollBoothPrefabData, skipping");
                                continue;
                            }

                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Getting Transform for entity {entity.Index}");
                            if (EntityManager.TryGetComponent<Game.Objects.Transform>(entity, out var transform))
                            {
                                var entityPosition = transform.m_Position;
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} position: {entityPosition}");

                                var rayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
                                var rayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z);

                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Calculating distance for entity {entity.Index}");
                                float distanceToEntity = CalculateRayToPointDistance(rayOrigin, rayDirection, entityPosition);
                                float selectionRadius = 5.0f;
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} distance: {distanceToEntity}, radius: {selectionRadius}");

                                if (distanceToEntity < selectionRadius && distanceToEntity < closestDistance)
                                {
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} is within selection radius");

                                    float rayDistance = math.distance(rayOrigin, entityPosition);
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} ray distance: {rayDistance}");

                                    if (rayDistance < 1000f)
                                    {
                                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} is closest candidate so far");
                                        closestDistance = distanceToEntity;
                                        selectedTollbooth = entity;
                                        break; // Found a tollbooth in the position of the mouse, no need to check further
                                    }
                                    else
                                    {
                                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} too far from ray ({rayDistance} > 1000)");
                                    }
                                }
                                else
                                {
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} outside selection radius or not closest");
                                }
                            }
                            else
                            {
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity {entity.Index} has no Transform component");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Exception processing entity {entity.Index}: {ex.Message}");
                        }
                    }

                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Entity iteration completed. Selected: {(selectedTollbooth != Entity.Null ? selectedTollbooth.Index.ToString() : "None")}");

                    if (selectedTollbooth != Entity.Null)
                    {
                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Actively selecting tollbooth INSTANCE entity {selectedTollbooth.Index}");

                        try
                        {
                            if (EntityManager.HasComponent<PrefabRef>(selectedTollbooth))
                            {
                                var prefabRef = EntityManager.GetComponentData<PrefabRef>(selectedTollbooth);
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Instance entity {selectedTollbooth.Index} references prefab {prefabRef.m_Prefab.Index}");
                            }
                            else
                            {
                                LogUtil.Warn($"TollboothSelectionSystem: HandleMouseSelection() - Selected entity {selectedTollbooth.Index} has no PrefabRef");
                            }

                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - Setting ToolSystem.selected to {selectedTollbooth.Index}");
                            m_ToolSystem.selected = selectedTollbooth;
                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseSelection() - ToolSystem.selected set successfully");
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Failed to set selected entity: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Click not on tollbooth, allowing vanilla selection");
                    }
                }
                finally
                {
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Disposing toll booth entities array");
                    tollBoothEntities.Dispose();
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Array disposed successfully");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - EXCEPTION in mouse selection handling: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: HandleMouseSelection() - Mouse selection handling completed");
        }

        private void MonitorSelectionChanges()
        {
            LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Starting selection monitoring");

            try
            {
                LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Getting current selected entity");
                Entity currentSelected = m_ToolSystem.selected;
                LogUtil.Info($"TollboothSelectionSystem: MonitorSelectionChanges() - Current selected: {(currentSelected != Entity.Null ? currentSelected.Index.ToString() : "None")}");
                LogUtil.Info($"TollboothSelectionSystem: MonitorSelectionChanges() - Last known selected: {(m_LastKnownSelected != Entity.Null ? m_LastKnownSelected.Index.ToString() : "None")}");

                if (currentSelected != m_LastKnownSelected)
                {
                    LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Selection has changed, updating");
                    m_LastKnownSelected = currentSelected;

                    if (currentSelected != Entity.Null)
                    {
                        LogUtil.Info($"TollboothSelectionSystem: MonitorSelectionChanges() - Checking if entity {currentSelected.Index} has TollBoothPrefabData");

                        try
                        {
                            if (EntityManager.HasComponent<TollBoothPrefabData>(currentSelected))
                            {
                                LogUtil.Info($"TollboothSelectionSystem: MonitorSelectionChanges() - Tollbooth {currentSelected.Index} is now selected (ToolSystem.selected = {currentSelected.Index})");
                            }
                            else
                            {
                                LogUtil.Info($"TollboothSelectionSystem: MonitorSelectionChanges() - Non-tollbooth entity {currentSelected.Index} selected");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: MonitorSelectionChanges() - Exception checking TollBoothPrefabData for entity {currentSelected.Index}: {ex.Message}");
                        }
                    }
                    else
                    {
                        LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Selection cleared");
                    }
                }
                else
                {
                    LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Selection unchanged");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: MonitorSelectionChanges() - EXCEPTION in selection monitoring: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: MonitorSelectionChanges() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: MonitorSelectionChanges() - Selection monitoring completed");
        }

        private void HandleMouseHover()
        {
            LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Starting mouse hover handling");

            try
            {
                LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Getting main camera");
                var camera = Camera.main;
                if (camera == null)
                {
                    LogUtil.Warn("TollboothSelectionSystem: HandleMouseHover() - Camera.main is null, clearing hover");
                    ClearHover();
                    return;
                }

                LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Getting mouse position and creating ray");
                var mousePosition = InputManager.instance.mousePosition;
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Mouse position: {mousePosition}");

                var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Ray created: Origin={ray.origin}, Direction={ray.direction}");

                LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Getting toll booth entities array");
                var tollBoothEntities = m_TollBoothQuery.ToEntityArray(Allocator.TempJob);
                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Found {tollBoothEntities.Length} toll booth entities");

                Entity hoveredEntity = Entity.Null;
                float closestDistance = float.MaxValue;

                try
                {
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Starting entity iteration for hover detection");
                    for (int i = 0; i < tollBoothEntities.Length; i++)
                    {
                        var entity = tollBoothEntities[i];
                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Processing entity {i + 1}/{tollBoothEntities.Length}: {entity.Index}");

                        try
                        {
                            LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Getting Transform for entity {entity.Index}");
                            if (EntityManager.TryGetComponent<Game.Objects.Transform>(entity, out var transform))
                            {
                                var entityPosition = transform.m_Position;
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} position: {entityPosition}");

                                var rayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
                                var rayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z);

                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Calculating distance for entity {entity.Index}");
                                float distanceToEntity = CalculateRayToPointDistance(rayOrigin, rayDirection, entityPosition);
                                float selectionRadius = 5.0f;
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} distance: {distanceToEntity}, radius: {selectionRadius}");

                                if (distanceToEntity < selectionRadius && distanceToEntity < closestDistance)
                                {
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} is within hover radius");

                                    float rayDistance = math.distance(rayOrigin, entityPosition);
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} ray distance: {rayDistance}");

                                    if (rayDistance < 1000f)
                                    {
                                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} is closest hover candidate");
                                        closestDistance = distanceToEntity;
                                        hoveredEntity = entity;
                                    }
                                    else
                                    {
                                        LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} too far from ray ({rayDistance} > 1000)");
                                    }
                                }
                                else
                                {
                                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} outside hover radius or not closest");
                                }
                            }
                            else
                            {
                                LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity {entity.Index} has no Transform component");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - Exception processing entity {entity.Index}: {ex.Message}");
                        }
                    }

                    LogUtil.Info($"TollboothSelectionSystem: HandleMouseHover() - Entity iteration completed. Hovered: {(hoveredEntity != Entity.Null ? hoveredEntity.Index.ToString() : "None")}");

                    LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Updating hover highlight");
                    UpdateHoverHighlight(hoveredEntity);
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Hover highlight updated");
                }
                finally
                {
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Disposing toll booth entities array");
                    tollBoothEntities.Dispose();
                    LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Array disposed successfully");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - EXCEPTION in mouse hover handling: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: HandleMouseHover() - Mouse hover handling completed");
        }

        private void UpdateHoverHighlight(Entity newHoveredEntity)
        {
            LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - New hovered: {(newHoveredEntity != Entity.Null ? newHoveredEntity.Index.ToString() : "None")}, Current: {(m_HoveredEntity != Entity.Null ? m_HoveredEntity.Index.ToString() : "None")}");

            try
            {
                if (newHoveredEntity != m_HoveredEntity)
                {
                    LogUtil.Info("TollboothSelectionSystem: UpdateHoverHighlight() - Hovered entity changed, updating highlights");

                    if (m_HoveredEntity != Entity.Null)
                    {
                        LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Removing highlight from previous entity {m_HoveredEntity.Index}");
                        RemoveHighlight(m_HoveredEntity);
                        LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Highlight removed from entity {m_HoveredEntity.Index}");
                    }

                    if (newHoveredEntity != Entity.Null)
                    {
                        LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Checking if new hovered entity {newHoveredEntity.Index} exists");
                        if (EntityManager.Exists(newHoveredEntity))
                        {
                            LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Adding highlight to new entity {newHoveredEntity.Index}");
                            AddHighlight(newHoveredEntity);
                            LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Highlight added to entity {newHoveredEntity.Index}");
                        }
                        else
                        {
                            LogUtil.Warn($"TollboothSelectionSystem: UpdateHoverHighlight() - New hovered entity {newHoveredEntity.Index} does not exist");
                            newHoveredEntity = Entity.Null;
                        }
                    }

                    Entity previousHovered = m_HoveredEntity;
                    m_HoveredEntity = newHoveredEntity;
                    LogUtil.Info($"TollboothSelectionSystem: UpdateHoverHighlight() - Hovered entity updated from {(previousHovered != Entity.Null ? previousHovered.Index.ToString() : "None")} to {(m_HoveredEntity != Entity.Null ? m_HoveredEntity.Index.ToString() : "None")}");

                    LogUtil.Info("TollboothSelectionSystem: UpdateHoverHighlight() - Invoking HoveredEntityChanged event");
                    try
                    {
                        HoveredEntityChanged?.Invoke(m_HoveredEntity);
                        LogUtil.Info("TollboothSelectionSystem: UpdateHoverHighlight() - HoveredEntityChanged event invoked successfully");
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - Exception invoking HoveredEntityChanged event: {ex.Message}");
                    }
                }
                else
                {
                    LogUtil.Info("TollboothSelectionSystem: UpdateHoverHighlight() - Hovered entity unchanged, no action needed");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - EXCEPTION updating hover highlight: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: UpdateHoverHighlight() - Hover highlight update completed");
        }

        private void ClearHover()
        {
            LogUtil.Info($"TollboothSelectionSystem: ClearHover() - Clearing hover for entity: {(m_HoveredEntity != Entity.Null ? m_HoveredEntity.Index.ToString() : "None")}");

            try
            {
                if (m_HoveredEntity != Entity.Null)
                {
                    LogUtil.Info($"TollboothSelectionSystem: ClearHover() - Removing highlight from entity {m_HoveredEntity.Index}");
                    RemoveHighlight(m_HoveredEntity);
                    LogUtil.Info($"TollboothSelectionSystem: ClearHover() - Highlight removed from entity {m_HoveredEntity.Index}");

                    m_HoveredEntity = Entity.Null;
                    LogUtil.Info("TollboothSelectionSystem: ClearHover() - Hovered entity set to null");
                }
                else
                {
                    LogUtil.Info("TollboothSelectionSystem: ClearHover() - No hovered entity to clear");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: ClearHover() - EXCEPTION clearing hover: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: ClearHover() - Stack trace: {ex.StackTrace}");
            }

            LogUtil.Info("TollboothSelectionSystem: ClearHover() - Hover clearing completed");
        }

        private float CalculateRayToPointDistance(float3 rayOrigin, float3 rayDirection, float3 point)
        {
            LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - Ray origin: {rayOrigin}, Direction: {rayDirection}, Point: {point}");

            try
            {
                var normalizedDirection = math.normalize(rayDirection);
                LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - Normalized direction: {normalizedDirection}");

                var toPoint = point - rayOrigin;
                LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - To point vector: {toPoint}");

                float projectionLength = math.dot(toPoint, normalizedDirection);
                LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - Projection length: {projectionLength}");

                var closestPointOnRay = rayOrigin + normalizedDirection * math.max(0, projectionLength);
                LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - Closest point on ray: {closestPointOnRay}");

                float distance = math.distance(point, closestPointOnRay);
                LogUtil.Info($"TollboothSelectionSystem: CalculateRayToPointDistance() - Calculated distance: {distance}");

                return distance;
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: CalculateRayToPointDistance() - EXCEPTION calculating distance: {ex.Message}");
                return float.MaxValue;
            }
        }

        private void AddHighlight(Entity entity)
        {
            LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Adding highlight to entity {entity.Index}");

            try
            {
                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Checking if entity {entity.Index} exists");
                if (!EntityManager.Exists(entity))
                {
                    LogUtil.Warn($"TollboothSelectionSystem: AddHighlight() - Entity {entity.Index} does not exist, cannot add highlight");
                    return;
                }

                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Checking if entity {entity.Index} already has Highlighted component");
                if (EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Entity {entity.Index} already has Highlighted component");
                    return;
                }

                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Adding Highlighted component to entity {entity.Index}");
                EntityManager.AddComponent<Game.Tools.Highlighted>(entity);
                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Highlighted component added to entity {entity.Index}");

                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Checking for CullingInfo component on entity {entity.Index}");
                if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                {
                    LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Adding BatchesUpdated component to entity {entity.Index}");
                    EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                    LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - BatchesUpdated component added to entity {entity.Index}");
                }
                else
                {
                    LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Entity {entity.Index} has no CullingInfo component, skipping BatchesUpdated");
                }

                LogUtil.Info($"TollboothSelectionSystem: AddHighlight() - Successfully added highlight to entity {entity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: AddHighlight() - EXCEPTION adding highlight to entity {entity.Index}: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: AddHighlight() - Stack trace: {ex.StackTrace}");
            }
        }

        private void RemoveHighlight(Entity entity)
        {
            LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Removing highlight from entity {entity.Index}");

            try
            {
                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Checking if entity {entity.Index} exists");
                if (!EntityManager.Exists(entity))
                {
                    LogUtil.Warn($"TollboothSelectionSystem: RemoveHighlight() - Entity {entity.Index} does not exist, cannot remove highlight");
                    return;
                }

                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Checking if entity {entity.Index} has Highlighted component");
                if (!EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Entity {entity.Index} does not have Highlighted component");
                    return;
                }

                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Removing Highlighted component from entity {entity.Index}");
                EntityManager.RemoveComponent<Game.Tools.Highlighted>(entity);
                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Highlighted component removed from entity {entity.Index}");

                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Checking for CullingInfo component on entity {entity.Index}");
                if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                {
                    LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Adding BatchesUpdated component to entity {entity.Index}");
                    EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                    LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - BatchesUpdated component added to entity {entity.Index}");
                }
                else
                {
                    LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Entity {entity.Index} has no CullingInfo component, skipping BatchesUpdated");
                }

                LogUtil.Info($"TollboothSelectionSystem: RemoveHighlight() - Successfully removed highlight from entity {entity.Index}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: RemoveHighlight() - EXCEPTION removing highlight from entity {entity.Index}: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: RemoveHighlight() - Stack trace: {ex.StackTrace}");
            }
        }

        protected override void OnDestroy()
        {
            LogUtil.Info("TollboothSelectionSystem: OnDestroy() - Starting system destruction");

            try
            {
                LogUtil.Info("TollboothSelectionSystem: OnDestroy() - Clearing hover state");
                ClearHover();
                LogUtil.Info("TollboothSelectionSystem: OnDestroy() - Hover state cleared");

                LogUtil.Info("TollboothSelectionSystem: OnDestroy() - Calling base.OnDestroy()");
                base.OnDestroy();
                LogUtil.Info("TollboothSelectionSystem: OnDestroy() - Base destruction completed");

                LogUtil.Info("TollboothSelectionSystem: OnDestroy() - System destruction completed successfully");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: OnDestroy() - EXCEPTION during system destruction: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: OnDestroy() - Stack trace: {ex.StackTrace}");
            }
        }
    }
}