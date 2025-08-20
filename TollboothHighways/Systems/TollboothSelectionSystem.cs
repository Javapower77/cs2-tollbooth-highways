using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.UI.Binding;
using Domain.Components;
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
            return m_HoveredEntity;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultToolSystem = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_TollBoothQuery = GetEntityQuery(ComponentType.ReadOnly<Domain.Components.TollBoothPrefabData>(), ComponentType.ReadOnly<PrefabRef>());

            RequireForUpdate(m_TollBoothQuery);

            LogUtil.Info("TollboothSelectionSystem: Created with active selection handling");
        }

        protected override void OnUpdate()
        {
            // Handle hover highlighting
            HandleMouseHover();
            
            // ACTIVE SELECTION HANDLING - This is the key fix
            HandleMouseSelection();
            
            // Monitor selection changes from vanilla system
            MonitorSelectionChanges();
        }

        private void HandleMouseSelection()
        {
            // Only handle selection when using default tool and left mouse button is clicked
            if (m_ToolSystem.activeTool != m_DefaultToolSystem)
                return;

            if (UnityEngine.InputSystem.Mouse.current == null || 
                !UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var mousePosition = InputManager.instance.mousePosition;
            var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));

            // Create a separate query for instance entities that have both TollBoothPrefabData and Transform
            // These are the actual placed tollbooth instances in the world
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
                foreach (var entity in tollBoothEntities)
                {
                    // Verify this is an instance entity, not a prefab
                    if (!EntityManager.HasComponent<PrefabRef>(entity))
                        continue;

                    var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                    
                    // Check if the prefab reference has TollBoothPrefabData
                    if (!EntityManager.HasComponent<TollBoothPrefabData>(prefabRef.m_Prefab))
                        continue;

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
                                selectedTollbooth = entity; // This is now the instance entity
                                break; // Found a tollbooth in the position of the mouse, no need to check further
                            }
                        }
                    }
                }

                // CRITICAL: Actively set the selection if we found a tollbooth instance
                if (selectedTollbooth != Entity.Null)
                {
                    LogUtil.Info($"TollboothSelectionSystem: Actively selecting tollbooth INSTANCE entity {selectedTollbooth.Index}");
                    
                    // Verify we're selecting the instance, not the prefab
                    if (EntityManager.HasComponent<PrefabRef>(selectedTollbooth))
                    {
                        var prefabRef = EntityManager.GetComponentData<PrefabRef>(selectedTollbooth);
                        LogUtil.Info($"TollboothSelectionSystem: Instance entity {selectedTollbooth.Index} references prefab {prefabRef.m_Prefab.Index}");
                    }
                    
                    m_ToolSystem.selected = selectedTollbooth;
                }
                else
                {
                    // Only clear selection if we didn't click on a tollbooth
                    // Let vanilla handle other entity selections
                    LogUtil.Info("TollboothSelectionSystem: Click not on tollbooth, allowing vanilla selection");
                }
            }
            finally
            {
                tollBoothEntities.Dispose();
            }
        }

        private void MonitorSelectionChanges()
        {
            Entity currentSelected = m_ToolSystem.selected;
            
            if (currentSelected != m_LastKnownSelected)
            {
                m_LastKnownSelected = currentSelected;
                
                if (currentSelected != Entity.Null && EntityManager.HasComponent<TollBoothPrefabData>(currentSelected))
                {
                    LogUtil.Info($"TollboothSelectionSystem: Tollbooth {currentSelected.Index} is now selected (ToolSystem.selected = {currentSelected.Index})");
                }
                else if (currentSelected != Entity.Null)
                {
                    LogUtil.Info($"TollboothSelectionSystem: Non-tollbooth entity {currentSelected.Index} selected");
                }
                else
                {
                    LogUtil.Info("TollboothSelectionSystem: Selection cleared");
                }
            }
        }

        private void HandleMouseHover()
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
                foreach (var entity in tollBoothEntities)
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
                        }
                    }
                }

                UpdateHoverHighlight(hoveredEntity);
            }
            finally
            {
                tollBoothEntities.Dispose();
            }
        }

        private void UpdateHoverHighlight(Entity newHoveredEntity)
        {
            if (newHoveredEntity != m_HoveredEntity)
            {
                if (m_HoveredEntity != Entity.Null)
                {
                    RemoveHighlight(m_HoveredEntity);
                }

                if (newHoveredEntity != Entity.Null && EntityManager.Exists(newHoveredEntity))
                {
                    AddHighlight(newHoveredEntity);
                }

                m_HoveredEntity = newHoveredEntity;
                HoveredEntityChanged?.Invoke(m_HoveredEntity);
            }
        }

        private void ClearHover()
        {
            if (m_HoveredEntity != Entity.Null)
            {
                RemoveHighlight(m_HoveredEntity);
                m_HoveredEntity = Entity.Null;
            }
        }

        private float CalculateRayToPointDistance(float3 rayOrigin, float3 rayDirection, float3 point)
        {
            var normalizedDirection = math.normalize(rayDirection);
            var toPoint = point - rayOrigin;
            float projectionLength = math.dot(toPoint, normalizedDirection);
            var closestPointOnRay = rayOrigin + normalizedDirection * math.max(0, projectionLength);
            return math.distance(point, closestPointOnRay);
        }

        private void AddHighlight(Entity entity)
        {
            try
            {
                if (EntityManager.Exists(entity) && !EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    EntityManager.AddComponent<Game.Tools.Highlighted>(entity);
                    if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                    {
                        EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                    }
                    LogUtil.Info($"Added highlight to tollbooth entity {entity.Index}");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Warn($"Error adding highlight to entity {entity.Index}: {ex.Message}");
            }
        }

        private void RemoveHighlight(Entity entity)
        {
            try
            {
                if (EntityManager.Exists(entity) && EntityManager.HasComponent<Game.Tools.Highlighted>(entity))
                {
                    EntityManager.RemoveComponent<Game.Tools.Highlighted>(entity);
                    if (EntityManager.HasComponent<Game.Rendering.CullingInfo>(entity))
                    {
                        EntityManager.AddComponent<Game.Common.BatchesUpdated>(entity);
                    }
                    LogUtil.Info($"Removed highlight from tollbooth entity {entity.Index}");
                }
            }
            catch (System.Exception ex)
            {
                LogUtil.Warn($"Error removing highlight from entity {entity.Index}: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            ClearHover();
            base.OnDestroy();
        }
    }
}
