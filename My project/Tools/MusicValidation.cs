using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Audio;
using PickleVerse.Settings;
using PickleVerse.Shop;
using PickleVerse.Multiplayer.UI;

public static class MusicValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static void DisableRoomRequests(Scene scene,LoadSceneMode mode)
    {
        foreach(var presenter in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayMultiplayerPresenter>(true)))presenter.enabled=false;
        foreach(var bootstrap in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PickleVerse.Multiplayer.CourtBootstrap>(true)))bootstrap.enabled=false;
    }
    static async Task Playing(MusicPlayer player)
    {
        float deadline=Time.realtimeSinceStartup+8;while(!player.Playing||player.Loading){if(Time.realtimeSinceStartup>deadline)throw new Exception("Music did not start: "+player.Error);await Task.Delay(25);}await Task.Delay(700);
    }
    static async Task Load(string scene)
    {SceneManager.LoadScene(scene);await Task.Delay(250);}
    static async Task Ready(string scene)
    {
        float end=Time.realtimeSinceStartup+8;
        while(SceneManager.GetActiveScene().name!=scene||UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy==true){if(Time.realtimeSinceStartup>end)throw new Exception("Scene not ready");await Task.Delay(15);}
        await Task.Delay(50);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();var saved=GameSettings.Current.Copy();var settings=PlayerPrefs.GetString(GameSettings.PreferenceKey,"");var wallet=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        SceneManager.sceneLoaded+=DisableRoomRequests;
        try
        {
            await Load("Menu");var player=MusicPlayer.Instance;var ui=UnityEngine.Object.FindAnyObjectByType<MusicUI>();var audition=saved.Copy();audition.master=1;audition.music=1;SettingsRuntime.Apply(audition);await Playing(player);
            Check("One shared player and two 2D music voices",UnityEngine.Object.FindObjectsByType<MusicPlayer>().Length==1&&player.GetComponentsInChildren<AudioSource>().Length==2&&player.GetComponentsInChildren<AudioSource>().All(s=>s.spatialBlend==0));
            Check("Four tracks and matching artwork",player.Library.tracks.Length==4&&player.Library.tracks.All(t=>t.clip&&t.disc));
            Check("Menu has mini title and caption mask",ui.miniTextRoot.activeSelf&&ui.miniTitle.text.Contains(player.Track.title)&&ui.miniViewport.GetComponent<UnityEngine.UI.RectMask2D>());
            ui.Open();Check("Music panel owns modal focus",ui.IsOpen&&FrontEndModalInput.IsOpen);
            var angle=ui.discTransform.localRotation;await Task.Delay(350);Check("Record rotates while playing",Quaternion.Angle(angle,ui.discTransform.localRotation)>3);
            var peaks=new List<object>();var samples=new float[512];
            for(int i=0;i<4;i++)
            {
                player.Select(i);await Playing(player);float peak=0;
                for(int poll=0;poll<8;poll++){foreach(var source in player.GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying)){source.GetOutputData(samples,0);peak=Mathf.Max(peak,samples.Max(s=>Mathf.Abs(s)));}if(peak>.00001f)break;await Task.Delay(100);}
                peaks.Add(new{title=player.Track.title,peak});Check(player.Track.title+" outputs audible audio samples",peak>.00001f);
                Check(player.Track.title+" selects correct disc/title",ui.disc.sprite==player.Track.disc&&ui.title.text==player.Track.title);
            }
            player.Select(2);await Playing(player);ui.Close();float offset=ui.MarqueeOffset;await Task.Delay(2300);Check("Menu title scrolls for long track name",Mathf.Abs(ui.MarqueeOffset-offset)>2);
            ui.Open();ui.playButton.onClick.Invoke();float paused=player.Position;angle=ui.discTransform.localRotation;await Task.Delay(200);
            Check("Pause freezes audio and record",player.UserPaused&&!player.Playing&&Mathf.Abs(player.Position-paused)<.1f&&Quaternion.Angle(angle,ui.discTransform.localRotation)<.01f);
            ui.playButton.onClick.Invoke();await Playing(player);Check("Resume continues playback",player.Playing&&player.Position>=paused);
            ui.progress.value=.5f;await Task.Delay(100);Check("Progress seek works",Mathf.Abs(player.Position-player.Duration*.5f)<1);
            player.Select(3);await Playing(player);player.Next();await Playing(player);Check("Next wraps last track to first",player.CurrentIndex==0);player.Previous();await Playing(player);Check("Previous wraps first to last",player.CurrentIndex==3);
            player.Seek(1);await Task.Delay(1200);await Playing(player);Check("Finished track automatically cycles playlist",player.CurrentIndex==0);
            var quiet=audition.Copy();quiet.music=.2f;quiet.effects=.37f;SettingsRuntime.Apply(quiet);await Task.Delay(50);
            Check("Music slider controls only music gain",player.GetComponentsInChildren<AudioSource>().Where(s=>s.isPlaying).All(s=>Mathf.Abs(s.volume-player.Library.gain*.2f)<.001f)&&Mathf.Abs(SettingsRuntime.EffectsVolume-.37f)<.001f);
            ui.settingsButton.onClick.Invoke();await Task.Delay(250);Check("Audio settings opens and hides music UI",SettingsOverlay.IsOpen&&!ui.IsOpen&&!ui.canvas.enabled&&!FrontEndModalInput.IsOpen);SettingsOverlay.Close();await Task.Delay(200);
            SettingsRuntime.Apply(audition);int index=player.CurrentIndex;float position=player.Position;
            var launcher=UnityEngine.Object.FindObjectsByType<ShopLauncher>().First(l=>l.name=="BtnStore");launcher.OpenShop();await Ready("Shop");
            Check("Shop continues same music without restart",MusicPlayer.Instance==player&&player.CurrentIndex==index&&player.Position>=position&&player.Playing);
            Check("Shop mini icon does not expose long caption",!ui.miniTextRoot.activeSelf&&ui.canvas.enabled);
            UnityEngine.Object.FindAnyObjectByType<ShopScreen>().OpenProduct(0);ui.Open();Check("Music panel works over full-screen skin preview",ui.IsOpen&&ui.canvas.worldCamera==Camera.main);ui.Close();UnityEngine.Object.FindAnyObjectByType<ShopScreen>().GoLobby();await Ready("Menu");
            Check("Returning from Shop keeps playback",player.CurrentIndex==index&&player.Playing&&ui.miniTextRoot.activeSelf);
            launcher=UnityEngine.Object.FindObjectsByType<ShopLauncher>().First(l=>l.name=="BtnCollection");launcher.OpenCollection();await Ready("Collection");Check("Collection music plays",player.Playing);UnityEngine.Object.FindAnyObjectByType<ShopScreen>().GoLobby();await Ready("Menu");
            await Load("ModeSelection");Check("Mode selection music plays",player.Allowed&&player.Playing);await Load("PlayMultiplayer");Check("Multiplayer room music plays without opening a room",player.Allowed&&player.Playing);
            await Load("BotMatch");Check("Bot match has no background music or music UI",!player.Allowed&&!player.Playing&&!ui.canvas.enabled&&player.GetComponentsInChildren<AudioSource>().All(s=>!s.isPlaying&&s.clip==null));
            await Load("MultiplayerCourt");Check("Online court has no background music",!player.Allowed&&player.GetComponentsInChildren<AudioSource>().All(s=>!s.isPlaying));
            await Load("Menu");await Playing(player);Check("Returning to Menu resumes saved track",player.Allowed&&player.Playing);
            Check("One listener remains active",UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1);
            Check("Real wallet and settings unchanged",PlayerPrefs.GetString(GameSettings.PreferenceKey,"")==settings&&PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==wallet);
            var report=new{passed=true,count=checks.Count,peaks,checks};Directory.CreateDirectory("../UnityWork/Music");File.WriteAllText("../UnityWork/Music/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{SceneManager.sceneLoaded-=DisableRoomRequests;SettingsRuntime.Apply(saved);if(SettingsOverlay.IsOpen)SettingsOverlay.Close();await Load("Menu");}
    }
}
