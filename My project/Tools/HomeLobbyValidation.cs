using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class HomeLobbyValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new InvalidOperationException(name);}
    static async Task Wait(Func<bool> ready)
    {
        var until=DateTime.UtcNow.AddSeconds(8);
        while(!ready()){if(DateTime.UtcNow>until)throw new TimeoutException("Scene load timed out.");await Task.Delay(50);}
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play in Menu first.");
        checks.Clear();
        var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
        Check("Home presentation loaded",home!=null&&SceneManager.GetActiveScene().name=="Menu");
        home.animateAtmosphere=false;
        Check("Old court and loose paddle removed",SceneManager.GetActiveScene().GetRootGameObjects().All(g=>g.name!="PickleballCourt"&&g.name!="PickleballPaddle"));
        Check("Copied complete BotMatch arena",GameObject.Find("Home Arena").GetComponentsInChildren<Renderer>().Length==28);
        Check("Requested Dance Win clip",home.dance.name=="PKV_Dance_Win"&&Mathf.Abs(home.dance.length-13.5f)<.001f);
        var secondary=home.femaleVisual.GetComponent<PickleVerse.CharacterMotion.CharacterSecondaryMotion>();
        Check("Hair and skirt physics initialized",secondary&&secondary.Initialized);
        var head=home.femaleVisual.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Head");
        home.SampleDance(.8f);Quaternion before=head.rotation;home.SampleDance(4.8f);
        Check("Dance changes character pose",Quaternion.Angle(before,head.rotation)>.1f);
        bool finite=true;
        for(int i=0;i<48;i++)
        {
            home.SampleDance(i*home.dance.length/47f);secondary.Step(1f/60);
            finite &= home.femaleVisual.GetComponentsInChildren<Transform>().All(t=>float.IsFinite(t.position.x)&&float.IsFinite(t.position.y)&&float.IsFinite(t.position.z));
        }
        Check("Dance loop and secondary bones remain finite",finite);
        home.EvaluateCycle(0);
        Check("Day panorama and sun",home.NightWeight==0&&home.sun.enabled&&home.floodlights.All(l=>!l.enabled)&&RenderSettings.skybox.GetFloat("_NightBlend")==0);
        home.EvaluateCycle(home.cycleSeconds*.32f);
        Check("Dusk blends skies and illumination",Mathf.Abs(home.NightWeight-.5f)<.001f&&home.sun.enabled&&home.floodlights.All(l=>l.enabled&&l.intensity>0&&l.intensity<40));
        home.EvaluateCycle(home.cycleSeconds*.55f);
        Check("Night lights illuminate court",home.NightWeight==1&&!home.sun.enabled&&home.floodlights.Length==4&&home.floodlights.All(l=>l.enabled&&Mathf.Abs(l.intensity-40)<.001f));
        home.EvaluateCycle(home.cycleSeconds*.82f);
        Check("Dawn blends back to daylight",Mathf.Abs(home.NightWeight-.5f)<.001f&&home.timeLabel.text=="BÌNH MINH");
        home.EvaluateCycle(home.cycleSeconds);
        Check("Sky cycle repeats",home.NightWeight==0&&home.timeLabel.text=="BAN NGÀY");
        float time=home.DanceTime;home.animateAtmosphere=true;await Task.Delay(800);
        Check("Animation and atmosphere tick automatically",home.DanceTime>time&&home.CycleElapsed>0);
        home.animateAtmosphere=false;home.EvaluateCycle(0);
        Check("Single camera and EventSystem",UnityEngine.Object.FindObjectsByType<Camera>().Length==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1);
        Check("Active Home hierarchy has no Missing Script",SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Sum(t=>UnityEditor.GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0);
        Canvas.ForceUpdateCanvases();
        Check("Visible UI labels have glyphs and fit",home.safeArea.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(t=>t.gameObject.activeInHierarchy).All(t=>t.font.HasCharacters(t.text)&&!t.isTextOverflowing));
        var canvas=home.playButton.GetComponentInParent<Canvas>();var r=(RectTransform)home.playButton.transform;
        var position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,r.TransformPoint(r.rect.center));var hits=new List<RaycastResult>();
        canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(new PointerEventData(EventSystem.current){position=position},hits);
        Check("Play is reachable through UI raycast",hits.Any(h=>h.gameObject==home.playButton.gameObject||h.gameObject.transform.IsChildOf(home.playButton.transform)));
        ExecuteEvents.Execute(home.playButton.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        await Wait(()=>SceneManager.GetActiveScene().name=="ModeSelection");await Task.Delay(200);
        Check("Home Play still opens mode selection",UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>()!=null);
        SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()!=null);await Task.Delay(200);
        Check("Returning to Home restarts its presentation",UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>().dance!=null&&RenderSettings.skybox!=null);
        var result=new{passed=true,count=checks.Count,checks=checks.ToArray()};Directory.CreateDirectory("../UnityWork/HomeLobby");
        File.WriteAllText("../UnityWork/HomeLobby/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
