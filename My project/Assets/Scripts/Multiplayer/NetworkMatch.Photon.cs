using Fusion;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        readonly MatchResultTransfer resultTransfer = new MatchResultTransfer();
        string resultTransferId;
        readonly HashSet<ulong> resultAcknowledgements = new HashSet<ulong>();
        public bool ResultsDelivered => !string.IsNullOrEmpty(ProgressionResults) && MultiplayerApp.Instance.ClientIdentities.Keys.All(resultAcknowledgements.Contains);
        public override void Render()
        {
            if (renderedStarted != Started)
            {
                renderedStarted = Started;
                StartChanged(!Started, Started);
            }
            if (renderedFinished != Finished)
            {
                renderedFinished = Finished;
                ResultChanged(!Finished, Finished);
            }
        }
        void Send(string kind) => PhotonSession.Find(Runner).Send(Object.StateAuthority, Object, kind, new object[0]);
        void Broadcast(string kind, params object[] values)
        {
            foreach (var peer in Runner.ActivePlayers)
                PhotonSession.Find(Runner).Send(peer, Object, kind, values);
        }
        void CourtReadyRpc() => Send("court-ready");
        void CourtRevealedRpc() => Send("court-revealed");
        void SendProgressionResultsToPeers()
        {
            foreach (var peer in Runner.ActivePlayers)
                if (peer != Runner.LocalPlayer) SendProgressionResults(peer);
        }
        void SendProgressionResults(PlayerRef destination)
        {
            if (string.IsNullOrEmpty(ProgressionResults)) return;
            string[] parts = MatchResultTransfer.Encode(ProgressionResults);
            string id = resultTransferId ?? (resultTransferId = Guid.NewGuid().ToString("N"));
            for (int i = 0; i < parts.Length; i++)
                PhotonSession.Find(Runner).Send(destination, Object, "result-part", new object[] { id, i, parts.Length, parts[i] });
        }
        void LeaveRpc() => Send("leave");
        void MatchSoundRpc(int sound) => Broadcast("sound", sound);
        void AnnouncementRpc(string title, string detail, int tone, float duration) => Broadcast("announce", title, detail, tone, duration);
        void ReplayEventRpc(string kind, string detail) => Broadcast("replay-event", kind, detail);
        public void ReceivePhotonMessage(PlayerRef source, string kind, string payload)
        {
            var a = JArray.Parse(payload);
            if (HasStateAuthority && kind == "result-ack" && a.Count == 1 && a[0].Value<string>() == resultTransferId && MultiplayerApp.Instance.ClientIdentities.ContainsKey((ulong)source.RawEncoded))
            { resultAcknowledgements.Add((ulong)source.RawEncoded); return; }
            if (HasStateAuthority && MultiplayerApp.Instance.ClientIdentities.ContainsKey((ulong)source.RawEncoded) && a.Count == 0)
            {
                if (kind == "court-ready")
                    CourtReadyFrom(source);
                else if (kind == "court-revealed")
                    CourtRevealedFrom(source);
                else if (kind == "leave")
                    LeaveFrom(source);
                else if (kind == "request-results")
                    SendProgressionResults(source);
            }
            if (source != Object.StateAuthority)
                return;
            if (kind == "result-part" && a.Count == 4)
            {
                if (resultTransfer.Add(a[0].Value<string>(), a[1].Value<int>(), a[2].Value<int>(), a[3].Value<string>(), out var result))
                {
                    if (result != ProgressionResults)
                    {
                        ProgressionResults = result;
                        resultTransferId = a[0].Value<string>();
                        ResultsAvailable(null, result);
                    }
                    PhotonSession.Find(Runner).Send(Object.StateAuthority, Object, "result-ack", new object[] { a[0].Value<string>() });
                }
            }
            else if (kind == "sound" && a.Count == 1)
                SoundEffect(a[0].Value<int>());
            else if (kind == "announce" && a.Count == 4)
                AnnouncementEffect(a[0].Value<string>(), a[1].Value<string>(), a[2].Value<int>(), a[3].Value<float>());
            else if (kind == "replay-event" && a.Count == 2)
                ReplayEventEffect(a[0].Value<string>(), a[1].Value<string>());
            else if (kind == "coaching-shot" && a.Count == 1)
                ReceiveCoachingShot(a[0].Value<string>());
            else if (kind == "coaching-checkpoint" && a.Count == 1)
                ReceiveCoachingCheckpoint(a[0].Value<bool>());
        }
    }
}
