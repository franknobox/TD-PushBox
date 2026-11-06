using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JobTest.TacticsVoxel
{
    public static class TDProjectPaths
    {
        public const string Root = "Assets/TD_PushBox";
        public const string Scenes = Root + "/Scenes";
        public const string LevelScenes = Scenes + "/Levels";
        public const string StartMenuScene=Scenes+"/StartMenu.unity";
        public const string LevelSelectScene=Scenes+"/LevelSelect.unity";
        public const string SrpgScene = LevelScenes + "/Level_SRPG.unity";
        public const string PushBoxScene = LevelScenes + "/Level_01.unity";
        public const string Palette = Root + "/Data/Authoring/PushBoxPalette.asset";
        public const string Levels = Root + "/Data/Levels";
        // One mapping for logical resource names shared by generators and authoring tools.
        public static string Resolve(string path)
        {
            if (path == null || !path.StartsWith(Root + "/", StringComparison.Ordinal)) return path;
            string local = path.Substring(Root.Length + 1);
            if (local.StartsWith("Data/") && local.Substring(5).IndexOf('/') < 0)
            {
                string name = local.Substring(5);
                if (name == "LevelPalette.asset") return Palette;
                if (name.StartsWith("TerrainChange")) return Levels + "/SRPG/" + name;
                return Root + "/Data/Definitions/" + name;
            }
            if (local.StartsWith("Prefabs/") && local.Substring(8).IndexOf('/') < 0)
            {
                string name = local.Substring(8);
                string group = name.StartsWith("Tile_") ? "Tiles" : name.StartsWith("Unit_") ? "Actors/Players"
                    : name.StartsWith("Enemy_") ? "Actors/Enemies" : name.StartsWith("Scenery_") || name.StartsWith("Detail_") ? "Environment" : "Objects";
                return Root + "/Prefabs/" + group + "/" + name;
            }
            return path;
        }
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new ArgumentException("Invalid asset folder: " + path);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        public static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Resolve(path));
        public static void CreateAsset(UnityEngine.Object asset, string path)
        {
            path = Resolve(path); EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateAsset(asset, path);
        }
        public static GameObject LoadPrefabContents(string path) => PrefabUtility.LoadPrefabContents(Resolve(path));
        public static GameObject SaveAsPrefabAsset(GameObject root, string path)
        {
            path = Resolve(path); EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/')); return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
    }
}
