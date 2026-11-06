using System.Linq;
using NUnit.Framework;
using TD.PushBox;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class EntrancePreviewTests
    {
        [Test]
        public void PreviewContainsOnlyTargetVisualsWithoutGameplayOrForeignSceneObjects()
        {
            var previous=SceneManager.GetActiveScene();
            var source=EditorSceneManager.NewPreviewScene();
            var foreign=EditorSceneManager.NewPreviewScene();
            var destination=EditorSceneManager.NewPreviewScene();
            try
            {
                var original=GameObject.CreatePrimitive(PrimitiveType.Cube);original.name="Target";SceneManager.MoveGameObjectToScene(original,source);
                original.transform.position=new Vector3(3,1,2);original.AddComponent<PushBoxObject>();
                var other=GameObject.CreatePrimitive(PrimitiveType.Cube);other.name="Foreign";SceneManager.MoveGameObjectToScene(other,foreign);
                var lamp=new GameObject("Sun",typeof(Light));SceneManager.MoveGameObjectToScene(lamp,source);
                EntranceContentBuilder.CopyPreviewVisuals(source,destination);
                var roots=destination.GetRootGameObjects();
                var renderers=roots.SelectMany(root=>root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                Assert.That(renderers.Length,Is.EqualTo(1));Assert.That(renderers[0].name,Is.EqualTo("Target"));
                Assert.That(renderers[0].transform.position,Is.EqualTo(original.transform.position));
                Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)),Is.Empty);
                Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Collider>(true)),Is.Empty);
                Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Light>(true)).Count(),Is.EqualTo(1));
                Assert.That(original.GetComponent<PushBoxObject>(),Is.Not.Null);Assert.That(original.scene,Is.EqualTo(source));
                Assert.That(EditorSceneManager.GetSceneCullingMask(destination)&EditorSceneManager.GetSceneCullingMask(foreign),Is.Zero);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(destination);EditorSceneManager.ClosePreviewScene(foreign);EditorSceneManager.ClosePreviewScene(source);
                if(previous.IsValid()&&previous.isLoaded&&SceneManager.GetActiveScene()!=previous) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
