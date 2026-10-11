using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using PickleVerse.Audio;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;
using PickleVerse.FrontEnd;
using PickleVerse.Shop;

public static class AnimeUiValidation
{
    sealed class Memory:ITutorialProgressStore{readonly Dictionary<string,int> data=new Dictionary<string,int>();public int Read(string k)=>data.TryGetValue(k,out int v)?v:0;public void Write(string k,int v){data[k]=v;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(250);}
    static GameObject Hit(RectTransform rect,Canvas canvas)
    {
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);return hits.FirstOrDefault().gameObject;
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        string savedWallet=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");int done=PlayerPrefs.GetInt(TutorialProgress.CompletedKey,-1),skipped=PlayerPrefs.GetInt(TutorialProgress.SkippedKey,-1);TutorialProgress.Configure(new Memory());
        try
        {
            await Load("Menu");var player=MusicPlayer.Instance;var music=UnityEngine.Object.FindAnyObjectByType<MusicUI>();
            music.Close();var lobby=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();var canvas=lobby.GetComponentInParent<Canvas>()??lobby.safeArea.GetComponentInParent<Canvas>();
            Check("Lobby widget visible and two music voices",music.canvas.enabled&&music.mini.gameObject.activeSelf&&player.GetComponentsInChildren<AudioSource>().Length==2);
            Check("Menu line icons have renderers and persisted scripts",lobby.safeArea.GetComponentsInChildren<UiLineIcon>().All(i=>i.GetComponent<CanvasRenderer>()&&UnityEditor.MonoScript.FromMonoBehaviour(i)?.GetClass()==typeof(UiLineIcon)));
            Check("Play button still receives pointer raycast",Hit((RectTransform)lobby.playButton.transform,canvas)==lobby.playButton.gameObject);
            music.Open();Canvas.ForceUpdateCanvases();await Task.Delay(100);
            Check("Music modal acquires scope and blocks menu clicks",music.IsOpen&&FrontEndModalInput.IsOpen&&Hit((RectTransform)lobby.playButton.transform,canvas)?.GetComponentInParent<MusicUI>()==music);
            Check("Four playlist rows are instantiated exactly once",music.playlistContent.GetComponentsInChildren<MusicTrackRow>().Length==4);
            var rows=music.playlistContent.GetComponentsInChildren<MusicTrackRow>();rows[1].button.onClick.Invoke();await Task.Delay(200);
            Check("Track choice changes artwork and studio index",player.CurrentIndex==1&&music.disc.sprite==player.Track.disc&&music.title.text==player.Track.title&&music.trackIndex.text.StartsWith("02"));
            rows[0].button.onClick.Invoke();music.Close();music.Open();Check("Reopening does not duplicate playlist",music.playlistContent.GetComponentsInChildren<MusicTrackRow>().Length==4);music.Close();
            lobby.playButton.onClick.Invoke();await Task.Delay(250);
            Check("Fresh player Play opens mode selection",SceneManager.GetActiveScene().name=="ModeSelection"&&!TutorialProgress.Completed);
            var mode=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();Check("Tutorial is an explicit selectable mode",mode.tutorialButton&&mode.tutorialButton.gameObject.activeInHierarchy&&mode.tutorialButton.interactable);
            mode.OpenBot();Check("Tutorial entry hides during bot configuration",!mode.tutorialButton.gameObject.activeSelf);mode.ShowModes();Check("Tutorial returns when choosing modes",mode.tutorialButton.gameObject.activeSelf);
            mode.tutorialButton.onClick.Invoke();await Task.Delay(250);Check("Tutorial choice starts tutorial scene",SceneManager.GetActiveScene().name=="Tutorial"&&UnityEngine.Object.FindAnyObjectByType<TutorialDirector>());
            await Load("Menu");
            foreach(string name in new[]{"Shop","Collection","ModeSelection"})
            {
                await Load(name);music.Open();
                Check(name+" keeps music without widget or hidden raycasts",MusicPlayer.Instance==player&&player.Allowed&&!music.canvas.enabled&&!music.mini.gameObject.activeSelf&&!music.canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled&&!music.IsOpen&&!FrontEndModalInput.IsOpen);
                if(name!="ModeSelection")
                {var wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Check(name+" keeps currency data and sheen",wallet&&wallet.tokenpad.text!=""&&wallet.elite.text!=""&&wallet.tokenpad.transform.parent.Find("CoinIcon").GetComponent<UnityEngine.UI.Image>().material.GetFloat("_Period")==1);}
            }
            await Load("BotMatch");Check("Match music stays stopped",!player.Playing&&!player.Allowed&&!music.canvas.enabled);
            await Load("Menu");Check("Lobby restores music widget",music.canvas.enabled&&music.mini.gameObject.activeSelf&&!music.IsOpen);
            Check("One active camera listener and EventSystem",UnityEngine.Object.FindObjectsByType<AudioListener>().Count(x=>x.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(x=>x.enabled)==1);
            Check("Real currency and tutorial progress unchanged",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==savedWallet&&PlayerPrefs.GetInt(TutorialProgress.CompletedKey,-1)==done&&PlayerPrefs.GetInt(TutorialProgress.SkippedKey,-1)==skipped);
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/AnimeUI");File.WriteAllText("../UnityWork/AnimeUI/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{TutorialProgress.Configure(null);UnityEngine.Object.FindAnyObjectByType<MusicUI>()?.Close();if(SettingsOverlay.IsOpen)SettingsOverlay.Close();await Load("Menu");}
    }
}
