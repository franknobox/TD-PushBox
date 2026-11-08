using System;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class EnemyAttackGizmos
    {
        [DrawGizmo(GizmoType.Selected|GizmoType.NonSelected)]
        private static void Draw(VoxelTacticsLevel level,GizmoType type)
        {
            if(SceneView.currentDrawingSceneView==null||!level.isActiveAndEnabled) return;
            var objects=level.GetComponentsInChildren<PushBoxObject>();
            if(!objects.Any(item=>item.Kind==PushBoxKind.Enemy)) return;
            var controller=level.GetComponent<PushBoxController>();
            bool playing=Application.IsPlaying(level.gameObject);
            PushBoxState state;
            if(playing) state=controller!=null?controller.Session?.State:null;
            else
            {
                try {state=PushBoxLevelValidation.ReadState(level);PushBoxRules.RefreshMechanisms(state);}
                catch(InvalidOperationException) {return;} // Incomplete drafts need not spam the Console during repaint.
            }
            if(state==null) return;
            var runtime=level.GetComponent<PushBoxLevel>();
            if(runtime==null) return;
            var previous=Handles.color;
            try
            {
                foreach(var item in objects.Where(item=>item.Kind==PushBoxKind.Enemy&&!SceneVisibilityManager.instance.IsHidden(item.gameObject)))
                {
                    var enemy=state.Find(item.StableId);
                    foreach(var p in PushBoxRules.AttackCells(state,enemy))
                    {
                        var cell=state.cells.TryGetValue(p,out var value)?value:null;
                        var center=playing?runtime.Surface(p):level.Surface(p,cell?.height??0);
                        if(!playing&&cell!=null&&cell.uphill!=Vector2Int.zero) center+=level.transform.up*(level.HeightStep*0.5f);
                        center+=level.transform.up*0.035f;
                        var x=level.transform.right*(level.CellSize*0.46f);
                        var z=level.transform.forward*(level.CellSize*0.46f);
                        var color=enemy.attack==PushBoxAttack.DragonFire?new Color(1,0.36f,0.08f):new Color(1,0.12f,0.12f);
                        Handles.DrawSolidRectangleWithOutline(new[]{center-x-z,center-x+z,center+x+z,center+x-z},
                            new Color(color.r,color.g,color.b,0.15f),new Color(color.r,color.g,color.b,0.8f));
                    }
                }
            }
            finally {Handles.color=previous;}
        }
    }
}
