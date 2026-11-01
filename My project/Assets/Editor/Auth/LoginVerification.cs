using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PickleVerse.FrontEnd;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using TMPro;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PickleVerse.EditorTools
{
    /// <summary>Explicit local UI verification. HTTP fixtures never contact production or send email.</summary>
    public static class LoginVerification
    {
        static string Output => LoginSceneInstaller.Output;
        static readonly List<string> checks = new List<string>();
        static readonly BindingFlags Private = BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string name) { if(!ok) throw new Exception("FAILED: "+name); checks.Add(name); }
        static AccountPanelView View => Object.FindAnyObjectByType<AccountPanelView>();
        static AccountScreen Controller => Object.FindAnyObjectByType<AccountScreen>();
        static bool Busy => (bool)typeof(AccountScreen).GetField("busy",Private).GetValue(Controller);
        static async Task Wait(Func<bool> ready,int milliseconds=15000)
        {
            double until=EditorApplication.timeSinceStartup+milliseconds/1000d;
            while(!ready()) { if(EditorApplication.timeSinceStartup>until) throw new TimeoutException("UI wait timed out."); await Task.Delay(20); }
        }
        static async Task Submit()
        {
            View.submit.onClick.Invoke();
            await Wait(()=>!Busy && !SceneLoading.IsBlocking,120000);
        }
        public static object VerifyAssets()
        {
            checks.Clear();
            Check(EditorBuildSettings.scenes[0].path==LoginSceneInstaller.ScenePath&&EditorBuildSettings.scenes[0].enabled,"Login is enabled startup scene");
            var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(LoginSceneInstaller.ScenePath,OpenSceneMode.Additive);
            try
            {
                var v=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<AccountPanelView>(true)).Single();
                Check(v.email&&v.password&&v.confirmPassword&&v.otp&&v.submit&&v.switchPage&&v.verifyLink&&v.resend,"Login contains all existing auth actions");
                Check(v.password.contentType==TMP_InputField.ContentType.Password&&v.confirmPassword.contentType==TMP_InputField.ContentType.Password,"Passwords masked by default");
                Check(!v.playerName&&!v.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Male"||t.name=="Female"||t.name=="Character"),"Login/register have no gender selector");
                Check(v.card.anchorMin==new Vector2(.5f,.5f)&&v.card.anchorMax==v.card.anchorMin,"Auth card centered");
                Check(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RawImage>(true)).Single().texture,"Cinematic background assigned");
                Check(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Count()==0,"Login has no 3D renderers");
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
            var prefab=AssetDatabase.LoadAssetAtPath<MenuAccountView>("Assets/Resources/Auth/MenuAccount.prefab");
            Check(prefab&&prefab.open&&prefab.panel.playerName,"Menu profile actions retained");
            Check(!prefab.panel.email&&!prefab.panel.password&&!prefab.panel.confirmPassword&&!prefab.panel.otp,"Menu prefab has no login/register/OTP inputs");
            Check(prefab.GetComponentsInChildren<TMP_InputField>(true).Length==1,"Only profile name input remains in Menu account UI");
            File.WriteAllText(Path.Combine(Output,"asset-checks.json"),JsonConvert.SerializeObject(checks,Formatting.Indented));
            return new {passed=checks.Count,checks=checks.ToArray()};
        }
        public static object Render(string page="Login",int width=1920,int height=1080)
        {
            var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.OpenScene(LoginSceneInstaller.ScenePath,OpenSceneMode.Additive);
            RenderTexture rt=null; Texture2D image=null;
            try
            {
                SceneManager.SetActiveScene(scene);
                var roots=scene.GetRootGameObjects();
                var v=roots.SelectMany(g=>g.GetComponentsInChildren<AccountPanelView>(true)).Single();
                bool register=page=="Register",verify=page=="Verify";
                v.confirmPassword.transform.parent.gameObject.SetActive(register); v.password.transform.parent.gameObject.SetActive(!verify); v.otp.transform.parent.gameObject.SetActive(verify);
                v.verifyLink.gameObject.SetActive(!register&&!verify); v.resend.gameObject.SetActive(verify);
                v.title.text=register?"Khởi đầu hành trình":verify?"Xác thực email":"Chào mừng trở lại";
                v.subtitle.text=register?"Tạo tài khoản để lưu tiến trình và cùng bạn bè thi đấu.":verify?"Nhập mã 6 chữ số được gửi đến email của bạn. Mã có hiệu lực 10 phút.":"Đăng nhập để tiếp tục hành trình PickleVerse.";
                v.switchHint.text=register?"Đã có tài khoản?":"Chưa có tài khoản?"; v.switchLabel.text=register?"Đăng nhập":"Đăng ký";
                v.submitLabel.text=register?"TẠO TÀI KHOẢN":verify?"XÁC THỰC":"ĐĂNG NHẬP";
                var canvas=roots.SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First(c=>c.isRootCanvas);
                canvas.GetComponent<CanvasScaler>().enabled=false; canvas.scaleFactor=height/800f;
                var cam=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(); cam.cullingMask=1<<5; cam.orthographic=true; cam.orthographicSize=400; cam.transform.position=new Vector3(0,0,-10);
                cam.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                rt=new RenderTexture(width,height,24); cam.targetTexture=rt; canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cam; canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(v.card);
                v.card.sizeDelta=new Vector2(Mathf.Min(444,width/(height/800f)-32),LayoutUtility.GetPreferredHeight(v.card)); v.card.localScale=Vector3.one;
                LayoutRebuilder.ForceRebuildLayoutImmediate(v.card); Canvas.ForceUpdateCanvases(); cam.Render();
                var prior=RenderTexture.active; RenderTexture.active=rt;
                image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); RenderTexture.active=prior;
                var path=Path.Combine(Output,$"{page}-{width}x{height}.png"); File.WriteAllBytes(path,image.EncodeToPNG()); return new {path};
            }
            finally
            {
                if(image) Object.DestroyImmediate(image); if(rt) Object.DestroyImmediate(rt);
                SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true);
            }
        }
        [Serializable] sealed class Setup { public SceneSetup[] scenes; }
        public static object PreparePlay(string initialScenePath=LoginSceneInstaller.ScenePath)
        {
            if(EditorSceneManager.GetSceneManagerSetup().Any(s=>SceneManager.GetSceneByPath(s.path).isDirty)) throw new Exception("Save user scene changes first.");
            SessionState.SetString("PKV.Login.Setup",JsonUtility.ToJson(new Setup{scenes=EditorSceneManager.GetSceneManagerSetup()}));
            EditorSceneManager.OpenScene(initialScenePath,OpenSceneMode.Single);
            EditorApplication.isPlaying=true; return new {started=true};
        }
        public static object Restore()
        {
            if(Application.isPlaying) throw new Exception("Stop Play first.");
            var saved=SessionState.GetString("PKV.Login.Setup","");
            if(saved.Length>0) { EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Setup>(saved).scenes); SessionState.EraseString("PKV.Login.Setup"); }
            return new {restored=true};
        }
        public static object Begin()
        {
            if(!Application.isPlaying) throw new Exception("Enter Play first.");
            Run(); return new {started=true,report=Path.Combine(Output,"play-checks.json")};
        }
        static async void Run()
        {
            checks.Clear();
            var transport=AccountRuntime.Ensure().GetComponent<UnityApiTransport>();
            var settings=transport.Settings; var local=Object.Instantiate(settings); Fixture server=null;
            bool runInBackground=Application.runInBackground; int fps=Application.targetFrameRate, vsync=QualitySettings.vSyncCount;
            try
            {
                Application.runInBackground=true; Application.targetFrameRate=-1; QualitySettings.vSyncCount=0;
                Check(SceneManager.GetActiveScene().name=="Login"&&!AccountRuntime.Instance.Account.IsSignedIn,"Unauthenticated startup enters Login");
                server=new Fixture(); local.baseUrl=server.Url; transport.Settings=local;
                await Task.Delay(800);
                var visible=await Sample();
                var ui=View.GetComponentInParent<Canvas>().rootCanvas.gameObject; ui.SetActive(false); await Task.Delay(400);
                var hidden=await Sample(); ui.SetActive(true); await Task.Delay(200);
                File.WriteAllText(Path.Combine(Output,"performance.json"),JsonConvert.SerializeObject(new{environment="Unity Editor / desktop, no FPS cap, 240 frames each; includes Editor overhead",login=visible,uiHiddenBaseline=hidden},Formatting.Indented));
                Check(View.card.anchorMin==new Vector2(.5f,.5f),"Runtime login card stays centered");
                View.email.text="invalid"; await Submit();
                Check(View.feedback.text.Contains("email hợp lệ")&&server.Count("/api/Auth/login")==0,"Invalid email blocked before HTTP");
                View.email.text="ui-probe@example.invalid"; View.switchPage.onClick.Invoke();
                Check(View.confirmPassword.transform.parent.gameObject.activeSelf,"Register button switches form");
                View.password.text="123"; View.confirmPassword.text="123"; await Submit();
                Check(View.feedback.text.Contains("ít nhất 6")&&server.Count("/api/Auth/register")==0,"Short registration password rejected");
                View.password.text="UiProbe!234"; View.confirmPassword.text="different"; await Submit();
                Check(View.feedback.text.Contains("chưa khớp"),"Password confirmation validated");
                View.passwordVisibility.onClick.Invoke(); Check(View.password.contentType==TMP_InputField.ContentType.Standard,"Password reveal works");
                View.passwordVisibility.onClick.Invoke(); Check(View.password.contentType==TMP_InputField.ContentType.Password,"Password can be concealed again");
                View.confirmPassword.text=View.password.text; View.submit.onClick.Invoke(); View.submit.onClick.Invoke(); await Wait(()=>!Busy);
                Check(server.Count("/api/Auth/register")==1&&!AccountRuntime.Instance.Account.IsSignedIn,"Double submit sends one registration and does not sign in");
                Check(View.otp.transform.parent.gameObject.activeSelf&&View.password.text==""&&View.confirmPassword.text=="","Registration transitions to OTP and clears passwords");
                View.otp.text="000000"; await Submit(); Check(View.feedback.text.Contains("không đúng"),"Wrong OTP remains recoverable");
                await Wait(()=>View.resend.interactable); View.resend.onClick.Invoke(); await Wait(()=>!Busy);
                Check(server.Count("/api/Auth/resend-otp")==1,"OTP resend uses existing API");
                View.otp.text="123456"; await Submit(); Check(View.password.transform.parent.gameObject.activeSelf&&!View.otp.transform.parent.gameObject.activeSelf,"Verified email returns to Login");
                View.password.text="wrong"; await Submit(); Check(!AccountRuntime.Instance.Account.IsSignedIn&&View.feedback.text.Contains("không đúng"),"Wrong credentials display server error");
                server.FailState=true; View.password.text="UiProbe!234"; await Submit();
                Check(AccountRuntime.Instance.Account.IsSignedIn&&SceneManager.GetActiveScene().name=="Login"&&View.email&&View.feedback.text.Contains("thử lại"),"Game-state failure returns to Login with retry");
                server.FailState=false; await Submit(); await Wait(()=>SceneManager.GetActiveScene().name=="Menu");
                Check(ProgressionRuntime.Instance.Ready,"Successful login loads game state before Menu");
                var menu=Object.FindAnyObjectByType<MenuAccountView>();
                Check(menu&&!menu.panel.email&&!menu.panel.password&&!menu.panel.otp,"Menu has no authentication forms after login");
                var home=Object.FindAnyObjectByType<HomeLobbyPresentation>();
                Check(home&&home.maleButton.interactable&&home.femaleButton.interactable,"Existing Menu gender buttons remain enabled");
                home.SelectFemale(); await Wait(()=>!home.CharacterChangePending); Check(!AccountRuntime.Instance.Account.Profile.IsMale&&!home.IsMaleSelected,"Menu female selection persists through profile API");
                home.SelectMale(); await Wait(()=>!home.CharacterChangePending); Check(AccountRuntime.Instance.Account.Profile.IsMale&&home.IsMaleSelected,"Menu male selection persists through profile API");
                menu.open.onClick.Invoke(); Check(menu.panel.gameObject.activeSelf&&menu.panel.playerName,"Menu profile opens");
                menu.panel.playerName.text="UiProbeRenamed"; await Submit();
                Check(AccountRuntime.Instance.Account.Profile.PlayerName=="UiProbeRenamed"&&AccountRuntime.Instance.Account.Profile.IsMale,"Profile rename preserves character selection");
                menu.panel.logout.onClick.Invoke(); await Wait(()=>SceneManager.GetActiveScene().name=="Login"&&!Busy);
                Check(!AccountRuntime.Instance.Account.IsSignedIn&&View.email&&View.password,"Logout clears session and returns to Login");
                local.baseUrl="http://127.0.0.1:1"; View.email.text="ui-probe@example.invalid"; View.password.text="UiProbe!234"; await Submit();
                Check(View.feedback.text.Contains("kết nối")&&!Busy&&View.form.interactable,"Connection failure restores usable Login form");
                File.WriteAllText(Path.Combine(Output,"play-checks.json"),JsonConvert.SerializeObject(new{success=true,passed=checks.Count,checks},Formatting.Indented));
            }
            catch(Exception error) { File.WriteAllText(Path.Combine(Output,"play-checks.json"),JsonConvert.SerializeObject(new{success=false,passed=checks.Count,checks,error=error.ToString()},Formatting.Indented)); }
            finally
            {
                server?.Dispose(); if(transport) transport.Settings=settings; Object.Destroy(local);
                Application.runInBackground=runInBackground; Application.targetFrameRate=fps; QualitySettings.vSyncCount=vsync;
            }
        }
        static async Task<object> Sample()
        {
            const int count=240; var elapsed=new double[count]; var main=new long[count]; var allocations=new long[count];
            var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread"); var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            int index=0,lastFrame=-1; var completion=new TaskCompletionSource<bool>();
            void Tick()
            {
                if(Time.frameCount==lastFrame) return; lastFrame=Time.frameCount;
                elapsed[index]=Time.unscaledDeltaTime*1000d; main[index]=cpu.LastValue; allocations[index]=gc.LastValue;
                if(++index>=count) { EditorApplication.update-=Tick; completion.TrySetResult(true); }
            }
            EditorApplication.update+=Tick; await completion.Task;
            bool cpuValid=cpu.Valid,gcValid=gc.Valid; cpu.Dispose(); gc.Dispose();
            Array.Sort(elapsed); Array.Sort(main); Array.Sort(allocations);
            return new {frames=count,frameMsP50=elapsed[120],frameMsP95=elapsed[228],mainThreadMsP50=main[120]/1000000d,mainThreadMsP95=main[228]/1000000d,gcBytesP50=allocations[120],gcBytesP95=allocations[228],cpuValid,gcValid};
        }
        internal sealed class Fixture : IDisposable
        {
            readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
            readonly System.Collections.Concurrent.ConcurrentDictionary<string,int> counts=new System.Collections.Concurrent.ConcurrentDictionary<string,int>();
            readonly Guid id=Guid.Parse("4d846dd5-1515-4dd0-b82a-d7cad2ddab56");
            readonly string lessons=JsonConvert.SerializeObject(TrainingCurriculum.Lessons);
            volatile bool running=true;
            bool male=true; volatile bool verified; string name="UiProbe";
            public volatile bool FailState;
            public volatile int LoginDelayMilliseconds, StateDelayMilliseconds;
            public bool Verified { get => verified; set => verified=value; }
            public string Url { get; }
            public Fixture() { listener.Start(); Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port; _=Task.Run(Listen); }
            public int Count(string path)=>counts.TryGetValue(path,out int value)?value:0;
            async Task Listen()
            {
                while(running) { try { var client=await listener.AcceptTcpClientAsync(); _=Task.Run(()=>Serve(client)); } catch { if(running) throw; } }
            }
            object Profile()=>new{userId=id,playerName=name,level=3,elo=250,isMale=male};
            async Task Serve(TcpClient client)
            {
                using(client)
                {
                    var stream=client.GetStream(); var header=new List<byte>(); var one=new byte[1];
                    while(header.Count<65536)
                    {
                        if(await stream.ReadAsync(one,0,1)==0)return; header.Add(one[0]); int n=header.Count;
                        if(n>=4&&header[n-4]==13&&header[n-3]==10&&header[n-2]==13&&header[n-1]==10) break;
                    }
                    string text=Encoding.ASCII.GetString(header.ToArray()); var lines=text.Split(new[]{"\r\n"},StringSplitOptions.None); var first=lines[0].Split(' '); string method=first[0],path=first[1].Split('?')[0];
                    var lengthLine=lines.FirstOrDefault(l=>l.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)); int length=lengthLine==null?0:int.Parse(lengthLine.Split(':')[1].Trim());
                    var body=new byte[length]; int read=0; while(read<length) { int n=await stream.ReadAsync(body,read,length-read); if(n==0)return; read+=n; }
                    var json=length==0?new JObject():JObject.Parse(Encoding.UTF8.GetString(body)); counts.AddOrUpdate(path,1,(_,n)=>n+1);
                    int status=200; object result=new{message="OK"};
                    if(path=="/api/Auth/register") { await Task.Delay(180); result=new{success=true,message="Mã đã gửi",otpLifetimeMinutes=2,otpResendCooldownSeconds=1}; }
                    else if(path=="/api/Auth/verify-email") { verified=(string)json["token"]=="123456"; if(!verified){ status=400; result=new{message="Mã xác thực không đúng.",code="invalid_otp"}; } else result=new{message="Đã xác thực email. Bạn có thể đăng nhập."}; }
                    else if(path=="/api/Auth/resend-otp") result=new{code="verification_requested",message="Đã gửi lại mã.",otpLifetimeMinutes=2,otpResendCooldownSeconds=1};
                    else if(path=="/api/Auth/login")
                    {
                        if(LoginDelayMilliseconds>0) await Task.Delay(LoginDelayMilliseconds);
                        if((string)json["password"]!="UiProbe!234") {status=401;result=new{message="Email hoặc mật khẩu không đúng.",code="invalid_credentials"};}
                        else if(!verified) {status=400;result=new{message="Email chưa xác thực.",code="email_not_verified"};}
                        else result=new{success=true,accessToken="local-ui-probe",refreshToken="local-ui-refresh",profile=Profile()};
                    }
                    else if(path=="/api/Profiles/me")
                    {
                        if(method=="PUT") { if(json["isMale"]!=null)male=(bool)json["isMale"]; if(json["playerName"]!=null)name=(string)json["playerName"]; } else result=Profile();
                    }
                    else if(path=="/api/Game/state")
                    {
                        if(StateDelayMilliseconds>0) await Task.Delay(StateDelayMilliseconds);
                        if(FailState) {status=503;result=new{message="Máy chủ thử nghiệm đang bận. Hãy thử lại.",code="probe_busy"};}
                        else result=new{revision=1,isMale=male,player=new PlayerProfile{id=id.ToString("N"),playerName=name,level=3,elo=250},inventory=new{owned=new string[0],equippedPaddle="paddle.default"},tutorial=new{completed=true,skipped=false},catalog=new{definitions=new object[0],lessons=JArray.Parse(lessons),cosmetics=new object[0],matchmaking=new{botFallbackSeconds=15,readyConfirmationSeconds=10},onlineMatch=new{heartbeatSeconds=5,disconnectTimeoutSeconds=30}}};
                    }
                    var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result)); var response=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response,0,response.Length); await stream.WriteAsync(bytes,0,bytes.Length);
                }
            }
            public void Dispose() { running=false; listener.Stop(); }
        }
    }
}
