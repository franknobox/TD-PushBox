using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using TD.PushBox.Frontend;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class LevelBuildValidation : IPreprocessBuildWithReport
    {
        public static bool IsRunning {get;private set;}
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            var issues=Check();
            if(issues.Count>0) throw new BuildFailedException("构建配置检查未通过（未修改场景、预览或构建名单）：\n"+string.Join("\n",issues));
        }

        public static List<string> Check()
        {
            if(IsRunning) throw new InvalidOperationException("构建配置检查正在执行。");
            var paths=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
            var issues=new List<string>();var references=new List<LevelBuildReference>();
            var catalog=AssetDatabase.LoadAssetAtPath<PushBoxLevelCatalog>(TDProjectPaths.Root+"/Resources/PushBoxLevels.asset");
            var previous=SceneManager.GetActiveScene();
            IsRunning=true;
            try
            {
                foreach(string path in paths.Distinct())
                {
                    if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null)
                    {issues.Add("构建场景文件不存在："+path);continue;}
                    var scene=SceneManager.GetSceneByPath(path);
                    bool opened=!scene.IsValid()||!scene.isLoaded;
                    if(!opened&&scene.isDirty)
                    {issues.Add("构建场景存在未保存修改，请先保存："+path);continue;}
                    try
                    {
                        if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                        ReadReferences(scene,catalog,references,issues);
                    }
                    catch(Exception error) {issues.Add("无法检查构建场景："+path+"\n"+error.Message);}
                    finally {if(opened&&scene.IsValid()&&scene.isLoaded) EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally
            {
                try
                {if(previous.IsValid()&&previous.isLoaded&&SceneManager.GetActiveScene()!=previous) SceneManager.SetActiveScene(previous);}
                finally {IsRunning=false;}
            }
            issues.AddRange(LevelBuildSceneRules.Check(paths,references));
            return issues;
        }

        private static void ReadReferences(Scene scene,PushBoxLevelCatalog catalog,List<LevelBuildReference> references,List<string> issues)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var menu in root.GetComponentsInChildren<StartMenuController>())
                    if(menu.isActiveAndEnabled) references.Add(new LevelBuildReference(scene.path+" / 开始游戏",
                        new SerializedObject(menu).FindProperty("levelSelectScene").stringValue));
                foreach(var selector in root.GetComponentsInChildren<LevelSelectController>())
                    if(selector.isActiveAndEnabled) references.Add(new LevelBuildReference(scene.path+" / 返回开始",
                        new SerializedObject(selector).FindProperty("startMenuScene").stringValue));
                foreach(var entrance in root.GetComponentsInChildren<PushBoxEntrance>())
                {
                    if(!entrance.isActiveAndEnabled||string.IsNullOrEmpty(entrance.SceneGuid)) continue;
                    string owner=scene.path+" / 入口 "+entrance.Number;
                    string target=AssetDatabase.GUIDToAssetPath(entrance.SceneGuid);
                    if(AssetDatabase.LoadAssetAtPath<SceneAsset>(target)==null)
                    {issues.Add(owner+"：关联的关卡场景已失效，请重新配置。");continue;}
                    references.Add(new LevelBuildReference(owner,target));
                    var entry=catalog!=null?catalog.Find(entrance.SceneGuid):null;
                    if(entry==null||entry.scenePath!=target)
                        issues.Add(owner+"：入口目录缺失或过期。请保存目标关卡，等待目录自动更新后再构建。");
                }
            }
        }
    }
}
