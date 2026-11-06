using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent,RequireComponent(typeof(Button))]
    public sealed class StartMenuButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        [SerializeField] private Image background,accent;
        [SerializeField] private Text label;
        [SerializeField] private RectTransform content,arrow;
        [SerializeField] private StartMenuController menu;
        [SerializeField] private bool primary;
        private bool hovered,pressed,initialized;
        private float emphasis,compression;
        private Vector2 contentPosition,arrowPosition;
        private readonly Color white=new(0.97f,0.98f,0.95f);
        private readonly Color ink=new(0.10f,0.23f,0.21f);
        private readonly Color gold=new(0.96f,0.76f,0.32f);
        public void Configure(Image fill,Image highlight,Text text,RectTransform body,RectTransform direction,StartMenuController controller,bool isPrimary)
        {background=fill;accent=highlight;label=text;content=body;arrow=direction;menu=controller;primary=isPrimary;}
        private void Capture()
        {
            if(initialized) return;initialized=true;
            if(content!=null) contentPosition=content.anchoredPosition;
            if(arrow!=null) arrowPosition=arrow.anchoredPosition;
        }
        private void Update()
        {
            Capture();
            float blend=1-Mathf.Exp(-16*Time.unscaledDeltaTime);
            emphasis=Mathf.Lerp(emphasis,hovered?1:0,blend);compression=Mathf.Lerp(compression,pressed?1:0,blend);
            if(background!=null) background.color=primary?Color.Lerp(white,new Color(1,0.90f,0.64f),emphasis):new Color(1,1,1,0.10f*emphasis);
            if(label!=null) label.color=primary?ink:Color.Lerp(white,gold,emphasis);
            if(accent!=null) accent.color=new Color(gold.r,gold.g,gold.b,primary?1:emphasis);
            if(content!=null)
            {
                content.anchoredPosition=contentPosition+Vector2.right*(6*emphasis);
                content.localScale=Vector3.one*(1-0.025f*compression);
            }
            if(arrow!=null) arrow.anchoredPosition=arrowPosition+Vector2.right*(5*emphasis);
        }
        public void OnPointerEnter(PointerEventData data) {hovered=true;if(menu!=null) menu.Hover();}
        public void OnPointerExit(PointerEventData data) {hovered=false;pressed=false;}
        public void OnPointerDown(PointerEventData data) {if(data.button==PointerEventData.InputButton.Left) pressed=true;}
        public void OnPointerUp(PointerEventData data) {if(data.button==PointerEventData.InputButton.Left) pressed=false;}
        private void OnDisable()
        {
            hovered=pressed=false;emphasis=compression=0;
            if(initialized&&content!=null) {content.anchoredPosition=contentPosition;content.localScale=Vector3.one;}
            if(initialized&&arrow!=null) arrow.anchoredPosition=arrowPosition;
        }
    }
}
