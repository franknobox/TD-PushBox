using UnityEngine;
using UnityEngine.InputSystem;

namespace TD.PushBox
{
    [DisallowMultipleComponent,RequireComponent(typeof(Camera))]
    public sealed class PushBoxCamera : MonoBehaviour
    {
        [SerializeField,InspectorName("观察中心")] private Vector3 focusPoint;
        [SerializeField,InspectorName("俯角"),Range(30,85)] private float pitch=60;
        [SerializeField,InspectorName("镜头距离"),Min(1)] private float distance=20;
        [SerializeField,InspectorName("透视视野（度）"),Range(30,70)] private float fieldOfView=50;
        [SerializeField,InspectorName("画面上移比例"),Range(-0.2f,0.2f)] private float verticalFramingOffset=0.05f;
        [SerializeField,InspectorName("每格滚轮缩放距离"),Min(0.01f)] private float zoomSpeed=1.5f;
        [SerializeField,InspectorName("最近距离"),Min(1)] private float minimumDistance=3;
        [SerializeField,InspectorName("最远距离"),Min(1)] private float maximumDistance=40;
        // Retained only to convert saved orthographic cameras once, without losing their framing.
        [SerializeField,HideInInspector] private float viewSize=7,minimumSize=2,maximumSize=16;
        [SerializeField,HideInInspector] private int projectionVersion;
        private Camera view;
        private float targetDistance,zoomVelocity;
        private bool zoomInputBlocked;
        public float FieldOfView=>fieldOfView;
        public float Distance=>distance;
        public void SetZoomInputBlocked(bool blocked)=>zoomInputBlocked=blocked;
        public void ConfigurePerspective(Vector3 center,float cameraDistance,float fov=50)
        {
            focusPoint=center;pitch=60;fieldOfView=Mathf.Clamp(fov,30,70);distance=Mathf.Max(3,cameraDistance);
            minimumDistance=Mathf.Max(3,distance*0.3f);maximumDistance=Mathf.Max(12,distance*2);
            zoomSpeed=1.5f;projectionVersion=1;ResetZoom();
        }
        private void OnEnable()=>ResetZoom();
        private void OnValidate()=>ResetZoom();
        private void ResetZoom()
        {
            Apply();targetDistance=distance;zoomVelocity=0;
        }
        private void Apply()
        {
            if(view==null) view=GetComponent<Camera>();
            if(view==null) return;
            if(projectionVersion<1)
            {
                float tangent=Mathf.Tan(fieldOfView*0.5f*Mathf.Deg2Rad);
                distance=Mathf.Max(1,viewSize/tangent);
                minimumDistance=Mathf.Max(1,minimumSize/tangent);
                maximumDistance=Mathf.Max(minimumDistance,maximumSize/tangent);
                zoomSpeed/=tangent;projectionVersion=1;
            }
            distance=Mathf.Clamp(distance,minimumDistance,Mathf.Max(minimumDistance,maximumDistance));
            var rotation=Quaternion.Euler(pitch,0,0);
            float framingShift=2*distance*Mathf.Tan(fieldOfView*0.5f*Mathf.Deg2Rad)*verticalFramingOffset;
            transform.SetPositionAndRotation(focusPoint-(rotation*Vector3.forward)*distance-(rotation*Vector3.up)*framingShift,rotation);
            view.orthographic=false;view.fieldOfView=fieldOfView;
            view.farClipPlane=Mathf.Max(view.farClipPlane,maximumDistance+10);
        }
        private void Update()
        {
            float scroll=zoomInputBlocked?0:Mouse.current?.scroll.ReadValue().y??0;
            // Unity 2022 on Windows reports 120 units per wheel tick, including fractional ticks.
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            scroll/=120f;
#endif
            if(scroll!=0)
                targetDistance=Mathf.Clamp(targetDistance-scroll*zoomSpeed,minimumDistance,Mathf.Max(minimumDistance,maximumDistance));
            if(distance==targetDistance&&zoomVelocity==0) return;
            distance=Mathf.SmoothDamp(distance,targetDistance,ref zoomVelocity,0.08f,Mathf.Infinity,Time.unscaledDeltaTime);
            if(Mathf.Abs(distance-targetDistance)<0.001f) {distance=targetDistance;zoomVelocity=0;}
            Apply();
        }
    }
}
