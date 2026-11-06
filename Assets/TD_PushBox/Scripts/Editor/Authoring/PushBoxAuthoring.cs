using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class PushBoxAuthoring
    {
        public const string PalettePath=TDProjectPaths.Root+"/Data/Authoring/PushBoxPalette.asset";
        public const string Prefabs=TDProjectPaths.Root+"/Prefabs/PushBox";
        static PushBoxAuthoring()
        {
            EditorSceneManager.sceneSaving+=(scene,path)=>BakeScene(scene);
            EditorApplication.playModeStateChanged+=change=>
            {
                if(change==PlayModeStateChange.EnteredEditMode) {UpgradeOpenScenes();return;}
                if(change!=PlayModeStateChange.ExitingEditMode) return;
                PreparePlay();
            };
            EditorApplication.delayCall+=UpgradeOpenScenes;
        }

        public static VoxelLevelPalette Palette()
        {
            var palette=TDProjectPaths.Load<VoxelLevelPalette>(PalettePath);
            if(palette==null) throw new InvalidOperationException("缺少推箱子资源目录，请从备份恢复 PushBoxPalette。");
            return palette;
        }
        private static void UpgradeOpenScenes()
        {
            if(TDProjectValidation.IsRunning||Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||AssetDatabase.IsAssetImportWorkerProcess()) return;
            foreach(var level in Object.FindObjectsByType<VoxelTacticsLevel>(FindObjectsSortMode.None))
                if(level.GetComponent<PushBoxLevel>()==null&&level.gameObject.scene.path.StartsWith(TDProjectPaths.Scenes+"/",StringComparison.Ordinal))
                    InitializeLevel(level);
        }
        public static void InitializeLevel(VoxelTacticsLevel level)
        {
            if(level.GetComponent<PushBoxLevel>()==null) level.gameObject.AddComponent<PushBoxLevel>();
            if(level.GetComponent<PushBoxController>()==null) level.gameObject.AddComponent<PushBoxController>();
            Bake(level);ConfigureCamera(level);
            EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }

        public static void Bake(VoxelTacticsLevel level)
        {
            var puzzle=level.GetComponent<PushBoxLevel>();if(puzzle==null) return;
            var terrain=PushBoxLevelValidation.ReadTerrain(level);
            string before=EditorJsonUtility.ToJson(puzzle);
            puzzle.SetLayout(terrain.size,level.GridOrigin,level.CellSize,level.HeightStep,terrain.cells.Values);
            if(before!=EditorJsonUtility.ToJson(puzzle)) {EditorUtility.SetDirty(puzzle);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);}
        }

        private static void BakeScene(Scene scene)
        {
            if(TDProjectValidation.IsRunning) return;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var level in root.GetComponentsInChildren<VoxelTacticsLevel>(true))
                    if(level.GetComponent<PushBoxLevel>()!=null)
                        try {Bake(level);}
                        catch(Exception error) {Debug.LogWarning("草稿已保留，地形尚不能烘焙："+error.Message,level);}
        }

        public static bool PreparePlay()
        {
            var levels=Object.FindObjectsByType<VoxelTacticsLevel>(FindObjectsSortMode.None)
                .Where(level=>!EditorSceneManager.IsPreviewScene(level.gameObject.scene)).ToArray();
            var issues=levels.SelectMany(PushBoxLevelValidation.Check).ToList();
            if(issues.Count>0)
            {
                PushBoxLevelValidation.Report(issues,"进入 Play 前检查：");
                EditorApplication.isPlaying=false;
                return false;
            }
            foreach(var level in levels) Bake(level);
            return true;
        }

        public static void ConfigureCamera(VoxelTacticsLevel level)
        {
            var camera=level.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if(camera==null) return;
            var control=camera.GetComponent<PushBoxCamera>()??camera.gameObject.AddComponent<PushBoxCamera>();
            var size=level.GetComponent<PushBoxLevel>().Size;
            Vector3 center=level.Surface(Vector2Int.zero,0)+level.transform.TransformVector(new Vector3((size.x-1)*level.CellSize/2,0,(size.y-1)*level.CellSize/2));
            const float fov=50;
            float halfDepth=size.y*level.CellSize/2,halfWidth=size.x*level.CellSize/2;
            float tangent=Mathf.Tan(fov*0.5f*Mathf.Deg2Rad);
            float fit=Mathf.Max(halfDepth*Mathf.Sin(60*Mathf.Deg2Rad)/tangent,halfWidth/(tangent*(16f/9f)));
            control.ConfigurePerspective(center,Mathf.Max(6,fit*1.08f+halfDepth*Mathf.Cos(60*Mathf.Deg2Rad)),fov);
            VoxelLevelEditOperations.Record(camera.transform);EditorUtility.SetDirty(control);EditorUtility.SetDirty(camera);
        }

        public static void SnapLayout(VoxelTacticsLevel level)
        {
            var state=PushBoxLevelValidation.ReadState(level);PushBoxRules.Validate(state);
            EditLayout(level,"按格点对齐场景",()=>
            {
                foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>()) tile.transform.position=level.Surface(tile.GridPosition,tile.HeightLevel);
                foreach(var owner in PushBoxLevelValidation.Entities(level)) owner.transform.position=Footing(level,state,PushBoxEditOperations.Position(owner.gameObject));
            });
        }

        public static void UpdateGridFromScene(VoxelTacticsLevel level)
        {
            var state=PushBoxLevelValidation.ReadState(level,true);PushBoxRules.Validate(state);
            var placements=PushBoxLevelValidation.Entities(level).ToDictionary(owner=>owner,owner=>level.GridPoint(owner.transform.position));
            EditLayout(level,"按场景位置更新格点",()=>
            {
                foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>())
                {
                    var p=level.GridPoint(tile.transform.position);int height=PushBoxLevelValidation.SceneHeight(level,tile.transform);
                    tile.ConfigureDefinition(tile.Definition,p,height);tile.transform.position=level.Surface(p,height);
                }
                foreach(var pair in placements)
                {
                    if(pair.Key is PushBoxActor actor) actor.SetPlacement(pair.Value,false);else ((PushBoxObject)pair.Key).SetPlacement(pair.Value,false);
                    pair.Key.transform.position=Footing(level,state,pair.Value);
                }
                var edit=new VoxelLevelEditOperations(level,Palette());
                foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>()) edit.SyncSupport(tile);
            });
        }

        private static Vector3 Footing(VoxelTacticsLevel level,PushBoxState state,Vector2Int p)
        {
            var cell=state.cells[p];float height=cell.height+(cell.uphill==Vector2Int.zero?0:0.5f);
            return level.transform.TransformPoint(level.GridOrigin+new Vector3(p.x*level.CellSize,height*level.HeightStep,p.y*level.CellSize));
        }

        private static void EditLayout(VoxelTacticsLevel level,string label,Action edit)
        {
            if(Application.isPlaying||EditorUtility.IsPersistent(level)) throw new InvalidOperationException("请在场景编辑模式操作。");
            var targets=level.GetComponentsInChildren<VoxelTacticsTile>().Cast<Component>().Concat(PushBoxLevelValidation.Entities(level)).ToArray();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(label);
            Undo.RecordObjects(targets.Cast<Object>().Concat(targets.Select(owner=>(Object)owner.transform)).Append(level.GetComponent<PushBoxLevel>()).ToArray(),label);
            try
            {
                edit();Bake(level);
                foreach(var owner in targets) {VoxelLevelEditOperations.Record(owner);VoxelLevelEditOperations.Record(owner.transform);}
                EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            }
            catch {Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);throw;}
        }

        public static void RepairIds(VoxelTacticsLevel level)
        {
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("修复缺失或重复标识");
            var used=new HashSet<string>();
            foreach(var actor in level.GetComponentsInChildren<PushBoxActor>(true))
                if(string.IsNullOrWhiteSpace(actor.StableId)||!used.Add(actor.StableId))
                {Undo.RecordObject(actor,"修复推箱子标识");actor.SetPlacement(actor.GridPosition,true);used.Add(actor.StableId);VoxelLevelEditOperations.Record(actor);}
            foreach(var item in level.GetComponentsInChildren<PushBoxObject>(true))
                if(string.IsNullOrWhiteSpace(item.StableId)||!used.Add(item.StableId))
                {Undo.RecordObject(item,"修复推箱子标识");item.SetPlacement(item.GridPosition,true);used.Add(item.StableId);VoxelLevelEditOperations.Record(item);}
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }
    }
}
