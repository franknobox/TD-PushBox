using UnityEngine;

namespace TD.PushBox
{
    public sealed class PushBoxAudioLibrary : ScriptableObject
    {
        public const string ResourcePath="Audio/PushBoxActions";
        [SerializeField,InspectorName("脚步声")] private AudioClip[] footsteps=new AudioClip[0];
        [SerializeField,InspectorName("木箱拖动")] private AudioClip[] crateDrags=new AudioClip[0];
        [SerializeField,InspectorName("魔法换位")] private AudioClip swap;
        [SerializeField,InspectorName("角色死亡")] private AudioClip death;
        [SerializeField,InspectorName("关卡完成")] private AudioClip clear;
        [SerializeField,InspectorName("进入关卡")] private AudioClip enterLevel;
        [SerializeField,InspectorName("木箱压碎")] private AudioClip crush;
        [SerializeField,InspectorName("打开预览")] private AudioClip preview;
        [SerializeField,InspectorName("压力板压下/松开")] private AudioClip platePress;
        public AudioClip[] Footsteps=>footsteps;
        public AudioClip[] CrateDrags=>crateDrags;
        public AudioClip Swap=>swap;
        public AudioClip Death=>death;
        public AudioClip Clear=>clear;
        public AudioClip EnterLevel=>enterLevel;
        public AudioClip Crush=>crush;
        public AudioClip Preview=>preview;
        public AudioClip PlatePress=>platePress;
    }
}
