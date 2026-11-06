using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class PushBoxObjectGroups
    {
        private static readonly string[] Names={"Obstacles","Goals","Doors","Plates","Movables"};
        private static string Name(PushBoxKind kind)=>kind switch
        {
            PushBoxKind.Blocker=>"Obstacles",PushBoxKind.Goal=>"Goals",PushBoxKind.Door=>"Doors",
            PushBoxKind.Plate=>"Plates",PushBoxKind.Crate=>"Movables",_=>null
        };
        public static Transform For(VoxelTacticsLevel level,PushBoxKind kind)
        {
            Ensure(level);string name=Name(kind);
            return name!=null?level.ObjectsContainer.Find(name):level.ObjectsContainer;
        }
        public static void Ensure(VoxelTacticsLevel level)
        {
            if(level.ObjectsContainer==null||Application.isPlaying||TDProjectValidation.IsRunning) return;
            foreach(string name in Names)
            {
                if(level.ObjectsContainer.Find(name)!=null) continue;
                var go=new GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,level.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go,"创建物件分组");
                Undo.SetTransformParent(go.transform,level.ObjectsContainer,"创建物件分组");
                go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                VoxelLevelEditOperations.Record(go.transform);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            }
        }
        public static void Normalize(VoxelTacticsLevel level)
        {
            if(level.ObjectsContainer==null||Application.isPlaying||TDProjectValidation.IsRunning||Undo.isProcessing) return;
            Ensure(level);
            foreach(var item in level.ObjectsContainer.GetComponentsInChildren<PushBoxObject>(true))
            {
                string name=Name(item.Kind);if(name==null) continue;
                var parent=item.transform.parent;
                bool managed=parent==level.ObjectsContainer;
                foreach(string group in Names) managed|=parent==level.ObjectsContainer.Find(group);
                if(!managed||parent==level.ObjectsContainer.Find(name)) continue;
                if(PrefabUtility.IsPartOfPrefabInstance(item)&&!PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)) continue;
                Undo.SetTransformParent(item.transform,level.ObjectsContainer.Find(name),"整理物件分组");
                VoxelLevelEditOperations.Record(item.transform);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(level.gameObject.scene);
            }
        }
    }
}
