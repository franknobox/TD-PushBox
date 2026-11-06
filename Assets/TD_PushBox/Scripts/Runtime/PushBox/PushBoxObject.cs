using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TD.PushBox
{
    [DisallowMultipleComponent]
    public sealed class PushBoxObject : MonoBehaviour
    {
        [SerializeField,InspectorName("类型")] private PushBoxKind kind=PushBoxKind.Crate;
        [SerializeField,InspectorName("唯一标识")] private string stableId;
        [SerializeField,InspectorName("格点")] private Vector2Int gridPosition;
        [SerializeField,InspectorName("占地")] private Vector2Int footprint=Vector2Int.one;
        [SerializeField,InspectorName("允许搬运")] private bool movable=true;
        [SerializeField,InspectorName("可被关门压碎")] private bool crushable=true;
        [SerializeField,InspectorName("攻击方式")] private PushBoxAttack attack;
        private Transform flame;
        [SerializeField,InspectorName("初始开启")] private bool startsOpen;
        [SerializeField,InspectorName("开门条件")] private PushBoxDoorRule doorRule;
        [SerializeField,InspectorName("再次压下切换开关")] private bool toggleOnPress;
        [SerializeField,InspectorName("关联压力板")] private List<PushBoxObject> inputs=new();
        [SerializeField,InspectorName("活动部件")] private Transform movingPart;
        [SerializeField,InspectorName("下降距离")] private float lowerDistance=0.8f;
        private Vector3 restPosition,restScale;
        private bool captured;
        private Renderer goalRenderer;
        private MaterialPropertyBlock goalProperties;
        public PushBoxKind Kind=>kind;
        public string StableId=>stableId;
        public Vector2Int GridPosition=>gridPosition;
        public Vector2Int Footprint=>footprint;
        public bool StartsOpen=>startsOpen;
        public PushBoxAttack Attack=>attack;
        public PushBoxDoorRule DoorRule=>doorRule;
        public bool IsDoorAnimating=>TryGetComponent<EnemySealDoorVisual>(out var visual)&&visual.IsAnimating;
        public bool IsSolid=>kind==PushBoxKind.Enemy||kind==PushBoxKind.Crate||kind==PushBoxKind.Blocker||kind==PushBoxKind.Door&&!startsOpen;
        public bool Covers(Vector2Int p)=>p.x>=gridPosition.x&&p.y>=gridPosition.y&&p.x<gridPosition.x+footprint.x&&p.y<gridPosition.y+footprint.y;
        public void Configure(PushBoxKind value,Vector2Int area,bool canMove,Transform part=null,float lower=0.8f,bool toggle=false)
        {kind=value;footprint=area;movable=canMove;movingPart=part;lowerDistance=lower;toggleOnPress=toggle;}
        public void SetPlacement(Vector2Int point,bool newId)
        {gridPosition=point;if(newId||string.IsNullOrEmpty(stableId)) stableId=Guid.NewGuid().ToString("N");}
        public PushBoxEntity CreateState()
        {
            bool usePlates=kind==PushBoxKind.Door&&doorRule==PushBoxDoorRule.PressurePlates;
            if(usePlates&&inputs.Any(input=>input==null||input.Kind!=PushBoxKind.Plate||input.GetComponentInParent<PushBoxLevel>()!=GetComponentInParent<PushBoxLevel>()))
                throw new InvalidOperationException("门引用的压力板无效："+name);
            var board=GetComponentInParent<PushBoxLevel>();
            Vector3 direction=board!=null?board.transform.InverseTransformDirection(-transform.forward):-transform.forward;
            return new PushBoxEntity{id=stableId,kind=kind,position=gridPosition,footprint=footprint,movable=movable,attack=attack,doorRule=doorRule,crushable=crushable,
                facing=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)?new Vector2Int(direction.x>0?1:-1,0):new Vector2Int(0,direction.z>0?1:-1),
                open=startsOpen,requestedOpen=startsOpen,toggleOnPress=toggleOnPress,inputs=usePlates?inputs.Select(input=>input.StableId).ToList():new List<string>()};
        }
        public void ShowFire(int cells,float spacing)
        {
            if(attack!=PushBoxAttack.DragonFire) return;
            if(flame==null) flame=GetComponentsInChildren<Transform>(true).FirstOrDefault(item=>item.name=="Flame");
            if(flame==null) return;
            flame.gameObject.SetActive(cells>0);
            if(cells>0)
            {
                var mesh=flame.GetComponent<MeshFilter>()?.sharedMesh;
                float depth=mesh!=null?Mathf.Max(0.01f,-mesh.bounds.min.z):3;
                flame.localScale=new Vector3(1,1,Mathf.Max(0.1f,(cells+0.5f)*spacing-Mathf.Abs(flame.localPosition.z))/depth);
            }
        }
        public void Present(PushBoxEntity state,bool animateDoor=false)
        {
            if(kind==PushBoxKind.Crate) {SetCrushFlash(false);gameObject.SetActive(!state.dead);return;}
            if(kind==PushBoxKind.Door&&doorRule==PushBoxDoorRule.AllEnemiesDefeated&&TryGetComponent<EnemySealDoorVisual>(out var sealDoor))
            {sealDoor.Present(state.open,animateDoor);return;}
            if(kind==PushBoxKind.Enemy) {gameObject.SetActive(!state.dead);return;}
            if(kind==PushBoxKind.Goal)
            {
                if(goalRenderer==null) goalRenderer=GetComponentInChildren<Renderer>();
                if(goalRenderer==null) return;
                // Only recolor the goal accent, leaving the stone frame unchanged.
                const int colorSlot=3;
                int count=goalRenderer.sharedMaterials.Length;if(count==0) return;
                int slot=count>colorSlot?colorSlot:0;
                goalProperties??=new MaterialPropertyBlock();
                Color color=state.goalDisabled?Color.black:state.pressed?new Color(0.48f,0.96f,0.48f):new Color(0.28f,0.76f,0.36f);
                goalProperties.SetColor("_BaseColor",color);goalProperties.SetColor("_Color",color);
                goalRenderer.SetPropertyBlock(goalProperties,slot);return;
            }
            if(movingPart==null) return;
            if(!captured) {restPosition=movingPart.localPosition;restScale=movingPart.localScale;captured=true;}
            movingPart.localPosition=restPosition+Vector3.down*(kind==PushBoxKind.Plate?(state.pressed?0.045f:0):state.open?lowerDistance:0);
            if(kind==PushBoxKind.Door)
            {
                movingPart.gameObject.SetActive(!state.open);
                foreach(var collider in GetComponentsInChildren<Collider>(true)) collider.enabled=!state.open;
            }
            movingPart.localScale=restScale;
        }
        public void SetCrushFlash(bool white)
        {
            if(kind!=PushBoxKind.Crate) return;
            var properties=white?new MaterialPropertyBlock():null;
            if(white)
            {
                properties.SetColor("_BaseColor",Color.white);properties.SetColor("_Color",Color.white);
                properties.SetColor("_EmissionColor",Color.white*2);
            }
            foreach(var renderer in GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(properties);
        }
    }
}
