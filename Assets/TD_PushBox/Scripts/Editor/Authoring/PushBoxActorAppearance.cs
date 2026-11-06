using System;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class PushBoxActorAppearance
    {
        public const string EyesName="Facing Eyes";
        public const string HairName="BackHair";
        static PushBoxActorAppearance()
        {
            EditorApplication.delayCall+=Refresh;
            EditorSceneManager.sceneOpened+=(scene,mode)=>UpdateScene(scene);
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode) Refresh();};
        }
        private static void Refresh()
        {
            if(TDProjectValidation.IsRunning||Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||AssetDatabase.IsAssetImportWorkerProcess()) return;
            if(EditorApplication.isUpdating||EditorApplication.isCompiling||Undo.isProcessing)
            {EditorApplication.delayCall+=Refresh;return;}
            var stage=PrefabStageUtility.GetCurrentPrefabStage();
            foreach(string role in new[]{"Warrior","Thief","Mage","Shieldbearer"})
            {
                string path=PushBoxAuthoring.Prefabs+"/Players/"+role+".prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null) continue;
                if(stage!=null&&stage.assetPath==path)
                {
                    var actor=stage.prefabContentsRoot.GetComponent<PushBoxActor>();
                    if(actor!=null&&EnsureDetails(actor.Visual,actor.Role,true)) EditorSceneManager.MarkSceneDirty(stage.scene);
                    continue;
                }
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var actor=root.GetComponent<PushBoxActor>();
                    if(actor!=null&&EnsureDetails(actor.Visual,actor.Role,false)) PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally {PrefabUtility.UnloadPrefabContents(root);}
            }
            for(int i=0;i<SceneManager.sceneCount;i++) UpdateScene(SceneManager.GetSceneAt(i));
        }
        public static bool EnsureDetails(Transform visual,PushBoxRole role,bool recordUndo)
        {
            bool changed=false;
            if(role==PushBoxRole.Warrior&&visual!=null)
            {
                const string path=TDProjectPaths.Root+"/Art/Meshes/Hero_Fighter.asset";
                var mesh=TDProjectPaths.Load<Mesh>(path);
                if(mesh==null) {mesh=VoxelModelBuilder.Hero("Warrior",true);mesh.name="Fighter";TDProjectPaths.CreateAsset(mesh,path);}
                var filter=visual.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=mesh)
                {
                    if(recordUndo) {Undo.RecordObject(filter,"更新力士模型");Undo.RecordObject(visual,"调整力士尺寸");}
                    filter.sharedMesh=mesh;visual.localScale=new Vector3(1.10f/1.16f,1.08f/1.12f,1.10f/1.16f);
                    VoxelLevelEditOperations.Record(filter);VoxelLevelEditOperations.Record(visual);changed=true;
                }
            }
            changed|=EnsureEyes(visual,role,recordUndo);
            changed|=EnsureBackHair(visual,role,recordUndo);
            return changed;
        }
        public static bool EnsureBackHair(Transform visual,PushBoxRole role,bool recordUndo)
        {
            if(visual==null||visual.Find(HairName)!=null) return false;
            var material=TDProjectPaths.Load<Material>(TDProjectPaths.Root+"/Art/Materials/MAT_Hero_Leather.mat");
            if(material==null) throw new InvalidOperationException("缺少角色头发使用的深色材质。");
            float width=role==PushBoxRole.Warrior?1.16f:role==PushBoxRole.Shield?1.10f:1;
            float height=role==PushBoxRole.Warrior?1.12f:role==PushBoxRole.Shield?1.08f:1;
            var hair=GameObject.CreatePrimitive(PrimitiveType.Cube);hair.name=HairName;
            SceneManager.MoveGameObjectToScene(hair,visual.gameObject.scene);
            if(recordUndo)
            {
                Undo.RegisterCreatedObjectUndo(hair,"添加后脑头发");Undo.DestroyObjectImmediate(hair.GetComponent<Collider>());
                Undo.SetTransformParent(hair.transform,visual,"添加后脑头发");Undo.RecordObject(hair.transform,"摆放后脑头发");
            }
            else {Object.DestroyImmediate(hair.GetComponent<Collider>());hair.transform.SetParent(visual,false);}
            hair.layer=visual.gameObject.layer;
            hair.transform.localPosition=new Vector3(0,1.19f*height,0.219f*width);
            hair.transform.localRotation=Quaternion.identity;hair.transform.localScale=new Vector3(0.456f*width,0.36f*height,0.036f*width);
            var renderer=hair.GetComponent<MeshRenderer>();if(recordUndo) Undo.RecordObject(renderer,"设置后脑头发");
            renderer.sharedMaterial=material;
            if(recordUndo) {VoxelLevelEditOperations.Record(hair.transform);VoxelLevelEditOperations.Record(renderer);}
            return true;
        }
        public static bool EnsureEyes(Transform visual,PushBoxRole role,bool recordUndo)
        {
            if(visual==null||visual.Find(EyesName)!=null) return false;
            var material=TDProjectPaths.Load<Material>(TDProjectPaths.Root+"/Art/Materials/MAT_Hero_Leather.mat");
            if(material==null) throw new InvalidOperationException("缺少角色眼睛使用的深色材质。");
            float width=role==PushBoxRole.Warrior?1.16f:role==PushBoxRole.Shield?1.10f:1;
            float height=role==PushBoxRole.Warrior?1.12f:role==PushBoxRole.Shield?1.08f:1;
            var group=new GameObject(EyesName);SceneManager.MoveGameObjectToScene(group,visual.gameObject.scene);
            if(recordUndo) {Undo.RegisterCreatedObjectUndo(group,"添加角色眼睛");Undo.SetTransformParent(group.transform,visual,"添加角色眼睛");}
            else group.transform.SetParent(visual,false);
            if(recordUndo) Undo.RecordObject(group.transform,"摆放角色眼睛");
            group.transform.localPosition=Vector3.zero;group.transform.localRotation=Quaternion.identity;group.transform.localScale=Vector3.one;
            foreach(float side in new[]{-1f,1f})
            {
                var eye=GameObject.CreatePrimitive(PrimitiveType.Cube);eye.name=side<0?"Left Eye":"Right Eye";
                SceneManager.MoveGameObjectToScene(eye,visual.gameObject.scene);
                if(recordUndo)
                {
                    Undo.RegisterCreatedObjectUndo(eye,"添加角色眼睛");Undo.DestroyObjectImmediate(eye.GetComponent<Collider>());
                    Undo.SetTransformParent(eye.transform,group.transform,"添加角色眼睛");Undo.RecordObject(eye.transform,"摆放角色眼睛");
                }
                else {Object.DestroyImmediate(eye.GetComponent<Collider>());eye.transform.SetParent(group.transform,false);}
                eye.layer=visual.gameObject.layer;
                eye.transform.localPosition=new Vector3(side*0.105f*width,1.23f*height,-0.218f*width);
                eye.transform.localRotation=Quaternion.identity;eye.transform.localScale=new Vector3(0.11f*width,0.032f*height,0.012f*width);
                var renderer=eye.GetComponent<MeshRenderer>();if(recordUndo) Undo.RecordObject(renderer,"设置眼睛外观");
                renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                if(recordUndo) {VoxelLevelEditOperations.Record(eye.transform);VoxelLevelEditOperations.Record(renderer);}
            }
            if(recordUndo) VoxelLevelEditOperations.Record(group.transform);
            return true;
        }
        private static void UpdateScene(Scene scene)
        {
            if(TDProjectValidation.IsRunning||Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||Undo.isProcessing||!scene.IsValid()||!scene.isLoaded
                ||EditorSceneManager.IsPreviewScene(scene)) return;
            var roots=scene.GetRootGameObjects();bool dirty=scene.isDirty,changed=false;
            Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("更新角色外观和行动音效");
            foreach(var root in roots)
            {
                foreach(var actor in root.GetComponentsInChildren<PushBoxActor>(true))
                {
                    changed|=EnsureDetails(actor.Visual,actor.Role,true);
                    if(actor.Role==PushBoxRole.Warrior&&actor.name.StartsWith("Warrior",StringComparison.Ordinal))
                    {Undo.RecordObject(actor.gameObject,"力士命名");actor.name="Fighter"+actor.name.Substring(7);VoxelLevelEditOperations.Record(actor.gameObject);changed=true;}
                }
                foreach(var controller in root.GetComponentsInChildren<PushBoxController>(true))
                    if(controller.GetComponent<PushBoxActionAudio>()==null) {Undo.AddComponent<PushBoxActionAudio>(controller.gameObject);changed=true;}
            }
            if(scene.path==TDProjectPaths.StartMenuScene)
            {
                var courtyard=roots.FirstOrDefault(root=>root.name=="Courtyard");
                if(courtyard!=null)
                    foreach(var spec in new[]{("Warrior",PushBoxRole.Warrior),("Thief",PushBoxRole.Thief),("Mage",PushBoxRole.Mage),("Shield",PushBoxRole.Shield)})
                    {
                        var actor=courtyard.transform.Find(spec.Item1);if(actor==null) continue;
                        changed|=EnsureDetails(actor.Find("Visual")??actor,spec.Item2,true);
                    }
            }
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(undo);
            if(!changed) return;
            EditorSceneManager.MarkSceneDirty(scene);if(!dirty&&!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        }
    }
}
