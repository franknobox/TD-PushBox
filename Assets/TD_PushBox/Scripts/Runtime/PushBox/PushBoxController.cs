using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TD.PushBox
{
    [DisallowMultipleComponent,RequireComponent(typeof(PushBoxLevel),typeof(PushBoxActionAudio))]
    public sealed class PushBoxController : MonoBehaviour
    {
        [SerializeField,InspectorName("单格移动时间"),Min(0.01f)] private float stepDuration=0.30f;
        [SerializeField,InspectorName("按住连续移动间隔"),Min(0.05f)] private float repeatDelay=0.34f;
        [SerializeField,InspectorName("换位表现时间"),Min(0.01f)] private float swapDuration=0.16f;
        [SerializeField,HideInInspector] private int motionVersion;
        private PushBoxLevel level;
        private PushBoxSession session;
        private PushBoxState initial;
        [SerializeField,HideInInspector] private PushBoxActor[] actors;
        private readonly Dictionary<string,Transform> bodies=new();
        [SerializeField,HideInInspector] private PushBoxObject[] objects;
        [SerializeField,HideInInspector] private bool hasStarted;
        [SerializeField,HideInInspector] private PushBoxSessionCheckpoint checkpoint;
        private int selected;
        private bool busy;
        private bool inputBlocked;
        private float nextMove;
        private Vector2Int heldDirection;
        private PushBoxActionAudio actionAudio;
        private GUIStyle clearStyle;
        public string LastFeedback {get;private set;}
        public bool IsBusy=>busy;
        public PushBoxSession Session=>session;
        public PushBoxActor SelectedActor=>actors!=null&&selected<actors.Length?actors[selected]:null;
        public void SetInputBlocked(bool blocked)
        {
            inputBlocked=blocked;
            if(blocked) {heldDirection=Vector2Int.zero;nextMove=Time.unscaledTime+repeatDelay;}
        }

        private void Awake()=>UpgradeMotion();
        private void OnValidate()=>UpgradeMotion();
        private void UpgradeMotion()
        {
            if(motionVersion>=1) return;
            // Existing scenes keep serialized values, so upgrade only the previous defaults.
            if(Mathf.Approximately(stepDuration,0.12f)) stepDuration=0.30f;
            if(Mathf.Approximately(repeatDelay,0.18f)) repeatDelay=0.34f;
            motionVersion=1;
        }
        private void OnEnable()
        {
            if(!Application.isPlaying||!hasStarted) return;
            if(session==null) {Initialize(checkpoint!=null);return;}
            busy=false;heldDirection=Vector2Int.zero;nextMove=Time.unscaledTime+repeatDelay;
            Present();checkpoint=null;
        }

        private void Start()
        {
            if(session!=null) return;
            hasStarted=true;Initialize(false);
        }

        private void Initialize(bool restore)
        {
            level=GetComponent<PushBoxLevel>();
            actionAudio=GetComponent<PushBoxActionAudio>();if(actionAudio==null) actionAudio=gameObject.AddComponent<PushBoxActionAudio>();
            try
            {
                string selectedId=restore?checkpoint.selectedId:null;
                if(restore)
                {
                    initial=checkpoint.initial.Read();session=PushBoxSession.RestoreCheckpoint(checkpoint);
                    var ids=session.State.entities.Select(entity=>entity.id).ToHashSet();
                    if(actors==null) actors=GetComponentsInChildren<PushBoxActor>(true).Where(actor=>ids.Contains(actor.StableId)).ToArray();
                    if(objects==null) objects=GetComponentsInChildren<PushBoxObject>(true).Where(item=>ids.Contains(item.StableId)).ToArray();
                }
                else
                {
                    actors=GetComponentsInChildren<PushBoxActor>();objects=GetComponentsInChildren<PushBoxObject>();
                    initial=level.BuildState();session=new PushBoxSession(initial);
                }
                bodies.Clear();
                foreach(var actor in actors) bodies.Add(actor.StableId,actor.transform);
                foreach(var item in objects) bodies.Add(item.StableId,item.transform);
                selected=Mathf.Max(0,Array.FindIndex(actors,actor=>actor.StableId==selectedId));
                checkpoint=null;heldDirection=Vector2Int.zero;nextMove=Time.unscaledTime+repeatDelay;busy=false;
                if(restore) Present();else StartCoroutine(OpeningFire());
            }
            catch(Exception error) {LastFeedback=error.Message;Debug.LogError("推箱子关卡无法启动："+error.Message,this);enabled=false;}
        }
        private void Update()
        {
            var keyboard=Keyboard.current;if(keyboard==null||session==null||inputBlocked) return;
            bool undo=!session.State.Failed&&keyboard.backspaceKey.wasPressedThisFrame;
#if !UNITY_EDITOR
            undo|=(keyboard.ctrlKey.isPressed||keyboard.leftMetaKey.isPressed||keyboard.rightMetaKey.isPressed)&&keyboard.zKey.wasPressedThisFrame;
#endif
            if(undo) {UndoCommand();return;}
            if(keyboard.rKey.wasPressedThisFrame)
            {Restart();return;}
            if(busy||session.State.Failed) return;
            if(session.State.Cleared) return;
            if(keyboard.qKey.wasPressedThisFrame&&actors.Length>0)
            {actionAudio.ResetCrateSequence();selected=(selected+1)%actors.Length;Present();actors[selected].FlashSelection();nextMove=Time.unscaledTime+repeatDelay;return;}
            Vector2Int pressed=Vector2Int.zero;
            if(keyboard.wKey.wasPressedThisFrame) pressed=Vector2Int.up;
            if(keyboard.sKey.wasPressedThisFrame) pressed=Vector2Int.down;
            if(keyboard.aKey.wasPressedThisFrame) pressed=Vector2Int.left;
            if(keyboard.dKey.wasPressedThisFrame) pressed=Vector2Int.right;
            if(pressed!=Vector2Int.zero) heldDirection=pressed;
            bool Held(Vector2Int d)=>d==Vector2Int.up?keyboard.wKey.isPressed:d==Vector2Int.down?keyboard.sKey.isPressed:
                d==Vector2Int.left?keyboard.aKey.isPressed:d==Vector2Int.right&&keyboard.dKey.isPressed;
            if(!Held(heldDirection))
                heldDirection=keyboard.wKey.isPressed?Vector2Int.up:keyboard.sKey.isPressed?Vector2Int.down:
                    keyboard.aKey.isPressed?Vector2Int.left:keyboard.dKey.isPressed?Vector2Int.right:Vector2Int.zero;
            if(actors.Length==0||heldDirection==Vector2Int.zero||pressed==Vector2Int.zero&&Time.unscaledTime<nextMove) return;
            nextMove=Time.unscaledTime+repeatDelay;
            var actor=actors[selected];var before=session.State.Copy();
            if(session.Move(actor.StableId,heldDirection,out string error)) StartCoroutine(Animate(before,session.State));
            else {actionAudio.ResetCrateSequence();LastFeedback=error;actor.Face(heldDirection,level.transform);}
        }
        private void Restart()
        {
            StopAllCoroutines();actionAudio.StopAll();busy=false;
            foreach(var item in objects) if(item!=null) item.ShowFire(0,level.CellSize);
            foreach(var actor in actors) if(actor!=null) actor.SetSelected(false);
            session=new PushBoxSession(initial);checkpoint=null;selected=0;heldDirection=Vector2Int.zero;
            Present();
            foreach(var item in objects)
                if(item!=null&&item.TryGetComponent<EnemySealDoorVisual>(out var sealDoor)) sealDoor.Present(false,false);
            StartCoroutine(OpeningFire());nextMove=Time.unscaledTime+repeatDelay;
        }
        public void UndoCommand()
        {
            if(session==null||busy||inputBlocked||!session.Undo()) return;
            actionAudio.StopAll();
            Present();nextMove=Time.unscaledTime+repeatDelay;
        }
        private IEnumerator Animate(PushBoxState before,PushBoxState after)
        {
            busy=true;LastFeedback=null;
            var changes=after.entities.Where(entity=>before.Find(entity.id).position!=entity.position).ToArray();
            var cues=PushBoxActionCues.Classify(before,after,actors[selected].StableId);
            if((cues&PushBoxActionCue.CrateDrag)==0) actionAudio.ResetCrateSequence();
            bool swapping=(cues&PushBoxActionCue.Swap)!=0,sounded=false;
            float duration=Mathf.Max(0.01f,swapping?swapDuration:stepDuration);
            foreach(var actor in actors) actor.Face(after.Find(actor.StableId).facing,level.transform);
            float elapsed=0;
            while(elapsed<duration)
            {
                elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
                if(!sounded&&t>=0.5f)
                {
                    if(swapping) actionAudio.Swap();
                    else if((cues&PushBoxActionCue.Footstep)!=0) actionAudio.Footstep();
                    if((cues&PushBoxActionCue.CrateDrag)!=0) actionAudio.CrateDrag(Mathf.Max(stepDuration,repeatDelay));
                    sounded=true;
                }
                foreach(var entity in changes)
                {
                    var old=before.Find(entity.id);
                    bodies[entity.id].position=swapping?level.Surface(t<0.5f?old.position:entity.position):Vector3.Lerp(level.Surface(old.position),level.Surface(entity.position),t);
                }
                yield return null;
            }
            yield return PlayCrushing(before,after);yield return PlayStrikes(after);yield return PlayFire(after);Present(true);
            if(after.entities.Any(entity=>entity.kind==PushBoxKind.Plate
                &&before.Find(entity.id) is PushBoxEntity oldPlate&&oldPlate.pressed!=entity.pressed))
                actionAudio.PlatePress();
            if(!before.Failed&&after.Failed) actionAudio.Death();
            while(objects.Any(item=>item.IsDoorAnimating)) yield return null;
            if(!before.Cleared&&after.Cleared) actionAudio.Clear();
            if(after.Failed) LastFeedback="角色已死亡，请按 Ctrl+Z 撤销。";
            busy=false;
        }
        private IEnumerator OpeningFire()
        {
            busy=true;yield return PlayCrushing(initial,session.State);yield return PlayStrikes(session.State);yield return PlayFire(session.State);Present(true);
            if(session.State.Failed) actionAudio.Death();
            while(objects.Any(item=>item.IsDoorAnimating)) yield return null;
            if(session.State.Cleared) actionAudio.Clear();
            busy=false;
        }
        private IEnumerator PlayCrushing(PushBoxState before,PushBoxState after)
        {
            var crushed=objects.Where(item=>item!=null&&item.Kind==PushBoxKind.Crate&&after.Find(item.StableId).dead&&!before.Find(item.StableId).dead).ToArray();
            if(crushed.Length==0) yield break;
            foreach(var item in objects.Where(item=>item!=null&&item.Kind==PushBoxKind.Door)) item.Present(after.Find(item.StableId),true);
            foreach(var item in crushed) {item.gameObject.SetActive(true);item.SetCrushFlash(true);}
            actionAudio.Crush();
            yield return new WaitForSecondsRealtime(0.3f);
            foreach(var item in crushed) item.Present(after.Find(item.StableId));
        }
        private IEnumerator PlayStrikes(PushBoxState state)
        {
            var lunges=new List<(Transform body,Vector3 origin,Vector3 offset)>();
            foreach(var group in state.strikes.Where(hit=>state.Find(hit.target)?.kind==PushBoxKind.Actor).GroupBy(hit=>hit.source))
            {
                if(!bodies.TryGetValue(group.Key,out var body)||body==null) continue;
                var source=state.Find(group.Key);
                var hit=group.OrderBy(item=>(state.Find(item.target).position-source.position).sqrMagnitude).First();
                var direction=Vector3.ProjectOnPlane(level.Surface(state.Find(hit.target).position)-body.position,level.transform.up);
                lunges.Add((body,body.position,direction.normalized*Mathf.Min(level.CellSize*0.3f,direction.magnitude*0.4f)));
            }
            if(lunges.Count==0) yield break;
            float elapsed=0;
            while(elapsed<0.18f)
            {
                elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/0.18f);
                float reach=t<0.4f?Mathf.SmoothStep(0,1,t/0.4f):Mathf.SmoothStep(1,0,(t-0.4f)/0.6f);
                foreach(var lunge in lunges) lunge.body.position=lunge.origin+lunge.offset*reach;
                yield return null;
            }
            foreach(var lunge in lunges) lunge.body.position=lunge.origin;
        }
        private IEnumerator PlayFire(PushBoxState state)
        {
            if(state.fireRays.Count==0) yield break;
            foreach(var ray in state.fireRays) objects.FirstOrDefault(item=>item.StableId==ray.source)?.ShowFire(ray.cells,level.CellSize);
            yield return new WaitForSecondsRealtime(0.28f);
            foreach(var item in objects) item.ShowFire(0,level.CellSize);
        }
        private void Present(bool animateDoors=false)
        {
            LastFeedback=session.State.Failed?"角色已死亡，请按 Ctrl+Z 撤销。":session.State.Cleared?"关卡完成":null;
            if(!inputBlocked)
            {
                var camera=Camera.main?.GetComponent<PushBoxCamera>();if(camera!=null) camera.enabled=!session.State.Failed;
                foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if(canvas.gameObject.scene==gameObject.scene&&canvas.TryGetComponent<CanvasGroup>(out var group)) group.interactable=!session.State.Failed;
            }
            foreach(var entity in session.State.entities) if(bodies.TryGetValue(entity.id,out var body)&&body!=null) body.position=level.Surface(entity.position);
            for(int i=0;i<actors.Length;i++)
            {
                if(actors[i]==null) continue;
                var state=session.State.Find(actors[i].StableId);
                actors[i].SetSelected(i==selected&&!state.dead);actors[i].Face(state.facing,level.transform);
                if(state.dead) actors[i].transform.rotation*=Quaternion.Euler(90,0,0);
            }
            foreach(var item in objects) if(item!=null) item.Present(session.State.Find(item.StableId),animateDoors);
        }
        private void OnDisable()
        {
            StopAllCoroutines();if(actionAudio!=null) actionAudio.StopAll();
            if(objects!=null) foreach(var item in objects) if(item!=null) item.ShowFire(0,1);
            busy=false;
            if(!Application.isPlaying||session==null||initial==null) return;
            checkpoint=session.SaveCheckpoint(initial,SelectedActor!=null?SelectedActor.StableId:null);
            if(level!=null&&actors!=null&&objects!=null) Present();
        }
        private void OnGUI()
        {
            if(session==null||busy||!session.State.Cleared) return;
            if(clearStyle==null)
            {
                clearStyle=new GUIStyle(GUI.skin.label)
                {
                    font=Resources.Load<Font>("Fonts/NotoSansSC-Regular"),fontSize=84,fontStyle=FontStyle.Bold,
                    alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1,0.85f,0.15f)}
                };
                foreach(var state in new[]{clearStyle.hover,clearStyle.active,clearStyle.focused,
                    clearStyle.onNormal,clearStyle.onHover,clearStyle.onActive,clearStyle.onFocused})
                    state.textColor=clearStyle.normal.textColor;
            }
            // Reinforce the weight of the shared regular CJK font without changing the font asset.
            for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++)
                GUI.Label(new Rect(16+x,72+y,Mathf.Max(0,Screen.width-32),180),"关卡完成！",clearStyle);
        }
    }
}
