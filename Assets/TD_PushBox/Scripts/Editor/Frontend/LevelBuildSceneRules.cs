using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JobTest.TacticsVoxel
{
    public readonly struct LevelBuildReference
    {
        public readonly string owner,target;
        public LevelBuildReference(string owner,string target) {this.owner=owner;this.target=target;}
    }

    public static class LevelBuildSceneRules
    {
        public static List<string> Check(IEnumerable<string> enabledScenes,IEnumerable<LevelBuildReference> references)
        {
            var paths=enabledScenes.ToArray();var issues=new List<string>();
            if(paths.Length==0) issues.Add("Build Settings 中没有启用的场景。");
            foreach(var group in paths.GroupBy(path=>path,StringComparer.Ordinal).Where(group=>group.Count()>1))
                issues.Add("构建场景重复："+group.Key);
            foreach(var reference in references)
            {
                bool fullPath=reference.target!=null&&(reference.target.Contains("/")||reference.target.Contains("\\"));
                bool included=!string.IsNullOrEmpty(reference.target)&&paths.Any(path=>path==reference.target
                    ||!fullPath&&Path.GetFileNameWithoutExtension(path)==reference.target);
                if(!included) issues.Add(reference.owner+"：目标场景未启用，请在 File > Build Settings 中添加并勾选："+reference.target);
            }
            return issues;
        }
    }
}
