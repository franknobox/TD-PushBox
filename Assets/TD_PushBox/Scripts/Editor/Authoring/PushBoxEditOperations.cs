using System.Linq;
using TD.PushBox;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class PushBoxEditOperations
    {
        public static bool IsEntity(GameObject go)=>go!=null&&(go.GetComponent<PushBoxActor>()!=null||go.GetComponent<PushBoxObject>()!=null);
        public static Vector2Int Position(GameObject go)=>go.GetComponent<PushBoxActor>() is PushBoxActor actor?actor.GridPosition:go.GetComponent<PushBoxObject>().GridPosition;
        public static bool Occupied(VoxelTacticsLevel level,Vector2Int p,GameObject ignore=null)=>
            level.GetComponentsInChildren<PushBoxActor>(true).Any(actor=>actor.gameObject.activeInHierarchy&&actor.gameObject!=ignore&&actor.GridPosition==p)
            ||level.GetComponentsInChildren<PushBoxObject>(true).Any(item=>item.gameObject.activeInHierarchy&&item.gameObject!=ignore&&item.Covers(p));

        public static string CheckPlacement(VoxelLevelEditOperations edit,GameObject prefab,Vector2Int p,GameObject ignore,bool decoration)
        {
            var actor=prefab.GetComponent<PushBoxActor>();var item=prefab.GetComponent<PushBoxObject>();
            if(actor==null&&item==null) return decoration?null:"当前资源缺少推箱子角色或物件组件。";
            var footprint=item!=null?item.Footprint:Vector2Int.one;
            var actors=edit.level.GetComponentsInChildren<PushBoxActor>(true).Where(other=>other.gameObject.activeInHierarchy&&other.gameObject!=ignore).ToArray();
            var items=edit.level.GetComponentsInChildren<PushBoxObject>(true).Where(other=>other.gameObject.activeInHierarchy&&other.gameObject!=ignore).ToArray();
            int? anchorHeight=null;
            for(int y=0;y<footprint.y;y++) for(int x=0;x<footprint.x;x++)
            {
                var point=p+new Vector2Int(x,y);
                if(!edit.Contains(point)||!edit.tiles.TryGetValue(point,out var tile)||!tile.Walkable) return "占地必须位于边界内的可通行地面。";
                anchorHeight??=tile.HeightLevel;
                if(footprint!=Vector2Int.one&&(tile.Slope!=VoxelSlopeDirection.None||tile.HeightLevel!=anchorHeight)) return "多格物件需要同高平地。";
                bool solid=actor!=null||item.IsSolid;
                if(solid&&(actors.Any(other=>other.GridPosition==point)||items.Any(other=>other.IsSolid&&other.Covers(point)))) return "终点被角色、箱子或阻挡物占用。";
                if(item!=null&&items.Any(other=>other.Kind==item.Kind&&other.Covers(point))) return "同一格不能重复放置同类机关。";
            }
            return null;
        }
    }
}
