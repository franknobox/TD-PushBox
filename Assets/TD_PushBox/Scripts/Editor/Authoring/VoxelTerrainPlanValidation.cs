using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class VoxelTerrainPlanValidation
    {
        public static void Validate(VoxelTacticsLevel level,VoxelTerrainChangeDefinition plan)
        {
            if(level==null||plan==null) throw new InvalidOperationException("请选择关卡和场地变化方案。");
            var edit=new VoxelLevelEditOperations(level,PushBoxAuthoring.Palette());
            var cells=edit.tiles.ToDictionary(p=>p.Key,p=>p.Value.CreateState());
            var tilePoints=new HashSet<Vector2Int>();
            foreach(var tile in plan.EffectiveTiles())
            {
                if(tile?.prefab==null||!EditorUtility.IsPersistent(tile.prefab)||!edit.Contains(tile.position)||!tilePoints.Add(tile.position))
                    throw new InvalidOperationException("场地方案包含无效地块、重复坐标或越界地块。");
                var cell=tile.prefab.CreateState();cell.position=tile.position;cell.height=tile.height;cells[tile.position]=cell;
            }
            void Ground(Vector2Int point,Vector2Int area)
            {
                int? first=null;
                for(int y=0;y<area.y;y++) for(int x=0;x<area.x;x++)
                {
                    var p=point+new Vector2Int(x,y);
                    if(!cells.TryGetValue(p,out var cell)||!edit.Contains(p)||!cell.walkable||cell.kind==VoxelGroundKind.Void)
                        throw new InvalidOperationException("目标或新增物件缺少可通行地面："+p);
                    first??=cell.height;
                    if(area!=Vector2Int.one&&(cell.height!=first||cell.slope!=VoxelSlopeDirection.None))
                        throw new InvalidOperationException("多格物件需要同高平地："+p);
                }
            }
            var existing=level.GetComponentsInChildren<PushBoxObject>(true).ToDictionary(o=>o.StableId);
            var keys=new HashSet<string>();
            void Entity(string key,string id,PushBoxObject prefab,bool remove,Vector2Int position,bool enemy)
            {
                if(string.IsNullOrWhiteSpace(key)||!keys.Add(key)) throw new InvalidOperationException("变化实体标识缺失或重复。");
                if(!string.IsNullOrEmpty(id)&&!existing.ContainsKey(id)) throw new InvalidOperationException("引用的初始物件已不存在："+id);
                if(remove) return;
                if(prefab==null||!EditorUtility.IsPersistent(prefab)||(prefab.Kind==PushBoxKind.Enemy)!=enemy)
                    throw new InvalidOperationException("变化实体需要对应类型的推箱子预制体。");
                Ground(position,prefab.Footprint);
            }
            foreach(var e in plan.enemies) Entity(e.key,e.existingId,e.targetPrefab,e.remove,e.position,true);
            foreach(var e in plan.objects) Entity(e.key,e.existingId,e.targetPrefab,e.remove,e.position,false);
            if(plan.exits.Distinct().Count()!=plan.exits.Count) throw new InvalidOperationException("目标点坐标重复。");
            if(plan.exits.Count>0&&(plan.targetExitPrefab==null||!EditorUtility.IsPersistent(plan.targetExitPrefab)||plan.targetExitPrefab.Kind!=PushBoxKind.Goal))
                throw new InvalidOperationException("目标点需要推箱子 Goal 预制体。");
            foreach(var p in plan.exits) Ground(p,Vector2Int.one);
            if(plan.bossPrefab!=null&&plan.overrideBossPosition)
            {
                if(plan.bossPrefab.Kind!=PushBoxKind.Enemy||!EditorUtility.IsPersistent(plan.bossPrefab)) throw new InvalidOperationException("Boss 必须是推箱子敌方预制体。");
                Ground(plan.bossPosition,plan.bossPrefab.Footprint);
            }
            var plates=new HashSet<string>(existing.Values.Where(o=>o.Kind==PushBoxKind.Plate&&!plan.objects.Any(e=>e.existingId==o.StableId&&e.remove)).Select(o=>o.StableId));
            foreach(var e in plan.objects) if(!e.remove&&e.targetPrefab?.Kind==PushBoxKind.Plate) plates.Add(string.IsNullOrEmpty(e.existingId)?"plan:"+e.key:e.existingId);
            foreach(var e in plan.objects) if(!e.remove&&e.overrideInputs&&e.inputs.Any(id=>!plates.Contains(id)))
                throw new InvalidOperationException("方案中的门引用了无效压力板。");
        }
    }
}
