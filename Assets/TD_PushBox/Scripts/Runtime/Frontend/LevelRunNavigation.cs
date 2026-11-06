using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent]
    public sealed class LevelRunNavigation : MonoBehaviour
    {
        [SerializeField] private string returnScenePath,currentScenePath,currentNumber;
        [SerializeField] private LevelRunStep[] route=Array.Empty<LevelRunStep>();
        private PushBoxController controller;
        private bool loading;
        private GUIStyle buttonStyle;

        public static void Begin(Scene source,PushBoxEntrance[] entrances,PushBoxEntrance selected,PushBoxLevelCatalog catalog)
        {
            var flow=FindFirstObjectByType<LevelRunNavigation>();
            if(flow==null)
            {
                var root=new GameObject("LevelRun");DontDestroyOnLoad(root);
                flow=root.AddComponent<LevelRunNavigation>();
            }
            flow.returnScenePath=source.path;flow.currentNumber=selected.Number;
            flow.currentScenePath=catalog.Find(selected.SceneGuid).scenePath;
            var steps=entrances.Where(item=>item!=null&&item.isActiveAndEnabled)
                .Select(item=>new LevelRunStep(item.Number,catalog.Find(item.SceneGuid)?.scenePath))
                .Where(step=>!string.IsNullOrEmpty(step.scenePath)&&Application.CanStreamedLevelBeLoaded(step.scenePath));
            flow.route=LevelRunRoute.Sort(steps);flow.controller=null;flow.loading=false;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded+=SceneLoaded;
            if(!string.IsNullOrEmpty(currentScenePath)) Bind(SceneManager.GetActiveScene());
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded-=SceneLoaded;
            if(loading&&controller!=null) controller.SetInputBlocked(false);
        }
        private void SceneLoaded(Scene scene,LoadSceneMode mode) {if(mode==LoadSceneMode.Single) Bind(scene);}
        private void Bind(Scene scene)
        {
            if(scene.path!=currentScenePath) {Destroy(gameObject);return;}
            controller=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PushBoxController>()).FirstOrDefault();
            loading=false;
            if(controller==null) Destroy(gameObject);
        }

        private void Go(string path,string number=null)
        {
            if(loading||controller==null||controller.IsBusy||controller.Session?.State.Cleared!=true) return;
            if(!Application.CanStreamedLevelBeLoaded(path))
            {Debug.LogWarning("目标场景未加入构建列表，无法跳转："+path,this);return;}
            string previousPath=currentScenePath,previousNumber=currentNumber;
            loading=true;controller.SetInputBlocked(true);
            if(number!=null) {currentScenePath=path;currentNumber=number;}
            try {if(SceneManager.LoadSceneAsync(path,LoadSceneMode.Single)!=null) return;}
            catch(Exception error) {Debug.LogError("关卡跳转失败："+error.Message,this);}
            currentScenePath=previousPath;currentNumber=previousNumber;loading=false;
            controller.SetInputBlocked(false);
        }

        private void OnGUI()
        {
            if(loading||controller==null||controller.IsBusy||controller.Session?.State.Cleared!=true) return;
            if(controller.gameObject.scene.path!=currentScenePath) return;
            if(buttonStyle==null)
            {
                buttonStyle=new GUIStyle(GUI.skin.button)
                {
                    font=Resources.Load<Font>("Fonts/NotoSansSC-Regular"),fontSize=26,fontStyle=FontStyle.Bold,
                    alignment=TextAnchor.MiddleCenter,border=new RectOffset(),padding=new RectOffset()
                };
                foreach(var state in new[]{buttonStyle.normal,buttonStyle.hover,buttonStyle.active,buttonStyle.focused,
                    buttonStyle.onNormal,buttonStyle.onHover,buttonStyle.onActive,buttonStyle.onFocused})
                {state.background=null;state.textColor=new Color(0.10f,0.23f,0.21f);}
            }
            var previousMatrix=GUI.matrix;
            float scale=Mathf.Min(Screen.width/1920f,Screen.height/1080f);
            if(scale<=0) return;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            try
            {
                float width=Screen.width/scale,height=Screen.height/scale;
                if(DrawButton(new Rect(48,height-112,240,64),"<  返回选关")) Go(returnScenePath);
                var next=LevelRunRoute.Next(route,currentNumber,currentScenePath);
                if(next!=null&&DrawButton(new Rect(width-288,height-112,240,64),"下一关  >")) Go(next.scenePath,next.number);
            }
            finally {GUI.matrix=previousMatrix;}
        }
        private bool DrawButton(Rect rect,string text)
        {
            var color=GUI.color;
            GUI.color=rect.Contains(Event.current.mousePosition)?new Color(1,0.90f,0.64f):new Color(0.97f,0.98f,0.95f);
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=new Color(0.96f,0.76f,0.32f);
            GUI.DrawTexture(new Rect(rect.x,rect.y+18,4,28),Texture2D.whiteTexture);
            GUI.color=color;
            return GUI.Button(rect,text,buttonStyle);
        }
    }
}
