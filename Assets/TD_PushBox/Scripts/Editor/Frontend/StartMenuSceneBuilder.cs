using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TD.PushBox.Frontend;
using TD.PushBox;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    [InitializeOnLoad]
    public static class StartMenuSceneBuilder
    {
        public const string ScenePath=TDProjectPaths.StartMenuScene;
        private const string MaterialPath=TDProjectPaths.Root+"/Art/Materials/StartMenu";
        private static Material ground,grass,grassLight,stone,stoneLight,stoneDark,water,waterLight,wood,darkWood,gold,leaf,leafTop;
        private static Transform scenery;
        private const float StreamLeft=-40;

        // Migrate loaded menus in place without reloading unsaved scene edits.
        static StartMenuSceneBuilder()
        {
            EditorApplication.delayCall+=UpdateOpenMenus;
            EditorSceneManager.sceneOpened+=(scene,mode)=>UpdateMenuLayout(scene);
            EditorApplication.playModeStateChanged+=change=>{if(change==PlayModeStateChange.EnteredEditMode) UpdateOpenMenus();};
        }
        private static void UpdateOpenMenus()
        {
            if(Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating) {EditorApplication.delayCall+=UpdateOpenMenus;return;}
            for(int i=0;i<SceneManager.sceneCount;i++) UpdateMenuLayout(SceneManager.GetSceneAt(i));
        }
        private static void UpdateMenuLayout(Scene scene)
        {
            if(TDProjectValidation.IsRunning||Application.isBatchMode||EditorApplication.isPlayingOrWillChangePlaymode||!scene.IsValid()||!scene.isLoaded||EditorSceneManager.IsPreviewScene(scene)||scene.path!=ScenePath) return;
            var roots=scene.GetRootGameObjects();
            var menu=roots.SelectMany(root=>root.GetComponentsInChildren<StartMenuController>(true)).FirstOrDefault();
            var canvas=menu!=null?(RectTransform)new SerializedObject(menu).FindProperty("interfaceRoot").objectReferenceValue:null;
            var column=canvas!=null?canvas.Find("Menu"):null;
            var obsolete=column!=null?column.Find("LevelSelect"):null;
            var quit=column!=null?column.Find("Quit") as RectTransform:null;
            var courtyard=roots.FirstOrDefault(root=>root.name=="Courtyard");
            var stream=courtyard!=null?courtyard.transform.Find("Stream"):null;
            var pieces=stream!=null?new[]{stream.Find("Bank"),stream.Find("Water")}.Where(piece=>piece!=null).ToArray():Array.Empty<Transform>();
            bool extend=pieces.Any(piece=>piece.localPosition.x-piece.localScale.x/2>StreamLeft+0.01f);
            bool moveQuit=quit!=null&&(obsolete!=null||Mathf.Approximately(quit.anchoredPosition.y,-424));
            if(obsolete==null&&!extend&&!moveQuit) return;
            bool unsaved=scene.isDirty;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("调整开始界面布局");
            if(moveQuit)
            {
                Undo.RecordObject(quit,"上移退出游戏按钮");
                quit.anchoredPosition=obsolete is RectTransform old?old.anchoredPosition:new Vector2(quit.anchoredPosition.x,-344);
            }
            if(obsolete!=null) Undo.DestroyObjectImmediate(obsolete.gameObject);
            foreach(var piece in pieces)
            {
                if(piece.localPosition.x-piece.localScale.x/2<=StreamLeft+0.01f) continue;
                Undo.RecordObject(piece,"延伸溪流");float right=piece.localPosition.x+piece.localScale.x/2;
                var position=piece.localPosition;position.x=(StreamLeft+right)/2;piece.localPosition=position;
                var scale=piece.localScale;scale.x=right-StreamLeft;piece.localScale=scale;
            }
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);
            if(!unsaved) EditorSceneManager.SaveScene(scene);
        }

        public static void Build()
        {
            if(File.Exists(ScenePath)) throw new InvalidOperationException("开始场景已存在，不自动覆盖手工编辑。");
            var preserved=new[]{TDProjectPaths.SrpgScene,TDProjectPaths.PushBoxScene}.Where(File.Exists).ToDictionary(path=>path,File.ReadAllText);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            CreateMaterials();
            scenery=new GameObject("Courtyard").transform;
            var trees=new List<Transform>();var characters=new List<Transform>();var glints=new List<Transform>();
            CreateGround();CreateCourtyard();CreateForest(trees);CreateStream(glints);
            var crate=Visual("Object_Box","主木箱",new Vector3(3.25f,0.05f,1.25f),1.42f,0);
            Visual("Object_Box","备用木箱",new Vector3(6.8f,0.05f,-0.25f),1.06f,-8);
            Visual("Object_Box","墙边木箱",new Vector3(7.7f,0.05f,5.1f),1.05f,4);
            Visual("Object_Box","叠放木箱",new Vector3(7.75f,0.93f,5.12f),0.9f,-4);
            var inlay=CreateTarget(new Vector3(3.25f,0.036f,1.25f));
            CreateCharacter("Unit_Warrior","战士",new Vector3(2.35f,0.05f,-0.50f),new Vector3(3.25f,0,1.25f),1.1f,characters);
            CreateCharacter("Unit_Thief","盗贼",new Vector3(6.4f,0.05f,2.4f),new Vector3(3.25f,0,1.25f),1.04f,characters);
            CreateCharacter("Unit_Mage","法师",new Vector3(3.1f,0.05f,4.5f),new Vector3(4,0,1.5f),1.06f,characters);
            CreateCharacter("Unit_Shieldbearer","盾兵",new Vector3(6.45f,0.05f,4.7f),new Vector3(4.5f,0,1.8f),1.03f,characters);
            CreateLighting();var camera=CreateCamera();
            var system=new GameObject("MenuCtrl");
            var motion=system.AddComponent<StartMenuWorldMotion>();motion.Configure(trees.ToArray(),characters.ToArray(),glints.ToArray(),crate.transform,inlay);
            var source=system.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=0.8f;
            var controller=system.AddComponent<StartMenuController>();
            CreateInterface(camera,controller,motion,source);
            var eventSystem=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().sendNavigationEvents=false;
            ConfigureInput(eventSystem.GetComponent<InputSystemUIInputModule>());
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(0.46f,0.52f,0.52f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            foreach(var pair in preserved) if(File.ReadAllText(pair.Key)!=pair.Value) throw new InvalidOperationException("不应修改已有场景："+pair.Key);
            Debug.Log("START_MENU: PushBox title, 3D courtyard, four visual-only characters, crates and UI feedback saved. Gameplay scenes unchanged.");
        }

        private static void CreateMaterials()
        {
            ground=Material("Ground",new Color(0.31f,0.44f,0.35f));
            grass=Material("Grass",new Color(0.40f,0.57f,0.34f));grassLight=Material("GrassLight",new Color(0.52f,0.65f,0.40f));
            stone=Material("Stone",new Color(0.63f,0.69f,0.66f));stoneLight=Material("StoneLight",new Color(0.72f,0.76f,0.71f));
            stoneDark=Material("StoneDark",new Color(0.37f,0.46f,0.43f));
            water=Material("Water",new Color(0.17f,0.48f,0.51f));waterLight=Material("WaterLight",new Color(0.52f,0.77f,0.73f));
            wood=Material("Wood",new Color(0.56f,0.39f,0.23f));darkWood=Material("WoodDark",new Color(0.29f,0.25f,0.18f));
            gold=Material("Target",new Color(0.90f,0.68f,0.21f));gold.EnableKeyword("_EMISSION");gold.SetColor("_EmissionColor",new Color(0.23f,0.12f,0.025f));
            leaf=Material("Leaf",new Color(0.28f,0.45f,0.34f));leafTop=Material("LeafTop",new Color(0.43f,0.60f,0.39f));
        }
        private static Material Material(string name,Color color)
        {
            string path=MaterialPath+"/"+name+".mat";
            var result=TDProjectPaths.Load<Material>(path);
            if(result==null)
            {
                result=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Menu_"+name};
                TDProjectPaths.CreateAsset(result,path);
            }
            result.SetColor("_BaseColor",color);result.SetFloat("_Smoothness",0.12f);EditorUtility.SetDirty(result);return result;
        }
        private static Transform Group(string name)=>new GameObject(ObjNames.En(name)){transform={parent=scenery}}.transform;
        private static GameObject Block(string name,Vector3 position,Vector3 size,Material material,Transform parent=null,float yaw=0)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=ObjNames.En(name);go.transform.SetParent(parent!=null?parent:scenery,false);
            go.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));go.transform.localScale=size;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static GameObject Visual(string prefab,string name,Vector3 position,float scale,float yaw,Transform parent=null)
        {
            var source=TDProjectPaths.Load<GameObject>(TDProjectPaths.Root+"/Prefabs/"+prefab+".prefab");
            if(source==null) throw new InvalidOperationException("缺少菜单美术资源："+prefab);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source,parent!=null?parent:scenery);
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach(var child in root.GetComponentsInChildren<Transform>(true).Where(child=>child.name=="Health Anchor"||child.name=="Selection Marker").ToArray())
                Object.DestroyImmediate(child.gameObject);
            root.name=ObjNames.En(name);root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));root.transform.localScale=Vector3.one*scale;
            return root;
        }
        private static void CreateGround()
        {
            var terrain=Group("草地与小路");
            Block("庭院地面",new Vector3(0,-0.68f,3),new Vector3(52,1.3f,42),ground,terrain);
            for(int i=0;i<9;i++)
                Block("林间步石",new Vector3(-5.2f+i*1.08f,0.015f,2.4f+(i%3-1)*0.18f),new Vector3(0.90f,0.05f,0.76f),i%3==0?stoneDark:stone,terrain,(i%3-1)*6);
        }
        private static void CreateCourtyard()
        {
            var paving=Group("遗迹铺地");
            for(int z=-1;z<7;z++) for(int x=0;x<10;x++)
            {
                if(x==0&&(z==-1||z>4)||x==9&&(z<1||z>4)||z==6&&(x<2||x>7)) continue;
                var material=(x*13+z*7)%6==0?grassLight:(x+z)%5==0?stoneLight:stone;
                Block("庭院石砖",new Vector3(x*1.02f-0.6f,0.008f,z*1.02f-0.7f),new Vector3(0.99f,0.055f,0.99f),material,paving);
            }
            var ruins=Group("残墙与石门");
            Visual("Scenery_Ruin","远侧石门",new Vector3(5.2f,0.04f,8.0f),1.45f,0,ruins);
            foreach(var spec in new[]{new Vector4(0,5.7f,2.2f,0.5f),new Vector4(8.3f,5.8f,2.0f,0.6f),new Vector4(9.2f,2.1f,0.6f,2.1f)})
            {
                Block("残墙墙脚",new Vector3(spec.x,0.2f,spec.y),new Vector3(spec.z,0.43f,spec.w),stoneDark,ruins);
                Block("残墙顶石",new Vector3(spec.x,0.47f,spec.y),new Vector3(spec.z+0.07f,0.12f,spec.w+0.07f),stone,ruins);
            }
            Block("角柱",new Vector3(8.9f,0.69f,5.8f),new Vector3(0.75f,1.4f,0.7f),stone,ruins,-4);
            Block("角柱顶",new Vector3(8.9f,1.44f,5.8f),new Vector3(0.91f,0.17f,0.84f),stoneLight,ruins,-4);
            foreach(var pos in new[]{new Vector3(-0.8f,0,4.5f),new Vector3(8.6f,0,-1.8f),new Vector3(9.4f,0,5.3f),new Vector3(0.6f,0,6.8f)})
                Visual("Scenery_Rock","庭院碎石",pos,0.9f,pos.x*13,ruins);
        }
        private static Renderer CreateTarget(Vector3 center)
        {
            var target=Group("箱子目标纹样");Renderer feature=null;
            foreach(var edge in new[]{new Vector3(-0.87f,0,0),new Vector3(0.87f,0,0),new Vector3(0,0,-0.87f),new Vector3(0,0,0.87f)})
            {
                var line=Block("金色嵌线",center+edge,new Vector3(edge.x==0?1.78f:0.055f,0.025f,edge.z==0?1.78f:0.055f),gold,target);
                if(feature==null) feature=line.GetComponent<Renderer>();
            }
            foreach(float x in new[]{-1.03f,1.03f}) foreach(float z in new[]{-1.03f,1.03f})
                Block("角部嵌石",center+new Vector3(x,-0.006f,z),new Vector3(0.16f,0.03f,0.16f),gold,target,45);
            return feature;
        }
        private static void CreateForest(List<Transform> moving)
        {
            var forest=Group("林木与花草");
            var positions=new[]{new Vector2(-8,5),new Vector2(-6.6f,7),new Vector2(-9.7f,8.6f),new Vector2(-4.0f,9.3f),new Vector2(-1.3f,10.2f),
                new Vector2(1.5f,11),new Vector2(4.2f,11.7f),new Vector2(7.4f,11),new Vector2(10.2f,10),new Vector2(12.3f,7.6f),
                new Vector2(12.8f,4.4f),new Vector2(13.4f,1.6f),new Vector2(-10,-1),new Vector2(-11,2),new Vector2(14.8f,-2.5f)};
            for(int i=0;i<positions.Length;i++)
            {
                var p=positions[i];float scale=1.65f+(i%4)*0.21f;
                var tree=Visual("Scenery_Tree","庭院乔木",new Vector3(p.x,0,p.y),scale,(i%5-2)*12,forest);
                foreach(var renderer in tree.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>material.name.Contains("Leaf")?leaf:material.name.Contains("Top")?leafTop:material).ToArray();
                if(i%2==0) moving.Add(tree.transform.Find("Visual")??tree.transform);
            }
            foreach(var p in new[]{new Vector2(0,-2.8f),new Vector2(1.1f,-2.5f),new Vector2(9.8f,3.0f),new Vector2(8.4f,6.6f),
                new Vector2(-1.7f,5.9f),new Vector2(5.4f,7.2f),new Vector2(10.4f,-2),new Vector2(-2.6f,-1.8f),new Vector2(2,-3.2f),new Vector2(11.3f,5.3f)})
                Visual("Detail_Flora","林间花草",new Vector3(p.x,0.01f,p.y),1.6f,p.x*11,forest);
        }
        private static void CreateStream(List<Transform> glints)
        {
            var stream=Group("溪流与木桥");
            Block("溪岸",new Vector3(-10.5f,-0.01f,-4.5f),new Vector3(59,0.05f,2.7f),stoneDark,stream);
            Block("溪水",new Vector3(-10.5f,0.008f,-4.5f),new Vector3(59,0.03f,2.2f),water,stream);
            for(int i=0;i<15;i++)
            {
                var ripple=Block("水面反光",new Vector3(-2+i*1.27f,0.028f,-4.5f+(i%4-1.5f)*0.38f),new Vector3(0.32f+(i%3)*0.18f,0.007f,0.025f),waterLight,stream);
                glints.Add(ripple.transform);
            }
            for(int i=0;i<9;i++) Block("桥面木板",new Vector3(6.0f,0.06f,-5.65f+i*0.29f),new Vector3(1.8f,0.15f,0.25f),wood,stream);
            foreach(float x in new[]{5.33f,6.67f}) Block("木桥横梁",new Vector3(x,-0.02f,-4.5f),new Vector3(0.15f,0.16f,3),darkWood,stream);
            foreach(var p in new[]{new Vector3(3,0,-3.2f),new Vector3(10.5f,0,-3.1f),new Vector3(2.2f,0,-5.8f),new Vector3(12.1f,0,-6)})
                Visual("Scenery_Rock","溪岸石",p,1.4f,p.x*9,stream);
        }
        private static void CreateCharacter(string prefab,string name,Vector3 position,Vector3 lookAt,float scale,List<Transform> motions)
        {
            var root=Visual(prefab,name,position,scale,0);
            var role=prefab=="Unit_Warrior"?PushBoxRole.Warrior:prefab=="Unit_Thief"?PushBoxRole.Thief:prefab=="Unit_Mage"?PushBoxRole.Mage:PushBoxRole.Shield;
            PushBoxActorAppearance.EnsureDetails(root.transform.Find("Visual")??root.transform,role,false);
            Vector3 direction=lookAt-position;direction.y=0;root.transform.rotation=Quaternion.LookRotation(-direction.normalized,Vector3.up);
            motions.Add(root.transform.Find("Visual")??root.transform);
        }
        private static void CreateLighting()
        {
            var root=Group("庭院光照");
            var key=new GameObject("Sun",typeof(Light));key.transform.SetParent(root,false);key.transform.rotation=Quaternion.Euler(48,-36,0);
            var light=key.GetComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,0.96f,0.86f);light.intensity=1.25f;light.shadows=LightShadows.Soft;
            RenderSettings.sun=light;
            var fill=new GameObject("Fill",typeof(Light));fill.transform.SetParent(root,false);fill.transform.position=new Vector3(4,5,-1);
            var point=fill.GetComponent<Light>();point.type=LightType.Point;point.intensity=0.75f;point.range=9;point.color=new Color(0.94f,0.97f,1);point.shadows=LightShadows.None;
        }
        private static Camera CreateCamera()
        {
            var root=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));root.tag="MainCamera";
            var camera=root.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6.5f;camera.nearClipPlane=0.1f;camera.farClipPlane=120;
            camera.backgroundColor=new Color(0.31f,0.43f,0.39f);camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=true;
            var rotation=Quaternion.Euler(44,-22,0);root.transform.SetPositionAndRotation(new Vector3(-0.5f,0,1)-(rotation*Vector3.forward)*25,rotation);
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            return camera;
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var rect=new GameObject(ObjNames.En(name),typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static RectTransform Stretch(Transform parent,string name)
        {
            var rect=Rect(parent,name,Vector2.zero,new Vector2(0.5f,0.5f),Vector2.zero,Vector2.zero);
            rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
        }
        private static Image Image(RectTransform rect,Color color)
        {var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
        private static Text Label(RectTransform rect,string value,Font font,int size,Color color,FontStyle style=FontStyle.Normal)
        {
            var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=size;text.fontStyle=style;text.color=color;
            text.alignment=TextAnchor.MiddleLeft;text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
        private static void CreateInterface(Camera camera,StartMenuController controller,StartMenuWorldMotion motion,AudioSource source)
        {
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");if(font==null) throw new InvalidOperationException("缺少界面字体。");
            var root=new GameObject("StartUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=0.8f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            var interfaceRoot=Stretch(root.transform,"菜单内容");var group=interfaceRoot.gameObject.AddComponent<CanvasGroup>();group.alpha=1;
            var column=Rect(interfaceRoot,"主菜单",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(680,650),new Vector2(136,-20));
            var white=new Color(0.97f,0.98f,0.95f);var ink=new Color(0.10f,0.23f,0.21f);
            var title=Label(Rect(column,"游戏标题",new Vector2(0,1),new Vector2(0,1),new Vector2(680,154),new Vector2(0,-8)),"PushBox",font,108,white,FontStyle.Bold);
            var shadow=title.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0.06f,0.13f,0.11f,0.38f);shadow.effectDistance=new Vector2(0,-5);
            Image(Rect(column,"标题短线",new Vector2(0,1),new Vector2(0,1),new Vector2(58,4),new Vector2(6,-183)),new Color(0.96f,0.76f,0.32f));
            CreateButton(column,"开始游戏",250,74,true,font,controller,controller.RequestStart,white,ink);
            CreateButton(column,"退出游戏",344,64,false,font,controller,controller.RequestQuit,white,ink);
            Label(Rect(interfaceRoot,"版本标记",Vector2.zero,Vector2.zero,new Vector2(200,30),new Vector2(140,52)),"DEMO",font,16,new Color(0.86f,0.92f,0.86f,0.65f));
            var hover=CreateTone("MenuHover",510,0.035f);var click=CreateTone("MenuClick",350,0.065f);
            controller.Configure(group,interfaceRoot,motion,source,hover,click);
        }
        private static void CreateButton(Transform parent,string value,float top,float height,bool primary,Font font,StartMenuController menu,UnityEngine.Events.UnityAction command,Color white,Color ink)
        {
            var rect=Rect(parent,value,new Vector2(0,1),new Vector2(0,1),new Vector2(408,height),new Vector2(0,-top));
            var image=Image(rect,primary?white:Color.clear);image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var content=Stretch(rect,"按钮内容");
            var label=Label(Rect(content,"文字",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(300,height),new Vector2(28,0)),value,font,primary?30:27,primary?ink:white,primary?FontStyle.Bold:FontStyle.Normal);
            var accent=Image(Rect(rect,"选中边线",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(4,primary?30:24),Vector2.zero),new Color(0.96f,0.76f,0.32f,primary?1:0));
            var arrow=Rect(content,"方向箭头",new Vector2(1,0.5f),new Vector2(1,0.5f),new Vector2(26,26),new Vector2(-24,0));
            var icon=arrow.gameObject.AddComponent<MenuChevronGraphic>();icon.color=primary?ink:white;icon.raycastTarget=false;
            rect.gameObject.AddComponent<StartMenuButtonFeedback>().Configure(image,accent,label,content,arrow,menu,primary);
            UnityEventTools.AddPersistentListener(button.onClick,command);
        }
        private static AudioClip CreateTone(string name,float frequency,float duration)
        {
            const int rate=22050;int count=(int)(rate*duration);string folder=TDProjectPaths.Root+"/Audio/UI";
            TDProjectPaths.EnsureFolder(folder);string path=folder+"/"+name+".wav";
            if(!File.Exists(path))
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                    for(int i=0;i<count;i++)
                    {
                        double t=i/(double)rate,envelope=(1-Math.Exp(-t/0.001))*Math.Exp(-t/0.014)*(1-t/duration);
                        double wave=Math.Sin(2*Math.PI*frequency*t)+0.28*Math.Sin(2*Math.PI*frequency*2.7*t);
                        writer.Write((short)(wave*envelope*7000));
                    }
                }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        internal static void ConfigureInput(InputSystemUIInputModule module)
        {
            string path=TDProjectPaths.Root+"/Data/Frontend/Menu.inputactions";
            if(!File.Exists(path))
            {
                var asset=ScriptableObject.CreateInstance<InputActionAsset>();var map=new InputActionMap("UI");asset.AddActionMap(map);
                map.AddAction("Point",InputActionType.PassThrough,"<Mouse>/position",expectedControlLayout:"Vector2");
                map.AddAction("Click",InputActionType.PassThrough,"<Mouse>/leftButton",expectedControlLayout:"Button");
                map.AddAction("RightClick",InputActionType.PassThrough,"<Mouse>/rightButton",expectedControlLayout:"Button");
                map.AddAction("Scroll",InputActionType.PassThrough,"<Mouse>/scroll",expectedControlLayout:"Vector2");
                TDProjectPaths.EnsureFolder(TDProjectPaths.Root+"/Data/Frontend");File.WriteAllText(path,asset.ToJson());Object.DestroyImmediate(asset);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            module.actionsAsset=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            var actions=AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name)=>actions.First(reference=>reference.action.name==name);
            module.point=Action("Point");module.leftClick=Action("Click");module.rightClick=Action("RightClick");module.scrollWheel=Action("Scroll");
        }
    }
}
