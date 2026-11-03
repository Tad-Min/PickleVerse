using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using PickleVerse.LocalMatch;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        // Simulation only mutates plain records. Serialization/network delivery and disk
        // checkpoints happen from LateUpdate, after the rally has been settled.
        readonly Dictionary<int, ShotTelemetry> coachingAttempts = new Dictionary<int, ShotTelemetry>();
        readonly Dictionary<int, ShotTelemetry> coachingContacts = new Dictionary<int, ShotTelemetry>();
        readonly Dictionary<int, int> coachingSequences = new Dictionary<int, int>();
        readonly List<CoachingDelivery> coachingRallyShots = new List<CoachingDelivery>();
        readonly Queue<CoachingDelivery> coachingDeliveries = new Queue<CoachingDelivery>();
        bool captureCoaching, coachingCheckpointPending, coachingFinalPending;
        int coachingRally;
        sealed class CoachingDelivery
        {
            public string sessionId, playerId;
            public ShotTelemetry shot;
        }
        static int CoachingSlot(NetworkPlayer player) => player.Team * 2 + player.Index;
        void CoachingNewRally()
        {
            if (coachingRally == 0)
#if UNITY_SERVER
                captureCoaching = DedicatedWorker.Instance?.TelemetryEnabled == true;
#else
                captureCoaching = ProgressionRuntime.Instance?.Snapshot?.catalog?.hybridTelemetryEnabled == true;
#endif
            coachingRally++;
            coachingAttempts.Clear(); coachingContacts.Clear();
        }
        ShotTelemetry CoachingAttempt(NetworkPlayer player, string kind)
        {
            if (!captureCoaching || player.Automated) return null;
            int slot = CoachingSlot(player);
            coachingSequences.TryGetValue(slot, out int sequence);
            var shot = new ShotTelemetry { shotId = Guid.NewGuid().ToString("N"), sequence = sequence,
                rally = coachingRally, kind = kind, technique = kind, source = "fusion-authority-observed",
                time = Math.Max(0, Runner.SimulationTime), bounces = Rules.bounceCount,
                outcome = "Miss", assisted = true, engineAimed = true };
            coachingSequences[slot] = sequence + 1;
            coachingAttempts[slot] = shot;
            coachingRallyShots.Add(new CoachingDelivery {sessionId = Roster.matchId, playerId = player.Identity.ToString(), shot = shot});
            return shot;
        }
        void CoachingContact(NetworkPlayer player, bool volley, bool success)
        {
            if (!captureCoaching || player.Automated) return;
            int slot = CoachingSlot(player);
            if (!coachingAttempts.TryGetValue(slot, out var shot)) return;
            var actor = player.Actor;
            shot.contact = CoachingVector(ballPosition);
            var left = actor.Feet(true); var right = actor.Feet(false);
            shot.leftFoot = new[] {left.x, left.y, left.z}; shot.rightFoot = new[] {right.x, right.y, right.z};
            shot.timingError = actor.SwingTime - actor.ContactDelay;
            shot.volley = volley; shot.kitchen = actor.InKitchen; shot.bounces = shot.kind == "Serve" || volley ? 0 : 1;
            shot.outcome = success ? "Contact" : "Fault";
            if (volley) shot.momentum = false;
            if (!success && Rules.reason == "Lỗi chân giao bóng") shot.outcome = "ServeFeet";
            coachingContacts[slot] = shot;
        }
        void CoachingMomentum(NetworkPlayer player)
        {
            if (captureCoaching && player.Actor.VolleyMomentum && player.Actor.InKitchen &&
                coachingContacts.TryGetValue(CoachingSlot(player), out var shot) && shot.volley == true)
            { shot.momentum = true; shot.outcome = "Momentum"; }
        }
        void CoachingNet(float clearance)
        {
            if (captureCoaching && coachingContacts.TryGetValue(Rules.lastHitPlayer, out var shot)) shot.netClearance = clearance;
        }
        void CoachingLanding(Vector3 point)
        {
            if (captureCoaching && Rules.bounceCount == 0 && coachingContacts.TryGetValue(Rules.lastHitPlayer, out var shot))
                shot.landing = CoachingVector(point);
        }
        void CoachingRallyEnded(int losingTeam, string reason)
        {
            if (!captureCoaching) return;
            if (reason == "Quá 10 giây giao bóng" || reason == "Giao bóng hụt")
            {
                var server = players.Values.FirstOrDefault(p => p.Team == Rules.servingTeam && p.Index == Rules.serverIndex);
                if (server && !server.Automated)
                {
                    if (!coachingAttempts.TryGetValue(CoachingSlot(server), out var shot)) shot = CoachingAttempt(server, "Serve");
                    shot.outcome = reason == "Quá 10 giây giao bóng" ? "LateServe" : "Miss";
                }
            }
            else if (reason == "Bóng nảy hai lần")
            {
                // In doubles, never blame an arbitrary teammate for an unreturned ball.
                var receiver = players.Values.FirstOrDefault(p => p.Team == losingTeam &&
                    (Rules.teamSizes[losingTeam] == 1 || Rules.strokeCount == 1 && p.Index == Rules.receiverIndex));
                if (receiver && !receiver.Automated)
                {
                    int slot = CoachingSlot(receiver);
                    if (!coachingAttempts.TryGetValue(slot, out var shot) || shot.contact != null) shot = CoachingAttempt(receiver, "Unknown");
                    shot.outcome = "Miss";
                }
            }
            else if (losingTeam == Rules.lastHitTeam && coachingContacts.TryGetValue(Rules.lastHitPlayer, out var shot))
            {
                if (reason == "Bóng ngoài sân") shot.outcome = "Out";
                if (reason == "Bóng không qua sân đối phương" && shot.netClearance < 0) shot.outcome = "Net";
            }
            foreach (var delivery in coachingRallyShots) coachingDeliveries.Enqueue(delivery);
            coachingRallyShots.Clear(); coachingCheckpointPending = true;
        }
        void FlushNetworkCoaching()
        {
            if (!IsServer || !captureCoaching || !coachingCheckpointPending && !coachingFinalPending) return;
            // Forfeit/disconnection can end a match before the current rally settles.
            foreach (var delivery in coachingRallyShots) coachingDeliveries.Enqueue(delivery);
            coachingRallyShots.Clear();
            var session = PhotonSession.Find(Runner);
            if (session.Channel != null && session.Channel.PendingCount >= 12) return;
            int budget = 2;
            while (coachingDeliveries.Count > 0 && budget-- > 0)
            {
                var delivery = coachingDeliveries.Peek();
#if UNITY_SERVER
                DedicatedWorker.Instance.Record(delivery.playerId, delivery.shot);
#else
                var peer = Runner.ActivePlayers.FirstOrDefault(p => MultiplayerApp.Instance.ClientIdentities.TryGetValue((ulong)p.RawEncoded, out var id) && id == delivery.playerId);
                if (peer != PlayerRef.None)
                    session.Send(peer, Object, "coaching-shot", new[] {JsonConvert.SerializeObject(delivery)});
#endif
                coachingDeliveries.Dequeue();
            }
            if (coachingDeliveries.Count > 0) return;
#if !UNITY_SERVER
            foreach (var peer in Runner.ActivePlayers)
                session.Send(peer, Object, "coaching-checkpoint", new object[] {coachingFinalPending});
#endif
            coachingCheckpointPending = coachingFinalPending = false;
        }
        void ReceiveCoachingShot(string json)
        {
            EnsureCoaching();
            if (coachingSession == null || coachingSession.closed) return;
            var delivery = JsonConvert.DeserializeObject<CoachingDelivery>(json);
            if (delivery?.shot == null || delivery.sessionId != coachingSession.sessionId ||
                delivery.playerId != coachingSession.playerId || coachingSession.shots.Any(s => s.shotId == delivery.shot.shotId)) return;
            coachingSession.shots.Add(delivery.shot);
        }
        void ReceiveCoachingCheckpoint(bool final)
        {
            EnsureCoaching();
            if (final) CoachingRuntime.Instance?.Close(coachingSession);
            else if (coachingSession != null) CoachingRuntime.Instance?.Checkpoint(coachingSession);
        }
        static float[] CoachingVector(Vector3 p) => new[] {p.x, p.y, p.z};
    }
}
