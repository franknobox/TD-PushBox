using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public enum VoxelGroundKind
    {
        [InspectorName("普通地面")] Ground, [InspectorName("石地")] Stone,
        [InspectorName("墙体")] Wall, [InspectorName("深渊")] Void, [InspectorName("土地")] Dirt
    }

    [CreateAssetMenu(menuName = "TD推箱子/地块配置")]
    public sealed class VoxelTileDefinition : ScriptableObject
    {
        [InspectorName("地块类型")] public VoxelGroundKind kind;
        [InspectorName("可通行")] public bool walkable = true;
        [InspectorName("阻挡视线")] public bool blocksSight;
        [InspectorName("生成底座")] public bool generateSupport=true;
        [Min(1), InspectorName("移动力消耗")] public int movementCost = 1;
    }
}
