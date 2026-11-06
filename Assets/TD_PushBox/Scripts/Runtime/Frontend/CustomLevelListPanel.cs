using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent]
    public sealed class CustomLevelListPanel : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        [SerializeField,InspectorName("关卡名称（外观预览）"),Tooltip("当前只预览列表名称，不加载关卡。")]
        private string[] levelNames=Array.Empty<string>();
        [SerializeField] private RectTransform body,content,chevron,rowTemplate;
        [SerializeField] private Text emptyState;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private CanvasGroup bodyGroup;
        [SerializeField] private UnityEvent<bool> pointerOverChanged=new();
        private readonly List<GameObject> rows=new();
        private bool expanded,refreshNeeded;
        private float reveal;
        public bool IsExpanded=>expanded;
        public UnityEvent<bool> PointerOverChanged=>pointerOverChanged;

        public void Configure(RectTransform dropdown,RectTransform items,RectTransform icon,RectTransform template,Text empty,ScrollRect scroller)
        {
            body=dropdown;content=items;chevron=icon;rowTemplate=template;emptyState=empty;scroll=scroller;
            bodyGroup=body.GetComponent<CanvasGroup>();ApplyReveal();
        }
        private void OnEnable()
        {
            expanded=false;reveal=0;refreshNeeded=true;ApplyReveal();
        }
        private void OnValidate() {levelNames??=Array.Empty<string>();refreshNeeded=true;}
        public void Toggle()=>SetExpanded(!expanded);
        public void SetExpanded(bool value)=>expanded=value;
        private void Update()
        {
            if(refreshNeeded) {refreshNeeded=false;RefreshRows();}
            float next=Mathf.MoveTowards(reveal,expanded?1:0,Time.unscaledDeltaTime/0.16f);
            if(next!=reveal) {reveal=next;ApplyReveal();}
        }
        private float BodyHeight=>levelNames.Length==0?84:Mathf.Min(levelNames.Length,6)*52+24;
        private void ApplyReveal()
        {
            if(body==null) return;
            float blend=Mathf.SmoothStep(0,1,reveal);
            body.gameObject.SetActive(reveal>0);
            body.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,BodyHeight*blend);
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,64+(BodyHeight+6)*blend);
            if(chevron!=null) chevron.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-90,90,blend));
            if(bodyGroup!=null) {bodyGroup.alpha=blend;bodyGroup.interactable=expanded&&reveal>0.95f;bodyGroup.blocksRaycasts=reveal>0;}
        }
        private void RefreshRows()
        {
            if(content==null||rowTemplate==null) return;
            foreach(var row in rows) {if(row==null) continue;row.SetActive(false);Destroy(row);}
            rows.Clear();
            for(int i=0;i<levelNames.Length;i++)
            {
                var row=Instantiate(rowTemplate,content,false);row.name="Custom_"+(i+1).ToString("00");
                row.GetComponentInChildren<Text>(true).text=string.IsNullOrWhiteSpace(levelNames[i])?"未命名关卡":levelNames[i];
                row.gameObject.SetActive(true);rows.Add(row.gameObject);
            }
            if(emptyState!=null) emptyState.gameObject.SetActive(levelNames.Length==0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if(scroll!=null) scroll.verticalNormalizedPosition=1;
            ApplyReveal();
        }
        public void OnPointerEnter(PointerEventData data)=>pointerOverChanged.Invoke(true);
        public void OnPointerExit(PointerEventData data)=>pointerOverChanged.Invoke(false);
        private void OnDisable()
        {
            expanded=false;reveal=0;ApplyReveal();pointerOverChanged.Invoke(false);
        }
    }
}
