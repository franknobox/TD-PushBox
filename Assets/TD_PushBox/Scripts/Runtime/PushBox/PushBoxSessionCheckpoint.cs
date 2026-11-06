using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD.PushBox
{
    // Unity cannot serialize the runtime dictionaries during script reload.
    [Serializable]
    public sealed class PushBoxSavedState
    {
        public Vector2Int size;
        public PushBoxCell[] cells;
        public PushBoxEntity[] entities;
        public static PushBoxSavedState From(PushBoxState state)=>new()
        {size=state.size,cells=state.cells.Values.ToArray(),entities=state.entities.ToArray()};
        public PushBoxState Read()
        {
            var state=new PushBoxState{size=size};
            foreach(var cell in cells) state.cells.Add(cell.position,cell.Copy());
            state.entities.AddRange(entities.Select(entity=>entity.Copy()));
            return state;
        }
    }

    [Serializable]
    public sealed class PushBoxSessionCheckpoint
    {
        public PushBoxSavedState initial,current;
        public List<PushBoxSavedState> history;
        public string selectedId;
    }
}
