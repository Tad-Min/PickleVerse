using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using PickleVerse.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StadiumValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    public static object Geometry()
    {
        checks.Clear();
        foreach(var path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/BotMatch.unity","Assets/Scenes/MultiplayerCourt.unity","Assets/Scenes/Training.unity","Assets/Scenes/Tutorial.unity"})
        {
            var s=EditorSceneManager.OpenScene(path);var stadium=UnityEngine.Object.FindAnyObjectByType<StadiumEnvironment>();
            Check(s.name+" has one new stadium and no old arena",stadium&&s.GetRootGameObjects().Count(g=>g.name=="Stadium Enhanced")==1&&!s.GetRootGameObjects().Any(g=>g.name=="Arena"||g.name=="Home Arena"||g.name=="PickleballCourt"||g.name=="NetVisual"));
            var blue=stadium.competitionSurface.GetComponent<Renderer>().bounds;var kitchen=stadium.kitchenSurface.GetComponent<Renderer>().bounds;
            Check(s.name+" boundaries match analytical Out rules",Mathf.Abs(blue.max.x-Court.HalfWidth)<.0002f&&Mathf.Abs(blue.min.x+Court.HalfWidth)<.0002f&&Mathf.Abs(blue.max.z-Court.HalfLength)<.0002f&&Mathf.Abs(blue.max.y)<.0002f);
            Check(s.name+" kitchen matches volley and serve-short rules",Mathf.Abs(kitchen.max.z-Court.Kitchen)<.0002f&&Mathf.Abs(kitchen.min.z+Court.Kitchen)<.0002f&&Mathf.Abs(kitchen.max.y-.001f)<.0002f);
            var net=stadium.courtLines.sharedMesh.vertices.Select(stadium.courtLines.transform.TransformPoint).Where(v=>v.y>.1f&&Mathf.Abs(v.z)<.05f).GroupBy(v=>Mathf.Round(v.x*10000)).Select(g=>g.OrderByDescending(v=>v.y).First()).ToArray();
            Check(s.name+" net tape matches swept net profile",net.All(v=>Mathf.Abs(v.y-Court.NetHeight(v.x))<.0003f)&&Mathf.Abs(net.Max(v=>v.x)-Court.NetHalfSpan)<.0003f);
            Check(s.name+" every scanned lamp has spotlight",stadium.poleLights.Length==40&&stadium.roofLights.Length==126&&stadium.poleLights.Concat(stadium.roofLights).All(l=>l.type==LightType.Spot));
            Check(s.name+" groups fixture illumination into 8 Y and 16 roof sources",stadium.poleIllumination.Length==8&&stadium.roofIllumination.Length==16);
            stadium.ApplyNight(0);Check(s.name+" day uses Y lights and no roof lights",stadium.poleIllumination.All(l=>l.enabled)&&stadium.poleLights.Count(l=>l.enabled)==8&&stadium.roofLights.All(l=>!l.enabled)&&stadium.roofEmitters.All(r=>r.sharedMaterial==stadium.lightsOff));
            stadium.ApplyNight(1);Check(s.name+" night lights every fixture face with grouped illumination",stadium.poleLights.Count(l=>l.enabled)==8&&stadium.roofLights.Count(l=>l.enabled)==16&&stadium.roofEmitters.All(r=>r.sharedMaterial==stadium.lightsOn));
            Check(s.name+" roof beams reach the court instead of the stands",stadium.roofIllumination.All(l=>{var p=l.transform.position;var f=l.transform.forward;var aim=p-f*(p.y/f.y);return f.y<0&&Mathf.Abs(aim.x)<=Court.HalfWidth&&Mathf.Abs(aim.z)<=Court.HalfLength&&l.range>Vector3.Distance(p,aim)&&l.intensity>5;}));
            Check(s.name+" four Y sources retain player shadows",stadium.poleIllumination.Count(l=>l.shadows!=LightShadows.None)==4&&stadium.roofIllumination.All(l=>l.shadows==LightShadows.None));
            if(s.name!="Menu")Check(s.name+" gameplay HUD retains full screen resolution",s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).All(c=>c.renderMode!=RenderMode.ScreenSpaceCamera));
            Check(s.name+" no missing scene scripts",s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0));
            if(s.name=="Menu"){var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();Check("Menu references new lighting and grounded opposite characters",home.stadium==stadium&&home.femaleVisual.parent.position.y==0&&home.maleVisual.parent.position.y==0);}
            if(s.name=="BotMatch"){var intro=UnityEngine.Object.FindAnyObjectByType<BotMatchIntro>();var end=UnityEngine.Object.FindAnyObjectByType<BotMatchVictory>();Check("Intro and result cameras use stadium anchors",intro.playerEntrance==stadium.nearEntrance&&intro.opponentEntrance==stadium.farEntrance&&end.nearPresentation==stadium.nearResult&&end.farPresentation==stadium.farResult);}
            if(s.name=="MultiplayerCourt")
            {
                var ground=s.GetRootGameObjects().Single(g=>g.name=="PhysicsGround").GetComponent<BoxCollider>();Check("Multiplayer collision floor agrees with analytic plane",Mathf.Abs(ground.center.y+ground.size.y*.5f)<.0001f&&!ground.GetComponent<Renderer>());
                var segments=s.GetRootGameObjects().Where(g=>g.name.StartsWith("PhysicsNet")).Select(g=>g.GetComponent<BoxCollider>()).ToArray();Check("Multiplayer net colliders match new net",segments.All(c=>Mathf.Abs(c.transform.position.y+c.size.y*.5f-Court.NetHeight(c.transform.position.x))<.0001f)&&Mathf.Abs(segments.Min(c=>c.transform.position.x-c.size.x*.5f)+Court.NetHalfSpan)<.0001f);
            }
        }
        var result=new{passed=true,count=checks.Count,checks};File.WriteAllText("../UnityWork/Stadium/geometry-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
    public static object View(string scene="BotMatch",bool night=false,string shot="play")
    {
        if(EditorApplication.isPlaying)throw new Exception("Use in Edit mode.");
        var s=EditorSceneManager.OpenScene(scene=="Menu"?"Assets/Scenes/UI/Menu.unity":"Assets/Scenes/"+scene+".unity");
        var camera=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
        foreach(var c in s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>()))c.gameObject.SetActive(false);
        var lighting=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();if(lighting)lighting.Apply(night);if(home)home.ApplyAtmosphere(night?1:0);
        if(shot=="overview"){camera.transform.position=new Vector3(-3,23,-7);camera.transform.LookAt(new Vector3(0,1,0));camera.fieldOfView=80;}
        var target=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=camera.targetTexture;var active=RenderTexture.active;
        try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();string path="../UnityWork/Stadium/"+scene+"-"+(night?"night":"day")+"-"+shot+".png";File.WriteAllBytes(path,image.EncodeToPNG());return new{path};}
        finally{camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
    }
}
