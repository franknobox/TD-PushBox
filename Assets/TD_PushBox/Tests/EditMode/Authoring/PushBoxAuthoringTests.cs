using System.Linq;
using NUnit.Framework;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class PushBoxAuthoringTests
    {
        [TestCase("石块")]
        [TestCase("灌木")]
        [TestCase("高地")]
        public void TerrainObstaclesPreserveTheirGroundAndFollowHeightChanges(string label)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                VoxelLevelPalette.Entry Entry(string name)=>palette.entries.First(entry=>entry.label==name);
                var level=TDLevelSceneFactory.Populate(scene,"Obstacle",new Vector2Int(3,3),palette,Entry("浅绿"));
                var edit=new VoxelLevelEditOperations(level,palette);var p=Vector2Int.one;
                var ground=edit.Paint(p,Entry("深棕"),1,false,out _);
                var obstacle=edit.Place(p,Entry(label),0,out string error);
                Assert.That(obstacle,Is.Not.Null,error);Assert.That(edit.tiles[p],Is.SameAs(ground));
                Assert.That(Entry(label).category,Is.EqualTo(VoxelPaletteCategory.Terrain));
                Assert.That(Entry(label).IsTerrainObject,Is.True);Assert.That(edit.IsTerrainObject(obstacle),Is.True);
                Assert.That(obstacle.transform.parent,Is.EqualTo(level.ObjectsContainer.Find("Obstacles")));
                Assert.That(obstacle.transform.position,Is.EqualTo(ground.SurfacePosition));
                var state=obstacle.GetComponent<PushBoxObject>().CreateState();
                Assert.That(state.kind,Is.EqualTo(PushBoxKind.Blocker));Assert.That(state.movable,Is.False);
                var flowers=edit.Place(p,Entry("花草"),0,out error);Assert.That(flowers,Is.Not.Null,error);
                Assert.That(Vector3.Dot(flowers.transform.parent.position-ground.SurfacePosition,level.transform.up),Is.GreaterThan(0.5f));
                string id=state.id;
                var changed=edit.Paint(p,Entry("浅灰"),0,false,out error);Assert.That(changed,Is.Not.Null,error);
                Assert.That(obstacle.transform.position,Is.EqualTo(changed.SurfacePosition));
                Assert.That(obstacle.GetComponent<PushBoxObject>().StableId,Is.EqualTo(id));
                PushBoxAuthoring.Bake(level);
                var board=level.GetComponent<PushBoxLevel>().BuildState();PushBoxRules.Validate(board);
                Assert.That(board.cells[p].walkable,Is.True);Assert.That(board.SolidAt(p).kind,Is.EqualTo(PushBoxKind.Blocker));
                Assert.That(edit.Erase(obstacle,out error),Is.True,error);Assert.That(edit.tiles[p],Is.SameAs(changed));
                Assert.That(changed,Is.Not.Null);Assert.That(edit.tiles.Count,Is.EqualTo(9));
            }
            finally {Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);}
        }

        [TestCase("石块")]
        [TestCase("高地")]
        public void RectangleObstacleBrushPreservesEveryGroundTileAndSkipsActors(string label)
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                VoxelLevelPalette.Entry Entry(string label)=>palette.entries.First(entry=>entry.label==label);
                var level=TDLevelSceneFactory.Populate(scene,"ObstacleRect",new Vector2Int(3,3),palette,Entry("浅绿"));
                var edit=new VoxelLevelEditOperations(level,palette);var ground=edit.tiles.ToDictionary(pair=>pair.Key,pair=>pair.Value);
                edit.Place(Vector2Int.zero,Entry("力士"),0,out _);
                Assert.That(edit.PaintRectangle(Vector2Int.zero,new Vector2Int(2,2),Entry(label),0,false,out string error),Is.True,error);
                foreach(var pair in ground) Assert.That(edit.tiles[pair.Key],Is.SameAs(pair.Value));
                Assert.That(level.GetComponentsInChildren<PushBoxObject>().Length,Is.EqualTo(8));
                Assert.That(level.GetComponentsInChildren<PushBoxActor>().Single().GridPosition,Is.EqualTo(Vector2Int.zero));
            }
            finally {Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);}
        }

        [TestCase(PushBoxRole.Warrior)]
        [TestCase(PushBoxRole.Thief)]
        [TestCase(PushBoxRole.Mage)]
        [TestCase(PushBoxRole.Shield)]
        public void BackHairCoversRearSkinWithoutCollidersOrDuplicates(PushBoxRole role)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var visual=new GameObject("Visual");SceneManager.MoveGameObjectToScene(visual,scene);
                Assert.That(PushBoxActorAppearance.EnsureDetails(visual.transform,role,false),Is.True);
                Assert.That(PushBoxActorAppearance.EnsureDetails(visual.transform,role,false),Is.False);
                var hair=visual.transform.Find(PushBoxActorAppearance.HairName);
                Assert.That(hair,Is.Not.Null);Assert.That(hair.GetComponentsInChildren<Collider>(),Is.Empty);
                float width=role==PushBoxRole.Warrior?1.16f:role==PushBoxRole.Shield?1.10f:1;
                float height=role==PushBoxRole.Warrior?1.12f:role==PushBoxRole.Shield?1.08f:1;
                Assert.That(hair.localPosition.z-hair.localScale.z/2,Is.LessThan(0.21f*width));
                Assert.That(hair.localPosition.z+hair.localScale.z/2,Is.GreaterThan(0.21f*width));
                Assert.That(hair.localPosition.y+hair.localScale.y/2,Is.GreaterThanOrEqualTo(1.35f*height));
                Assert.That(hair.localPosition.y-hair.localScale.y/2,Is.GreaterThan(0.93f*height));
                Assert.That(visual.transform.Find(PushBoxActorAppearance.EyesName),Is.Not.Null);
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void OldMovementDefaultsUpgradeOnceWithoutOverwritingCustomTimings()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Motion");SceneManager.MoveGameObjectToScene(root,scene);
                var controller=root.AddComponent<PushBoxController>();var data=new SerializedObject(controller);
                void Set(float step,float repeat,int version)
                {
                    data.Update();data.FindProperty("stepDuration").floatValue=step;
                    data.FindProperty("repeatDelay").floatValue=repeat;data.FindProperty("motionVersion").intValue=version;
                    data.ApplyModifiedPropertiesWithoutUndo();controller.SendMessage("OnValidate");data.Update();
                }
                Set(0.12f,0.18f,0);
                Assert.That(data.FindProperty("stepDuration").floatValue,Is.EqualTo(0.30f).Within(0.001f));
                Assert.That(data.FindProperty("repeatDelay").floatValue,Is.EqualTo(0.34f).Within(0.001f));
                Assert.That(data.FindProperty("swapDuration").floatValue,Is.EqualTo(0.16f).Within(0.001f));
                Set(0.12f,0.18f,1);
                Assert.That(data.FindProperty("stepDuration").floatValue,Is.EqualTo(0.12f).Within(0.001f));
                Set(0.45f,0.52f,0);
                Assert.That(data.FindProperty("stepDuration").floatValue,Is.EqualTo(0.45f).Within(0.001f));
                Assert.That(data.FindProperty("repeatDelay").floatValue,Is.EqualTo(0.52f).Within(0.001f));
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [TestCase(PushBoxRole.Warrior)]
        [TestCase(PushBoxRole.Thief)]
        [TestCase(PushBoxRole.Mage)]
        [TestCase(PushBoxRole.Shield)]
        public void FacingEyesAreTwoThinFrontBarsWithoutCollidersOrDuplicates(PushBoxRole role)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var visual=new GameObject("Visual");SceneManager.MoveGameObjectToScene(visual,scene);
                Assert.That(PushBoxActorAppearance.EnsureEyes(visual.transform,role,false),Is.True);
                Assert.That(PushBoxActorAppearance.EnsureEyes(visual.transform,role,false),Is.False);
                var eyes=visual.transform.Find(PushBoxActorAppearance.EyesName);Assert.That(eyes.childCount,Is.EqualTo(2));
                Assert.That(eyes.GetComponentsInChildren<Collider>(),Is.Empty);
                foreach(Transform eye in eyes)
                {Assert.That(eye.localPosition.z,Is.LessThan(0));Assert.That(eye.localScale.x,Is.GreaterThan(eye.localScale.y*3));}
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void PuzzleCameraFramesItsCenterSlightlyHigherAtDifferentZoomDistances()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=new GameObject("Camera Framing");SceneManager.MoveGameObjectToScene(go,scene);
                var camera=go.AddComponent<Camera>();var control=go.AddComponent<PushBoxCamera>();
                var center=new Vector3(2,0,3);
                foreach(float distance in new[]{8f,20f})
                {
                    control.ConfigurePerspective(center,distance);
                    var point=camera.WorldToViewportPoint(center);
                    Assert.That(point.x,Is.EqualTo(0.5f).Within(0.001f));
                    Assert.That(point.y,Is.EqualTo(0.55f).Within(0.001f));
                    Assert.That(camera.fieldOfView,Is.EqualTo(50).Within(0.001f));
                    Assert.That(camera.transform.eulerAngles.x,Is.EqualTo(60).Within(0.001f));
                }
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void ExistingOrthographicCameraConvertsItsFramingOnlyOnce()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=new GameObject("Camera Migration");SceneManager.MoveGameObjectToScene(go,scene);
                var camera=go.AddComponent<Camera>();var control=go.AddComponent<PushBoxCamera>();
                var data=new SerializedObject(control);
                data.FindProperty("projectionVersion").intValue=0;
                data.FindProperty("viewSize").floatValue=5.2f;
                data.FindProperty("minimumSize").floatValue=2;
                data.FindProperty("maximumSize").floatValue=16;
                data.FindProperty("zoomSpeed").floatValue=0.7f;
                data.ApplyModifiedPropertiesWithoutUndo();
                control.SendMessage("OnValidate");
                float tangent=Mathf.Tan(25*Mathf.Deg2Rad),converted=control.Distance;
                Assert.That(camera.orthographic,Is.False);Assert.That(camera.fieldOfView,Is.EqualTo(50).Within(0.001f));
                Assert.That(converted,Is.EqualTo(5.2f/tangent).Within(0.001f));
                control.SendMessage("OnValidate");data.Update();
                Assert.That(control.Distance,Is.EqualTo(converted).Within(0.001f));
                Assert.That(data.FindProperty("zoomSpeed").floatValue,Is.EqualTo(0.7f/tangent).Within(0.001f));
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
        }

        [Test]
        public void PuzzlePrefabPaletteContainsNoCombatUnitsOrHealthUi()
        {
            var palette=PushBoxAuthoring.Palette();
            Assert.That(palette.puzzlePalette,Is.True);
            foreach(var entry in palette.entries)
            {
                Assert.That(VoxelLevelPalette.Check(entry),Is.Null,entry.label);
                Assert.That(entry.prefab.GetComponentsInChildren<Transform>(true).Any(item=>item.name=="Health Anchor"),Is.False,entry.label);
            }
            Assert.That(palette.entries.Count(entry=>entry.category==VoxelPaletteCategory.Players),Is.EqualTo(4));
            Assert.That(palette.entries.Where(entry=>entry.category==VoxelPaletteCategory.Enemies).All(entry=>entry.prefab.GetComponent<PushBoxObject>().Kind==PushBoxKind.Enemy),Is.True);
        }

        [Test]
        public void NewPuzzleSceneBakesEditorTerrainAndUsesOnlyTheFixedCameraController()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();var floor=palette.entries.First(entry=>entry.label=="浅绿");
                var level=TDLevelSceneFactory.Populate(scene,"Puzzle Authoring",new Vector2Int(3,3),palette,floor);
                var edit=new VoxelLevelEditOperations(level,palette);
                var actor=edit.Place(Vector2Int.zero,palette.entries.First(entry=>entry.label=="力士"),0,out string error);
                Assert.That(actor,Is.Not.Null,error);
                var box=edit.Place(Vector2Int.right,palette.entries.First(entry=>entry.label=="箱子"),0,out error);
                Assert.That(box,Is.Not.Null,error);
                Assert.That(edit.Paint(Vector2Int.zero,palette.entries.First(entry=>entry.label=="深棕"),0,false,out error),Is.Not.Null,error);
                Assert.That(actor.transform.parent,Is.EqualTo(level.ActorsContainer));
                Assert.That(edit.Move(box,new Vector2Int(2,0),0,out error),Is.True,error);
                PushBoxAuthoring.Bake(level);var session=new PushBoxSession(level.GetComponent<PushBoxLevel>().BuildState());
                Assert.That(session.State.cells.Count,Is.EqualTo(9));Assert.That(session.State.entities.Count,Is.EqualTo(2));
                var camera=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>()).Single();
                Assert.That(camera.GetComponent<PushBoxCamera>(),Is.Not.Null);
                Assert.That(camera.orthographic,Is.False);Assert.That(camera.fieldOfView,Is.EqualTo(50).Within(0.001f));
                Assert.That(camera.transform.eulerAngles.x,Is.EqualTo(60).Within(0.001f));Assert.That(camera.transform.eulerAngles.y,Is.EqualTo(0).Within(0.001f));
            }
            finally
            {
                Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        [Test]
        public void RectanglePaintPreservesPlacementsIdsAndDoorInputs()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                VoxelLevelPalette.Entry Entry(string label)=>palette.entries.First(e=>e.label==label);
                var level=TDLevelSceneFactory.Populate(scene,"Rect Check",new Vector2Int(4,3),palette,Entry("浅绿"));
                var edit=new VoxelLevelEditOperations(level,palette);
                var actor=edit.Place(Vector2Int.zero,Entry("力士"),0,out _).GetComponent<PushBoxActor>();
                var plate=edit.Place(new Vector2Int(1,1),Entry("压力板"),0,out _).GetComponent<PushBoxObject>();
                var door=edit.Place(new Vector2Int(2,1),Entry("门"),0,out _).GetComponent<PushBoxObject>();
                Assert.That(plate.transform.parent,Is.EqualTo(level.ObjectsContainer.Find("Plates")));
                Assert.That(door.transform.parent,Is.EqualTo(level.ObjectsContainer.Find("Doors")));
                var data=new SerializedObject(door);var inputs=data.FindProperty("inputs");inputs.arraySize=1;
                inputs.GetArrayElementAtIndex(0).objectReferenceValue=plate;data.ApplyModifiedPropertiesWithoutUndo();
                string id=actor.StableId;Vector3 position=actor.transform.position;
                Assert.That(edit.PaintRectangle(Vector2Int.zero,new Vector2Int(3,2),Entry("深棕"),0,false,out string error),Is.True,error);
                Assert.That(actor.GridPosition,Is.EqualTo(Vector2Int.zero));Assert.That(actor.StableId,Is.EqualTo(id));
                Assert.That(actor.transform.position,Is.EqualTo(position));Assert.That(door.CreateState().inputs,Is.EquivalentTo(new[]{plate.StableId}));
                Assert.That(edit.PaintRectangle(Vector2Int.zero,new Vector2Int(3,2),Entry("深渊"),0,false,out error),Is.True,error);
                Assert.That(edit.tiles[Vector2Int.zero].Walkable,Is.True);
                Assert.That(edit.tiles[new Vector2Int(1,1)].Walkable,Is.True);
            }
            finally {Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);}
        }

        [Test]
        public void TerrainPlanAcceptsOccupiedAbyssButRejectsBrokenPlateReference()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var plan=ScriptableObject.CreateInstance<VoxelTerrainChangeDefinition>();
            try
            {
                var palette=PushBoxAuthoring.Palette();
                VoxelLevelPalette.Entry Entry(string label)=>palette.entries.First(e=>e.label==label);
                var level=TDLevelSceneFactory.Populate(scene,"Plan Check",new Vector2Int(4,3),palette,Entry("浅绿"));
                var edit=new VoxelLevelEditOperations(level,palette);
                var actor=edit.Place(Vector2Int.zero,Entry("力士"),0,out _).GetComponent<PushBoxActor>();
                plan.tiles.Add(new(){position=Vector2Int.zero,prefab=Entry("深渊").prefab.GetComponent<VoxelTacticsTile>()});
                plan.objects.Add(new(){key="plate",targetPrefab=Entry("压力板").prefab.GetComponent<PushBoxObject>(),position=new Vector2Int(1,1)});
                var door=new VoxelTerrainChangeDefinition.ObjectChange{key="door",targetPrefab=Entry("门").prefab.GetComponent<PushBoxObject>(),position=new Vector2Int(2,1),overrideInputs=true,inputs=new(){"plan:plate"}};
                plan.objects.Add(door);
                Assert.DoesNotThrow(()=>VoxelTerrainPlanValidation.Validate(level,plan));
                Assert.That(actor.GridPosition,Is.EqualTo(Vector2Int.zero));Assert.That(edit.tiles[Vector2Int.zero].Walkable,Is.True);
                door.inputs[0]="missing";
                Assert.Throws<System.InvalidOperationException>(()=>VoxelTerrainPlanValidation.Validate(level,plan));
            }
            finally {Object.DestroyImmediate(plan);Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);}
        }
    }
}
