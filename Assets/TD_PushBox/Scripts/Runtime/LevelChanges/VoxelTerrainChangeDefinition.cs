using System;
using System.Collections.Generic;
using TD.PushBox;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    [CreateAssetMenu(menuName="TD推箱子/场地变化方案")]
    public sealed class VoxelTerrainChangeDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class TileChange
        {
            [InspectorName("格点")] public Vector2Int position;
            [InspectorName("替换地块")] public VoxelTacticsTile prefab;
            [InspectorName("高度层")] public int height;
        }
        [Serializable]
        public sealed class EnemyChange
        {
            public string key,existingId;
            public PushBoxObject targetPrefab;
            public Vector2Int position;
            public int yaw;
            public bool remove;
        }
        [Serializable]
        public sealed class ObjectChange
        {
            public string key,existingId;
            public PushBoxObject targetPrefab;
            public Vector2Int position;
            public int yaw;
            public bool remove,overrideInputs,overrideOpen,startsOpen;
            public List<string> inputs=new();
        }
        [SerializeField,InspectorName("复制的初始地形")] private List<TileChange> basis=new();
        [InspectorName("修改地块")] public List<TileChange> tiles=new();
        [InspectorName("目标点")] public List<Vector2Int> exits=new();
        [InspectorName("目标点预制体")] public PushBoxObject targetExitPrefab;
        [InspectorName("替换原目标点")] public bool switchToEvacuation=true;
        [InspectorName("敌方变化")] public List<EnemyChange> enemies=new();
        [InspectorName("机关变化")] public List<ObjectChange> objects=new();
        [InspectorName("生成的 Boss")] public PushBoxObject bossPrefab;
        [InspectorName("配置 Boss 出生点")] public bool overrideBossPosition;
        [InspectorName("Boss 出生格")] public Vector2Int bossPosition;
        public int BasisCount=>basis.Count;
        public void SetBasis(IEnumerable<TileChange> source)
        {
            basis=new List<TileChange>();
            foreach(var item in source) basis.Add(new TileChange{position=item.position,prefab=item.prefab,height=item.height});
        }
        public TileChange TileAt(Vector2Int p)=>tiles.Find(item=>item!=null&&item.position==p)??basis.Find(item=>item!=null&&item.position==p);
        public IEnumerable<TileChange> EffectiveTiles()
        {
            var overridden=new HashSet<Vector2Int>();
            foreach(var item in tiles) if(item!=null) overridden.Add(item.position);
            foreach(var item in basis) if(item==null||!overridden.Contains(item.position)) yield return item;
            foreach(var item in tiles) yield return item;
        }
        public static bool MatchesInitial(TileChange item,VoxelTacticsTile tile)
        {
            if(item?.prefab==null||tile==null||item.height!=tile.HeightLevel||item.prefab.Definition!=tile.Definition
                ||item.prefab.Slope!=tile.Slope||item.prefab.Walkable!=tile.Walkable) return false;
            var a=item.prefab.transform.Find("Visual")??item.prefab.transform;
            var b=tile.transform.Find("Visual")??tile.transform;
            var ma=a.GetComponentsInChildren<MeshFilter>(true);var mb=b.GetComponentsInChildren<MeshFilter>(true);
            if(ma.Length!=mb.Length) return false;
            for(int i=0;i<ma.Length;i++)
            {
                if(ma[i].sharedMesh!=mb[i].sharedMesh) return false;
                var pa=item.prefab.transform.worldToLocalMatrix*ma[i].transform.localToWorldMatrix;
                var pb=tile.transform.worldToLocalMatrix*mb[i].transform.localToWorldMatrix;
                for(int k=0;k<16;k++) if(Mathf.Abs(pa[k]-pb[k])>0.0001f) return false;
            }
            var ra=a.GetComponentsInChildren<Renderer>(true);var rb=b.GetComponentsInChildren<Renderer>(true);
            if(ra.Length!=rb.Length) return false;
            for(int i=0;i<ra.Length;i++)
            {
                var first=ra[i].sharedMaterials;var second=rb[i].sharedMaterials;
                if(first.Length!=second.Length) return false;
                for(int j=0;j<first.Length;j++) if(first[j]!=second[j]) return false;
            }
            return true;
        }
        public static string EntityId(string source,string key)=>$"{source}:change:{key}";
        public static string LinkId(string source,string id)=>id!=null&&id.StartsWith("plan:",StringComparison.Ordinal)?EntityId(source,id.Substring(5)):id;
    }
}
