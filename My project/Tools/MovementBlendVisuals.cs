using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PickleVerse.LocalMatch;

public static class MovementBlendVisuals
{
    static void Capture(Camera c,string name)
    {
        var rt=new RenderTexture(960,540,24);var old=RenderTexture.active;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes("../UnityWork/MovingStrike/"+name+".png",image.EncodeToPNG());}
        finally{c.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
    public static object Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Menu first");
        var oldCamera=Camera.main;var renderers=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>();var enabled=renderers.Select(r=>r.enabled).ToArray();
        foreach(var renderer in renderers)renderer.enabled=false;
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/FemaleActor.prefab"));var actor=go.GetComponent<MatchActor>();actor.Initialize();
        var cameraObject=new GameObject("Movement QA Camera");var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=42;camera.clearFlags=CameraClearFlags.Skybox;oldCamera.tag="Untagged";camera.tag="MainCamera";
        try
        {
            actor.ResetAt(new Vector3(0,0,-3.3f),-1);actor.RefreshPose();actor.Swing(new ShotIntent(ShotKind.Drive),actor.transform.TransformPoint(actor.ContactOffset(new ShotIntent(ShotKind.Drive))));
            for(int frame=0;frame<28;frame++)
            {
                actor.Step(frame<6?Vector2.zero:frame<12?Vector2.right:Vector2.up,frame>=12,1f/60,true);
                camera.transform.position=actor.transform.position+new Vector3(2.35f,1.85f,-2.8f);camera.transform.LookAt(actor.transform.position+Vector3.up*.98f);
                if(frame==5||frame==8||frame==15)Capture(camera,frame==5?"before-movement":frame==8?"moving-strike": "turning-strike");
            }
            actor.ResetAt(new Vector3(0,0,-3.3f),-1);actor.RefreshPose();for(int i=0;i<20;i++)actor.Step(Vector2.up,false,1f/60,true);
            actor.Swing(new ShotIntent(ShotKind.Drive),actor.transform.TransformPoint(actor.ContactOffset(new ShotIntent(ShotKind.Drive)))+Vector3.forward*.7f);
            for(int i=0;i<12;i++)actor.Step(Vector2.up,false,1f/60,true);
            camera.transform.position=actor.transform.position+new Vector3(2.35f,1.85f,-2.8f);camera.transform.LookAt(actor.transform.position+Vector3.up*.98f);Capture(camera,"held-movement-strike");
            return new{images=4};
        }
        finally{oldCamera.tag="MainCamera";UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(go);for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=enabled[i];}
    }
}
