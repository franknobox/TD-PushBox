using System;
using System.Linq;
using TD.PushBox;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    [CustomEditor(typeof(PushBoxObject))]
    public sealed class PushBoxObjectEditor : Editor
    {
        private static GUIStyle plateLabel,plateShadow;

        [DrawGizmo(GizmoType.Selected|GizmoType.NonSelected)]
        private static void DrawPlateCoordinate(PushBoxObject item,GizmoType gizmoType)
        {
            var sceneView=SceneView.currentDrawingSceneView;
            if(sceneView==null||item.Kind!=PushBoxKind.Plate||!item.isActiveAndEnabled||SceneVisibilityManager.instance.IsHidden(item.gameObject)) return;
            Vector3 anchor=item.transform.position+item.transform.up*0.32f;
            if(sceneView.camera!=null&&sceneView.camera.WorldToViewportPoint(anchor).z<=0) return;
            plateLabel??=new GUIStyle(EditorStyles.boldLabel){fontSize=14,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,0.9f,0.4f)}};
            plateShadow??=new GUIStyle(plateLabel){normal={textColor=Color.black}};
            string text=$"({item.GridPosition.x}, {item.GridPosition.y})";
            Handles.BeginGUI();
            Vector2 point=HandleUtility.WorldToGUIPoint(anchor),size=plateLabel.CalcSize(new GUIContent(text));
            var rect=new Rect(point.x-size.x/2,point.y-size.y/2,size.x,size.y);
            GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),text,plateShadow);
            GUI.Label(rect,text,plateLabel);
            Handles.EndGUI();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();var property=serializedObject.GetIterator();bool enter=true;
            while(property.NextVisible(enter))
            {
                enter=false;
                if(property.name=="crushable"&&((PushBoxObject)target).Kind!=PushBoxKind.Crate) continue;
                if(property.name=="inputs") {DrawInputs(property,(PushBoxObject)target);continue;}
                using(new EditorGUI.DisabledScope(property.name=="m_Script")) EditorGUILayout.PropertyField(property,true);
            }
            serializedObject.ApplyModifiedProperties();
        }
        public static void DrawInputs(SerializedProperty inputs,PushBoxObject door)
        {
            if(door.Kind!=PushBoxKind.Door||door.DoorRule!=PushBoxDoorRule.PressurePlates) return;
            var level=door.GetComponentInParent<PushBoxLevel>();
            if(level==null) {EditorGUILayout.HelpBox("请在关卡场景中配置关联压力板。",MessageType.Info);return;}
            var plates=level.GetComponentsInChildren<PushBoxObject>().Where(item=>item.Kind==PushBoxKind.Plate)
                .OrderBy(item=>item.name,StringComparer.Ordinal).ToArray();
            inputs.isExpanded=EditorGUILayout.Foldout(inputs.isExpanded,"关联压力板",true);
            if(!inputs.isExpanded) return;
            EditorGUI.indentLevel++;
            for(int i=0;i<inputs.arraySize;i++)
            {
                var element=inputs.GetArrayElementAtIndex(i);var current=element.objectReferenceValue as PushBoxObject;
                int index=Array.IndexOf(plates,current);bool invalid=current!=null&&index<0;
                var names=new[]{invalid?"失效关联（请重新选择）":"未配置"}.Concat(plates.Select(item=>$"{item.name} [{item.GridPosition.x}, {item.GridPosition.y}]" )).ToArray();
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();int next=EditorGUILayout.Popup("压力板 "+(i+1),index+1,names);
                    if(EditorGUI.EndChangeCheck()) element.objectReferenceValue=next>0?plates[next-1]:null;
                    if(GUILayout.Button(new GUIContent("-","移除此关联"),GUILayout.Width(24)))
                    {element.objectReferenceValue=null;inputs.DeleteArrayElementAtIndex(i);break;}
                }
            }
            if(GUILayout.Button(new GUIContent("+","添加压力板关联"),GUILayout.Width(28)))
            {int i=inputs.arraySize;inputs.arraySize++;inputs.GetArrayElementAtIndex(i).objectReferenceValue=null;}
            if(plates.Length==0) EditorGUILayout.HelpBox("当前关卡没有压力板。",MessageType.Info);
            EditorGUI.indentLevel--;
        }
    }
}
