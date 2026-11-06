using UnityEngine;

namespace TD.PushBox
{
    [DisallowMultipleComponent]
    public sealed class PushBoxActionAudio : MonoBehaviour
    {
        [SerializeField,InspectorName("音效配置")] private PushBoxAudioLibrary library;
        [SerializeField,InspectorName("总音量"),Range(0,1)] private float volume=0.8f;
        [SerializeField,InspectorName("脚步音量"),Range(0,1)] private float footstepVolume=0.55f;
        [SerializeField,InspectorName("木箱移动音量"),Range(0,1)] private float crateVolume=0.55f;
        [SerializeField,InspectorName("换位音量"),Range(0,1)] private float swapVolume=0.55f;
        private AudioSource steps;
        private AudioSource magic;
        private AudioSource effects;
        private AudioSource crates;
        private int nextCrateClip;
        private float crateRepeatUntil=float.NegativeInfinity;

        private void Awake()
        {
            if(library==null) library=Resources.Load<PushBoxAudioLibrary>(PushBoxAudioLibrary.ResourcePath);
            magic=Channel("Magic Pop");
            steps=Channel("Footsteps");
            effects=Channel("Events");
            crates=Channel("Crate Move");
            if(library==null) Debug.LogWarning("缺少推箱子行动音效配置 Audio/PushBoxActions。",this);
        }
        private AudioSource Channel(string name)
        {
            var root=new GameObject(name){hideFlags=HideFlags.DontSaveInEditor};root.transform.SetParent(transform,false);
            var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.priority=120;
            return source;
        }
        public void Swap()
        {
            if(library==null||library.Swap==null||magic==null) return;
            magic.Stop();magic.pitch=1;magic.PlayOneShot(library.Swap,volume*swapVolume);
        }
        public void Footstep()
        {
            if(library==null||steps==null||library.Footsteps==null) return;
            var clips=library.Footsteps;
            int count=0;
            foreach(var clip in clips) if(clip!=null) count++;
            if(count==0) return;
            int index=Random.Range(0,count);
            foreach(var clip in clips)
            {
                if(clip==null) continue;
                if(index--!=0) continue;
                steps.Stop();steps.PlayOneShot(clip,volume*footstepVolume);
                return;
            }
        }
        private void PlayEvent(AudioClip clip)
        {if(clip!=null&&effects!=null) effects.PlayOneShot(clip,volume);}
        public void CrateDrag(float repeatInterval)
        {
            if(library==null||library.CrateDrags==null||crates==null) return;
            var clips=library.CrateDrags;
            if(Time.unscaledTime>crateRepeatUntil) nextCrateClip=0;
            for(int i=0;i<clips.Length;i++)
            {
                var clip=clips[nextCrateClip%clips.Length];
                nextCrateClip=(nextCrateClip+1)%clips.Length;
                if(clip==null) continue;
                crates.Stop();crates.PlayOneShot(clip,volume*crateVolume);
                crateRepeatUntil=Time.unscaledTime+Mathf.Max(0.5f,repeatInterval+0.15f);
                return;
            }
        }
        public void ResetCrateSequence() {nextCrateClip=0;crateRepeatUntil=float.NegativeInfinity;}
        public void Death()=>PlayEvent(library!=null?library.Death:null);
        public void Clear()=>PlayEvent(library!=null?library.Clear:null);
        public void Crush()=>PlayEvent(library!=null?library.Crush:null);
        public void Preview()=>PlayEvent(library!=null?library.Preview:null);
        public void PlatePress()=>PlayEvent(library!=null?library.PlatePress:null);
        public void EnterLevel()
        {
            var clip=library!=null?library.EnterLevel:null;
            if(clip==null) return;
            // The entry cue must finish even when its source scene has unloaded.
            var source=Channel("Enter Level");source.transform.SetParent(null);
            DontDestroyOnLoad(source.gameObject);
            source.clip=clip;source.volume=volume;source.Play();
            Destroy(source.gameObject,clip.length+0.1f);
        }
        public void StopAll()
        {
            if(magic!=null) magic.Stop();if(steps!=null) steps.Stop();if(effects!=null) effects.Stop();if(crates!=null) crates.Stop();
            ResetCrateSequence();
        }
        private void OnDisable()=>StopAll();
    }
}
