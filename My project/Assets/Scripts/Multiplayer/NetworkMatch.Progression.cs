using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PickleVerse.Progression;
using Fusion;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        // End-of-match JSON is streamed once, rather than reserved in every snapshot.
        public string ProgressionResults { get; private set; } = "";
        [Networked]
        public long StartedUtc { get; set; }
        [Networked]
        [Capacity(512)]
        public string CompletedGameScores { get; set; } = "[]";
        readonly StrokeProgressTracker progressStrokes = new StrokeProgressTracker();
        readonly HashSet<string> forfeitedPlayers = new HashSet<string>();
        readonly List<GameScore> teamGameScores = new List<GameScore>();
        [Networked]
        public int GameStartSide { get; set; } = -1;
        readonly int[] teamPoints = new int[2];
        CoachingSession coachingSession;
        void EnsureCoaching()
        {
#if UNITY_SERVER
            return;
#else
            if (PhotonSession.Find(Runner)?.IsDedicated == true) return;
            if (coachingSession != null || !Started || Roster == null || !ProgressionRuntime.Exists) return;
            if (ProgressionRuntime.Instance.Snapshot?.catalog?.hybridTelemetryEnabled != true) return;
            var local = NetworkPlayer.All.FirstOrDefault(p => p && p.IsLocalHuman && p.Actor);
            if (!local) return;
            coachingSession = CoachingRuntime.Instance.Begin(Roster.matchId, "match");
#endif
        }
        MatchResult localProgression;
        void BindProgression()
        {
            if (IsServer)
                StartedUtc = DateTime.UtcNow.Ticks;

            ProgressionRuntime.Quitting += OnProgressionQuit;
            ResultsAvailable(default, ProgressionResults);
        }
        void UnbindProgression()
        {
            ProgressionRuntime.Quitting -= OnProgressionQuit;
        }
        MatchResult Template(RoomMember member)
        {
            var enemies = Roster.members.Where(m => m.team != member.team).ToList();
            return new MatchResult
            {
                id = Roster.matchId,
                playerId = member.id,
                startedUtcTicks = StartedUtc,
                kind = Roster.custom ? MatchKind.Custom : Roster.mode == PlayMode.Singles ? MatchKind.RankedSingles : MatchKind.MatchedDoubles,
                ownElo = member.elo,
                opponentElo = enemies[0].elo,
                opponents = enemies.Select(m => new OpponentInfo { id = m.id, name = m.name, elo = m.elo }).ToList()
            };
        }
        void BeginLocalProgression()
        {
#if UNITY_SERVER
            return;
#else
            if (localProgression != null || !Application.isPlaying)
                return;
            var app = MultiplayerApp.Instance;
            var member = Roster.Find(app.PlayerId);
            if (member == null)
                return;
            localProgression = Template(member);
            localProgression.playerId = ProgressionRuntime.Instance.Player.id;
            ProgressionRuntime.Instance.BeginMatch(localProgression);
#endif
        }
        void CaptureProgressionGame()
        {
            if (teamGameScores.Count < Rules.GameNumber)
            {
                teamGameScores.Add(new GameScore(Rules.score[0], Rules.score[1], true, GameStartSide));
                CompletedGameScores = JsonConvert.SerializeObject(teamGameScores);
            }
        }
        void PublishProgressionResults()
        {
            if (!IsServer || !string.IsNullOrEmpty(ProgressionResults))
                return;
            var completed = new List<OnlineMatchResult>();
            foreach (var member in Roster.members)
            {
                var r = Template(member);
                r.endedUtcTicks = DateTime.UtcNow.Ticks;
                r.outcome = Rules.winner == member.team ? MatchOutcome.Won : MatchOutcome.Lost;
                r.localForfeit = forfeitedPlayers.Contains(member.id);
                r.reason = Rules.reason;
                r.gamesWon = Rules.games[member.team];
                r.gamesLost = Rules.games[1 - member.team];
                r.points = teamPoints[member.team];
                int index = Roster.members.Where(m => m.team == member.team).ToList().FindIndex(m => m.id == member.id);
                r.validReturns = progressStrokes.Returns(member.team * 2 + index);
                r.validServes = progressStrokes.Serves(member.team * 2 + index);
                r.scores = teamGameScores.Select(s => new GameScore(member.team == 0 ? s.own : s.opponent, member.team == 0 ? s.opponent : s.own, true, s.teamAStartSide)).ToList();
                if (!Rules.gameFinished)
                    r.scores.Add(new GameScore(Rules.score[member.team], Rules.score[1 - member.team], false, GameStartSide));
                completed.Add(new OnlineMatchResult { onlineId = member.id, result = r });
            }
            var envelope = JsonConvert.SerializeObject(completed);
#if UNITY_SERVER
            DedicatedWorker.Instance.SubmitResults(envelope);
#else
            DeliverProgressionResults(envelope);
#endif
        }
        internal void DeliverProgressionResults(string envelope)
        {
            ProgressionResults = envelope;
            ResultsAvailable(default, envelope);
            SendProgressionResultsToPeers();
        }
        void ResultsAvailable(string old, string value)
        {
#if !UNITY_SERVER
            // The reliable result can reach a client that missed the final state snapshot.
            if (Application.isPlaying && MultiplayerApp.Instance)
                MultiplayerApp.Instance.ApplyOnlineProgressionResults(this, value);
#endif
        }
        public void RecordLocalExit(bool forfeit, string reason)
        {
            CoachingRuntime.Instance?.Close(coachingSession, true);
            if (!Started || Finished || !ProgressionRuntime.Exists)
                return;
            BeginLocalProgression();
            if (localProgression == null)
                return;
            var r = JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(localProgression));
            int team = Roster.Find(MultiplayerApp.Instance.PlayerId).team;
            r.endedUtcTicks = DateTime.UtcNow.Ticks;
            r.outcome = forfeit ? MatchOutcome.Lost : MatchOutcome.Interrupted;
            r.localForfeit = forfeit;
            r.reason = reason;
            r.gamesWon = team == 0 ? GamesA : GamesB;
            r.gamesLost = team == 0 ? GamesB : GamesA;
            var completed = JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores ?? "[]");
            r.scores = completed.Select(s => new GameScore(team == 0 ? s.own : s.opponent, team == 0 ? s.opponent : s.own, true, s.teamAStartSide)).ToList();
            r.scores.Add(new GameScore(team == 0 ? ScoreA : ScoreB, team == 0 ? ScoreB : ScoreA, false, GameStartSide));
            ProgressionRuntime.Instance.Apply(r);
        }
        void SnapshotPendingProgression()
        {
            if (!Started || Finished || !ProgressionRuntime.Exists || Roster == null)
                return;
            var runtime = ProgressionRuntime.Instance;
            var pending = runtime.Player.pendingMatch;
            if (pending == null || pending.id != Roster.matchId)
                return;
            var member = Roster.Find(MultiplayerApp.Instance.PlayerId);
            if (member == null)
                return;
            var snapshot = JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(pending));
            int team = member.team;
            snapshot.gamesWon = team == 0 ? GamesA : GamesB;
            snapshot.gamesLost = team == 0 ? GamesB : GamesA;
            var completed = JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores ?? "[]") ?? new List<GameScore>();
            snapshot.scores = completed.Select(s => new GameScore(team == 0 ? s.own : s.opponent, team == 0 ? s.opponent : s.own, true, s.teamAStartSide)).ToList();
            if (!BetweenGames)
                snapshot.scores.Add(new GameScore(team == 0 ? ScoreA : ScoreB, team == 0 ? ScoreB : ScoreA, false, GameStartSide));
            runtime.BeginMatch(snapshot);
        }
        void OnProgressionQuit()
        {
            // Dedicated reserves the seat on disconnect. Only the explicit leave action forfeits it.
            if (IsSpawned && PhotonSession.Find(Runner)?.IsDedicated != true)
                RequestLeave();
        }
    }
}
