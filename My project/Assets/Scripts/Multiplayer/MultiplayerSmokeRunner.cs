using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>Opt-in build smoke runs. No bot or automation is enabled in normal player sessions.</summary>
    public sealed class MultiplayerSmokeRunner : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(Environment.GetCommandLineArgs().Contains("-pkvCase"))
            { var go=new GameObject("MultiplayerSmokeRunner"); DontDestroyOnLoad(go); go.AddComponent<MultiplayerSmokeRunner>(); }
        }
        async void Start()
        {
            var args=Environment.GetCommandLineArgs();
            string Arg(string key,string fallback="") { int i=Array.IndexOf(args,key); return i>=0&&i+1<args.Length?args[i+1]:fallback; }
            string test=Arg("-pkvCase"),profile=Arg("-pkvProfile",PickleVerse.Progression.ProgressionRuntime.Instance.Player.id),file=Arg("-pkvReceipt");
            try
            {
                await Task.Delay(500); var app=MultiplayerApp.Ensure(); app.DisplayName=profile;
                FlowState last=app.State;
                app.Changed+=()=> { if(app.State!=last) { last=app.State; Debug.Log("SMOKE "+profile+" "+last+" "+app.Elapsed+" "+app.Notice); } };
                _=Diagnostics(app,profile);
                if(Arg("-pkvBehavior")!="cancelConnect")_=AcceptForSmoke(app);
                await app.PlayAsync(profile);
                if(test.StartsWith("departure"))
                {
                    await MultiplayerSmokeChecks.RoomDeparture(app,test.EndsWith("Host"),test.Contains("Custom"),file);
                    string departureResult="PASS "+test+" "+profile+": host transfer / custom closure";
                    File.WriteAllText(file+"."+profile+".txt",departureResult); Debug.Log(departureResult); return;
                }
                if(test=="duoControlHost"||test=="duoControlGuest")
                {
                    await MultiplayerSmokeChecks.DuoControls(app,test=="duoControlHost",file);
                    string controlResult="PASS "+test+" "+profile+": guest cancel, party retained, captain Singles disbands party";
                    File.WriteAllText(file+"."+profile+".txt",controlResult); Debug.Log(controlResult); return;
                }
                if(test=="customAdminHost"||test=="customAdminGuest") await MultiplayerSmokeChecks.CustomAdministration(app,test=="customAdminHost",file);
                if(test=="doubles") await app.SelectModeAsync(PlayMode.Doubles);
                if(test=="customHost")
                {
                    await app.CreateCustomAsync(Arg("-pkvMode","1")=="2"?PlayMode.Doubles:PlayMode.Singles);
                    MultiplayerSmokeChecks.WriteSharedCode(file+".code",app.Room.Code);
                    int count = int.Parse(Arg("-pkvCount", (2*(int)app.Mode).ToString()));
                    double nextRoomDiagnostic=0;
                    while(app.Members().Count<count || !UI.RoomViewModel.CanStartCustom(app))
                    {
                        if(Time.realtimeSinceStartupAsDouble>=nextRoomDiagnostic)
                        {
                            nextRoomDiagnostic=Time.realtimeSinceStartupAsDouble+8;
                            Debug.Log("CUSTOM_WAIT " + profile + " captain=" + app.IsCaptain + " busy=" + app.Busy +
                                " teams=" + string.Join(",",app.Members().Select(m=>m.team)) + " room=" +
                                (app.Room.Properties.TryGetValue("room",out var data)?data.Value:"missing"));
                        }
                        await Task.Delay(100);
                    }
                    Debug.Log("CUSTOM_ROSTER " + profile + " mode=" + app.Mode + " teams=" +
                        string.Join(",", app.Members().Select(m => m.team)));
                    await app.RoomService.StartCustomAsync();
                }
                else if(test=="customGuest" || test=="duoGuest")
                {
                    while(!File.Exists(file+".code")) await Task.Delay(500);
                    await app.JoinRoomAsync(File.ReadAllText(file+".code"));
                    if(test=="customGuest")
                    {
                        double teamDeadline=Time.realtimeSinceStartupAsDouble+20;
                        while(app.Members().First(m=>m.id==app.PlayerId).team<0 && Time.realtimeSinceStartupAsDouble<teamDeadline) await Task.Delay(200);
                    }
                }
                else if(test=="duoHost")
                {
                    await app.SelectModeAsync(PlayMode.Doubles); MultiplayerSmokeChecks.WriteSharedCode(file+".code",app.Room.Code);
                    while(app.Members().Count!=2) await Task.Delay(500);
                    await app.FindAsync();
                }
                else if(test!="customAdminHost"&&test!="customAdminGuest") await app.FindAsync();
                if(Arg("-pkvBehavior")=="cancelConnect") {await MultiplayerSmokeChecks.CancelDuringConnect(app,Arg("-pkvRole")=="S1");_=AcceptForSmoke(app);}
                if(test=="fallback" || test=="cancelFallback")
                {
                    double began=Time.realtimeSinceStartupAsDouble;
                    if(test=="cancelFallback")
                    {
                        while(Time.realtimeSinceStartupAsDouble-began<PickleVerse.Progression.RankedSearchGate.DeadlineSeconds-1) await Task.Delay(50);
                        await app.CancelSearchAsync(); await Task.Delay(2000);
                        if(app.Network.IsListening || app.Roster?.RankedBot==true || app.State!=FlowState.WaitingRoom) throw new InvalidOperationException("Bot xuất hiện sau hủy sát hạn.");
                    }
                    else
                    {
                        while(app.State!=FlowState.InMatch && Time.realtimeSinceStartupAsDouble-began<PickleVerse.Progression.RankedSearchGate.DeadlineSeconds+35) await Task.Delay(50);
                        var pending=PickleVerse.Progression.ProgressionRuntime.Instance.Player.pendingMatch;
                        if(pending==null || pending.kind!=PickleVerse.Progression.MatchKind.RankedBot || Time.realtimeSinceStartupAsDouble-began<PickleVerse.Progression.RankedSearchGate.DeadlineSeconds) throw new InvalidOperationException("Bot rank không xuất hiện đúng hạn.");
                        var match=NetworkMatch.Instance;
                        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MultiplayerCourt" || !app.Roster.RankedBot || NetworkPlayer.All.Count!=2 || NetworkPlayer.All.Count(p=>p.IsLocalHuman)!=1 || !app.Roster.members.Any(m=>m.bot && !string.IsNullOrEmpty(m.name))) throw new InvalidOperationException("Bot chưa dùng đội hình và sân multiplayer.");
                        match.AutomateLocalPlayerForSmoke(); Time.timeScale=4;
                        double gameDeadline=Time.realtimeSinceStartupAsDouble+200;
                        while(PickleVerse.Progression.ProgressionRuntime.Instance.Player.pendingMatch!=null && Time.realtimeSinceStartupAsDouble<gameDeadline) await Task.Delay(50);
                        Time.timeScale=1;
                        var history=PickleVerse.Progression.ProgressionRuntime.Instance.Player.history;
                        if(history.Count==0 || history[0].kind!=PickleVerse.Progression.MatchKind.RankedBot || history[0].localForfeit || !history[0].scores.Any(s=>s.completed) || match.BotReturns==0) throw new InvalidOperationException("Bot rank chưa hoàn tất trận bằng gameplay thật.");
                        await OpenSummaryForSmoke(); var summary=MultiplayerCourtUI.Instance.resultScreen;
                        if(!summary.IsVisible || summary.RenderedMatchId!=history[0].id || !summary.delta.text.Contains(history[0].eloDelta.ToString()) || !summary.rank.text.Contains(PickleVerse.Progression.EloPolicy.Rank(PickleVerse.Progression.ProgressionRuntime.Instance.Player.elo).ToUpperInvariant())) throw new InvalidOperationException("Trang tổng kết bot thiếu Elo/rank đã lưu.");
                        File.WriteAllText(file+"."+profile+".history.json",JsonUtility.ToJson(PickleVerse.Progression.ProgressionRuntime.Instance.Player,true));
                    }
                    string fallbackResult="PASS "+test+" "+profile+": elapsed="+(Time.realtimeSinceStartupAsDouble-began).ToString("0.000")+" state="+app.State+" scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    File.WriteAllText(file+"."+profile+".txt",fallbackResult); Debug.Log(fallbackResult); return;
                }
                double until=Time.realtimeSinceStartupAsDouble+160;
                while(app.State!=FlowState.InMatch && Time.realtimeSinceStartupAsDouble<until)
                {
                    await Task.Delay(500);
                }
                while(NetworkPlayer.All.Count<(app.Roster?.Count ?? 2*(int)app.Mode) && Time.realtimeSinceStartupAsDouble<until) await Task.Delay(200);
                if(app.State!=FlowState.InMatch || NetworkPlayer.All.Count!=app.Roster.Count) throw new InvalidOperationException("Không vào được sân: "+app.Notice);
                if(!app.Roster.IsValid() || (!app.Custom && app.Roster.members.GroupBy(m=>m.group).Any(g=>g.Select(m=>m.team).Distinct().Count()>1))) throw new InvalidOperationException("Nhóm bị tách đội.");
                string result=$"PASS {test} {profile}: {NetworkPlayer.All.Count} players, host={app.Network.IsHost}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
                if(file.Length>0) File.WriteAllText(file+"."+profile+".txt",result);
                Debug.Log(result);
                if(args.Contains("-pkvBehavior")&&Arg("-pkvBehavior")!="cancelConnect")
                {
                    string behavior=Arg("-pkvBehavior"); await MultiplayerSmokeChecks.GameDeparture(app,behavior);
                    result+="; "+behavior+" PASS"; File.WriteAllText(file+"."+profile+".txt",result); Debug.Log(result); return;
                }
                if(args.Contains("-pkvReconnect")) { await MultiplayerSmokeChecks.Reconnect(app); result+="; reconnect PASS"; File.WriteAllText(file+"."+profile+".txt",result); Debug.Log(result); }
                if(args.Contains("-pkvLifecycle"))
                {
                    double deadline=Time.realtimeSinceStartupAsDouble+300,nextServe=0,nextReport=0;
                    while(app.State!=FlowState.Results && Time.realtimeSinceStartupAsDouble<deadline)
                    {
                        var match=NetworkMatch.Instance;
                        if(match!=null && match.IsServer && Time.realtimeSinceStartupAsDouble>=nextReport)
                        {
                            nextReport=Time.realtimeSinceStartupAsDouble+5;
                            Debug.Log($"LIFECYCLE {profile} games={match.GamesA.Value}:{match.GamesB.Value} score={match.ScoreA.Value}:{match.ScoreB.Value} hits={match.Rules.strokeCount} live={match.Rules.live} paused={match.Paused.Value} reason={match.Rules.reason}");
                        }
                        if(match!=null && match.IsServer && match.BetweenGames.Value) match.RequestNextGame();
                        if(match!=null && match.IsServer && match.Rules.waitingServe && Time.realtimeSinceStartupAsDouble>=nextServe)
                        {
                            var server=NetworkPlayer.All.First(p=>p.Team.Value==match.Rules.servingTeam && p.Index.Value==match.Rules.serverIndex);
                            match.TrySwing(server,new ShotIntent{kind=ShotKind.Groundstroke}); nextServe=Time.realtimeSinceStartupAsDouble+2;
                        }
                        await Task.Delay(50);
                    }
                    if(app.State!=FlowState.Results) throw new InvalidOperationException("Không kết thúc game bằng giao bóng thật: "+NetworkMatch.Instance?.Status.Value);
                    string score=NetworkMatch.Instance.ScoreA.Value+":"+NetworkMatch.Instance.ScoreB.Value;
                    string games=NetworkMatch.Instance.GamesA.Value+":"+NetworkMatch.Instance.GamesB.Value;
                    if(Math.Max(NetworkMatch.Instance.GamesA.Value,NetworkMatch.Instance.GamesB.Value)!=2) throw new InvalidOperationException("Trận chưa đủ hai game thắng: "+games);
                    if(NetworkMatch.Instance.Rules!=null && (!NetworkMatch.Instance.Rules.complete || NetworkMatch.Instance.Rules.score.Max()<11)) throw new InvalidOperationException("Game kết thúc do rời/mất kết nối, chưa phải thắng 11 điểm");
                    if(Math.Max(NetworkMatch.Instance.ScoreA.Value,NetworkMatch.Instance.ScoreB.Value)<11) throw new InvalidOperationException("Kết quả không phải game đủ 11 điểm: "+NetworkMatch.Instance.Status.Value);
                    await OpenSummaryForSmoke();
                    var saved=PickleVerse.Progression.ProgressionRuntime.Instance.Player.history.Find(h=>h.id==app.Roster.matchId);
                    var summary=MultiplayerCourtUI.Instance.resultScreen;
                    if(saved==null || !summary.IsVisible || summary.RenderedMatchId!=saved.id || !summary.delta.text.Contains(saved.eloDelta.ToString()) || !summary.rank.text.Contains(PickleVerse.Progression.EloPolicy.Rank(PickleVerse.Progression.ProgressionRuntime.Instance.Player.elo).ToUpperInvariant())) throw new InvalidOperationException("Trang tổng kết multiplayer thiếu Elo/rank đã lưu.");
                    result+="; summary="+summary.delta.text+" rank="+PickleVerse.Progression.EloPolicy.Rank(PickleVerse.Progression.ProgressionRuntime.Instance.Player.elo);
                    await app.ReturnToRoomAsync("Smoke game đã kết thúc.");
                    if(app.State!=FlowState.WaitingRoom || (app.Custom && app.Members().Any(m=>m.ready))) throw new InvalidOperationException("Không trở về phòng đúng.");
                    result+="; lifecycle PASS games="+games+" score="+score+" room="+app.Mode+" members="+app.Members().Count;
                    File.WriteAllText(file+"."+profile+".txt",result); Debug.Log(result);
                }
            }
            catch(Exception e)
            {
                string result="FAIL "+test+" "+profile+": "+e.GetBaseException().Message;
                if(file.Length>0) File.WriteAllText(file+"."+profile+".txt",result); Debug.LogWarning(result);
            }
        }
        async Task Diagnostics(MultiplayerApp app,string profile)
        {
            for(int i=0;i<40 && app!=null;i++)
            {
                await Task.Delay(5000);
                if(app.State==FlowState.Connecting || app.State==FlowState.Searching)
                    Debug.Log("DIAGNOSTIC "+profile+" "+Newtonsoft.Json.JsonConvert.SerializeObject(new{
                        state=app.State.ToString(),app.Notice,session=app.Match?.Id,sessionHost=app.Match?.Host,roster=app.Roster,
                        listening=app.Network.IsListening,host=app.Network.IsHost,connected=app.Network.IsConnectedClient,
                        clients=app.Network.ConnectedClientsIds,identities=app.ClientIdentities}));
            }
        }
        static async Task AcceptForSmoke(MultiplayerApp app)
        {
            // Only installed by the explicit -pkvCase command-line switch.
            while(app && app.State!=FlowState.InMatch && app.State!=FlowState.Results)
            {if(app.CanAcceptMatch)await app.AcceptMatchAsync();await Task.Delay(100);}
        }
        static async Task OpenSummaryForSmoke()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+15;
            while(Time.realtimeSinceStartupAsDouble<deadline)
            {
                var ui=MultiplayerCourtUI.Instance;
                if(ui && ui.cinematic && ui.cinematic.OutcomeReady){ui.resultScreen.Open();await Task.Delay(200);return;}
                await Task.Delay(100);
            }
            throw new InvalidOperationException("Thông báo/cinematic kết quả chưa hoàn tất.");
        }
    }
}
