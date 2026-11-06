using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public static class TDProjectValidation
    {
        private static bool checkingProject;
        public static bool IsRunning=>checkingProject||LevelBuildValidation.IsRunning;

        [MenuItem("TD推箱子/检查工程")]
        public static void Run()
        {
            var issues=CheckProject();
            PushBoxLevelValidation.Report(issues,"工程检查：");
            if(issues.Count==0) Debug.Log("工程检查通过；未保存或修改任何关卡、方案和构建配置。");
        }

        public static List<LevelValidationIssue> CheckProject()
        {
            if(IsRunning||EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在场景编辑模式执行检查。");
            var issues=new List<LevelValidationIssue>();
            var active=SceneManager.GetActiveScene();
            var loaded=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .Where(scene=>scene.isLoaded&&!EditorSceneManager.IsPreviewScene(scene)).ToArray();
            var opened=new List<Scene>();checkingProject=true;
            try
            {
                var palette=PushBoxAuthoring.Palette();
                foreach(var entry in palette.entries)
                {
                    string error=VoxelLevelPalette.Check(entry);
                    if(error!=null) issues.Add(new LevelValidationIssue(palette,entry.label+"： "+error));
                }
                foreach(var scene in loaded) CheckScene(scene,issues);
                foreach(string path in AssetDatabase.FindAssets("t:Scene",new[]{TDProjectPaths.Scenes}).Select(AssetDatabase.GUIDToAssetPath))
                {
                    if(loaded.Any(scene=>scene.path==path)) continue;
                    try
                    {
                        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);opened.Add(scene);
                        CheckScene(scene,issues);
                    }
                    catch(Exception error) {issues.Add(new LevelValidationIssue(AssetDatabase.LoadMainAssetAtPath(path),path+"： "+error.Message));}
                }
                foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{TDProjectPaths.Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
                    CheckScripts(AssetDatabase.LoadAssetAtPath<GameObject>(path),issues);
                issues.AddRange(LevelBuildValidation.Check().Select(message=>new LevelValidationIssue(null,message)));
                return issues;
            }
            finally
            {
                // Only close scenes loaded by this check. Never save or reload the author's scenes.
                foreach(var scene in opened) if(scene.IsValid()&&scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
                if(active.IsValid()&&active.isLoaded&&SceneManager.GetActiveScene()!=active) SceneManager.SetActiveScene(active);
                checkingProject=false;
            }
        }

        private static void CheckScene(Scene scene,List<LevelValidationIssue> issues)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                CheckScripts(root,issues);
                foreach(var level in root.GetComponentsInChildren<VoxelTacticsLevel>())
                    foreach(var issue in PushBoxLevelValidation.Check(level))
                        issues.Add(new LevelValidationIssue(issue.target,scene.name+" / "+issue));
            }
        }

        private static void CheckScripts(GameObject root,List<LevelValidationIssue> issues)
        {
            if(root==null) return;
            foreach(var child in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject)>0)
                    issues.Add(new LevelValidationIssue(child.gameObject,"缺失脚本："+child.name));
        }
    }
}
