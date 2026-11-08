using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD.PushBox
{
    public enum PushBoxRole { [InspectorName("力士")] Warrior, [InspectorName("盗贼")] Thief, [InspectorName("法师")] Mage, [InspectorName("盾兵")] Shield }
    public enum PushBoxKind { Actor, [InspectorName("箱子")] Crate, [InspectorName("阻挡物")] Blocker,
        [InspectorName("压力板")] Plate, [InspectorName("门")] Door, [InspectorName("目标点")] Goal, [InspectorName("敌人")] Enemy,
        [InspectorName("关卡入口")] Entrance }

    public enum PushBoxAttack { [InspectorName("近身攻击")] Contact=0, [InspectorName("直线喷火")] DragonFire=1, [InspectorName("四向远程攻击")] Ranged=2 }
    public enum PushBoxDoorRule { [InspectorName("压力板")] PressurePlates, [InspectorName("清空敌方")] AllEnemiesDefeated }
    public readonly struct PushBoxFireRay
    {
        public readonly string source;
        public readonly int cells;
        public PushBoxFireRay(string id,int count) {source=id;cells=count;}
    }
    public readonly struct PushBoxStrike
    {
        public readonly string source,target;
        public PushBoxStrike(string source,string target) {this.source=source;this.target=target;}
    }
    [Serializable]
    public sealed class PushBoxCell
    {
        public Vector2Int position,uphill;
        public int height;
        public bool supports=true,walkable=true,blocksSight;
        public long EdgeHeight(Vector2Int direction)=>2L*height+(uphill==Vector2Int.zero?0:1)+(long)uphill.x*direction.x+(long)uphill.y*direction.y;
        public PushBoxCell Copy()=>(PushBoxCell)MemberwiseClone();
    }

    [Serializable]
    public sealed class PushBoxEntity
    {
        public string id;
        public PushBoxKind kind;
        public PushBoxRole role;
        public PushBoxAttack attack;
        public PushBoxDoorRule doorRule;
        public Vector2Int position,facing=Vector2Int.down,footprint=Vector2Int.one;
        public bool movable,open,requestedOpen,toggleOnPress,signal,pressed;
        public bool dead;
        public bool crushable=true;
        public bool goalDisabled;
        public List<string> inputs=new();
        public bool IsSolid=>!dead&&(kind==PushBoxKind.Actor||kind==PushBoxKind.Enemy||kind==PushBoxKind.Crate||kind==PushBoxKind.Blocker||kind==PushBoxKind.Door&&!open);
        public bool CanSwap=>movable&&footprint==Vector2Int.one&&(kind==PushBoxKind.Actor||kind==PushBoxKind.Crate);
        public bool Covers(Vector2Int p)=>p.x>=position.x&&p.y>=position.y&&(long)p.x<(long)position.x+footprint.x&&(long)p.y<(long)position.y+footprint.y;
        public PushBoxEntity Copy()
        {var result=(PushBoxEntity)MemberwiseClone();result.inputs=new List<string>(inputs);return result;}
    }

    public sealed class PushBoxState
    {
        public Vector2Int size;
        public readonly Dictionary<Vector2Int,PushBoxCell> cells=new();
        public readonly List<PushBoxEntity> entities=new();
        public readonly List<PushBoxFireRay> fireRays=new();
        public readonly List<PushBoxStrike> strikes=new();
        public bool Failed=>entities.Any(entity=>entity.kind==PushBoxKind.Actor&&entity.dead);
        public bool Cleared
        {
            get
            {
                if(Failed) return false;
                bool hasGoal=false;
                foreach(var entity in entities)
                    if(entity.kind==PushBoxKind.Goal) {hasGoal=true;if(!entity.pressed||entity.goalDisabled) return false;}
                return hasGoal;
            }
        }
        public bool Inside(Vector2Int p)=>p.x>=0&&p.y>=0&&p.x<size.x&&p.y<size.y;
        public PushBoxEntity Find(string id)=>entities.Find(entity=>entity.id==id);
        public PushBoxEntity SolidAt(Vector2Int p,string ignoreA=null,string ignoreB=null)=>entities.Find(entity=>entity.IsSolid&&entity.id!=ignoreA&&entity.id!=ignoreB&&entity.Covers(p));
        public PushBoxState Copy()
        {
            var result=new PushBoxState{size=size};
            foreach(var cell in cells) result.cells.Add(cell.Key,cell.Value.Copy());
            result.entities.AddRange(entities.Select(entity=>entity.Copy()));result.fireRays.AddRange(fireRays);
            result.strikes.AddRange(strikes);return result;
        }
    }
}
