using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public readonly struct LevelValidationIssue
    {
        public readonly UnityEngine.Object target;
        public readonly string message;
        public LevelValidationIssue(UnityEngine.Object owner,string text) {target=owner;message=text;}
        public override string ToString()=>target!=null?target.name+"： "+message:message;
    }

    public static class PushBoxLevelValidation
    {
        private const float PositionTolerance=0.015f;
        public static IEnumerable<Component> Entities(VoxelTacticsLevel level)=>
            level.GetComponentsInChildren<PushBoxActor>().Cast<Component>().Concat(level.GetComponentsInChildren<PushBoxObject>());

        public static int SceneHeight(VoxelTacticsLevel level,Transform body)=>
            Mathf.RoundToInt((level.transform.InverseTransformPoint(body.position).y-level.GridOrigin.y)/level.HeightStep);

        public static PushBoxState ReadTerrain(VoxelTacticsLevel level,bool scenePositions=false)
        {
            var state=new PushBoxState{size=level.BoardSize};
            foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>())
            {
                var source=tile.CreateState();
                var point=scenePositions?level.GridPoint(tile.transform.position):source.position;
                var cell=new PushBoxCell{position=point,height=scenePositions?SceneHeight(level,tile.transform):source.height,
                    uphill=source.Uphill,supports=source.kind!=VoxelGroundKind.Void,walkable=source.walkable,blocksSight=source.blocksSight};
                if(!state.cells.TryAdd(point,cell)) throw new InvalidOperationException("地块坐标重复："+point);
            }
            if(state.size.x<=0||state.size.y<=0)
                state.size=state.cells.Count==0?Vector2Int.zero:new Vector2Int(state.cells.Keys.Max(p=>p.x)+1,state.cells.Keys.Max(p=>p.y)+1);
            return state;
        }

        public static PushBoxState ReadState(VoxelTacticsLevel level,bool scenePositions=false)
        {
            var state=ReadTerrain(level,scenePositions);
            var runtime=level.GetComponent<PushBoxLevel>();
            foreach(var owner in Entities(level))
            {
                var entity=owner is PushBoxActor actor?actor.CreateState(runtime):((PushBoxObject)owner).CreateState();
                if(scenePositions) entity.position=level.GridPoint(owner.transform.position);
                state.entities.Add(entity);
            }
            return state;
        }

        public static List<LevelValidationIssue> Check(VoxelTacticsLevel level)
        {
            var issues=new List<LevelValidationIssue>();
            if(level==null) {issues.Add(new LevelValidationIssue(null,"缺少关卡根节点。"));return issues;}
            if(!level.gameObject.scene.IsValid()) {issues.Add(new LevelValidationIssue(level,"请检查场景中的关卡实例。"));return issues;}
            void Error(UnityEngine.Object owner,string text)=>issues.Add(new LevelValidationIssue(owner,text));
            if(level.GetComponent<PushBoxLevel>()==null||level.GetComponent<PushBoxController>()==null)
                Error(level,"缺少推箱子关卡或控制器组件。");
            var groups=new[]{level.TerrainContainer,level.ActorsContainer,level.ObjectsContainer,level.EnvironmentContainer,level.DetailsContainer,level.RuntimeContainer};
            if(groups.Any(group=>group==null||!group.IsChildOf(level.transform))||groups.Distinct().Count()!=6) Error(level,"六个容器必须完整、互不重复，并属于当前关卡。");
            if(level.CellSize<=0||level.HeightStep<=0) {Error(level,"格距与层高必须大于零。");return issues;}
            var entities=Entities(level).ToArray();var ids=new HashSet<string>();
            if(!entities.Any(owner=>owner is PushBoxActor)) Error(level,"没有可操作角色。制作中关卡可以保存，但请先放置角色再进入 Play。");
            foreach(var owner in entities)
            {
                string id=owner is PushBoxActor actor?actor.StableId:((PushBoxObject)owner).StableId;
                if(string.IsNullOrWhiteSpace(id)||!ids.Add(id)) Error(owner,"标识缺失或重复，请使用“修复缺失或重复标识”。");
                if(owner.GetComponents<PushBoxActor>().Length+owner.GetComponents<PushBoxObject>().Length!=1)
                    Error(owner,"一个物体只能拥有一个玩法实体组件。");
                if(owner.transform.parent!=null&&owner.transform.parent.GetComponentInParent<VoxelTacticsTile>()!=null)
                    Error(owner,"角色和机关不能放在地块节点下面。");
                CheckReferences(owner,Error);
            }
            PushBoxState state;
            try {state=ReadTerrain(level);}
            catch(Exception error) {Error(level,error.Message);return issues;}
            if(state.cells.Count==0) Error(level,"没有可用地块。");
            foreach(var cell in state.cells.Values) if(!state.Inside(cell.position)) Error(level,"地块越界："+cell.position);
            foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>())
            {
                if(tile.Definition==null) Error(tile,"缺少地块定义。");
                CheckPosition(tile,level.Surface(tile.GridPosition,tile.HeightLevel),Error);
            }
            foreach(var owner in entities)
            {
                var point=owner is PushBoxActor actor?actor.GridPosition:((PushBoxObject)owner).GridPosition;
                if(!state.cells.TryGetValue(point,out var cell)||!cell.supports||!cell.walkable)
                {Error(owner,"格点不在有效地面上："+point);continue;}
                float height=cell.height+(cell.uphill==Vector2Int.zero?0:0.5f);
                var expected=level.transform.TransformPoint(level.GridOrigin+new Vector3(point.x*level.CellSize,height*level.HeightStep,point.y*level.CellSize));
                CheckPosition(owner,expected,Error);
            }
            try {PushBoxRules.Validate(ReadState(level));}
            catch(Exception error) {Error(level,error.Message);}
            if(!level.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>()).Any(camera=>camera.enabled&&camera.CompareTag("MainCamera")))
                Error(level,"场景缺少启用的 MainCamera。");
            if(level.TerrainChange!=null)
                try {VoxelTerrainPlanValidation.Validate(level,level.TerrainChange);}
                catch(Exception error) {Error(level.TerrainChange,error.Message);}
            return issues;
        }

        private static void CheckPosition(Component owner,Vector3 expected,Action<UnityEngine.Object,string> error)
        {
            if(Vector3.Distance(owner.transform.position,expected)>PositionTolerance)
                error(owner,"场景位置与格点不一致。请选择“按格点对齐场景”或“按场景位置更新格点”。");
        }

        private static void CheckReferences(Component owner,Action<UnityEngine.Object,string> error)
        {
            if(!(owner is PushBoxObject item)||item.Kind!=PushBoxKind.Door) return;
            var data=new SerializedObject(item);
            if(data.FindProperty("movingPart").objectReferenceValue==null) error(item,"门缺少活动部件。");
            if(item.DoorRule!=PushBoxDoorRule.AllEnemiesDefeated) return;
            var visual=item.GetComponent<EnemySealDoorVisual>();
            if(visual==null) {error(item,"封印石门缺少 EnemySealDoorVisual。");return;}
            var view=new SerializedObject(visual);
            if(view.FindProperty("slab").objectReferenceValue==null||view.FindProperty("seal").objectReferenceValue==null)
                error(item,"封印石门缺少门体或封印渲染器引用。");
        }

        public static void Report(IEnumerable<LevelValidationIssue> issues,string prefix="")
        {
            foreach(var issue in issues) Debug.LogError(prefix+issue,issue.target);
        }
    }
}
