using System.Linq;
using TD.PushBox;
using TD.PushBox.Frontend;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class LevelSelectInterfaceBuilder
    {
        static LevelSelectInterfaceBuilder()
        {
            EditorApplication.delayCall+=RefreshLoadedScenes;
            EditorSceneManager.sceneOpened+=(scene,mode)=>UpgradeScene(scene);
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode) RefreshLoadedScenes();};
        }
        private static void RefreshLoadedScenes()
        {
            if(Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||AssetDatabase.IsAssetImportWorkerProcess()) return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||Undo.isProcessing)
            {EditorApplication.delayCall+=RefreshLoadedScenes;return;}
            for(int i=0;i<SceneManager.sceneCount;i++) UpgradeScene(SceneManager.GetSceneAt(i));
        }
        private static void UpgradeScene(Scene scene)
        {
            if(TDProjectValidation.IsRunning||Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||Undo.isProcessing||!scene.IsValid()||!scene.isLoaded
                ||EditorSceneManager.IsPreviewScene(scene)||scene.path!=TDProjectPaths.LevelSelectScene) return;
            var controller=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<LevelSelectController>(true)).FirstOrDefault();
            if(controller==null) return;
            var canvas=controller.GetComponent<Canvas>();
            var camera=canvas!=null?canvas.worldCamera?.GetComponent<PushBoxCamera>():null;
            bool unsaved=scene.isDirty;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("调整关卡选择界面");
            if(!Upgrade(controller.gameObject,camera,true)) return;
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!unsaved) EditorSceneManager.SaveScene(scene);
        }

        public static bool Upgrade(GameObject root,PushBoxCamera camera,bool recordUndo)
        {
            var controller=root.GetComponent<LevelSelectController>();
            if(controller==null||new SerializedObject(controller).FindProperty("interfaceVersion").intValue>=1) return false;
            var button=root.transform.Find("Back") as RectTransform;
            if(button==null) return false;
            var content=button.Find("Content") as RectTransform;
            var arrow=content?.Find("BackIcon") as RectTransform;
            var label=content?.Find("Label") as RectTransform;
            if(recordUndo)
            {
                foreach(var rect in new[]{button,content,arrow,label}.Where(rect=>rect!=null)) Undo.RecordObject(rect,"调整返回按钮");
                Undo.RecordObject(controller,"关联自制关卡列表");
            }
            button.anchorMin=button.anchorMax=button.pivot=Vector2.zero;
            button.anchoredPosition=new Vector2(48,48);button.sizeDelta=new Vector2(192,60);
            if(content!=null) content.sizeDelta=button.sizeDelta;
            if(arrow!=null)
            {
                arrow.anchorMin=arrow.anchorMax=new Vector2(0,0.5f);arrow.pivot=new Vector2(0.5f,0.5f);
                arrow.anchoredPosition=new Vector2(32,0);arrow.sizeDelta=new Vector2(24,24);arrow.localRotation=Quaternion.Euler(0,0,180);
            }
            if(label!=null) {label.anchoredPosition=new Vector2(58,0);label.sizeDelta=new Vector2(114,60);}

            var list=root.GetComponentInChildren<CustomLevelListPanel>(true);
            bool created=list==null;
            if(list==null)
            {
                list=CreateList(root.transform);
            }
            else if(recordUndo) Undo.RecordObject(list,"关联镜头输入");
            if(camera!=null) UnityEventTools.AddPersistentListener(list.PointerOverChanged,camera.SetZoomInputBlocked);
            if(recordUndo&&created) Undo.RegisterCreatedObjectUndo(list.gameObject,"添加自制关卡列表");
            var module=root.scene.GetRootGameObjects().SelectMany(item=>item.GetComponentsInChildren<InputSystemUIInputModule>(true)).FirstOrDefault();
            if(module!=null)
            {
                if(recordUndo) Undo.RecordObject(module,"调整列表滚轮输入");
                ConfigureScroll(module);EditorUtility.SetDirty(module);
            }
            controller.Configure(root.GetComponent<CanvasGroup>(),list);
            EditorUtility.SetDirty(controller);
            return true;
        }

        internal static void ConfigureScroll(InputSystemUIInputModule module)
        {
            // The Input System's native tick normalization is unavailable in Unity 2022.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            module.scrollDeltaPerTick=6f/120f;
#else
            module.scrollDeltaPerTick=6;
#endif
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var rect=new GameObject(ObjNames.En(name),typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static RectTransform Stretch(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var rect=Rect(parent,name,Vector2.zero,new Vector2(0.5f,0.5f),Vector2.zero,Vector2.zero);
            rect.anchorMax=Vector2.one;rect.offsetMin=min;rect.offsetMax=max;return rect;
        }
        private static Image Fill(RectTransform rect,Color color,bool raycast=false)
        {var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;}
        private static Text Text(RectTransform rect,string value,Font font,int size,Color color)
        {
            var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=size;text.color=color;
            text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
        private static Button Button(RectTransform rect,Image background)
        {
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=background;button.transition=Selectable.Transition.None;
            button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
        }
        private static CustomLevelListPanel CreateList(Transform parent)
        {
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            var white=new Color(0.97f,0.98f,0.95f);var ink=new Color(0.10f,0.23f,0.21f);var gold=new Color(0.96f,0.76f,0.32f);
            var root=Rect(parent,"自制关卡",Vector2.one,Vector2.one,new Vector2(320,64),new Vector2(-48,-48));
            var panel=root.gameObject.AddComponent<CustomLevelListPanel>();
            var header=Rect(root,"展开收起",Vector2.up,Vector2.up,new Vector2(320,64),Vector2.zero);
            var background=Fill(header,white,true);var toggle=Button(header,background);
            var headerContent=Stretch(header,"按钮内容",Vector2.zero,Vector2.zero);
            var title=Text(Rect(headerContent,"标题",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(224,64),new Vector2(24,0)),"自制关卡",font,26,ink);
            title.fontStyle=FontStyle.Bold;
            Fill(Rect(header,"金色边线",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(4,28),Vector2.zero),gold);
            var chevron=Rect(headerContent,"展开箭头",new Vector2(1,0.5f),new Vector2(0.5f,0.5f),new Vector2(24,24),new Vector2(-30,0));
            var icon=chevron.gameObject.AddComponent<MenuChevronGraphic>();icon.color=ink;icon.raycastTarget=false;
            header.gameObject.AddComponent<StartMenuButtonFeedback>().Configure(background,null,title,headerContent,null,null,true);
            UnityEventTools.AddPersistentListener(toggle.onClick,panel.Toggle);

            var body=Rect(root,"下拉列表",Vector2.up,Vector2.up,new Vector2(320,84),new Vector2(0,-70));
            Fill(body,new Color(0.065f,0.15f,0.125f,0.96f),true);body.gameObject.AddComponent<CanvasGroup>();
            var viewport=Stretch(body,"可视区域",Vector2.zero,new Vector2(-16,0));viewport.gameObject.AddComponent<RectMask2D>();
            var items=Rect(viewport,"关卡条目",Vector2.up,Vector2.up,new Vector2(0,0),Vector2.zero);
            items.anchorMax=Vector2.one;items.anchorMin=new Vector2(0,1);
            var layout=items.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(12,4,12,12);
            layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            items.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var empty=Text(Stretch(viewport,"空列表",new Vector2(16,0),new Vector2(-8,0)),"暂无自制关卡",font,21,new Color(0.81f,0.87f,0.82f));
            empty.alignment=TextAnchor.MiddleCenter;
            var barRect=Rect(body,"滚动条",new Vector2(1,0),new Vector2(1,0),new Vector2(4,0),new Vector2(-8,10));
            barRect.anchorMax=Vector2.one;barRect.offsetMax=new Vector2(-8,-10);barRect.offsetMin=new Vector2(-12,10);
            Fill(barRect,new Color(1,1,1,0.07f),true);
            var handle=Stretch(barRect,"滑块",Vector2.zero,Vector2.zero);var thumb=Fill(handle,new Color(0.72f,0.79f,0.69f),true);
            var scrollbar=barRect.gameObject.AddComponent<Scrollbar>();scrollbar.targetGraphic=thumb;scrollbar.handleRect=handle;scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scrollbar.navigation=new Navigation{mode=Navigation.Mode.None};
            var scroller=body.gameObject.AddComponent<ScrollRect>();scroller.content=items;scroller.viewport=viewport;
            scroller.horizontal=false;scroller.vertical=true;scroller.movementType=ScrollRect.MovementType.Clamped;scroller.scrollSensitivity=52f/6;
            scroller.verticalScrollbar=scrollbar;scroller.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;

            var template=Rect(root,"条目模板",Vector2.up,Vector2.up,new Vector2(288,52),Vector2.zero);
            template.gameObject.AddComponent<LayoutElement>().preferredHeight=52;
            var rowFill=Fill(template,Color.clear,true);Button(template,rowFill);
            var rowContent=Stretch(template,"按钮内容",Vector2.zero,Vector2.zero);
            var name=Text(Stretch(rowContent,"名称",new Vector2(14,4),new Vector2(-28,-4)),"",font,22,white);
            name.resizeTextForBestFit=true;name.resizeTextMinSize=16;name.resizeTextMaxSize=22;
            Fill(Rect(template,"分隔线",Vector2.zero,Vector2.zero,new Vector2(280,1),Vector2.zero),new Color(1,1,1,0.09f));
            template.gameObject.AddComponent<StartMenuButtonFeedback>().Configure(rowFill,null,name,rowContent,null,null,false);
            template.gameObject.SetActive(false);
            panel.Configure(body,items,chevron,template,empty,scroller);
            return panel;
        }
    }
}
