using System;
using System.IO;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    [CustomEditor(typeof(PushBoxEntrance))]
    public sealed class PushBoxEntranceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() {Draw((PushBoxEntrance)target);}

        public static bool Draw(PushBoxEntrance entrance)
        {
            var data=new SerializedObject(entrance);data.Update();
            var number=data.FindProperty("number");
            number.stringValue=PushBoxEntrance.LimitNumber(EditorGUILayout.TextField("关卡编号",number.stringValue));
            var scene=data.FindProperty("sceneGuid");
            var options=EntranceSceneCatalog.Scenes;
            int selected=Array.FindIndex(options,item=>item.guid==scene.stringValue);
            bool missing=!string.IsNullOrEmpty(scene.stringValue)&&selected<0;
            string[] names=new[]{missing?"失效关卡（请重新配置）":"未配置"}.Concat(options.Select(item=>item.label)).ToArray();
            EditorGUI.BeginChangeCheck();
            int next=EditorGUILayout.Popup("对应关卡",selected+1,names);
            if(EditorGUI.EndChangeCheck()) scene.stringValue=next>0?options[next-1].guid:"";
            if(missing) EditorGUILayout.HelpBox("原关卡已删除或不在 Scenes/Levels 中，请重新选择。",MessageType.Warning);
            else if(options.Length==0) EditorGUILayout.HelpBox("Scenes/Levels 中尚无关卡场景。",MessageType.Info);
            bool changed=data.ApplyModifiedProperties();
            if(changed) {entrance.RefreshLabel();SceneView.RepaintAll();}
            return changed;
        }
    }

    [InitializeOnLoad]
    public static class EntranceSceneCatalog
    {
        public readonly struct Entry
        {
            public readonly string guid,path,label;
            public Entry(string id,string assetPath)
            {guid=id;path=assetPath;label=Path.ChangeExtension(assetPath.Substring(TDProjectPaths.LevelScenes.Length+1),null);}
        }
        private static Entry[] scenes;
        static EntranceSceneCatalog() {EditorApplication.projectChanged+=()=>scenes=null;}
        public static Entry[] Scenes=>scenes??=AssetDatabase.IsValidFolder(TDProjectPaths.LevelScenes)
            ?AssetDatabase.FindAssets("t:Scene",new[]{TDProjectPaths.LevelScenes})
                .Select(guid=>new Entry(guid,AssetDatabase.GUIDToAssetPath(guid))).OrderBy(item=>item.label,StringComparer.OrdinalIgnoreCase).ToArray()
            :Array.Empty<Entry>();
    }
}
