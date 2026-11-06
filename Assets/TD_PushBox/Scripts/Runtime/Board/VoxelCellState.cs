using System;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public enum VoxelSlopeDirection
    {
        [InspectorName("无斜坡")] None,[InspectorName("北向升高")] North,[InspectorName("东向升高")] East,
        [InspectorName("南向升高")] South,[InspectorName("西向升高")] West
    }
    [Serializable]
    public sealed class VoxelCellState
    {
        public Vector2Int position;
        public int height;
        public VoxelSlopeDirection slope;
        public bool walkable,blocksSight;
        public int movementCost=1;
        public VoxelGroundKind kind;
        public string appearanceId;
        public Vector2Int Uphill=>slope switch
        {
            VoxelSlopeDirection.North=>Vector2Int.up,VoxelSlopeDirection.East=>Vector2Int.right,
            VoxelSlopeDirection.South=>Vector2Int.down,VoxelSlopeDirection.West=>Vector2Int.left,_=>Vector2Int.zero
        };
        public long CenterHeightTwice=>2L*height+(slope==VoxelSlopeDirection.None?0:1);
        public long EdgeHeightTwice(Vector2Int edge)=>CenterHeightTwice+edge.x*Uphill.x+edge.y*Uphill.y;
        public VoxelCellState Copy()=>(VoxelCellState)MemberwiseClone();
    }
}
