using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Fusion;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

using UnityEngine;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using PickleVerse.Audio;

namespace PickleVerse.Multiplayer
{
    [Serializable]
    public struct BallFrame : INetworkStruct, IEquatable<BallFrame>
    {
        public Vector3 position, velocity;
        public double time;
        public ushort motionEpoch;
        public NetworkBool live, released;
        public bool Equals(BallFrame o) => position == o.position && velocity == o.velocity && motionEpoch == o.motionEpoch && (bool)live == (bool)o.live && (bool)released == (bool)o.released && time == o.time;
    }
    public sealed partial class NetworkMatch : NetworkBehaviour, IPhotonMessageTarget
    {
        public static NetworkMatch Instance { get; private set; }
        [Networked]
        [Capacity(2048)]
        public string RosterJson { get; set; }
        [Networked]
        [Capacity(512)]
        public string Status { get; set; }
        [Networked]
        public int ScoreA { get; set; }
        [Networked]
        public int ScoreB { get; set; }
        [Networked]
        public int ServerTeam { get; set; }
        [Networked]
        public int ServerIndex { get; set; }
        [Networked]
        public int ServerNumber { get; set; }
        [Networked]
        public int GamesA { get; set; }
        [Networked]
        public int GamesB { get; set; }
        [Networked]
        public int LastHitTeam { get; set; } = -1;
        [Networked]
        public int Bounces { get; set; }
        [Networked]
        public int TimeoutTeam { get; set; } = -1;
        [Networked]
        public int WinningTeam { get; set; } = -1;
        [Networked]
        public float ServeElapsed { get; set; }
        [Networked]
        public float TimeoutRemaining { get; set; }
        [Networked]
        public bool TimedOut { get; set; }
        [Networked]
        public bool EndsSwapped { get; set; }
        [Networked]
        public bool BetweenGames { get; set; }
        [Networked]
        public bool BetweenPoints { get; set; }
        [Networked]
        public bool Paused { get; set; }
        [Networked]
        public bool Finished { get; set; }
        [Networked]
        public bool WaitingServe { get; set; }
        [Networked]
        public bool Started { get; set; }
        [Networked]
        public bool CourtPrepared { get; set; }
        [Networked]
        public BallFrame Ball { get; set; }
        [Networked]
        public double IntroStart { get; set; } = -1;
        public bool IsServer => HasStateAuthority;
        public bool IsSpawned => Object && Object.IsValid;
        bool renderedFinished, renderedStarted;
        public PickleballRules Rules { get; private set; }
        public Rigidbody Body { get; private set; }
        public MatchRoster Roster { get; private set; }
        readonly Dictionary<string, double> reservations = new Dictionary<string, double>();
        readonly Dictionary<string, NetworkPlayer> players = new Dictionary<string, NetworkPlayer>();
        readonly HashSet<ulong> courtReady = new HashSet<ulong>();
        readonly HashSet<ulong> courtRevealed = new HashSet<ulong>();
        double serveDeadline, rallyDeadline;
        Vector3 savedVelocity;
        bool rallyEnding, rallyCommitted;
        MatchRoster initialRoster;
        string shotLabel = ""; double shotLabelUntil;
        readonly IGameplayShotService shots = new BotMatchGameplayService();
        Vector3 ballPosition, ballVelocity, serveOrigin;
        BallFrame confirmedContact;
        TrailRenderer ballTrail;
        Transform landingMarker, aimMarker;
        float accumulator;
        bool releasedServe;
        ushort ballMotionEpoch;
        double nextGameDeadline;
        double timeoutDeadline;
        int countdownSecond = -1;
        public int PlayerEnd(int team) => (team == 0 ? -1 : 1) * (EndsSwapped ? -1 : 1);
        public void RequestNextGame()
        {
            if (IsServer)
                NextGame();
        }
        void NextGame()
        {
            if (!BetweenGames || Finished)
                return;
            Rules.NextGame();
            EndsSwapped = Rules.ends[0] > 0;
            ReplayEventRpc("EndChange", "");
            BetweenGames = false;
            BeginRally();
        }

        public override void Spawned()
        {
            Instance = this;
            if (IsServer)
                RosterJson = JsonConvert.SerializeObject(initialRoster ?? MultiplayerApp.Instance.Roster);
            Roster = JsonConvert.DeserializeObject<MatchRoster>(RosterJson.ToString());
            ApplyEnvironment();
            BuildBall();
            MultiplayerApp.Instance.CourtLoaded();


            BindProgression();
            if (IsServer)
            {
                Rules = new PickleballRules(Roster.mode, Roster.members.Count(p => p.team == 0), Roster.members.Count(p => p.team == 1), UnityEngine.Random.Range(0, 2));
                SpawnPlayersGuarded();
                MultiplayerApp.Instance.PeerConnected += PlayerConnected;
                Body.isKinematic = true;
            }
            else
            {
                StartCoroutine(ClientCamera());
                if (Finished) Send("request-results");
            }
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            replay?.Finish(false);
            if (hasState)
                SnapshotPendingProgression();
            UnbindProgression();
            if (Instance == this)
                Instance = null;
            if (MultiplayerApp.Instance)
                MultiplayerApp.Instance.PeerConnected -= PlayerConnected;
            if (Body != null)
                Destroy(Body.gameObject);
            if (landingMarker)
                Destroy(landingMarker.gameObject);
            if (aimMarker)
                Destroy(aimMarker.gameObject);
        }
        public void SetRoster(MatchRoster roster)
        {
            initialRoster = roster;
        }
        void ApplyEnvironment()
        {
            if (Roster == null)
                return;
            var venue = UnityEngine.Object.FindAnyObjectByType<CourtVenueSelection>();
            if (venue)
                venue.Select(Roster.venue);
            var environment = UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
            if (environment && Roster != null)
                environment.SelectForMatch(Roster.night);
        }
        void BuildBall()
        {
            var settings = MultiplayerApp.Instance.Settings;
#if UNITY_SERVER
            var ball = new GameObject("Server Ball");
            Body = ball.AddComponent<Rigidbody>(); Body.isKinematic = true; Body.useGravity = false;
            return;
#else
            var ball = settings.ballVisualPrefab ? Instantiate(settings.ballVisualPrefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "NetworkPickleball";
            if (!settings.ballVisualPrefab)
            {
                ball.transform.localScale = Vector3.one * (Court.BallRadius * 2);
                ball.GetComponent<Renderer>().sharedMaterial = settings.ballMaterial;
            }
            foreach (var collider in ball.GetComponentsInChildren<Collider>())
                collider.enabled = false;
            Body = ball.GetComponent<Rigidbody>();
            if (!Body)
                Body = ball.AddComponent<Rigidbody>();
            Body.mass = .026f;
            Body.isKinematic = true;
            Body.useGravity = false;
            ballTrail = ball.GetComponent<TrailRenderer>();
            if (!ballTrail)
            {
                ballTrail = ball.AddComponent<TrailRenderer>();
                ballTrail.time = .22f;
                ballTrail.startWidth = .025f;
                ballTrail.endWidth = .002f;
                ballTrail.sharedMaterial = settings.trailMaterial;
            }
            ballTrail.emitting = false;
            ballTrail.Clear();
            if (settings.landingMarkerPrefab)
            {
                landingMarker = Instantiate(settings.landingMarkerPrefab).transform;
                landingMarker.gameObject.SetActive(false);
            }
            if (settings.aimMarkerPrefab)
            {
                aimMarker = Instantiate(settings.aimMarkerPrefab).transform;
                aimMarker.gameObject.SetActive(false);
            }
#endif
        }
        async void SpawnPlayersGuarded()
        {
            try { await SpawnPlayers(); }
            catch (Exception error)
            {
                Debug.LogError("[Court] Không tải được đội hình: " + error.GetType().Name + ": " + error.Message + "\n" + error.StackTrace);
#if UNITY_SERVER
                DedicatedWorker.Instance?.Abort();
#endif
            }
        }
        async Task SpawnPlayers()
        {
            var app = MultiplayerApp.Instance;
            foreach (var m in Roster.members)
            {
                int index = Roster.members.Where(p => p.team == m.team).ToList().FindIndex(p => p.id == m.id);
                var controller = m.bot ? PlayerRef.None : PlayerRef.FromEncoded((int)app.ClientIdentities.First(p => p.Value == m.id).Key);
                var prefab = app.Settings.PlayerPrefab(app.Match?.IsDedicated == true);
                if (!prefab) throw new InvalidOperationException("Thiếu prefab người chơi cho cấu hình máy chủ.");
                var networkObject = await Runner.SpawnAsync(prefab, inputAuthority: Runner.IsServer ? controller : (PlayerRef?)null, onBeforeSpawned: (runner, obj) =>
                {
                    var created = obj.GetComponent<NetworkPlayer>();
                    created.Initialize(m, index, StartingPosition(m.team, index));
                    created.ControllerPeer = m.bot ? PlayerRef.None : PlayerRef.FromEncoded((int)app.ClientIdentities.First(p => p.Value == m.id).Key);
                });
                if (m.bot)
                    bots[m.id] = new MultiplayerBot(m.id.GetHashCode());
                players[m.id] = networkObject.GetComponent<NetworkPlayer>();
            }
#if !UNITY_SERVER
            StartCoroutine(ClientCamera());
#endif
        }
        Vector3 StartingPosition(int team, int index)
        {
            float facing = Rules.TeamFacing(team);
            if (team == Rules.servingTeam && index == Rules.serverIndex)
                return new Vector3(Mathf.Sign(Rules.serverWorldX) * 1.45f, 0, -facing * 7.25f);
            if (team != Rules.servingTeam && index == Rules.receiverIndex)
                return new Vector3(-Mathf.Sign(Rules.serverWorldX) * 1.45f, 0, -facing * 6.1f);
            int right = Rules.courtRight[team];
            return new Vector3((index == right ? 1 : -1) * facing * 1.5f, 0, -facing * 4.2f);
        }
        IEnumerator ClientCamera()
        {
            while (NetworkPlayer.All.Count < Roster.Count || NetworkPlayer.All.All(p => !p.IsLocalHuman))
                yield return null;
            var player = NetworkPlayer.All.First(p => p.IsLocalHuman);
            var camera = Camera.main;
            if (camera)
            {
                var follow = camera.GetComponent<CourtCamera>();
                if (!follow)
                    follow = camera.gameObject.AddComponent<CourtCamera>();
                follow.target = player.transform;
                follow.SnapToPlayer();
            }
            if (Environment.GetCommandLineArgs().Contains("-pkvSlowCourt"))
                yield return new WaitForSecondsRealtime(3);
            yield return PickleVerse.Settings.SceneLoading.PrepareNetworkCourt(() => MultiplayerApp.Instance && MultiplayerApp.Instance.CourtSceneSynchronized);
            if (!IsSpawned || !PickleVerse.Settings.SceneLoading.IsExternal)
                yield break;
            CourtReadyRpc();
            while (IsSpawned && !CourtPrepared && !Started)
                yield return null;
            if (IsSpawned)
                PickleVerse.Settings.SceneLoading.CompleteExternal();
            while (IsSpawned && PickleVerse.Settings.SceneLoading.IsBlocking)
                yield return null;
            if (!IsSpawned)
                yield break;
            CourtRevealedRpc();
            if (Started)
                StartChanged(false, true);
            if (Finished)
                ResultChanged(false, true);
        }
        void CourtReadyFrom(PlayerRef source)
        {
            if (Started || !MultiplayerApp.Instance.ClientIdentities.ContainsKey((ulong)source.RawEncoded))
                return;
            courtReady.Add((ulong)source.RawEncoded);
            if (courtReady.Count == Roster.HumanCount)
                CourtPrepared = true;
        }
        void CourtRevealedFrom(PlayerRef source)
        {
            if (Started || !CourtPrepared || !MultiplayerApp.Instance.ClientIdentities.ContainsKey((ulong)source.RawEncoded))
                return;
            courtRevealed.Add((ulong)source.RawEncoded);
            // Every machine has finished its 100% frame/fade before the shared clock begins.
            if (courtRevealed.Count == Roster.HumanCount && IntroStart < 0)
                IntroStart = Runner.SimulationTime + .25;
        }
        void StartChanged(bool old, bool started)
        {
            if (started)
            {
                BeginLocalProgression();
                MultiplayerApp.Instance.MatchStarted();
            }
        }
        public void TrySwing(NetworkPlayer player, ShotIntent intent)
        {
            if (!IsServer || !Started || Paused || Finished || BetweenGames || TimedOut || !player.Connected || player.Actor.Swinging)
                return;
            if (intent.attack && intent.crossCourt)
                return;
            bool serve = Rules.waitingServe && player.Team == Rules.servingTeam && player.Index == Rules.serverIndex && intent.kind == ShotKind.Groundstroke;
            if (Rules.waitingServe && !serve || !Rules.live && !serve)
                return;
            if (serve)
                intent.kind = ShotKind.Serve;
            var local = BotMatchGameplayService.ToLocal(intent);
            float delay = PaddleContact.Anticipation(player.Actor, local, ballPosition, ballVelocity, out var predicted);
            player.BeginSwing(intent, predicted, delay);
            if (player.Actor.Swinging) CoachingAttempt(player, local.kind.ToString());
            if (serve)
                releasedServe = false;
        }
        void BeginRally()
        {
            ballMotionEpoch++;
            confirmedContact = default;
            CoachingNewRally();
            ReplayEventRpc("NewRally", "");
            Rules.NewRally();
            rallyEnding = rallyCommitted = false;
            BetweenPoints = false;
            releasedServe = false;
            progressStrokes.NewRally();
            foreach (var brain in bots.Values)
                brain.Reset();
            countdownSecond = -1;
            foreach (var player in players.Values)
            {
                player.Teleport(StartingPosition(player.Team, player.Index));
                player.ResetInput();
            }
            var server = players.Values.First(p => p.Team == Rules.servingTeam && p.Index == Rules.serverIndex);
            server.Actor.RefreshPose();
            ballPosition = server.Actor.HeldBallPosition;
            ballVelocity = Vector3.zero;
            SetBallPosition(ballPosition);
            if (ballTrail)
            {
                ballTrail.emitting = false;
                ballTrail.Clear();
            }
            serveDeadline = Time.realtimeSinceStartupAsDouble + 10;
            Publish();
        }
        public override void FixedUpdateNetwork()
        {
            if (!IsSpawned || Body == null || IsServer && Rules == null)
                return;
            if (!Started)
            {
                if (IsServer && IntroStart > 0 && Runner.SimulationTime >= IntroStart + MultiplayerCinematic.IntroDuration)
                {
                    BeginRally();
                    Started = true;
                }
                return;
            }
            if (!IsServer)
                return; // Client ball presentation runs at render rate in LateUpdate.
            if (Paused)
            {
                foreach (var r in reservations.ToArray())
                    if (Time.realtimeSinceStartupAsDouble > r.Value)
                    {
                        forfeitedPlayers.Add(r.Key);
                        Rules.Forfeit(Roster.Find(r.Key).team, "Mất kết nối quá 30 giây");
                        reservations.Clear();
                        Finish();
                        break;
                    }
                return;
            }
            accumulator += Runner.DeltaTime;
            const float tick = 1f / 120f;
            while (accumulator >= tick)
            {
                StepSimulation(tick);
                accumulator -= tick;
            }
            SetBallPosition(ballPosition);
            Ball = new BallFrame { position = ballPosition, velocity = ballVelocity, live = Rules.live, released = releasedServe, time = Runner.SimulationTime, motionEpoch = ballMotionEpoch };
            Publish();
        }
        void StepSimulation(float dt)
        {
            if (Finished)
                return;
            foreach (var player in players.Values)
            {
                if (bots.TryGetValue(player.Identity.ToString(), out var brain))
                {
                    brain.Step(this, player, dt, out var move, out var run);
                    player.SetAutomatedInput(move, run);
                }
                player.Simulate(dt, !Finished && !BetweenGames && !rallyCommitted && !TimedOut);
            }
            if (Finished)
                return;
            if (TimedOut)
            {
                if (Time.realtimeSinceStartupAsDouble >= timeoutDeadline)
                    EndTimeout();
                return;
            }
            if (BetweenGames)
            {
                if (Time.realtimeSinceStartupAsDouble >= nextGameDeadline)
                    NextGame();
                return;
            }
            if (rallyCommitted)
            {
                if (Time.realtimeSinceStartupAsDouble >= rallyDeadline)
                    BeginRally();
                return;
            }
            foreach (var player in players.Values)
            {
                Rules.CheckActorMomentum(player.Team, player.Index, player.Actor.VolleyMomentum, player.Actor.InKitchen);
                CoachingMomentum(player);
                if (Rules.live && Mathf.Abs(player.transform.position.z) < .31f)
                    Rules.Fault(player.Team, "Chạm lưới");
            }
            if (Rules.waitingServe)
            {
                int second = Mathf.FloorToInt((float)(10 - (serveDeadline - Time.realtimeSinceStartupAsDouble)) + .0001f);
                if (second >= 0 && second < 10 && second != countdownSecond)
                {
                    countdownSecond = second;
                    MatchSoundRpc((int)GameSfx.Countdown);
                }
                var server = players.Values.First(p => p.Team == Rules.servingTeam && p.Index == Rules.serverIndex);
                var actor = server.Actor;
                var previousServePosition = ballPosition;
                if (BotMissedContact(server))
                {
                    // The released ball keeps falling after a whiff; never reattach it for a retry.
                    BallFlight.Integrate(ref ballPosition, ref ballVelocity, dt);
                    if (ballPosition.y <= Court.BallRadius)
                    {
                        ballPosition.y = Court.BallRadius;
                        Rules.Fault(server.Team, "Giao bóng hụt");
                    }
                }
                else ServeBallMotion.Step(actor, ref releasedServe, ref serveOrigin, ref ballPosition, ref ballVelocity, dt);
                if (Rules.waitingServe && actor.Swinging && actor.Intent.kind == PickleVerse.LocalMatch.ShotKind.Serve)
                {
                    if (actor.SwingTime >= .8f && actor.CanContact(previousServePosition, ballPosition, MultiplayerApp.Instance.Settings.contactAssistRadius, out _))
                    {
                        var left = actor.Feet(true);
                        var right = actor.Feet(false);
                        bool attempted = TryBotContact(server);
                        bool served = attempted && Rules.Serve(server.Team, server.Index, server.transform.position, ballPosition,
                            new Vector3(0, actor.PaddleHeadTop, 0), actor.hand.position, actor.PaddleVelocity,
                            Court.LegalServeFeet(left, right, actor.end, Rules.RightServe ? 0 : 1));
                        if (attempted) CoachingContact(server, false, served);
                        if (served)
                        {
                            progressStrokes.Launch(server.Team * 2 + server.Index, true, Rules.strokeCount);
                            ballVelocity = LaunchBall(server, actor.Intent);
                            server.ConsumeContact(false, ballPosition, true, ballVelocity);
                            ReplayEventRpc("Serve", server.Identity.ToString());
                        }
                    }
                }
                if (Rules.waitingServe && Time.realtimeSinceStartupAsDouble > serveDeadline)
                    Rules.Fault(Rules.servingTeam, "Quá 10 giây giao bóng");
            }
            else if (Rules.live)
            {
                var previous = ballPosition;
                BallFlight.Integrate(ref ballPosition, ref ballVelocity, dt);
                if (previous.z * ballPosition.z < 0 && Mathf.Abs(ballPosition.x) <= Court.NetHalfSpan)
                {
                    var crossing = Vector3.Lerp(previous, ballPosition, previous.z / (previous.z - ballPosition.z));
                    float top = Court.NetHeight(crossing.x);
                    CoachingNet(crossing.y - Court.BallRadius - top);
                    if (crossing.y - Court.BallRadius <= top)
                    {
                        if (crossing.y >= top - .01f)
                        {
                            ballVelocity.z *= .6f;
                            ballVelocity.x *= .6f;
                            ballVelocity.y *= .5f;
                        }
                        else
                        {
                            ballPosition.z = Mathf.Sign(previous.z) * .04f;
                            ballVelocity.z *= -.16f;
                            ballVelocity.x *= .45f;
                            ballVelocity.y *= .35f;
                        }
                    }
                }
                foreach (var player in players.Values)
                {
                    if (!Rules.live || player.Team == Rules.lastHitTeam || !shots.CanContact(player.Actor, previous, ballPosition, Rules.bounceCount, MultiplayerApp.Instance.Settings.contactAssistRadius))
                        continue;
                    var actor = player.Actor;
                    var shot = actor.Intent;
                    if (!TryBotContact(player))
                        continue;
                    ballPosition = actor.ContactPoint;
                    bool volley = Rules.bounceCount == 0;
                    bool hit = Rules.Hit(player.Team, player.Index, actor.transform.position,
                        ballPosition, BotMatchGameplayService.FromLocal(shot).kind, actor.InKitchen);
                    CoachingContact(player, volley, hit);
                    if (hit)
                    {
                        progressStrokes.AcceptFlight();
                        progressStrokes.Launch(player.Team * 2 + player.Index, false, Rules.strokeCount);
                        ballVelocity = LaunchBall(player, shot);
                        player.ConsumeContact(volley, ballPosition, true, ballVelocity);
                        ReplayEventRpc("Hit", player.Identity + ":" + shot.kind);
                        foreach (var other in players.Values.Where(p => p.Team != player.Team))
                            other.SplitStep();
                        shotLabel = shot.ToString();
                        shotLabelUntil = Time.realtimeSinceStartupAsDouble + 1;
                    }
                    else player.ConsumeContact(volley, ballPosition, false);
                }
                if (Rules.live && ballPosition.y <= Court.BallRadius)
                {
                    var bounce = ballPosition;
                    bounce.y = Court.BallRadius;
                    CoachingLanding(bounce);
                    Rules.Bounce(bounce);
                    if (Rules.live)
                    {
                        progressStrokes.AcceptFlight();
                        BallFlight.Rebound(ref ballPosition, ref ballVelocity);
                    }
                }
                if (Rules.live)
                {
                    foreach (var player in players.Values.Where(p => p.Team != Rules.lastHitTeam))
                        if (ballPosition.y > .25f && ballPosition.y < 1.65f && BotMatchGameplayService.SegmentDistance(previous, ballPosition, player.transform.position + Vector3.up * .85f) < .24f)
                        {
                            Rules.BodyContact(player.Team);
                            break;
                        }
                    if (Mathf.Abs(ballPosition.x) > 7 || Mathf.Abs(ballPosition.z) > 12)
                        Rules.Fault(Rules.bounceCount > 0 ? 1 - Rules.lastHitTeam : Rules.lastHitTeam, "Bóng ngoài sân");
                }
            }
            if (!Rules.live && !Rules.waitingServe && !rallyEnding)
            {
                rallyEnding = true;
                ballVelocity = Vector3.zero;
                MatchSoundRpc((int)GameSfx.FaultWhistle);
                ReplayEventRpc("Fault", Rules.reason);
            }
            if (rallyEnding && !Array.Exists(Rules.volleyMomentum, v => v))
            {
                int loser = Rules.pendingFault, endedGame = Rules.GameNumber, priorServer = Rules.servingTeam;
                string fault = Rules.reason;
                CoachingRallyEnded(loser, fault);
                int beforeA = Rules.score[0], beforeB = Rules.score[1];
                Rules.SettleRally();
                if (Rules.gameFinished)
                    ReplayEventRpc("GameEnd", Rules.score[0] + ":" + Rules.score[1]);
                teamPoints[0] += Math.Max(0, Rules.score[0] - beforeA);
                teamPoints[1] += Math.Max(0, Rules.score[1] - beforeB);
                if (Rules.gameFinished)
                    CaptureProgressionGame();
                rallyCommitted = true;
                BetweenPoints = true;
                rallyDeadline = Time.realtimeSinceStartupAsDouble + 2.1;
                AnnouncementRpc(fault.Contains("ngoài sân") ? "OUT" : "FAULT / LỖI", fault + " • Đội " + (loser == 0 ? "A" : "B") + " phạm lỗi\n" + (priorServer == 1 - loser ? "Đội " + (priorServer == 0 ? "A" : "B") + " ghi điểm" : "Đổi giao bóng / Side Out"), (int)AnnouncementTone.Fault, 1.2f);
                foreach (var player in players.Values)
                    player.React(player.Team != loser, Rules.complete);
                if (Rules.complete)
                    Finish();
                else if (Rules.gameFinished)
                {
                    MatchSoundRpc((int)GameSfx.GameWhistle);
                    BetweenGames = true;
                    nextGameDeadline = Time.realtimeSinceStartupAsDouble + 4.2;
                    AnnouncementRpc("GAME " + endedGame + " KẾT THÚC", "Số ván thắng " + Rules.games[0] + " - " + Rules.games[1], (int)AnnouncementTone.Neutral, 1.6f);
                    AnnouncementRpc("ĐỔI BÊN", "ĐỔI BÊN • Chuẩn bị ván " + (endedGame + 1), (int)AnnouncementTone.ChangeEnds, 1.4f);
                }
                else if (Rules.endChangeDue)
                {
                    MatchSoundRpc((int)GameSfx.GameWhistle);
                    Rules.SwapEnds();
                    EndsSwapped = Rules.ends[0] > 0;
                    ReplayEventRpc("EndChange", "");
                    rallyDeadline = Time.realtimeSinceStartupAsDouble + 2.6;
                    AnnouncementRpc("ĐỔI BÊN", "ĐỔI BÊN • Mốc 6 điểm trong ván quyết định", (int)AnnouncementTone.ChangeEnds, 1.4f);
                }
            }
        }
        void Publish()
        {
            ScoreA = Rules.score[0];
            ScoreB = Rules.score[1];
            ServerTeam = Rules.servingTeam;
            ServerIndex = Rules.serverIndex;
            ServerNumber = Rules.serverNumber;
            WaitingServe = Rules.waitingServe;
            GamesA = Rules.games[0];
            GamesB = Rules.games[1];
            LastHitTeam = Rules.lastHitTeam;
            Bounces = Rules.bounceCount;
            ServeElapsed = Rules.waitingServe && !TimedOut ? Mathf.Clamp((float)(10 - (serveDeadline - Time.realtimeSinceStartupAsDouble)), 0, 10) : 0;
            TimeoutRemaining = TimedOut ? Mathf.Max(0, (float)(timeoutDeadline - Time.realtimeSinceStartupAsDouble)) : 0;
            if (!Finished)
                Status = TimedOut ? "Tạm dừng • đội " + (TimeoutTeam == 0 ? "A" : "B") + " • tối đa 60 giây" : BetweenGames ? "Hết ván • đổi sân, tự tiếp tục ván kế tiếp." : Rules.live && Time.realtimeSinceStartupAsDouble < shotLabelUntil ? shotLabel : Rules.reason;
        }
        void LateUpdate()
        {
            if (!IsSpawned) return;
            EnsureReplay();
            EnsureCoaching();
            FlushNetworkCoaching();
            if (!Body || !Started)
                return;
            var frame = Ball;
            if (!IsServer && !Paused && !Finished)
            {
                // A reliable contact may arrive before the next ball snapshot. Never interpolate
                // backwards through the paddle using a snapshot from before that contact.
                if (confirmedContact.time > frame.time)
                    frame = confirmedContact;
                float age = frame.live || frame.released ? (float)Math.Max(0, Math.Min(.08, Runner.SimulationTime - frame.time)) : 0;
                Vector3 p = frame.position, v = frame.velocity;
                if (TryGetSnapshotsBuffers(out var fromBuffer, out var toBuffer, out float alpha))
                {
                    var (from, to) = GetPropertyReader<BallFrame>(nameof(Ball)).Read(fromBuffer, toBuffer);
                    if (confirmedContact.time <= to.time)
                        p = from.motionEpoch == to.motionEpoch ? Vector3.Lerp(from.position, to.position, alpha) : to.position;
                    else BallFlight.Integrate(ref p, ref v, age);
                }
                else BallFlight.Integrate(ref p, ref v, age);
                p.y = Mathf.Max(Court.BallRadius, p.y);
                SetBallPosition(p);
            }
            var server = NetworkPlayer.All.FirstOrDefault(p => p.Team == ServerTeam && p.Index == ServerIndex);
            if (!IsServer && WaitingServe && !frame.released && server && server.Actor)
                SetBallPosition(server.Actor.HeldBallPosition);
            bool flying = (frame.live || WaitingServe && frame.released) && Body.position.y > Court.BallRadius + .02f && frame.velocity.sqrMagnitude > .04f;
            if (ballTrail)
            {
                ballTrail.emitting = flying && !Paused && !Finished;
                if (!ballTrail.emitting)
                    ballTrail.Clear();
            }
            if (flying && !Paused)
                Body.transform.Rotate(new Vector3(80, 140, 40) * Time.deltaTime);
            var local = NetworkPlayer.All.FirstOrDefault(p => p.IsLocalHuman);
            if (landingMarker)
            {
                bool visible = frame.live && local && local.Team != LastHitTeam && !Finished;
                landingMarker.gameObject.SetActive(visible);
                if (visible)
                {
                    var p = frame.position;
                    var v = frame.velocity;
                    for (int i = 0; i < 300 && p.y > Court.BallRadius; i++)
                        BallFlight.Integrate(ref p, ref v, .01f);
                    landingMarker.position = new Vector3(p.x, .018f, p.z);
                }
            }
            if (aimMarker)
            {
                bool visible = local && local.Actor && local.Actor.Swinging && !Finished;
                aimMarker.gameObject.SetActive(visible);
                if (visible)
                {
                    var p = shots.Target(local.Actor, local.Actor.Intent, frame.position, Mathf.Sign(server ? server.transform.position.x : 1));
                    p.y = .022f;
                    aimMarker.position = p;
                }
            }
        }
        public void PresentContact(Vector3 position, Vector3 velocity, double time)
        {
            if (IsServer)
                return;
            confirmedContact = new BallFrame { position = position, velocity = velocity, time = time, live = true, released = true };
            if (Body)
                SetBallPosition(position);
        }
        void SetBallPosition(Vector3 position)
        {
            // The ball is simulated with BallFlight, not PhysX. Publish both poses
            // immediately so a reset/rotation cannot restore a stale Rigidbody pose.
            Body.position = position;
            Body.transform.position = position;
        }
        public void RequestTimeout(NetworkPlayer player)
        {
            if (!IsServer || !Started || Paused || TimedOut || BetweenGames || BetweenPoints || Finished || !player.Connected || players.Values.Any(p => p.Actor.Swinging) || !Rules.TakeTimeout(player.Team))
                return;
            TimedOut = true;
            TimeoutTeam = player.Team;
            timeoutDeadline = Time.realtimeSinceStartupAsDouble + 60;
            foreach (var p in players.Values)
                p.ResetInput();
            SfxPlayer.Stop(GameSfx.Countdown);
            AnnouncementRpc("TẠM DỪNG", "Đội " + (player.Team == 0 ? "A" : "B") + " • tối đa 60 giây • Enter để tiếp tục", (int)AnnouncementTone.Neutral, 1.4f);
            Publish();
        }
        public void ResumeTimeout(NetworkPlayer player)
        {
            if (IsServer && TimedOut && !Paused && player.Connected && player.Team == TimeoutTeam)
                EndTimeout();
        }
        void EndTimeout()
        {
            TimedOut = false;
            TimeoutTeam = -1;
            serveDeadline = Time.realtimeSinceStartupAsDouble + 10;
            countdownSecond = -1;
            Publish();
        }
        public void ReserveDisconnected(ulong client)
        {
            if (!IsServer || Finished)
                return;
            var app = MultiplayerApp.Instance;
            if (!app.ClientIdentities.TryGetValue(client, out var id) || !players.TryGetValue(id, out var player))
                return;
            player.Connected = false;
            player.ResetInput();
            reservations[id] = Time.realtimeSinceStartupAsDouble + app.Settings.reconnectSeconds;
            if (!Paused)
                savedVelocity = ballVelocity;
            Paused = true;
            Body.isKinematic = true;
            Status = "Tạm dừng — chờ người chơi kết nối lại, tối đa 30 giây";
            app.ClientIdentities.Remove(client);
        }
        void PlayerConnected(ulong client)
        {
            if (!IsServer)
                return;
            var app = MultiplayerApp.Instance;
            if (!app.ClientIdentities.TryGetValue(client, out var id) || !reservations.Remove(id) || !players.TryGetValue(id, out var player))
                return;
            player.ControllerPeer = PlayerRef.FromEncoded((int)client);
            if (Runner.IsServer) player.Object.AssignInputAuthority(player.ControllerPeer);
            player.Connected = true;
            player.ResetInput();
            if (reservations.Count == 0)
            {
                Paused = false;
                Body.isKinematic = true;
                if (Rules.live)
                    ballVelocity = savedVelocity;
                serveDeadline = Time.realtimeSinceStartupAsDouble + 10;
                countdownSecond = -1;
                Publish();
            }
        }
        public void RequestLeave()
        {
            RecordLocalExit(true, "Chủ động rời trận");
            LeaveRpc();
        }
        public void PlayerLeftSession(string id)
        {
            var player = Roster?.Find(id);
            if (IsServer && !Finished && player != null)
            {
                forfeitedPlayers.Add(id);
                Rules.Forfeit(player.team, "Đội có người chủ động rời trận bị xử thua");
                Finish();
            }
        }
        void LeaveFrom(PlayerRef source)
        {
            if (Finished)
                return;
            if (MultiplayerApp.Instance.ClientIdentities.TryGetValue((ulong)source.RawEncoded, out var id))
            {
                forfeitedPlayers.Add(id);
                Rules.Forfeit(Roster.Find(id).team, "Đội có người chủ động rời trận bị xử thua");
                Finish();
            }
        }
        void Finish()
        {
            if (captureCoaching) coachingFinalPending = true;
            if (!Finished)
                MatchSoundRpc((int)GameSfx.GameWhistle);
            Body.isKinematic = true;
            Publish();
            PublishProgressionResults();
            replay?.Finish(true);
            WinningTeam = Rules.winner;
            Status = $"Đội {(Rules.winner == 0 ? "A" : "B")} thắng trận {Rules.games[0]} : {Rules.games[1]} ván — điểm ván cuối {Rules.score[0]} : {Rules.score[1]}\n{Rules.reason}";
            Finished = true;
            AnnouncementRpc("KẾT THÚC TRẬN ĐẤU", Status.ToString(), (int)AnnouncementTone.Success, 2.1f);
        }
        void ResultChanged(bool old, bool complete)
        {
            if (!complete)
                return;
            replay?.Event("MatchEnd", Status.ToString());
            replay?.Finish(true);
            MultiplayerApp.Instance.ShowResult(Status.ToString());
            MultiplayerCourtUI.Instance?.EnsureEndAnnouncement(Status.ToString());
        }
        void SoundEffect(int sound)
        {
#if !UNITY_SERVER
            SfxPlayer.Play((GameSfx)sound);
#endif
        }
        void AnnouncementEffect(string title, string detail, int tone, float duration)
        {
            var ui = MultiplayerCourtUI.Instance;
            if (ui)
                ui.ShowAnnouncement(title, detail, duration, (AnnouncementTone)tone);
        }
    }
}
