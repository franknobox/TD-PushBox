using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class TDLevelCreationTests
    {
        private Scene scene,previous;
        private VoxelLevelPalette palette;
        private VoxelLevelPalette.Entry ground;
        private string savedPath;

        [SetUp]
        public void Setup()
        {
            previous=SceneManager.GetActiveScene();
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            palette=TDProjectPaths.Load<VoxelLevelPalette>(TDProjectPaths.Palette);
            ground=palette.entries.First(entry=>entry.label=="浅绿");
        }

        [TearDown]
        public void Teardown()
        {
            if(scene.IsValid()&&scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            if(!string.IsNullOrEmpty(savedPath)) AssetDatabase.DeleteAsset(savedPath);
        }

        [Test]
        public void RectangularBoardHasCenteredUniqueCoordinatesAndAssignedContainers()
        {
            var size=new Vector2Int(3,5);
            var level=TDLevelSceneFactory.Populate(scene,"Test Level",size,palette,ground);
            Assert.That(level.BoardSize,Is.EqualTo(size));
            foreach(var group in new[]{level.TerrainContainer,level.ActorsContainer,level.ObjectsContainer,
                level.EnvironmentContainer,level.DetailsContainer,level.RuntimeContainer})
            {Assert.That(group,Is.Not.Null);Assert.That(group.parent,Is.EqualTo(level.transform));}
            var tiles=level.GetComponentsInChildren<VoxelTacticsTile>();
            Assert.That(tiles.Length,Is.EqualTo(15));
            Assert.That(tiles.Select(tile=>tile.GridPosition).Distinct().Count(),Is.EqualTo(15));
            foreach(var tile in tiles)
            {
                Assert.That(tile.transform.parent,Is.EqualTo(level.TerrainContainer));
                Assert.That(Vector3.Distance(tile.transform.position,level.Surface(tile.GridPosition,0)),Is.LessThan(0.0001f));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(tile.gameObject),Is.EqualTo(ground.prefab));
                Assert.That(tile.transform.Find(VoxelLevelEditOperations.SupportName),Is.Not.Null);
                Assert.That(tile.GetComponentsInChildren<Collider>().Length,Is.EqualTo(1));
            }
            Assert.That(level.Surface(Vector2Int.zero,0)+level.Surface(size-Vector2Int.one,0),Is.EqualTo(Vector3.zero));
            Assert.That(level.ActorsContainer.childCount,Is.Zero);
            Assert.That(level.ObjectsContainer.Cast<Transform>().Select(item=>item.name),Is.EquivalentTo(new[]{"Obstacles","Goals","Doors","Plates","Movables"}));
            Assert.That(scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>()).Count(),Is.EqualTo(1));
            Assert.That(RenderSettings.fog,Is.False);
            var edit=new VoxelLevelEditOperations(level,palette);
            Assert.That(edit.Size,Is.EqualTo(size));
            Assert.That(edit.Contains(size-Vector2Int.one),Is.True);Assert.That(edit.Contains(size),Is.False);
        }

        [Test]
        public void FreshLevelHasTheRequestedEnvironmentDefaults()
        {
            var level=TDLevelSceneFactory.Populate(scene,"Defaults",Vector2Int.one,palette,ground);
            TDLevelScenery.ReadSettings(level,out var theme,out int seed,out int coverage);
            Assert.That(theme,Is.EqualTo(TDLevelTheme.Forest));Assert.That(seed,Is.EqualTo(447));
            Assert.That(coverage,Is.Zero);Assert.That(level.EnvironmentContainer.childCount,Is.Zero);
            Assert.That(level.GetComponentsInChildren<Transform>(true).Any(item=>item.name==VoxelDetailScatter.GroupName),Is.False);
            Assert.That(palette.entries.Any(e=>e.category==VoxelPaletteCategory.Decoration&&e.label=="树木"),Is.False);
        }

        [TestCase(0,5)]
        [TestCase(5,-1)]
        [TestCase(65,5)]
        public void InvalidSizesDoNotModifyTheScene(int width,int height)
        {
            Assert.Throws<ArgumentException>(()=>TDLevelSceneFactory.Populate(scene,"Invalid",new Vector2Int(width,height),palette,ground));
            Assert.That(scene.rootCount,Is.Zero);
        }

        [Test]
        public void NonFlatTerrainAndPopulatedScenesAreRejected()
        {
            foreach(string label in new[]{"高地","石块","灌木","深渊"})
                Assert.That(TDLevelSceneFactory.Validate(Vector2Int.one,palette,palette.entries.First(entry=>entry.label==label)),Is.Not.Null,label);
            Assert.That(TDLevelSceneFactory.Validate(new Vector2Int(64,64),palette,ground),Is.Null);
            TDLevelSceneFactory.Populate(scene,"One",Vector2Int.one,palette,ground);
            int count=scene.rootCount;
            Assert.Throws<InvalidOperationException>(()=>TDLevelSceneFactory.Populate(scene,"Two",Vector2Int.one,palette,ground));
            Assert.That(scene.rootCount,Is.EqualTo(count));
        }

        [Test]
        public void ExistingReferenceSceneCannotBeOverwritten()
        {
            Assert.Throws<InvalidOperationException>(()=>TDLevelSceneFactory.Create(TDProjectPaths.SrpgScene,Vector2Int.one,palette,ground));
            Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(scene));Assert.That(scene.rootCount,Is.Zero);
        }

        [Test]
        public void SavedSceneRetainsDimensionsPrefabOverridesAndSupports()
        {
            savedPath=TDProjectPaths.LevelScenes+"/__CreateLevelTest_"+Guid.NewGuid().ToString("N")+".unity";
            TDLevelSceneFactory.Populate(scene,"Saved Test",new Vector2Int(2,3),palette,ground);
            Assert.That(EditorSceneManager.SaveScene(scene,savedPath),Is.True);
            EditorSceneManager.CloseScene(scene,true);
            scene=EditorSceneManager.OpenScene(savedPath,OpenSceneMode.Additive);
            var restored=scene.GetRootGameObjects().Select(root=>root.GetComponent<VoxelTacticsLevel>()).First(item=>item!=null);
            Assert.That(restored.BoardSize,Is.EqualTo(new Vector2Int(2,3)));
            Assert.That(restored.GetComponentsInChildren<VoxelTacticsTile>().Select(tile=>tile.GridPosition).Distinct().Count(),Is.EqualTo(6));
            Assert.That(restored.TerrainContainer.childCount,Is.EqualTo(6));
            foreach(var tile in restored.GetComponentsInChildren<VoxelTacticsTile>())
                Assert.That(tile.transform.Find(VoxelLevelEditOperations.SupportName),Is.Not.Null);
        }
    }
}
