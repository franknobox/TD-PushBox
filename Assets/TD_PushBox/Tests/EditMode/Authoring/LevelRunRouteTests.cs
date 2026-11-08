using System.Linq;
using NUnit.Framework;
using TD.PushBox.Frontend;

namespace TD.PushBox.Tests
{
    public sealed class LevelRunRouteTests
    {
        [Test]
        public void RouteUsesNumericChapterAndLevelOrderForConfiguredEntries()
        {
            var route=LevelRunRoute.Sort(new[]{new LevelRunStep("1-10","Ten"),new LevelRunStep("1-2","Two"),
                new LevelRunStep("1-1","One"),new LevelRunStep("2-1","Other"),new LevelRunStep("1-3",null),
                new LevelRunStep("abc","Invalid"),new LevelRunStep("1-0","Zero")});
            CollectionAssert.AreEqual(new[]{"1-1","1-2","1-10","2-1"},route.Select(step=>step.number));
            Assert.That(LevelRunRoute.Next(route,"1-10","Ten").number,Is.EqualTo("2-1"));
        }

        [Test]
        public void LastOrUnmatchedLevelHasNoNextAndSameSceneAliasesStillAdvanceByNumber()
        {
            var route=LevelRunRoute.Sort(new[]{new LevelRunStep("1-1","Same"),new LevelRunStep("1-2","Same"),new LevelRunStep("1-3","Three")});
            Assert.That(LevelRunRoute.Next(route,"1-1","Same").number,Is.EqualTo("1-2"));
            Assert.That(LevelRunRoute.Next(route,"1-2","Same").scenePath,Is.EqualTo("Three"));
            Assert.That(LevelRunRoute.Next(route,"1-3","Three"),Is.Null);
            Assert.That(LevelRunRoute.Next(route,"1-1","Unrelated"),Is.Null);
            Assert.That(LevelRunRoute.Next(route,"Other","Same"),Is.Null);
        }

        [Test]
        public void DuplicateNumbersAreNotSilentlyMappedToAnArbitraryScene()
        {
            var route=LevelRunRoute.Sort(new[]{new LevelRunStep("1-1","One"),new LevelRunStep("1-2","Two"),new LevelRunStep("1-2","OtherTwo")});
            Assert.That(route.Length,Is.EqualTo(1));
            Assert.That(LevelRunRoute.Next(route,"1-1","One"),Is.Null);
        }
    }
}
