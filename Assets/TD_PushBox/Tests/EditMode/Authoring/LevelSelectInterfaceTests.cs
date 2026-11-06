using NUnit.Framework;
using TD.PushBox;
using TD.PushBox.Frontend;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class LevelSelectInterfaceTests
    {
        [Test]
        public void InterfaceUpgradeMovesReturnAndAddsOnlyOneCollapsedList()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup),typeof(LevelSelectController));
                SceneManager.MoveGameObjectToScene(root,scene);
                RectTransform Child(Transform parent,string name)
                {
                    var item=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
                    item.SetParent(parent,false);return item;
                }
                var back=Child(root.transform,"Back");var content=Child(back,"Content");
                var arrow=Child(content,"BackIcon");Child(content,"Label");
                var cameraRoot=new GameObject("Camera",typeof(Camera),typeof(PushBoxCamera));SceneManager.MoveGameObjectToScene(cameraRoot,scene);
                var camera=cameraRoot.GetComponent<PushBoxCamera>();
                Assert.That(LevelSelectInterfaceBuilder.Upgrade(root,camera,false),Is.True);
                Assert.That(LevelSelectInterfaceBuilder.Upgrade(root,camera,false),Is.False);
                Assert.That(back.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(back.pivot,Is.EqualTo(Vector2.zero));
                Assert.That(back.anchoredPosition,Is.EqualTo(new Vector2(48,48)));
                Assert.That(arrow.pivot,Is.EqualTo(new Vector2(0.5f,0.5f)));
                Assert.That(arrow.anchoredPosition.x-arrow.sizeDelta.x/2,Is.GreaterThan(0));
                Assert.That(arrow.anchoredPosition.x+arrow.sizeDelta.x/2,Is.LessThan(58));
                var lists=root.GetComponentsInChildren<CustomLevelListPanel>(true);Assert.That(lists.Length,Is.EqualTo(1));
                Assert.That(lists[0].IsExpanded,Is.False);
                Assert.That(lists[0].transform.Find("Dropdown").gameObject.activeSelf,Is.False);
                Assert.That(lists[0].PointerOverChanged.GetPersistentEventCount(),Is.EqualTo(1));
                Assert.That(lists[0].PointerOverChanged.GetPersistentTarget(0),Is.EqualTo(camera));
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
