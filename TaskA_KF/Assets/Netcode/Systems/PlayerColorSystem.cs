using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using Unity.NetCode;

[BurstCompile]
public partial struct PlayerColorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        //state.Enabled = false;

        var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (baseColor, ghostOwner) in SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, RefRO<GhostOwner>>())
        {
            if (ghostOwner.ValueRO.NetworkId == 1)
                baseColor.ValueRW.Value = new float4(1f, 0f, 0f, 1f);
            else baseColor.ValueRW.Value = new float4(0f, 0f, 1f, 1f);
        }
        commandBuffer.Playback(state.EntityManager);
    }
}