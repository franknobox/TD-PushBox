using System;
using System.Collections.Generic;
using System.Linq;

namespace JobTest.TacticsVoxel
{
    public static class ObjNames
    {
        // Palette labels stay localized; hierarchy names are short, stable identifiers.
        private static readonly Dictionary<string,string> Names=new()
        {
            {"力士","Fighter"},{"灌木","Shrub"},
            {"关卡根节点","LevelRoot"},{"关卡选择棋盘","SelectBoard"},{"地形","Tiles"},{"角色","Units"},
            {"箱子与机关","Objs"},{"环境细节","Details"},{"环境","Env"},{"运行时对象","RT"},{"系统","Sys"},{"光照","Lights"},
            {"战士","Warrior"},{"盗贼","Thief"},{"法师","Mage"},{"盾兵","Shield"},{"近战守卫","Guard"},{"远程哨兵","Sentry"},
            {"古木妖怪","WoodBoss"},{"远古妖怪","WoodBoss"},{"赤角妖怪","Oni"},{"苔石妖怪","Golem"},
            {"水晶","Crystal"},{"关卡入口","Entrance"},{"宝箱","Chest"},{"箱子","Crate"},{"压力板","Plate"},{"石门","StoneGate"},{"门","Door"},{"祭坛","Altar"},
            {"火龙石像","Dragon"},{"火龙雕塑","Dragon"},{"树木","Tree"},{"石块","Rock"},{"花草","Flora"},{"目标区","Goal"},{"目标点","Goal"},{"撤离点","Exit"},
            {"深棕","Soil_D"},{"浅棕","Soil_L"},{"浅灰","Stone_L"},{"深绿","Grass_D"},{"浅绿","Grass_L"},
            {"高地","High"},{"北坡","Ramp_N"},{"东坡","Ramp_E"},{"南坡","Ramp_S"},{"西坡","Ramp_W"},{"深渊","Pit"},
            {"开始界面控制","MenuCtrl"},{"开始界面","StartUI"},{"关卡选择界面","SelectUI"},{"战棋界面","BattleUI"},
            {"主菜单","Menu"},{"菜单内容","MenuBody"},{"游戏标题","GameTitle"},{"标题短线","TitleLine"},{"标题","Title"},
            {"开始游戏","Start"},{"退出游戏","Quit"},{"关卡选择","LevelSelect"},{"返回开始","Back"},{"返回箭头","BackIcon"},
            {"按钮内容","Content"},{"方向箭头","Arrow"},{"选中边线","Accent"},{"版本标记","Version"},{"文字","Label"},
            {"自制关卡","CustomLevels"},{"展开收起","Toggle"},{"金色边线","Accent"},{"展开箭头","Arrow"},
            {"下拉列表","Dropdown"},{"可视区域","Viewport"},{"关卡条目","Items"},{"空列表","Empty"},
            {"滚动条","ScrollBar"},{"滑块","Handle"},{"条目模板","RowTpl"},{"名称","Label"},{"分隔线","Divider"},
            {"林间庭院","Courtyard"},{"草地与小路","Ground"},{"遗迹铺地","Paving"},{"残墙与石门","Ruins"},
            {"林木与花草","Forest"},{"溪流与木桥","Stream"},{"地面与庭院边沿","Ground"},
            {"庭院地面","Ground"},{"庭院基座","Base"},{"庭院侧沿","SideRim"},{"庭院后沿","BackRim"},{"入口侧沿","EntryRim"},
            {"入口步石","EntryStep"},{"散落步石","Step"},{"林间步石","Step"},{"庭院石砖","Paver"},
            {"低矮残墙","LowWall"},{"残墙墙脚","WallBase"},{"残墙顶石","WallCap"},{"墙顶石","WallCap"},
            {"角柱顶","PillarCap"},{"角柱","Pillar"},{"远侧石门","Gate"},{"庭院碎石","Rubble"},
            {"庭院乔木","Tree"},{"林间花草","Flora"},{"林地","Ground"},
            {"主木箱","MainCrate"},{"备用木箱","SpareCrate"},{"墙边木箱","WallCrate"},{"庭院木箱","Crate"},
            {"叠放木箱","StackCrate"},{"左侧木箱","LeftCrate"},{"箱子目标纹样","GoalInlay"},
            {"金色嵌线","GoldLine"},{"角部嵌石","Corner"},{"溪岸石","BankRock"},{"溪岸","Bank"},{"溪水","Water"},
            {"水面反光","Glint"},{"桥面木板","Plank"},{"木桥横梁","Beam"},
            {"庭院光照","Lights"},{"主光源","Sun"},{"日光","Sun"},{"木箱补光","Fill"},
            {"敌方变化 · ","EnemyDelta_"},{"新增敌方 · ","Spawn_"},{"机关变化 · ","ObjDelta_"},
            {"Boss 出生点","BossSpawn"},{"场地变化","TerrainDelta"}
        };
        private static readonly KeyValuePair<string,string>[] Ordered=Names.OrderByDescending(item=>item.Key.Length).ToArray();

        public static bool HasHan(string value)=>value!=null&&value.Any(ch=>ch>='\u3400'&&ch<='\u9fff');
        public static string Label(string label,string prefabName)=>label!=null&&Names.TryGetValue(label,out string name)?name:En(prefabName);
        public static string En(string value)
        {
            if(string.IsNullOrEmpty(value)) return value;
            string result=value.Replace("（"," (").Replace("）",")");
            if(!HasHan(result)) return result;
            foreach(var item in Ordered) result=result.Replace(item.Key,item.Value);
            if(HasHan(result)) throw new ArgumentException("物体名称没有英文映射，请使用简短英文或拼音："+value);
            return result;
        }
    }
}
