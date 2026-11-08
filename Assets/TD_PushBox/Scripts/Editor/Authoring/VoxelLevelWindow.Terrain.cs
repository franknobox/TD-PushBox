using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed partial class VoxelLevelWindow
    {
        [SerializeField] private bool afterSummon,placingBoss;
        [SerializeField] private string changeEnemyKey,changeObjectKey;
        private VoxelTerrainChangePreview terrainPreview;
        private bool terrainPreviewDirty=true;
        private int previewPlanVersion=-1;
        private VoxelTerrainChangeDefinition previewPlan;
        private VoxelTerrainChangeDefinition ChangePlan=>level!=null?level.TerrainChange:null;
        private bool IsChangeEntry(VoxelLevelPalette.Entry entry)=>entry.category==VoxelPaletteCategory.Terrain
            ||entry.prefab!=null&&entry.prefab.GetComponent<PushBoxObject>()!=null
                &&(entry.category==VoxelPaletteCategory.Enemies||entry.category==VoxelPaletteCategory.Objects);
        private void ClearTerrainPreview() {terrainPreview?.Dispose();terrainPreview=null;terrainPreviewDirty=true;}
        private void HierarchyChanged() {terrainPreviewDirty=true;context=null;Repaint();}
        private void BeforeSceneSave(Scene scene,string path) {SetEditing(false);ClearTerrainPreview();afterSummon=false;}
        private void SetAfterSummon(bool value)
        {
            SetEditing(false);ClearTerrainPreview();afterSummon=value;placingBoss=false;changeEnemyKey=changeObjectKey=null;
            if(value) {SetCategory(VoxelPaletteCategory.Terrain);mode=Mode.Paint;}
            Repaint();SceneView.RepaintAll();
        }
        private void AssignChangePlan(VoxelTerrainChangeDefinition plan)
        {
            var data=new SerializedObject(level);data.FindProperty("terrainChange").objectReferenceValue=plan;
            data.ApplyModifiedProperties();VoxelLevelEditOperations.Record(level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);ClearTerrainPreview();
        }
        private void DrawTerrainChangeControls()
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Toggle(!afterSummon,"初始场地",EditorStyles.miniButtonLeft,GUILayout.Height(28))&&afterSummon) SetAfterSummon(false);
                if(GUILayout.Toggle(afterSummon,"场地变化",EditorStyles.miniButtonRight,GUILayout.Height(28))&&!afterSummon) SetAfterSummon(true);
            }
            if(!afterSummon) return;
            var plan=(VoxelTerrainChangeDefinition)EditorGUILayout.ObjectField("场地变化方案",ChangePlan,typeof(VoxelTerrainChangeDefinition),false);
            if(plan!=ChangePlan) AssignChangePlan(plan);
            if(ChangePlan==null) {if(GUILayout.Button("新建方案（复制初始地形）",GUILayout.Height(26))) CreateTerrainPlan();return;}
            EditorGUILayout.HelpBox("方案保留地形、目标点、敌方和机关配置；推箱子触发规则尚未接入，不使用旧战棋回合。",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var marker=(PushBoxObject)EditorGUILayout.ObjectField("目标点预制体",ChangePlan.targetExitPrefab,typeof(PushBoxObject),false);
            bool replace=EditorGUILayout.Toggle("替换原目标点",ChangePlan.switchToEvacuation);
            var boss=(PushBoxObject)EditorGUILayout.ObjectField("生成的 Boss",ChangePlan.bossPrefab,typeof(PushBoxObject),false);
            bool custom=EditorGUILayout.Toggle("配置 Boss 出生点",ChangePlan.overrideBossPosition);
            Vector2Int spawn=ChangePlan.bossPosition;
            using(new EditorGUI.DisabledScope(!custom)) spawn=EditorGUILayout.Vector2IntField("Boss 出生格",spawn);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(ChangePlan,"修改场地方案");ChangePlan.targetExitPrefab=marker;ChangePlan.switchToEvacuation=replace;
                ChangePlan.bossPrefab=boss;ChangePlan.overrideBossPosition=custom;ChangePlan.bossPosition=spawn;ChangedPlan();
            }
            if(GUILayout.Button(placingBoss?"正在点选 Boss 出生格":"在场景中点选 Boss 出生格",GUILayout.Height(26))) {placingBoss=true;SetEditing(true);}
            EditorGUILayout.LabelField($"基础 {ChangePlan.BasisCount} 格 · 修改 {ChangePlan.tiles.Count} 格 · 目标点 {ChangePlan.exits.Count}");
            EditorGUILayout.LabelField($"敌方变化 {ChangePlan.enemies.Count} · 机关变化 {ChangePlan.objects.Count}");
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("新建方案",GUILayout.Height(24))) CreateTerrainPlan();
                if(GUILayout.Button("校验方案",GUILayout.Height(24))) ValidateChangePlan();
            }
        }
        private VoxelTerrainChangeDefinition CopyInitialTerrain()
        {
            var plan=CreateInstance<VoxelTerrainChangeDefinition>();
            try
            {
                var basis=new List<VoxelTerrainChangeDefinition.TileChange>();
                foreach(var tile in context.tiles.Values.OrderBy(t=>t.GridPosition.y).ThenBy(t=>t.GridPosition.x))
                {
                    var prefab=PrefabUtility.GetCorrespondingObjectFromSource(tile);
                    if(prefab==null||!VoxelTerrainChangeDefinition.MatchesInitial(new(){prefab=prefab,height=tile.HeightLevel},tile))
                        prefab=palette.entries.Where(e=>e.category==VoxelPaletteCategory.Terrain&&e.prefab!=null).Select(e=>e.prefab.GetComponent<VoxelTacticsTile>())
                            .FirstOrDefault(t=>VoxelTerrainChangeDefinition.MatchesInitial(new(){prefab=t,height=tile.HeightLevel},tile));
                    if(prefab==null) throw new InvalidOperationException("地块无法对应到预制体："+tile.GridPosition);
                    basis.Add(new(){position=tile.GridPosition,prefab=prefab,height=tile.HeightLevel});
                }
                plan.SetBasis(basis);
                var goals=level.GetComponentsInChildren<PushBoxObject>(true).Where(o=>o.Kind==PushBoxKind.Goal&&o.gameObject.activeInHierarchy).ToArray();
                plan.exits.AddRange(goals.Select(o=>o.GridPosition).Distinct());
                plan.targetExitPrefab=palette.entries.Select(e=>e.prefab?.GetComponent<PushBoxObject>()).FirstOrDefault(o=>o!=null&&o.Kind==PushBoxKind.Goal);
                return plan;
            }
            catch {DestroyImmediate(plan);throw;}
        }
        private void CreateTerrainPlan()
        {
            string path=EditorUtility.SaveFilePanelInProject("复制初始地形","TerrainChange","asset","选择方案位置",TDProjectPaths.Levels);
            if(string.IsNullOrEmpty(path)) return;
            var plan=CopyInitialTerrain();TDProjectPaths.CreateAsset(plan,AssetDatabase.GenerateUniqueAssetPath(path));AssignChangePlan(plan);AssetDatabase.SaveAssets();
            status=$"已复制 {plan.BasisCount} 格初始地形。";
        }
        private void ValidateChangePlan()
        {
            try {VoxelTerrainPlanValidation.Validate(level,ChangePlan);status="方案配置校验通过；机关解法和触发流程仍需跑测。";}
            catch(Exception error) {status=error.Message;}
        }
        private VoxelCellState ChangeCell(Vector2Int p)
        {
            var changed=ChangePlan?.TileAt(p);
            if(changed?.prefab==null) return context.tiles.TryGetValue(p,out var tile)?tile.CreateState():null;
            var cell=changed.prefab.CreateState();cell.position=p;cell.height=changed.height;return cell;
        }
        private Vector3 ChangeSurface(Vector2Int p)
        {
            var cell=ChangeCell(p);return cell==null?context.Surface(p,height):level.Surface(p,cell.height)+level.transform.up*(cell.slope==VoxelSlopeDirection.None?0:level.HeightStep/2);
        }
        private int ChangeHeight(Vector2Int p)
        {
            var old=ChangeCell(p);var template=Entry?.prefab?.GetComponent<VoxelTacticsTile>();
            return preserveFlatHeight&&template!=null&&template.HeightLevel==0&&template.Walkable&&template.Slope==VoxelSlopeDirection.None
                &&old!=null&&old.walkable&&old.slope==VoxelSlopeDirection.None?old.height:height;
        }
        private bool IsTerrainPrefab(PushBoxObject item)=>palette.entries.Any(entry=>entry.IsTerrainObject&&entry.prefab==item?.gameObject);
        private bool TerrainObjectChangeAt(Vector2Int p)
        {
            if(category!=VoxelPaletteCategory.Terrain) return false;
            if(mode==Mode.Paint||mode==Mode.Rectangle) return Entry?.IsTerrainObject==true;
            return EffectiveObjects().Any(item=>IsTerrainPrefab(item.targetPrefab)&&(mode==Mode.Move?item.key==changeObjectKey:ChangeCovers(item,p)));
        }
        private string CheckChange(Vector2Int p)
        {
            if(ChangePlan==null) return "请先指定场地变化方案。";
            if(!context.Contains(p)||!context.tiles.ContainsKey(p)) return "只能编辑棋盘已有格子。";
            if(placingBoss) return CheckEntityPlacement(p,Vector2Int.one,null,true,false);
            if(mode==Mode.Erase||mode==Mode.Pick||mode==Mode.Select||mode==Mode.Move) return null;
            if(Entry==null||!IsChangeEntry(Entry)) return "请选择地形、敌方或机关。";
            string error=VoxelLevelPalette.Check(Entry);if(error!=null) return error;
            if(category==VoxelPaletteCategory.Terrain&&!Entry.IsTerrainObject)
            {
                var cell=Entry.prefab.GetComponent<VoxelTacticsTile>().CreateState();
                if(ChangePlan.overrideBossPosition&&p==ChangePlan.bossPosition&&(!cell.walkable||cell.kind==VoxelGroundKind.Void)) return "Boss 出生点需要可通行地面。";
                if(ChangePlan.exits.Contains(p)&&(!cell.walkable||cell.kind==VoxelGroundKind.Void)) return "目标点需要可通行地面。";
                return null;
            }
            var obj=Entry.prefab.GetComponent<PushBoxObject>();
            return CheckEntityPlacement(p,obj.Footprint,null,obj.IsSolid);
        }
        private void ApplyTerrainChange(Vector2Int p)
        {
            status=CheckChange(p);if(status!=null) return;coordinate=p;
            if(placingBoss) {Undo.RecordObject(ChangePlan,"设置 Boss 出生点");ChangePlan.overrideBossPosition=true;ChangePlan.bossPosition=p;placingBoss=false;ChangedPlan();return;}
            if(TerrainObjectChangeAt(p)) {ApplyObjectChange(p);return;}
            if(category==VoxelPaletteCategory.Terrain)
            {
                changeObjectKey=changeEnemyKey=null;
                if(mode==Mode.Select) return;
                if(mode==Mode.Move) {status="请先选择高地、石块或灌木。";return;}
            }
            if(category==VoxelPaletteCategory.Enemies) {ApplyEnemyChange(p);return;}
            if(category==VoxelPaletteCategory.Objects)
            {
                bool goalBrush=Entry?.prefab?.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Goal;
                if(mode==Mode.Pick&&ChangePlan.exits.Contains(p))
                {
                    int index=palette.entries.FindIndex(e=>e.prefab?.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Goal);
                    if(index>=0) ChooseForPainting(index);return;
                }
                if(mode==Mode.Select||mode==Mode.Move||mode==Mode.Erase&&!ChangePlan.exits.Contains(p)||!goalBrush)
                {ApplyObjectChange(p);return;}
            }
            if(mode==Mode.Pick)
            {
                var template=ChangePlan.TileAt(p)?.prefab;
                var prefab=template!=null?template.gameObject:PrefabUtility.GetCorrespondingObjectFromSource(context.tiles[p].gameObject);
                int index=palette.entries.FindIndex(e=>e.prefab==prefab);if(index>=0) {Choose(index);category=VoxelPaletteCategory.Terrain;}
                height=ChangeCell(p).height;mode=Mode.Paint;return;
            }
            Undo.RecordObject(ChangePlan,"绘制场地变化");
            if(category==VoxelPaletteCategory.Terrain)
            {
                int targetHeight=ChangeHeight(p);ChangePlan.tiles.RemoveAll(t=>t.position==p);
                if(mode!=Mode.Erase) ChangePlan.tiles.Add(new(){position=p,prefab=Entry.prefab.GetComponent<VoxelTacticsTile>(),height=targetHeight});
            }
            else if(mode==Mode.Erase) ChangePlan.exits.Remove(p);
            else if(!ChangePlan.exits.Contains(p)) ChangePlan.exits.Add(p);
            ChangedPlan();
        }
        private void ApplyChangeRectangle(Vector2Int from,Vector2Int to)
        {
            var points=new List<Vector2Int>();
            for(int y=Math.Min(from.y,to.y);y<=Math.Max(from.y,to.y);y++) for(int x=Math.Min(from.x,to.x);x<=Math.Max(from.x,to.x);x++)
            {var p=new Vector2Int(x,y);status=CheckChange(p);if(status!=null) return;points.Add(p);}
            foreach(var p in points) ApplyTerrainChange(p);
            status=$"已配置 {points.Count} 格场地变化。";
        }
        private void ChangedPlan() {EditorUtility.SetDirty(ChangePlan);terrainPreviewDirty=true;Repaint();SceneView.RepaintAll();}
        private VoxelTerrainChangeDefinition.EnemyChange CopyEnemy(VoxelTerrainChangeDefinition.EnemyChange e)=>new(){key=e.key,existingId=e.existingId,targetPrefab=e.targetPrefab,position=e.position,yaw=e.yaw,remove=e.remove};
        private VoxelTerrainChangeDefinition.ObjectChange CopyObject(VoxelTerrainChangeDefinition.ObjectChange e)=>new(){key=e.key,existingId=e.existingId,targetPrefab=e.targetPrefab,position=e.position,yaw=e.yaw,remove=e.remove,overrideInputs=e.overrideInputs,overrideOpen=e.overrideOpen,startsOpen=e.startsOpen,inputs=new(e.inputs)};
        private PushBoxObject Prefab(PushBoxObject obj)
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(obj);if(source!=null) return source;
            var mesh=obj.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            return palette.entries.Select(e=>e.prefab?.GetComponent<PushBoxObject>()).FirstOrDefault(p=>p!=null
                &&p.Kind==obj.Kind&&p.Footprint==obj.Footprint&&p.GetComponentInChildren<MeshFilter>(true)?.sharedMesh==mesh);
        }
        private IEnumerable<VoxelTerrainChangeDefinition.EnemyChange> EffectiveEnemies()
        {
            foreach(var e in ChangePlan.enemies) if(!e.remove) yield return CopyEnemy(e);
            foreach(var obj in level.GetComponentsInChildren<PushBoxObject>(true))
                if(obj.gameObject.activeInHierarchy&&obj.Kind==PushBoxKind.Enemy&&!ChangePlan.enemies.Any(e=>e.existingId==obj.StableId))
                    yield return new(){key="existing:"+obj.StableId,existingId=obj.StableId,targetPrefab=Prefab(obj),position=obj.GridPosition,yaw=Mathf.RoundToInt((Quaternion.Inverse(level.transform.rotation)*obj.transform.rotation).eulerAngles.y/90)*90};
        }
        private IEnumerable<VoxelTerrainChangeDefinition.ObjectChange> EffectiveObjects()
        {
            foreach(var e in ChangePlan.objects) if(!e.remove) yield return CopyObject(e);
            foreach(var obj in level.GetComponentsInChildren<PushBoxObject>(true))
            {
                if(!obj.gameObject.activeInHierarchy||obj.Kind==PushBoxKind.Enemy||obj.Kind==PushBoxKind.Goal||ChangePlan.objects.Any(e=>e.existingId==obj.StableId)) continue;
                var data=new SerializedObject(obj);var inputs=data.FindProperty("inputs");var links=new List<string>();
                for(int i=0;i<inputs.arraySize;i++) if(inputs.GetArrayElementAtIndex(i).objectReferenceValue is PushBoxObject plate) links.Add(plate.StableId);
                yield return new(){key="existing:"+obj.StableId,existingId=obj.StableId,targetPrefab=Prefab(obj),position=obj.GridPosition,startsOpen=obj.StartsOpen,inputs=links,
                    yaw=Mathf.RoundToInt((Quaternion.Inverse(level.transform.rotation)*obj.transform.rotation).eulerAngles.y/90)*90};
            }
        }
        private static bool ChangeCovers(VoxelTerrainChangeDefinition.ObjectChange e,Vector2Int p)=>e.targetPrefab!=null
            &&p.x>=e.position.x&&p.y>=e.position.y&&p.x<e.position.x+e.targetPrefab.Footprint.x&&p.y<e.position.y+e.targetPrefab.Footprint.y;
        private static bool ChangeBlocks(VoxelTerrainChangeDefinition.ObjectChange e)=>e.targetPrefab!=null&&e.targetPrefab.IsSolid
            &&(e.targetPrefab.Kind!=PushBoxKind.Door||!e.startsOpen);
        private string CheckEntityPlacement(Vector2Int p,Vector2Int footprint,string ignore,bool solid,bool reserveBoss=true)
        {
            var first=ChangeCell(p);
            for(int y=0;y<footprint.y;y++) for(int x=0;x<footprint.x;x++)
            {
                var point=p+new Vector2Int(x,y);var cell=ChangeCell(point);
                if(!context.Contains(point)||cell==null||!cell.walkable||cell.kind==VoxelGroundKind.Void) return "目标需要最终场地中的可通行地面。";
                if(footprint!=Vector2Int.one&&(cell.height!=first.height||cell.slope!=VoxelSlopeDirection.None)) return "多格机关需要同高平地。";
                if(solid&&reserveBoss&&ChangePlan.overrideBossPosition&&point==ChangePlan.bossPosition) return "此格已保留给 Boss。";
                if(solid&&EffectiveEnemies().Any(e=>e.key!=ignore&&e.position==point)) return "目标已有变化后的敌人。";
                if(EffectiveObjects().Any(e=>e.key!=ignore&&ChangeCovers(e,point)&&ChangeBlocks(e))) return "目标被变化后的机关占据。";
            }
            return null;
        }
        private void SaveEnemyChange(VoxelTerrainChangeDefinition.EnemyChange e) {Undo.RecordObject(ChangePlan,"编辑场地敌方变化");ChangePlan.enemies.RemoveAll(o=>o.key==e.key);ChangePlan.enemies.Add(e);ChangedPlan();}
        private void SaveObjectChange(VoxelTerrainChangeDefinition.ObjectChange e) {Undo.RecordObject(ChangePlan,"编辑场地机关变化");ChangePlan.objects.RemoveAll(o=>o.key==e.key);ChangePlan.objects.Add(e);ChangedPlan();}
        private void ApplyEnemyChange(Vector2Int p)
        {
            var hit=EffectiveEnemies().FirstOrDefault(e=>e.position==p);
            if(mode==Mode.Select) {changeEnemyKey=hit?.key;changeObjectKey=null;return;}
            if(mode==Mode.Erase)
            {
                if(hit==null) return;
                if(!string.IsNullOrEmpty(hit.existingId)) {hit.remove=true;SaveEnemyChange(hit);}
                else {Undo.RecordObject(ChangePlan,"删除敌方变化");ChangePlan.enemies.RemoveAll(e=>e.key==hit.key);ChangedPlan();}
                return;
            }
            if(mode==Mode.Pick) {int i=palette.entries.FindIndex(e=>e.prefab==hit?.targetPrefab?.gameObject);if(i>=0) ChooseForPainting(i);return;}
            var item=mode==Mode.Move?EffectiveEnemies().FirstOrDefault(e=>e.key==changeEnemyKey):null;
            if(mode==Mode.Move&&item==null) {status="请先选择敌方单位。";return;}
            item??=new(){key=Guid.NewGuid().ToString("N"),targetPrefab=Entry?.prefab?.GetComponent<PushBoxObject>(),yaw=statueDirection*90};
            if(item.targetPrefab?.Kind!=PushBoxKind.Enemy) {status="请选择敌方预制体。";return;}
            status=CheckEntityPlacement(p,Vector2Int.one,item.key,true);if(status!=null) return;
            item.position=p;changeEnemyKey=item.key;changeObjectKey=null;SaveEnemyChange(item);
        }
        private void ApplyObjectChange(Vector2Int p)
        {
            var hit=EffectiveObjects().FirstOrDefault(e=>ChangeCovers(e,p)&&(category==VoxelPaletteCategory.Terrain?IsTerrainPrefab(e.targetPrefab):!IsTerrainPrefab(e.targetPrefab)));
            if(mode==Mode.Select) {changeObjectKey=hit?.key;changeEnemyKey=null;return;}
            if(mode==Mode.Erase)
            {
                if(hit==null) return;
                if(!string.IsNullOrEmpty(hit.existingId)) {hit.remove=true;SaveObjectChange(hit);}
                else {Undo.RecordObject(ChangePlan,"删除机关变化");ChangePlan.objects.RemoveAll(e=>e.key==hit.key);ChangedPlan();}
                return;
            }
            if(mode==Mode.Pick) {int i=palette.entries.FindIndex(e=>e.prefab==hit?.targetPrefab?.gameObject);if(i>=0) ChooseForPainting(i);return;}
            var item=mode==Mode.Move?EffectiveObjects().FirstOrDefault(e=>e.key==changeObjectKey):null;
            if(mode==Mode.Move&&item==null) {status="请先选择机关。";return;}
            item??=new(){key=Guid.NewGuid().ToString("N"),targetPrefab=Entry?.prefab?.GetComponent<PushBoxObject>(),overrideInputs=true,yaw=statueDirection*90};
            if(item.targetPrefab==null) {status="请选择机关预制体。";return;}
            status=CheckEntityPlacement(p,item.targetPrefab.Footprint,item.key,ChangeBlocks(item));if(status!=null) return;
            item.position=p;changeObjectKey=item.key;changeEnemyKey=null;SaveObjectChange(item);
        }
        private void DrawChangeSelection()
        {
            if(ChangePlan==null) return;
            if(category==VoxelPaletteCategory.Enemies)
            {
                var item=EffectiveEnemies().FirstOrDefault(e=>e.key==changeEnemyKey)??ChangePlan.enemies.Where(e=>e.key==changeEnemyKey).Select(CopyEnemy).FirstOrDefault();
                if(item!=null)
                {
                    EditorGUILayout.LabelField(item.targetPrefab!=null?item.targetPrefab.name:"敌方单位",EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();item.position=EditorGUILayout.Vector2IntField("变化后格点",item.position);
                    if(EditorGUI.EndChangeCheck()) {status=CheckEntityPlacement(item.position,Vector2Int.one,item.key,true);if(status==null) SaveEnemyChange(item);}
                    if(GUILayout.Button("撤销该单位的方案修改")) {Undo.RecordObject(ChangePlan,"还原敌方方案");ChangePlan.enemies.RemoveAll(e=>e.key==item.key);ChangedPlan();}
                }
            }
            else if(category==VoxelPaletteCategory.Objects||category==VoxelPaletteCategory.Terrain&&changeObjectKey!=null)
            {
                var item=EffectiveObjects().FirstOrDefault(e=>e.key==changeObjectKey)??ChangePlan.objects.Where(e=>e.key==changeObjectKey).Select(CopyObject).FirstOrDefault();
                if(item?.targetPrefab!=null)
                {
                    EditorGUILayout.LabelField(item.targetPrefab.name,EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();item.position=EditorGUILayout.Vector2IntField("变化后格点",item.position);
                    item.yaw=EditorGUILayout.IntPopup("朝向",item.yaw,new[]{"南","西","北","东"},new[]{0,90,180,270});
                    if(item.targetPrefab.Kind==PushBoxKind.Door&&item.targetPrefab.DoorRule==PushBoxDoorRule.PressurePlates)
                    {
                        item.overrideOpen=EditorGUILayout.Toggle("改写开关状态",item.overrideOpen);
                        if(item.overrideOpen||string.IsNullOrEmpty(item.existingId)) item.startsOpen=EditorGUILayout.Toggle("变化后开启",item.startsOpen);
                        item.overrideInputs=EditorGUILayout.Toggle("改写关联压力板",item.overrideInputs);
                        using(new EditorGUI.DisabledScope(!item.overrideInputs))
                            foreach(var plate in EffectiveObjects().Where(e=>e.targetPrefab?.Kind==PushBoxKind.Plate))
                            {
                                string id=string.IsNullOrEmpty(plate.existingId)?"plan:"+plate.key:plate.existingId;
                                bool on=item.inputs.Contains(id);bool next=EditorGUILayout.Toggle($"压力板 {plate.position}",on);
                                if(on!=next) {if(next) item.inputs.Add(id);else item.inputs.Remove(id);}
                            }
                    }
                    if(EditorGUI.EndChangeCheck()) {status=CheckEntityPlacement(item.position,item.targetPrefab.Footprint,item.key,ChangeBlocks(item));if(status==null) SaveObjectChange(item);}
                    if(GUILayout.Button("撤销该机关的方案修改")) {Undo.RecordObject(ChangePlan,"还原机关方案");ChangePlan.objects.RemoveAll(e=>e.key==item.key);ChangedPlan();}
                }
            }
            coordinate=EditorGUILayout.Vector2IntField("当前格点",coordinate);
            var cell=ChangeCell(coordinate);
            if(cell!=null) EditorGUILayout.LabelField("变化后",$"{(cell.kind==VoxelGroundKind.Void?"深渊":!cell.walkable?"石块":"可通行地块")} · 高度 {cell.height}");
            using(new EditorGUI.DisabledScope(!ChangePlan.tiles.Exists(e=>e.position==coordinate)&&!ChangePlan.exits.Contains(coordinate)))
                if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Refresh").image,"还原当前格点"),GUILayout.Height(26)))
                    OneAction(()=>{Undo.RecordObject(ChangePlan,"还原场地格点");ChangePlan.tiles.RemoveAll(e=>e.position==coordinate);ChangePlan.exits.Remove(coordinate);ChangedPlan();});
        }
        private void RefreshTerrainPreview()
        {
            if(!afterSummon) return;
            int version=ChangePlan!=null?EditorUtility.GetDirtyCount(ChangePlan):0;
            if(!terrainPreviewDirty&&ChangePlan==previewPlan&&version==previewPlanVersion) return;
            previewPlan=ChangePlan;previewPlanVersion=version;terrainPreviewDirty=false;terrainPreview??=new();
            try {terrainPreview.Build(context,ChangePlan);}catch(Exception error) {status=error.Message;}
        }
    }
}
