using System;
using System.Collections.Generic;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public enum TDLevelTheme { [InspectorName("林地")] Forest, [InspectorName("城堡")] Castle }

    // Shared board authoring; the playable state belongs to PushBoxLevel.
    [DisallowMultipleComponent]
    public sealed class VoxelTacticsLevel : MonoBehaviour
    {
        public const float DefaultHeightStep=0.45f;
        public const int DefaultScenerySeed=447;
        public const int DefaultFloraCoverage=0;
        [SerializeField, InspectorName("网格原点")] private Vector3 gridOrigin=new(-6.16f,0.28f,-5.04f);
        [SerializeField, Min(0.01f), InspectorName("地块尺寸")] private float cellSize=1.12f;
        [SerializeField, Min(0.01f), InspectorName("每层高度")] private float heightStep=DefaultHeightStep;
        [SerializeField, InspectorName("编辑边界（零为自动）")] private Vector2Int boardSize;
        [SerializeField, InspectorName("场地变化方案")] private VoxelTerrainChangeDefinition terrainChange;
        [SerializeField, InspectorName("地形容器")] private Transform terrainContainer;
        [SerializeField, InspectorName("角色容器")] private Transform actorsContainer;
        [SerializeField, InspectorName("箱子机关容器")] private Transform objectsContainer;
        [SerializeField, InspectorName("环境容器")] private Transform environmentContainer;
        [SerializeField, InspectorName("环境细节容器")] private Transform detailsContainer;
        [SerializeField, InspectorName("运行时容器")] private Transform runtimeContainer;
        [SerializeField, HideInInspector] private TDLevelTheme sceneryTheme;
        [SerializeField, HideInInspector] private int scenerySeed=DefaultScenerySeed;
        [SerializeField, HideInInspector] private int floraCoverage=DefaultFloraCoverage;
        [SerializeField, HideInInspector] private bool sceneryConfigured;
        [SerializeField, HideInInspector] private Transform generatedScenery;
        public VoxelTerrainChangeDefinition TerrainChange=>terrainChange;
        public Transform TerrainContainer=>terrainContainer;
        public Transform ActorsContainer=>actorsContainer;
        public Transform ObjectsContainer=>objectsContainer;
        public Transform EnvironmentContainer=>environmentContainer;
        public Transform DetailsContainer=>detailsContainer;
        public Transform RuntimeContainer=>runtimeContainer;
        public TDLevelTheme SceneryTheme=>sceneryTheme;
        public int ScenerySeed=>scenerySeed;
        public int FloraCoverage=>floraCoverage;
        public bool SceneryConfigured=>sceneryConfigured;
        public Transform GeneratedScenery=>generatedScenery;
        public void ConfigureScenery(TDLevelTheme theme,int seed,int coverage)
        {sceneryTheme=theme;scenerySeed=seed;floraCoverage=Mathf.Clamp(coverage,0,100);sceneryConfigured=true;}
        public void AssignGeneratedScenery(Transform value)=>generatedScenery=value;
        public Vector3 GridOrigin=>gridOrigin;
        public float CellSize=>cellSize;
        public float HeightStep=>heightStep;
        public Vector2Int BoardSize=>boardSize;
        public void AssignContainers(Transform terrain,Transform actors,Transform objects,Transform environment,Transform details,Transform runtime)
        {terrainContainer=terrain;actorsContainer=actors;objectsContainer=objects;environmentContainer=environment;detailsContainer=details;runtimeContainer=runtime;}
        public void AssignContainer(string kind,Transform value)
        {
            switch(kind)
            {
                case "Tiles": terrainContainer=value;break;
                case "Units": actorsContainer=value;break;
                case "Objects": objectsContainer=value;break;
                case "Environment": environmentContainer=value;break;
                case "Details": detailsContainer=value;break;
                case "Runtime": runtimeContainer=value;break;
                default: throw new ArgumentException("Unknown container: "+kind);
            }
        }
        public Vector2Int GridPoint(Vector3 world)
        {
            Vector3 local=transform.InverseTransformPoint(world)-gridOrigin;
            return Vector2Int.RoundToInt(new Vector2(local.x/cellSize,local.z/cellSize));
        }
        public Vector3 Surface(Vector2Int point,int height)=>transform.TransformPoint(gridOrigin+new Vector3(point.x*cellSize,height*heightStep,point.y*cellSize));
        public void Configure(Vector3 origin,float size,float step) {gridOrigin=origin;cellSize=size;heightStep=step;}
        public void SnapLayout()
        {
            if(Application.isPlaying) return;
            var targets=new List<Transform>();
            foreach(var tile in GetComponentsInChildren<VoxelTacticsTile>(true)) targets.Add(tile.transform);
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObjects(targets.ToArray(),"关卡对齐网格");
#endif
            foreach(var tile in GetComponentsInChildren<VoxelTacticsTile>(true)) tile.transform.position=Surface(tile.GridPosition,tile.HeightLevel);
#if UNITY_EDITOR
            foreach(var target in targets) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
#endif
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(0.2f,0.9f,0.8f,0.5f);
            foreach(var tile in GetComponentsInChildren<VoxelTacticsTile>(true))
                Gizmos.DrawWireCube(Surface(tile.GridPosition,tile.HeightLevel),new Vector3(cellSize,0.03f,cellSize));
        }
    }
}
