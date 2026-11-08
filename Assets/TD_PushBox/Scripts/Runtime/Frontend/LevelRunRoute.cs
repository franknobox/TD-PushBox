using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TD.PushBox.Frontend
{
    [Serializable]
    public sealed class LevelRunStep
    {
        public string number,scenePath;
        public LevelRunStep(string number,string scenePath) {this.number=number;this.scenePath=scenePath;}
    }

    public static class LevelRunRoute
    {
        private static (int chapter,int level) Order(string number)
        {
            var parts=number?.Split('-');
            return parts?.Length==2&&int.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out int chapter)&&chapter>0
                &&int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out int index)&&index>0?(chapter,index):(0,0);
        }

        public static LevelRunStep[] Sort(IEnumerable<LevelRunStep> entries)=>entries
            .Where(step=>step!=null&&Order(step.number).chapter>0&&!string.IsNullOrEmpty(step.scenePath))
            .GroupBy(step=>Order(step.number))
            // Duplicate numbers are ambiguous; do not silently choose a different scene.
            .Where(group=>group.Count()==1).OrderBy(group=>group.Key).Select(group=>group.First()).ToArray();

        public static LevelRunStep Next(IReadOnlyList<LevelRunStep> route,string number,string scenePath)
        {
            for(int i=0;i<route.Count;i++)
                if(route[i].number==number&&route[i].scenePath==scenePath) return i+1<route.Count?route[i+1]:null;
            return null;
        }
    }
}
