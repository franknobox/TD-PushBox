using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD.PushBox
{
    public static class PushBoxRules
    {
        private static readonly Vector2Int[] AttackDirections={Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};

        public static IEnumerable<Vector2Int> AttackCells(PushBoxState state,PushBoxEntity enemy)
        {
            if(enemy==null||enemy.dead||enemy.kind!=PushBoxKind.Enemy) yield break;
            bool fire=enemy.attack==PushBoxAttack.DragonFire;
            int range=fire?Math.Max(state.size.x,state.size.y):enemy.attack==PushBoxAttack.Ranged?2:1;
            foreach(var direction in AttackDirections)
            {
                if(fire&&direction!=enemy.facing) continue;
                for(int step=1;step<=range;step++)
                {
                    var p=enemy.position+direction*step;
                    if(!state.Inside(p)) break;
                    if(state.cells.TryGetValue(p,out var cell)&&(cell.blocksSight||cell.supports&&!cell.walkable)) break;
                    if(state.entities.Any(item=>item!=enemy&&item.IsSolid&&item.kind!=PushBoxKind.Actor&&item.kind!=PushBoxKind.Enemy&&item.Covers(p))) break;
                    yield return p;
                }
            }
        }
        public static bool CanStand(PushBoxState state,Vector2Int p,string ignoreA=null,string ignoreB=null)=>
            state.Inside(p)&&state.cells.TryGetValue(p,out var cell)&&cell.supports&&cell.walkable&&state.SolidAt(p,ignoreA,ignoreB)==null;

        private static bool CanStep(PushBoxState state,Vector2Int from,Vector2Int to)=>
            state.cells.TryGetValue(from,out var first)&&state.cells.TryGetValue(to,out var second)
            &&first.EdgeHeight(to-from)==second.EdgeHeight(from-to);

        public static bool TryMove(PushBoxState state,string id,Vector2Int direction,out string error)
        {
            error=null;var actor=state.Find(id);
            if(state.Failed) {error="角色已死亡，请按 Ctrl+Z 撤销。";return false;}
            if(state.Cleared) {error="关卡已完成。";return false;}
            if(actor==null||actor.kind!=PushBoxKind.Actor) {error="没有可操作的角色。";return false;}
            if(Math.Abs(direction.x)+Math.Abs(direction.y)!=1) {error="只能向上下左右移动。";return false;}
            actor.facing=direction;
            if(actor.role==PushBoxRole.Mage)
            {
                var target=FindSwapTarget(state,actor,direction);
                if(target!=null)
                {
                    if(!CanStand(state,target.position,actor.id,target.id)||!CanStand(state,actor.position,actor.id,target.id))
                    {error="换位落点被阻挡。";return false;}
                    (actor.position,target.position)=(target.position,actor.position);
                    RefreshMechanisms(state);ResolveContacts(state);RefreshMechanisms(state);return true;
                }
            }
            Vector2Int from=actor.position,to=from+direction;
            var blocker=state.SolidAt(to,actor.id);
            if(blocker!=null)
            {
                if(actor.role!=PushBoxRole.Warrior)
                {error="前方被占用。";return false;}
                var chain=new List<PushBoxEntity>();var p=to;
                while((blocker=state.SolidAt(p,actor.id))!=null)
                {
                    if(chain.Count==3||!blocker.movable||blocker.footprint!=Vector2Int.one
                        ||(blocker.kind!=PushBoxKind.Crate&&blocker.kind!=PushBoxKind.Actor&&blocker.kind!=PushBoxKind.Enemy))
                    {error="最多推动三个连续的可移动物体。";return false;}
                    chain.Add(blocker);p+=direction;
                }
                if(!CanStand(state,p)||!CanStep(state,from,to)||chain.Any(item=>
                    !state.cells.TryGetValue(item.position+direction,out var cell)||!cell.supports||!cell.walkable
                    ||!CanStep(state,item.position,item.position+direction)))
                {error="连推路径或终点不可通行。";return false;}
                foreach(var item in chain) item.position+=direction;
            }
            else if(!CanStand(state,to,actor.id)||!CanStep(state,from,to))
            {error="前方没有可通行地面，或高差不连通。";return false;}

            if(actor.role==PushBoxRole.Thief)
            {
                var target=state.SolidAt(from-direction,actor.id);
                if(target!=null&&(target.kind==PushBoxKind.Crate||target.kind==PushBoxKind.Actor)
                    &&target.movable&&target.footprint==Vector2Int.one&&CanStep(state,target.position,from)) target.position=from;
            }
            actor.position=to;RefreshMechanisms(state);ResolveContacts(state);RefreshMechanisms(state);return true;
        }

        public static void ResolveContacts(PushBoxState state)
        {
            var victims=new HashSet<PushBoxEntity>();
            state.fireRays.Clear();
            state.strikes.Clear();
            foreach(var enemy in state.entities.Where(item=>item.kind==PushBoxKind.Enemy&&!item.dead))
            {
                var cells=AttackCells(state,enemy).ToArray();
                var targets=state.entities.Where(item=>!item.dead&&item!=enemy
                    &&(item.kind==PushBoxKind.Actor||item.kind==PushBoxKind.Enemy)&&cells.Any(item.Covers)).ToArray();
                victims.UnionWith(targets);
                if(enemy.attack!=PushBoxAttack.DragonFire)
                    foreach(var target in targets) state.strikes.Add(new PushBoxStrike(enemy.id,target.id));
                if(enemy.attack==PushBoxAttack.DragonFire&&targets.Length>0)
                    state.fireRays.Add(new PushBoxFireRay(enemy.id,cells.Length));
            }
            foreach(var victim in victims) victim.dead=true;
        }

        public static PushBoxEntity FindSwapTarget(PushBoxState state,PushBoxEntity actor,Vector2Int direction)
        {
            for(Vector2Int p=actor.position+direction;state.Inside(p);p+=direction)
            {
                if(state.cells.TryGetValue(p,out var cell)&&(cell.blocksSight||cell.supports&&!cell.walkable)) return null;
                var target=state.SolidAt(p,actor.id);
                if(target!=null) return target.CanSwap?target:null;
            }
            return null;
        }

        public static void RefreshMechanisms(PushBoxState state)
        {
            // Crushing can release another pressure plate; settle the whole chain in this action.
            while(RefreshMechanismsOnce(state)) {}
        }
        private static bool RefreshMechanismsOnce(PushBoxState state)
        {
            bool crushed=false;
            bool failed=state.Failed;
            foreach(var goal in state.entities.Where(entity=>entity.kind==PushBoxKind.Goal))
            {
                goal.goalDisabled=failed;
                goal.pressed=!failed&&state.entities.Any(entity=>entity.kind==PushBoxKind.Actor&&!entity.dead&&goal.Covers(entity.position));
            }
            foreach(var plate in state.entities.Where(entity=>entity.kind==PushBoxKind.Plate))
                plate.pressed=state.entities.Any(entity=>!entity.dead&&(entity.kind==PushBoxKind.Actor||entity.kind==PushBoxKind.Crate||entity.kind==PushBoxKind.Enemy)&&entity.position==plate.position);
            foreach(var door in state.entities.Where(entity=>entity.kind==PushBoxKind.Door))
            {
                if(door.doorRule==PushBoxDoorRule.AllEnemiesDefeated)
                {
                    door.requestedOpen=!state.entities.Any(entity=>entity.kind==PushBoxKind.Enemy&&!entity.dead);
                    crushed|=CloseDoor(state,door);
                    continue;
                }
                bool all=door.inputs.Count>0&&door.inputs.All(id=>state.Find(id)?.pressed==true);
                if(door.inputs.Count>0)
                {
                    if(door.toggleOnPress) {if(all&&!door.signal) door.requestedOpen=!door.requestedOpen;}
                    else door.requestedOpen=all;
                }
                door.signal=all;
                crushed|=CloseDoor(state,door);
            }
            return crushed;
        }
        private static bool CloseDoor(PushBoxState state,PushBoxEntity door)
        {
            bool occupied=state.entities.Any(entity=>entity.id!=door.id&&entity.IsSolid&&entity.kind!=PushBoxKind.Door
                &&!CanCrush(entity)&&door.Covers(entity.position));
            door.open=door.requestedOpen||occupied;
            bool crushed=false;
            if(!door.open)
                foreach(var victim in state.entities.Where(entity=>!entity.dead&&CanCrush(entity)&&door.Covers(entity.position)))
                {victim.dead=true;crushed=true;}
            return crushed;
        }
        private static bool CanCrush(PushBoxEntity entity)=>entity.kind==PushBoxKind.Actor||entity.kind==PushBoxKind.Enemy
            ||entity.kind==PushBoxKind.Crate&&entity.crushable;

        public static void Validate(PushBoxState state)
        {
            if(state.size.x<1||state.size.y<1) throw new InvalidOperationException("关卡尺寸无效。");
            var ids=new HashSet<string>();
            foreach(var entity in state.entities)
            {
                if(string.IsNullOrWhiteSpace(entity.id)||!ids.Add(entity.id)) throw new InvalidOperationException("角色或物件标识缺失或重复："+entity.id);
                if(entity.footprint.x<1||entity.footprint.y<1) throw new InvalidOperationException("物件占地尺寸无效："+entity.id);
                for(int y=0;y<entity.footprint.y;y++) for(int x=0;x<entity.footprint.x;x++)
                {
                    var p=entity.position+new Vector2Int(x,y);
                    if(!state.Inside(p)||!state.cells.TryGetValue(p,out var cell)||!cell.supports||!cell.walkable)
                        throw new InvalidOperationException("角色或物件不在有效地面上："+entity.id);
                    if(entity.IsSolid&&state.SolidAt(p,entity.id)!=null) throw new InvalidOperationException("关卡中有重叠占位："+p);
                }
                if(entity.kind==PushBoxKind.Door&&entity.doorRule==PushBoxDoorRule.PressurePlates&&entity.inputs.Any(id=>state.Find(id)?.kind!=PushBoxKind.Plate))
                    throw new InvalidOperationException("门只能关联本关卡的压力板："+entity.id);
            }
        }
    }

    public sealed class PushBoxSession
    {
        private readonly List<PushBoxState> history=new();
        public PushBoxState State {get;private set;}
        public bool CanUndo=>history.Count>0;
        public PushBoxSession(PushBoxState initial)
        {
            State=initial.Copy();PushBoxRules.Validate(State);var before=State.Copy();
            PushBoxRules.RefreshMechanisms(State);PushBoxRules.ResolveContacts(State);
            if(State.entities.Any(entity=>entity.dead)) history.Add(before);
            PushBoxRules.RefreshMechanisms(State);
        }
        public bool Move(string actor,Vector2Int direction,out string error)
        {
            var previous=State.Copy();
            if(!PushBoxRules.TryMove(State,actor,direction,out error)) return false;
            if(history.Count==512) history.RemoveAt(0);history.Add(previous);return true;
        }
        public bool Undo()
        {if(!CanUndo) return false;State=history[history.Count-1];history.RemoveAt(history.Count-1);return true;}

        public PushBoxSessionCheckpoint SaveCheckpoint(PushBoxState initial,string selectedId)=>new()
        {
            initial=PushBoxSavedState.From(initial),current=PushBoxSavedState.From(State),
            history=history.Select(PushBoxSavedState.From).ToList(),selectedId=selectedId
        };

        private PushBoxSession() {}
        public static PushBoxSession RestoreCheckpoint(PushBoxSessionCheckpoint checkpoint)
        {
            var session=new PushBoxSession{State=checkpoint.current.Read()};
            PushBoxRules.Validate(session.State);
            session.history.AddRange(checkpoint.history.Select(saved=>saved.Read()));
            return session;
        }
    }
}
