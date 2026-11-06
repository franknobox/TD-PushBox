using NUnit.Framework;

namespace JobTest.TacticsVoxel
{
    public sealed class LevelBuildSceneRulesTests
    {
        [Test]
        public void ValidationPreservesSelectedOrderAndDoesNotRequireUnreferencedDrafts()
        {
            var paths=new[]{"Scenes/Start.unity","Scenes/Select.unity","Scenes/Level.unity"};
            var original=(string[])paths.Clone();
            var issues=LevelBuildSceneRules.Check(paths,new[]{new LevelBuildReference("Menu","Select"),new LevelBuildReference("Entrance","Scenes/Level.unity")});
            Assert.That(issues,Is.Empty);CollectionAssert.AreEqual(original,paths);
        }

        [Test]
        public void DisabledOrMissingTargetIsReportedWithoutAddingIt()
        {
            var paths=new[]{"Scenes/Select.unity"};
            var issues=LevelBuildSceneRules.Check(paths,new[]{new LevelBuildReference("入口 1-2","Scenes/Level.unity")});
            Assert.That(issues.Count,Is.EqualTo(1));StringAssert.Contains("入口 1-2",issues[0]);
            Assert.That(paths.Length,Is.EqualTo(1));
        }

        [Test]
        public void EmptyAndDuplicateListsAreRejected()
        {
            Assert.That(LevelBuildSceneRules.Check(new string[0],new LevelBuildReference[0]).Count,Is.EqualTo(1));
            Assert.That(LevelBuildSceneRules.Check(new[]{"A.unity","A.unity"},new LevelBuildReference[0]).Count,Is.EqualTo(1));
        }
    }
}
