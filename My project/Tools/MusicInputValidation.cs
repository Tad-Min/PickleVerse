using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PickleVerse.Audio;
using PickleVerse.Shop;
using PickleVerse.Settings;

public static class MusicInputValidation
{
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");var results=new List<object>();
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(250);var music=UnityEngine.Object.FindAnyObjectByType<MusicUI>();music.Open();
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));await Task.Delay(50);InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(25);
            bool closed=SceneManager.GetActiveScene().name=="Menu"&&!music.IsOpen&&!FrontEndModalInput.IsOpen;results.Add(new{escapeClosesLobbyMusicOnly=closed});if(!closed)throw new Exception("Escape leaked from music panel");
            foreach(string scene in new[]{"Shop","Collection","ModeSelection"})
            {
                SceneManager.LoadScene(scene);await Task.Delay(200);var ui=UnityEngine.Object.FindAnyObjectByType<MusicUI>();ui.Open();
                bool passed=!ui.IsOpen&&!ui.canvas.enabled&&!ui.mini.gameObject.activeSelf&&!FrontEndModalInput.IsOpen;results.Add(new{scene,widgetHiddenAndCannotAcquireFocus=passed});if(!passed)throw new Exception("Hidden music widget is interactive: "+scene);
            }
            SceneManager.LoadScene("BotMatch");await Task.Delay(200);
            int count=SfxPlayer.PlayCounts[(int)GameSfx.HitNormal];SfxPlayer.Play(GameSfx.HitNormal,Vector3.zero);await Task.Delay(10);
            bool sfx=SfxPlayer.PlayCounts[(int)GameSfx.HitNormal]==count+1&&MusicPlayer.Instance.GetComponentsInChildren<AudioSource>().Length==2&&!MusicPlayer.Instance.Playing;
            results.Add(new{sfxWorksWithMusicStopped=sfx});if(!sfx)throw new Exception("SFX regression");
            var report=new{passed=true,checks=results};File.WriteAllText("../UnityWork/Music/input-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());SceneManager.LoadScene("Menu");}
    }
}
