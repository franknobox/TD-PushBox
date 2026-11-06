using UnityEngine;

namespace JobTest.TacticsVoxel
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class VoxelTacticsTile : MonoBehaviour
    {
        [SerializeField, InspectorName("地块配置")] private VoxelTileDefinition definition;
        [SerializeField, InspectorName("网格坐标")] private Vector2Int gridPosition;
        [SerializeField, InspectorName("高度层")] private int heightLevel;
        [SerializeField, InspectorName("斜坡升高方向")] private VoxelSlopeDirection slope;
        [SerializeField, InspectorName("可通行")] private bool walkable = true;
        [SerializeField, InspectorName("地表偏移")] private Vector3 surfaceOffset;
        private VoxelTacticsLevel level;
        public Vector2Int GridPosition => gridPosition;
        public int HeightLevel => heightLevel;
        public VoxelTileDefinition Definition => definition;
        public VoxelSlopeDirection Slope => slope;
        public bool Walkable => definition != null ? definition.walkable : walkable;
        public Vector3 SurfacePosition => Level != null
            ? Level.Surface(gridPosition, heightLevel) + Level.transform.TransformVector(Vector3.up * SlopeCenterOffset)
            : transform.TransformPoint(surfaceOffset + Vector3.up * SlopeCenterOffset);
        private float SlopeCenterOffset => slope == VoxelSlopeDirection.None ? 0 : (Level != null ? Level.HeightStep : VoxelTacticsLevel.DefaultHeightStep) * 0.5f;
        private VoxelTacticsLevel Level => level != null ? level : level = GetComponentInParent<VoxelTacticsLevel>();
        public VoxelCellState CreateState() => new()
        {
            position = gridPosition, height = heightLevel, slope = slope, walkable = Walkable,
            blocksSight = definition != null && definition.blocksSight,
            movementCost = definition != null ? definition.movementCost : 1,
            kind = definition != null ? definition.kind : VoxelGroundKind.Ground
        };
        public void ConfigureDefinition(VoxelTileDefinition data, Vector2Int position, int height)
        { definition = data; gridPosition = position; heightLevel = height; surfaceOffset = Vector3.zero; }
        public void ConfigureSlope(VoxelSlopeDirection direction) => slope = direction;
    }
}
