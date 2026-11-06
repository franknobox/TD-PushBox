using UnityEditor;
using UnityEditor.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public static class TDProjectEntry
    {
        [MenuItem("TD推箱子/打开场景/StartMenu 开始界面")]
        public static void OpenStartMenu()=>Open(TDProjectPaths.StartMenuScene);
        [MenuItem("TD推箱子/打开场景/LevelSelect 关卡选择")]
        public static void OpenLevelSelect()=>Open(TDProjectPaths.LevelSelectScene);
        private static void Open(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
        }
    }
}
