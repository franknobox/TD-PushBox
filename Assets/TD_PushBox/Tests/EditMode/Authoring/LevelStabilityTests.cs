using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TD.PushBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JobTest.TacticsVoxel
{
    public sealed class LevelStabilityTests
    {
        private Scene previous,scene;
        private VoxelTacticsLevel level;
        private VoxelLevelPalette palette;
        private VoxelLevelEditOperations edit;
        private VoxelLevelPalette.Entry Entry(string label)=>palette.entries.First(entry=>entry.label==label);
        private GameObject Place(Vector2Int point,string label)=>edit.Place(point,Entry(label),0,out _);

        [SetUp]
        public void SetUp()
        {
            previous=SceneManager.GetActiveScene();
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            palette=PushBoxAuthoring.Palette();
            level=TDLevelSceneFactory.Populate(scene,"Stability",new Vector2Int(4,3),palette,Entry("浅绿"));
            edit=new VoxelLevelEditOperations(level,palette);Place(Vector2Int.zero,"力士");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();EditorSceneManager.CloseScene(scene,true);
            if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
        }

        [Test]
        public void ValidationReportsIdsAndPositionWithoutRepairingThem()
        {
            Assert.That(PushBoxLevelValidation.Check(level),Is.Empty);
            var actor=level.GetComponentInChildren<PushBoxActor>();var box=Place(Vector2Int.right,"箱子").GetComponent<PushBoxObject>();
            var data=new SerializedObject(box);data.FindProperty("stableId").stringValue=actor.StableId;data.ApplyModifiedPropertiesWithoutUndo();
            actor.transform.position+=Vector3.right*0.3f;
            string before=EditorJsonUtility.ToJson(actor);Vector3 position=actor.transform.position;
            var errors=PushBoxLevelValidation.Check(level);
            Assert.That(errors.Any(issue=>issue.target==box&&issue.message.Contains("标识")),Is.True);
            Assert.That(errors.Any(issue=>issue.target==actor&&issue.message.Contains("位置")),Is.True);
            Assert.That(box.StableId,Is.EqualTo(actor.StableId));Assert.That(actor.transform.position,Is.EqualTo(position));
            Assert.That(EditorJsonUtility.ToJson(actor),Is.EqualTo(before));
        }

        [Test]
        public void WorldToGridSyncPreservesDoorInputsIdsAndSupportsUndo()
        {
            var actor=level.GetComponentInChildren<PushBoxActor>();
            var plate=Place(new Vector2Int(1,1),"压力板").GetComponent<PushBoxObject>();
            var door=Place(new Vector2Int(3,1),"门").GetComponent<PushBoxObject>();
            var data=new SerializedObject(door);var inputs=data.FindProperty("inputs");inputs.arraySize=1;
            inputs.GetArrayElementAtIndex(0).objectReferenceValue=plate;data.ApplyModifiedPropertiesWithoutUndo();
            string actorId=actor.StableId,plateId=plate.StableId;
            actor.transform.position=level.Surface(new Vector2Int(2,0),0);
            plate.transform.position=level.Surface(new Vector2Int(2,1),0);
            PushBoxAuthoring.UpdateGridFromScene(level);
            Assert.That(actor.GridPosition,Is.EqualTo(new Vector2Int(2,0)));
            Assert.That(plate.GridPosition,Is.EqualTo(new Vector2Int(2,1)));
            Assert.That(actor.StableId,Is.EqualTo(actorId));Assert.That(plate.StableId,Is.EqualTo(plateId));
            Assert.That(door.CreateState().inputs,Is.EquivalentTo(new[]{plateId}));
            Assert.That(PushBoxLevelValidation.Check(level),Is.Empty);
            Undo.PerformUndo();
            Assert.That(actor.GridPosition,Is.EqualTo(Vector2Int.zero));Assert.That(plate.GridPosition,Is.EqualTo(new Vector2Int(1,1)));
            Assert.That(actor.transform.position,Is.EqualTo(level.Surface(new Vector2Int(2,0),0)));
            Assert.That(door.CreateState().inputs,Is.EquivalentTo(new[]{plateId}));
        }

        [Test]
        public void InvalidWorldSyncLeavesEverythingUnchangedAndGridSnapOnlyMovesViews()
        {
            var actor=level.GetComponentInChildren<PushBoxActor>();Place(Vector2Int.right,"箱子");
            actor.transform.position=level.Surface(Vector2Int.right,0);
            string runtime=EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>());
            Assert.Throws<InvalidOperationException>(()=>PushBoxAuthoring.UpdateGridFromScene(level));
            Assert.That(actor.GridPosition,Is.EqualTo(Vector2Int.zero));
            Assert.That(actor.transform.position,Is.EqualTo(level.Surface(Vector2Int.right,0)));
            Assert.That(EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>()),Is.EqualTo(runtime));
            PushBoxAuthoring.SnapLayout(level);
            Assert.That(actor.transform.position,Is.EqualTo(level.Surface(Vector2Int.zero,0)));
            Assert.That(PushBoxLevelValidation.Check(level),Is.Empty);
        }

        [Test]
        public void CheckpointSurvivesUnitySerializationWithUndoHistory()
        {
            PushBoxAuthoring.Bake(level);var initial=level.GetComponent<PushBoxLevel>().BuildState();
            var actor=level.GetComponentInChildren<PushBoxActor>();var session=new PushBoxSession(initial);
            Assert.That(session.Move(actor.StableId,Vector2Int.right,out _),Is.True);
            var saved=JsonUtility.FromJson<PushBoxSessionCheckpoint>(JsonUtility.ToJson(session.SaveCheckpoint(initial,actor.StableId)));
            var restored=PushBoxSession.RestoreCheckpoint(saved);
            Assert.That(saved.selectedId,Is.EqualTo(actor.StableId));
            Assert.That(restored.State.Find(actor.StableId).position,Is.EqualTo(Vector2Int.right));
            Assert.That(restored.Undo(),Is.True);Assert.That(restored.State.Find(actor.StableId).position,Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void ProjectCheckDoesNotSaveOrChangeScenesAssetsAndBuildSettings()
        {
            var scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var dirty=scenes.Select(item=>item.isDirty).ToArray();var active=SceneManager.GetActiveScene();
            string actor=EditorJsonUtility.ToJson(level.GetComponentInChildren<PushBoxActor>());
            string runtime=EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>());
            var files=Directory.GetFiles(TDProjectPaths.Scenes,"*.unity",SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(TDProjectPaths.Root+"/Data","*.asset",SearchOption.AllDirectories))
                .Append("ProjectSettings/EditorBuildSettings.asset").ToDictionary(path=>path,File.ReadAllBytes);
            var build=EditorBuildSettings.scenes.Select(item=>(item.path,item.enabled)).ToArray();
            Assert.DoesNotThrow(()=>TDProjectValidation.CheckProject());
            Assert.That(TDProjectValidation.IsRunning,Is.False);Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(active));
            Assert.That(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt),Is.EqualTo(scenes));
            Assert.That(scenes.Select(item=>item.isDirty),Is.EqualTo(dirty));
            Assert.That(EditorJsonUtility.ToJson(level.GetComponentInChildren<PushBoxActor>()),Is.EqualTo(actor));
            Assert.That(EditorJsonUtility.ToJson(level.GetComponent<PushBoxLevel>()),Is.EqualTo(runtime));
            Assert.That(EditorBuildSettings.scenes.Select(item=>(item.path,item.enabled)),Is.EqualTo(build));
            foreach(var file in files) Assert.That(File.ReadAllBytes(file.Key),Is.EqualTo(file.Value),file.Key);
        }
    }
}
