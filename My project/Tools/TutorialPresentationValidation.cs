using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;

public static class TutorialPresentationValidation
{
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");
        var checks=new List<object>();
        void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
        SceneManager.LoadScene("Tutorial");await Task.Delay(200);
        var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;
        Check("Scene registered for builds",SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Tutorial.unity")>=0);
        var labels=t.dimensions.GetComponentsInChildren<TMPro.TextMeshPro>(true).Concat(t.depthLabel.GetComponentsInChildren<TMPro.TextMeshPro>(true)).ToArray();
        Check("World labels have one valid billboard",labels.All(l=>l.GetComponents<TutorialBillboard>().Length==1&&l.GetComponent<TutorialBillboard>().view==t.view));
        SettingsOverlay.Open(t);await Task.Delay(250);
        Check("Settings loads and pauses lesson",SettingsOverlay.IsOpen&&t.Paused&&!t.hud.GetComponent<Canvas>().enabled);
        float elapsed=t.Elapsed;t.Advance(1);Check("Settings freezes simulation",t.Elapsed==elapsed);
        SettingsOverlay.Close();await Task.Delay(250);
        Check("Closing settings resumes lesson and UI",!SettingsOverlay.IsOpen&&!t.Paused&&t.hud.GetComponent<Canvas>().enabled);
        for(int stage=0;stage<7;stage++){for(int i=0;i<1800&&!t.CanContinue;i++)t.Advance(1f/120);t.Next();}
        for(int i=0;i<50;i++)t.Advance(1f/120,0,Vector2.left,true);t.Next();
        var cue=(RectTransform)t.hud.keyPanel.transform;
        Check("Keyboard cue stays left of player",cue.anchorMax.x<=.40f);
        t.hud.touchToggle.onClick.Invoke();await Task.Delay(100);
        Check("Touch cue stays above court",cue.anchorMin.y>=.75f);
        Check("Touch buttons use resize-safe anchors",t.touchControls.All(c=>((RectTransform)c.transform).anchorMin==((RectTransform)c.transform).anchorMax&&((RectTransform)c.transform).anchoredPosition==Vector2.zero));
        for(int i=0;i<155;i++)t.Advance(1f/120);
        var result=new{passed=true,count=checks.Count,checks};
        File.WriteAllText("../UnityWork/Tutorial/presentation-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
    }
}
