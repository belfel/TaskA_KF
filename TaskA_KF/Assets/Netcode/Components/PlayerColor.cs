using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_Color")]
public struct PlayerColor : IComponentData
{
    public float4 Value;
}
