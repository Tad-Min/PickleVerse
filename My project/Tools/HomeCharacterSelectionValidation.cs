using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.CharacterMotion;
using PickleVerse.FrontEnd;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class HomeCharacterSelectionValidation
{
    static readonly List<object> checks = new List<object>();
    static void Check(string name, bool ok) { checks.Add(new {name, passed=ok}); if (!ok) throw new InvalidOperationException(name); }
    static async Task Ready(string name)
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while (SceneManager.GetActiveScene().name != name || UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy == true)
        { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(name); await Task.Delay(20); }
        await Task.Delay(350);
    }
    static Quaternion[] Pose(Transform visual) { return visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("mixamorig:")).Select(t=>t.localRotation).ToArray(); }
    static bool Equal(Quaternion[] a, Quaternion[] b) { return a.Length == b.Length && a.Zip(b,(x,y)=>Quaternion.Angle(x,y)).All(x=>x<.01f); }
    static async Task Click(UnityEngine.UI.Button button)
    {
        float deadline=Time.realtimeSinceStartup+5;
        while(button.image.depth<0 || button.image.canvasRenderer.cull)
        { if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Button canvas did not render: "+button.name); await Task.Delay(20); }
        var canvas = button.GetComponentInParent<Canvas>(); var rect = (RectTransform)button.transform;
        var position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) {position=position,button=PointerEventData.InputButton.Left};
        var hits = new List<RaycastResult>(); canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(data,hits);
        Check(button.name + " receives pointer input", hits.Count > 0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)));
        ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
    }
    public static async Task<object> Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Menu Play mode first.");
        checks.Clear();
        string key = HomeLobbyPresentation.CharacterPreferenceKey;
        bool hadPreference = PlayerPrefs.HasKey(key); int previousPreference = PlayerPrefs.GetInt(key,0);
        string wallet = PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        string settings = PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"");
        try
        {
            SceneManager.LoadScene("Menu"); await Ready("Menu");
            var home = UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
            home.animateAtmosphere = false; home.ApplyAtmosphere(0);
            Check("Both character rigs are on opposite court halves", home.femaleVisual.position.z < -5 && home.maleVisual.position.z > 5);
            Check("Female has Win and Boom; male has AllNight", home.dance.name=="PKV_Dance_Win" && home.femaleBoom.name=="PKV_Dance_Boom" && home.maleDance.name=="PKV_Dance_AllNight");
            Check("No held paddle or ball on either character", new[]{home.femaleVisual,home.maleVisual}.All(v=>v.GetComponentsInChildren<Renderer>(true).All(r=>!r.name.Contains("Paddle") && !r.name.Contains("Ball"))));
            Check("No automatic Animator or Animation evaluations", new[]{home.femaleVisual,home.maleVisual}.All(v=>v.GetComponentsInChildren<Animator>(true).All(a=>!a.enabled)&&v.GetComponentsInChildren<Animation>(true).All(a=>!a.enabled)));
            Canvas.ForceUpdateCanvases(); await Click(home.femaleButton);
            Check("Female selection is marked", !home.IsMaleSelected && home.femaleSelection.gameObject.activeSelf && !home.maleSelection.gameObject.activeSelf);
            var maleIdle = Pose(home.maleVisual);
            home.SampleDance(.9f); var femaleFirst = Pose(home.femaleVisual); home.SampleDance(3.4f);
            Check("Only female changes pose during her Win dance", !Equal(femaleFirst,Pose(home.femaleVisual)) && Equal(maleIdle,Pose(home.maleVisual)));
            home.SampleDance(home.dance.length + .8f); var femaleBoom = Pose(home.femaleVisual);
            home.SampleDance(home.dance.length + 3.2f);
            Check("Female Boom plays after Win and animates the rig", home.CurrentDance==home.femaleBoom && !Equal(femaleBoom,Pose(home.femaleVisual)) && Equal(maleIdle,Pose(home.maleVisual)));
            home.SampleDance(home.dance.length + home.femaleBoom.length + .2f);
            Check("Female playlist returns to Win",home.CurrentDance==home.dance);
            await Click(home.maleButton); await Task.Delay(1800);
            Check("Male selection is marked and uses AllNight",home.IsMaleSelected && home.maleSelection.gameObject.activeSelf && !home.femaleSelection.gameObject.activeSelf && home.CurrentDance==home.maleDance);
            var femaleIdle = Pose(home.femaleVisual);
            home.SampleDance(.8f); var maleFirst = Pose(home.maleVisual); home.SampleDance(3.6f);
            Check("Only male changes pose when selected", !Equal(maleFirst,Pose(home.maleVisual)) && Equal(femaleIdle,Pose(home.femaleVisual)));
            Check("Secondary physics runs only on the selected character", !home.femaleVisual.GetComponent<CharacterSecondaryMotion>().enabled && home.maleVisual.GetComponent<CharacterSecondaryMotion>().enabled);
            Check("Camera moves to the male side without creating a camera",home.lobbyCamera.transform.position.z > 10 && UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1);
            var head = home.maleVisual.GetComponentsInChildren<Transform>().Single(t=>t.name=="mixamorig:Head"); var point=home.lobbyCamera.WorldToViewportPoint(head.position);
            Check("Selected male is visible beside the left UI",point.x>.36f && point.x<.96f && point.y>.22f && point.y<.72f && point.z>0);
            Check("Floor paddle and ball remain near the selected actor",home.courtProps.GetComponentsInChildren<Renderer>().All(r=>Vector3.Distance(r.bounds.center,home.maleVisual.position)<3));
            var femalePosition=home.femaleVisual.position; var malePosition=home.maleVisual.position;
            bool finite=true;
            foreach(bool male in new[]{false,true})
            {
                if(male)home.SelectMale();else home.SelectFemale();
                float duration=male?home.maleDance.length:home.dance.length+home.femaleBoom.length;
                for(int i=0;i<=80;i++)
                {
                    home.SampleDance(duration*i/80);
                    var active=male?home.maleVisual:home.femaleVisual;active.GetComponent<CharacterSecondaryMotion>().Step(1f/60);
                    finite &= active.GetComponentsInChildren<Transform>().All(t=>float.IsFinite(t.position.x)&&float.IsFinite(t.position.y)&&float.IsFinite(t.position.z));
                }
            }
            Check("Both dance loops and hair/skirt stay finite",finite);
            Check("Dance sampling preserves both stage positions",Vector3.Distance(home.femaleVisual.position,femalePosition)<.001f&&Vector3.Distance(home.maleVisual.position,malePosition)<.001f);
            home.SelectMale(); await Task.Delay(1800);
            var menuScene=SceneManager.GetActiveScene();var selectedMale=home.maleVisual;var sky=RenderSettings.skybox;home.ApplyAtmosphere(1);
            for(int pass=0;pass<3;pass++)
            {
                if(pass==0)home.playButton.onClick.Invoke();
                else if(pass==1)LobbyNavigation.OpenShop();else LobbyNavigation.OpenCollection();
                await Ready(pass==0?"ModeSelection":pass==1?"Shop":"Collection");
                Check("Leaving suspends both rigs " + pass,!home.enabled && new[]{home.femaleVisual,home.maleVisual}.All(v=>v.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled)));
                LobbyNavigation.ReturnToLobby(); await Ready("Menu");
                Check("Return reuses both models and preserves male selection " + pass,SceneManager.GetActiveScene()==menuScene && UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()==home && home.maleVisual==selectedMale && home.IsMaleSelected && home.CurrentDance==home.maleDance);
                Check("Return preserves lighting and one camera/listener/input " + pass,RenderSettings.skybox==sky && home.NightWeight==1 && UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1 && UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1 && UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.enabled)==1);
            }
            SceneManager.LoadScene("Menu"); await Ready("Menu"); home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
            Check("Saved male selection survives a fresh Menu load",home.IsMaleSelected && home.CurrentDance==home.maleDance && home.lobbyCamera.transform.position.z>10);
            await Click(home.femaleButton); await Task.Delay(1800);
            Check("Returning to female restores the original viewpoint",!home.IsMaleSelected && home.lobbyCamera.transform.position.z<-10 && home.femaleVisual.GetComponent<CharacterSecondaryMotion>().enabled && !home.maleVisual.GetComponent<CharacterSecondaryMotion>().enabled);
            Check("Wallet and graphics settings remain unchanged",wallet==PlayerPrefs.GetString(LocalEconomyStorage.Key,"") && settings==PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,""));
            Canvas.ForceUpdateCanvases();
            Check("Selector labels fit and have Vietnamese glyphs",home.femaleButton.transform.parent.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>t.font.HasCharacters(t.text)&&!t.isTextOverflowing));
            var result = new {passed=true,count=checks.Count,checks}; Directory.CreateDirectory("../UnityWork/HomeCharacterSelection");
            File.WriteAllText("../UnityWork/HomeCharacterSelection/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            if(hadPreference)PlayerPrefs.SetInt(key,previousPreference);else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); SceneManager.LoadScene("Menu");
        }
    }
}
