using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using PickleVerse.Progression;
public static class MatchHudVisualProbe
{
    static Task task;
    public static object Start(){task=Run();return "Visual probe started";}
    static async Task Picture(string name,MatchScoreboard b)
    {
        await Task.Delay(450);Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.GetFullPath("../UnityWork/MatchHud20261020/probe-"+name+".png"));
        var rows=new[]{b.game,b.clock,b.firstName,b.firstPoints.current}.Select(t=>new{name=t.name,text=t.text,alpha=t.color.a,canvas=t.canvas.name,channels=t.canvas.additionalShaderChannels.ToString(),characters=t.textInfo.characterInfo.Take(t.textInfo.characterCount).Select(c=>new{c.character,c.isVisible,alpha=c.vertex_BL.color.a,uv=c.vertex_BL.uv.ToString(),scale=c.vertex_BL.uv2.ToString()}).ToArray()}).ToArray();
        File.WriteAllText("../UnityWork/MatchHud20261020/probe-"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(rows));await Task.Delay(200);
    }
    static async Task Run()
    {
        var rt=ProgressionRuntime.Instance;string original=rt.Player.id,qa="HudUI"+Guid.NewGuid().ToString("N").Substring(0,8);File.WriteAllText("../UnityWork/MatchHud20261020/original-profile.txt",original);
        try
        {
            rt.SwitchPlayer(qa,"HUD visual");var op=SceneManager.LoadSceneAsync("BotMatch");while(!op.isDone)await Task.Delay(20);await Task.Delay(300);
            var m=UnityEngine.Object.FindAnyObjectByType<BotMatch>();m.Continue();m.opening?.Skip();m.enabled=false;m.hud.enabled=false;m.hud.modal.SetActive(false);
            var b=m.hud.broadcast;b.SetTeams("BẠN · KIỂM TRA\nĐồng đội","ĐỐI THỦ\nĐồng đội B","TRỰC TUYẾN · 2V2");b.SetScores(5,7,0,1,2,0);b.SetClock(CourtClockMode.Serve,8,false,"ĐỘI BẠN · NGƯỜI 2 · LƯỢT 2");await Picture("normal",b);
            SettingsOverlay.Open(m);while(SettingsOverlay.Loading)await Task.Delay(30);SettingsOverlay.Close();while(SettingsOverlay.Loading)await Task.Delay(30);await Picture("settings",b);
            var a=typeof(UnityEditor.Editor).Assembly;var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;var vt=a.GetType("UnityEditor.GameView");var view=UnityEditor.EditorWindow.GetWindow(vt);var st=a.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",f).GetValue(null);var group=st.GetMethod("GetGroup",f).Invoke(sizes,new[]{vt.GetProperty("currentSizeGroupType",f).GetValue(view)});var gt=group.GetType();var selection=vt.GetProperty("selectedSizeIndex",f);int prior=(int)selection.GetValue(view);
            for(int step=0;step<2;step++){int w=step==0?1280:1920,h=step==0?720:1080;var entry=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),w,h,"Probe");gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Repaint();await Task.Delay(450);await Picture("resize"+step,b);}
            foreach(var mask in b.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true))mask.enabled=false;await Picture("unmasked",b);
            foreach(var mask in b.GetComponentsInChildren<UnityEngine.UI.RectMask2D>(true))mask.enabled=true;
            foreach(var text in b.GetComponentsInChildren<TMPro.TMP_Text>(true)){text.SetVerticesDirty();text.SetMaterialDirty();text.ForceMeshUpdate();}await Picture("rebuilt",b);
            selection.SetValue(view,prior);for(int i=0;i<2;i++)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1});
        }
        catch(Exception e){File.WriteAllText("../UnityWork/MatchHud20261020/probe-error.txt",e.ToString());Debug.LogException(e);}
        finally
        {var op=SceneManager.LoadSceneAsync("ModeSelection");while(!op.isDone)await Task.Delay(20);rt.SwitchPlayer(original,null);string root=Path.Combine(Application.persistentDataPath,"LocalPlayers");foreach(var suffix in new[]{".json",".json.bak",".lock"}){string file=Path.Combine(root,qa+suffix);if(File.Exists(file))File.Delete(file);}}
    }
}
