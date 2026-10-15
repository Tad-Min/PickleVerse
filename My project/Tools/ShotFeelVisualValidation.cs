using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;

public static class ShotFeelVisualValidation
{
    static void Capture(Camera camera,string path)
    {
        var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying||PickleVerse.Progression.ProgressionRuntime.Instance.Player.id!="ShotFeelQA20261015")throw new Exception("QA Play required");
        SceneManager.LoadScene("BotMatch");await Task.Delay(250);
        var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.enabled=false;
        var main=Camera.main;main.tag="Untagged";
        var cameraGo=new GameObject("Contact QA Camera");var camera=cameraGo.AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.Skybox;
        Directory.CreateDirectory("../UnityWork/ShotFeel");long bytes=0;double ms=0;
        try
        {
            foreach(var actor in new[]{m.human,m.bot})
            {
                m.human.gameObject.SetActive(actor==m.human);m.bot.gameObject.SetActive(actor==m.bot);
                actor.ResetAt(new Vector3(0,0,-3.3f),-1);actor.RefreshPose();var shot=new ShotIntent(ShotKind.Drive,actor==m.bot);
                Vector3 target=actor.transform.TransformPoint(actor.ContactOffset(shot));
                actor.Swing(shot,target,.16f);actor.Step(Vector2.zero,false,.16f,false);actor.ConsumeContact(false,target);
                m.ball.position=target;var fx=actor.GetComponent<PaddleImpactVfx>();
                camera.transform.position=new Vector3(2.35f,1.85f,-6.1f);camera.transform.LookAt(actor.transform.position+Vector3.up*.98f);
                fx.Advance(.035f);Capture(camera,"../UnityWork/ShotFeel/"+(actor==m.human?"female-contact":"male-power-contact")+".png");
                fx.Play(target,shot,Vector3.forward);fx.Advance(.01f);
                var timer=new System.Diagnostics.Stopwatch();long start=GC.GetAllocatedBytesForCurrentThread();timer.Start();
                for(int i=0;i<1000;i++){fx.Play(target,shot,Vector3.forward);fx.Advance(.03f);fx.Clear();}
                timer.Stop();bytes+=GC.GetAllocatedBytesForCurrentThread()-start;ms+=timer.Elapsed.TotalMilliseconds;
            }
            return new{images=2,iterations=2000,vfxManagedBytes=bytes,vfxMilliseconds=ms};
        }
        finally{main.tag="MainCamera";UnityEngine.Object.DestroyImmediate(cameraGo);SceneManager.LoadScene("Menu");}
    }
}
