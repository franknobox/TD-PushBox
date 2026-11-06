using System;
using System.Linq;
using NUnit.Framework;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    public sealed class TDLevelSceneryTests
    {
        [TestCase(TDLevelTheme.Forest,1,1)]
        [TestCase(TDLevelTheme.Castle,1,1)]
        [TestCase(TDLevelTheme.Forest,9,6)]
        [TestCase(TDLevelTheme.Castle,9,6)]
        [TestCase(TDLevelTheme.Forest,1,64)]
        [TestCase(TDLevelTheme.Castle,64,1)]
        public void SceneryIsReproducibleOutsideTheBoardAndContainsNoGameplay(TDLevelTheme theme,int width,int height)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                var level=TDLevelSceneFactory.Populate(scene,"SceneryTest",new Vector2Int(width,height),palette,palette.entries.First(entry=>entry.label=="浅绿"));
                TDLevelScenery.Build(level,theme,731);
                var env=level.EnvironmentContainer.GetChild(0);
                Assert.That(env.name,Is.EqualTo(theme+"_731"));
                Assert.That(env.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(env.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
                Assert.That(env.GetComponentsInChildren<MeshRenderer>().All(renderer=>renderer.sharedMaterials.All(material=>material!=null)),Is.True);
                Assert.That(level.GetComponentsInChildren<VoxelTacticsTile>().Length,Is.EqualTo(width*height));
                Assert.That(level.GetComponent<PushBoxLevel>().BuildState().entities,Is.Empty);
                var ground=env.Find("Ground");
                var tops=ground.Cast<Transform>().Where(node=>node.name.StartsWith("Top_",StringComparison.Ordinal)).ToArray();
                Assert.That(tops.Length,Is.GreaterThanOrEqualTo(8));
                Assert.That(tops.Select(node=>node.localScale).Distinct().Count(),Is.GreaterThan(3));
                Assert.That(tops.Select(node=>node.localPosition.y).Distinct().Count(),Is.GreaterThan(1));
                Assert.That(ground.Find("Base").localScale.x,Is.LessThanOrEqualTo(width*level.CellSize+0.4f));
                Assert.That(ground.Find("Base").localScale.z,Is.LessThanOrEqualTo(height*level.CellSize+0.4f));
                Assert.That(env.GetComponentsInChildren<Transform>(true).Any(node=>node.name=="Step"||node.name.StartsWith("Apron_",StringComparison.Ordinal)),Is.False);
                float hx=width*level.CellSize/2,hz=height*level.CellSize/2;
                foreach(string group in new[]{"Back","Sides","Front"})
                {
                    Assert.That(env.Find(group).childCount,Is.GreaterThan(0),group);
                    foreach(var renderer in env.Find(group).GetComponentsInChildren<Renderer>())
                    {
                        var bounds=renderer.bounds;
                        Assert.That(bounds.min.x>=hx||bounds.max.x<=-hx||bounds.min.z>=hz||bounds.max.z<=-hz,Is.True,renderer.name);
                        if(group=="Front") Assert.That(bounds.max.y,Is.LessThan(1.0f),renderer.name);
                    }
                }
                var first=env.GetComponentsInChildren<Transform>(true).Select(node=>(node.name,node.localPosition,node.localRotation,node.localScale)).ToArray();
                Assert.Throws<InvalidOperationException>(()=>TDLevelScenery.Build(level,theme,99));
                Assert.That(level.EnvironmentContainer.GetChild(0),Is.EqualTo(env));
                Object.DestroyImmediate(env.gameObject);TDLevelScenery.Build(level,theme,731);
                var second=level.EnvironmentContainer.GetChild(0).GetComponentsInChildren<Transform>(true)
                    .Select(node=>(node.name,node.localPosition,node.localRotation,node.localScale)).ToArray();
                Assert.That(second,Is.EqualTo(first));
                Assert.That(RenderSettings.fog,Is.False);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        private static void WithBoard(Action<VoxelTacticsLevel,VoxelLevelPalette> check)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                var level=TDLevelSceneFactory.Populate(scene,"EnvCheck",new Vector2Int(5,4),palette,palette.entries.First(e=>e.label=="浅绿"));
                check(level,palette);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void RefreshKeepsGameplayFlowersAndManualSiblingsAndCanUndo()
        {
            WithBoard((level,palette)=>
            {
                var edit=new VoxelLevelEditOperations(level,palette);
                VoxelLevelPalette.Entry Entry(string label)=>palette.entries.First(e=>e.label==label);
                var actor=edit.Place(Vector2Int.zero,Entry("力士"),0,out _).GetComponent<PushBoxActor>();
                var plate=edit.Place(Vector2Int.right,Entry("压力板"),0,out _).GetComponent<PushBoxObject>();
                var door=edit.Place(Vector2Int.right*2,Entry("门"),0,out _).GetComponent<PushBoxObject>();
                var data=new SerializedObject(door);var links=data.FindProperty("inputs");links.arraySize=1;
                links.GetArrayElementAtIndex(0).objectReferenceValue=plate;data.ApplyModifiedPropertiesWithoutUndo();
                TDLevelScenery.Build(level,TDLevelTheme.Forest,447);
                var manual=new GameObject("Manual");manual.transform.SetParent(level.EnvironmentContainer,false);
                VoxelDetailScatter.Scatter(edit,25,447,false);
                int oldFlowers=level.GetComponentsInChildren<VoxelTacticsTile>().Count(t=>t.transform.Find(VoxelDetailScatter.GroupName)!=null);
                var flowerIds=level.GetComponentsInChildren<VoxelTacticsTile>().Select(t=>t.transform.Find(VoxelDetailScatter.GroupName)).Where(t=>t!=null).Select(t=>t.GetInstanceID()).ToArray();
                var cells=edit.tiles.ToDictionary(pair=>pair.Key,pair=>(pair.Value,pair.Value.transform.position));
                var camera=level.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PushBoxCamera>()).Single();
                var cameraPosition=camera.transform.position;float distance=camera.Distance;
                string actorId=actor.StableId,board=EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>());
                Undo.FlushUndoRecordObjects();

                TDLevelScenery.Refresh(level,palette,TDLevelTheme.Castle,992);
                Assert.That(level.GetComponentsInChildren<VoxelTacticsTile>().Select(t=>t.transform.Find(VoxelDetailScatter.GroupName)).Where(t=>t!=null).Select(t=>t.GetInstanceID()),Is.EqualTo(flowerIds));
                Assert.That(level.EnvironmentContainer.childCount,Is.EqualTo(2));
                Assert.That(manual.transform.parent,Is.EqualTo(level.EnvironmentContainer));
                Assert.That(level.GeneratedScenery.name,Is.EqualTo("Castle_992"));
                Assert.That(actor.StableId,Is.EqualTo(actorId));Assert.That(actor.GridPosition,Is.EqualTo(Vector2Int.zero));
                Assert.That(door.CreateState().inputs,Is.EquivalentTo(new[]{plate.StableId}));
                foreach(var pair in cells)
                {Assert.That(edit.tiles[pair.Key],Is.EqualTo(pair.Value.Item1));Assert.That(pair.Value.Item1.transform.position,Is.EqualTo(pair.Value.Item2));}
                Assert.That(EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>()),Is.EqualTo(board));
                Assert.That(camera.transform.position,Is.EqualTo(cameraPosition));Assert.That(camera.Distance,Is.EqualTo(distance));

                Undo.PerformUndo();
                Assert.That(level.GeneratedScenery.name,Is.EqualTo("Forest_447"));
                Assert.That(level.ScenerySeed,Is.EqualTo(447));
                Assert.That(level.EnvironmentContainer.childCount,Is.EqualTo(2));
                Assert.That(level.GetComponentsInChildren<VoxelTacticsTile>().Count(t=>t.transform.Find(VoxelDetailScatter.GroupName)!=null),Is.EqualTo(oldFlowers));
                Assert.That(door.CreateState().inputs,Is.EquivalentTo(new[]{plate.StableId}));
            });
        }

        [Test]
        public void LegacySceneryRequiresConfirmationAndGameplayBranchesAreProtected()
        {
            WithBoard((level,palette)=>
            {
                var manual=new GameObject("OldEnv");manual.transform.SetParent(level.EnvironmentContainer,false);
                Assert.That(TDLevelScenery.NeedsLegacyReplacement(level),Is.True);
                Assert.Throws<InvalidOperationException>(()=>TDLevelScenery.Refresh(level,palette,TDLevelTheme.Forest,447));
                Assert.That(level.EnvironmentContainer.childCount,Is.EqualTo(1));
                TDLevelScenery.Refresh(level,palette,TDLevelTheme.Forest,447,true);
                Assert.That(level.GeneratedScenery,Is.Not.Null);Undo.PerformUndo();
                Assert.That(level.GeneratedScenery,Is.Null);
                var unsafeObject=new GameObject("Unsafe");unsafeObject.transform.SetParent(level.EnvironmentContainer,false);
                unsafeObject.AddComponent<PushBoxObject>().Configure(PushBoxKind.Blocker,Vector2Int.one,false);
                Assert.Throws<InvalidOperationException>(()=>TDLevelScenery.Refresh(level,palette,TDLevelTheme.Castle,447,true));
                Assert.That(level.EnvironmentContainer.childCount,Is.EqualTo(2));
            });
        }

        [Test]
        public void FlowerCoverageUsesEligibleCellsAndSeedAndSupportsZeroWithoutFloraAssets()
        {
            WithBoard((level,palette)=>
            {
                var edit=new VoxelLevelEditOperations(level,palette);
                edit.Place(Vector2Int.zero,palette.entries.First(e=>e.label=="力士"),0,out _);
                edit.Place(Vector2Int.right,palette.entries.First(e=>e.label=="箱子"),0,out _);
                int count=VoxelDetailScatter.Scatter(edit,50,447,false);
                var first=edit.tiles.Values.Where(t=>t.transform.Find(VoxelDetailScatter.GroupName)!=null).Select(t=>t.GridPosition).OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
                Assert.That(count,Is.EqualTo(9));Assert.That(first.Contains(Vector2Int.zero),Is.False);Assert.That(first.Contains(Vector2Int.right),Is.False);
                VoxelDetailScatter.Scatter(edit,50,447,false);
                var second=edit.tiles.Values.Where(t=>t.transform.Find(VoxelDetailScatter.GroupName)!=null).Select(t=>t.GridPosition).OrderBy(p=>p.x).ThenBy(p=>p.y).ToArray();
                Assert.That(second,Is.EqualTo(first));
                var empty=ScriptableObject.CreateInstance<VoxelLevelPalette>();
                try
                {
                    Assert.That(VoxelDetailScatter.CheckPalette(empty,50),Is.Not.Null);
                    Assert.That(VoxelDetailScatter.Scatter(new VoxelLevelEditOperations(level,empty),0,447,false),Is.Zero);
                    Assert.That(level.GetComponentsInChildren<VoxelTacticsTile>().All(t=>t.transform.Find(VoxelDetailScatter.GroupName)==null),Is.True);
                }
                finally {Object.DestroyImmediate(empty);}
            });
        }
    }
}
