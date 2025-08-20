using Colossal.Entities;
using Game.Input;
using Game.Tools;
using Game.UI;
using Game.UI.Tooltip;
using Game.UI.Widgets;
using System.Linq;
using TollboothHighways.Domain.Components;
using TollboothHighways.Utilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TollboothHighways.Systems
{
    public partial class TollBoothTooltipUISystem : TooltipSystemBase
    {
        private TooltipSystemBase m_TooltipSystem;
        private ToolSystem m_ToolSystem;
        private DefaultToolSystem m_DefaultTool;
        private ToolRaycastSystem m_ToolRaycastSystem;

        private TollboothSelectionSystem m_SelectionSystem;
        private TooltipGroup m_TollBoothTooltipGroup;
        private TooltipUISystem m_TooltipUISystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_DefaultTool = World.GetOrCreateSystemManaged<DefaultToolSystem>();
            m_ToolRaycastSystem = World.GetOrCreateSystemManaged<ToolRaycastSystem>();
            m_TooltipUISystem = World.GetOrCreateSystemManaged<TooltipUISystem>();

            m_TooltipSystem = World.GetOrCreateSystemManaged<TooltipSystemBase>();
            m_SelectionSystem = World.GetOrCreateSystemManaged<TollboothSelectionSystem>();

            m_TollBoothTooltipGroup = new TooltipGroup()
            {
                path = "tollBoothsTooltips",
                position = default,
                horizontalAlignment = TooltipGroup.Alignment.Start,
                verticalAlignment = TooltipGroup.Alignment.Start
            };

            LogUtil.Info("TollBoothTooltipUISystem: Initialized using vanilla TooltipGroup.");
        }

        protected override void OnUpdate()
        {
            if (m_SelectionSystem == null) return;

            Entity hoveredEntity = m_SelectionSystem.GetCurrentHoveredEntity();

            m_TollBoothTooltipGroup.children.Clear();
            m_TollBoothTooltipGroup.path = PathSegment.Empty;

            if (hoveredEntity != Entity.Null && EntityManager.HasComponent<TollBoothPrefabData>(hoveredEntity))
            {
                AddTollBoothTooltip(hoveredEntity);
            }
        }

        private void AddTollBoothTooltip(Entity entity)
        {
            if (!EntityManager.TryGetComponent<TollBoothPrefabData>(entity, out var tollBoothData))
            {
                return;
            }

            // Also set the entity's custom name through the NameSystem
            var nameSystem = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            nameSystem.TryGetCustomName(entity, out string tollBoothNameSystem);

            var mousePosition = InputManager.instance.mousePosition;

            // Calculate positioning similar to vanilla tooltips
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;
            var tooltipWidth = 200f;
            var tooltipHeight = 60f;

            float offsetX = 15f;
            float offsetY = -10f;

            // Adjust position if tooltip would go off-screen
            if (mousePosition.x + tooltipWidth + offsetX > screenWidth)
            {
                offsetX = -tooltipWidth - 15f;
            }

            if (mousePosition.y - tooltipHeight + offsetY < 0)
            {
                offsetY = tooltipHeight + 15f;
            }

            var tooltipPosition = new Vector2(
                mousePosition.x + offsetX,
                screenHeight - mousePosition.y + offsetY
            );

            var tooltipPath = $"tollBoothsTooltips_{tollBoothNameSystem}";
            
            // Check if tooltip group already exists
            var existingGroup = m_TooltipUISystem.groups.FirstOrDefault(g => g.path == tooltipPath);
            
            if (existingGroup != null)
            {
                // Update existing group
                existingGroup.children.Clear();
                existingGroup.position = tooltipPosition;
                
                StringTooltip nameTollbooth = new()
                {
                    icon = "coui://javapower-tollbooth-highways/Tollbooth.png",
                    value = $"{tollBoothNameSystem}",
                };            
                existingGroup.children.Add(nameTollbooth);

                StringTooltip panelViewTollbooth = new()
                {
                    icon = "Media/Mouse/LMB.svg",
                    value = "Info Panel",
                };
                existingGroup.children.Add(panelViewTollbooth);
                
                LogUtil.Info($"TollBoothTooltipUISystem: Updating existing tooltip group {tooltipPath} at position {tooltipPosition}");
            }
            else
            {
                // Create new tooltip group
                var newTooltipGroup = new TooltipGroup()
                {
                    path = tooltipPath,
                    position = tooltipPosition,
                    horizontalAlignment = TooltipGroup.Alignment.Start,
                    verticalAlignment = TooltipGroup.Alignment.Start
                };

                StringTooltip nameTollbooth = new()
                {
                    icon = "coui://javapower-tollbooth-highways/Tollbooth.png",
                    value = $"{tollBoothNameSystem}",
                };            
                newTooltipGroup.children.Add(nameTollbooth);

                StringTooltip panelViewTollbooth = new()
                {
                    icon = "Media/Mouse/LMB.svg",
                    value = "Info Panel",
                };
                newTooltipGroup.children.Add(panelViewTollbooth);

                AddGroup(newTooltipGroup);
                LogUtil.Info($"TollBoothTooltipUISystem: Adding new tooltip group {tooltipPath} at position {tooltipPosition}");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}