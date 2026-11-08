using NUnit.Framework;
using UnityEngine;

namespace TD.PushBox.Tests
{
    public sealed class PushBoxRulesTests
    {
        [Test]
        public void ClosingDoorKillsCharacterDisablesGoalsAndCanBeUndone()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,role=PushBoxRole.Shield,movable=true,position=new Vector2Int(5,3)});
            state.entities.Add(new PushBoxEntity{id="plate",kind=PushBoxKind.Plate,position=new Vector2Int(5,3)});
            state.entities.Add(new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(2,2),open=true,inputs=new(){"plate"}});
            state.entities.Add(new PushBoxEntity{id="goal",kind=PushBoxKind.Goal,position=new Vector2Int(0,0)});
            var session=new PushBoxSession(state);
            Assert.That(session.State.Failed,Is.False);
            Assert.That(session.Move("friend",Vector2Int.down,out _),Is.True);
            Assert.That(session.State.Find("actor").dead,Is.True);Assert.That(session.State.Find("door").open,Is.False);
            Assert.That(session.State.Failed,Is.True);Assert.That(session.State.Find("goal").goalDisabled,Is.True);
            Assert.That(session.Move("friend",Vector2Int.left,out _),Is.False);
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Failed,Is.False);Assert.That(session.State.Find("door").open,Is.True);
            Assert.That(session.State.Find("goal").goalDisabled,Is.False);
        }

        [Test]
        public void ClosingDoorCrushesWoodAndUndoRestoresTheCrateAndDoor()
        {
            var state=DoorCrate(true);var session=new PushBoxSession(state);
            Assert.That(session.State.Find("door").open,Is.True);
            Assert.That(session.Move("actor",Vector2Int.left,out _),Is.True);
            Assert.That(session.State.Find("box").dead,Is.True);Assert.That(session.State.Find("door").open,Is.False);
            Assert.That(session.State.Failed,Is.False);
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Find("box").dead,Is.False);Assert.That(session.State.Find("door").open,Is.True);
        }

        [Test]
        public void CrystalCannotBeCrushedAndKeepsTheDoorOpen()
        {
            var session=new PushBoxSession(DoorCrate(false));
            Assert.That(session.Move("actor",Vector2Int.left,out _),Is.True);
            Assert.That(session.State.Find("box").dead,Is.False);
            Assert.That(session.State.Find("door").requestedOpen,Is.False);
            Assert.That(session.State.Find("door").open,Is.True);
        }

        [Test]
        public void CrushedWoodReleasesItsPlateAndSettlesOtherDoorsImmediately()
        {
            var state=DoorCrate(true);
            state.entities.Add(new PushBoxEntity{id="under",kind=PushBoxKind.Plate,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="other",kind=PushBoxKind.Door,position=new Vector2Int(6,4),open=true,inputs=new(){"under"}});
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.left,out _),Is.True);
            Assert.That(session.State.Find("under").pressed,Is.False);Assert.That(session.State.Find("other").open,Is.False);
        }

        private static PushBoxState DoorCrate(bool crushable)
        {
            var state=Board(PushBoxRole.Shield);Box(state,new Vector2Int(4,2)).crushable=crushable;
            state.entities.Add(new PushBoxEntity{id="plate",kind=PushBoxKind.Plate,position=new Vector2Int(2,2)});
            state.entities.Add(new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(4,2),open=true,inputs=new(){"plate"}});
            return state;
        }

        [Test]
        public void SessionCheckpointKeepsHistoryDeathDoorsAndInitialLayout()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="a",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="b",kind=PushBoxKind.Enemy,position=new Vector2Int(5,3)});
            state.entities.Add(new PushBoxEntity{id="seal",kind=PushBoxKind.Door,doorRule=PushBoxDoorRule.AllEnemiesDefeated,position=new Vector2Int(0,4)});
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            var saved=session.SaveCheckpoint(state,"actor");
            var restored=PushBoxSession.RestoreCheckpoint(saved);
            Assert.That(saved.selectedId,Is.EqualTo("actor"));
            Assert.That(restored.State.Find("a").dead&&restored.State.Find("b").dead,Is.True);
            Assert.That(restored.State.Find("seal").open,Is.True);
            Assert.That(restored.Undo(),Is.True);
            Assert.That(restored.State.Find("a").dead||restored.State.Find("b").dead,Is.False);
            Assert.That(restored.State.Find("seal").open,Is.False);
            Assert.That(restored.CanUndo,Is.False);
            Assert.That(restored.State.Find("actor").position,Is.EqualTo(state.Find("actor").position));
            Assert.That(saved.initial.Read().Find("actor").position,Is.EqualTo(state.Find("actor").position));
            Assert.That(session.State.Find("a").dead,Is.True,"恢复的状态不得与原棋局共用可变实体。");
        }

        [Test]
        public void SessionCheckpointRetainsToggleSignalAndFailedGoalState()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="plate",kind=PushBoxKind.Plate,position=new Vector2Int(2,3)});
            state.entities.Add(new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(6,4),toggleOnPress=true,inputs=new(){"plate"}});
            state.entities.Add(new PushBoxEntity{id="goal",kind=PushBoxKind.Goal,position=new Vector2Int(0,0)});
            state.entities.Add(new PushBoxEntity{id="enemy",kind=PushBoxKind.Enemy,position=new Vector2Int(3,3)});
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.up,out _),Is.True);
            var restored=PushBoxSession.RestoreCheckpoint(session.SaveCheckpoint(state,"actor"));
            Assert.That(restored.State.Failed,Is.True);Assert.That(restored.State.Find("goal").goalDisabled,Is.True);
            Assert.That(restored.State.Find("door").open,Is.True);
            Assert.That(restored.State.Find("door").signal,Is.False);
            Assert.That(restored.Undo(),Is.True);Assert.That(restored.State.Failed,Is.False);
            Assert.That(restored.State.Find("door").requestedOpen,Is.False);
        }

        [Test]
        public void EnemySealIgnoresPressureAndOpensOnlyWhenNoLivingEnemyRemains()
        {
            var state=Board(PushBoxRole.Shield);
            var gate=new PushBoxEntity{id="seal",kind=PushBoxKind.Door,doorRule=PushBoxDoorRule.AllEnemiesDefeated,position=new Vector2Int(6,4),open=true};
            gate.inputs.Add("unused");state.entities.Add(gate);
            var enemy=new PushBoxEntity{id="enemy",kind=PushBoxKind.Enemy,position=new Vector2Int(5,4)};state.entities.Add(enemy);
            PushBoxRules.RefreshMechanisms(state);Assert.That(gate.open,Is.False);
            enemy.dead=true;PushBoxRules.RefreshMechanisms(state);Assert.That(gate.open,Is.True);
            enemy.dead=false;PushBoxRules.RefreshMechanisms(state);Assert.That(gate.open,Is.False);
        }

        [Test]
        public void ClearingLastEnemiesOpensSealAndUndoRestoresClosedSeal()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="a",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="b",kind=PushBoxKind.Enemy,position=new Vector2Int(5,3)});
            state.entities.Add(new PushBoxEntity{id="seal",kind=PushBoxKind.Door,doorRule=PushBoxDoorRule.AllEnemiesDefeated,position=new Vector2Int(0,4)});
            var session=new PushBoxSession(state);Assert.That(session.State.Find("seal").open,Is.False);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("a").dead&&session.State.Find("b").dead,Is.True);
            Assert.That(session.State.Find("seal").open,Is.True);
            Assert.That(session.Undo(),Is.True);Assert.That(session.State.Find("seal").open,Is.False);
        }

        [Test]
        public void EnemySealWithoutEnemiesStartsReadyToOpen()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="seal",kind=PushBoxKind.Door,doorRule=PushBoxDoorRule.AllEnemiesDefeated,position=new Vector2Int(0,4)});
            Assert.That(new PushBoxSession(state).State.Find("seal").open,Is.True);
        }

        private static PushBoxEntity Dragon(PushBoxState state,Vector2Int point,Vector2Int facing)
        {var dragon=new PushBoxEntity{id="dragon",kind=PushBoxKind.Enemy,attack=PushBoxAttack.DragonFire,position=point,facing=facing,movable=true};state.entities.Add(dragon);return dragon;}

        [Test]
        public void EnemyStrikesRecordTheActualAttackerAndCopyWithoutSharingTheList()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,position=new Vector2Int(3,2)});
            PushBoxRules.ResolveContacts(state);
            Assert.That(state.strikes.Count,Is.EqualTo(1));
            Assert.That(state.strikes[0].source,Is.EqualTo("guard"));Assert.That(state.strikes[0].target,Is.EqualTo("actor"));
            var copy=state.Copy();state.strikes.Clear();Assert.That(copy.strikes.Count,Is.EqualTo(1));
        }

        [Test]
        public void DoorDeathAndDragonFireDoNotProduceMeleeStrikes()
        {
            var state=DoorCrate(true);state.Find("actor").dead=true;
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,position=new Vector2Int(3,2)});
            PushBoxRules.ResolveContacts(state);Assert.That(state.strikes,Is.Empty);
            state=Board(PushBoxRole.Shield);Dragon(state,new Vector2Int(6,2),Vector2Int.left);
            PushBoxRules.ResolveContacts(state);Assert.That(state.Find("actor").dead,Is.True);Assert.That(state.strikes,Is.Empty);
        }

        [Test]
        public void SentryAttacksTwoCellsInCardinalDirectionsButNotDiagonalsOrThirdCell()
        {
            foreach(var point in new[]{new Vector2Int(4,2),new Vector2Int(3,3),new Vector2Int(5,2)})
            {
                var state=Board(PushBoxRole.Shield);state.Find("actor").position=point;
                var enemy=new PushBoxEntity{id="sentry",kind=PushBoxKind.Enemy,attack=PushBoxAttack.Ranged,position=new Vector2Int(2,2)};
                state.entities.Add(enemy);PushBoxRules.ResolveContacts(state);
                Assert.That(state.Find("actor").dead,Is.EqualTo(point==new Vector2Int(4,2)));
            }
        }

        [Test]
        public void AttackCellsStopBeforeObstaclesAndReflectOpenDoorsAndDeath()
        {
            var state=Board(PushBoxRole.Shield);var dragon=Dragon(state,new Vector2Int(6,2),Vector2Int.left);
            var door=new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(4,2)};state.entities.Add(door);
            CollectionAssert.AreEqual(new[]{new Vector2Int(5,2)},PushBoxRules.AttackCells(state,dragon));
            door.open=true;Assert.That(System.Linq.Enumerable.Count(PushBoxRules.AttackCells(state,dragon)),Is.EqualTo(6));
            dragon.dead=true;Assert.That(PushBoxRules.AttackCells(state,dragon),Is.Empty);
        }

        [Test]
        public void SentryCannotShootThroughCrateButCharactersDoNotBlockItsRange()
        {
            var state=Board(PushBoxRole.Shield);state.Find("actor").position=new Vector2Int(4,2);
            var enemy=new PushBoxEntity{id="sentry",kind=PushBoxKind.Enemy,attack=PushBoxAttack.Ranged,position=new Vector2Int(2,2)};
            state.entities.Add(enemy);Box(state,new Vector2Int(3,2));
            PushBoxRules.ResolveContacts(state);Assert.That(state.Find("actor").dead,Is.False);
            state.Find("box").dead=true;
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,position=new Vector2Int(3,2)});
            PushBoxRules.ResolveContacts(state);
            Assert.That(state.Find("actor").dead&&state.Find("guard").dead&&enemy.dead,Is.True);
        }

        [Test]
        public void DragonHitsBothTeamsAlongItsFacingAndDoesNotAttackItsSides()
        {
            var state=Board(PushBoxRole.Shield);state.Find("actor").position=new Vector2Int(1,2);
            var dragon=Dragon(state,new Vector2Int(6,2),Vector2Int.left);
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,position=new Vector2Int(4,2)});
            PushBoxRules.ResolveContacts(state);
            Assert.That(state.Find("actor").dead&&state.Find("guard").dead,Is.True);
            Assert.That(dragon.dead,Is.False);Assert.That(state.fireRays.Count,Is.EqualTo(1));
            state=Board(PushBoxRole.Shield);Dragon(state,new Vector2Int(2,3),Vector2Int.up);
            PushBoxRules.ResolveContacts(state);Assert.That(state.Find("actor").dead,Is.False);Assert.That(state.fireRays,Is.Empty);
        }

        [Test]
        public void DragonFireStopsAtCratesTerrainAndClosedDoorsButCrossesOpenDoors()
        {
            foreach(int obstacle in new[]{0,1,2,3})
            {
                var state=Board(PushBoxRole.Shield);Dragon(state,new Vector2Int(6,2),Vector2Int.left);
                if(obstacle==0) state.cells[new Vector2Int(4,2)].blocksSight=true;
                else state.entities.Add(new PushBoxEntity{id="block",kind=obstacle==1?PushBoxKind.Crate:PushBoxKind.Door,position=new Vector2Int(4,2),open=obstacle==3});
                PushBoxRules.ResolveContacts(state);
                Assert.That(state.Find("actor").dead,Is.EqualTo(obstacle==3));
            }
        }

        [Test]
        public void PushedGuardAndFacingDragonAttackSimultaneouslyAndUndoRestoresBoth()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(4,2)});
            Dragon(state,new Vector2Int(5,3),Vector2Int.down);
            var session=new PushBoxSession(state);Assert.That(session.State.Failed,Is.False);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("guard").dead&&session.State.Find("dragon").dead,Is.True);
            Assert.That(session.State.Failed,Is.False);Assert.That(session.State.fireRays.Count,Is.EqualTo(1));
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Find("guard").dead||session.State.Find("dragon").dead,Is.False);
            Assert.That(session.State.Find("guard").position,Is.EqualTo(new Vector2Int(4,2)));
        }

        [Test]
        public void GuardAttacksDragonFromSideWithoutReceivingAnUnfiredCounterattack()
        {
            var state=Board(PushBoxRole.Shield);state.Find("actor").position=Vector2Int.zero;
            var dragon=Dragon(state,new Vector2Int(4,2),Vector2Int.up);
            state.entities.Add(new PushBoxEntity{id="guard",kind=PushBoxKind.Enemy,position=new Vector2Int(3,2)});
            PushBoxRules.ResolveContacts(state);Assert.That(dragon.dead,Is.True);Assert.That(state.Find("guard").dead,Is.False);
        }

        [Test]
        public void EntrancesAllowWalkingAndDoNotActAsGoalsOrMovableTargets()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="entry",kind=PushBoxKind.Entrance,position=new Vector2Int(2,3)});
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.up,out _),Is.True);
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(2,3)));
            Assert.That(session.State.Find("entry").IsSolid||session.State.Find("entry").CanSwap,Is.False);
            Assert.That(session.State.Cleared||session.State.Find("entry").pressed,Is.False);
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Find("entry").position,Is.EqualTo(new Vector2Int(2,3)));
        }

        [Test]
        public void AllGoalsNeedFriendlyActorsAndVictoryCanBeUndone()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,role=PushBoxRole.Shield,movable=true,position=new Vector2Int(4,3)});
            state.entities.Add(new PushBoxEntity{id="g1",kind=PushBoxKind.Goal,position=new Vector2Int(2,2)});
            state.entities.Add(new PushBoxEntity{id="g2",kind=PushBoxKind.Goal,position=new Vector2Int(4,2)});
            var session=new PushBoxSession(state);
            Assert.That(session.State.Find("g1").pressed,Is.True);Assert.That(session.State.Cleared,Is.False);
            Assert.That(session.Move("friend",Vector2Int.down,out _),Is.True);
            Assert.That(session.State.Cleared,Is.True);
            Assert.That(session.Move("actor",Vector2Int.left,out _),Is.False);
            Assert.That(session.Undo(),Is.True);Assert.That(session.State.Cleared,Is.False);
            Assert.That(session.State.Find("g1").pressed,Is.True);Assert.That(session.State.Find("g2").pressed,Is.False);
        }

        [Test]
        public void CratesAndEnemiesDoNotActivateGoals()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="g1",kind=PushBoxKind.Goal,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="g2",kind=PushBoxKind.Goal,position=new Vector2Int(6,4)});
            Box(state,new Vector2Int(4,2));
            state.entities.Add(new PushBoxEntity{id="enemy",kind=PushBoxKind.Enemy,position=new Vector2Int(6,4)});
            var session=new PushBoxSession(state);
            Assert.That(session.State.Find("g1").pressed||session.State.Find("g2").pressed,Is.False);
            Assert.That(session.State.Cleared,Is.False);Assert.That(session.State.Failed,Is.False);
        }

        [Test]
        public void FriendlyDeathDisablesEveryGoalAndUndoRestoresActivation()
        {
            var state=Board(PushBoxRole.Shield);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,role=PushBoxRole.Shield,movable=true,position=new Vector2Int(4,3)});
            state.entities.Add(new PushBoxEntity{id="g1",kind=PushBoxKind.Goal,position=new Vector2Int(2,2)});
            state.entities.Add(new PushBoxEntity{id="g2",kind=PushBoxKind.Goal,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="enemy",kind=PushBoxKind.Enemy,position=new Vector2Int(5,2)});
            var session=new PushBoxSession(state);Assert.That(session.Move("friend",Vector2Int.down,out _),Is.True);
            Assert.That(session.State.Failed,Is.True);Assert.That(session.State.Cleared,Is.False);
            Assert.That(session.State.Find("g1").goalDisabled&&session.State.Find("g2").goalDisabled,Is.True);
            Assert.That(session.State.Find("g1").pressed||session.State.Find("g2").pressed,Is.False);
            Assert.That(session.Undo(),Is.True);Assert.That(session.State.Failed,Is.False);
            Assert.That(session.State.Find("g1").goalDisabled||session.State.Find("g2").goalDisabled,Is.False);
            Assert.That(session.State.Find("g1").pressed,Is.True);
        }

        [Test]
        public void BoardWithoutGoalsDoesNotClearAutomatically()
        {
            Assert.That(new PushBoxSession(Board(PushBoxRole.Shield)).State.Cleared,Is.False);
        }

        private static PushBoxState Board(PushBoxRole role)
        {
            var state=new PushBoxState{size=new Vector2Int(7,5)};
            for(int y=0;y<5;y++) for(int x=0;x<7;x++) state.cells.Add(new Vector2Int(x,y),new PushBoxCell{position=new Vector2Int(x,y)});
            state.entities.Add(new PushBoxEntity{id="actor",kind=PushBoxKind.Actor,role=role,position=new Vector2Int(2,2),movable=true});return state;
        }
        private static PushBoxEntity Box(PushBoxState state,Vector2Int p,string id="box")
        {var box=new PushBoxEntity{id=id,kind=PushBoxKind.Crate,position=p,movable=true};state.entities.Add(box);return box;}

        [Test]
        public void ActionCuesDistinguishWalkingDraggingAndAdjacentOrDistantSwaps()
        {
            foreach(var sample in new[]{
                (PushBoxRole.Warrior,3,PushBoxActionCue.Footstep|PushBoxActionCue.CrateDrag),
                (PushBoxRole.Thief,1,PushBoxActionCue.Footstep|PushBoxActionCue.CrateDrag),
                (PushBoxRole.Mage,3,PushBoxActionCue.Swap),(PushBoxRole.Mage,5,PushBoxActionCue.Swap),
                (PushBoxRole.Shield,-1,PushBoxActionCue.Footstep),(PushBoxRole.Mage,-1,PushBoxActionCue.Footstep)})
            {
                var state=Board(sample.Item1);if(sample.Item2>=0) Box(state,new Vector2Int(sample.Item2,2));
                var before=state.Copy();Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
                Assert.That(PushBoxActionCues.Classify(before,state,"actor"),Is.EqualTo(sample.Item3),sample.Item1.ToString());
            }
        }

        [Test]
        public void MovingCrystalUsesTheCrateSound()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2)).crushable=false;
            var before=state.Copy();Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(PushBoxActionCues.Classify(before,state,"actor"),Is.EqualTo(PushBoxActionCue.Footstep|PushBoxActionCue.CrateDrag));
        }

        [Test]
        public void FailedMoveDoesNotProduceActionSounds()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));Box(state,new Vector2Int(4,2),"second");
            state.cells[new Vector2Int(5,2)].walkable=false;
            var before=state.Copy();Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.False);
            Assert.That(PushBoxActionCues.Classify(before,state,"actor"),Is.EqualTo(PushBoxActionCue.None));
        }

        [Test]
        public void ThreeMixedTargetsMoveAndPlayerDeathIsUndoable()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="enemy",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(5,3)});
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,movable=true,position=new Vector2Int(4,2)});
            Box(state,new Vector2Int(5,2),"third");
            var session=new PushBoxSession(state);Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("friend").position,Is.EqualTo(new Vector2Int(5,2)));
            Assert.That(session.State.Failed,Is.True);Assert.That(session.Move("actor",Vector2Int.left,out _),Is.False);
            Assert.That(session.Undo(),Is.True);Assert.That(session.State.Failed,Is.False);
            Assert.That(session.State.Find("enemy").position,Is.EqualTo(new Vector2Int(5,3)));
        }

        [Test]
        public void EnemyContactsKillBothSimultaneouslyAndUndoRestoresThem()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="a",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(4,2)});
            state.entities.Add(new PushBoxEntity{id="b",kind=PushBoxKind.Enemy,movable=true,position=new Vector2Int(5,3)});
            var session=new PushBoxSession(state);Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("a").dead&&session.State.Find("b").dead,Is.True);
            Assert.That(session.State.Failed,Is.False);session.Undo();
            Assert.That(session.State.Find("a").dead||session.State.Find("b").dead,Is.False);
        }

        [Test]
        public void WarriorPushIsAtomicAndUndoRestoresBothBodies()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(3,2)));
            Assert.That(session.State.Find("box").position,Is.EqualTo(new Vector2Int(4,2)));
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(session.State.Find("box").position,Is.EqualTo(new Vector2Int(3,2)));
        }

        [Test]
        public void BlockedPushAndChainPushDoNotConsumeHistoryOrMoveAnything()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));Box(state,new Vector2Int(4,2),"second");
            Box(state,new Vector2Int(5,2),"third");Box(state,new Vector2Int(6,2),"fourth");
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.False);
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(session.State.Find("box").position,Is.EqualTo(new Vector2Int(3,2)));Assert.That(session.CanUndo,Is.False);
        }

        [Test]
        public void ThiefPullsFriendlyCharacterAndUndoRestoresBothPositions()
        {
            var state=Board(PushBoxRole.Thief);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,movable=true,position=new Vector2Int(1,2)});
            state.entities.Add(new PushBoxEntity{id="plate",kind=PushBoxKind.Plate,position=new Vector2Int(2,2)});
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(3,2)));
            Assert.That(session.State.Find("friend").position,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(session.State.Find("plate").pressed,Is.True);
            Assert.That(session.Undo(),Is.True);
            Assert.That(session.State.Find("friend").position,Is.EqualTo(new Vector2Int(1,2)));
            Assert.That(session.State.Find("actor").position,Is.EqualTo(new Vector2Int(2,2)));
        }

        [Test]
        public void BlockedThiefMoveDoesNotPullFriendlyCharacter()
        {
            var state=Board(PushBoxRole.Thief);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,movable=true,position=new Vector2Int(1,2)});
            Box(state,new Vector2Int(3,2));
            var session=new PushBoxSession(state);
            Assert.That(session.Move("actor",Vector2Int.right,out _),Is.False);
            Assert.That(session.State.Find("friend").position,Is.EqualTo(new Vector2Int(1,2)));
            Assert.That(session.CanUndo,Is.False);
        }

        [Test]
        public void ThiefAutomaticallyPullsTheCrateBehindAndCannotPush()
        {
            var state=Board(PushBoxRole.Thief);Box(state,new Vector2Int(1,2));
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(state.Find("actor").position,Is.EqualTo(new Vector2Int(3,2)));
            Assert.That(state.Find("box").position,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.left,out _),Is.False);
        }

        [Test]
        public void MageMovementSwapsWithNearestTargetAcrossGapWithoutWalkingAlongTheRay()
        {
            var state=Board(PushBoxRole.Mage);Box(state,new Vector2Int(5,2));Box(state,new Vector2Int(6,2),"further");
            state.cells[new Vector2Int(3,2)].supports=false;state.cells[new Vector2Int(3,2)].walkable=false;
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(state.Find("actor").position,Is.EqualTo(new Vector2Int(5,2)));
            Assert.That(state.Find("box").position,Is.EqualTo(new Vector2Int(2,2)));
            Assert.That(state.Find("further").position,Is.EqualTo(new Vector2Int(6,2)));
        }

        [Test]
        public void MageCanSwapWithAnotherCharacterButNotThroughAClosedDoor()
        {
            var state=Board(PushBoxRole.Mage);
            state.entities.Add(new PushBoxEntity{id="friend",kind=PushBoxKind.Actor,movable=true,position=new Vector2Int(5,2)});
            state.entities.Add(new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(4,2)});
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(state.Find("actor").position,Is.EqualTo(new Vector2Int(3,2)));
            state.Find("door").open=true;state.Find("door").requestedOpen=true;
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(state.Find("actor").position,Is.EqualTo(new Vector2Int(5,2)));
            Assert.That(state.Find("friend").position,Is.EqualTo(new Vector2Int(3,2)));
        }

        [Test]
        public void ShieldCannotPushAndAbyssCannotBeWalkedInto()
        {
            var state=Board(PushBoxRole.Shield);Box(state,new Vector2Int(3,2));
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.False);
            state.cells[new Vector2Int(2,3)].supports=false;
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.up,out _),Is.False);
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.left,out _),Is.True);
        }

        [Test]
        public void AllPlatesOpenDoorAndUndoRestoresTheSignalAndGate()
        {
            var state=Board(PushBoxRole.Warrior);Box(state,new Vector2Int(3,2));
            state.entities.Add(new PushBoxEntity{id="plate",kind=PushBoxKind.Plate,position=new Vector2Int(4,2)});
            var door=new PushBoxEntity{id="door",kind=PushBoxKind.Door,position=new Vector2Int(6,4)};door.inputs.Add("plate");state.entities.Add(door);
            var session=new PushBoxSession(state);Assert.That(session.Move("actor",Vector2Int.right,out _),Is.True);
            Assert.That(session.State.Find("plate").pressed,Is.True);Assert.That(session.State.Find("door").open,Is.True);
            session.Undo();Assert.That(session.State.Find("plate").pressed,Is.False);Assert.That(session.State.Find("door").open,Is.False);
        }

        [Test]
        public void HeightChangesRequireMatchingRampEdges()
        {
            var state=Board(PushBoxRole.Shield);state.cells[new Vector2Int(3,2)].height=1;
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.False);
            state.cells[new Vector2Int(3,2)].height=0;state.cells[new Vector2Int(3,2)].uphill=Vector2Int.right;
            state.cells[new Vector2Int(4,2)].height=1;
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
            Assert.That(PushBoxRules.TryMove(state,"actor",Vector2Int.right,out _),Is.True);
        }
    }
}
