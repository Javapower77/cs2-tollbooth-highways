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
            try
            {
                HandleMouseHover();
                HandleMouseSelection();
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: OnUpdate() - CRITICAL ERROR in update cycle: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: OnUpdate() - Stack trace: {ex.StackTrace}");
            }
        }

        private void HandleMouseSelection()
        {
            try
            {
                if (m_ToolSystem.activeTool != m_DefaultToolSystem)
                {
                    return;
                }

                if (UnityEngine.InputSystem.Mouse.current == null)
                {
                    return;
                }

                if (!UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                {
                    return;
                }

                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                var mousePosition = InputManager.instance.mousePosition;
                var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));
                var instanceQuery = GetEntityQuery(
                    ComponentType.ReadOnly<TollBoothPrefabData>(),
                    ComponentType.ReadOnly<Game.Objects.Transform>(),
                    ComponentType.Exclude<Deleted>()
                );
                var tollBoothEntities = instanceQuery.ToEntityArray(Allocator.TempJob);

                Entity selectedTollbooth = Entity.Null;
                float closestDistance = float.MaxValue;

                try
                {
                    for (int i = 0; i < tollBoothEntities.Length; i++)
                    {
                        var entity = tollBoothEntities[i];
                        try
                        {
                            if (!EntityManager.HasComponent<PrefabRef>(entity))
                            {
                                continue;
                            }

                            var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);

                            if (!EntityManager.HasComponent<TollBoothPrefabData>(prefabRef.m_Prefab))
                            {
                                continue;
                            }

                            if (EntityManager.TryGetComponent<Game.Objects.Transform>(entity, out var transform))
                            {
                                var entityPosition = transform.m_Position;
                                var rayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
                                var rayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z);
                                float distanceToEntity = CalculateRayToPointDistance(rayOrigin, rayDirection, entityPosition);
                                float selectionRadius = 5.0f;

                                if (distanceToEntity < selectionRadius && distanceToEntity < closestDistance)
                                {
                                    float rayDistance = math.distance(rayOrigin, entityPosition);
                                    if (rayDistance < 1000f)
                                    {
                                        closestDistance = distanceToEntity;
                                        selectedTollbooth = entity;
                                        break; // Found a tollbooth in the position of the mouse, no need to check further
                                    }
                                    else
                                    {
                                    }
                                }
                                else
                                {
                                }
                            }
                            else
                            {
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Exception processing entity {entity.Index}: {ex.Message}");
                        }
                    }

                    if (selectedTollbooth != Entity.Null)
                    {
                        try
                        {
                            if (EntityManager.HasComponent<PrefabRef>(selectedTollbooth))
                            {
                                var prefabRef = EntityManager.GetComponentData<PrefabRef>(selectedTollbooth);
                            }
                            else
                            {
                            }

                            m_ToolSystem.selected = selectedTollbooth;
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Failed to set selected entity: {ex.Message}");
                        }
                    }
                    else
                    {
                    }
                }
                finally
                {
                    tollBoothEntities.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - EXCEPTION in mouse selection handling: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseSelection() - Stack trace: {ex.StackTrace}");
            }
        }

        private void HandleMouseHover()
        {
            try
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    ClearHover();
                    return;
                }

                var mousePosition = InputManager.instance.mousePosition;
                var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));
                var tollBoothEntities = m_TollBoothQuery.ToEntityArray(Allocator.TempJob);
                Entity hoveredEntity = Entity.Null;
                float closestDistance = float.MaxValue;

                try
                {
                    for (int i = 0; i < tollBoothEntities.Length; i++)
                    {
                        var entity = tollBoothEntities[i];
                        try
                        {
                            if (EntityManager.TryGetComponent<Game.Objects.Transform>(entity, out var transform))
                            {
                                var entityPosition = transform.m_Position;
                                var rayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
                                var rayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z);
                                float distanceToEntity = CalculateRayToPointDistance(rayOrigin, rayDirection, entityPosition);
                                float selectionRadius = 5.0f;

                                if (distanceToEntity < selectionRadius && distanceToEntity < closestDistance)
                                {
                                    float rayDistance = math.distance(rayOrigin, entityPosition);
                                    if (rayDistance < 1000f)
                                    {
                                        closestDistance = distanceToEntity;
                                        hoveredEntity = entity;
                                    }
                                    else
                                    {
                                    }
                                }
                                else
                                {
                                }
                            }
                            else
                            {
                            }
                        }
                        catch (System.Exception ex)
                        {
                            LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - Exception processing entity {entity.Index}: {ex.Message}");
                        }
                    }
                    UpdateHoverHighlight(hoveredEntity);
                }
                finally
                {
                    tollBoothEntities.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - EXCEPTION in mouse hover handling: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: HandleMouseHover() - Stack trace: {ex.StackTrace}");
            }
        }

        private void UpdateHoverHighlight(Entity newHoveredEntity)
        {
            try
            {
                if (newHoveredEntity != m_HoveredEntity)
                {
                    if (m_HoveredEntity != Entity.Null)
                    {
                        RemoveHighlight(m_HoveredEntity);
                    }

                    if (newHoveredEntity != Entity.Null)
                    {
                        if (EntityManager.Exists(newHoveredEntity))
                        {
                            AddHighlight(newHoveredEntity);
                        }
                        else
                        {
                            newHoveredEntity = Entity.Null;
                        }
                    }

                    Entity previousHovered = m_HoveredEntity;
                    m_HoveredEntity = newHoveredEntity;

                    try
                    {
                        HoveredEntityChanged?.Invoke(m_HoveredEntity);
                    }
                    catch (System.Exception ex)
                    {
                        LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - Exception invoking HoveredEntityChanged event: {ex.Message}");
                    }
                }
                else
                {
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - EXCEPTION updating hover highlight: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: UpdateHoverHighlight() - Stack trace: {ex.StackTrace}");
            }

        }

        private void ClearHover()
        {
            try
            {
                if (m_HoveredEntity != Entity.Null)
                {
                    RemoveHighlight(m_HoveredEntity);
                    m_HoveredEntity = Entity.Null;
                }
                else
                {
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: ClearHover() - EXCEPTION clearing hover: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: ClearHover() - Stack trace: {ex.StackTrace}");
            }
        }

        private float CalculateRayToPointDistance(float3 rayOrigin, float3 rayDirection, float3 point)
        {
            try
            {
                var normalizedDirection = math.normalize(rayDirection);
                var toPoint = point - rayOrigin;
                float projectionLength = math.dot(toPoint, normalizedDirection);
                var closestPointOnRay = rayOrigin + normalizedDirection * math.max(0, projectionLength);
                float distance = math.distance(point, closestPointOnRay);
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
            try
            {
                if (!EntityManager.Exists(entity))
                {
                    return;
                }

                if (EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    return;
                }

                EntityManager.AddComponent<Game.Tools.Highlighted>(entity);
                if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                {
                    EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                }
                else
                {
                }

            }
            catch (System.Exception ex)
            {
                LogUtil.Error($"TollboothSelectionSystem: AddHighlight() - EXCEPTION adding highlight to entity {entity.Index}: {ex.Message}");
                LogUtil.Error($"TollboothSelectionSystem: AddHighlight() - Stack trace: {ex.StackTrace}");
            }
        }

        private void RemoveHighlight(Entity entity)
        {
            try
            {
                if (!EntityManager.Exists(entity))
                {
                    return;
                }

                if (!EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    return;
                }

                EntityManager.RemoveComponent<Game.Tools.Highlighted>(entity);
                if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                {
                    EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                }
                else
                {
                }
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