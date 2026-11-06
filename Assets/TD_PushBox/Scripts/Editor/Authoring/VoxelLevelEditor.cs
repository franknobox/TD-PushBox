using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    [CustomEditor(typeof(VoxelTacticsLevel))]
    public sealed class VoxelLevelEditor : Editor
    {
        private List<LevelValidationIssue> issues;
        private void OnEnable() {Undo.undoRedoPerformed+=ClearResults;}
        private void OnDisable() {Undo.undoRedoPerformed-=ClearResults;}
        private void ClearResults() {issues=null;Repaint();}

        public override void OnInspectorGUI()
        {
            if(DrawDefaultInspector()) issues=null;
            var level=(VoxelTacticsLevel)target;
            EditorGUILayout.LabelField("地块数量",level.GetComponentsInChildren<VoxelTacticsTile>(true).Length.ToString());
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if(GUILayout.Button("打开关卡编辑")) VoxelLevelWindow.Open(level,VoxelLevelPalette.For(level));
                if(GUILayout.Button("检查当前关卡")) issues=PushBoxLevelValidation.Check(level);
                EditorGUILayout.Space(4);
                if(GUILayout.Button(new GUIContent("按格点对齐场景","以已有格点为准，恢复地块和实体位置，不改变标识。"))) Apply(()=>PushBoxAuthoring.SnapLayout(level));
                if(GUILayout.Button(new GUIContent("按场景位置更新格点","读取直接拖动后的位置，校验通过后更新格点并吸附到网格；支持撤销。"))) Apply(()=>PushBoxAuthoring.UpdateGridFromScene(level));
                if(GUILayout.Button("修复缺失或重复标识")) Apply(()=>PushBoxAuthoring.RepairIds(level));
            }
            if(issues==null) return;
            if(issues.Count==0) EditorGUILayout.HelpBox("当前关卡检查通过。",MessageType.Info);
            for(int i=0;i<Mathf.Min(issues.Count,8);i++)
            {
                EditorGUILayout.HelpBox(issues[i].ToString(),MessageType.Error);
                if(issues[i].target!=null&&GUILayout.Button("定位对象",EditorStyles.miniButton)) EditorGUIUtility.PingObject(issues[i].target);
            }
            if(issues.Count>8) EditorGUILayout.LabelField("另有 "+(issues.Count-8)+" 项，请修复后重新检查。");
        }

        private void Apply(Action operation)
        {
            try {operation();issues=PushBoxLevelValidation.Check((VoxelTacticsLevel)target);}
            catch(Exception error) {issues=new List<LevelValidationIssue>{new(target,error.Message)};}
        }
    }
}
