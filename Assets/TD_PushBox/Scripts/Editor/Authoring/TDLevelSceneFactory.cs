using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public static class TDLevelSceneFactory
    {
        public const int MaximumSide=64;
        private const float CellSize=1.12f;
        private const float HeightStep=VoxelTacticsLevel.DefaultHeightStep;

        public static bool IsFlatGround(VoxelLevelPalette.Entry entry)
        {
            if(entry?.category!=VoxelPaletteCategory.Terrain||VoxelLevelPalette.Check(entry)!=null) return false;
            var tile=entry.prefab.GetComponent<VoxelTacticsTile>();
            return tile!=null&&tile.Walkable&&tile.HeightLevel==0&&tile.Slope==VoxelSlopeDirection.None
                &&tile.CreateState().kind==VoxelGroundKind.Ground;
        }

        public static string Validate(Vector2Int size,VoxelLevelPalette palette,VoxelLevelPalette.Entry ground)
        {
            if(size.x<1||size.y<1||size.x>MaximumSide||size.y>MaximumSide)
                return $"宽和高均需在 1～{MaximumSide} 格之间。";
            if(palette==null) return "请选择资源目录。";
            if(!palette.entries.Contains(ground)||!IsFlatGround(ground)) return "请选择资源目录中的可通行平地。";
            if(palette.generateSupport&&palette.supportMaterial==null) return "资源目录缺少底座材质。";
            return null;
        }

        public static VoxelTacticsLevel Create(string path,Vector2Int size,VoxelLevelPalette palette,VoxelLevelPalette.Entry ground,
            TDLevelTheme theme=TDLevelTheme.Forest,int scenerySeed=VoxelTacticsLevel.DefaultScenerySeed,int floraCoverage=VoxelTacticsLevel.DefaultFloraCoverage)
        {
            string error=Validate(size,palette,ground);
            if(error!=null) throw new ArgumentException(error);
            error=VoxelDetailScatter.CheckPalette(palette,floraCoverage);
            if(error!=null) throw new ArgumentException(error);
            if(floraCoverage<0||floraCoverage>100) throw new ArgumentOutOfRangeException(nameof(floraCoverage));
            if(!Enum.IsDefined(typeof(TDLevelTheme),theme)) throw new ArgumentException("未知的场景风格。");
            if(string.IsNullOrWhiteSpace(path)||!path.StartsWith(TDProjectPaths.LevelScenes+"/",StringComparison.Ordinal)
                ||path.Contains("/../")||path.Contains("/./")||path.Contains('\\')
                ||!path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请将新关卡保存到 Scenes/Levels 目录或其子目录。");
            TDProjectPaths.EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));
            if(File.Exists(path)||AssetDatabase.LoadMainAssetAtPath(path)!=null)
                throw new InvalidOperationException("同名场景已存在，请使用其他名称，不会覆盖原关卡。");
            if(EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("请在场景编辑模式下创建关卡。");

            var previousActive=SceneManager.GetActiveScene();
            var previousScenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var level=Populate(scene,Path.GetFileNameWithoutExtension(path),size,palette,ground);
                level.ConfigureScenery(theme,scenerySeed,floraCoverage);
                TDLevelScenery.Build(level,theme,scenerySeed);
                if(floraCoverage>0) VoxelDetailScatter.Scatter(new VoxelLevelEditOperations(level,palette),floraCoverage,scenerySeed,false);
                if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("新关卡保存失败，原场景仍保留。");
                foreach(var previous in previousScenes) EditorSceneManager.CloseScene(previous,true);
                return level;
            }
            catch
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previousActive.IsValid()&&previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                throw;
            }
        }

        public static VoxelTacticsLevel Populate(Scene scene,string levelName,Vector2Int size,VoxelLevelPalette palette,VoxelLevelPalette.Entry ground)
        {
            string error=Validate(size,palette,ground);
            if(error!=null) throw new ArgumentException(error);
            if(!scene.IsValid()||!scene.isLoaded||scene.rootCount!=0||SceneManager.GetActiveScene()!=scene)
                throw new InvalidOperationException("只能在当前的空白场景中创建关卡。");

            var root=CreateObject(scene,levelName);
            var level=root.AddComponent<VoxelTacticsLevel>();
            level.Configure(new Vector3(-(size.x-1)*CellSize/2,0,-(size.y-1)*CellSize/2),CellSize,HeightStep);
            var data=new SerializedObject(level);
            data.FindProperty("boardSize").vector2IntValue=size;
            data.ApplyModifiedPropertiesWithoutUndo();
            Transform Group(string label)=>CreateObject(scene,label,root.transform).transform;
            level.AssignContainers(Group("地形"),Group("角色"),Group("箱子与机关"),Group("环境"),Group("环境细节"),Group("运行时对象"));
            PushBoxObjectGroups.Ensure(level);

            var edit=new VoxelLevelEditOperations(level,palette);
            var template=ground.prefab.GetComponent<VoxelTacticsTile>();
            for(int y=0;y<size.y;y++)
                for(int x=0;x<size.x;x++)
                {
                    var point=new Vector2Int(x,y);
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(ground.prefab,level.TerrainContainer);
                    go.name=$"{ObjNames.Label(ground.label,ground.prefab.name)}_{x:00}_{y:00}";
                    go.transform.SetPositionAndRotation(level.Surface(point,0),Quaternion.identity);
                    go.transform.localScale=Vector3.one;
                    var tile=go.GetComponent<VoxelTacticsTile>();
                    tile.ConfigureDefinition(template.Definition,point,0);tile.ConfigureSlope(VoxelSlopeDirection.None);
                    edit.SyncSupport(tile,false);
                    // New scenes start with a saved baseline, not thousands of editor undo records.
                    VoxelLevelEditOperations.Record(go);VoxelLevelEditOperations.Record(go.transform);VoxelLevelEditOperations.Record(tile);
                }

            CreateCamera(scene,size);
            var light=CreateObject(scene,"主光源").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.sun=light;RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.65f,0.68f,0.64f);
            PushBoxAuthoring.InitializeLevel(level);
            EditorSceneManager.MarkSceneDirty(scene);
            return level;
        }

        private static GameObject CreateObject(Scene scene,string name,Transform parent=null)
        {
            var go=new GameObject(ObjNames.En(name));SceneManager.MoveGameObjectToScene(go,scene);
            if(parent!=null) go.transform.SetParent(parent,false);
            return go;
        }

        private static void CreateCamera(Scene scene,Vector2Int size)
        {
            var camera=CreateObject(scene,"Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(0.19f,0.25f,0.23f);camera.orthographic=false;camera.fieldOfView=50;
            var rotation=Quaternion.Euler(60,0,0);
            float distance=Mathf.Max(12,new Vector2(size.x,size.y).magnitude*CellSize*1.5f);
            camera.transform.SetPositionAndRotation(-(rotation*Vector3.forward)*distance,rotation);
            camera.nearClipPlane=0.1f;camera.farClipPlane=distance*3;
            camera.gameObject.AddComponent<AudioListener>();
        }
    }
}
