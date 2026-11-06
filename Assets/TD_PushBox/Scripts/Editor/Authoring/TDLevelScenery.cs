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
    public static class TDLevelScenery
    {
        private const string Root=TDProjectPaths.Root;
        private const float Floor=-0.28f;
        private sealed class Shelf
        {
            public Rect area;
            public float top;
            public int side;
            public bool outer;
        }

        public static void Build(VoxelTacticsLevel level,TDLevelTheme theme,int seed)
        {
            ValidateLevel(level,theme);
            if(level.EnvironmentContainer.childCount!=0) throw new InvalidOperationException("创建时仅对空环境容器生成布景。");
            var root=Generate(level,theme,seed,level.BoardSize);
            level.ConfigureScenery(theme,seed,level.FloraCoverage);level.AssignGeneratedScenery(root);
            VoxelLevelEditOperations.Record(level);FrameCamera(level);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }

        private static void ValidateLevel(VoxelTacticsLevel level,TDLevelTheme theme)
        {
            if(level==null||level.EnvironmentContainer==null||!Enum.IsDefined(typeof(TDLevelTheme),theme))
                throw new ArgumentException("无效的关卡或背景环境。");
            if(!level.gameObject.scene.IsValid()||level.EnvironmentContainer==level.transform||!level.EnvironmentContainer.IsChildOf(level.transform)
                ||level.EnvironmentContainer.gameObject.scene!=level.gameObject.scene)
                throw new InvalidOperationException("环境容器必须属于当前关卡，不能引用其他关卡或场景。");
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("运行期间不能刷新布景。");
        }

        private static Transform GeneratedRoot(VoxelTacticsLevel level)
        {
            var env=level.EnvironmentContainer;
            if(level.GeneratedScenery!=null)
            {
                if(level.GeneratedScenery.parent!=env) throw new InvalidOperationException("生成布景必须位于当前关卡的环境容器下。");
                return level.GeneratedScenery;
            }
            // Recognize scenes created before the generated-root reference was stored.
            var candidates=env.Cast<Transform>().Where(t=>TryLegacySettings(t,out _,out _)).ToArray();
            if(candidates.Length>1) throw new InvalidOperationException("存在多个旧生成布景分组，请先确认保留哪一个。");
            return candidates.SingleOrDefault();
        }

        private static bool TryLegacySettings(Transform root,out TDLevelTheme theme,out int seed)
        {
            theme=TDLevelTheme.Forest;seed=VoxelTacticsLevel.DefaultScenerySeed;
            int split=root.name.LastIndexOf('_');
            return split>0&&Enum.TryParse(root.name.Substring(0,split),out theme)&&Enum.IsDefined(typeof(TDLevelTheme),theme)
                &&int.TryParse(root.name.Substring(split+1),out seed)
                &&new[]{"Ground","Back","Sides","Front"}.All(name=>root.Find(name)!=null);
        }

        public static void ReadSettings(VoxelTacticsLevel level,out TDLevelTheme theme,out int seed,out int coverage)
        {
            theme=level.SceneryTheme;seed=level.ScenerySeed;coverage=level.FloraCoverage;
            if(level.SceneryConfigured||level.EnvironmentContainer==null) return;
            var root=GeneratedRoot(level);
            if(root!=null&&TryLegacySettings(root,out var oldTheme,out int oldSeed)) {theme=oldTheme;seed=oldSeed;}
        }

        public static bool NeedsLegacyReplacement(VoxelTacticsLevel level)=>
            level.EnvironmentContainer!=null&&GeneratedRoot(level)==null
                &&level.EnvironmentContainer.Cast<Transform>().Any(t=>t.name!=VoxelDetailScatter.LegacyGroupName);

        public static void Refresh(VoxelTacticsLevel level,VoxelLevelPalette palette,TDLevelTheme theme,int seed,bool replaceLegacy=false)
        {
            ValidateLevel(level,theme);
            if(EditorUtility.IsPersistent(level)||PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("请在场景编辑模式中刷新布景。");
            var edit=new VoxelLevelEditOperations(level,palette);
            var previous=GeneratedRoot(level);
            var old=previous!=null?new[]{previous}:level.EnvironmentContainer.Cast<Transform>().Where(t=>t.name!=VoxelDetailScatter.LegacyGroupName).ToArray();
            if(previous==null&&old.Length>0&&!replaceLegacy)
                throw new InvalidOperationException("现有环境不是工具生成的布景，需要确认后才能替换。");
            if(old.Any(t=>t.GetComponentsInChildren<MonoBehaviour>(true).Length>0))
                throw new InvalidOperationException("待刷新布景中含有玩法或自定义脚本，请先将这些对象移出环境分组。");

            Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("刷新关卡布景");
            Transform next=null;bool registered=false;
            try
            {
                next=Generate(level,theme,seed,edit.Size);
                Undo.RegisterCreatedObjectUndo(next.gameObject,"生成关卡布景");registered=true;
                Undo.RecordObject(level,"更新布景配置");
                level.ConfigureScenery(theme,seed,level.FloraCoverage);level.AssignGeneratedScenery(next);
                foreach(var root in old) Undo.DestroyObjectImmediate(root.gameObject);
                VoxelLevelEditOperations.Record(level);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
                Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            }
            catch
            {
                if(next!=null&&!registered) Object.DestroyImmediate(next.gameObject);
                Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(group);throw;
            }
        }

        private static Transform Generate(VoxelTacticsLevel level,TDLevelTheme theme,int seed,Vector2Int size)
        {
            if(size.x<1||size.y<1) throw new ArgumentException("棋盘尺寸无效。");
            var stone=Mat("Stone");var light=Mat("StoneLight");var dark=Mat("StoneDark");
            var soil=Mat("WoodDark");var grass=Mat("Grass");var grassLight=Mat("GrassLight");
            var land=theme==TDLevelTheme.Forest?Mat("Ground"):dark;
            var tree=Prefab("Scenery_Tree");var rock=Prefab("Scenery_Rock");var flora=Prefab("Detail_Flora");var ruin=Prefab("Scenery_Ruin");
            GameObject wall=null,tower=null,banner=null;
            if(theme==TDLevelTheme.Castle)
            {
                var cloth=TDProjectPaths.Load<Material>(Root+"/Art/Materials/MAT_Warrior_Cloth.mat");
                if(cloth==null) throw new InvalidOperationException("缺少城堡旗帜材质。");
                var materials=new[]{stone,light,dark,soil,cloth};
                wall=CastlePart("Wall",materials,b=>
                {
                    b.Box(new Vector3(0,0.12f,0),new Vector3(2.1f,0.24f,0.64f),2);
                    b.Box(new Vector3(0,0.7f,0),new Vector3(2,1.16f,0.44f),0);
                    b.Box(new Vector3(0,1.23f,0),new Vector3(2.12f,0.14f,0.57f),1);
                    for(int i=0;i<4;i++) b.Box(new Vector3(-0.81f+i*0.54f,1.43f,0),new Vector3(0.35f,0.30f,0.53f),0);
                });
                tower=CastlePart("Tower",materials,b=>
                {
                    b.Box(new Vector3(0,0.13f,0),new Vector3(1.45f,0.26f,1.45f),2);
                    b.Box(new Vector3(0,1.15f,0),new Vector3(1.17f,2.1f,1.17f),0);
                    b.Box(new Vector3(0,2.2f,0),new Vector3(1.43f,0.2f,1.43f),1);
                    foreach(float x in new[]{-0.51f,0.51f}) foreach(float z in new[]{-0.51f,0.51f})
                        b.Box(new Vector3(x,2.49f,z),new Vector3(0.38f,0.4f,0.38f),0);
                    b.Box(new Vector3(0,1.43f,-0.594f),new Vector3(0.13f,0.52f,0.025f),2);
                });
                banner=CastlePart("Banner",materials,b=>
                {
                    b.Box(new Vector3(0,1.12f,0),new Vector3(0.07f,2.24f,0.07f),3);
                    b.Box(new Vector3(0,2.28f,0),new Vector3(0.14f,0.14f,0.14f),1);
                    b.Box(new Vector3(0.38f,1.78f,0),new Vector3(0.7f,0.66f,0.045f),4);
                    b.Box(new Vector3(0.14f,1.38f,0),new Vector3(0.22f,0.15f,0.045f),4);
                    b.Box(new Vector3(0.61f,1.38f,0),new Vector3(0.23f,0.15f,0.045f),4);
                    b.Box(new Vector3(0.38f,1.8f,-0.026f),new Vector3(0.09f,0.4f,0.012f),1);
                });
            }

            var env=new GameObject(theme+"_"+seed).transform;
            SceneManager.MoveGameObjectToScene(env.gameObject,level.gameObject.scene);env.SetParent(level.EnvironmentContainer,false);
            try
            {
                env.localPosition=level.GridOrigin+new Vector3((size.x-1)*level.CellSize/2,0,(size.y-1)*level.CellSize/2);
                var ground=Group(env,"Ground");var back=Group(env,"Back");var sides=Group(env,"Sides");var front=Group(env,"Front");
                var random=new System.Random(seed);float Range(float min,float max)=>min+(max-min)*(float)random.NextDouble();
                float hx=size.x*level.CellSize/2,hz=size.y*level.CellSize/2;
                Block(ground,"Base",new Vector3(0,-0.94f,0),new Vector3((hx+0.16f)*2,1.0f,(hz+0.16f)*2),land);
                var shelves=BuildGround(ground,hx,hz,random,theme==TDLevelTheme.Forest?soil:dark,
                    theme==TDLevelTheme.Forest?new[]{grass,grass,grassLight,land}:new[]{stone,stone,light,dark});
                foreach(float sign in new[]{-1f,1f})
                {
                    var footing=theme==TDLevelTheme.Forest?soil:dark;
                    Block(ground,sign<0?"Foot_L":"Foot_R",new Vector3(sign*(hx+0.08f),-0.55f,0),new Vector3(0.16f,0.66f,hz*2),footing);
                    Block(ground,sign<0?"Foot_F":"Foot_B",new Vector3(0,-0.55f,sign*(hz+0.08f)),new Vector3((hx+0.16f)*2,0.66f,0.16f),footing);
                    Block(ground,sign<0?"Rim_L":"Rim_R",new Vector3(sign*(hx+0.08f),-0.11f,0),new Vector3(0.16f,0.22f,hz*2),dark);
                    Block(ground,sign<0?"Rim_F":"Rim_B",new Vector3(0,-0.11f,sign*(hz+0.08f)),new Vector3((hx+0.16f)*2,0.22f,0.16f),dark);
                }
                int index=0;
                Transform Prop(GameObject source,string name,Vector3 p,Vector3 scale,float yaw,Transform parent,Shelf shelf)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
                    if(go.GetComponentsInChildren<MonoBehaviour>(true).Length>0||go.GetComponentsInChildren<Collider>(true).Length>0)
                    {
                        PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
                        foreach(var collider in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                        foreach(var node in go.GetComponentsInChildren<Transform>(true)
                            .Where(node=>node.name=="Health Anchor"||node.name=="Selection Marker").ToArray()) Object.DestroyImmediate(node.gameObject);
                    }
                    go.name=name+"_"+(index++).ToString("00");go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=scale;
                    var bounds=LocalBounds(go,env);
                    float fit=Mathf.Min(1,(shelf.area.width-0.2f)/Mathf.Max(0.01f,bounds.size.x),(shelf.area.height-0.2f)/Mathf.Max(0.01f,bounds.size.z));
                    if(fit<0.45f) {Object.DestroyImmediate(go);return null;}
                    if(fit<1) {go.transform.localScale*=fit;bounds=LocalBounds(go,env);}
                    var clamped=new Vector3(
                        Mathf.Clamp(bounds.center.x,shelf.area.xMin+bounds.extents.x+0.1f,shelf.area.xMax-bounds.extents.x-0.1f),
                        bounds.center.y,
                        Mathf.Clamp(bounds.center.z,shelf.area.yMin+bounds.extents.z+0.1f,shelf.area.yMax-bounds.extents.z-0.1f));
                    go.transform.localPosition+=clamped-bounds.center+Vector3.up*(shelf.top-bounds.min.y);
                    // Fit both the model and its footing, not just its pivot, to the generated shelf.
                    if(OverlapsBoard(go,env,hx,hz)) {Object.DestroyImmediate(go);return null;}
                    foreach(var node in go.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=0;
                    VoxelLevelEditOperations.Record(go);VoxelLevelEditOperations.Record(go.transform);return go.transform;
                }
                var landmark=shelves.Where(shelf=>shelf.side==1&&!shelf.outer).OrderByDescending(shelf=>shelf.area.width).First();
                foreach(var shelf in shelves)
                {
                    bool near=shelf.side==0;
                    var parent=near?front:shelf.side==1?back:sides;
                    var p=new Vector3(shelf.area.center.x,shelf.top,shelf.area.center.y);
                    if(shelf.outer)
                    {
                        if(Range(0,1)<0.7f) Prop(theme==TDLevelTheme.Forest?flora:rock,"Detail",p,Vector3.one*Range(0.35f,0.65f),Range(0,360),parent,shelf);
                        continue;
                    }
                    if(theme==TDLevelTheme.Forest)
                    {
                        if(!near)
                        {
                            bool oldRuin=Range(0,1)<0.16f;
                            Prop(oldRuin?ruin:tree,oldRuin?"Ruin":"Tree",p+new Vector3(Range(-0.3f,0.3f),0,Range(-0.2f,0.2f)),
                                Vector3.one*Range(1.0f,1.55f),Range(-35,35),parent,shelf);
                        }
                        else Prop(rock,"Rock",p,Vector3.one*Range(0.4f,0.8f),Range(0,360),parent,shelf);
                        Prop(flora,"Flora",p+new Vector3(Range(-0.6f,0.6f),0,Range(-0.6f,0.6f)),Vector3.one*Range(0.65f,1.1f),Range(0,360),parent,shelf);
                    }
                    else
                    {
                        float choice=Range(0,1);float yaw=Range(-14,14);
                        bool isTower=!near&&(shelf==landmark||choice>0.67f&&choice<0.87f);
                        if(isTower)
                        {
                            Prop(tower,"Tower",p+Vector3.forward*0.25f,Vector3.one*Range(0.8f,1),yaw,parent,shelf);
                            if(shelf.area.width>2.1f&&shelf.area.height>2.4f&&Range(0,1)<0.7f)
                                Prop(banner,"Banner",p+Quaternion.Euler(0,yaw,0)*new Vector3(-0.35f,0,-0.7f),Vector3.one*0.85f,yaw,parent,shelf);
                        }
                        else if(choice<0.67f)
                        {
                            bool alongX=shelf.side<2;
                            float length=alongX?shelf.area.width:shelf.area.height;
                            int count=length>3.6f&&Range(0,1)<0.5f?2:1;
                            for(int i=0;i<count;i++)
                            {
                                var offset=(alongX?Vector3.right:Vector3.forward)*((i-(count-1)*0.5f)*1.75f);
                                Prop(wall,"Wall",p+offset,new Vector3(Range(0.65f,0.95f),near?Range(0.18f,0.32f):Range(0.5f,1),1),
                                    (alongX?0:90)+Range(-14,14),parent,shelf);
                            }
                        }
                        else if(!near) Prop(ruin,"Ruin",p,Vector3.one*Range(0.8f,1.1f),Range(-25,25),parent,shelf);
                        Prop(rock,"Rubble",p+new Vector3(Range(-0.65f,0.65f),0,Range(-0.55f,0.55f)),Vector3.one*Range(0.35f,0.65f),Range(0,360),parent,shelf);
                        if(Range(0,1)<0.3f&&shelf.area.width>1.6f&&shelf.area.height>1.6f)
                            Block(parent,"Paver",p+new Vector3(Range(-0.2f,0.2f),0.03f,Range(-0.2f,0.2f)),new Vector3(Range(0.4f,0.7f),0.06f,Range(0.25f,0.5f)),light,Range(-35,35));
                    }
                }
                return env;
            }
            catch {Object.DestroyImmediate(env.gameObject);throw;}
        }

        private static Material Mat(string name)=>TDProjectPaths.Load<Material>(Root+"/Art/Materials/StartMenu/"+name+".mat")
            ??throw new InvalidOperationException("缺少布景材质："+name);
        private static GameObject Prefab(string name)=>TDProjectPaths.Load<GameObject>(Root+"/Prefabs/"+name+".prefab")
            ??throw new InvalidOperationException("缺少布景资源："+name);
        private static Transform Group(Transform parent,string name)
        {var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);go.transform.SetParent(parent,false);return go.transform;}
        private static void Block(Transform parent,string name,Vector3 p,Vector3 size,Material material,float yaw=0)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;SceneManager.MoveGameObjectToScene(go,parent.gameObject.scene);go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localScale=size;go.transform.localRotation=Quaternion.Euler(0,yaw,0);Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        private static List<Shelf> BuildGround(Transform parent,float hx,float hz,System.Random random,Material body,Material[] tops)
        {
            var result=new List<Shelf>();
            float Range(float min,float max)=>min+(max-min)*(float)random.NextDouble();
            void Add(int side,float t,float length,float offset,float depth,float top,bool outer)
            {
                bool alongX=side<2;float edge=(alongX?hz:hx)+0.12f+offset+depth/2;
                var center=alongX?new Vector2(t,(side==0?-1:1)*edge):new Vector2((side==2?-1:1)*edge,t);
                var size=alongX?new Vector2(length,depth):new Vector2(depth,length);
                var shelf=new Shelf{area=new Rect(center-size/2,size),top=top,side=side,outer=outer};
                int index=result.Count;float bottom=-1.1f-Range(0,0.15f);
                Block(parent,"Land_"+index,new Vector3(center.x,(top-0.03f+bottom)/2,center.y),new Vector3(size.x,top-0.03f-bottom,size.y),body);
                Block(parent,"Top_"+index,new Vector3(center.x,top-0.015f,center.y),new Vector3(size.x,0.03f,size.y),tops[random.Next(tops.Length)]);
                result.Add(shelf);
            }
            for(int side=0;side<4;side++)
            {
                float length=Mathf.Max(3.6f,(side<2?hx:hz)*2+(side<2?0:Range(0.5f,1.3f)));
                int count=Mathf.Clamp(Mathf.CeilToInt(length/3.8f),2,28);
                var weights=new float[count];float total=0;
                for(int i=0;i<count;i++) {weights[i]=i==0?Range(0.65f,0.95f):i==1?Range(1.2f,1.65f):Range(0.65f,1.65f);total+=weights[i];}
                float cursor=-length/2+Range(-0.25f,0.25f);
                for(int i=0;i<count;i++)
                {
                    float span=length*weights[i]/total,center=cursor+span/2;
                    float depth=Range(1.6f,3.8f),top=Floor+Range(-0.22f,0.04f);
                    Add(side,center,span,0,depth,top,false);
                    if(random.NextDouble()<0.5)
                        Add(side,center+Range(-0.18f,0.18f)*span,span*Range(0.35f,0.6f),depth,Range(0.75f,1.4f),top-Range(0.1f,0.22f),true);
                    cursor+=span;
                }
            }
            return result;
        }
        private static Bounds LocalBounds(GameObject go,Transform space)
        {
            var result=new Bounds(space.InverseTransformPoint(go.transform.position),Vector3.zero);
            bool first=true;
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    var point=space.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if(first) {result=new Bounds(point,Vector3.zero);first=false;} else result.Encapsulate(point);
                }
            }
            return result;
        }
        private static bool OverlapsBoard(GameObject go,Transform space,float hx,float hz)
        {
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;var local=new Bounds(space.InverseTransformPoint(bounds.center),Vector3.zero);
                for(int i=0;i<8;i++) local.Encapsulate(space.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                if(local.min.x<hx+0.2f&&local.max.x>-hx-0.2f&&local.min.z<hz+0.2f&&local.max.z>-hz-0.2f) return true;
            }
            return false;
        }
        private static GameObject CastlePart(string name,Material[] materials,Action<VoxelModelBuilder> build)
        {
            string path=Root+"/Prefabs/Environment/Env_"+name+".prefab";
            var prefab=TDProjectPaths.Load<GameObject>(path);if(prefab!=null) return prefab;
            string meshPath=Root+"/Art/Meshes/Env_"+name+".asset";
            var mesh=TDProjectPaths.Load<Mesh>(meshPath);
            if(mesh==null) {var builder=new VoxelModelBuilder();build(builder);mesh=builder.Mesh(name);TDProjectPaths.CreateAsset(mesh,meshPath);}
            var root=new GameObject(name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterials=materials;
                return TDProjectPaths.SaveAsPrefabAsset(root,path);
            }
            finally {Object.DestroyImmediate(root);}
        }
        private static void FrameCamera(VoxelTacticsLevel level)
        {
            var camera=level.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PushBoxCamera>()).FirstOrDefault();
            if(camera==null) return;
            float halfWidth=level.BoardSize.x*level.CellSize/2+1.5f,halfDepth=level.BoardSize.y*level.CellSize/2+1;
            float tan=Mathf.Tan(25*Mathf.Deg2Rad);
            float fit=Mathf.Max(halfWidth/(tan*16/9),halfDepth*Mathf.Sin(60*Mathf.Deg2Rad)/tan);
            var center=level.Surface(Vector2Int.zero,0)+level.transform.TransformVector(new Vector3((level.BoardSize.x-1)*level.CellSize/2,0,(level.BoardSize.y-1)*level.CellSize/2));
            camera.ConfigurePerspective(center,Mathf.Max(10,fit*1.08f+halfDepth*0.5f));
        }
    }
}
