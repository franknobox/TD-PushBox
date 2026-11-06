using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TD.PushBox.Frontend
{
    [DefaultExecutionOrder(-50),DisallowMultipleComponent]
    public sealed class LevelEntrancePresenter : MonoBehaviour
    {
        private const float PreviewDuration=0.6f;
        private PushBoxController controller;
        private PushBoxActionAudio actionAudio;
        private LevelSelectController selector;
        private PushBoxEntrance[] entrances;
        private PushBoxEntrance focused;
        private PushBoxLevelCatalog catalog;
        private PushBoxLevelEntry entry;
        private EntranceOverlay view;
        private Camera cameraView;
        private PushBoxCamera cameraControl;
        private CanvasGroup menuGroup;
        private float progress,fadeFrom,fadeElapsed;
        private bool held,modal,loading,menuWasInteractable,menuWasBlocking,cameraWasEnabled;
        public bool IsModal=>modal||loading;

        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            progress=fadeFrom=fadeElapsed=0;held=modal=loading=false;focused=null;entry=null;
            selector=GetComponent<LevelSelectController>();
            controller=FindObjectsByType<PushBoxController>(FindObjectsSortMode.None).FirstOrDefault(item=>item.gameObject.scene==gameObject.scene);
            if(controller==null||selector==null) {enabled=false;return;}
            actionAudio=controller.GetComponent<PushBoxActionAudio>();
            entrances=controller.GetComponentsInChildren<PushBoxEntrance>(true);
            cameraView=Camera.main;cameraControl=cameraView!=null?cameraView.GetComponent<PushBoxCamera>():null;
            menuGroup=GetComponent<CanvasGroup>();
            catalog=Resources.Load<PushBoxLevelCatalog>(PushBoxLevelCatalog.ResourceName);
            view=EntranceOverlay.Create(gameObject.scene);
        }

        private void Update()
        {
            if(view==null||loading) return;
            if(selector.IsLeaving) {Focus(null);return;}
            if(!modal) UpdateFocus();
            bool previewHeld=focused!=null&&Application.isFocused&&Keyboard.current?.vKey.isPressed==true;
            if(previewHeld&&entry?.preview==null)
            {
                if(Keyboard.current.vKey.wasPressedThisFrame) view.Notify(entry==null?"还没有配置关卡呢～":"请先保存对应关卡，生成地图预览。");
                previewHeld=false;
            }
            if(previewHeld!=held)
            {
                if(previewHeld&&actionAudio!=null) actionAudio.Preview();
                held=previewHeld;fadeFrom=progress;fadeElapsed=0;
            }
            fadeElapsed+=Time.unscaledDeltaTime;
            progress=Mathf.Lerp(fadeFrom,held?1:0,Mathf.SmoothStep(0,1,Mathf.Clamp01(fadeElapsed/PreviewDuration)));
            SetModal(held||progress>0);
            view.ShowPreview(entry?.preview,progress);
            if(!modal&&focused!=null&&Keyboard.current?.eKey.wasPressedThisFrame==true) Enter();
        }

        private void LateUpdate()
        {
            if(view==null) return;
            Vector3 anchor=focused!=null?focused.transform.position+Vector3.up*2.0f:Vector3.zero;
            view.ShowPrompt(cameraView,anchor,focused!=null&&!modal&&!loading);
        }

        private void UpdateFocus()
        {
            var actor=controller.SelectedActor;
            var state=actor!=null?controller.Session?.State.Find(actor.StableId):null;
            PushBoxEntrance next=null;
            if(state!=null&&!state.dead&&!controller.IsBusy&&!controller.Session.State.Failed)
                next=entrances.FirstOrDefault(item=>item!=null&&item.isActiveAndEnabled&&item.GetComponent<PushBoxObject>().Covers(state.position));
            Focus(next);
        }

        private void Focus(PushBoxEntrance next)
        {
            if(focused==next) return;
            if(focused!=null) focused.SetNumberVisible(true);
            focused=next;entry=catalog!=null&&focused!=null?catalog.Find(focused.SceneGuid):null;
            if(focused!=null) focused.SetNumberVisible(false);
        }
        private void SetModal(bool value)
        {
            if(modal==value) return;
            modal=value;controller.SetInputBlocked(value||loading);
            if(value)
            {
                if(menuGroup!=null) {menuWasInteractable=menuGroup.interactable;menuWasBlocking=menuGroup.blocksRaycasts;menuGroup.interactable=false;menuGroup.blocksRaycasts=false;}
                if(cameraControl!=null) {cameraWasEnabled=cameraControl.enabled;cameraControl.enabled=false;}
            }
            else
            {
                if(menuGroup!=null) {menuGroup.interactable=menuWasInteractable;menuGroup.blocksRaycasts=menuWasBlocking;}
                if(cameraControl!=null) cameraControl.enabled=cameraWasEnabled;
            }
        }

        private void Enter()
        {
            if(entry==null||string.IsNullOrEmpty(entry.scenePath)) {view.Notify("还没有配置关卡呢～");return;}
            if(!Application.CanStreamedLevelBeLoaded(entry.scenePath)) {view.Notify("对应关卡尚未加入构建列表，请保存关卡后重试。");return;}
            LevelRunNavigation.Begin(gameObject.scene,entrances,focused,catalog);
            SetModal(true);loading=true;
            if(actionAudio!=null) actionAudio.EnterLevel();
            StartCoroutine(Load(entry.scenePath));
        }

        private IEnumerator Load(string path)
        {
            float elapsed=0;
            while(elapsed<0.22f) {elapsed+=Time.unscaledDeltaTime;view.FadeOut(Mathf.Clamp01(elapsed/0.22f));yield return null;}
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Single);
        }
        private void OnDisable()
        {
            StopAllCoroutines();
            if(focused!=null) focused.SetNumberVisible(true);
            if(modal||loading)
            {
                if(controller!=null) controller.SetInputBlocked(false);
                if(menuGroup!=null) {menuGroup.interactable=menuWasInteractable;menuGroup.blocksRaycasts=menuWasBlocking;}
                if(cameraControl!=null) cameraControl.enabled=cameraWasEnabled;
            }
            held=modal=loading=false;progress=fadeFrom=fadeElapsed=0;focused=null;entry=null;
            if(view!=null) {view.gameObject.SetActive(false);Destroy(view.gameObject);view=null;}
        }
    }
}
