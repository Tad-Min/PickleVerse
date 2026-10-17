using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;
public static class AnimeUiIdleBudget
{
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");SceneManager.LoadScene("ModeSelection");await Task.Delay(700);
  var components=UnityEngine.Object.FindObjectsByType<MonoBehaviour>().Where(c=>c is UiInteractionVfx||c is UiPanelReveal||c is UiSafeArea).ToArray();
  var actions=components.Select(c=>(Action)Delegate.CreateDelegate(typeof(Action),c,c.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic))).ToArray();foreach(var action in actions)action();
  var watch=new Stopwatch();long before=GC.GetAllocatedBytesForCurrentThread();watch.Start();for(int i=0;i<10000;i++)foreach(var action in actions)action();watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
  var result=new{idleComponents=actions.Length,iterations=10000,allocatedBytes=bytes,microsecondsPerUiUpdate=watch.Elapsed.TotalMilliseconds*1000/10000,newTextures=0,sharedThemeBytes=Directory.GetFiles("Assets/FrontEndContent/ScreenRefresh","*.mat").Sum(f=>new FileInfo(f).Length)+new FileInfo("Assets/Resources/ScreenUiTheme.asset").Length,fontAtlasCount=ScreenUiTheme.Current.font.atlasTextures.Length};
  File.WriteAllText(Path.GetFullPath("../UnityWork/AnimeScreens20261016/ui-idle-budget.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
