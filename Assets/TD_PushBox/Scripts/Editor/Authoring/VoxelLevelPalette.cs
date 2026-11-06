using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    public enum VoxelPaletteCategory { [InspectorName("地形")] Terrain, [InspectorName("我方")] Players,
        [InspectorName("敌方")] Enemies, [InspectorName("机关")] Objects, [InspectorName("环境")] Decoration }

    [CreateAssetMenu(menuName="TD推箱子/关卡编辑器资源目录")]
    public sealed class VoxelLevelPalette : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [InspectorName("名称")] public string label;
            [InspectorName("分类")] public VoxelPaletteCategory category;
            [InspectorName("预制体")] public GameObject prefab;
            [InspectorName("地表碎屑")] public bool surfaceDetail;
            [InspectorName("机关模型（尚无玩法组件）")] public bool modelOnly;
            public bool IsTerrainObject=>category==VoxelPaletteCategory.Terrain&&prefab!=null&&prefab.GetComponent<PushBoxObject>()?.Kind==PushBoxKind.Blocker;
        }
        [InspectorName("自动生成底座")] public bool generateSupport=true;
        [InspectorName("底座材质")] public Material supportMaterial;
        [Min(0.01f),InspectorName("底座深度")] public float supportDepth=0.84f;
        [InspectorName("资源")] public List<Entry> entries=new();
        public const string DefaultPath=TDProjectPaths.Palette;
        [HideInInspector] public bool puzzlePalette;
        public static VoxelLevelPalette For(VoxelTacticsLevel level)=>PushBoxAuthoring.Palette();
        public static VoxelLevelPalette Default()=>PushBoxAuthoring.Palette();

        public static string Check(Entry entry)
        {
            if(entry?.prefab==null||!PrefabUtility.IsPartOfPrefabAsset(entry.prefab)) return "请选择有效的 Prefab 资源。";
            if(entry.surfaceDetail&&entry.category!=VoxelPaletteCategory.Decoration) return "地表碎屑必须属于环境分类。";
            if(entry.modelOnly&&entry.category!=VoxelPaletteCategory.Objects) return "机关模型必须属于机关分类。";
            var tile=entry.prefab.GetComponent<VoxelTacticsTile>();
            var puzzleActor=entry.prefab.GetComponent<PushBoxActor>();var puzzleObject=entry.prefab.GetComponent<PushBoxObject>();
            if(puzzleActor!=null||puzzleObject!=null)
            {
                if(tile!=null||puzzleActor!=null&&puzzleObject!=null
                    ||entry.prefab.GetComponentsInChildren<PushBoxActor>(true).Length+(entry.prefab.GetComponentsInChildren<PushBoxObject>(true).Length)>1)
                    return "推箱子预制体不能混入战斗组件或嵌套玩法实体。";
                if(puzzleActor!=null) return entry.category==VoxelPaletteCategory.Players?null:"推箱子角色需要放在我方分类。";
                if(puzzleObject.Kind==PushBoxKind.Actor||puzzleObject.Footprint.x<1||puzzleObject.Footprint.y<1) return "推箱子物件类型或占地尺寸无效。";
                if(entry.IsTerrainObject) return puzzleObject.Footprint==Vector2Int.one&&!puzzleObject.CreateState().movable?null:"地形障碍必须是不可搬运的单格阻挡物。";
                return entry.category==VoxelPaletteCategory.Objects||entry.category==VoxelPaletteCategory.Decoration
                    ||entry.category==VoxelPaletteCategory.Enemies&&puzzleObject.Kind==PushBoxKind.Enemy?null:"推箱子物件分类无效。";
            }
            int total=entry.prefab.GetComponentsInChildren<VoxelTacticsTile>(true).Length;
            if(entry.category==VoxelPaletteCategory.Terrain)
                return tile!=null&&total==1&&tile.Definition!=null?null:"地形需要根节点地块组件和配置。";
            if(total>0) return "地块请放在地形分类。";
            if(entry.category==VoxelPaletteCategory.Players||entry.category==VoxelPaletteCategory.Enemies)
                return "角色或敌方需要根节点的推箱子组件。";
            if(entry.category==VoxelPaletteCategory.Objects&&!entry.modelOnly) return "机关需要推箱子物件组件。";
            if(entry.surfaceDetail&&entry.prefab.GetComponentsInChildren<Collider>(true).Length>0) return "花草不能有碰撞体。";
            return null;
        }
    }
}
