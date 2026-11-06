using UnityEngine;

namespace TD.PushBox.Frontend
{
    [DisallowMultipleComponent]
    public sealed class StartMenuWorldMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] foliage=new Transform[0],actors=new Transform[0],waterGlints=new Transform[0];
        [SerializeField] private Transform featureCrate;
        [SerializeField] private Renderer targetInlay;
        private Quaternion[] foliageRotations,actorRotations;
        private Vector3[] actorPositions,glintPositions;
        private Vector3 crateScale;
        private float pulseTime=-10;
        private MaterialPropertyBlock properties;
        public void Configure(Transform[] trees,Transform[] characters,Transform[] glints,Transform crate,Renderer target)
        {foliage=trees;actors=characters;waterGlints=glints;featureCrate=crate;targetInlay=target;}
        private void Awake()
        {
            foliageRotations=new Quaternion[foliage.Length];actorPositions=new Vector3[actors.Length];actorRotations=new Quaternion[actors.Length];glintPositions=new Vector3[waterGlints.Length];
            for(int i=0;i<foliage.Length;i++) if(foliage[i]!=null) foliageRotations[i]=foliage[i].localRotation;
            for(int i=0;i<actors.Length;i++) if(actors[i]!=null) {actorPositions[i]=actors[i].localPosition;actorRotations[i]=actors[i].localRotation;}
            for(int i=0;i<waterGlints.Length;i++) if(waterGlints[i]!=null) glintPositions[i]=waterGlints[i].localPosition;
            if(featureCrate!=null) crateScale=featureCrate.localScale;
            properties=new MaterialPropertyBlock();
        }
        public void Pulse()=>pulseTime=Time.unscaledTime;
        private void Update()
        {
            float time=Time.unscaledTime;
            for(int i=0;i<foliage.Length;i++) if(foliage[i]!=null) foliage[i].localRotation=foliageRotations[i]*Quaternion.Euler(0,0,Mathf.Sin(time*0.6f+i*1.8f)*0.65f);
            for(int i=0;i<actors.Length;i++) if(actors[i]!=null)
            {
                actors[i].localPosition=actorPositions[i]+Vector3.up*(Mathf.Sin(time*1.9f+i)*0.009f);
                actors[i].localRotation=actorRotations[i]*Quaternion.Euler(0,0,Mathf.Sin(time*0.9f+i*2)*0.5f);
            }
            for(int i=0;i<waterGlints.Length;i++) if(waterGlints[i]!=null) waterGlints[i].localPosition=glintPositions[i]+Vector3.right*(Mathf.Sin(time*0.7f+i)*0.09f);
            float pulse=Mathf.Clamp01(1-(time-pulseTime)/0.35f);
            if(featureCrate!=null) featureCrate.localScale=crateScale*(1+Mathf.Sin(pulse*Mathf.PI)*0.035f);
            if(targetInlay!=null)
            {
                float glow=0.8f+Mathf.Sin(time*1.1f)*0.1f+pulse*0.6f;
                properties.SetColor("_BaseColor",new Color(0.90f,0.68f,0.21f)*glow);
                properties.SetColor("_EmissionColor",new Color(0.48f,0.28f,0.05f)*glow);
                targetInlay.SetPropertyBlock(properties);
            }
        }
    }
}
