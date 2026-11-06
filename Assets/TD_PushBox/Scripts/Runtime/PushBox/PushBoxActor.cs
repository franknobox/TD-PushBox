using System;
using System.Collections.Generic;
using UnityEngine;

namespace TD.PushBox
{
    [DisallowMultipleComponent]
    public sealed class PushBoxActor : MonoBehaviour
    {
        [SerializeField,InspectorName("角色")] private PushBoxRole role;
        [SerializeField,InspectorName("唯一标识")] private string stableId;
        [SerializeField,InspectorName("格点")] private Vector2Int gridPosition;
        [SerializeField,InspectorName("选中标记")] private GameObject selectionMarker;
        [SerializeField,InspectorName("模型根节点")] private Transform visualRoot;
        private Renderer[] flashingRenderers;
        private MaterialPropertyBlock[] originalProperties;
        private float flashUntil;
        private readonly Dictionary<Renderer,Material[]> originalMaterials=new();
        private readonly Dictionary<Material,Material> modelMaterials=new();
        public PushBoxRole Role=>role;
        public string StableId=>stableId;
        public Vector2Int GridPosition=>gridPosition;
        public Transform Visual=>visualRoot!=null?visualRoot:transform;
        public void Configure(PushBoxRole value,string id,Vector2Int point,Transform model,GameObject marker)
        {role=value;stableId=id;gridPosition=point;visualRoot=model;selectionMarker=marker;}
        public PushBoxEntity CreateState(PushBoxLevel level)
        {
            Vector3 facing=level.transform.InverseTransformDirection(-transform.forward);
            return new PushBoxEntity{id=stableId,kind=PushBoxKind.Actor,role=role,position=gridPosition,movable=true,
                facing=Mathf.Abs(facing.x)>Mathf.Abs(facing.z)?new Vector2Int(facing.x>0?1:-1,0):new Vector2Int(0,facing.z>0?1:-1)};
        }
        public void SetSelected(bool selected)
        {if(selectionMarker!=null) selectionMarker.SetActive(selected);if(!selected) ClearSelectionFlash();}
        public void FlashSelection()
        {
            ClearSelectionFlash();
            flashingRenderers=Visual.GetComponentsInChildren<Renderer>(true);
            originalProperties=new MaterialPropertyBlock[flashingRenderers.Length];
            var white=new MaterialPropertyBlock();white.SetColor("_BaseColor",Color.white);white.SetColor("_Color",Color.white);
            white.SetColor("_EmissionColor",Color.white*2);
            for(int i=0;i<flashingRenderers.Length;i++)
            {
                originalProperties[i]=new MaterialPropertyBlock();flashingRenderers[i].GetPropertyBlock(originalProperties[i]);
                flashingRenderers[i].SetPropertyBlock(white);
            }
            flashUntil=Time.unscaledTime+0.2f;
        }
        private void Update() {if(flashingRenderers!=null&&Time.unscaledTime>=flashUntil) ClearSelectionFlash();}
        private void OnEnable()
        {
            if(!Application.isPlaying) return;
            // Draw models after the overlay ring, retaining their normal depth test against the world.
            foreach(var renderer in Visual.GetComponentsInChildren<Renderer>(true))
            {
                if(selectionMarker!=null&&renderer.transform.IsChildOf(selectionMarker.transform)) continue;
                var materials=renderer.sharedMaterials;
                originalMaterials.Add(renderer,materials);
                var copies=new Material[materials.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if(material==null) continue;
                    if(!modelMaterials.TryGetValue(material,out var copy))
                    {
                        copy=new Material(material){hideFlags=HideFlags.HideAndDontSave,renderQueue=(int)UnityEngine.Rendering.RenderQueue.Overlay+1};
                        modelMaterials.Add(material,copy);
                    }
                    copies[i]=copy;
                }
                renderer.sharedMaterials=copies;
            }
        }
        private void OnDisable()
        {
            ClearSelectionFlash();
            foreach(var entry in originalMaterials) if(entry.Key!=null) entry.Key.sharedMaterials=entry.Value;
            foreach(var material in modelMaterials.Values) Destroy(material);
            originalMaterials.Clear();modelMaterials.Clear();
        }
        private void ClearSelectionFlash()
        {
            if(flashingRenderers==null) return;
            for(int i=0;i<flashingRenderers.Length;i++)
                if(flashingRenderers[i]!=null) flashingRenderers[i].SetPropertyBlock(originalProperties!=null&&i<originalProperties.Length?originalProperties[i]:null);
            flashingRenderers=null;originalProperties=null;
        }
        public void Face(Vector2Int direction,Transform board)
        {
            if(direction!=Vector2Int.zero) transform.rotation=Quaternion.LookRotation(-board.TransformDirection(new Vector3(direction.x,0,direction.y)),board.up);
        }
        public void SetPlacement(Vector2Int point,bool newId)
        {gridPosition=point;if(newId||string.IsNullOrEmpty(stableId)) stableId=Guid.NewGuid().ToString("N");}
    }
}
