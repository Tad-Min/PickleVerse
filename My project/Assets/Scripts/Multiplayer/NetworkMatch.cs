using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using PickleVerse.Audio;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct BallFrame : INetworkSerializable,IEquatable<BallFrame>
    {
        public Vector3 position,velocity;
        public bool live;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter { serializer.SerializeValue(ref position); serializer.SerializeValue(ref velocity); serializer.SerializeValue(ref live); }
        public bool Equals(BallFrame o)=>position==o.position&&velocity==o.velocity&&live==o.live;
    }
    public sealed class NetworkMatch : NetworkBehaviour
    {
        public static NetworkMatch Instance { get; private set; }
        public NetworkVariable<FixedString4096Bytes> RosterJson=new NetworkVariable<FixedString4096Bytes>();
        public NetworkVariable<FixedString512Bytes> Status=new NetworkVariable<FixedString512Bytes>();
        public NetworkVariable<int> ScoreA=new NetworkVariable<int>(),ScoreB=new NetworkVariable<int>(),ServerTeam=new NetworkVariable<int>(),ServerIndex=new NetworkVariable<int>(),ServerNumber=new NetworkVariable<int>();
        public NetworkVariable<int> GamesA=new NetworkVariable<int>(),GamesB=new NetworkVariable<int>();
        public NetworkVariable<bool> EndsSwapped=new NetworkVariable<bool>(),BetweenGames=new NetworkVariable<bool>(),BetweenPoints=new NetworkVariable<bool>();
        public NetworkVariable<bool> Paused=new NetworkVariable<bool>(),Finished=new NetworkVariable<bool>(),WaitingServe=new NetworkVariable<bool>(),Started=new NetworkVariable<bool>();
        public NetworkVariable<BallFrame> Ball=new NetworkVariable<BallFrame>();
        public PickleballRules Rules { get; private set; }
        public Rigidbody Body { get; private set; }
        public MatchRoster Roster { get; private set; }
        readonly Dictionary<string,double> reservations=new Dictionary<string,double>();
        readonly Dictionary<string,NetworkPlayer> players=new Dictionary<string,NetworkPlayer>();
        readonly HashSet<ulong> courtReady=new HashSet<ulong>();
        double serveDeadline,rallyDeadline;
        Vector3 savedVelocity;
        bool rallyEnding, rallyCommitted;
        MatchRoster initialRoster;
        string shotLabel=""; double shotLabelUntil;
        readonly IGameplayShotService shots = new BotMatchGameplayService();
        Vector3 ballPosition, ballVelocity, serveOrigin;
        TrailRenderer ballTrail;
        float accumulator;
        bool releasedServe;
        double nextGameDeadline;
        int countdownSecond=-1;
        public int PlayerEnd(int team) => (team==0 ? -1 : 1) * (EndsSwapped.Value ? -1 : 1);
        public void RequestNextGame() { if(IsServer) NextGame(); }
        void NextGame()
        { if(!BetweenGames.Value || Finished.Value) return; Rules.NextGame(); EndsSwapped.Value=Rules.ends[0]>0; BetweenGames.Value=false; BeginRally(); }

        public override void OnNetworkSpawn()
        {
            Instance=this;
            if(IsServer) RosterJson.Value=new FixedString4096Bytes(JsonConvert.SerializeObject(initialRoster??MultiplayerApp.Instance.Roster));
            Roster=JsonConvert.DeserializeObject<MatchRoster>(RosterJson.Value.ToString());
            BuildBall(); MultiplayerApp.Instance.CourtLoaded();
            Finished.OnValueChanged+=ResultChanged;
            Started.OnValueChanged+=StartChanged;
            if(IsServer)
            {
                Rules=new PickleballRules(Roster.mode, Roster.members.Count(p=>p.team==0), Roster.members.Count(p=>p.team==1));
                SpawnPlayers(); NetworkManager.OnClientConnectedCallback+=PlayerConnected;
                Body.isKinematic=true;
            }
            else StartCoroutine(ClientCamera());
        }
        public override void OnNetworkDespawn()
        {
            if(Instance==this) Instance=null; Finished.OnValueChanged-=ResultChanged; Started.OnValueChanged-=StartChanged;
            if(NetworkManager!=null) NetworkManager.OnClientConnectedCallback-=PlayerConnected;
            if(Body!=null) Destroy(Body.gameObject);
        }
        public void SetRoster(MatchRoster roster) { initialRoster=roster; }
        void BuildBall()
        {
            var settings=MultiplayerApp.Instance.Settings;
            var ball = settings.ballVisualPrefab ? Instantiate(settings.ballVisualPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name="NetworkPickleball";
            if (!settings.ballVisualPrefab) { ball.transform.localScale=Vector3.one*(Court.BallRadius*2); ball.GetComponent<Renderer>().sharedMaterial=settings.ballMaterial; }
            foreach(var collider in ball.GetComponentsInChildren<Collider>()) collider.enabled=false;
            Body=ball.GetComponent<Rigidbody>();
            if (!Body) Body=ball.AddComponent<Rigidbody>();
            Body.mass=.026f; Body.isKinematic=true; Body.useGravity=false;
            ballTrail=ball.GetComponent<TrailRenderer>();
            if (!ballTrail)
            {
                ballTrail=ball.AddComponent<TrailRenderer>(); ballTrail.time=.22f;
                ballTrail.startWidth=.025f; ballTrail.endWidth=.002f; ballTrail.sharedMaterial=settings.trailMaterial;
            }
            ballTrail.emitting=false; ballTrail.Clear();
        }
        void SpawnPlayers()
        {
            var app=MultiplayerApp.Instance;
            foreach(var m in Roster.members)
            {
                var client=app.ClientIdentities.First(p=>p.Value==m.id).Key;
                int index=Roster.members.Where(p=>p.team==m.team).ToList().FindIndex(p=>p.id==m.id);
                var go=Instantiate(app.Settings.playerPrefab);
                var player=go.GetComponent<NetworkPlayer>(); player.Initialize(m,index,StartingPosition(m.team,index));
                var networkObject=go.GetComponent<NetworkObject>(); networkObject.DontDestroyWithOwner=true; networkObject.SpawnAsPlayerObject(client,true);
                players[m.id]=player;
            }
            StartCoroutine(ClientCamera());
        }
        Vector3 StartingPosition(int team,int index)
        {
            float facing=Rules.TeamFacing(team);
            if(team==Rules.servingTeam && index==Rules.serverIndex) return new Vector3(Mathf.Sign(Rules.serverWorldX)*1.45f,0,-facing*7.25f);
            if(team!=Rules.servingTeam && index==Rules.receiverIndex) return new Vector3(-Mathf.Sign(Rules.serverWorldX)*1.45f,0,-facing*6.1f);
            int right=Rules.courtRight[team];
            return new Vector3((index==right?1:-1)*facing*1.5f,0,-facing*4.2f);
        }
        IEnumerator ClientCamera()
        {
            while(NetworkPlayer.All.Count<Roster.Count || NetworkPlayer.All.All(p=>!p.IsOwner)) yield return null;
            var player=NetworkPlayer.All.First(p=>p.IsOwner);
            var camera=Camera.main;
            if(camera) { var follow=camera.GetComponent<CourtCamera>(); if(!follow) follow=camera.gameObject.AddComponent<CourtCamera>(); follow.target=player.transform; }
            if(Environment.GetCommandLineArgs().Contains("-pkvSlowCourt")) yield return new WaitForSecondsRealtime(3);
            CourtReadyRpc();
            if(Started.Value) StartChanged(false,true);
            if(Finished.Value) ResultChanged(false,true);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void CourtReadyRpc(RpcParams rpc=default)
        {
            if(Started.Value || !MultiplayerApp.Instance.ClientIdentities.ContainsKey(rpc.Receive.SenderClientId)) return;
            courtReady.Add(rpc.Receive.SenderClientId);
            if(courtReady.Count==Roster.Count) { BeginRally(); Started.Value=true; }
        }
        void StartChanged(bool old,bool started) { if(started) MultiplayerApp.Instance.MatchStarted(); }
        public void TrySwing(NetworkPlayer player, ShotIntent intent)
        {
            if (!IsServer || !Started.Value || Paused.Value || Finished.Value || BetweenGames.Value || !player.Connected || player.Actor.Swinging) return;
            if (intent.attack && intent.crossCourt) return;
            bool serve = Rules.waitingServe && player.Team.Value == Rules.servingTeam && player.Index.Value == Rules.serverIndex && intent.kind == ShotKind.Groundstroke;
            if (Rules.waitingServe && !serve || !Rules.live && !serve) return;
            if (serve) intent.kind = ShotKind.Serve;
            var local = BotMatchGameplayService.ToLocal(intent);
            var predicted = ballPosition + ballVelocity * local.ContactDelay + Vector3.down * (.5f * BallFlight.Gravity * local.ContactDelay * local.ContactDelay);
            player.BeginSwing(intent, predicted);
            if (serve) { releasedServe = false; serveOrigin = player.transform.position + player.transform.right * .3f + player.transform.forward * .5f + Vector3.up * .94f; }
        }
        void BeginRally()
        {
            Rules.NewRally(); rallyEnding = rallyCommitted = false; BetweenPoints.Value=false; releasedServe = false;
            countdownSecond=-1;
            foreach (var player in players.Values) { player.Teleport(StartingPosition(player.Team.Value, player.Index.Value)); player.ResetInput(); }
            var server = players.Values.First(p => p.Team.Value == Rules.servingTeam && p.Index.Value == Rules.serverIndex);
            ballPosition = server.transform.position + Vector3.up * .9f; ballVelocity = Vector3.zero; Body.position = ballPosition;
            if (ballTrail) { ballTrail.emitting=false; ballTrail.Clear(); }
            serveDeadline = Time.realtimeSinceStartupAsDouble + 10; Publish();
        }
        void FixedUpdate()
        {
            if (!IsSpawned || Body == null || !Started.Value) return;
            if (ballTrail) ballTrail.emitting=(IsServer ? Rules.live : Ball.Value.live) && !Paused.Value && !Finished.Value;
            if (!IsServer) { Body.position = Vector3.Lerp(Body.position, Ball.Value.position, 1 - Mathf.Exp(-22 * Time.fixedDeltaTime)); return; }
            if (Paused.Value)
            {
                foreach (var r in reservations.ToArray()) if (Time.realtimeSinceStartupAsDouble > r.Value)
                { Rules.Forfeit(Roster.Find(r.Key).team, "Mất kết nối quá 30 giây"); reservations.Clear(); Finish(); break; }
                return;
            }
            accumulator += Time.fixedDeltaTime;
            const float tick = 1f / 120f;
            while (accumulator >= tick) { StepSimulation(tick); accumulator -= tick; }
            Body.position = ballPosition;
            Ball.Value = new BallFrame { position = ballPosition, velocity = ballVelocity, live = Rules.live };
            Publish();
        }
        void StepSimulation(float dt)
        {
            foreach (var player in players.Values) player.Simulate(dt, !Finished.Value && !BetweenGames.Value && !rallyCommitted);
            if (Finished.Value) return;
            if (BetweenGames.Value) { if(Time.realtimeSinceStartupAsDouble>=nextGameDeadline) NextGame(); return; }
            if (rallyCommitted) { if(Time.realtimeSinceStartupAsDouble>=rallyDeadline) BeginRally(); return; }
            foreach (var player in players.Values)
            {
                Rules.CheckActorMomentum(player.Team.Value, player.Index.Value, player.Actor.VolleyMomentum, player.Actor.InKitchen);
                if (Rules.live && Mathf.Abs(player.transform.position.z) < .31f) Rules.Fault(player.Team.Value, "Chạm lưới");
            }
            if (Rules.waitingServe)
            {
                int second=Mathf.FloorToInt((float)(10-(serveDeadline-Time.realtimeSinceStartupAsDouble))+.0001f);
                if(second>=0&&second<10&&second!=countdownSecond){countdownSecond=second;MatchSoundRpc((int)GameSfx.Countdown);}
                var server = players.Values.First(p => p.Team.Value == Rules.servingTeam && p.Index.Value == Rules.serverIndex);
                var actor = server.Actor;
                if (actor.Swinging && actor.Intent.kind == PickleVerse.LocalMatch.ShotKind.Serve)
                {
                    if (actor.SwingTime >= .5f && !releasedServe)
                    { releasedServe = true; serveOrigin = actor.transform.TransformPoint(actor.ContactOffset(actor.Intent)) + Vector3.up * (.5f * BallFlight.Gravity * .3f * .3f); }
                    if (releasedServe) ballPosition = serveOrigin + Vector3.down * (.5f * BallFlight.Gravity * Mathf.Pow(actor.SwingTime - .5f, 2));
                    if (actor.SwingTime >= .8f && actor.ContactActive && Vector3.Distance(ballPosition, actor.paddleFace.position) <= MultiplayerApp.Instance.Settings.contactAssistRadius)
                    {
                        var left = actor.Feet(true); var right = actor.Feet(false);
                        if (!Court.LegalServeFeet(left, right, actor.end, Rules.RightServe ? 0 : 1)) Rules.Fault(server.Team.Value, "Lỗi chân giao bóng");
                        else if (Rules.Serve(server.Team.Value, server.Index.Value, server.transform.position, ballPosition,
                            new Vector3(0, actor.PaddleHeadTop, 0), actor.hand.position, actor.PaddleVelocity))
                        {
                            server.ConsumeContact(false);
                            ballVelocity = shots.Launch(actor, actor.Intent, ballPosition, Rules.serverWorldX);
                        }
                    }
                }
                else ballPosition = actor.transform.position + actor.transform.right * .25f + Vector3.up * .9f;
                if (Rules.waitingServe && Time.realtimeSinceStartupAsDouble > serveDeadline) Rules.Fault(Rules.servingTeam, "Quá 10 giây giao bóng");
            }
            else if (Rules.live)
            {
                var previous = ballPosition;
                BallFlight.Integrate(ref ballPosition, ref ballVelocity, dt);
                if (previous.z * ballPosition.z < 0 && Mathf.Abs(ballPosition.x) <= Court.NetHalfSpan)
                {
                    var crossing = Vector3.Lerp(previous, ballPosition, previous.z / (previous.z - ballPosition.z));
                    float top = Court.NetHeight(crossing.x);
                    if (crossing.y - Court.BallRadius <= top)
                    {
                        if (crossing.y >= top - .01f) { ballVelocity.z *= .6f; ballVelocity.x *= .6f; ballVelocity.y *= .5f; }
                        else { ballPosition.z = Mathf.Sign(previous.z) * .04f; ballVelocity.z *= -.16f; ballVelocity.x *= .45f; ballVelocity.y *= .35f; }
                    }
                }
                foreach (var player in players.Values)
                {
                    if (!Rules.live || player.Team.Value == Rules.lastHitTeam || !shots.CanContact(player.Actor, previous, ballPosition, Rules.bounceCount, MultiplayerApp.Instance.Settings.contactAssistRadius)) continue;
                    var actor = player.Actor; var shot = actor.Intent;
                    bool volley = Rules.bounceCount == 0;
                    if (Rules.Hit(player.Team.Value, player.Index.Value, actor.InKitchen ? new Vector3(0,0,-actor.end * Court.Kitchen) : actor.transform.position,
                        ballPosition, BotMatchGameplayService.FromLocal(shot).kind))
                    {
                        player.ConsumeContact(volley); ballVelocity = shots.Launch(actor, shot, ballPosition, Rules.serverWorldX);
                        foreach (var other in players.Values.Where(p => p.Team.Value != player.Team.Value)) other.SplitStep();
                        shotLabel = shot.ToString(); shotLabelUntil = Time.realtimeSinceStartupAsDouble + 1;
                    }
                }
                if (Rules.live && ballPosition.y <= Court.BallRadius)
                {
                    var bounce = ballPosition; bounce.y = Court.BallRadius; Rules.Bounce(bounce);
                    if (Rules.live) BallFlight.Rebound(ref ballPosition, ref ballVelocity);
                }
                if (Rules.live)
                {
                    foreach (var player in players.Values.Where(p => p.Team.Value != Rules.lastHitTeam))
                        if (ballPosition.y > .25f && ballPosition.y < 1.65f && BotMatchGameplayService.SegmentDistance(previous, ballPosition, player.transform.position + Vector3.up * .85f) < .24f)
                        { Rules.BodyContact(player.Team.Value); break; }
                    if (Mathf.Abs(ballPosition.x) > 7 || Mathf.Abs(ballPosition.z) > 12)
                        Rules.Fault(Rules.bounceCount > 0 ? 1 - Rules.lastHitTeam : Rules.lastHitTeam, "Bóng ngoài sân");
                }
            }
            if (!Rules.live && !Rules.waitingServe && !rallyEnding)
            { rallyEnding = true; ballVelocity = Vector3.zero;MatchSoundRpc((int)GameSfx.FaultWhistle); }
            if (rallyEnding && !Array.Exists(Rules.volleyMomentum, v => v))
            {
                int loser = Rules.pendingFault; Rules.SettleRally();
                rallyCommitted=true; BetweenPoints.Value=true; rallyDeadline=Time.realtimeSinceStartupAsDouble+2.1;
                foreach (var player in players.Values) player.React(player.Team.Value != loser, Rules.complete);
                if (Rules.complete) Finish();
                else if (Rules.gameFinished) { MatchSoundRpc((int)GameSfx.GameWhistle);BetweenGames.Value=true; nextGameDeadline=Time.realtimeSinceStartupAsDouble+120; }
                else if(Rules.endChangeDue) { MatchSoundRpc((int)GameSfx.GameWhistle);Rules.SwapEnds(); EndsSwapped.Value=Rules.ends[0]>0; }
            }
        }
        void Publish()
        {
            ScoreA.Value=Rules.score[0]; ScoreB.Value=Rules.score[1]; ServerTeam.Value=Rules.servingTeam; ServerIndex.Value=Rules.serverIndex; ServerNumber.Value=Rules.serverNumber; WaitingServe.Value=Rules.waitingServe;
            GamesA.Value=Rules.games[0]; GamesB.Value=Rules.games[1];
            if (!Finished.Value)
                Status.Value=new FixedString512Bytes(BetweenGames.Value?"Hết game — đổi sân. Chủ trận bấm Game tiếp; tự tiếp tục sau 120 giây.":Rules.live && Time.realtimeSinceStartupAsDouble<shotLabelUntil?shotLabel:Rules.reason);
        }
        public void ReserveDisconnected(ulong client)
        {
            if(!IsServer||Finished.Value) return;
            var app=MultiplayerApp.Instance;
            if(!app.ClientIdentities.TryGetValue(client,out var id)||!players.TryGetValue(id,out var player)) return;
            player.Connected=false; player.ResetInput();
            reservations[id]=Time.realtimeSinceStartupAsDouble+app.Settings.reconnectSeconds;
            if(!Paused.Value) savedVelocity=ballVelocity;
            Paused.Value=true; Body.isKinematic=true;
            Status.Value=new FixedString512Bytes("Tạm dừng — chờ người chơi kết nối lại, tối đa 30 giây");
            app.ClientIdentities.Remove(client);
        }
        void PlayerConnected(ulong client)
        {
            if(!IsServer) return;
            var app=MultiplayerApp.Instance;
            if(!app.ClientIdentities.TryGetValue(client,out var id)||!reservations.Remove(id)||!players.TryGetValue(id,out var player)) return;
            player.NetworkObject.ChangeOwnership(client); player.Connected=true; player.ResetInput();
            if(reservations.Count==0) { Paused.Value=false; Body.isKinematic=true; if(Rules.live) ballVelocity=savedVelocity; serveDeadline=Time.realtimeSinceStartupAsDouble+10;countdownSecond=-1; Publish(); }
        }
        public void RequestLeave() { LeaveRpc(); }
        public void PlayerLeftSession(string id)
        {
            var player=Roster?.Find(id);
            if(IsServer && !Finished.Value && player!=null)
            { Rules.Forfeit(player.team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void LeaveRpc(RpcParams rpc=default)
        {
            if(Finished.Value) return;
            if(MultiplayerApp.Instance.ClientIdentities.TryGetValue(rpc.Receive.SenderClientId,out var id))
            { Rules.Forfeit(Roster.Find(id).team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        void Finish()
        {
            if(!Finished.Value)MatchSoundRpc((int)GameSfx.GameWhistle);
            Body.isKinematic=true; Publish();
            Status.Value=new FixedString512Bytes($"Đội {(Rules.winner==0?"A":"B")} thắng trận {Rules.games[0]} : {Rules.games[1]} game — điểm game cuối {Rules.score[0]} : {Rules.score[1]}\n{Rules.reason}"); Finished.Value=true;
        }
        void ResultChanged(bool old,bool complete) { if(complete) MultiplayerApp.Instance.ShowResult(Status.Value.ToString()); }
        [Rpc(SendTo.ClientsAndHost)] void MatchSoundRpc(int sound)
        { SfxPlayer.Play((GameSfx)sound); }
    }
}
