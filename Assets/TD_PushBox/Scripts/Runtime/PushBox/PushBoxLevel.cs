using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD.PushBox
{
    [DisallowMultipleComponent]
    public sealed class PushBoxLevel : MonoBehaviour
    {
        [SerializeField,HideInInspector] private Vector2Int size;
        [SerializeField,HideInInspector] private Vector3 gridOrigin;
        [SerializeField,HideInInspector] private float cellSize=1.12f,heightStep=0.45f;
        [SerializeField,HideInInspector] private List<PushBoxCell> cells=new();
        public Vector2Int Size=>size;
        public float CellSize=>cellSize;
        public void SetLayout(Vector2Int bounds,Vector3 origin,float spacing,float step,IEnumerable<PushBoxCell> ground)
        {size=bounds;gridOrigin=origin;cellSize=spacing;heightStep=step;cells=new List<PushBoxCell>(ground);}
        public Vector3 Surface(Vector2Int p)
        {
            var cell=cells.Find(item=>item.position==p);
            float y=cell!=null?(cell.height+(cell.uphill==Vector2Int.zero?0:0.5f))*heightStep:0;
            return transform.TransformPoint(gridOrigin+new Vector3(p.x*cellSize,y,p.y*cellSize));
        }
        public PushBoxState BuildState()
        {
            var state=new PushBoxState{size=size};
            foreach(var cell in cells)
            {
                if(!state.Inside(cell.position)||!state.cells.TryAdd(cell.position,cell.Copy()))
                    throw new InvalidOperationException("地块坐标越界或重复："+cell.position);
            }
            foreach(var actor in GetComponentsInChildren<PushBoxActor>()) state.entities.Add(actor.CreateState(this));
            foreach(var item in GetComponentsInChildren<PushBoxObject>()) state.entities.Add(item.CreateState());
            return state;
        }
    }
}
