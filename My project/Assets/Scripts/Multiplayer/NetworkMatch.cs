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
        public double time;
        public bool live, released;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter { serializer.SerializeValue(ref position); serializer.SerializeValue(ref velocity); serializer.SerializeValue(ref live); serializer.SerializeValue(ref released); serializer.SerializeValue(ref time); }
        public bool Equals(BallFrame o)=>position==o.position&&velocity==o.velocity&&live==o.live&&released==o.released&&time==o.time;
    }
    public sealed partial class NetworkMatch : NetworkBehaviour
    {
        public static NetworkMatch Instance { get; private set; }
        public NetworkVariable<FixedString4096Bytes> RosterJson=new NetworkVariable<FixedString4096Bytes>();
        public NetworkVariable<FixedString512Bytes> Status=new NetworkVariable<FixedString512Bytes>();
        public NetworkVariable<int> ScoreA=new NetworkVariable<int>(),ScoreB=new NetworkVariable<int>(),ServerTeam=new NetworkVariable<int>(),ServerIndex=new NetworkVariable<int>(),ServerNumber=new NetworkVariable<int>();
        public NetworkVariable<int> GamesA=new NetworkVariable<int>(),GamesB=new NetworkVariable<int>();
        public NetworkVariable<int> LastHitTeam=new NetworkVariable<int>(-1),Bounces=new NetworkVariable<int>(),TimeoutTeam=new NetworkVariable<int>(-1);
        public NetworkVariable<float> ServeElapsed=new NetworkVariable<float>(),TimeoutRemaining=new NetworkVariable<float>();
        public NetworkVariable<bool> TimedOut=new NetworkVariable<bool>();
        public NetworkVariable<bool> EndsSwapped=new NetworkVariable<bool>(),BetweenGames=new NetworkVariable<bool>(),BetweenPoints=new NetworkVariable<bool>();
        public NetworkVariable<bool> Paused=new NetworkVariable<bool>(),Finished=new NetworkVariable<bool>(),WaitingServe=new NetworkVariable<bool>(),Started=new NetworkVariable<bool>();
        public NetworkVariable<BallFrame> Ball=new NetworkVariable<BallFrame>();
        public NetworkVariable<double> IntroStart=new NetworkVariable<double>(-1);
        public NetworkVariable<bool> CourtPrepared=new NetworkVariable<bool>();
        public NetworkVariable<int> WinningTeam=new NetworkVariable<int>(-1);
        public PickleballRules Rules { get; private set; }
        public Rigidbody Body { get; private set; }
        public MatchRoster Roster { get; private set; }
        readonly Dictionary<string,double> reservations=new Dictionary<string,double>();
        readonly Dictionary<string,NetworkPlayer> players=new Dictionary<string,NetworkPlayer>();
        readonly HashSet<ulong> courtReady=new HashSet<ulong>();
        readonly HashSet<ulong> courtRevealed=new HashSet<ulong>();
        double serveDeadline,rallyDeadline;
        Vector3 savedVelocity;
        bool rallyEnding, rallyCommitted;
        MatchRoster initialRoster;
        string shotLabel=""; double shotLabelUntil;
        readonly IGameplayShotService shots = new BotMatchGameplayService();
        Vector3 ballPosition, ballVelocity, serveOrigin;
        BallFrame confirmedContact;
        TrailRenderer ballTrail;
        Transform landingMarker, aimMarker;
        float accumulator;
        bool releasedServe;
        double nextGameDeadline;
        double timeoutDeadline;
        int countdownSecond=-1;
        public int PlayerEnd(int team) => (team==0 ? -1 : 1) * (EndsSwapped.Value ? -1 : 1);
        public void RequestNextGame() { if(IsServer) NextGame(); }
        void NextGame()
        { if(!BetweenGames.Value || Finished.Value) return; Rules.NextGame(); EndsSwapped.Value=Rules.ends[0]>0; ReplayEventRpc("EndChange","");BetweenGames.Value=false; BeginRally(); }

        public override void OnNetworkSpawn()
        {
            Instance=this;
            if(IsServer) RosterJson.Value=new FixedString4096Bytes(JsonConvert.SerializeObject((initialRoster??MultiplayerApp.Instance.Roster).WithEnvironment(BotMatchLaunchSettings.Night,(CourtVenue)UnityEngine.Random.Range(0,2))));
            Roster=JsonConvert.DeserializeObject<MatchRoster>(RosterJson.Value.ToString());
            ApplyEnvironment();
            BuildBall(); MultiplayerApp.Instance.CourtLoaded();
            Finished.OnValueChanged+=ResultChanged;
            Started.OnValueChanged+=StartChanged;
            BindProgression();
            if(IsServer)
            {
                Rules=new PickleballRules(Roster.mode, Roster.members.Count(p=>p.team==0), Roster.members.Count(p=>p.team==1), UnityEngine.Random.Range(0,2));
                SpawnPlayers(); NetworkManager.OnClientConnectedCallback+=PlayerConnected;
                Body.isKinematic=true;
            }
            else StartCoroutine(ClientCamera());
        }
        public override void OnNetworkDespawn()
        {
            replay?.Finish(false);
            SnapshotPendingProgression();
            UnbindProgression();
            if(Instance==this) Instance=null; Finished.OnValueChanged-=ResultChanged; Started.OnValueChanged-=StartChanged;
            if(NetworkManager!=null) NetworkManager.OnClientConnectedCallback-=PlayerConnected;
            if(Body!=null) Destroy(Body.gameObject);
            if(landingMarker) Destroy(landingMarker.gameObject);
            if(aimMarker) Destroy(aimMarker.gameObject);
        }
        public void SetRoster(MatchRoster roster) { initialRoster=roster; }
        void ApplyEnvironment()
        {
            if(Roster==null)return;
            var venue=UnityEngine.Object.FindAnyObjectByType<CourtVenueSelection>();
            if(venue)venue.Select(Roster.venue);
            var environment=UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
            if(environment&&Roster!=null)environment.SelectForMatch(Roster.night);
        }
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
            if(settings.landingMarkerPrefab) { landingMarker=Instantiate(settings.landingMarkerPrefab).transform; landingMarker.gameObject.SetActive(false); }
            if(settings.aimMarkerPrefab) { aimMarker=Instantiate(settings.aimMarkerPrefab).transform; aimMarker.gameObject.SetActive(false); }
        }
        void SpawnPlayers()
        {
            var app=MultiplayerApp.Instance;
            foreach(var m in Roster.members)
            {
                int index=Roster.members.Where(p=>p.team==m.team).ToList().FindIndex(p=>p.id==m.id);
                var go=Instantiate(app.Settings.playerPrefab);
                var player=go.GetComponent<NetworkPlayer>(); player.Initialize(m,index,StartingPosition(m.team,index));
                var networkObject=go.GetComponent<NetworkObject>(); networkObject.DontDestroyWithOwner=true;
                if(m.bot) { networkObject.Spawn(true); bots[m.id]=new MultiplayerBot(m.id.GetHashCode()); }
                else networkObject.SpawnAsPlayerObject(app.ClientIdentities.First(p=>p.Value==m.id).Key,true);
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
            while(NetworkPlayer.All.Count<Roster.Count || NetworkPlayer.All.All(p=>!p.IsLocalHuman)) yield return null;
            var player=NetworkPlayer.All.First(p=>p.IsLocalHuman);
            var camera=Camera.main;
            if(camera) { var follow=camera.GetComponent<CourtCamera>(); if(!follow) follow=camera.gameObject.AddComponent<CourtCamera>(); follow.target=player.transform; follow.SnapToPlayer(); }
            if(Environment.GetCommandLineArgs().Contains("-pkvSlowCourt")) yield return new WaitForSecondsRealtime(3);
            yield return PickleVerse.Settings.SceneLoading.PrepareNetworkCourt(()=>MultiplayerApp.Instance&&MultiplayerApp.Instance.CourtSceneSynchronized);
            if(!IsSpawned||!PickleVerse.Settings.SceneLoading.IsExternal)yield break;
            CourtReadyRpc();
            while(IsSpawned&&!CourtPrepared.Value&&!Started.Value)yield return null;
            if(IsSpawned)PickleVerse.Settings.SceneLoading.CompleteExternal();
            while(IsSpawned&&PickleVerse.Settings.SceneLoading.IsBlocking)yield return null;
            if(!IsSpawned)yield break;
            CourtRevealedRpc();
            if(Started.Value) StartChanged(false,true);
            if(Finished.Value) ResultChanged(false,true);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void CourtReadyRpc(RpcParams rpc=default)
        {
            if(Started.Value || !MultiplayerApp.Instance.ClientIdentities.ContainsKey(rpc.Receive.SenderClientId)) return;
            courtReady.Add(rpc.Receive.SenderClientId);
            if(courtReady.Count==Roster.HumanCount)CourtPrepared.Value=true;
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void CourtRevealedRpc(RpcParams rpc=default)
        {
            if(Started.Value||!CourtPrepared.Value||!MultiplayerApp.Instance.ClientIdentities.ContainsKey(rpc.Receive.SenderClientId))return;
            courtRevealed.Add(rpc.Receive.SenderClientId);
            // Every machine has finished its 100% frame/fade before the shared clock begins.
            if(courtRevealed.Count==Roster.HumanCount&&IntroStart.Value<0)IntroStart.Value=NetworkManager.ServerTime.Time+.25;
        }
        void StartChanged(bool old,bool started) { if(started) { BeginLocalProgression(); MultiplayerApp.Instance.MatchStarted(); } }
        public void TrySwing(NetworkPlayer player, ShotIntent intent)
        {
            if (!IsServer || !Started.Value || Paused.Value || Finished.Value || BetweenGames.Value || TimedOut.Value || !player.Connected || player.Actor.Swinging) return;
            if (intent.attack && intent.crossCourt) return;
            bool serve = Rules.waitingServe && player.Team.Value == Rules.servingTeam && player.Index.Value == Rules.serverIndex && intent.kind == ShotKind.Groundstroke;
            if (Rules.waitingServe && !serve || !Rules.live && !serve) return;
            if (serve) intent.kind = ShotKind.Serve;
            var local = BotMatchGameplayService.ToLocal(intent);
            float delay = PaddleContact.Anticipation(player.Actor,local,ballPosition,ballVelocity,out var predicted);
            player.BeginSwing(intent, predicted,delay);
            if (serve) releasedServe = false;
        }
        void BeginRally()
        {
            ReplayEventRpc("NewRally","");
            Rules.NewRally(); rallyEnding = rallyCommitted = false; BetweenPoints.Value=false; releasedServe = false;
            progressStrokes.NewRally();
            foreach(var brain in bots.Values) brain.Reset();
            countdownSecond=-1;
            foreach (var player in players.Values) { player.Teleport(StartingPosition(player.Team.Value, player.Index.Value)); player.ResetInput(); }
            var server = players.Values.First(p => p.Team.Value == Rules.servingTeam && p.Index.Value == Rules.serverIndex);
            server.Actor.RefreshPose();
            ballPosition = server.Actor.HeldBallPosition; ballVelocity = Vector3.zero; SetBallPosition(ballPosition);
            if (ballTrail) { ballTrail.emitting=false; ballTrail.Clear(); }
            serveDeadline = Time.realtimeSinceStartupAsDouble + 10; Publish();
        }
        void FixedUpdate()
        {
            if (!IsSpawned || Body == null) return;
            if(!Started.Value)
            {
                if(IsServer && IntroStart.Value>0 && NetworkManager.ServerTime.Time>=IntroStart.Value+MultiplayerCinematic.IntroDuration)
                { BeginRally(); Started.Value=true; }
                return;
            }
            if (!IsServer) return; // Client ball presentation runs at render rate in LateUpdate.
            if (Paused.Value)
            {
                foreach (var r in reservations.ToArray()) if (Time.realtimeSinceStartupAsDouble > r.Value)
                { forfeitedPlayers.Add(r.Key); Rules.Forfeit(Roster.Find(r.Key).team, "Mất kết nối quá 30 giây"); reservations.Clear(); Finish(); break; }
                return;
            }
            accumulator += Time.fixedDeltaTime;
            const float tick = 1f / 120f;
            while (accumulator >= tick) { StepSimulation(tick); accumulator -= tick; }
            SetBallPosition(ballPosition);
            Ball.Value = new BallFrame { position = ballPosition, velocity = ballVelocity, live = Rules.live, released = releasedServe, time = NetworkManager.ServerTime.Time };
            Publish();
        }
        void StepSimulation(float dt)
        {
            if (Finished.Value) return;
            foreach (var player in players.Values)
            {
                if(bots.TryGetValue(player.Identity.Value.ToString(),out var brain))
                { brain.Step(this,player,dt,out var move,out var run); player.SetAutomatedInput(move,run); }
                player.Simulate(dt, !Finished.Value && !BetweenGames.Value && !rallyCommitted && !TimedOut.Value);
            }
            if (Finished.Value) return;
            if (TimedOut.Value) { if(Time.realtimeSinceStartupAsDouble>=timeoutDeadline) EndTimeout(); return; }
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
                var previousServePosition=ballPosition;
                if(BotMissedContact(server))
                {
                    // The released ball keeps falling after a whiff; never reattach it for a retry.
                    BallFlight.Integrate(ref ballPosition,ref ballVelocity,dt);
                    if(ballPosition.y<=Court.BallRadius)
                    { ballPosition.y=Court.BallRadius; Rules.Fault(server.Team.Value,"Giao bóng hụt"); }
                }
                else ServeBallMotion.Step(actor, ref releasedServe, ref serveOrigin, ref ballPosition, ref ballVelocity,dt);
                if (Rules.waitingServe && actor.Swinging && actor.Intent.kind == PickleVerse.LocalMatch.ShotKind.Serve)
                {
                    if (actor.SwingTime >= .8f && actor.CanContact(previousServePosition,ballPosition,MultiplayerApp.Instance.Settings.contactAssistRadius,out _))
                    {
                        var left = actor.Feet(true); var right = actor.Feet(false);
                        if (TryBotContact(server) && Rules.Serve(server.Team.Value, server.Index.Value, server.transform.position, ballPosition,
                            new Vector3(0, actor.PaddleHeadTop, 0), actor.hand.position, actor.PaddleVelocity,
                            Court.LegalServeFeet(left, right, actor.end, Rules.RightServe ? 0 : 1)))
                        {
                            progressStrokes.Launch(server.Team.Value*2+server.Index.Value,true,Rules.strokeCount);
                            ballVelocity = LaunchBall(server,actor.Intent);
                            server.ConsumeContact(false,ballPosition,true,ballVelocity);
                            ReplayEventRpc("Serve",server.Identity.Value.ToString());
                        }
                    }
                }
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
                    if(!TryBotContact(player)) continue;
                    ballPosition=actor.ContactPoint;
                    bool volley = Rules.bounceCount == 0;
                    bool hit = Rules.Hit(player.Team.Value, player.Index.Value, actor.transform.position,
                        ballPosition, BotMatchGameplayService.FromLocal(shot).kind, actor.InKitchen);
                    if (hit)
                    {
                        progressStrokes.AcceptFlight();
                        progressStrokes.Launch(player.Team.Value*2+player.Index.Value,false,Rules.strokeCount);
                        ballVelocity = LaunchBall(player,shot);
                        player.ConsumeContact(volley,ballPosition,true,ballVelocity);
                        ReplayEventRpc("Hit",player.Identity.Value+":"+shot.kind);
                        foreach (var other in players.Values.Where(p => p.Team.Value != player.Team.Value)) other.SplitStep();
                        shotLabel = shot.ToString(); shotLabelUntil = Time.realtimeSinceStartupAsDouble + 1;
                    }
                    else player.ConsumeContact(volley,ballPosition,false);
                }
                if (Rules.live && ballPosition.y <= Court.BallRadius)
                {
                    var bounce = ballPosition; bounce.y = Court.BallRadius; Rules.Bounce(bounce);
                    if (Rules.live) { progressStrokes.AcceptFlight(); BallFlight.Rebound(ref ballPosition, ref ballVelocity); }
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
            { rallyEnding = true; ballVelocity = Vector3.zero;MatchSoundRpc((int)GameSfx.FaultWhistle);ReplayEventRpc("Fault",Rules.reason); }
            if (rallyEnding && !Array.Exists(Rules.volleyMomentum, v => v))
            {
                int loser = Rules.pendingFault, endedGame = Rules.GameNumber, priorServer = Rules.servingTeam;
                string fault = Rules.reason;
                int beforeA=Rules.score[0], beforeB=Rules.score[1];
                Rules.SettleRally();
                if(Rules.gameFinished)ReplayEventRpc("GameEnd",Rules.score[0]+":"+Rules.score[1]);
                teamPoints[0]+=Math.Max(0,Rules.score[0]-beforeA); teamPoints[1]+=Math.Max(0,Rules.score[1]-beforeB);
                if(Rules.gameFinished) CaptureProgressionGame();
                rallyCommitted=true; BetweenPoints.Value=true; rallyDeadline=Time.realtimeSinceStartupAsDouble+2.1;
                AnnouncementRpc(fault.Contains("ngoài sân")?"OUT":"FAULT / LỖI",fault+" • Đội "+(loser==0?"A":"B")+" phạm lỗi\n"+(priorServer==1-loser?"Đội "+(priorServer==0?"A":"B")+" ghi điểm":"Đổi giao bóng / Side Out"),(int)AnnouncementTone.Fault,1.2f);
                foreach (var player in players.Values) player.React(player.Team.Value != loser, Rules.complete);
                if (Rules.complete) Finish();
                else if (Rules.gameFinished)
                {
                    MatchSoundRpc((int)GameSfx.GameWhistle);BetweenGames.Value=true; nextGameDeadline=Time.realtimeSinceStartupAsDouble+4.2;
                    AnnouncementRpc("GAME "+endedGame+" KẾT THÚC","Số game thắng "+Rules.games[0]+" - "+Rules.games[1],(int)AnnouncementTone.Neutral,1.6f);
                    AnnouncementRpc("ĐỔI BÊN","CHANGE ENDS • Chuẩn bị game "+(endedGame+1),(int)AnnouncementTone.ChangeEnds,1.4f);
                }
                else if(Rules.endChangeDue)
                {
                    MatchSoundRpc((int)GameSfx.GameWhistle);Rules.SwapEnds(); EndsSwapped.Value=Rules.ends[0]>0;ReplayEventRpc("EndChange","");
                    rallyDeadline=Time.realtimeSinceStartupAsDouble+2.6;
                    AnnouncementRpc("ĐỔI BÊN","CHANGE ENDS • Mốc 6 điểm trong game quyết định",(int)AnnouncementTone.ChangeEnds,1.4f);
                }
            }
        }
        void Publish()
        {
            ScoreA.Value=Rules.score[0]; ScoreB.Value=Rules.score[1]; ServerTeam.Value=Rules.servingTeam; ServerIndex.Value=Rules.serverIndex; ServerNumber.Value=Rules.serverNumber; WaitingServe.Value=Rules.waitingServe;
            GamesA.Value=Rules.games[0]; GamesB.Value=Rules.games[1];
            LastHitTeam.Value=Rules.lastHitTeam; Bounces.Value=Rules.bounceCount;
            ServeElapsed.Value=Rules.waitingServe && !TimedOut.Value ? Mathf.Clamp((float)(10-(serveDeadline-Time.realtimeSinceStartupAsDouble)),0,10) : 0;
            TimeoutRemaining.Value=TimedOut.Value ? Mathf.Max(0,(float)(timeoutDeadline-Time.realtimeSinceStartupAsDouble)) : 0;
            if (!Finished.Value)
                Status.Value=new FixedString512Bytes(TimedOut.Value?"Time-out • đội "+(TimeoutTeam.Value==0?"A":"B")+" • tối đa 60 giây":BetweenGames.Value?"Hết game • đổi sân, tự tiếp tục game kế tiếp.":Rules.live && Time.realtimeSinceStartupAsDouble<shotLabelUntil?shotLabel:Rules.reason);
        }
        void LateUpdate()
        {
            EnsureReplay();
            if(!IsSpawned || !Body || !Started.Value)return;
            var frame=Ball.Value;
            if (!IsServer && !Paused.Value && !Finished.Value)
            {
                // A reliable contact may arrive before the next ball snapshot. Never interpolate
                // backwards through the paddle using a snapshot from before that contact.
                if (confirmedContact.time > frame.time) frame=confirmedContact;
                float age=frame.live||frame.released?(float)Math.Max(0,Math.Min(.08,NetworkManager.ServerTime.Time-frame.time)):0;
                Vector3 p=frame.position,v=frame.velocity;BallFlight.Integrate(ref p,ref v,age);
                p.y=Mathf.Max(Court.BallRadius,p.y);SetBallPosition(p);
            }
            var server=NetworkPlayer.All.FirstOrDefault(p=>p.Team.Value==ServerTeam.Value && p.Index.Value==ServerIndex.Value);
            if(!IsServer && WaitingServe.Value && !frame.released && server && server.Actor)SetBallPosition(server.Actor.HeldBallPosition);
            bool flying=(frame.live || WaitingServe.Value && frame.released) && Body.position.y>Court.BallRadius+.02f && frame.velocity.sqrMagnitude>.04f;
            if(ballTrail) {ballTrail.emitting=flying && !Paused.Value && !Finished.Value;if(!ballTrail.emitting)ballTrail.Clear();}
            if(flying && !Paused.Value)Body.transform.Rotate(new Vector3(80,140,40)*Time.deltaTime);
            var local=NetworkPlayer.All.FirstOrDefault(p=>p.IsLocalHuman);
            if(landingMarker)
            {
                bool visible=frame.live && local && local.Team.Value!=LastHitTeam.Value && !Finished.Value;
                landingMarker.gameObject.SetActive(visible);
                if(visible) {var p=frame.position;var v=frame.velocity;for(int i=0;i<300 && p.y>Court.BallRadius;i++)BallFlight.Integrate(ref p,ref v,.01f);landingMarker.position=new Vector3(p.x,.018f,p.z);}
            }
            if(aimMarker)
            {
                bool visible=local && local.Actor && local.Actor.Swinging && !Finished.Value;
                aimMarker.gameObject.SetActive(visible);
                if(visible) {var p=shots.Target(local.Actor,local.Actor.Intent,frame.position,Mathf.Sign(server?server.transform.position.x:1));p.y=.022f;aimMarker.position=p;}
            }
        }
        public void PresentContact(Vector3 position, Vector3 velocity, double time)
        {
            if (IsServer) return;
            confirmedContact=new BallFrame {position=position,velocity=velocity,time=time,live=true,released=true};
            if(Body)SetBallPosition(position);
        }
        void SetBallPosition(Vector3 position)
        {
            // The ball is simulated with BallFlight, not PhysX. Publish both poses
            // immediately so a reset/rotation cannot restore a stale Rigidbody pose.
            Body.position=position;Body.transform.position=position;
        }
        public void RequestTimeout(NetworkPlayer player)
        {
            if(!IsServer || !Started.Value || Paused.Value || TimedOut.Value || BetweenGames.Value || BetweenPoints.Value || Finished.Value || !player.Connected || players.Values.Any(p=>p.Actor.Swinging) || !Rules.TakeTimeout(player.Team.Value))return;
            TimedOut.Value=true;TimeoutTeam.Value=player.Team.Value;timeoutDeadline=Time.realtimeSinceStartupAsDouble+60;
            foreach(var p in players.Values)p.ResetInput();
            SfxPlayer.Stop(GameSfx.Countdown);
            AnnouncementRpc("TIME-OUT","Đội "+(player.Team.Value==0?"A":"B")+" • tối đa 60 giây • Enter để tiếp tục",(int)AnnouncementTone.Neutral,1.4f);
            Publish();
        }
        public void ResumeTimeout(NetworkPlayer player)
        {if(IsServer && TimedOut.Value && !Paused.Value && player.Connected && player.Team.Value==TimeoutTeam.Value)EndTimeout();}
        void EndTimeout()
        {TimedOut.Value=false;TimeoutTeam.Value=-1;serveDeadline=Time.realtimeSinceStartupAsDouble+10;countdownSecond=-1;Publish();}
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
        public void RequestLeave() { RecordLocalExit(true,"Chủ động rời trận"); LeaveRpc(); }
        public void PlayerLeftSession(string id)
        {
            var player=Roster?.Find(id);
            if(IsServer && !Finished.Value && player!=null)
            { forfeitedPlayers.Add(id); Rules.Forfeit(player.team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void LeaveRpc(RpcParams rpc=default)
        {
            if(Finished.Value) return;
            if(MultiplayerApp.Instance.ClientIdentities.TryGetValue(rpc.Receive.SenderClientId,out var id))
            { forfeitedPlayers.Add(id); Rules.Forfeit(Roster.Find(id).team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        void Finish()
        {
            if(!Finished.Value)MatchSoundRpc((int)GameSfx.GameWhistle);
            Body.isKinematic=true; Publish();
            PublishProgressionResults();
            WinningTeam.Value=Rules.winner;
            Status.Value=new FixedString512Bytes($"Đội {(Rules.winner==0?"A":"B")} thắng trận {Rules.games[0]} : {Rules.games[1]} game — điểm game cuối {Rules.score[0]} : {Rules.score[1]}\n{Rules.reason}"); Finished.Value=true;
            AnnouncementRpc("KẾT THÚC TRẬN ĐẤU",Status.Value.ToString(),(int)AnnouncementTone.Success,2.1f);
        }
        void ResultChanged(bool old,bool complete)
        {
            if(!complete)return;
            replay?.Event("MatchEnd",Status.Value.ToString());replay?.Finish(true);
            MultiplayerApp.Instance.ShowResult(Status.Value.ToString());
            MultiplayerCourtUI.Instance?.EnsureEndAnnouncement(Status.Value.ToString());
        }
        [Rpc(SendTo.ClientsAndHost)] void MatchSoundRpc(int sound)
        { SfxPlayer.Play((GameSfx)sound); }
        [Rpc(SendTo.ClientsAndHost)] void AnnouncementRpc(string title,string detail,int tone,float duration)
        {var ui=MultiplayerCourtUI.Instance;if(ui)ui.ShowAnnouncement(title,detail,duration,(AnnouncementTone)tone);}
    }
}
