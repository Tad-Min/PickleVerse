using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PickleVerse.LocalMatch;
using UnityEngine;

public static class StadiumCinematicValidation
{
    public static object Run()
    {
        var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();if(!match)throw new Exception("Play BotMatch first.");
        bool enabled=match.enabled;match.enabled=false;match.SetPaused(false);var intro=match.opening;var camera=intro.followCamera.GetComponent<Camera>();var stadium=UnityEngine.Object.FindAnyObjectByType<StadiumEnvironment>();var results=new List<object>();
        try
        {
            intro.Play();
            foreach(float time in new[]{0f,1f,2.8f,4.6f,5.2f,7f,8.7f,9.5f})
            {
                intro.Seek(time);var actor=intro.Stage=="Opponent entrance"?match.bot:match.human;
                var viewport=camera.WorldToViewportPoint(actor.transform.position+Vector3.up*.9f);
                bool entrance=intro.Stage.Contains("entrance");bool visible=!entrance||(viewport.z>0&&viewport.x>.1f&&viewport.x<.9f&&viewport.y>.1f&&viewport.y<.9f);
                bool clear=Mathf.Abs(camera.transform.position.x)<12.8f&&Mathf.Abs(camera.transform.position.z)<18.4f;
                if(!visible||!clear)throw new Exception("Camera blocked/out of frame at "+time);
                results.Add(new{time,intro.Stage,visible,clear,player=match.human.transform.position.ToString(),opponent=match.bot.transform.position.ToString()});
                if(Mathf.Abs(time-2.8f)<.01f||Mathf.Abs(time-7f)<.01f)Capture(camera,"intro-"+(time<5?"player":"opponent"));
            }
            intro.Skip();if(match.human.transform.position.y!=0||intro.followCamera.enabled==false)throw new Exception("Gameplay camera or player floor failed to restore.");
            var result=new{passed=true,samples=results};File.WriteAllText("../UnityWork/Stadium/intro-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{intro.Skip();match.enabled=enabled;}
    }
    static void Capture(Camera camera,string name)
    {
        var texture=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=camera.targetTexture;var active=RenderTexture.active;
        try{camera.targetTexture=texture;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("../UnityWork/Stadium/"+name+".png",image.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
