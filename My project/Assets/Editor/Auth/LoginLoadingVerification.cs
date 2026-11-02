using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    /// <summary>UI -> real Unity HTTP transport -> loopback API -> Loading -> rendered Menu.</summary>
    public static class LoginLoadingVerification
    {
        public static string Output=>SessionState.GetString("PKV.Loading.Output",Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/login-loading-20261101")));
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly List<string> checks=new List<string>();
        static readonly List<Trace> traces=new List<Trace>();
        static Trace trace;
        static int renderFrame=-1;
        static double lastRender;
        static bool renderedAuthPending,earlyReveal,sawWaitingBox;
        static int renderedWhileCovered,afterReveal;
        static readonly List<double> revealFrames=new List<double>(600);
        static AccountPanelView View=>Object.FindAnyObjectByType<AccountPanelView>();
        static AccountScreen Controller=>Object.FindAnyObjectByType<AccountScreen>();
        static bool Busy=>(bool)typeof(AccountScreen).GetField("busy",Private).GetValue(Controller);
        [Serializable] sealed class Trace
        {
            public string name;
            public List<string> activeScenes=new List<string>();
            public List<float> progress=new List<float>();
            public bool hit100,loadingBeforeRequest,renderedBeforeAuthentication;
            public int loginRequests;
        }
        static void Check(bool value,string label) { if(!value)throw new Exception("FAILED: "+label); checks.Add(label); }
        static async Task Wait(Func<bool> ready,int timeoutMs=120000)
        {
            double until=EditorApplication.timeSinceStartup+timeoutMs/1000d;
            while(!ready()) { if(EditorApplication.timeSinceStartup>until)throw new TimeoutException("E2E wait timed out: "+trace?.name); await Task.Delay(15); }
        }
        public static object Prepare(string outputDirectory=null)
        {
            if(!string.IsNullOrEmpty(outputDirectory))SessionState.SetString("PKV.Loading.Output",Path.GetFullPath(outputDirectory));
            Directory.CreateDirectory(Output);
            if(Application.isBatchMode)throw new InvalidOperationException("This verification requires a rendered Game View, not a headless Editor.");
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var window=EditorWindow.GetWindow(type); window.Show(); window.Focus();
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var selection=type.GetProperty("selectedSizeIndex",flags);
            SessionState.SetInt("PKV.Loading.GameViewSize",(int)selection.GetValue(window));
            var assembly=typeof(EditorWindow).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
            var groupClass=group.GetType();
            int total=(int)groupClass.GetMethod("GetBuiltinCount",flags).Invoke(group,null)+(int)groupClass.GetMethod("GetCustomCount",flags).Invoke(group,null),index=-1;
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            for(int i=0;i<total;i++)
            {
                var size=groupClass.GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
                if((int)sizeType.GetProperty("width",flags).GetValue(size)==1920&&(int)sizeType.GetProperty("height",flags).GetValue(size)==1080){index=i;break;}
            }
            if(index<0)
            {
                var kind=assembly.GetType("UnityEditor.GameViewSizeType");
                var size=Activator.CreateInstance(sizeType,flags,null,new[]{Enum.Parse(kind,"FixedResolution"),(object)1920,1080,"PickleVerse E2E 1080p"},null);
                groupClass.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size}); index=total;
            }
            selection.SetValue(window,index);
            return LoginVerification.PreparePlay();
        }
        public static object Restore()
        {
            var result=LoginVerification.Restore();
            int index=SessionState.GetInt("PKV.Loading.GameViewSize",-1);
            if(index>=0)
            {
                var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                var window=EditorWindow.GetWindow(type);
                type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(window,index);
                SessionState.EraseInt("PKV.Loading.GameViewSize");
            }
            SessionState.EraseString("PKV.Loading.Output");
            return result;
        }
        public static object Begin()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
            Run(); return new{started=true,report=Path.Combine(Output,"e2e.json")};
        }
        static void ActiveChanged(Scene previous,Scene next)
        {
            if(trace!=null&&(trace.activeScenes.Count==0||trace.activeScenes.Last()!=next.name))trace.activeScenes.Add(next.name);
        }
        static void Observe()
        {
            if(trace==null)return;
            if(SceneLoading.IsBlocking)
            {
                float value=SceneLoading.Progress;
                if(trace.progress.Count==0||Math.Abs(trace.progress.Last()-value)>.0001f)trace.progress.Add(value);
                if(SceneLoading.View&&SceneLoading.View.DisplayedProgress>=.9999f)
                {
                    trace.hit100=true;
                    earlyReveal|=!ProgressionRuntime.Instance.Ready||!SceneLoading.LocalRenderReady;
                }
            }
            var v=View;
            if(v&&!v.profileOnly)
                sawWaitingBox|=v.title.text.Contains("Sẵn sàng vào sân")||v.title.text.Contains("Chờ vào sân");
        }
        static void Rendered(ScriptableRenderContext context,Camera camera)
        {
            if(!camera||camera.cameraType!=CameraType.Game||camera.gameObject.scene.name!="Menu"||renderFrame==Time.frameCount)return;
            renderFrame=Time.frameCount;
            double now=Time.realtimeSinceStartupAsDouble;
            if(SceneLoading.IsBlocking)
            {
                renderedWhileCovered++;
                if(!AccountRuntime.Instance.Account.IsSignedIn)renderedAuthPending=true;
            }
            else if(trace?.name=="first-success")
            {
                if(afterReveal++>0&&revealFrames.Count<600)revealFrames.Add((now-lastRender)*1000);
            }
            lastRender=now;
        }
        static void StartTrace(string name)
        {
            trace=new Trace{name=name}; trace.activeScenes.Add(SceneManager.GetActiveScene().name); traces.Add(trace);
        }
        static void Login(string password="UiProbe!234")
        {
            View.email.text="ui-probe@example.invalid"; View.password.text=password;
            View.submit.onClick.Invoke();
        }
        static async Task SettledLogin()
        {
            await Wait(()=>SceneManager.GetActiveScene().name=="Login"&&!SceneLoading.IsBlocking&&!Busy&&View);
            Check(!SceneManager.GetSceneByName("Menu").isLoaded&&!SceneManager.GetSceneByName("Loading").isLoaded,"Failure drains additive scenes: "+trace.name);
            Check(View.form.interactable&&EventSystem.current&&EventSystem.current.enabled,"Login input restored: "+trace.name);
            Check(!FrontEndModalInput.IsOpen,"No modal input lock leaked: "+trace.name);
        }
        static async Task Logout()
        {
            var menu=Object.FindAnyObjectByType<MenuAccountView>(); menu.open.onClick.Invoke();
            Check(menu.panel.gameObject.activeSelf,"Menu account button bound after additive activation");
            menu.panel.logout.onClick.Invoke();
            await Wait(()=>SceneManager.GetActiveScene().name=="Login"&&!Busy&&!SceneLoading.IsBlocking&&View);
        }
        static object Stats(IEnumerable<double> source)
        {
            var values=source.OrderBy(v=>v).ToArray();
            return new{frames=values.Length,p50Ms=values.Length>0?values[values.Length/2]:0,p95Ms=values.Length>0?values[(int)(values.Length*.95)]:0,p99Ms=values.Length>0?values[(int)(values.Length*.99)]:0,maxMs=values.Length>0?values.Last():0,over20ms=values.Count(v=>v>20),over33ms=values.Count(v=>v>33.34)};
        }
        static async void Run()
        {
            checks.Clear(); traces.Clear(); revealFrames.Clear(); trace=null;
            renderedAuthPending=earlyReveal=sawWaitingBox=false; renderedWhileCovered=afterReveal=0; renderFrame=-1; lastRender=0;
            var transport=AccountRuntime.Ensure().GetComponent<UnityApiTransport>(); var original=transport.Settings; var local=Object.Instantiate(original);
            var fixture=new LoginVerification.Fixture{Verified=true,LoginDelayMilliseconds=5000,StateDelayMilliseconds=1000};
            bool background=Application.runInBackground; int fps=Application.targetFrameRate,vsync=QualitySettings.vSyncCount;
            Application.runInBackground=true; Application.targetFrameRate=-1; QualitySettings.vSyncCount=0; transport.Settings=local; local.baseUrl=fixture.Url;
            SceneManager.activeSceneChanged+=ActiveChanged; EditorApplication.update+=Observe; RenderPipelineManager.endCameraRendering+=Rendered;
            try
            {
                Check(!Application.isBatchMode,"Rendering-enabled Editor");
                Check(SceneManager.GetActiveScene().name=="Login"&&!AccountRuntime.Instance.Account.IsSignedIn,"Starts on Login without a session");
                StartTrace("validation"); View.email.text="invalid"; View.submit.onClick.Invoke(); await Wait(()=>!Busy);
                Check(!SceneLoading.IsBlocking&&fixture.Count("/api/Auth/login")==0,"Invalid input stays on Login before HTTP/loading");

                StartTrace("first-success"); int before=fixture.Count("/api/Auth/login"); Login(); View.submit.onClick.Invoke();
                await Wait(()=>SceneManager.GetActiveScene().name=="Loading"&&SceneLoading.View);
                Check(SceneLoading.View.DisplayedProgress<1&&SceneLoading.View.group.alpha==1,"Loading becomes visible before completion");
                await Wait(()=>fixture.Count("/api/Auth/login")>before);
                trace.loadingBeforeRequest=SceneLoading.IsBlocking&&SceneLoading.View&&SceneLoading.View.group.alpha==1;
                Check(trace.loadingBeforeRequest,"Authentication HTTP runs under Loading");
                await Wait(()=>renderedAuthPending);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"loading-parallel.png"));
                Check(SceneManager.GetSceneByName("Menu").isLoaded&&!AccountRuntime.Instance.Account.IsSignedIn,"Menu loads and renders concurrently with delayed authentication");
                await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&!SceneLoading.IsBlocking);
                trace.renderedBeforeAuthentication=renderedAuthPending; trace.loginRequests=fixture.Count("/api/Auth/login")-before;
                Check(trace.activeScenes.SequenceEqual(new[]{"Login","Loading","Menu"}),"Exact success path Login -> Loading -> Menu");
                Check(trace.loginRequests==1,"Double click submits a single login request");
                Check(trace.hit100&&!earlyReveal&&SceneLoading.LocalRenderReady,"100 percent only after game state and rendered frames are ready");
                Check(ProgressionRuntime.Instance.Ready&&renderedWhileCovered>=36,"Destination rendered behind Loading before reveal");
                Check(EventSystem.current&&EventSystem.current.enabled&&!FrontEndModalInput.IsOpen,"Menu input restored after reveal");
                Check(!SceneManager.GetSceneByName("Login").isLoaded&&!SceneManager.GetSceneByName("Loading").isLoaded,"Source and staging scenes unloaded on success");
                await Wait(()=>revealFrames.Count>=600);
                Check(revealFrames.OrderBy(v=>v).ElementAt(570)<16.67,"Post-reveal p95 meets a 60 FPS frame budget at 1080p");
                Check(revealFrames.Take(60).Max()<20,"First 60 rendered frames have no spike above 20 ms");
                ScreenCapture.CaptureScreenshot(Path.Combine(Output,"menu-ready.png"));
                await Task.Delay(300);
                Check(!sawWaitingBox,"Login never creates the removed waiting/sync box");
                var home=Object.FindAnyObjectByType<HomeLobbyPresentation>();
                Check(home.maleButton.interactable&&home.femaleButton.interactable,"Menu character controls remain available");
                home.SelectFemale(); await Wait(()=>!home.CharacterChangePending); Check(!AccountRuntime.Instance.Account.Profile.IsMale,"Menu character API still works");
                StartTrace("logout-after-success"); await Logout(); Check(!AccountRuntime.Instance.Account.IsSignedIn,"Logout returns to Login");

                fixture.LoginDelayMilliseconds=fixture.StateDelayMilliseconds=0;
                StartTrace("wrong-password"); Login("wrong"); await SettledLogin();
                Check(View.feedback.text.Contains("không đúng")&&View.email.text=="ui-probe@example.invalid","Wrong credentials preserve email and show server error");
                Check(!trace.hit100,"Failed authentication never reaches 100 percent");

                fixture.Verified=false; StartTrace("unverified"); Login(); await SettledLogin();
                Check(View.otp.transform.parent.gameObject.activeSelf,"Unverified email returns to OTP form");
                View.otp.text="123456"; View.submit.onClick.Invoke(); await Wait(()=>!Busy);
                Check(View.password.transform.parent.gameObject.activeSelf,"OTP verification still returns to Login");

                StartTrace("connection-failure"); local.baseUrl="http://127.0.0.1:1"; Login(); await SettledLogin();
                Check(View.feedback.text.Contains("kết nối"),"Connection failure is recoverable on Login"); local.baseUrl=fixture.Url;

                fixture.FailState=true; StartTrace("game-state-failure"); Login(); await SettledLogin();
                Check(AccountRuntime.Instance.Account.IsSignedIn&&!ProgressionRuntime.Instance.Ready,"Partial authentication remains available for retry");
                Check(View.password.transform.parent.gameObject.activeSelf&&View.feedback.text.Contains("thử lại"),"Game-state error shows original Login form, no sync box");

                fixture.FailState=false; fixture.StateDelayMilliseconds=700; StartTrace("retry-session"); before=fixture.Count("/api/Auth/login");
                Check(View.password.text=="","Password was cleared after the original attempt"); View.submit.onClick.Invoke();
                await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&!SceneLoading.IsBlocking);
                Check(fixture.Count("/api/Auth/login")==before,"Retry reuses verified session without resending credentials");
                Check(trace.activeScenes.SequenceEqual(new[]{"Login","Loading","Menu"})&&trace.hit100,"Retry passes through Loading and reaches 100 percent");
                StartTrace("logout-after-retry"); await Logout();

                fixture.StateDelayMilliseconds=50; StartTrace("expired-during-warmup"); Login();
                await Wait(()=>SceneManager.GetActiveScene().name=="Menu"&&SceneLoading.IsPreparedTransition&&!SceneLoading.LocalRenderReady);
                AccountRuntime.Instance.Account.Clear(); await SettledLogin();
                Check(!AccountRuntime.Instance.Account.IsSignedIn&&!trace.hit100,"Session lost during warm-up cannot reveal Menu");
                Check(traces.All(t=>t.progress.Zip(t.progress.Skip(1),(a,b)=>b>=a).All(v=>v)),"All progress sequences are monotonic");
                Check(!sawWaitingBox&&!earlyReveal,"No sync dialog or premature reveal in any scenario");
                File.WriteAllText(Path.Combine(Output,"e2e.json"),JsonConvert.SerializeObject(new{success=true,passed=checks.Count,checks,traces,environment=new{batch=Application.isBatchMode,gpu=SystemInfo.graphicsDeviceName,api="loopback HTTP fixture; real Unity transport/services; no database/email",resolution=Screen.width+"x"+Screen.height},postReveal=Stats(revealFrames),first60Frames=Stats(revealFrames.Take(60)),frameSamplesMs=revealFrames},Formatting.Indented));
            }
            catch(Exception error)
            {
                File.WriteAllText(Path.Combine(Output,"e2e.json"),JsonConvert.SerializeObject(new{success=false,passed=checks.Count,checks,traces,error=error.ToString(),postReveal=Stats(revealFrames)},Formatting.Indented));
            }
            finally
            {
                trace=null; SceneManager.activeSceneChanged-=ActiveChanged; EditorApplication.update-=Observe; RenderPipelineManager.endCameraRendering-=Rendered;
                // Only restore transport after no local scene operation is left in flight.
                if(!SceneLoading.IsBlocking){fixture.Dispose();if(transport)transport.Settings=original;Object.Destroy(local);}
                Application.runInBackground=background; Application.targetFrameRate=fps; QualitySettings.vSyncCount=vsync;
            }
        }
    }
}
