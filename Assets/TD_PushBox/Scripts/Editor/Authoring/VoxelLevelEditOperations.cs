using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    public sealed class VoxelLevelEditOperations
    {
        public const string SupportName="Terrain Support";
        public const string DecorationGroup="Details";
        public readonly VoxelTacticsLevel level;
        public readonly VoxelLevelPalette palette;
        public readonly Dictionary<Vector2Int,VoxelTacticsTile> tiles=new();
        private readonly List<GameObject> modelObjects;
        public Vector2Int Size { get; }
        public bool IsPushBox=>level.GetComponent<PushBoxLevel>()!=null;

        public VoxelLevelEditOperations(VoxelTacticsLevel level,VoxelLevelPalette palette)
        {
            if(level==null||EditorUtility.IsPersistent(level)||!level.gameObject.scene.IsValid()||Application.isPlaying)
                throw new InvalidOperationException("请选择编辑模式下的场景关卡。");
            this.level=level;this.palette=palette;
            Vector2Int inferred=Vector2Int.one;
            foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>(true))
            {
                if(!tile.gameObject.activeInHierarchy) continue;
                if(!tiles.TryAdd(tile.GridPosition,tile)) throw new InvalidOperationException("地块坐标重复："+tile.GridPosition);
                inferred=Vector2Int.Max(inferred,tile.GridPosition+Vector2Int.one);
            }
            Size=level.BoardSize.x>0&&level.BoardSize.y>0?level.BoardSize:inferred;
            modelObjects=level.GetComponentsInChildren<Transform>(true)
                .Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&IsModelOnlyObject(t.gameObject)).Select(t=>t.gameObject).ToList();
        }

        public bool Contains(Vector2Int p)=>p.x>=0&&p.y>=0&&p.x<Size.x&&p.y<Size.y;
        private static bool Under(GameObject candidate,GameObject root)=>candidate==root||candidate.transform.IsChildOf(root.transform);
        private void Dirty()=>EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        public static void Record(UnityEngine.Object target)
        {
            if(Application.isPlaying||Undo.isProcessing||target==null) return;
            EditorUtility.SetDirty(target);if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private Transform Group(string name)
        {
            var group=name=="Tiles"?level.TerrainContainer:name=="Units"?level.ActorsContainer:
                name=="Objects"?level.ObjectsContainer:level.DetailsContainer;
            if(group!=null) return group;
            string label=name=="Tiles"?"Tiles":name=="Units"?"Units":name=="Objects"?"Objs":DecorationGroup;
            var go=new GameObject(label);Undo.RegisterCreatedObjectUndo(go,"创建关卡分组");
            Undo.SetTransformParent(go.transform,level.transform,"创建关卡分组");
            Undo.RecordObject(level,"关联关卡分组");level.AssignContainer(name==DecorationGroup?"Details":name,go.transform);Record(level);
            return go.transform;
        }
        private GameObject Instantiate(GameObject prefab,Transform parent)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            Undo.RegisterCreatedObjectUndo(go,"放置关卡资源");return go;
        }
        public Vector3 Surface(Vector2Int p,int fallbackHeight=0)=>tiles.TryGetValue(p,out var tile)?tile.SurfacePosition:level.Surface(p,fallbackHeight);

        private Vector2Int OccupantPosition(GameObject go)
        {
            if(PushBoxEditOperations.IsEntity(go)) return PushBoxEditOperations.Position(go);
            return PositionedCell(go.transform);
        }
        private Vector2Int PositionedCell(Transform target)
        {
            Vector3 point=level.transform.InverseTransformPoint(target.position)-level.GridOrigin;
            return Vector2Int.RoundToInt(new Vector2(point.x/level.CellSize,point.z/level.CellSize));
        }
        private bool HasOccupants(Vector2Int p)=>modelObjects.Any(go=>go!=null&&OccupantPosition(go)==p)||PushBoxEditOperations.Occupied(level,p);
        private GameObject[] TileContents(VoxelTacticsTile tile)=>tile==null?Array.Empty<GameObject>():
            tile.GetComponentsInChildren<PushBoxActor>(true).Select(actor=>actor.gameObject)
                .Concat(tile.GetComponentsInChildren<PushBoxObject>(true).Select(item=>item.gameObject))
                .Concat(modelObjects.Where(go=>go!=null&&go.transform.IsChildOf(tile.transform))).Distinct().ToArray();
        private static bool FlatTemplate(VoxelTacticsTile tile)=>tile!=null&&tile.HeightLevel==0&&tile.Slope==VoxelSlopeDirection.None&&tile.Walkable;
        public int ResolveTerrainHeight(Vector2Int p,VoxelLevelPalette.Entry entry,int height,bool preserveHeight)
        {
            if(!preserveHeight||entry?.prefab==null||!FlatTemplate(entry.prefab.GetComponent<VoxelTacticsTile>())
                ||!tiles.TryGetValue(p,out var old)||old.Slope!=VoxelSlopeDirection.None) return height;
            var source=PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject);
            return source!=null&&FlatTemplate(source.GetComponent<VoxelTacticsTile>())?old.HeightLevel:height;
        }

        public string CheckTerrain(Vector2Int p,VoxelLevelPalette.Entry entry,bool erase,int? targetHeight=null)
        {
            if(!Contains(p)) return "格点在编辑边界之外。";
            if(!erase)
            {
                string error=VoxelLevelPalette.Check(entry);if(error!=null) return error;
                if(entry.category!=VoxelPaletteCategory.Terrain) return "当前资源不是地块。";
                if(entry.IsTerrainObject) return CheckPlacement(entry.prefab,p);
            }
            tiles.TryGetValue(p,out var old);
            var contents=TileContents(old);
            if(old!=null)
            {
                foreach(Transform child in old.transform)
                    if(child.name!="Visual"&&child.name!=SupportName&&child.name!="Selection Outline"&&child.name!=VoxelDetailScatter.GroupName
                        &&!contents.Contains(child.gameObject))
                        return "原地块含有自定义子节点，请先移出再替换："+child.name;
                foreach(var go in contents)
                    if(go==old.gameObject||PrefabUtility.IsPartOfPrefabInstance(go)&&!PrefabUtility.IsAddedGameObjectOverride(go)
                        &&PrefabUtility.GetOutermostPrefabInstanceRoot(go)!=go)
                        return "角色或机关属于地块预制体内部，请先移出后再修改地形："+go.name;
            }
            bool blocked=erase||!entry.prefab.GetComponent<VoxelTacticsTile>().Walkable;
            if(blocked&&(HasOccupants(p)||contents.Length>0))
                return "此格有角色或机关，不能移除地面或刷成不可通行地块。";
            var puzzleWide=level.GetComponentsInChildren<PushBoxObject>(true).FirstOrDefault(item=>item.Footprint!=Vector2Int.one&&item.Covers(p));
            if(!blocked&&puzzleWide!=null&&tiles.TryGetValue(puzzleWide.GridPosition,out var puzzleSupport)
                &&(entry.prefab.GetComponent<VoxelTacticsTile>().Slope!=VoxelSlopeDirection.None||targetHeight.HasValue&&targetHeight!=puzzleSupport.HeightLevel))
                return "多格物件占地必须保持同高平地。";

            return null;
        }

        public VoxelTacticsTile Paint(Vector2Int p,VoxelLevelPalette.Entry entry,int height,bool preserveHeight,out string error)
        {
            if(entry!=null&&entry.IsTerrainObject)
            {
                var placed=Place(p,entry,height,out error);
                return placed!=null?tiles[p]:null;
            }
            height=ResolveTerrainHeight(p,entry,height,preserveHeight);
            error=CheckTerrain(p,entry,false,height);if(error!=null) return null;
            tiles.TryGetValue(p,out var old);
            var template=entry.prefab.GetComponent<VoxelTacticsTile>();
            if(old!=null&&PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject)==entry.prefab&&old.HeightLevel==height) return old;
            var contents=TileContents(old);
            var go=Instantiate(entry.prefab,old!=null?old.transform.parent:Group("Tiles"));
            var tile=go.GetComponent<VoxelTacticsTile>();
            Undo.RecordObject(tile,"绘制地形");tile.ConfigureDefinition(template.Definition,p,height);tile.ConfigureSlope(template.Slope);
            go.name=$"{ObjNames.Label(entry.label,entry.prefab.name)}_{p.x:00}_{p.y:00}";
            go.transform.SetPositionAndRotation(level.Surface(p,height),level.transform.rotation);go.transform.localScale=Vector3.one;
            Record(tile);Record(go.transform);
            if(old!=null)
            {
                // Scene-authored occupants must outlive replacement of their supporting tile.
                foreach(var child in contents)
                    if(child!=null&&child.transform.IsChildOf(old.transform))
                    {
                        Undo.SetTransformParent(child.transform,Group(child.GetComponent<PushBoxActor>()!=null?"Units":"Objects"),"保留地块上的角色和机关");
                        Record(child.transform);
                    }
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            tiles[p]=tile;SyncSupport(tile);SnapOccupants(p,contents);Dirty();return tile;
        }

        public bool PaintRectangle(Vector2Int from,Vector2Int to,VoxelLevelPalette.Entry entry,int height,bool preserveHeight,out string message)
        {
            message=VoxelLevelPalette.Check(entry);if(message!=null) return false;
            if(entry.category!=VoxelPaletteCategory.Terrain) {message="矩形绘制仅支持地形。";return false;}
            if(!Contains(from)||!Contains(to)) {message="矩形不能超出棋盘边界。";return false;}
            bool blocked=entry.IsTerrainObject||!entry.prefab.GetComponent<VoxelTacticsTile>().Walkable;
            var cells=new List<Vector2Int>();int skipped=0;
            for(int y=Mathf.Min(from.y,to.y);y<=Mathf.Max(from.y,to.y);y++)
                for(int x=Mathf.Min(from.x,to.x);x<=Mathf.Max(from.x,to.x);x++)
                {
                    var p=new Vector2Int(x,y);
                    if(blocked&&(HasOccupants(p)||tiles.TryGetValue(p,out var old)&&TileContents(old).Length>0)) {skipped++;continue;}
                    message=CheckTerrain(p,entry,false,ResolveTerrainHeight(p,entry,height,preserveHeight));if(message!=null) return false;
                    cells.Add(p);
                }
            foreach(var p in cells) if(Paint(p,entry,height,preserveHeight,out message)==null) return false;
            message=$"已绘制 {cells.Count} 格地形。"+(skipped>0?$"跳过 {skipped} 个有角色或机关的格子，保留原地面。":"");
            return true;
        }

        public bool EraseTerrain(Vector2Int p,out string error)
        {
            error=CheckTerrain(p,null,true);if(error!=null) return false;
            if(!tiles.TryGetValue(p,out var tile)) return true;
            Undo.DestroyObjectImmediate(tile.gameObject);tiles.Remove(p);Dirty();return true;
        }

        public void SyncSupport(VoxelTacticsTile tile,bool recordUndo=true)
        {
            void Remove(UnityEngine.Object target)
            {if(recordUndo) Undo.DestroyObjectImmediate(target);else UnityEngine.Object.DestroyImmediate(target);}
            var old=tile.transform.Find(SupportName);
            bool needed=palette!=null&&palette.generateSupport&&tile.CreateState().kind!=VoxelGroundKind.Void
                &&(tile.Definition==null||tile.Definition.generateSupport);
            if(!needed) { if(old!=null) Remove(old.gameObject);return; }
            float top=-0.56f;
            var visual=tile.transform.Find("Visual");
            if(visual!=null)
            {
                bool found=false;
                foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    if(filter.sharedMesh==null) continue;
                    Bounds bounds=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        Vector3 corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        float y=tile.transform.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                        if(!found||y<top) top=y;found=true;
                    }
                }
            }
            float bottom=-tile.HeightLevel*level.HeightStep-0.56f-Mathf.Max(0.01f,palette.supportDepth);
            if(top<=bottom) { if(old!=null) Remove(old.gameObject);return; }
            if(old==null)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=SupportName;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,tile.gameObject.scene);
                if(recordUndo) Undo.RegisterCreatedObjectUndo(go,"更新地块底座");
                Remove(go.GetComponent<Collider>());
                if(recordUndo) Undo.SetTransformParent(go.transform,tile.transform,"更新地块底座");
                else go.transform.SetParent(tile.transform,false);
                old=go.transform;
            }
            if(recordUndo) Undo.RecordObject(old,"更新地块底座");old.localPosition=new Vector3(0,(top+bottom)/2,0);
            old.localRotation=Quaternion.identity;old.localScale=new Vector3(1.08f,top-bottom,1.08f);
            var renderer=old.GetComponent<MeshRenderer>();if(recordUndo) Undo.RecordObject(renderer,"更新地块底座");renderer.sharedMaterial=palette.supportMaterial;
            Record(old);Record(renderer);
        }

        private void SnapOccupants(Vector2Int p,IEnumerable<GameObject> preserved)
        {
            var targets=modelObjects.Concat(preserved)
                .Concat(level.GetComponentsInChildren<PushBoxActor>(true).Select(actor=>actor.gameObject))
                .Concat(level.GetComponentsInChildren<PushBoxObject>(true).Select(item=>item.gameObject))
                .Where(go=>go!=null&&PositionedCell(go.transform)==p).Distinct().ToArray();
            foreach(var target in targets)
            {
                // Painting terrain only changes footing; grid placement belongs to the move tool.
                var body=target.transform;Vector3 up=level.transform.up;
                Undo.RecordObject(body,"对齐关卡对象高度");
                body.position+=up*Vector3.Dot(Surface(p)-body.position,up);Record(body);
            }
        }

        public string CheckPlacement(GameObject prefab,Vector2Int p,GameObject ignore=null,bool decoration=false,bool surfaceDetail=false,bool modelOnly=false)
        {
            if(modelOnly) return Contains(p)&&tiles.TryGetValue(p,out var support)&&support.Walkable?null:"机关模型需要边界内的可通行地面。";
            if(!surfaceDetail) return PushBoxEditOperations.CheckPlacement(this,prefab,p,ignore,decoration);
            if(!Contains(p)||!tiles.TryGetValue(p,out var tile)||!VoxelDetailScatter.Supports(tile))
                return "花草需要地面或石块表面，不能铺设在深渊上。";
            if(PushBoxEditOperations.Occupied(level,p,TerrainObjectAt(p)?.gameObject)) return "有角色或机关的格子不能铺设花草。";
            var group=tile.transform.Find(VoxelDetailScatter.GroupName);
            if(group!=null) foreach(Transform child in group)
                if(child.gameObject!=ignore&&PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)==prefab) return "此格已有同类花草。";
            return null;
        }

        public GameObject Place(Vector2Int p,VoxelLevelPalette.Entry entry,int height,out string error,int facingYaw=0)
        {
            error=VoxelLevelPalette.Check(entry);if(error!=null) return null;
            if(entry.category==VoxelPaletteCategory.Terrain&&!entry.IsTerrainObject) { error="请使用地形画笔。";return null; }
            bool decoration=entry.category==VoxelPaletteCategory.Decoration;
            error=CheckPlacement(entry.prefab,p,null,decoration,entry.surfaceDetail,entry.modelOnly);if(error!=null) return null;
            bool logicalObject=entry.prefab.GetComponent<PushBoxObject>()!=null;
            var go=Instantiate(entry.prefab,entry.surfaceDetail?VoxelDetailScatter.Group(tiles[p]):Group(logicalObject||entry.category==VoxelPaletteCategory.Objects?"Objects":decoration?DecorationGroup:"Units"));
            if(logicalObject) Undo.SetTransformParent(go.transform,PushBoxObjectGroups.For(level,go.GetComponent<PushBoxObject>().Kind),"放入物件分组");
            if(PushBoxEditOperations.IsEntity(go))
                VoxelLevelHierarchyNames.SetUniqueName(go,ObjNames.Label(entry.label,entry.prefab.name));
            else go.name=ObjNames.Label(entry.label,entry.prefab.name);
            go.transform.SetPositionAndRotation(decoration?Surface(p,height):Surface(p),level.transform.rotation);
            if(go.GetComponent<PushBoxObject>()!=null) go.transform.rotation=level.transform.rotation*Quaternion.Euler(0,facingYaw,0);
            if(entry.surfaceDetail) VoxelDetailScatter.Align(go,tiles[p]);
            Initialize(go,p,true);
            if(entry.IsTerrainObject) RefreshDetails(p);
            if(entry.modelOnly) modelObjects.Add(go);
            Record(go.transform);Dirty();return go;
        }

        public void RotateStatue(GameObject go,int yaw)
        {
            if(!IsOwned(go)||go.GetComponent<PushBoxObject>()==null) return;
            Undo.RecordObject(go.transform,"旋转火龙石像");
            go.transform.rotation=level.transform.rotation*Quaternion.Euler(0,yaw,0);
            Record(go.transform);Dirty();
        }

        private static void Initialize(GameObject go,Vector2Int p,bool newId)
        {
            if(PushBoxEditOperations.IsEntity(go))
            {
                Component puzzle=go.GetComponent<PushBoxActor>();if(puzzle==null) puzzle=go.GetComponent<PushBoxObject>();
                Undo.RecordObject(puzzle,"设置推箱子对象");
                if(puzzle is PushBoxActor actor) actor.SetPlacement(p,newId);else ((PushBoxObject)puzzle).SetPlacement(p,newId);
                Record(puzzle);return;
            }
        }
        public bool IsOwned(GameObject go)=>go!=null&&go!=level.gameObject&&go.transform.IsChildOf(level.transform);
        public PushBoxObject TerrainObjectAt(Vector2Int p)=>level.GetComponentsInChildren<PushBoxObject>()
            .FirstOrDefault(item=>item.Covers(p)&&IsTerrainObject(item.gameObject));
        private void RefreshDetails(Vector2Int p)
        {
            if(tiles.TryGetValue(p,out var tile)&&tile.transform.Find(VoxelDetailScatter.GroupName)!=null) VoxelDetailScatter.Group(tile);
        }
        public bool IsTerrainObject(GameObject go)
        {
            if(go==null||palette==null) return false;
            var prefab=PrefabUtility.GetCorrespondingObjectFromSource(go);
            return palette.entries.Any(entry=>entry.IsTerrainObject&&entry.prefab==prefab);
        }
        public bool IsSurfaceDetail(GameObject go)
        {
            if(go==null||palette==null) return false;
            var prefab=PrefabUtility.GetCorrespondingObjectFromSource(go);
            return prefab!=null&&palette.entries.Any(e=>e.surfaceDetail&&e.prefab==prefab);
        }
        public bool IsModelOnlyObject(GameObject go)
        {
            if(go==null||palette==null) return false;
            var prefab=PrefabUtility.GetCorrespondingObjectFromSource(go);
            return prefab!=null&&palette.entries.Any(e=>e.category==VoxelPaletteCategory.Objects&&e.modelOnly&&e.prefab==prefab);
        }
        public bool IsDecoration(GameObject go)=>IsOwned(go)&&!IsModelOnlyObject(go)&&go.GetComponent<PushBoxActor>()==null
            &&go.GetComponent<VoxelTacticsTile>()==null&&(go.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Blocker&&palette.entries.Any(entry=>entry.category==VoxelPaletteCategory.Decoration&&entry.prefab==PrefabUtility.GetCorrespondingObjectFromSource(go))
                ||go.transform.parent==level.DetailsContainer||IsSurfaceDetail(go));

        public bool Move(GameObject go,Vector2Int p,int height,out string error)
        {
            error=null;if(!IsOwned(go)) { error="只能编辑当前关卡的对象。";return false; }
            bool decoration=IsDecoration(go),detail=IsSurfaceDetail(go);
            Vector2Int? oldPoint=IsTerrainObject(go)?PushBoxEditOperations.Position(go):null;
            error=CheckPlacement(detail?PrefabUtility.GetCorrespondingObjectFromSource(go):go,p,go,decoration,detail,IsModelOnlyObject(go));if(error!=null) return false;
            Initialize(go,p,false);Undo.RecordObject(go.transform,"移动关卡对象");
            if(detail) {Undo.SetTransformParent(go.transform,VoxelDetailScatter.Group(tiles[p]),"移动地表碎屑");VoxelDetailScatter.Align(go,tiles[p]);}
            else go.transform.position=decoration?Surface(p,height):Surface(p);
            if(oldPoint.HasValue) {RefreshDetails(oldPoint.Value);RefreshDetails(p);}
            Record(go.transform);Dirty();return true;
        }

        public bool Erase(GameObject go,out string error)
        {
            error=null;
            if(!IsOwned(go)) { error="只能删除当前关卡的对象。";return false; }
            if(go.TryGetComponent<VoxelTacticsTile>(out var tile)) return EraseTerrain(tile.GridPosition,out error);
            if(go.TryGetComponent<PushBoxObject>(out var puzzleItem))
                foreach(var other in level.GetComponentsInChildren<PushBoxObject>(true))
                {
                    var links=new SerializedObject(other).FindProperty("inputs");
                    for(int i=0;i<links.arraySize;i++) if(links.GetArrayElementAtIndex(i).objectReferenceValue==puzzleItem)
                    {error="物件仍被机关引用，请先解除关联："+other.name;return false;}
                }
            if(!PushBoxEditOperations.IsEntity(go)&&!IsDecoration(go)&&!IsModelOnlyObject(go))
            { error="此对象不是工具管理的角色、机关或环境物件。";return false; }
            modelObjects.RemoveAll(model=>model==null||Under(model,go));
            Vector2Int? oldPoint=IsTerrainObject(go)?PushBoxEditOperations.Position(go):null;
            Undo.DestroyObjectImmediate(go);if(oldPoint.HasValue) RefreshDetails(oldPoint.Value);Dirty();return true;
        }
    }
}
