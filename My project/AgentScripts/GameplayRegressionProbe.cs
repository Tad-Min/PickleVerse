using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Multiplayer;
using System.Threading.Tasks;
using System.Collections.Generic;
using Fusion;
using Newtonsoft.Json.Linq;

public static class GameplayRegressionProbe
{
    public static string RestoreBuildSettings()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (PlayerSettings.bundleVersion != "0.1.0-gameplay.20261105") throw new InvalidOperationException("Build version changed outside the probe.");
        PlayerSettings.bundleVersion = "0.1.0";
        // App UI's build processor added this preload; the baseline has only the input actions.
        PlayerSettings.SetPreloadedAssets(Array.FindAll(PlayerSettings.GetPreloadedAssets(), item => !item ||
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item)) != "99f9c9493070a9d4c979a8fec7c5a8d3"));
        AssetDatabase.SaveAssets();
        return "Restored baseline version and preloaded assets.";
    }
    public static async Task<string> Presentation()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
        var oldCanvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.enabled).ToArray();
        var oldCameras=Camera.allCameras.Where(c=>c.enabled).ToArray();
        foreach(var c in oldCanvases)c.enabled=false;
        foreach(var c in oldCameras)c.enabled=false;
        var root=new GameObject("Gameplay presentation verification");
        var output=System.IO.Path.GetFullPath("../artifacts/gameplay-fixes-20261105");
        System.IO.Directory.CreateDirectory(output);
        Material groundMaterial=null;
        try
        {
            int firstFrame=Time.frameCount;
            var settings=Resources.Load<MultiplayerSettings>("MultiplayerSettings");
            var human=UnityEngine.Object.Instantiate(settings.femaleActorPrefab,root.transform).GetComponent<MatchActor>();
            human.Initialize();human.ResetAt(new Vector3(1.2f,0,-6),-1);
            var opponent=UnityEngine.Object.Instantiate(settings.maleActorPrefab,root.transform).GetComponent<MatchActor>();
            opponent.Initialize();opponent.ResetAt(new Vector3(-1.2f,0,6),1);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.SetParent(root.transform);
            ground.transform.localScale=new Vector3(1.5f,1,2.7f);
            groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMaterial.color=new Color(.07f,.26f,.23f);
            ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            var light=new GameObject("Light").AddComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(48,-30,0);
            var view=new GameObject("Gameplay probe camera").AddComponent<Camera>();view.transform.SetParent(root.transform);view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.1f,.17f,.24f);
            CourtFollowView.Apply(view,human.transform.position,-1,false,0,true);
            var match=root.AddComponent<BotMatch>();match.enabled=false;match.human=human;match.bot=opponent;
            var rules=new PickleVerse.Rules.SinglesRules(0);typeof(BotMatch).GetProperty("Rules").SetValue(match,rules);
            // Let the first frame upload the real character materials before timing the toast.
            await Task.Delay(700);
            rules.Fail(0,PickleVerse.Rules.Fault.Out);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(BotMatch).GetMethod("BeginFault",flags).Invoke(match,null);
            typeof(BotMatch).GetMethod("LateUpdate",flags).Invoke(match,null);
            var toast=root.GetComponent<CoachingQuickToast>();
            var tip=toast.GetComponentInChildren<TMPro.TMP_Text>();
            await Task.Delay(300);
            tip.ForceMeshUpdate();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"bot-feedback.png"));
            await Task.Delay(250);
            if(!tip.text.Contains("ra ngoài") || tip.isTextOverflowing) throw new Exception("Bot coaching toast: " + tip.text + "; rect=" + tip.rectTransform.rect + "; lines=" + tip.textInfo.lineCount + "; font=" + tip.fontSize + "; overflow=" + tip.isTextOverflowing);
            var noticeRoot=new GameObject("Online notice verification",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));noticeRoot.transform.SetParent(root.transform);
            noticeRoot.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;noticeRoot.GetComponent<Canvas>().sortingOrder=70;
            var scaler=noticeRoot.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var ui=noticeRoot.AddComponent<MultiplayerCourtUI>();ui.enabled=false;noticeRoot.GetComponent<MultiplayerResultScreen>().enabled=false;
            var notices=noticeRoot.AddComponent<MatchAnnouncements>();notices.externallyDriven=true;
            notices.panel=MatchPresentationUi.Rect("Referee",noticeRoot.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            notices.panel.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.03f,.05f,.09f,.98f);
            notices.opacity=notices.panel.gameObject.AddComponent<CanvasGroup>();
            notices.headline=MatchPresentationUi.Label("Title",notices.panel,tip.font,"",28,new Vector2(.04f,.57f),new Vector2(.96f,.96f),Color.white);
            notices.detail=MatchPresentationUi.Label("Detail",notices.panel,tip.font,"",18,new Vector2(.04f,.07f),new Vector2(.96f,.55f),Color.white);
            notices.accent=MatchPresentationUi.Image("Accent",notices.panel,Vector2.zero,new Vector2(.006f,1),Color.white);
            ui.announcements=notices;
            Canvas.ForceUpdateCanvases();
            var network=root.AddComponent<NetworkMatch>();
            typeof(NetworkMatch).GetMethod("AnnouncementEffect",flags).Invoke(network,new object[]{"FAULT / LỖI","Bóng ngoài sân • Đội A phạm lỗi\nĐổi giao bóng / Side Out",(int)AnnouncementTone.Fault,1.2f});
            notices.Advance(.22f);await Task.Delay(250);
            if(!notices.Busy || notices.ActiveTitle!="LỖI" || !notices.panel.gameObject.activeInHierarchy)throw new Exception("Online fault notice did not render.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"online-fault-notice.png"));await Task.Delay(250);
            var canvas=new GameObject("Analysis probe",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(root.transform);
            var summary=canvas.AddComponent<CoachingSessionView>();
            var analysis=new PickleVerse.Progression.CoachingAnalysis {summary="Đã ghi nhận 12 lượt đánh, 10 lần tiếp xúc bóng và 3 lượt giao bóng.\nCần cải thiện: Bóng ngoài sân 2/12 lần quan sát.\nƯu tiên kiểm soát hướng và lực để giữ bóng trong sân."};
            typeof(CoachingSessionView).GetField("summary",flags).SetValue(summary,new PickleVerse.Progression.CoachingSummary {status="ready",analysis=analysis});
            typeof(CoachingSessionView).GetMethod("Open",flags).Invoke(summary,null);
            await Task.Delay(300);
            var details=(GameObject)typeof(CoachingSessionView).GetField("details",flags).GetValue(summary);
            var body=details.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="Findings");body.ForceMeshUpdate();
            if(body.isTextOverflowing) throw new Exception("Session analysis is clipped.");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(output,"session-analysis.png"));
            await Task.Delay(250);
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(new {frames=Time.frameCount-firstFrame,tip=tip.text,tipOverflow=tip.isTextOverflowing,notice=notices.ActiveTitle,analysis=body.text,analysisOverflow=body.isTextOverflowing,cameraPosition=new[]{view.transform.position.x,view.transform.position.y,view.transform.position.z},fieldOfView=view.fieldOfView});
            System.IO.File.WriteAllText(System.IO.Path.Combine(output,"presentation.json"),json);
            Debug.Log("[Gameplay verification] Realtime tip and session analysis rendered without clipping.");
            return json;
        }
        finally
        {
            UnityEngine.Object.Destroy(root);
            if(groundMaterial)UnityEngine.Object.Destroy(groundMaterial);
            foreach(var c in oldCanvases)if(c)c.enabled=true;
            foreach(var c in oldCameras)if(c)c.enabled=true;
        }
    }
    public static async Task<string> Transport()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
        MultiplayerApp.Ensure();
        var sessions = new List<PhotonSession>();
        var events = new List<object>();
        string name = "pkv-animation-probe-" + Guid.NewGuid().ToString("N");
        try
        {
            var server = PhotonSession.Create("Probe Server"); sessions.Add(server);
            server.PrepareDedicated(new MatchRoster());
            var config = PhotonSession.ConfigForSession(true);
            config.PeerMode = NetworkProjectConfig.PeerModes.Multiple;
            var scene = new NetworkSceneInfo();
            scene.AddSceneRef(SceneRef.FromPath("Assets/Scenes/PhotonLobby.unity"),LoadSceneMode.Additive);
            var started = await server.Runner.StartGame(new StartGameArgs {GameMode=GameMode.Server,SessionName=name,PlayerCount=2,
                IsVisible=false,Config=config,Scene=scene,SceneManager=server.Runner.gameObject.AddComponent<PhotonSceneManager>(),CustomPhotonAppSettings=PhotonSession.Settings("asia")});
            if (!started.Ok) throw new Exception("Server: " + started.ShutdownReason);
            for (int i=0;i<2;i++)
            {
                var client = PhotonSession.Create("Probe Client " + i); sessions.Add(client);
                client.PrepareDedicated(new MatchRoster());
                var receive = client.Callbacks.Reliable;
                client.Callbacks.Reliable = (source, bytes) => {
                    var msg=Newtonsoft.Json.JsonConvert.DeserializeObject<PhotonMessage>(System.Text.Encoding.UTF8.GetString(bytes));
                    client.Runner.TryFindObject(new NetworkId {Raw=msg.target},out var target);
                    events.Add(new {kind=msg.kind,source=source.RawEncoded,authority=target ? target.StateAuthority.RawEncoded : -999, found=(bool)target});
                    receive(source,bytes);
                };
                started=await client.Runner.StartGame(new StartGameArgs {GameMode=GameMode.Client,SessionName=name,EnableClientSessionCreation=false,
                    Config=config,Scene=scene,SceneManager=client.Runner.gameObject.AddComponent<PhotonSceneManager>(),CustomPhotonAppSettings=PhotonSession.Settings("asia")});
                if(!started.Ok) throw new Exception("Client: " + started.ShutdownReason);
            }
            var prefab=Resources.Load<MultiplayerSettings>("MultiplayerSettings").dedicatedPlayerPrefab;
            var spawned=await server.Runner.SpawnAsync(prefab,Vector3.zero,Quaternion.identity,onBeforeSpawned:(runner,obj)=>
                obj.GetComponent<NetworkPlayer>().Initialize(new RoomMember {id="probe",gender="Male"},0,new Vector3(0,0,-6)));
            await Task.Delay(1500);
            var replicas=NetworkPlayer.All.Where(p=>p.Runner!=server.Runner).ToArray();
            var actor=spawned.GetComponent<NetworkPlayer>();
            actor.BeginSwing(new PickleVerse.Multiplayer.ShotIntent {kind=PickleVerse.Multiplayer.ShotKind.Groundstroke},new Vector3(0,1,-5));
            server.Send(sessions[1].Runner.LocalPlayer,spawned,"split",new object[0]);
            await Task.Delay(700);
            var result = new {events,replicas=replicas.Select(p=>new {p.Actor.Swinging,p.Actor.CurrentClip,authority=p.Object.StateAuthority.RawEncoded}).ToArray()};
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(result);
            System.IO.Directory.CreateDirectory("../artifacts/gameplay-fixes-20261105");
            System.IO.File.WriteAllText("../artifacts/gameplay-fixes-20261105/dedicated-events-after.json",json);
            Debug.Log("[Gameplay verification] Dedicated event replicas: " + json);
            if (replicas.Length != 2 || replicas.Any(p=>!p.Actor.Swinging)) throw new Exception("Both replicas must play the confirmed swing.");
            return json;
        }
        finally
        {
            foreach(var s in sessions.AsEnumerable().Reverse()) await s.LeaveAsync();
        }
    }
    public static string Inspect()
    {
        var settings = Resources.Load<MultiplayerSettings>("MultiplayerSettings");
        var report = new System.Collections.Generic.List<object>();
        foreach (var prefab in new[] {settings.femaleActorPrefab, settings.maleActorPrefab})
        {
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var actor = root.GetComponent<MatchActor>();
                actor.Initialize();
                actor.ResetAt(new Vector3(0,0,-6),-1);
                var bones = actor.visual.GetComponentsInChildren<Transform>();
                var before = bones.Select(b=>b.localRotation).ToArray();
                for (int i=0;i<40;i++) actor.RenderNetworkPose(Vector3.forward*3,false,1f/60,true,Vector2.up);
                int changed = bones.Where((b,i)=>Quaternion.Angle(before[i],b.localRotation)>.5f).Count();
                report.Add(new {prefab=prefab.name,actor.visual.name,clips=actor.clips.Length, clip=actor.CurrentClip,changed,
                    paths=actor.clips.Take(2).Select(c=>new {c.name,asset=AssetDatabase.GetAssetPath(c.clip),curves=AnimationUtility.GetCurveBindings(c.clip).Take(2).Select(b=>b.path).ToArray()}).ToArray()});
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }
}
