#if UNITY_SERVER
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PickleVerse.Progression;
using PickleVerse.Replays;
using UnityEngine;
using UnityEngine.Networking;

namespace PickleVerse.Multiplayer
{
    public sealed class DedicatedWorker : MonoBehaviour
    {
        public static DedicatedWorker Instance { get; private set; }
        [Serializable] sealed class Allocation
        {
            public string matchId, leaseId, session, region, state, replayWriter;
            public int protocol;
            public bool telemetryEnabled;
            public MatchRoster roster;
        }
        Allocation job;
        [Serializable] sealed class PlayerSession { public string playerId; public int authVersion; }
        [Serializable] sealed class HeartbeatReply
        {
            public string stopOperationId, reason;
            public PlayerSession[] expiredSessions;
        }
        sealed class ServerApiFailure : IOException
        {
            public readonly long Status;
            public ServerApiFailure(long status) : base("Server API returned HTTP " + status) => Status = status;
            public bool IsTransient => Status == 0 || Status >= 500;
        }
        string serviceKey, url, results, abandonedLease;
        bool stopping, fenced, handlingStop, measureTicks;
        PhotonSession session;
        readonly Dictionary<string, CoachingSession> telemetry = new Dictionary<string, CoachingSession>();
        readonly HashSet<string> usedNonces = new HashSet<string>();
        readonly Dictionary<string, SemaphoreSlim> admissionGates = new Dictionary<string, SemaphoreSlim>();
        readonly DedicatedPlayerSessions playerSessions = new DedicatedPlayerSessions();
        readonly string workerId = Guid.NewGuid().ToString("N");
        public LocalReplayRepository Replays { get; private set; }
        public string ReplayWriter => job.replayWriter;
        public bool TelemetryEnabled => job?.telemetryEnabled == true;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => new GameObject("Dedicated Worker").AddComponent<DedicatedWorker>();
        void Awake()
        {
            Instance = this; DontDestroyOnLoad(gameObject); Application.runInBackground = true; Application.targetFrameRate = 60;
            serviceKey = Environment.GetEnvironmentVariable("PKV_GAME_SERVER_KEY") ?? "";
            url = (Environment.GetEnvironmentVariable("PKV_BACKEND_URL") ?? "http://backend-api:8080").TrimEnd('/');
            measureTicks = Environment.GetEnvironmentVariable("PKV_SAMPLE_TICKS") == "1";
            Replays = new LocalReplayRepository(Path.Combine(Application.persistentDataPath, "server-replays"));
            if (serviceKey.Length < 32) { Debug.LogError("[Dedicated] Missing PKV_GAME_SERVER_KEY."); Application.Quit(78); return; }
            _ = Loop();
        }
        public void Record(string player, ShotTelemetry shot)
        {
            if (job == null || !job.roster.members.Any(m => m.id == player && !m.bot)) return;
            if (!telemetry.TryGetValue(player, out var target)) telemetry[player] = target = new CoachingSession { playerId = player, sessionId = job.matchId, sessionType = "match", gameVersion = Services.MultiplayerSceneConfiguration.Release };
            if (target.shots.Count < 100000) target.shots.Add(shot);
        }
        public void SubmitResults(string json) { if (job != null && results == null) results = json; }
        public void Abort() => fenced = true;
        async Task Loop()
        {
            while (!stopping)
            {
                try
                {
                    job = await Post<Allocation>("poll", new { });
                    await MarkHealthy();
                    if (job == null || job.leaseId == abandonedLease) { job = null; await Task.Delay(1000); continue; }
                    if (job.protocol != Services.MultiplayerSceneConfiguration.ProtocolVersion) throw new InvalidOperationException("Protocol mismatch.");
                    fenced = false; handlingStop = false; results = null; telemetry.Clear(); usedNonces.Clear(); admissionGates.Clear(); playerSessions.Clear();
                    session = PhotonSession.Create("Dedicated Fusion Server"); session.PrepareDedicated(job.roster);
                    var app = MultiplayerApp.Ensure(); app.AttachDedicatedServer(session, job.roster);
                    session.PeerJoined += (runner, peer) =>
                    {
                        var claims = Claims(runner.GetPlayerConnectionToken(peer));
                        string rejected = claims == null ? "invalid-ticket" : !usedNonces.Add(claims.nonce) ? "used-ticket" :
                            runner.GetPlayerUserId(peer) != DedicatedAdmission.ConnectionIdentity(claims.playerId, claims.authVersion) ? "identity-mismatch" : null;
                        if (rejected != null) { Debug.LogWarning("[Dedicated] Admission rejected: " + rejected); runner.Disconnect(peer); return; }
                        if (!admissionGates.TryGetValue(claims.playerId, out var gate))
                            admissionGates[claims.playerId] = gate = new SemaphoreSlim(1, 1);
                        _ = Admit(runner, peer, claims, job.leaseId, gate);
                    };
                    session.PeerLeft += (runner, peer) =>
                    {
                        if (session?.Runner == runner) playerSessions.RemovePeer((ulong)peer.RawEncoded);
                    };
                    _ = Heartbeat(job.leaseId);
                    ushort port = ushort.TryParse(Environment.GetEnvironmentVariable("PKV_GAME_PORT"), out var configured) ? configured : (ushort)3669;
                    await session.StartDedicatedAsync(job.session, job.region, job.roster, port, bytes =>
                    {
                        bool valid = Claims(bytes) != null;
                        if (!valid) Debug.LogWarning("[Dedicated] Connection refused: invalid-ticket (bytes=" + (bytes?.Length ?? 0) + ").");
                        return valid;
                    });
                    if (fenced) throw new IOException("Allocation interrupted during startup.");
                    await HandleOperation(await Retry(() => Post<HeartbeatReply>("ready", Command())));
                    Debug.Log($"[Dedicated] Session ready match={job.matchId} worker={workerId} players={job.roster.HumanCount}");
                    double courtUntil = Time.realtimeSinceStartupAsDouble + 115;
                    double until = Time.realtimeSinceStartupAsDouble + 7200;
                    double lastSample = 0;
                    int lastTick = 0;
                    while (!stopping && !fenced && results == null && Time.realtimeSinceStartupAsDouble < until)
                    {
                        var match = NetworkMatch.Instance;
                        double now = Time.realtimeSinceStartupAsDouble;
                        bool inCourt = match && match.Runner == session.Runner && match.Started;
                        if (now >= courtUntil && !inCourt)
                            throw new TimeoutException("Court did not start before its admission deadline.");
                        if (measureTicks && inCourt && now - lastSample >= 15)
                        {
                            int tick = session.Runner.Tick.Raw;
                            int advanced = tick - lastTick;
                            if (lastSample > 0)
                                Debug.Log($"[Dedicated] Tick sample match={job.matchId} worker={workerId} ticks={advanced} seconds={now - lastSample:F3}");
                            lastSample = now;
                            lastTick = tick;
                        }
                        await Task.Delay(100);
                    }
                    if (fenced || results == null) throw new InvalidOperationException("Lease lost or session timed out.");
                    await Task.Delay(500);
                    var recorder = NetworkMatch.Instance?.GetComponent<ReplayRecorder>();
                    if (recorder) { recorder.Finish(true); await recorder.Completion; }
                    await Retry(() => Post<JToken>("complete", new { LeaseId = job.leaseId, Results = JArray.Parse(results) }));
                    foreach (var captured in telemetry.Values)
                    {
                        captured.closed = true;
                        foreach (var part in CoachingRuntime.Split(captured)) await Retry(() => Post<JToken>(job.leaseId + "/telemetry", part));
                    }
                    await UploadReplay();
                    double deliveryDeadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (NetworkMatch.Instance && !NetworkMatch.Instance.ResultsDelivered && Time.realtimeSinceStartupAsDouble < deliveryDeadline && !fenced)
                        await Task.Delay(100);
                    Debug.Log($"[Dedicated] Session persisted match={job.matchId} worker={workerId}");
                }
                catch (Exception error)
                {
                    // Never start a failed allocation again with empty simulation state or reusable tickets.
                    if (job != null) abandonedLease = job.leaseId;
                    Debug.LogWarning($"[Dedicated] Session stopped match={job?.matchId} worker={workerId}: {error.Message}");
                }
                finally
                {
                    fenced = true;
                    if (session != null) { try { await session.LeaveAsync(); } catch { } session = null; }
                    if (job != null)
                        try { await Post<JToken>("release", Command()); } catch (IOException) { }
                    job = null; results = null;
                }
                await Task.Delay(1000);
            }
        }
        DedicatedAdmission.Claims Claims(byte[] token)
        {
            if (job == null) return null;
            var claims = DedicatedAdmission.Verify(token, serviceKey, job.matchId, job.leaseId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            return claims != null && !usedNonces.Contains(claims.nonce) && job.roster.members.Any(m => m.id == claims.playerId && !m.bot) ? claims : null;
        }
        async Task Admit(Fusion.NetworkRunner runner, Fusion.PlayerRef peer, DedicatedAdmission.Claims claims, string lease, SemaphoreSlim gate)
        {
            await gate.WaitAsync();
            try
            {
                if (fenced || job?.leaseId != lease || !runner || !runner.ActivePlayers.Contains(peer)) return;
                string player = claims.playerId;
                await Post<JToken>("admission", new { LeaseId = lease, PlayerId = Guid.ParseExact(player, "N"), AuthVersion = claims.authVersion }, timeout: 4);
                if (fenced || job?.leaseId != lease || !runner || !runner.ActivePlayers.Contains(peer)) return;
                var existing = playerSessions.Find(player);
                if (existing != null && !runner.ActivePlayers.Any(p => (ulong)p.RawEncoded == existing.Peer))
                    playerSessions.RemovePeer(existing.Peer);
                if (!playerSessions.TryAdmit(player, claims.authVersion, (ulong)peer.RawEncoded, out var previous))
                { runner.Disconnect(peer); return; }
                var app = MultiplayerApp.Instance;
                app.RegisterDedicatedPeer(peer, player, previous);
                if (previous != null) Debug.Log($"[Dedicated] Device takeover match={job.matchId} player={player} version={claims.authVersion}");
                Debug.Log($"[Dedicated] Admitted match={job.matchId} worker={workerId} peers={app.ClientIdentities.Count}/{job.roster.HumanCount}");
            }
            catch (Exception error)
            {
                if (runner && runner.ActivePlayers.Contains(peer)) runner.Disconnect(peer);
                Debug.LogWarning("[Dedicated] Admission denied (" + error.GetType().Name + ").");
            }
            finally { gate.Release(); }
        }
        object Command() => new { LeaseId = job.leaseId, MatchId = job.matchId };
        async Task Heartbeat(string lease)
        {
            const double renewalWindowSeconds = 24;
            double renewBy = Time.realtimeSinceStartupAsDouble + renewalWindowSeconds;
            while (!stopping && !fenced && job?.leaseId == lease)
            {
                try
                {
                    var reply = await Post<HeartbeatReply>("heartbeat",
                        playerSessions.CreateHeartbeat(lease, NetworkMatch.Instance?.LiveStatus()), timeout: 4);
                    if (job?.leaseId != lease || fenced) return;
                    renewBy = Time.realtimeSinceStartupAsDouble + renewalWindowSeconds;
                    ExpireSessions(reply);
                    await HandleOperation(reply);
                    await MarkHealthy();
                    await Task.Delay(5000);
                }
                catch (ServerApiFailure error) when (error.IsTransient && Time.realtimeSinceStartupAsDouble < renewBy)
                {
                    await Task.Delay(1000);
                }
                catch (Exception error)
                {
                    if (job?.leaseId == lease)
                    {
                        Debug.LogWarning($"[Dedicated] Heartbeat lost match={job.matchId} worker={workerId}: {error.Message}");
                        fenced = true;
                        if (session?.Runner) await session.Runner.Shutdown();
                    }
                    return;
                }
            }
        }
        void ExpireSessions(HeartbeatReply reply)
        {
            if (reply?.expiredSessions == null || !session?.Runner) return;
            foreach (var expired in reply.expiredSessions)
            {
                if (expired == null || !Guid.TryParse(expired.playerId, out var player)) continue;
                var current = playerSessions.TakeExpired(player.ToString("N"), expired.authVersion);
                if (current == null) continue;
                NetworkMatch.Instance?.ReserveDisconnected(current.Peer);
                MultiplayerApp.Instance.RemoveClientIdentity(current.Peer);
                session.Runner.Disconnect(Fusion.PlayerRef.FromEncoded((int)current.Peer));
                Debug.Log($"[Dedicated] Expired login disconnected match={job.matchId} player={current.Player}");
            }
        }
        async Task HandleOperation(HeartbeatReply reply)
        {
            if (job == null || string.IsNullOrEmpty(reply?.stopOperationId) || fenced || handlingStop) return;
            handlingStop = true;
            NetworkMatch.Instance?.InterruptFromServer(reply.reason);
            await Retry(() => Post<JToken>("operations/ack", new { LeaseId = job.leaseId, OperationId = reply.stopOperationId }));
            Debug.Log("[Dedicated] Stop operation acknowledged: " + reply.stopOperationId);
            await Task.Delay(1500);
            fenced = true;
        }
        static Task MarkHealthy() => Task.Run(() =>
        {
            try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "pickleverse-worker-alive"), "alive"); }
            catch (IOException) { }
        });
        async Task UploadReplay()
        {
            var metadata = Replays.Read(job.replayWriter, job.matchId); if (metadata == null) return;
            var chunks = metadata.chunks.ToArray(); metadata.complete = false;
            await Retry(() => Post<JToken>(job.leaseId + "/replay", metadata));
            for (int index = 0; index < chunks.Length; index++)
            {
                string json = JsonUtility.ToJson(Replays.ReadChunk(metadata, chunks[index]));
                string data = await Task.Run(() =>
                {
                    using (var bytes = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(bytes, System.IO.Compression.CompressionLevel.Fastest, true)) { byte[] raw = Encoding.UTF8.GetBytes(json); gzip.Write(raw, 0, raw.Length); }
                        return Convert.ToBase64String(bytes.ToArray());
                    }
                });
                await Retry(() => Post<JToken>(job.leaseId + "/replay/chunks/" + index, new { Data = data }));
            }
            metadata.complete = true;
            await Retry(() => Post<JToken>(job.leaseId + "/replay", metadata));
        }
        async Task<T> Retry<T>(Func<Task<T>> action)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await action(); }
                catch when (attempt < 4 && !fenced) { await Task.Delay(1000 * (attempt + 1)); }
            }
        }
        async Task<T> Post<T>(string route, object body, int timeout = 15)
        {
            using (var request = new UnityWebRequest(url + "/api/Server/" + route, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = timeout;
                request.SetRequestHeader("Content-Type", "application/json"); request.SetRequestHeader("X-Game-Server-Key", serviceKey);
                request.SetRequestHeader("X-Game-Worker", workerId);
                request.SetRequestHeader("X-Game-Protocol", Services.MultiplayerSceneConfiguration.ProtocolVersion.ToString());
                var pending = request.SendWebRequest(); while (!pending.isDone) await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success) throw new ServerApiFailure(request.responseCode);
                if (string.IsNullOrWhiteSpace(request.downloadHandler.text)) return default;
                var envelope = JObject.Parse(request.downloadHandler.text); var data = envelope["data"];
                return data == null || data.Type == JTokenType.Null ? default : data.ToObject<T>();
            }
        }
        void OnApplicationQuit() => stopping = true;
    }
}
#endif
