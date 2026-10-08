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
            string test=Arg("-pkvCase"),profile=Arg("-pkvProfile","Smoke"+System.Diagnostics.Process.GetCurrentProcess().Id),file=Arg("-pkvReceipt");
            try
            {
                await Task.Delay(500); var app=MultiplayerApp.Ensure(); app.DisplayName=profile;
                FlowState last=app.State;
                app.Changed+=()=> { if(app.State!=last) { last=app.State; Debug.Log("SMOKE "+profile+" "+last+" "+app.Elapsed+" "+app.Notice); } };
                _=Diagnostics(app,profile);
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
                    File.WriteAllText(file+".code",app.Room.Code);
                    int count = int.Parse(Arg("-pkvCount", (2*(int)app.Mode).ToString()));
                    while(app.Members().Count<count || app.Members().Any(m=>m.team<0) || app.Members().Select(m=>m.team).Distinct().Count()!=2) await Task.Delay(500);
                    await app.StartCustomAsync();
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
                    await app.SelectModeAsync(PlayMode.Doubles); File.WriteAllText(file+".code",app.Room.Code);
                    while(app.Members().Count!=2) await Task.Delay(500);
                    await app.FindAsync();
                }
                else if(test!="customAdminHost"&&test!="customAdminGuest") await app.FindAsync();
                if(Arg("-pkvBehavior")=="cancelConnect") await MultiplayerSmokeChecks.CancelDuringConnect(app,Arg("-pkvRole")=="S1");
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
                    await Task.Delay(2000); await app.ReturnToRoomAsync("Smoke game đã kết thúc.");
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
    }
}
