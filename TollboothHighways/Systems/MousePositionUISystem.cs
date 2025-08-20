using Colossal.UI.Binding;
using Game.Input;
using Game.UI;
using Unity.Mathematics;
using UnityEngine;

namespace TollboothHighways.Systems
{
    public partial class MousePositionUISystem : UISystemBase
    {
        private ValueBinding<bool> m_ShowMousePanel;
        private ValueBinding<float2> m_ScreenPosition;
        private ValueBinding<float3> m_WorldPosition;
        private ValueBinding<string> m_FormattedPosition;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Create UI bindings
            m_ShowMousePanel = new ValueBinding<bool>("mousePosition", "showPanel", true);
            m_ScreenPosition = new ValueBinding<float2>("mousePosition", "screenPosition", float2.zero);
            m_WorldPosition = new ValueBinding<float3>("mousePosition", "worldPosition", float3.zero);
            m_FormattedPosition = new ValueBinding<string>("mousePosition", "formattedPosition", "");

            // Add UI bindings
            AddBinding(m_ShowMousePanel);
            AddBinding(m_ScreenPosition);
            AddBinding(m_WorldPosition);
            AddBinding(m_FormattedPosition);
        }

        protected override void OnUpdate()
        {
            // Get current mouse position
            var mousePosition = InputManager.instance.mousePosition;
            var screenPos = new float2(mousePosition.x, mousePosition.y);
            
            // Update screen position
            m_ScreenPosition.Update(screenPos);

            // Try to get world position from screen position
            var camera = Camera.main;
            if (camera != null)
            {
                // Create a ray from camera through mouse position
                var ray = camera.ScreenPointToRay(new Vector3(mousePosition.x, mousePosition.y, 0));
                
                // Cast ray to ground plane (assuming Y=0 is ground level)
                // You can adjust this based on your game's coordinate system
                float t = -ray.origin.y / ray.direction.y;
                if (t > 0)
                {
                    var worldPoint = ray.origin + ray.direction * t;
                    var worldPos = new float3(worldPoint.x, worldPoint.y, worldPoint.z);
                    
                    m_WorldPosition.Update(worldPos);
                    
                    // Format position string
                    var formattedPos = $"Screen: ({screenPos.x:F0}, {screenPos.y:F0}) | World: ({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})";
                    m_FormattedPosition.Update(formattedPos);
                }
                else
                {
                    // Fallback when ray doesn't hit ground
                    var formattedPos = $"Screen: ({screenPos.x:F0}, {screenPos.y:F0}) | World: N/A";
                    m_FormattedPosition.Update(formattedPos);
                }
            }
        }

        public void TogglePanel()
        {
            m_ShowMousePanel.Update(!m_ShowMousePanel.value);
        }

        // Add this method to handle the keybinding
        public void HandleToggleKeybinding()
        {
            TogglePanel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}