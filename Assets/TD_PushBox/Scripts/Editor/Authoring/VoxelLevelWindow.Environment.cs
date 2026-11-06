using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public sealed partial class VoxelLevelWindow
    {
        private void SaveEnvironmentSettings(TDLevelTheme theme,int seed,int coverage)
        {
            Undo.RecordObject(level,"修改关卡环境配置");level.ConfigureScenery(theme,seed,coverage);
            VoxelLevelEditOperations.Record(level);EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
        }
        private bool ReadEnvironmentSettings(out TDLevelTheme theme,out int seed,out int coverage)
        {
            try {TDLevelScenery.ReadSettings(level,out theme,out seed,out coverage);return true;}
            catch(Exception error)
            {theme=TDLevelTheme.Forest;seed=VoxelTacticsLevel.DefaultScenerySeed;coverage=VoxelTacticsLevel.DefaultFloraCoverage;EditorGUILayout.HelpBox(error.Message,MessageType.Warning);return false;}
        }
        private void DrawEnvironmentControls()
        {
            if(!ReadEnvironmentSettings(out var theme,out int seed,out int coverage)) return;
            EditorGUILayout.LabelField("环境",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            theme=(TDLevelTheme)EditorGUILayout.EnumPopup("背景环境",theme);
            using(new EditorGUILayout.HorizontalScope())
            {
                seed=EditorGUILayout.IntField("布景种子",seed);
                if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Refresh").image,"更换布景种子"),GUILayout.Width(24),GUILayout.Height(18)))
                {seed=Guid.NewGuid().GetHashCode();GUI.changed=true;}
            }
            if(EditorGUI.EndChangeCheck()) SaveEnvironmentSettings(theme,seed,coverage);
            if(GUILayout.Button(new GUIContent("刷新布景",EditorGUIUtility.IconContent("d_Refresh").image,"只重建外围布景；保留棋盘花草、地形、角色、机关、镜头与独立手工布景，支持撤销"),GUILayout.Height(28)))
                RefreshEnvironment(theme,seed);
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            coverage=EditorGUILayout.IntSlider("花草覆盖率（%）",coverage,0,100);
            if(EditorGUI.EndChangeCheck()) SaveEnvironmentSettings(theme,seed,coverage);
            DrawDetailControls(coverage);
        }
        private void RefreshEnvironment(TDLevelTheme theme,int seed)
        {
            SetEditing(false);
            try
            {
                bool replace=TDLevelScenery.NeedsLegacyReplacement(level);
                if(replace&&!EditorUtility.DisplayDialog("替换现有布景", "当前环境不是工具生成的布景。刷新会替换环境容器中的现有布景，保留棋盘、角色与机关。此操作支持撤销。", "替换布景", "取消")) return;
                TDLevelScenery.Refresh(level,palette,theme,seed,replace);
                status="已刷新外围布景，棋盘花草保持不变。";
            }
            catch(Exception error) {status=error.Message;}
            finally {Invalidate();}
        }
        private void DrawDetailControls(int coverage)
        {
            EditorGUILayout.Space(4);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(new GUIContent("刷新花草",EditorGUIUtility.IconContent("d_Refresh").image,"按覆盖率重新铺设棋盘花草，不刷新外围布景"),GUILayout.Height(24)))
                    OneAction(()=>{int count=VoxelDetailScatter.Scatter(context,coverage,Guid.NewGuid().GetHashCode());status=$"已刷新 {count} 格花草。";},"刷新花草");
                if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash").image,"清空当前棋盘花草，保留外围布景与其他环境物件"),GUILayout.Width(30),GUILayout.Height(24)))
                    OneAction(()=>{VoxelDetailScatter.Clear(level);status="已清空棋盘花草。";},"清空花草");
            }
        }
    }
}
