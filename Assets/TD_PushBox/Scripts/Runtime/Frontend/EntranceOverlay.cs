using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TD.PushBox.Frontend
{
    public sealed class EntranceOverlay : MonoBehaviour
    {
        private RectTransform canvasRect,prompt,previewRect;
        private CanvasGroup promptGroup;
        private Image shade,curtain;
        private RawImage preview;
        private Text feedback;
        private Material previewMaterial;
        private float feedbackUntil;
        private static readonly Color Ink=new(0.10f,0.22f,0.18f),Gold=new(0.95f,0.77f,0.35f),White=new(0.97f,0.98f,0.93f);

        public static EntranceOverlay Create(Scene scene)
        {
            var root=new GameObject("EntranceUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(root,scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            var view=root.AddComponent<EntranceOverlay>();view.Build();return view;
        }

        private void Build()
        {
            canvasRect=(RectTransform)transform;
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            shade=Fill(Stretch(transform,"Shade"),new Color(0.025f,0.07f,0.05f,0));
            previewRect=Rect(transform,"Preview",new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(1600,900),Vector2.zero);
            preview=previewRect.gameObject.AddComponent<RawImage>();preview.raycastTarget=false;preview.enabled=false;
            var source=Resources.Load<Material>("UI/LevelPreview");
            if(source!=null) {previewMaterial=new Material(source);preview.material=previewMaterial;}
            prompt=Rect(transform,"Actions",new Vector2(0.5f,0.5f),new Vector2(0.5f,0),new Vector2(244,118),Vector2.zero);
            promptGroup=prompt.gameObject.AddComponent<CanvasGroup>();promptGroup.alpha=0;
            Row(prompt,"Enter","E","进入关卡",EntranceIcon.Door,0,font);
            Row(prompt,"View","V","预览关卡",EntranceIcon.Glasses,-62,font);
            feedback=Label(Rect(transform,"Feedback",new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(1000,48),new Vector2(0,34)),"",font,24,White);
            curtain=Fill(Stretch(transform,"Curtain"),new Color(0.025f,0.06f,0.045f,0));
        }

        private static void Row(Transform parent,string name,string key,string title,EntranceIcon icon,float y,Font font)
        {
            var row=Rect(parent,name,Vector2.up,Vector2.up,new Vector2(244,56),new Vector2(0,y));
            Fill(row,new Color(Ink.r,Ink.g,Ink.b,0.95f));
            Fill(Rect(row,"Accent",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(3,28),Vector2.zero),Gold);
            var keyRect=Rect(row,"Key",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(36,36),new Vector2(12,0));
            Fill(keyRect,White);
            var keyLabel=Label(Stretch(keyRect,"Label"),key,font,26,Ink);
            // CJK font line metrics can exceed the keycap even when the Latin glyph fits.
            keyLabel.verticalOverflow=VerticalWrapMode.Overflow;keyLabel.horizontalOverflow=HorizontalWrapMode.Overflow;
            keyLabel.alignByGeometry=true;
            var mark=Rect(row,"Icon",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(30,30),new Vector2(62,0));
            var graphic=mark.gameObject.AddComponent<EntranceActionGraphic>();graphic.icon=icon;graphic.color=Gold;graphic.raycastTarget=false;
            var text=Label(Rect(row,"Label",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(130,48),new Vector2(102,0)),title,font,24,White);
            text.alignment=TextAnchor.MiddleLeft;
        }

        public void ShowPrompt(Camera camera,Vector3 point,bool visible)
        {
            promptGroup.alpha=visible?Mathf.MoveTowards(promptGroup.alpha,1,Time.unscaledDeltaTime/0.12f):0;
            if(!visible||camera==null) return;
            var screen=camera.WorldToScreenPoint(point);
            if(screen.z<=0) {promptGroup.alpha=0;return;}
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,screen,null,out var local);
            var bounds=canvasRect.rect;
            prompt.anchoredPosition=new Vector2(Mathf.Clamp(local.x,bounds.xMin+144,bounds.xMax-144),Mathf.Clamp(local.y+12,bounds.yMin+24,bounds.yMax-142));
        }

        public void ShowPreview(Texture2D texture,float progress)
        {
            preview.texture=texture;preview.enabled=texture!=null&&progress>0;
            if(previewMaterial!=null) previewMaterial.SetFloat("_Progress",progress);
            else preview.color=new Color(1,1,1,progress);
            shade.color=new Color(0.025f,0.07f,0.05f,progress*0.58f);
            if(texture==null) return;
            float aspect=(float)texture.width/texture.height,width=canvasRect.rect.width*0.75f,height=canvasRect.rect.height*0.75f;
            if(width/height>aspect) width=height*aspect;else height=width/aspect;
            previewRect.sizeDelta=new Vector2(width,height);
        }
        public void FadeOut(float alpha) {curtain.color=new Color(0.025f,0.06f,0.045f,alpha);}
        public void Notify(string message) {feedback.text=message;feedbackUntil=Time.unscaledTime+2.5f;}
        private void Update() {if(feedback!=null&&Time.unscaledTime>feedbackUntil) feedback.text="";}
        private void OnDestroy() {if(previewMaterial!=null) Destroy(previewMaterial);}

        private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static RectTransform Stretch(Transform parent,string name)
        {var rect=Rect(parent,name,Vector2.zero,new Vector2(0.5f,0.5f),Vector2.zero,Vector2.zero);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
        private static Image Fill(RectTransform rect,Color color)
        {var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static Text Label(RectTransform rect,string value,Font font,int size,Color color)
        {
            var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=size;text.color=color;
            text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
    }
}
