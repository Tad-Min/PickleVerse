using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
public static class TutorialUiLayoutValidation
{
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy;
 sealed class Memory:ITutorialProgressStore{public int Read(string key)=>0;public void Write(string key,int value){}}
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");TutorialProgress.Configure(new Memory());
  var a=typeof(Editor).Assembly;var type=a.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First();var st=a.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();var select=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)select.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);var issues=new List<string>();int checks=0;
  try
  {
   SceneManager.LoadScene("Tutorial");await Task.Delay(600);var t=UnityEngine.Object.FindAnyObjectByType<TutorialDirector>();t.enabled=false;var begin=typeof(TutorialDirector).GetMethod("Begin",Flags);
   foreach(var size in new[]{new[]{1280,720},new[]{2400,1080},new[]{1024,768}})
   {
    var entry=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Tutorial UI QA");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});select.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();var until=DateTime.UtcNow.AddSeconds(5);while(Screen.width!=size[0]||Screen.height!=size[1]){if(DateTime.UtcNow>until)throw new Exception("Resize timeout");await Task.Delay(40);}
    for(int i=0;i<TutorialCurriculum.Lessons.Length;i++)
    {
     begin.Invoke(t,new object[]{i,true});Canvas.ForceUpdateCanvases();await Task.Delay(30);
     foreach(var label in new[]{t.hud.title,t.hud.body,t.hud.keys,t.hud.instruction,t.hud.progress})
     {if(!label.gameObject.activeInHierarchy)continue;label.ForceMeshUpdate();checks++;if(label.isTextOverflowing)issues.Add(size[0]+"x"+size[1]+" lesson "+i+" "+label.name);}
    }
    begin.Invoke(t,new object[]{1,true});await Task.Delay(300);ScreenCapture.CaptureScreenshot(Path.GetFullPath("../UnityWork/AnimeScreens20261016/Preview/tutorial-"+size[0]+"x"+size[1]+".png"));await Task.Delay(150);
   }
   File.WriteAllText(Path.GetFullPath("../UnityWork/AnimeScreens20261016/tutorial-layout.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{checks,issues},Newtonsoft.Json.Formatting.Indented));
   return new{passed=issues.Count==0,checks,issues};
  }
  finally{TutorialProgress.Configure(null);select.SetValue(view,prior);int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});view.Repaint();}
 }
}
