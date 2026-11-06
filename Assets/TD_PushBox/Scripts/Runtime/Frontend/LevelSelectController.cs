using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectController : MonoBehaviour
    {
        [SerializeField,InspectorName("开始界面场景")] private string startMenuScene="StartMenu";
        [SerializeField] private CanvasGroup interfaceGroup;
        [SerializeField] private CustomLevelListPanel customLevels;
        [SerializeField,HideInInspector] private int interfaceVersion;
        private bool leaving;
        private LevelEntrancePresenter entrances;
        public bool IsLeaving=>leaving;
        private void Awake()
        {entrances=GetComponent<LevelEntrancePresenter>();if(entrances==null) entrances=gameObject.AddComponent<LevelEntrancePresenter>();}

        public void Configure(CanvasGroup group,CustomLevelListPanel list=null)
        {interfaceGroup=group;customLevels=list;interfaceVersion=list!=null?1:0;}
        private void Update()
        {
            if(entrances!=null&&entrances.IsModal||FindFirstObjectByType<PushBoxController>()?.Session?.State.Failed==true) return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame!=true) return;
            if(customLevels!=null&&customLevels.IsExpanded) customLevels.SetExpanded(false);
            else ReturnToStart();
        }
        public void ReturnToStart()
        {
            if(entrances!=null&&entrances.IsModal||FindFirstObjectByType<PushBoxController>()?.Session?.State.Failed==true) return;
            if(leaving) return;
            if(!Application.CanStreamedLevelBeLoaded(startMenuScene))
            {Debug.LogError("无法返回开始界面，请确认场景已加入 Build Settings："+startMenuScene,this);return;}
            leaving=true;StartCoroutine(Return());
        }
        private IEnumerator Return()
        {
            if(interfaceGroup!=null)
            {
                interfaceGroup.interactable=false;interfaceGroup.blocksRaycasts=false;
                float alpha=interfaceGroup.alpha,elapsed=0;
                while(elapsed<0.18f)
                {elapsed+=Time.unscaledDeltaTime;interfaceGroup.alpha=Mathf.Lerp(alpha,0,elapsed/0.18f);yield return null;}
            }
            yield return SceneManager.LoadSceneAsync(startMenuScene,LoadSceneMode.Single);
        }
        private void OnDisable()
        {
            StopAllCoroutines();leaving=false;
            if(interfaceGroup!=null) {interfaceGroup.alpha=1;interfaceGroup.interactable=true;interfaceGroup.blocksRaycasts=true;}
        }
    }
}
