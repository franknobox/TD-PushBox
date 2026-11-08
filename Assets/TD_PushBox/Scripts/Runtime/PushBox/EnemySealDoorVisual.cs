using System.Collections;
using UnityEngine;

namespace TD.PushBox
{
    [DisallowMultipleComponent]
    public sealed class EnemySealDoorVisual : MonoBehaviour
    {
        [SerializeField] private Transform slab;
        [SerializeField] private Renderer seal;
        [SerializeField] private ParticleSystem aura;
        [SerializeField] private Light glow;
        [SerializeField] private float sinkDistance=1.46f;
        private Vector3 rest;
        private bool captured,shownOpen;
        private MaterialPropertyBlock properties;
        public bool IsAnimating {get;private set;}

        public void Configure(Transform part,Renderer mark,ParticleSystem particles,Light light,float distance)
        {slab=part;seal=mark;aura=particles;glow=light;sinkDistance=distance;captured=false;}

        public void Present(bool open,bool animate)
        {
            if(slab==null) return;
            if(!captured) {rest=slab.localPosition;captured=true;}
            if(animate&&Application.isPlaying&&open&&!shownOpen&&!IsAnimating)
            {StartCoroutine(Open());return;}
            if(IsAnimating&&animate&&open) return;
            StopAllCoroutines();IsAnimating=false;shownOpen=open;
            SetSeal(open);slab.localPosition=rest+(open?Vector3.down*sinkDistance:Vector3.zero);slab.gameObject.SetActive(!open);
            foreach(var box in GetComponentsInChildren<Collider>(true)) box.enabled=!open;
        }

        private void SetSeal(bool released)
        {
            Color color=released?new Color(0.24f,0.88f,0.34f):new Color(0.025f,0.075f,0.045f);
            if(seal!=null)
            {
                properties??=new MaterialPropertyBlock();properties.SetColor("_BaseColor",color);properties.SetColor("_Color",color);
                properties.SetColor("_EmissionColor",released?new Color(0.12f,2.2f,0.25f):new Color(0.018f,0.09f,0.032f));seal.SetPropertyBlock(properties);
            }
            if(aura!=null)
            {
                var main=aura.main;main.startColor=released?new Color(0.3f,1,0.42f,0.75f):new Color(0.04f,0.15f,0.075f,0.72f);
                if(Application.isPlaying) {aura.Clear();if(aura.gameObject.activeInHierarchy) aura.Play();}
            }
            if(glow!=null) {glow.color=released?new Color(0.25f,1,0.38f):new Color(0.06f,0.25f,0.12f);glow.intensity=released?0.75f:0.08f;}
        }

        private IEnumerator Open()
        {
            IsAnimating=true;slab.gameObject.SetActive(true);slab.localPosition=rest;SetSeal(true);
            yield return new WaitForSecondsRealtime(1);
            GetComponentInParent<PushBoxActionAudio>()?.PlatePress();
            float elapsed=0;
            while(elapsed<0.32f)
            {elapsed+=Time.unscaledDeltaTime;slab.localPosition=rest+Vector3.down*sinkDistance*Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/0.32f));yield return null;}
            shownOpen=true;IsAnimating=false;slab.gameObject.SetActive(false);
            foreach(var box in GetComponentsInChildren<Collider>(true)) box.enabled=false;
        }
        private void OnDisable() {StopAllCoroutines();IsAnimating=false;}
    }
}
