using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public sealed class TDLevelCreateWindow : EditorWindow
    {
        [SerializeField] private string levelName="Level_01";
        [SerializeField] private Vector2Int size=new(16,16);
        [SerializeField] private VoxelLevelPalette palette;
        [SerializeField] private GameObject groundPrefab;
        [SerializeField] private TDLevelTheme theme=TDLevelTheme.Forest;
        [SerializeField] private int scenerySeed=VoxelTacticsLevel.DefaultScenerySeed;
        [SerializeField,HideInInspector] private int settingsVersion;
        private Vector2 scroll;
        private string status;

        [MenuItem("TD推箱子/创建关卡")]
        public static void Open()
        {
            var window=GetWindow<TDLevelCreateWindow>("创建关卡");
            window.minSize=new Vector2(360,390);window.Show();
        }

        private void OnEnable()
        {
            titleContent=new GUIContent("创建关卡");
            if(settingsVersion==0) {if(scenerySeed==731) scenerySeed=VoxelTacticsLevel.DefaultScenerySeed;settingsVersion=1;}
            if(palette==null||!palette.puzzlePalette) palette=PushBoxAuthoring.Palette();
        }

        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.Space(10);
                levelName=EditorGUILayout.TextField("关卡名称",levelName);
                palette=(VoxelLevelPalette)EditorGUILayout.ObjectField("资源目录",palette,typeof(VoxelLevelPalette),false);
                size.x=EditorGUILayout.IntField("宽",size.x);
                size.y=EditorGUILayout.IntField("高",size.y);
                EditorGUILayout.LabelField("地块总数",size.x>0&&size.y>0?((long)size.x*size.y).ToString():"0");

                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.LabelField("环境",EditorStyles.boldLabel);
                theme=(TDLevelTheme)EditorGUILayout.EnumPopup("背景环境",theme);
                using(new EditorGUILayout.HorizontalScope())
                {
                    scenerySeed=EditorGUILayout.IntField("布景种子",scenerySeed);
                    if(GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_Refresh").image,"更换布景种子"),GUILayout.Width(24),GUILayout.Height(18)))
                        scenerySeed=Guid.NewGuid().GetHashCode();
                }
                var grounds=palette!=null?palette.entries.Where(TDLevelSceneFactory.IsFlatGround).ToArray():Array.Empty<VoxelLevelPalette.Entry>();
                int index=Array.FindIndex(grounds,entry=>entry.prefab==groundPrefab);
                if(index<0&&grounds.Length>0)
                {
                    index=Mathf.Max(0,Array.FindIndex(grounds,entry=>entry.label==(theme==TDLevelTheme.Castle?"浅灰":"浅绿")));groundPrefab=grounds[index].prefab;
                }
                using(new EditorGUI.DisabledScope(grounds.Length==0))
                {
                    index=EditorGUILayout.Popup("地面颜色",Mathf.Max(0,index),grounds.Length>0?grounds.Select(entry=>entry.label).ToArray():new[]{"无可用平地"});
                    if(grounds.Length>0) groundPrefab=grounds[index].prefab;
                }
                if(EditorGUI.EndChangeCheck()) status=null;
                var ground=grounds.FirstOrDefault(entry=>entry.prefab==groundPrefab);
                string error=TDLevelSceneFactory.Validate(size,palette,ground)??VoxelDetailScatter.CheckPalette(palette,VoxelTacticsLevel.DefaultFloraCoverage);
                if(string.IsNullOrWhiteSpace(levelName)||levelName.Trim().IndexOfAny(Path.GetInvalidFileNameChars())>=0
                    ||levelName.Trim()=="."||levelName.Trim()=="..") error="请输入有效的关卡名称，不要包含路径或文件名非法字符。";
                else if(ObjNames.HasHan(levelName)) error="关卡名称请使用简短英文、缩写或拼音，例如 Level_02。";
                if(PrefabStageUtility.GetCurrentPrefabStage()!=null) error="请先退出 Prefab 编辑模式。";
                if(error!=null) EditorGUILayout.HelpBox(error,MessageType.Warning);
                if(!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status,MessageType.Error);
                EditorGUILayout.Space(12);
                using(new EditorGUI.DisabledScope(error!=null))
                    if(GUILayout.Button(new GUIContent("创建关卡",EditorGUIUtility.IconContent("Toolbar Plus").image),GUILayout.Height(32))) Create(ground);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Create(VoxelLevelPalette.Entry ground)
        {
            TDProjectPaths.EnsureFolder(TDProjectPaths.LevelScenes);
            string path=EditorUtility.SaveFilePanelInProject("创建关卡",levelName.Trim(),"unity","选择新关卡的保存位置",TDProjectPaths.LevelScenes);
            if(string.IsNullOrEmpty(path)) return;
            if(File.Exists(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)
            {status="同名场景已存在，请使用其他名称，不会覆盖原关卡。";return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("创建关卡",$"{size.x} × {size.y}",0.25f);
                var level=TDLevelSceneFactory.Create(path,size,palette,ground,theme,scenerySeed);
                Selection.activeGameObject=level.gameObject;
                VoxelLevelWindow.Open(level,PushBoxAuthoring.Palette());status=null;
            }
            catch(Exception exception) {status=exception.Message;Debug.LogException(exception);}
            finally {EditorUtility.ClearProgressBar();}
        }
    }
}
