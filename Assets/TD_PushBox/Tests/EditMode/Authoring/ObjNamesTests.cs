using NUnit.Framework;

namespace JobTest.TacticsVoxel
{
    public sealed class ObjNamesTests
    {
        [TestCase("近战守卫（2）","Guard (2)")]
        [TestCase("深棕_03_07","Soil_D_03_07")]
        [TestCase("关卡选择界面","SelectUI")]
        [TestCase("按钮内容","Content")]
        [TestCase("Custom_01","Custom_01")]
        public void LabelsAndCopyNumbersProduceEnglishHierarchyNames(string label,string expected)
        {Assert.That(ObjNames.En(label),Is.EqualTo(expected));}

        [Test]
        public void CustomDisplayLabelUsesThePrefabNameWithoutGuessingItsTranslation()
        {
            Assert.That(ObjNames.Label("作者自定义显示名称","MyCrate"),Is.EqualTo("MyCrate"));
            Assert.That(ObjNames.HasHan("MyCrate"),Is.False);
        }
    }
}
