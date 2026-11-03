using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    // Observations are buffered in memory; filesystem work never runs in simulation.
    public sealed class NetworkQualityDiagnostics : MonoBehaviour
    {
        static readonly List<float> corrections = new List<float>();
        static int inputBacklog;
        static bool enabledForSession;
        static uint latestInput, latestAck;
        readonly List<float> frameMilliseconds = new List<float>();
        readonly List<double> rttVariation = new List<double>();
        double nextRtt, previousRtt;
        double nextSample;
        string path;
        Task writing = Task.CompletedTask;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            corrections.Clear(); inputBacklog = 0; latestInput = latestAck = 0;
            enabledForSession = Application.isEditor || Environment.GetCommandLineArgs().Contains("-pkvNetworkDiagnostics");
            if (enabledForSession) new GameObject("Network Quality Diagnostics").AddComponent<NetworkQualityDiagnostics>();
        }
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            path = Path.Combine(Application.persistentDataPath, "network-quality-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".jsonl");
            Debug.Log("[NetworkQuality] Diagnostics file: " + path);
        }
        public static void RecordCorrection(float metres, int backlog, uint input, uint ack)
        {
            if (!enabledForSession) return;
            if (corrections.Count < 2048) corrections.Add(metres);
            RecordInput(input, ack, backlog);
        }
        public static void RecordInput(uint input, uint ack, int backlog)
        {
            if (!enabledForSession) return;
            inputBacklog = Math.Max(inputBacklog, backlog);
            latestInput = input; latestAck = ack;
        }
        void LateUpdate()
        {
            var app = MultiplayerApp.Instance;
            var runner = app?.Network;
            var match = NetworkMatch.Instance;
            // DeltaTime is unavailable before Fusion has received RuntimeConfig. Spawned
            // match objects are only delivered after that configuration is ready.
            if (!runner || !runner.IsRunning || !match || !match.IsSpawned || match.Runner != runner)
            { frameMilliseconds.Clear(); rttVariation.Clear(); previousRtt = 0; return; }
            if (frameMilliseconds.Count < 4096) frameMilliseconds.Add(Time.unscaledDeltaTime * 1000);
            var peer = runner.LocalPlayer == PlayerRef.None ? runner.ActivePlayers.FirstOrDefault() : runner.LocalPlayer;
            double rtt = peer == PlayerRef.None ? 0 : runner.GetPlayerRtt(peer);
            if (Time.realtimeSinceStartupAsDouble >= nextRtt)
            {
                nextRtt = Time.realtimeSinceStartupAsDouble + .25;
                if (previousRtt > 0 && rttVariation.Count < 128) rttVariation.Add(Math.Abs(rtt - previousRtt));
                previousRtt = rtt;
            }
            if (Time.realtimeSinceStartupAsDouble < nextSample || !writing.IsCompleted) return;
            nextSample = Time.realtimeSinceStartupAsDouble + 5;
            corrections.Sort();
            frameMilliseconds.Sort(); rttVariation.Sort();
            var sample = new Sample { utc = DateTime.UtcNow.ToString("O"), build = Services.MultiplayerSceneConfiguration.Release + "/" + Application.version,
                matchId = match.Roster?.matchId,
                protocol = Services.MultiplayerSceneConfiguration.ProtocolVersion, region = runner.SessionInfo.Region,
                role = runner.IsSharedModeMasterClient ? "shared-authority" : runner.IsServer ? "server" : "client",
                connection = runner.CurrentConnectionType.ToString(), fps = 1f / Mathf.Max(.0001f, Time.unscaledDeltaTime),
                tickSeconds = runner.DeltaTime, rttSeconds = rtt,
                frameP95Milliseconds = frameMilliseconds.Count == 0 ? 0 : frameMilliseconds[(int)((frameMilliseconds.Count - 1) * .95f)],
                rttVariationP95Seconds = rttVariation.Count == 0 ? 0 : rttVariation[(int)((rttVariation.Count - 1) * .95f)],
                latestInput = latestInput, latestAck = latestAck,
                authorityPeer = NetworkMatch.Instance ? NetworkMatch.Instance.Object.StateAuthority.RawEncoded : 0,
                pendingCommands = app.Match?.Channel?.PendingCount ?? 0, unacknowledgedInputs = inputBacklog,
                correctionP95Metres = corrections.Count == 0 ? 0 : corrections[(int)((corrections.Count - 1) * .95f)],
                correctionMaxMetres = corrections.Count == 0 ? 0 : corrections[corrections.Count - 1],
                snapshotAgeSeconds = NetworkMatch.Instance ? Math.Max(0, runner.SimulationTime - NetworkMatch.Instance.Ball.time) : 0 };
            string line = JsonUtility.ToJson(sample) + "\n";
            corrections.Clear(); inputBacklog = 0; frameMilliseconds.Clear(); rttVariation.Clear();
            writing = Task.Run(() => { try { File.AppendAllText(path, line); } catch (IOException) { } });
        }
        [Serializable] sealed class Sample
        {
            public string utc, build, matchId, region, role, connection;
            public int protocol, pendingCommands, unacknowledgedInputs;
            public int authorityPeer;
            public uint latestInput, latestAck;
            public float fps, tickSeconds, correctionP95Metres, correctionMaxMetres, frameP95Milliseconds;
            public double rttSeconds, snapshotAgeSeconds, rttVariationP95Seconds;
        }
    }
}
