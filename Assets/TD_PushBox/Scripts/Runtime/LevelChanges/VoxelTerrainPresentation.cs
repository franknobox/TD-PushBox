using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class VoxelTerrainPresentation
    {
        public static void SupportStyle(VoxelTacticsLevel level,out Material material,out float bottom)
        {
            material=null;bottom=-0.7f;
            foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>(true))
            {
                var support=tile.transform.Find("Terrain Support");if(support==null) continue;
                material=support.GetComponent<MeshRenderer>()?.sharedMaterial;
                bottom=level.transform.InverseTransformPoint(support.TransformPoint(Vector3.down*0.5f)).y-level.GridOrigin.y;
                return;
            }
        }

        public static VoxelTacticsTile Create(VoxelTacticsTile prefab,VoxelTacticsLevel level,VoxelCellState cell,
            Transform parent,Material supportMaterial,float supportBottom)
        {
            var tile=Object.Instantiate(prefab,parent);tile.name=$"TerrainDelta_{cell.position.x}_{cell.position.y}";
            tile.ConfigureDefinition(prefab.Definition,cell.position,cell.height);tile.ConfigureSlope(cell.slope);
            tile.transform.SetPositionAndRotation(level.Surface(cell.position,cell.height),level.transform.rotation);
            tile.transform.localScale=Vector3.one;
            SyncSupport(tile,level,supportMaterial,supportBottom);
            return tile;
        }

        public static void SyncSupport(VoxelTacticsTile tile,VoxelTacticsLevel level,Material material,float bottom)
        {
            var old=tile.transform.Find("Terrain Support");
            if(old!=null) {old.gameObject.SetActive(false);Remove(old.gameObject);}
            if(tile.CreateState().kind==VoxelGroundKind.Void||material==null||tile.Definition!=null&&!tile.Definition.generateSupport) return;
            float top=-0.56f;bool found=false;
            var visual=tile.transform.Find("Visual");
            if(visual!=null)
                foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null) continue;
                    var bounds=filter.sharedMesh.bounds;
                    for(int i=0;i<8;i++)
                    {
                        var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        float y=tile.transform.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                        if(!found||y<top) top=y;found=true;
                    }
                }
            bottom-=tile.HeightLevel*level.HeightStep;
            if(top<=bottom) return;
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Terrain Support";
            var collider=go.GetComponent<Collider>();collider.enabled=false;Remove(collider);
            go.transform.SetParent(tile.transform,false);go.transform.localPosition=new Vector3(0,(top+bottom)/2,0);
            go.transform.localScale=new Vector3(level.CellSize-0.04f,top-bottom,level.CellSize-0.04f);
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
        }

        private static void Remove(Object target)
        {if(Application.isPlaying) Object.Destroy(target);else Object.DestroyImmediate(target);}
    }
}
