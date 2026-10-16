using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using PickleVerse.Shop;
public static class AnimeScreensCapture
{
 static string folder=Path.GetFullPath("../UnityWork/AnimeScreens20261016/Preview");
 static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(900);}
 static async Task Capture(string name){Canvas.ForceUpdateCanvases();await Task.Delay(300);ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));await Task.Delay(200);}
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");Directory.CreateDirectory(folder);
  await Load("ModeSelection");await Capture("01-Modes");UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>().OpenRooms();await Capture("02-Create-Room");
  await Load("Settings");var settings=UnityEngine.Object.FindAnyObjectByType<SettingsScreen>();await Capture("03-Settings");settings.ShowTab(2);await Capture("04-Graphics");settings.ShowTab(3);await Capture("05-Controls");
  await Load("Shop");await Capture("06-Shop");UnityEngine.Object.FindAnyObjectByType<ShopScreen>().OpenProduct(0);await Capture("07-Product");
  await Load("Collection");await Capture("08-Collection");
  await Load("Tutorial");await Capture("09-Tutorial");
  var replayPath=Path.Combine(Application.persistentDataPath,"Replays");
  if(Directory.Exists(replayPath))foreach(var path in Directory.GetFiles(replayPath,"metadata.json",SearchOption.AllDirectories))
  {var metadata=JsonUtility.FromJson<PickleVerse.Replays.ReplayMetadata>(File.ReadAllText(path));if(metadata!=null&&metadata.actors.Count>=2&&metadata.chunks.Count>0&&metadata.duration>2){PickleVerse.Replays.ReplayLaunch.Select(metadata.playerId,metadata.matchId);break;}}
  await Load("Replay");await Capture("10-Replay");return new{folder,resolution=new{Screen.width,Screen.height}};
 }
}
