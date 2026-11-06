using System;
using System.IO;
using System.Linq;
using TD.PushBox.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace JobTest.TacticsVoxel
{
    public static class StartMenuPreview
    {
        private static int frames;
        public static void BuildAndCapture()
        {
            try
            {
                if(!File.Exists(StartMenuSceneBuilder.ScenePath)) StartMenuSceneBuilder.Build();
                else EditorSceneManager.OpenScene(StartMenuSceneBuilder.ScenePath);
                frames=0;EditorApplication.update+=CaptureWhenReady;
            }
            catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static void CaptureWhenReady()
        {
            if(++frames<12) {EditorApplication.QueuePlayerLoopUpdate();return;}
            EditorApplication.update-=CaptureWhenReady;
            try
            {
                ValidateScene();
                Capture(1920,1080);Capture(1280,800);
                Debug.Log("START_MENU_PREVIEW: two editor-only renders passed; no PlayMode or gameplay navigation.");
                EditorApplication.Exit(0);
            }
            catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
        }
        private static void ValidateScene()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            foreach(var root in roots)
                foreach(var node in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject)>0) throw new InvalidOperationException("Missing menu script: "+node.name);
            var controller=Object.FindFirstObjectByType<StartMenuController>();
            var buttons=Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            if(controller==null||buttons.Length!=2) throw new InvalidOperationException("Expected one menu controller and two buttons.");
            if(buttons.Any(button=>button.name!="Start"&&button.name!="Quit")) throw new InvalidOperationException("Unexpected menu entry.");
            foreach(var button in buttons)
                if(button.onClick.GetPersistentEventCount()!=1||button.onClick.GetPersistentTarget(0)!=controller)
                    throw new InvalidOperationException("Menu button feedback is not connected: "+button.name);
            if(!Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Any(text=>text.text=="PushBox")) throw new InvalidOperationException("Missing PushBox title.");
            if(RenderSettings.fog) throw new InvalidOperationException("Menu fog must be disabled.");
        }
        private static void Capture(int width,int height)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new InvalidOperationException("Preview needs graphics, not -nographics.");
            var camera=Object.FindFirstObjectByType<Camera>();var canvas=Object.FindFirstObjectByType<Canvas>();var scaler=canvas.GetComponent<CanvasScaler>();
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                target.Create();camera.targetTexture=target;camera.aspect=width/(float)height;
                scaler.enabled=false;canvas.scaleFactor=Mathf.Sqrt(width/1920f*height/1080f);
                var labels=Object.FindObjectsByType<Text>(FindObjectsSortMode.None);
                foreach(var text in labels)
                {
                    text.font.RequestCharactersInTexture(text.text,Mathf.RoundToInt(text.fontSize*canvas.scaleFactor),text.fontStyle);
                    text.SetAllDirty();
                }
                // A dynamic font atlas rebuild invalidates UI vertices, including in editor-only renders.
                for(int pass=0;pass<3;pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                }
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                string folder="Archive/Reports/StartMenu";Directory.CreateDirectory(folder);
                File.WriteAllBytes(folder+$"/StartMenu_{width}x{height}.png",texture.EncodeToPNG());
                var pixels=texture.GetPixels32();var colors=new System.Collections.Generic.HashSet<int>();
                for(int i=0;i<pixels.Length;i+=97) colors.Add(pixels[i].r<<16|pixels[i].g<<8|pixels[i].b);
                if(colors.Count<120) throw new InvalidOperationException("Rendered scene appears blank or unlit.");
                foreach(var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                {
                    Vector3[] corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                    var bounds=corners.Select(camera.WorldToScreenPoint).ToArray();
                    if(bounds.Any(point=>point.x<0||point.y<0||point.x>width||point.y>height)) throw new InvalidOperationException("UI outside viewport: "+text.name);
                    if(text.preferredWidth>text.rectTransform.rect.width+2) throw new InvalidOperationException("UI label overflows: "+text.text);
                }
                Debug.Log($"START_MENU_PREVIEW: {width}x{height}; sampled colors={colors.Count}; text bounds passed.");
            }
            finally
            {
                camera.targetTexture=null;camera.ResetAspect();scaler.enabled=true;RenderTexture.active=previous;
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
            }
        }
    }
}
