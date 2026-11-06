using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    public static class VoxelDetailScatter
    {
        public const string GroupName="Scattered Details";
        public const string LegacyGroupName="Tile Details";

        public static bool Supports(VoxelTacticsTile tile)
        {
            var cell=tile.CreateState();
            return cell.kind!=VoxelGroundKind.Void&&(tile.Walkable||cell.kind==VoxelGroundKind.Wall||cell.kind==VoxelGroundKind.Stone);
        }

        private static Vector3 Surface(VoxelTacticsTile tile)
        {
            Vector3 surface=tile.SurfacePosition;
            var level=tile.GetComponentInParent<VoxelTacticsLevel>();
            var prefabs=PushBoxAuthoring.Palette().entries.Where(entry=>entry.IsTerrainObject).Select(entry=>entry.prefab).ToHashSet();
            var obstacle=level.GetComponentsInChildren<PushBoxObject>().FirstOrDefault(item=>item.Covers(tile.GridPosition)
                &&prefabs.Contains(PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject)));
            if(tile.Walkable&&obstacle==null) return surface;
            var visual=(obstacle!=null?obstacle.transform:tile.transform).Find("Visual");if(visual==null) return surface;
            Vector3 up=level.transform.up;float top=0;
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if(filter.sharedMesh==null) continue;
                Bounds bounds=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    top=Mathf.Max(top,Vector3.Dot(filter.transform.TransformPoint(corner)-surface,up));
                }
            }
            return surface+up*top;
        }

        public static Transform Group(VoxelTacticsTile tile,bool recordUndo=true)
        {
            var group=tile.transform.Find(GroupName);
            if(group==null)
            {
                var go=new GameObject(GroupName);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,tile.gameObject.scene);
                if(recordUndo)
                {
                    Undo.RegisterCreatedObjectUndo(go,"创建地表碎屑分组");
                    Undo.SetTransformParent(go.transform,tile.transform,"创建地表碎屑分组");
                }
                else go.transform.SetParent(tile.transform,false);
                group=go.transform;
            }
            if(recordUndo) Undo.RecordObject(group,"对齐地表碎屑");
            group.SetPositionAndRotation(Surface(tile),tile.GetComponentInParent<VoxelTacticsLevel>().transform.rotation);
            VoxelLevelEditOperations.Record(group);return group;
        }

        public static void Align(GameObject go,VoxelTacticsTile tile)
        {
            float rise=tile.GetComponentInParent<VoxelTacticsLevel>().HeightStep/1.08f;
            Vector2Int uphill=tile.CreateState().Uphill;
            Vector3 normal=new Vector3(-uphill.x*rise,1,-uphill.y*rise).normalized;
            go.transform.localPosition=new Vector3(0,0.005f,0);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,normal);
        }

        public static string CheckPalette(VoxelLevelPalette palette,int coverage)
        {
            if(coverage<=0) return null;
            if(palette==null) return "请选择资源目录。";
            var entries=palette.entries.Where(e=>e.surfaceDetail).ToArray();
            if(entries.Length==0) return "资源目录中没有花草资源。";
            foreach(var entry in entries)
            {
                string error=VoxelLevelPalette.Check(entry);
                if(error!=null) return entry.label+"："+error;
            }
            return null;
        }

        public static void Validate(VoxelLevelEditOperations edit,int coverage)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("运行期间不能铺设花草。");
            string error=CheckPalette(edit.palette,coverage);
            if(error!=null) throw new InvalidOperationException(error);
            ValidateGroups(DetailGroups(edit.level));
        }

        public static int Scatter(VoxelLevelEditOperations edit,int coverage,int seed,bool recordUndo=true)
        {
            Validate(edit,coverage);
            var entries=edit.palette.entries.Where(e=>e.surfaceDetail).ToArray();
            var occupied=new HashSet<Vector2Int>();
            foreach(var actor in edit.level.GetComponentsInChildren<PushBoxActor>(true).Where(actor=>actor.gameObject.activeInHierarchy)) occupied.Add(actor.GridPosition);
            foreach(var item in edit.level.GetComponentsInChildren<PushBoxObject>(true).Where(item=>item.gameObject.activeInHierarchy))
            {
                if(edit.IsTerrainObject(item.gameObject)) continue;
                for(int y=0;y<item.Footprint.y;y++) for(int x=0;x<item.Footprint.x;x++) occupied.Add(item.GridPosition+new Vector2Int(x,y));
            }
            var cells=edit.tiles.Values.Where(t=>edit.Contains(t.GridPosition)&&Supports(t)&&!occupied.Contains(t.GridPosition))
                .OrderBy(t=>t.GridPosition.y).ThenBy(t=>t.GridPosition.x).ToArray();
            int count=Mathf.RoundToInt(cells.Length*Mathf.Clamp(coverage,0,100)/100f);
            Clear(edit.level,recordUndo);
            var random=new System.Random(seed);
            for(int i=0;i<count;i++)
            {
                int next=random.Next(i,cells.Length);(cells[i],cells[next])=(cells[next],cells[i]);
                var tile=cells[i];var entry=entries[random.Next(entries.Length)];
                var go=(GameObject)PrefabUtility.InstantiatePrefab(entry.prefab,Group(tile,recordUndo));
                if(recordUndo) Undo.RegisterCreatedObjectUndo(go,"铺设花草");
                go.name=ObjNames.Label(entry.label,entry.prefab.name);Align(go,tile);
                VoxelLevelEditOperations.Record(go);VoxelLevelEditOperations.Record(go.transform);
            }
            EditorSceneManager.MarkSceneDirty(edit.level.gameObject.scene);return count;
        }

        private static List<Transform> DetailGroups(VoxelTacticsLevel level)
        {
            var groups=level.GetComponentsInChildren<VoxelTacticsTile>(true).Select(t=>t.transform.Find(GroupName)).Where(g=>g!=null).ToList();
            var legacy=level.EnvironmentContainer!=null?level.EnvironmentContainer.Find(LegacyGroupName):null;if(legacy!=null) groups.Add(legacy);
            return groups;
        }
        private static void ValidateGroups(IEnumerable<Transform> groups)
        {
            foreach(var group in groups)
                if(group.GetComponentsInChildren<VoxelTacticsTile>(true).Length>0||group.GetComponentsInChildren<PushBoxActor>(true).Length>0
                    ||group.GetComponentsInChildren<PushBoxObject>(true).Length>0||group.GetComponentsInChildren<Collider>(true).Length>0)
                    throw new InvalidOperationException("花草分组中存在玩法组件或碰撞体，请先移出："+group.name);
        }
        public static void Clear(VoxelTacticsLevel level,bool recordUndo=true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("运行期间不能清空花草。");
            var groups=DetailGroups(level);ValidateGroups(groups);
            foreach(var group in groups)
                if(recordUndo) Undo.DestroyObjectImmediate(group.gameObject);else UnityEngine.Object.DestroyImmediate(group.gameObject);
            if(groups.Count>0) EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }
    }
}
