using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent]
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup interfaceGroup;
        [SerializeField] private RectTransform interfaceRoot;
        [SerializeField] private StartMenuWorldMotion world;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hoverSound,clickSound;
        [SerializeField,InspectorName("开始游戏（打开关卡选择）")] private UnityEvent startRequested=new();
        [SerializeField,InspectorName("退出游戏请求")] private UnityEvent quitRequested=new();
        [SerializeField,InspectorName("关卡选择场景")] private string levelSelectScene="LevelSelect";
        private float entered,lastHover=-1;
        private Vector2 restingPosition;
        private bool leaving;

        public void Configure(CanvasGroup group,RectTransform root,StartMenuWorldMotion background,AudioSource source,AudioClip hover,AudioClip click)
        {interfaceGroup=group;interfaceRoot=root;world=background;audioSource=source;hoverSound=hover;clickSound=click;}
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            leaving=false;
            entered=Time.unscaledTime;
            if(interfaceRoot!=null) restingPosition=interfaceRoot.anchoredPosition;
            if(interfaceGroup!=null) {interfaceGroup.alpha=0;interfaceGroup.interactable=false;interfaceGroup.blocksRaycasts=false;}
        }
        private void Update()
        {
            if(leaving) return;
            float t=Mathf.Clamp01((Time.unscaledTime-entered)/0.75f),eased=1-Mathf.Pow(1-t,3);
            if(interfaceGroup!=null) {interfaceGroup.alpha=eased;interfaceGroup.interactable=t>0.45f;interfaceGroup.blocksRaycasts=t>0.45f;}
            if(interfaceRoot!=null) interfaceRoot.anchoredPosition=restingPosition+Vector2.down*(14*(1-eased));
        }
        public void Hover()
        {
            if(Time.unscaledTime-lastHover<0.065f) return;
            lastHover=Time.unscaledTime;
            if(audioSource!=null&&hoverSound!=null) audioSource.PlayOneShot(hoverSound,0.22f);
        }
        private void Feedback()
        {
            if(audioSource!=null&&clickSound!=null) audioSource.PlayOneShot(clickSound,0.35f);
            if(world!=null) world.Pulse();
        }
        public void RequestStart()
        {
            if(leaving) return;
            if(!Application.CanStreamedLevelBeLoaded(levelSelectScene))
            {Debug.LogError("无法打开关卡选择，请确认场景已加入 Build Settings："+levelSelectScene,this);return;}
            leaving=true;Feedback();startRequested.Invoke();StartCoroutine(OpenLevelSelect());
        }
        private IEnumerator OpenLevelSelect()
        {
            if(interfaceGroup!=null)
            {
                interfaceGroup.interactable=false;interfaceGroup.blocksRaycasts=false;
                float alpha=interfaceGroup.alpha,elapsed=0;
                while(elapsed<0.18f)
                {elapsed+=Time.unscaledDeltaTime;interfaceGroup.alpha=Mathf.Lerp(alpha,0,elapsed/0.18f);yield return null;}
            }
            yield return SceneManager.LoadSceneAsync(levelSelectScene,LoadSceneMode.Single);
        }
        // Keep old scene callbacks valid until the editor migrates their obsolete button.
        public void RequestLevelSelect()=>RequestStart();
        public void RequestQuit()
        {
            if(leaving||!Application.isPlaying) return;
            leaving=true;Feedback();quitRequested.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void OnDisable()
        {
            StopAllCoroutines();leaving=false;
            if(interfaceRoot!=null&&Application.isPlaying) interfaceRoot.anchoredPosition=restingPosition;
            if(interfaceGroup!=null) {interfaceGroup.alpha=1;interfaceGroup.interactable=true;interfaceGroup.blocksRaycasts=true;}
        }
    }
}
