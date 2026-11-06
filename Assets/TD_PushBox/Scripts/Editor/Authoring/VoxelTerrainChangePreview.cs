using System;
using System.Collections.Generic;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    internal sealed class VoxelTerrainChangePreview : IDisposable
    {
        private readonly List<GameObject> hidden=new();
        private GameObject root;
        private void Hide(GameObject go)
        {
            if(go==null) return;
            foreach(var child in go.GetComponentsInChildren<Transform>(true))
                if(!SceneVisibilityManager.instance.IsHidden(child.gameObject)) {hidden.Add(child.gameObject);SceneVisibilityManager.instance.Hide(child.gameObject,false);}
        }
        private static void PreviewOnly(GameObject go)
        {
            foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags=HideFlags.HideAndDontSave;
            foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(script);
            foreach(var collider in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
        }
        public void Build(VoxelLevelEditOperations context,VoxelTerrainChangeDefinition plan)
        {
            Dispose();if(plan==null) return;
            root=new GameObject("Terrain Preview"){hideFlags=HideFlags.HideAndDontSave};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,context.level.gameObject.scene);
            VoxelTerrainPresentation.SupportStyle(context.level,out var material,out float bottom);
            try
            {
                var changes=plan.EffectiveTiles().Where(e=>e?.prefab!=null&&context.tiles.TryGetValue(e.position,out var tile)&&!VoxelTerrainChangeDefinition.MatchesInitial(e,tile)).ToList();
                foreach(var item in changes)
                {
                    var original=context.tiles[item.position];
                    foreach(string name in new[]{"Visual","Terrain Support",VoxelDetailScatter.GroupName}) Hide(original.transform.Find(name)?.gameObject);
                    var cell=item.prefab.CreateState();cell.position=item.position;cell.height=item.height;
                    PreviewOnly(VoxelTerrainPresentation.Create(item.prefab,context.level,cell,root.transform,material,bottom).gameObject);
                }
                Vector3 Surface(Vector2Int p)
                {
                    var e=plan.TileAt(p);return e?.prefab==null?context.Surface(p):
                        context.level.Surface(p,e.height)+context.level.transform.up*(e.prefab.Slope==VoxelSlopeDirection.None?0:context.level.HeightStep/2);
                }
                bool Void(Vector2Int p)=>plan.TileAt(p)?.prefab?.CreateState().kind==VoxelGroundKind.Void;
                void Spawn(GameObject source,Vector2Int point,Quaternion rotation,bool? open=null)
                {
                    if(source==null||Void(point)) return;
                    var ghost=UnityEngine.Object.Instantiate(source,root.transform);
                    ghost.name="Preview_"+source.name;ghost.transform.SetPositionAndRotation(Surface(point),rotation);
                    if(open.HasValue&&ghost.TryGetComponent<PushBoxObject>(out var obj)) obj.Present(new PushBoxEntity{open=open.Value});
                    PreviewOnly(ghost);
                }
                var rotation=context.level.transform.rotation;
                foreach(var actor in context.level.GetComponentsInChildren<PushBoxActor>(true))
                    if(actor.gameObject.activeInHierarchy&&changes.Any(e=>e.position==actor.GridPosition))
                    {Hide(actor.gameObject);Spawn(actor.gameObject,actor.GridPosition,actor.transform.rotation);}
                foreach(var obj in context.level.GetComponentsInChildren<PushBoxObject>(true))
                {
                    if(!obj.gameObject.activeInHierarchy) continue;
                    var enemy=plan.enemies.Find(e=>e.existingId==obj.StableId);
                    var edited=plan.objects.Find(e=>e.existingId==obj.StableId);
                    if(obj.Kind==PushBoxKind.Goal&&plan.switchToEvacuation) {Hide(obj.gameObject);continue;}
                    if(enemy!=null)
                    {Hide(obj.gameObject);if(!enemy.remove) Spawn(obj.gameObject,enemy.position,rotation*Quaternion.Euler(0,enemy.yaw,0));continue;}
                    if(edited!=null)
                    {Hide(obj.gameObject);if(!edited.remove) Spawn(edited.targetPrefab!=null?edited.targetPrefab.gameObject:obj.gameObject,edited.position,rotation*Quaternion.Euler(0,edited.yaw,0),edited.overrideOpen?edited.startsOpen:obj.StartsOpen);continue;}
                    if(changes.Any(e=>obj.Covers(e.position)))
                    {Hide(obj.gameObject);Spawn(obj.gameObject,obj.GridPosition,obj.transform.rotation);}
                }
                foreach(var p in plan.exits) Spawn(plan.targetExitPrefab?.gameObject,p,rotation);
                foreach(var e in plan.enemies) if(!e.remove&&string.IsNullOrEmpty(e.existingId)) Spawn(e.targetPrefab?.gameObject,e.position,rotation*Quaternion.Euler(0,e.yaw,0));
                foreach(var e in plan.objects) if(!e.remove&&string.IsNullOrEmpty(e.existingId)) Spawn(e.targetPrefab?.gameObject,e.position,rotation*Quaternion.Euler(0,e.yaw,0),e.startsOpen);
                if(plan.overrideBossPosition) Spawn(plan.bossPrefab?.gameObject,plan.bossPosition,rotation);
            }
            catch {Dispose();throw;}
        }
        public void Dispose()
        {
            foreach(var go in hidden) if(go!=null) SceneVisibilityManager.instance.Show(go,false);
            hidden.Clear();if(root!=null) UnityEngine.Object.DestroyImmediate(root);root=null;
        }
    }
}
