using System;
using System.Linq;

namespace TD.PushBox
{
    [Flags]
    public enum PushBoxActionCue { None=0,Footstep=1,CrateDrag=2,Swap=4 }

    public static class PushBoxActionCues
    {
        public static PushBoxActionCue Classify(PushBoxState before,PushBoxState after,string actorId)
        {
            var old=before.Find(actorId);var actor=after.Find(actorId);
            if(old==null||actor==null||old.position==actor.position) return PushBoxActionCue.None;
            if(actor.role==PushBoxRole.Mage&&after.entities.Any(entity=>entity.id!=actorId&&entity.position==old.position
                &&before.Find(entity.id)?.position==actor.position)) return PushBoxActionCue.Swap;
            var result=PushBoxActionCue.Footstep;
            if(after.entities.Any(entity=>entity.kind==PushBoxKind.Crate&&entity.crushable
                &&before.Find(entity.id) is PushBoxEntity prior&&prior.position!=entity.position)) result|=PushBoxActionCue.CrateDrag;
            return result;
        }
    }
}
