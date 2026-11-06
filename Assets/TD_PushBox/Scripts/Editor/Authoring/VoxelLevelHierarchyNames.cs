using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TD.PushBox;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class VoxelLevelHierarchyNames
    {
        private static readonly Regex NumberSuffix = new(@"(?: *\([0-9]+\)|（[0-9]+）)$", RegexOptions.CultureInvariant);
        private static bool queued;

        static VoxelLevelHierarchyNames()
        {
            EditorApplication.hierarchyChanged += QueueRefresh;
            QueueRefresh();
        }

        private static void QueueRefresh()
        {
            if (TDProjectValidation.IsRunning || queued || Undo.isProcessing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            queued = true;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            queued = false;
            if (TDProjectValidation.IsRunning || Undo.isProcessing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            foreach (var level in UnityEngine.Object.FindObjectsByType<VoxelTacticsLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!EditorUtility.IsPersistent(level) && level.gameObject.scene.IsValid() && level.gameObject.scene.isLoaded
                    && !EditorSceneManager.IsPreviewScene(level.gameObject.scene)) Normalize(level);
        }

        private static string BaseName(string name)
        {
            Match suffix;
            while ((suffix = NumberSuffix.Match(name)).Success && suffix.Index > 0)
                name = name.Substring(0, suffix.Index);
            return name;
        }

        public static void SetUniqueName(GameObject target, string requestedName)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (target.transform.parent != null)
                foreach (Transform sibling in target.transform.parent)
                    if (sibling.gameObject != target) used.Add(sibling.name);
            string basis = BaseName(ObjNames.En(requestedName)), name = basis;
            for (int number = 2; used.Contains(name); number++) name = $"{basis} ({number})";
            if (target.name == name) return;
            target.name = name;
            VoxelLevelEditOperations.Record(target);
            EditorSceneManager.MarkSceneDirty(target.scene);
        }

        public static void Normalize(VoxelTacticsLevel level)
        {
            PushBoxObjectGroups.Normalize(level);
            var targets = level.GetComponentsInChildren<PushBoxActor>(true).Select(item=>item.gameObject)
                .Concat(level.GetComponentsInChildren<PushBoxObject>(true).Select(item=>item.gameObject)).ToHashSet();
            foreach (var parent in targets.Select(go => go.transform.parent).Where(parent => parent != null).Distinct())
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (Transform child in parent)
                {
                    bool duplicate = !seen.Add(child.name);
                    if (!targets.Contains(child.gameObject)) continue;
                    // Convert Unity's copy suffix without nesting another number after it.
                    bool unityCopy = child.name.EndsWith(")", StringComparison.Ordinal) && BaseName(child.name) != child.name;
                    if (duplicate || unityCopy) SetUniqueName(child.gameObject, child.name);
                    seen.Add(child.name);
                }
            }
        }
    }
}
