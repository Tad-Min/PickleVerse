using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using PickleVerse.Shop;

public static class LobbyEquipmentValidation
{
    const string Root="../UnityWork/LobbyPaddlePose20261016/";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool pass){checks.Add(new{name,pass});if(!pass)throw new Exception(name);}
    static void Save(string name,object data)=>File.WriteAllText(Root+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
    // Actual nontransparent content, measured from the new native Blender tile.
    static Rect PaddleContent(HomeMenuShell s)
    {
        var r=s.paddle.rect;
        Vector3 lo=s.play.InverseTransformPoint(s.paddle.TransformPoint(new Vector3(r.xMin+r.width*57/256f,r.yMin+r.height*31/256f,0)));
        Vector3 hi=s.play.InverseTransformPoint(s.paddle.TransformPoint(new Vector3(r.xMin+r.width*175/256f,r.yMin+r.height*229/256f,0)));
        return Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);
    }
    static async Task Capture(string name)
    {ScreenCapture.CaptureScreenshot(Path.GetFullPath(Root+name+".png"));await Task.Delay(180);}
    public static async Task<object> Layout()
    {
        checks.Clear();var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();
        if(!Application.isPlaying||!shell)throw new Exception("Play Menu first.");
        if(SettingsOverlay.IsOpen)throw new Exception("Settings is covering Menu during QA.");
        var a=typeof(Editor).Assembly;var type=a.GetType("UnityEditor.GameView");
        var view=Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First();
        var st=a.GetType("UnityEditor.GameViewSizes");var sizes=st.GetProperty("instance",Flags).GetValue(null);
        var group=st.GetMethod("GetGroup",Flags).Invoke(sizes,new[]{type.GetProperty("currentSizeGroupType",Flags).GetValue(view)});var gt=group.GetType();
        var selection=type.GetProperty("selectedSizeIndex",Flags);int prior=(int)selection.GetValue(view),count=(int)gt.GetMethod("GetCustomCount").Invoke(group,null);
        Touchscreen touch=null;
        try
        {
            var cases=new[]{new[]{1280,720,0,0},new[]{1920,1080,0,0},new[]{2560,1080,0,0},new[]{1024,768,0,0},new[]{1334,750,1,326},new[]{2400,1080,1,400},new[]{2532,1170,1,460},new[]{2048,1536,1,264},new[]{1080,1920,1,420}};
            foreach(var size in cases)
            {
                if(size[2]==1&&touch==null)touch=InputSystem.AddDevice<Touchscreen>("Equipment QA touch");
                if(touch!=null)touch.MakeCurrent();shell.editorPreviewDpi=size[3];
                var entry=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size[0],size[1],"Equipment QA");
                gt.GetMethod("AddCustomSize").Invoke(group,new[]{entry});selection.SetValue(view,(int)gt.GetMethod("GetTotalCount").Invoke(group,null)-1);view.Focus();view.Repaint();
                var until=DateTime.UtcNow.AddSeconds(8);while(Screen.width!=size[0]||Screen.height!=size[1]){if(DateTime.UtcNow>until)throw new TimeoutException("GameView resize");await Task.Delay(50);}
                await Task.Delay(200);Canvas.ForceUpdateCanvases();string tag=size[0]+"x"+size[1];
                Check("Orientation refreshes navigation "+tag,shell.navigation.constraintCount==(size[1]>size[0]?3:6));
                Check("Menu visible during capture "+tag,!SettingsOverlay.IsOpen);
                var content=PaddleContent(shell);var button=shell.play.rect;
                Check("Visible paddle extends above button "+tag,content.yMax>button.yMax+10);
                Check("Grip sits inside lower card "+tag,content.yMin>button.yMin+8&&content.yMin<button.yMin+button.height*.3f);
                Vector3 idleBall=shell.play.InverseTransformPoint(shell.ball.TransformPoint(shell.ball.rect.center));
                Check("Ball floats above paddle head "+tag,idleBall.y>content.yMax+8);
                Check("Home button removed "+tag,!shell.nav.Find("BtnHome")&&shell.nav.GetComponentsInChildren<UnityEngine.UI.Button>().Length==6);
                Check("Nav right aligned with gutter "+tag,shell.nav.anchorMin.x==1&&Mathf.Abs(shell.nav.anchoredPosition.x+(size[1]>size[0]?28:24))<.1f);
                Vector3 navCenter=shell.safe.InverseTransformPoint(shell.nav.TransformPoint(shell.nav.rect.center));
                Vector3 playCenter=shell.safe.InverseTransformPoint(shell.play.TransformPoint(shell.play.rect.center));
                Check("Play and nav share row center "+tag,Mathf.Abs(navCenter.y-playCenter.y)<.5f);
                Vector3 navLeft=shell.safe.InverseTransformPoint(shell.nav.TransformPoint(new Vector3(shell.nav.rect.xMin,0)));
                Vector3 playRight=shell.safe.InverseTransformPoint(shell.play.TransformPoint(new Vector3(shell.play.rect.xMax,0)));
                Check("Play is left of nav with spacing "+tag,navLeft.x-playRight.x>=17.9f);
                shell.playTitle.ForceMeshUpdate();var ink=shell.playTitle.textBounds;
                Vector3 inkLo=shell.play.InverseTransformPoint(shell.playTitle.rectTransform.TransformPoint(ink.min)),inkHi=shell.play.InverseTransformPoint(shell.playTitle.rectTransform.TransformPoint(ink.max));
                var title=Rect.MinMaxRect(inkLo.x,inkLo.y,inkHi.x,inkHi.y);
                Check("Paddle leaves title clear "+tag,!content.Overlaps(title));
                shell.playTitle.ForceMeshUpdate();Check("Play title readable "+tag,!shell.playTitle.isTextOverflowing);
                Check("Nav labels readable "+tag,shell.nav.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
                var world=shell.play.TransformPoint(new Vector3(content.xMin,content.yMin));var local=shell.safe.InverseTransformPoint(world);
                Check("Paddle content stays on screen "+tag,local.x>=shell.safe.rect.xMin&&local.y>=shell.safe.rect.yMin);
                Check("Decoration cannot intercept clicks "+tag,!shell.paddle.GetComponent<UnityEngine.UI.Image>().raycastTarget&&!shell.ball.GetComponent<UnityEngine.UI.Image>().raycastTarget);
                Check("No clipping ancestor "+tag,shell.paddle.GetComponentsInParent<UnityEngine.UI.RectMask2D>(true).Length==0&&shell.paddle.GetComponentsInParent<UnityEngine.UI.Mask>(true).Length==0);
                if(size[0]==1280||size[0]==2400||size[0]==1080)await Capture("menu-"+tag);
            }
            var texture=shell.theme.paddle.texture;
            Check("One shared unchanged-size atlas",texture==shell.theme.ball.texture&&texture==shell.theme.play.texture&&texture.width==1024&&texture.height==1024);
            Check("No live 3D render texture",shell.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Length==0);
            return new{passed=true,count=checks.Count,resolutions=cases.Length,textureBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture)};
        }
        finally
        {
            selection.SetValue(view,prior);
            int builtins=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null);
            for(int i=(int)gt.GetMethod("GetCustomCount").Invoke(group,null)-1;i>=count;i--)gt.GetMethod("RemoveCustomSize").Invoke(group,new object[]{builtins+i});
            shell.editorPreviewDpi=0;if(touch!=null)InputSystem.RemoveDevice(touch);view.Repaint();Save("layout",checks);
        }
    }
    public static async Task<object> Strike()
    {
        checks.Clear();var shell=UnityEngine.Object.FindAnyObjectByType<HomeMenuShell>();
        if(!Application.isPlaying||!shell)throw new Exception("Play Menu first.");
        // GameView restore and CanvasScaler changes settle on separate frames.
        // Leave the live layout enabled until that has finished before posing it.
        await Task.Delay(400);Canvas.ForceUpdateCanvases();
        try
        {
            shell.enabled=false;Canvas.ForceUpdateCanvases();
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,shell.playTitle.rectTransform.TransformPoint(shell.playTitle.rectTransform.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check("Play receives pointer",hits.Count>0&&(hits[0].gameObject==shell.play.gameObject||hits[0].gameObject.transform.IsChildOf(shell.play)));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
            Check("Play click begins strike",shell.IsPlayingAnimation);
            float idleY=shell.ball.anchoredPosition.y;
            shell.AdvancePlay(.10f);Check("Ball approaches paddle from above",shell.ball.anchoredPosition.y<idleY-10);
            shell.AdvancePlay(.05f);Canvas.ForceUpdateCanvases();await Capture("strike-contact");
            Check("Ball and flash share contact point",Vector3.Distance(shell.ball.TransformPoint(shell.ball.rect.center),shell.flash.rectTransform.TransformPoint(shell.flash.rectTransform.rect.center))<1);
            Check("Paddle swings at contact",Quaternion.Angle(shell.paddle.localRotation,Quaternion.identity)>40);
            shell.AdvancePlay(.04f);Canvas.ForceUpdateCanvases();await Capture("strike-vfx");
            Check("Contact VFX visible",shell.flash.color.a>.2f);
            shell.AdvancePlay(.22f);shell.enabled=true;
            var until=DateTime.UtcNow.AddSeconds(15);
            while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="ModeSelection"||LobbyNavigation.IsBusy){if(DateTime.UtcNow>until)throw new TimeoutException("ModeSelection");await Task.Delay(50);}
            Check("Strike opens mode selection",true);
            LobbyNavigation.ReturnToLobby();until=DateTime.UtcNow.AddSeconds(15);
            while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Menu"||LobbyNavigation.IsBusy){if(DateTime.UtcNow>until)throw new TimeoutException("Menu");await Task.Delay(50);}
            await Task.Delay(200);
            Check("Lobby return resets strike",shell&&!shell.IsPlayingAnimation&&shell.playButton.IsInteractable()&&Quaternion.Angle(shell.paddle.localRotation,Quaternion.identity)<.1f);
            await Capture("menu-final");return new{passed=true,count=checks.Count};
        }
        finally{if(shell)shell.enabled=true;Save("strike",checks);}
    }
}
