using Unity.Entities;
using UnityEngine;
using Unity.Rendering;

public struct Player : IComponentData
{
    
}

public class PlayerAuthoring : MonoBehaviour
{
    class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Player component = default;
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, component);
            AddComponent(entity, new URPMaterialPropertyBaseColor());
        }
    }
}
