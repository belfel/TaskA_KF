using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct PlayerInput : IInputComponentData
{
    public int Horizontal;
    public int Vertical;
}

[DisallowMultipleComponent]
public class PlayerInputAuthoring : MonoBehaviour
{
    class CubeInputBaking : Unity.Entities.Baker<PlayerInputAuthoring>
    {
        public override void Bake(PlayerInputAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerInput>(entity);
        }
    }
}

[UpdateInGroup(typeof(GhostInputSystemGroup))]
public partial struct SampleCubeInput : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        state.RequireForUpdate<PlayerSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var playerInput in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
        {
            playerInput.ValueRW = default;
            if (Input.GetKey("left"))
                playerInput.ValueRW.Horizontal -= 1;
            if (Input.GetKey("right"))
                playerInput.ValueRW.Horizontal += 1;
            if (Input.GetKey("down"))
                playerInput.ValueRW.Vertical -= 1;
            if (Input.GetKey("up"))
                playerInput.ValueRW.Vertical += 1;
        }
    }
}