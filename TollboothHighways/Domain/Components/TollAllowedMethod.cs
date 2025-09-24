using Game.Pathfind;
using Unity.Entities;

namespace TollboothHighways.Domain.Components
{
    public struct TollAllowedMethod : IComponentData
    {
        public PathMethod Value;   // Bit mask (your custom TollPathMethods.*)
    }

    public struct TollPatchedLane : IComponentData {}
}