using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Progression
{
    // Telemetry is an upload spool, never an authoritative player/progression save.
    public sealed class CoachingSession
    {
        public string playerId, sessionId, sessionType, batchId = Guid.NewGuid().ToString("N"), gameVersion;
        public bool closed, partial;
        public long createdUtcTicks = DateTime.UtcNow.Ticks;
        public List<ShotTelemetry> shots = new List<ShotTelemetry>();
        [JsonIgnore] public ShotTelemetry Last => shots.LastOrDefault();
        public ShotTelemetry Attempt(string kind, string technique, string source, double time)
        {
            if (closed) return null;
            var shot = new ShotTelemetry { shotId = Guid.NewGuid().ToString("N"), sequence = shots.Count,
                kind = kind, technique = technique, source = source, time = time };
            shots.Add(shot);
            return shot;
        }
    }
    public sealed class CoachingRuntime : MonoBehaviour
    {
        static CoachingRuntime instance;
        public static CoachingRuntime Instance
        {
            get
            {
                if (!Application.isPlaying) return null;
                if (!instance) new GameObject("Hybrid Coaching").AddComponent<CoachingRuntime>();
                return instance;
            }
        }
        public CoachingSummary Summary { get; private set; }
        public TierEstimate PracticeTier
        {
            get
            {
                var account = AccountRuntime.Ensure().Account;
                return account.IsSignedIn && summaryOwner == account.Profile.UserId.ToString("N") ? Summary?.analysis?.tier : null;
            }
        }
        string summaryOwner;
        readonly Dictionary<string, Task> writes = new Dictionary<string, Task>();
        Task uploading = Task.CompletedTask;
        float nextRetry;
        string root;
        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            root = Path.Combine(Application.persistentDataPath, "hybrid-telemetry");
        }
        public CoachingSession Begin(string sessionId, string type)
        {
            if (ProgressionRuntime.Instance?.Snapshot?.catalog?.hybridTelemetryEnabled != true) return null;
            var account = AccountRuntime.Ensure().Account;
            if (!account.IsSignedIn || !Guid.TryParse(sessionId, out _)) return null;
            string directory = Path.Combine(root, account.Profile.UserId.ToString("N"));
            foreach (var file in SessionFiles(directory))
                    try
                    {
                        var saved = JsonConvert.DeserializeObject<CoachingSession>(File.ReadAllText(file));
                        if (saved != null && saved.playerId == account.Profile.UserId.ToString("N") && !saved.closed && saved.createdUtcTicks>=DateTime.UtcNow.AddDays(-30).Ticks && saved.sessionId == sessionId && saved.sessionType == type) return saved;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (JsonException) { }
            return new CoachingSession { playerId = account.Profile.UserId.ToString("N"), sessionId = sessionId,
                sessionType = type, gameVersion = Application.version };
        }
        static string[] SessionFiles(string directory)
        {
            try { return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : Array.Empty<string>(); }
            catch (IOException) { return Array.Empty<string>(); }
            catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        }
        public void Checkpoint(CoachingSession session)
        {
            if (session == null) return;
            // Capture plain managed data on the caller thread. Serialization and disk I/O run off simulation.
            var snapshot = new CoachingSession { playerId = session.playerId, sessionId = session.sessionId,
                sessionType = session.sessionType, batchId = session.batchId, gameVersion = session.gameVersion,
                partial = session.partial, closed = session.closed,
                createdUtcTicks = session.createdUtcTicks,
                shots = session.shots.Select(Clone).ToList() };
            string file = Path.Combine(root, session.playerId, session.batchId + ".json");
            writes.TryGetValue(file, out var previous);
            writes[file] = SaveAfter(previous, file, snapshot);
        }
        static ShotTelemetry Clone(ShotTelemetry s) => new ShotTelemetry {
            shotId=s.shotId,kind=s.kind,technique=s.technique,source=s.source,outcome=s.outcome,sequence=s.sequence,rally=s.rally,time=s.time,
            timingError=s.timingError,contact=s.contact?.ToArray(),leftFoot=s.leftFoot?.ToArray(),rightFoot=s.rightFoot?.ToArray(),
            intendedTarget=s.intendedTarget?.ToArray(),landing=s.landing?.ToArray(),kitchen=s.kitchen,volley=s.volley,momentum=s.momentum,
            recovered=s.recovered,targetHit=s.targetHit,bounces=s.bounces,netClearance=s.netClearance,engineAimed=s.engineAimed,assisted=s.assisted };
        static async Task SaveAfter(Task previous, string file, CoachingSession session)
        {
            if (previous != null) { try { await previous; } catch (IOException) { } }
            await Task.Run(() => {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                string temp = file + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(session), Encoding.UTF8);
                if (File.Exists(file)) File.Replace(temp, file, null); else File.Move(temp, file);
            });
        }
        public void Close(CoachingSession session, bool partial = false)
        {
            if (session == null || session.closed) return;
            session.closed = true; session.partial = partial;
            Checkpoint(session);
            nextRetry = 0;
        }
        void Update()
        {
            if (!uploading.IsCompleted || Time.unscaledTime < nextRetry) return;
            nextRetry = Time.unscaledTime + 30;
            uploading = Flush();
        }
        async Task Flush()
        {
            if (ProgressionRuntime.Instance?.Snapshot?.catalog?.hybridTelemetryEnabled != true) return;
            var account = AccountRuntime.Ensure().Account;
            if (!account.IsSignedIn) return;
            var owner = account.Profile.UserId.ToString("N");
            string dir = Path.Combine(root, owner);
            foreach (var file in SessionFiles(dir))
            {
                try
                {
                    if (writes.TryGetValue(file, out var writing)) await writing;
                    var session = await Task.Run(() => JsonConvert.DeserializeObject<CoachingSession>(File.ReadAllText(file)));
                    if (session.createdUtcTicks<DateTime.UtcNow.AddDays(-30).Ticks) { await Task.Run(()=>File.Delete(file)); writes.Remove(file); continue; }
                    if (!session.closed || session.playerId != owner) continue;
                    var parts = await Task.Run(() => Split(session));
                    foreach (var part in parts)
                    {
                        if (!account.IsSignedIn || account.Profile.UserId.ToString("N") != owner) return;
                        var receipt = await account.SendAuthorizedAsync<TelemetryReceipt>("POST", "api/AiCoaching/telemetry", part);
                        if (receipt == null || receipt.batchId != session.batchId) throw new IOException("Invalid telemetry receipt");
                    }
                    await Task.Run(() => File.Delete(file));
                    writes.Remove(file);
                    if (summaryOwner == owner) Summary = null;
                }
                catch (Exception) { /* Durable spool retries on reconnect; gameplay never waits for analytics. */ }
            }
        }
        public static List<TelemetryBatch> Split(CoachingSession session)
            => TelemetryBatching.Split(new TelemetryBatch { batchId = session.batchId, sessionId = session.sessionId,
                sessionType = session.sessionType, playerId = session.playerId, gameVersion = session.gameVersion, partial = session.partial },
                session.shots, shot => Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(shot)));
        public async Task<CoachingSummary> LoadSummary(bool refresh = false)
        {
            var account = AccountRuntime.Ensure().Account;
            if (!account.IsSignedIn) return null;
            string owner = account.Profile.UserId.ToString("N");
            if (!refresh && Summary != null && summaryOwner == owner) return Summary;
            var result = await account.SendAuthorizedAsync<CoachingSummary>("GET", "api/AiCoaching/summary");
            if (account.IsSignedIn && account.Profile.UserId.ToString("N") == owner) { Summary = result; summaryOwner = owner; }
            return result;
        }
    }
}
