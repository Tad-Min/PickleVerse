using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using PickleVerse.Settings;
using Newtonsoft.Json;
public static class LoadingVisualValidation
{
    static Task running;const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.FlattenHierarchy;
    public static object Start(){if(!Application.isPlaying)throw new Exception("Play required");running=SafeRun();return "Loading responsive checks started";}
    static async Task SafeRun(){try{await Run();}catch(Exception e){File.WriteAllText("../UnityWork/Loading20261019/responsive.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()}));Debug.LogException(e);}}
    static bool Inside(RectTransform r)
    {var corners=new Vector3[4];r.GetWorldCorners(corners);return corners.All(p=>p.x>=-1&&p.y>=-1&&p.x<=Screen.width+1&&p.y<=Screen.height+1);}
    static async Task Run()
    {
        var results=new List<object>();var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);view.Show();view.Focus();
        var st=assembly.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();var selection=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)selection.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);
        try
        {
            SceneLoading.BeginExternal("MultiplayerCourt");await Task.Delay(250);
            foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{2400,1080},new[]{1024,768}})
            {
                var entry=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Loading QA");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();
                double until=Time.realtimeSinceStartupAsDouble+5;while((Screen.width!=size[0]||Screen.height!=size[1])&&Time.realtimeSinceStartupAsDouble<until)await Task.Delay(30);
                SceneLoading.ExternalProgress(.68f,"Đang ổn định hình ảnh");await Task.Delay(450);Canvas.ForceUpdateCanvases();var ui=SceneLoading.View;
                bool passed=Inside(ui.track)&&Inside(ui.ball)&&Inside(ui.percentage.rectTransform)&&Inside(ui.destination.rectTransform)&&Inside(ui.caption.rectTransform);
                results.Add(new{width=Screen.width,height=Screen.height,passed,backgroundAspect=ui.artwork.uvRect.ToString(),ui.DisplayedProgress});
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("../UnityWork/Loading20261019/loading-"+size[0]+"x"+size[1]+".png"));await Task.Delay(180);
            }
            File.WriteAllText("../UnityWork/Loading20261019/responsive.json",JsonConvert.SerializeObject(new{passed=results.All(r=>(bool)r.GetType().GetProperty("passed").GetValue(r)),results},Formatting.Indented));
        }
        catch(Exception e){File.WriteAllText("../UnityWork/Loading20261019/responsive.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString(),results}));Debug.LogException(e);}
        finally{SceneLoading.CancelExternal();selection.SetValue(view,prior);while((int)gt.GetMethod("GetCustomCount").Invoke(group,null)>count)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{count});view.Repaint();}
    }
}
