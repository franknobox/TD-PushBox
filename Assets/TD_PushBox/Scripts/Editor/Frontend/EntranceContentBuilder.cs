using System;
using System.Collections.Generic;
using System.IO;
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
    public static class EntranceContentBuilder
    {
        private const string CatalogPath=TDProjectPaths.Root+"/Resources/PushBoxLevels.asset";
        private const string PreviewFolder=TDProjectPaths.Root+"/Art/Previews";
        private const string MaterialPath=TDProjectPaths.Root+"/Resources/UI/LevelPreview.mat";
        private const string PreviewVersion="isolated-3";
        private static bool pending=true,running;
        static EntranceContentBuilder()
        {
            EditorApplication.update+=TrySync;
            EditorSceneManager.sceneSaved+=scene=>{if(IsLevel(scene.path)) Request();};
        }
        public static bool IsLevel(string path)=>path!=null&&path.StartsWith(TDProjectPaths.LevelScenes+"/",StringComparison.OrdinalIgnoreCase)
            &&path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase);
        public static void Request() {if(!TDProjectValidation.IsRunning) pending=true;}
        private static void TrySync()
        {
            if(TDProjectValidation.IsRunning||!pending||running||Application.isBatchMode||AssetDatabase.IsAssetImportWorkerProcess()||EditorApplication.isCompiling||EditorApplication.isUpdating
                ||EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer||Undo.isProcessing||GUIUtility.hotControl!=0||PrefabStageUtility.GetCurrentPrefabStage()!=null) return;
            pending=false;
            try {Sync();}
            catch(Exception error)
            {
                Debug.LogError("关卡入口预览更新失败："+error.Message);
                if(Directory.Exists("Archive")) File.WriteAllText("Archive/EntranceContentResult.txt","ERROR: "+error);
            }
        }

        public static void Sync()
        {
            if(running) return;
            running=true;
            try
            {
                TDProjectPaths.EnsureFolder(TDProjectPaths.LevelScenes);
                TDProjectPaths.EnsureFolder(PreviewFolder);
                EnsureMaterial();
                var catalog=AssetDatabase.LoadAssetAtPath<PushBoxLevelCatalog>(CatalogPath);
                if(catalog==null) {catalog=ScriptableObject.CreateInstance<PushBoxLevelCatalog>();TDProjectPaths.CreateAsset(catalog,CatalogPath);}
                var scenes=AssetDatabase.FindAssets("t:Scene",new[]{TDProjectPaths.LevelScenes})
                    .Select(guid=>(guid,path:AssetDatabase.GUIDToAssetPath(guid))).OrderBy(item=>item.path,StringComparer.Ordinal).ToArray();
                bool changed=catalog.levels.Count!=scenes.Length;int captured=0;
                var entries=new List<PushBoxLevelEntry>();
                foreach(var scene in scenes)
                {
                    var entry=catalog.Find(scene.guid)??new PushBoxLevelEntry{guid=scene.guid};
                    string hash=PreviewVersion+":"+AssetDatabase.GetAssetDependencyHash(scene.path);
                    var loaded=SceneManager.GetSceneByPath(scene.path);
                    if((entry.preview==null||entry.previewHash!=hash)&&(!loaded.IsValid()||!loaded.isLoaded||!loaded.isDirty))
                    {
                        try
                        {
                            entry.preview=Capture(scene.path,PreviewFolder+"/"+scene.guid+".png");
                            entry.previewHash=hash;changed=true;captured++;
                        }
                        catch(Exception error) {Debug.LogWarning("地图预览尚未生成（不影响进入关卡）："+scene.path+"\n"+error.Message);}
                    }
                    if(entry.scenePath!=scene.path) {entry.scenePath=scene.path;changed=true;}
                    entries.Add(entry);
                }
                if(!changed) changed=!catalog.levels.Select(item=>item.guid).SequenceEqual(entries.Select(item=>item.guid));
                if(changed) {catalog.levels=entries;EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);}
                if(Directory.Exists("Archive")) File.WriteAllText("Archive/EntranceContentResult.txt",$"DONE: {entries.Count} level entries, {captured} map previews captured; no PlayMode; no authored scenes or build settings changed\n");
            }
            finally {running=false;}
        }

        private static void EnsureMaterial()
        {
            if(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)!=null) return;
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(TDProjectPaths.Root+"/Art/Shaders/LevelPreview.shader");
            if(shader==null) throw new InvalidOperationException("缺少地图预览材质着色器。");
            TDProjectPaths.CreateAsset(new Material(shader){name="LevelPreview"},MaterialPath);
        }

        public static void CopyPreviewVisuals(Scene source,Scene destination)
        {
            if(!EditorSceneManager.IsPreviewScene(destination)||source==destination)
                throw new ArgumentException("预览内容只能复制到独立预览场景。");
            var root=EditorUtility.CreateGameObjectWithHideFlags("MapVisuals",HideFlags.HideAndDontSave);
            SceneManager.MoveGameObjectToScene(root,destination);root.SetActive(false);
            var transforms=new Dictionary<Transform,Transform>();
            void Copy(Transform original,Transform parent)
            {
                var go=new GameObject(original.name){layer=original.gameObject.layer};
                var copy=go.transform;copy.SetParent(parent,false);
                copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
                go.SetActive(original.gameObject.activeSelf);transforms.Add(original,copy);
                foreach(Transform child in original) Copy(child,copy);
            }
            foreach(var original in source.GetRootGameObjects()) Copy(original.transform,root.transform);
            foreach(var pair in transforms)
            {
                var original=pair.Key.gameObject;var go=pair.Value.gameObject;
                var filter=original.GetComponent<MeshFilter>();
                if(filter!=null) go.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var text=original.GetComponent<TextMesh>();
                if(text!=null) EditorUtility.CopySerialized(text,go.AddComponent<TextMesh>());
                foreach(var renderer in original.GetComponents<Renderer>())
                {
                    if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer)&&!(renderer is SpriteRenderer)) continue;
                    var copy=go.GetComponent(renderer.GetType());
                    if(copy==null) copy=go.AddComponent(renderer.GetType());
                    EditorUtility.CopySerialized(renderer,copy);
                    var targetRenderer=(Renderer)copy;
                    targetRenderer.probeAnchor=renderer.probeAnchor!=null&&transforms.TryGetValue(renderer.probeAnchor,out var anchor)?anchor:null;
                    if(copy is SkinnedMeshRenderer skin&&renderer is SkinnedMeshRenderer sourceSkin)
                    {
                        skin.rootBone=sourceSkin.rootBone!=null&&transforms.TryGetValue(sourceSkin.rootBone,out var bone)?bone:null;
                        skin.bones=sourceSkin.bones.Select(item=>item!=null&&transforms.TryGetValue(item,out var targetBone)?targetBone:null).ToArray();
                    }
                }
                var light=original.GetComponent<Light>();
                if(light!=null) EditorUtility.CopySerialized(light,go.AddComponent<Light>());
            }
            // Copy render components only: preview generation must not run gameplay or editor scripts.
            root.SetActive(true);
        }

        private static Texture2D Capture(string path,string output)
        {
            var previous=SceneManager.GetActiveScene();
            var sourceScene=SceneManager.GetSceneByPath(path);
            bool opened=!sourceScene.IsValid()||!sourceScene.isLoaded;
            if(opened) sourceScene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var scene=EditorSceneManager.NewPreviewScene();
            var oldTarget=RenderTexture.active;
            RenderTexture target=null;Texture2D image=null;
            try
            {
                var source=sourceScene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).FirstOrDefault(item=>item.CompareTag("MainCamera"));
                if(source==null) throw new InvalidOperationException("关卡缺少 MainCamera："+path);
                CopyPreviewVisuals(sourceScene,scene);
                var go=EditorUtility.CreateGameObjectWithHideFlags("MapCam",HideFlags.HideAndDontSave,typeof(Camera));
                SceneManager.MoveGameObjectToScene(go,scene);
                var camera=go.GetComponent<Camera>();camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                camera.enabled=false;camera.scene=scene;camera.cameraType=CameraType.Preview;camera.aspect=16f/9f;
                camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.useOcclusionCulling=false;
                target=RenderTexture.GetTemporary(1024,576,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                camera.targetTexture=target;
                // Use the target's ambient lighting without changing the editor's active scene.
                Unsupported.SetOverrideLightingSettings(sourceScene);
                try {camera.Render();}
                finally {Unsupported.RestoreOverrideLightingSettings();}
                RenderTexture.active=target;
                image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                var pixels=image.GetPixels32();int min=255,max=0;
                for(int i=0;i<pixels.Length;i+=31) {int value=(pixels[i].r+pixels[i].g+pixels[i].b)/3;min=Math.Min(min,value);max=Math.Max(max,value);}
                if(max-min<6) throw new InvalidOperationException("地图预览为空白，请检查关卡镜头："+path);
                File.WriteAllBytes(output,image.EncodeToPNG());
                camera.targetTexture=null;
            }
            finally
            {
                RenderTexture.active=oldTarget;
                if(image!=null) Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(scene);
                if(opened) EditorSceneManager.CloseScene(sourceScene,true);
                if(target!=null) RenderTexture.ReleaseTemporary(target);
                if(previous.IsValid()&&previous.isLoaded&&SceneManager.GetActiveScene()!=previous) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(output);
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=1024;
            importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
        }
    }

    public sealed class EntranceContentImport : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(EntranceContentBuilder.IsLevel)) EntranceContentBuilder.Request();
        }
    }

}
