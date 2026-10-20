using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.LocalMatch.Editor
{
    public static class TouchControlsDiagnostics
    {
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../UnityWork/TouchControls"));
        public static string Prepare()
        {
            const string path="Assets/Resources/Touch/ControlsAtlas.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var t=(TextureImporter)AssetImporter.GetAtPath(path);
            t.textureType=TextureImporterType.Default;t.sRGBTexture=true;t.alphaIsTransparency=true;
            t.mipmapEnabled=false;t.isReadable=false;t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;
            t.maxTextureSize=1024;t.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"})
                t.SetPlatformTextureSettings(new TextureImporterPlatformSettings {name=platform,overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_4x4,compressionQuality=100});
            t.SaveAndReimport();
            Directory.CreateDirectory(Output);
            return "Shared atlas imported, no mipmaps/readback; ASTC 4x4 on phones.";
        }
        public static string Snapshot()
        {
            var pad=UnityEngine.Object.FindAnyObjectByType<MobileTouchPad>(FindObjectsInactive.Include);
            if(!pad)return "No pad";
            pad.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();pad.Layout();
            var rows=pad.GetComponentInParent<Canvas>(true).GetComponentsInChildren<RectTransform>(true)
                .Where(r=>r.gameObject.activeInHierarchy&&(r.GetComponent<UnityEngine.UI.Button>()||r.name.Contains("Coach")||r.name.Contains("Panel")||r.name.Contains("Instruction")))
                .Select(r=>r.name+" parent="+r.parent.name+" rect="+r.rect+" anchors="+r.anchorMin+"/"+r.anchorMax).ToArray();
            return string.Join("\n",rows);
        }
        public static string Capture(int width=844,int height=390,string direction="")
        {
            var pad=UnityEngine.Object.FindAnyObjectByType<MobileTouchPad>(FindObjectsInactive.Include);
            if(!pad)throw new InvalidOperationException("No touch pad in the scene.");
            pad.gameObject.SetActive(true);
            if(pad.Receiver is TutorialDirector tutorial)
            {
                typeof(TutorialHud).GetField("touchWanted",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(tutorial.hud,true);
                tutorial.hud.ShowLesson();
            }
            if(pad.Receiver is TrainingDirector training){training.hud.Refresh();if(!training.Paused&&training.hud.pausePanel)training.hud.pausePanel.SetActive(false);}
            var canvas=pad.GetComponentInParent<Canvas>(true);var camera=Camera.main;
            if(!camera)camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;
            var oldTarget=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(camera.nearClipPlane+.1f,1);
                Canvas.ForceUpdateCanvases();pad.Layout(((RectTransform)pad.transform).rect);
                if(direction.Length>0)
                {
                    var control=pad.Drive;
                    var e=new PointerEventData(EventSystem.current){pointerId=888,position=RectTransformUtility.WorldToScreenPoint(camera,control.transform.position)};
                    pad.Down(control,e);
                    e.position+=direction=="down"?Vector2.down*80*pad.Unit*canvas.scaleFactor:Vector2.up*80*pad.Unit*canvas.scaleFactor;
                    pad.Drag(control,e);
                }
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var raycaster=canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                var audit=new System.Collections.Generic.List<string>();
                foreach(var control in new[]{pad.Joystick,pad.Drive,pad.Dink,pad.Volley})
                {
                    var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,control.transform.position)};
                    var hits=new System.Collections.Generic.List<RaycastResult>();if(raycaster)raycaster.Raycast(e,hits);
                    audit.Add(control.name+":"+(hits.Any(h=>h.gameObject==control.gameObject)?"PASS":"FAIL")+" top="+(hits.Count>0?hits[0].gameObject.name:"none"));
                }
                image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                string name=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+"-"+width+"x"+height+(direction.Length>0?"-"+direction:"")+".png";
                Directory.CreateDirectory(Output);string path=Path.Combine(Output,name);File.WriteAllBytes(path,image.EncodeToPNG());
                File.WriteAllLines(path+".raycast.txt",audit);
                return path;
            }
            finally
            {
                pad.CancelAll();canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;
                camera.targetTexture=oldTarget;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);
                Canvas.ForceUpdateCanvases();pad.Layout();
            }
        }
        public static string BenchmarkDrag(int count=10000)
        {
            var pad=UnityEngine.Object.FindAnyObjectByType<MobileTouchPad>(FindObjectsInactive.Include);
            pad.gameObject.SetActive(true);Canvas.ForceUpdateCanvases();pad.Layout();
            var canvas=pad.GetComponentInParent<Canvas>();
            Vector2 start=RectTransformUtility.WorldToScreenPoint(null,pad.Drive.transform.position);
            var e=new PointerEventData(EventSystem.current){pointerId=777,position=start};
            pad.Down(pad.Drive,e);
            if(!pad.Gesture.Active)throw new InvalidOperationException("Choose an interactive drill first.");
            for(int i=0;i<100;i++){e.position=start+Vector2.up*(35+i%40)*pad.Unit*canvas.scaleFactor;pad.Drag(pad.Drive,e);}
            long before=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<count;i++){e.position=start+Vector2.up*(35+i%40)*pad.Unit*canvas.scaleFactor;pad.Drag(pad.Drive,e);}
            timer.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            pad.CancelAll();
            string json="{\"samples\":"+count+",\"totalCpuMs\":"+timer.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"allocatedBytes\":"+allocated+"}";
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"drag-benchmark.json"),json);return json;
        }
        public static string BenchmarkRender(int count=30)
        {
            var pad=UnityEngine.Object.FindAnyObjectByType<MobileTouchPad>(FindObjectsInactive.Include);
            var canvas=pad.GetComponentInParent<Canvas>(true);var camera=Camera.main;
            var target=camera.targetTexture;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
            bool visible=pad.gameObject.activeSelf;var rt=RenderTexture.GetTemporary(844,390,24);
            double[] ms=new double[2];
            try
            {
                camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                for(int pass=0;pass<2;pass++)
                {
                    pad.gameObject.SetActive(pass==1);Canvas.ForceUpdateCanvases();if(pass==1)pad.Layout(((RectTransform)pad.transform).rect);
                    for(int i=0;i<5;i++)camera.Render();
                    var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<count;i++)camera.Render();watch.Stop();
                    ms[pass]=watch.Elapsed.TotalMilliseconds/count;
                }
                string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string json="{\"scene\":\""+scene+"\",\"samplesPerVariant\":"+count+",\"withoutTouchCpuMs\":"+ms[0].ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"withTouchCpuMs\":"+ms[1].ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"scope\":\"Editor offscreen Camera.Render CPU submission at 844x390; excludes full gameplay and device GPU/presentation\"}";
                Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,scene+"-render-benchmark.json"),json);return json;
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;camera.targetTexture=target;
                pad.gameObject.SetActive(visible);RenderTexture.ReleaseTemporary(rt);Canvas.ForceUpdateCanvases();if(visible)pad.Layout();
            }
        }
    }
}
