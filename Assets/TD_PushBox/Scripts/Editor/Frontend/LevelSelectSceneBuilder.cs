using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TD.PushBox;
using TD.PushBox.Frontend;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    public static class LevelSelectSceneBuilder
    {
        public const string ScenePath=TDProjectPaths.LevelSelectScene;
        private const string Materials=TDProjectPaths.Root+"/Art/Materials/StartMenu/";

        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("请在场景编辑模式下创建关卡选择。");
            if(File.Exists(ScenePath)) throw new InvalidOperationException("关卡选择场景已存在，不覆盖手工编辑。");
            var protectedScenes=new[]{TDProjectPaths.StartMenuScene,TDProjectPaths.SrpgScene,TDProjectPaths.PushBoxScene}
                .Where(File.Exists).ToDictionary(path=>path,File.ReadAllBytes);
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var palette=PushBoxAuthoring.Palette();
                var ground=palette.entries.First(entry=>entry.label=="浅灰");
                var level=TDLevelSceneFactory.Populate(scene,"关卡选择棋盘",new Vector2Int(9,6),palette,ground);
                DressBoard(level);
                var edit=new VoxelLevelEditOperations(level,palette);
                var avatar=edit.Place(new Vector2Int(4,0),palette.entries.First(entry=>entry.prefab.GetComponent<PushBoxActor>()?.Role==PushBoxRole.Warrior),0,out string error);
                if(avatar==null) throw new InvalidOperationException(error);
                avatar.GetComponent<PushBoxActor>().Face(Vector2Int.up,level.transform);
                VoxelLevelEditOperations.Record(avatar.transform);
                PushBoxAuthoring.Bake(level);

                var camera=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Camera>()).Single();
                camera.GetComponent<PushBoxCamera>().ConfigurePerspective(Vector3.zero,12.6f);
                camera.backgroundColor=new Color(0.31f,0.43f,0.39f);camera.allowHDR=true;
                var cameraData=camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                var sunlight=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Light>()).Single();
                sunlight.transform.rotation=Quaternion.Euler(48,-36,0);
                sunlight.color=new Color(1,0.96f,0.86f);sunlight.intensity=1.25f;
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(0.46f,0.52f,0.52f);

                CreateSurroundings(level.EnvironmentContainer);
                CreateInterface(camera);
                Validate(level);
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("关卡选择场景保存失败。");
                AssetDatabase.SaveAssets();
                foreach(var pair in protectedScenes)
                    if(!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) throw new InvalidOperationException("不应修改已有场景："+pair.Key);
                Debug.Log("LEVEL_SELECT: saved 9x6 walkable courtyard, one avatar and menu navigation; build settings unchanged; no PlayMode.");
            }
            finally
            {
                if(!Application.isBatchMode) EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static Material Mat(string name)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Materials+name+".mat");
            if(material==null) throw new InvalidOperationException("缺少庭院材质："+name);
            return material;
        }
        private static Transform Group(Transform parent,string name)
        {
            var root=new GameObject(ObjNames.En(name));root.transform.SetParent(parent,false);return root.transform;
        }
        private static GameObject Block(Transform parent,string name,Vector3 position,Vector3 size,string material,float yaw=0)
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Cube);root.name=ObjNames.En(name);root.transform.SetParent(parent,false);
            root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));root.transform.localScale=size;
            Object.DestroyImmediate(root.GetComponent<Collider>());root.GetComponent<Renderer>().sharedMaterial=Mat(material);return root;
        }
        private static Transform Visual(Transform parent,string prefab,string name,Vector3 position,float scale,float yaw)
        {
            var source=TDProjectPaths.Load<GameObject>(TDProjectPaths.Root+"/Prefabs/"+prefab+".prefab");
            if(source==null) throw new InvalidOperationException("缺少庭院外观："+prefab);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach(var child in root.GetComponentsInChildren<Transform>(true)
                .Where(child=>child.name=="Health Anchor"||child.name=="Selection Marker").ToArray()) Object.DestroyImmediate(child.gameObject);
            root.name=ObjNames.En(name);root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));root.transform.localScale=Vector3.one*scale;
            return root.transform;
        }

        private static void DressBoard(VoxelTacticsLevel level)
        {
            foreach(var tile in level.GetComponentsInChildren<VoxelTacticsTile>())
            {
                var p=tile.GridPosition;
                bool moss=(p.x==0||p.x==8||p.y==5)&&(p.x*3+p.y)%4==0;
                var material=Mat(moss?"GrassLight":(p.x*7+p.y*3)%9==0?"StoneLight":"Stone");
                foreach(var renderer in tile.transform.Find("Visual").GetComponentsInChildren<Renderer>())
                {renderer.sharedMaterial=material;VoxelLevelEditOperations.Record(renderer);}
            }
        }

        private static void CreateSurroundings(Transform parent)
        {
            var courtyard=Group(parent,"林间庭院");
            var ground=Group(courtyard,"地面与庭院边沿");
            Block(ground,"林地",new Vector3(0,-0.77f,1),new Vector3(64,1.4f,48),"Ground");
            Block(ground,"庭院基座",new Vector3(0,-0.42f,0),new Vector3(10.65f,0.65f,7.3f),"StoneDark");
            foreach(float side in new[]{-1f,1f})
            {
                Block(ground,"庭院侧沿",new Vector3(side*5.22f,-0.035f,0),new Vector3(0.22f,0.12f,6.9f),"StoneDark");
                Block(ground,"入口侧沿",new Vector3(side*2.98f,-0.035f,-3.45f),new Vector3(4.44f,0.12f,0.2f),"StoneDark");
            }
            Block(ground,"庭院后沿",new Vector3(0,-0.035f,3.45f),new Vector3(10.65f,0.12f,0.2f),"StoneDark");
            for(int i=0;i<3;i++)
                Block(ground,"入口步石",new Vector3(0,-0.015f,-3.8f-i*0.48f),new Vector3(1.03f,0.08f,0.4f),"Stone",i%2==0?-3:2);
            foreach(float side in new[]{-1f,1f}) for(int i=0;i<4;i++)
                Block(ground,"散落步石",new Vector3(side*(5.8f+i*0.45f),-0.025f,2.3f+i*0.17f),new Vector3(0.54f,0.07f,0.43f),"StoneDark",side*(i*8-10));

            var ruins=Group(courtyard,"残墙与石门");
            Visual(ruins,"Scenery_Ruin","远侧石门",new Vector3(0,-0.04f,5.5f),1.25f,0);
            foreach(var wall in new[]{new Vector4(-3.6f,4.35f,2.1f,0.48f),new Vector4(3.9f,4.5f,1.9f,0.48f),
                new Vector4(-5.9f,0.5f,0.42f,1.8f),new Vector4(6.0f,1.1f,0.42f,1.4f)})
            {
                Block(ruins,"低矮残墙",new Vector3(wall.x,0.16f,wall.y),new Vector3(wall.z,0.43f,wall.w),"StoneDark");
                Block(ruins,"墙顶石",new Vector3(wall.x,0.41f,wall.y),new Vector3(wall.z+0.06f,0.12f,wall.w+0.06f),"Stone");
            }
            Visual(ruins,"Object_Box","庭院木箱",new Vector3(6,-0.04f,3.5f),0.93f,-8);
            Visual(ruins,"Object_Box","叠放木箱",new Vector3(6,0.74f,3.5f),0.75f,4);
            Visual(ruins,"Object_Box","左侧木箱",new Vector3(-6.2f,-0.04f,-1.8f),0.8f,10);
            foreach(var p in new[]{new Vector2(-6.3f,3.5f),new Vector2(6.1f,-2.2f),new Vector2(-3.2f,5.7f),new Vector2(4.6f,5.8f)})
                Visual(ruins,"Scenery_Rock","庭院碎石",new Vector3(p.x,-0.05f,p.y),0.7f,p.x*11);

            var trees=new List<Transform>();var glints=new List<Transform>();
            var forest=Group(courtyard,"林木与花草");
            var positions=new[]{new Vector2(-7.6f,-3.3f),new Vector2(-8.6f,-0.4f),new Vector2(-7.8f,3.3f),new Vector2(-6.2f,6),
                new Vector2(-3.4f,7.2f),new Vector2(-0.9f,8.3f),new Vector2(2.5f,7.6f),new Vector2(5.6f,6.8f),
                new Vector2(8.2f,4.5f),new Vector2(8.5f,1.2f),new Vector2(7.8f,-2.9f),new Vector2(-6.8f,-6.8f),new Vector2(7,-6.9f)};
            for(int i=0;i<positions.Length;i++)
            {
                var p=positions[i];float scale=i>10?0.92f:1.35f+(i%3)*0.17f;
                var tree=Visual(forest,"Scenery_Tree","庭院乔木",new Vector3(p.x,-0.06f,p.y),scale,(i%5-2)*13);
                foreach(var renderer in tree.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>material.name.Contains("Top")?Mat("LeafTop"):
                        material.name.Contains("Leaf")?Mat("Leaf"):material).ToArray();
                if(i%2==0) trees.Add(tree.Find("Visual")??tree);
            }
            foreach(var p in new[]{new Vector2(-5.8f,-2.8f),new Vector2(5.9f,-3.1f),new Vector2(-2.8f,4.2f),new Vector2(2.3f,4.5f),
                new Vector2(-6.5f,1.9f),new Vector2(6.5f,-0.9f),new Vector2(-2,-4.4f),new Vector2(3.9f,-4.1f)})
                Visual(forest,"Detail_Flora","林间花草",new Vector3(p.x,-0.035f,p.y),1.35f,p.x*17);

            var stream=Group(courtyard,"溪流与木桥");
            Block(stream,"溪岸",new Vector3(0,-0.05f,-5.6f),new Vector3(64,0.07f,1.95f),"StoneDark");
            Block(stream,"溪水",new Vector3(0,-0.025f,-5.6f),new Vector3(64,0.04f,1.55f),"Water");
            for(int i=0;i<22;i++)
            {
                var ripple=Block(stream,"水面反光",new Vector3(-17+i*1.6f,0.001f,-5.6f+(i%3-1)*0.37f),
                    new Vector3(0.35f+i%3*0.13f,0.008f,0.024f),"WaterLight");
                glints.Add(ripple.transform);
            }
            for(int i=0;i<8;i++) Block(stream,"桥面木板",new Vector3(0,0.035f,-6.6f+i*0.29f),new Vector3(1.5f,0.12f,0.25f),"Wood");
            foreach(float x in new[]{-0.56f,0.56f}) Block(stream,"木桥横梁",new Vector3(x,-0.06f,-5.6f),new Vector3(0.13f,0.15f,2.55f),"WoodDark");
            foreach(var p in new[]{new Vector2(-3.8f,-4.7f),new Vector2(4.9f,-4.8f),new Vector2(-2.4f,-6.6f),new Vector2(5.5f,-6.6f)})
                Visual(stream,"Scenery_Rock","溪岸石",new Vector3(p.x,-0.055f,p.y),0.9f,p.x*12);
            courtyard.gameObject.AddComponent<StartMenuWorldMotion>().Configure(trees.ToArray(),Array.Empty<Transform>(),glints.ToArray(),null,null);
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position)
        {
            var rect=new GameObject(ObjNames.En(name),typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static Text Label(RectTransform rect,string value,Font font,int size,Color color)
        {
            var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=size;text.color=color;
            text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
        private static void CreateInterface(Camera camera)
        {
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if(font==null) throw new InvalidOperationException("缺少界面字体。");
            var root=new GameObject("SelectUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CanvasGroup));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=0.8f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            var navigation=root.AddComponent<LevelSelectController>();navigation.Configure(root.GetComponent<CanvasGroup>());
            var white=new Color(0.97f,0.98f,0.95f);var ink=new Color(0.10f,0.23f,0.21f);
            var title=Label(Rect(root.transform,"标题",Vector2.up,Vector2.up,new Vector2(280,60),new Vector2(64,-44)),"关卡选择",font,36,white);
            title.fontStyle=FontStyle.Bold;var shadow=title.gameObject.AddComponent<Shadow>();shadow.effectDistance=new Vector2(0,-2);shadow.effectColor=new Color(0,0,0,0.25f);
            var buttonRect=Rect(root.transform,"返回开始",Vector2.zero,Vector2.zero,new Vector2(192,60),new Vector2(48,48));
            var fill=buttonRect.gameObject.AddComponent<Image>();fill.color=white;
            var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=fill;button.transition=Selectable.Transition.None;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var content=Rect(buttonRect,"按钮内容",new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(192,60),Vector2.zero);
            var arrow=Rect(content,"返回箭头",new Vector2(0,0.5f),new Vector2(0.5f,0.5f),new Vector2(24,24),new Vector2(32,0));
            arrow.localRotation=Quaternion.Euler(0,0,180);var chevron=arrow.gameObject.AddComponent<MenuChevronGraphic>();chevron.color=ink;chevron.raycastTarget=false;
            var label=Label(Rect(content,"文字",new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(114,56),new Vector2(58,0)),"返回开始",font,23,ink);
            buttonRect.gameObject.AddComponent<StartMenuButtonFeedback>().Configure(fill,null,label,content,null,null,true);
            UnityEventTools.AddPersistentListener(button.onClick,navigation.ReturnToStart);
            LevelSelectInterfaceBuilder.Upgrade(root,camera.GetComponent<PushBoxCamera>(),false);
            var eventSystem=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().sendNavigationEvents=false;
            StartMenuSceneBuilder.ConfigureInput(eventSystem.GetComponent<InputSystemUIInputModule>());
            LevelSelectInterfaceBuilder.ConfigureScroll(eventSystem.GetComponent<InputSystemUIInputModule>());
        }

        private static void Validate(VoxelTacticsLevel level)
        {
            var state=new PushBoxSession(level.GetComponent<PushBoxLevel>().BuildState());
            if(state.State.size!=new Vector2Int(9,6)||state.State.cells.Count!=54||state.State.cells.Values.Any(cell=>!cell.walkable||!cell.supports))
                throw new InvalidOperationException("关卡选择必须有 9x6 格可通行地面。");
            var avatar=state.State.entities.Single();
            if(avatar.kind!=PushBoxKind.Actor||!state.Move(avatar.id,Vector2Int.up,out string error))
                throw new InvalidOperationException("关卡选择角色无法移动。");
            foreach(var node in level.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)>0)
                    throw new InvalidOperationException("关卡选择缺少脚本："+node.name);
            Debug.Log("LEVEL_SELECT_DATA: 54 cells, one avatar, one pure-rule movement passed; no level portals yet.");
        }
    }
}
