using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using PickleVerse.Audio;
using PickleVerse.FrontEnd;
using PickleVerse.Multiplayer;
using PickleVerse.Shop;

public static class ModeNavigationValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Ready(string scene)
    {float deadline=Time.realtimeSinceStartup+10;while(SceneManager.GetActiveScene().name!=scene||UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy==true){if(Time.realtimeSinceStartup>deadline)throw new Exception("Navigation timeout");await Task.Delay(10);}await Task.Delay(50);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");checks.Clear();
        string wallet=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");string settings=PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,"");
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(250);var scene=SceneManager.GetActiveScene();var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);var sky=RenderSettings.skybox;var music=MusicPlayer.Instance;
            for(int pass=0;pass<3;pass++)
            {
                float dance=home.DanceTime;var button=UnityEngine.Object.FindAnyObjectByType<MenuPlayMultiplayer>();button.Play();button.Play();await Ready("ModeSelection");
                Check("Mode keeps one suspended lobby "+pass,scene.isLoaded&&SceneManager.sceneCount==2&&!home.enabled&&home.DanceTime-dance<.04f);
                Check("Mode has no hidden lobby render or lights "+pass,scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).All(r=>!r.enabled)&&scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)).All(l=>!l.enabled));
                Check("Mode has one listener, camera and input "+pass,UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.enabled)==1);
                var mode=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();mode.OpenBot();mode.Back();Check("Back from bot setup stays in modes "+pass,SceneManager.GetActiveScene().name=="ModeSelection"&&!mode.IsBotSetup);
                mode.Back();mode.Back();await Ready("Menu");
                Check("Return reuses exact scene and character "+pass,SceneManager.GetActiveScene()==scene&&SceneManager.sceneCount==1&&UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>()==home);
                Check("Return preserves night and resumes dance "+pass,home.enabled&&RenderSettings.skybox==sky&&home.NightWeight==1&&home.DanceTime>=dance&&home.floodlights.All(l=>l.enabled));
                Check("Return preserves audio and widget "+pass,MusicPlayer.Instance==music&&UnityEngine.Object.FindAnyObjectByType<MusicUI>().canvas.enabled);
            }
            UnityEngine.Object.FindAnyObjectByType<MenuPlayMultiplayer>().Play();await Ready("ModeSelection");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().tutorialButton.onClick.Invoke();await Task.Delay(250);
            Check("Tutorial releases suspended lobby",SceneManager.GetActiveScene().name=="Tutorial"&&!scene.isLoaded&&!home&&SceneManager.sceneCount==1);
            SceneManager.LoadScene("ModeSelection");await Task.Delay(250);UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().Back();await Ready("Menu");Check("Direct mode scene can return without cache",SceneManager.sceneCount==1&&SceneManager.GetActiveScene().name=="Menu");
            Check("Wallet and graphics unchanged",wallet==PlayerPrefs.GetString(LocalEconomyStorage.Key,"")&&settings==PlayerPrefs.GetString(PickleVerse.Settings.GameSettings.PreferenceKey,""));
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/ModePerformance");File.WriteAllText("../UnityWork/ModePerformance/navigation-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{SceneManager.LoadScene("Menu");}
    }
}
