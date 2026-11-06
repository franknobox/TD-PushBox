using UnityEngine;

namespace TD.PushBox
{
    [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(PushBoxObject))]
    public sealed class PushBoxEntrance : MonoBehaviour
    {
        public const int MaxNumberLength=3;
        [SerializeField,InspectorName("关卡编号")] private string number="1-1";
        // A GUID survives scene renames and moves; loading is intentionally not implemented yet.
        [SerializeField,HideInInspector] private string sceneGuid;
        [SerializeField,HideInInspector] private TextMesh label;
        [SerializeField,HideInInspector] private TextMesh shadow;
        public string Number=>number;
        public string SceneGuid=>sceneGuid;
        public void SetNumberVisible(bool visible)
        {
            if(label!=null) label.gameObject.SetActive(visible);
        }

        public void ConfigureVisual(TextMesh text,TextMesh dropShadow)
        {label=text;shadow=dropShadow;RefreshLabel();}

        public static string LimitNumber(string value)
        {
            value??="";
            return value.Length>MaxNumberLength?value.Substring(0,MaxNumberLength):value;
        }

        private void OnValidate() {number=LimitNumber(number);}
        private void OnEnable() {RefreshLabel();}
        public void RefreshLabel()
        {
            if(label!=null&&label.text!=number) label.text=number;
            if(shadow!=null&&shadow.text!=number) shadow.text=number;
        }
        private void LateUpdate()
        {
            RefreshLabel();
            if(!Application.IsPlaying(gameObject)) return;
            var camera=Camera.main;
            if(label!=null&&camera!=null&&Quaternion.Angle(label.transform.rotation,camera.transform.rotation)>0.01f)
                label.transform.rotation=camera.transform.rotation;
        }
    }
}
