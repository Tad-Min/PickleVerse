using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using PickleVerse.Progression;
using Unity.Netcode;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct ProgressionEnvelope : INetworkSerializable, IEquatable<ProgressionEnvelope>
    {
        public string json;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter { serializer.SerializeValue(ref json); }
        public bool Equals(ProgressionEnvelope other) => json==other.json;
    }
    [Serializable] public sealed class OnlineMatchResult
    {
        public string onlineId;
        public MatchResult result;
    }
    public sealed partial class NetworkMatch
    {
        public NetworkVariable<ProgressionEnvelope> ProgressionResults = new NetworkVariable<ProgressionEnvelope>(new ProgressionEnvelope { json="" });
        public NetworkVariable<long> StartedUtc = new NetworkVariable<long>();
        public NetworkVariable<ProgressionEnvelope> CompletedGameScores = new NetworkVariable<ProgressionEnvelope>(new ProgressionEnvelope {json="[]"});
        readonly StrokeProgressTracker progressStrokes = new StrokeProgressTracker();
        readonly HashSet<string> forfeitedPlayers = new HashSet<string>();
        readonly List<GameScore> teamGameScores = new List<GameScore>();
        readonly int[] teamPoints = new int[2];
        MatchResult localProgression;
        void BindProgression()
        {
            if(IsServer) StartedUtc.Value=DateTime.UtcNow.Ticks;
            ProgressionResults.OnValueChanged+=ResultsAvailable;
            ProgressionRuntime.Quitting+=OnProgressionQuit;
            ResultsAvailable(default,ProgressionResults.Value);
        }
        void UnbindProgression()
        { ProgressionResults.OnValueChanged-=ResultsAvailable; ProgressionRuntime.Quitting-=OnProgressionQuit; }
        MatchResult Template(RoomMember member)
        {
            var enemies=Roster.members.Where(m=>m.team!=member.team).ToList();
            return new MatchResult {
                id=Roster.matchId, startedUtcTicks=StartedUtc.Value,
                kind=Roster.custom?MatchKind.Custom:Roster.RankedBot?MatchKind.RankedBot:Roster.mode==PlayMode.Singles?MatchKind.RankedSingles:MatchKind.MatchedDoubles,
                ownElo=member.elo, opponentElo=enemies[0].elo,
                opponents=enemies.Select(m=>new OpponentInfo {id=m.id,name=m.name,elo=m.elo}).ToList()
            };
        }
        void BeginLocalProgression()
        {
            if(localProgression!=null || !Application.isPlaying) return;
            var app=MultiplayerApp.Instance; var member=Roster.Find(app.PlayerId);
            if(member==null) return;
            localProgression=Template(member); localProgression.playerId=ProgressionRuntime.Instance.Player.id;
            ProgressionRuntime.Instance.BeginMatch(localProgression);
        }
        void CaptureProgressionGame()
        { if(teamGameScores.Count<Rules.GameNumber) { teamGameScores.Add(new GameScore(Rules.score[0],Rules.score[1],true)); CompletedGameScores.Value=new ProgressionEnvelope {json=JsonConvert.SerializeObject(teamGameScores)}; } }
        void PublishProgressionResults()
        {
            if(!IsServer || !string.IsNullOrEmpty(ProgressionResults.Value.json)) return;
            var completed=new List<OnlineMatchResult>();
            foreach(var member in Roster.members)
            {
                var r=Template(member); r.endedUtcTicks=DateTime.UtcNow.Ticks;
                r.outcome=Rules.winner==member.team?MatchOutcome.Won:MatchOutcome.Lost;
                r.localForfeit=forfeitedPlayers.Contains(member.id); r.reason=Rules.reason;
                r.gamesWon=Rules.games[member.team]; r.gamesLost=Rules.games[1-member.team]; r.points=teamPoints[member.team];
                int index=Roster.members.Where(m=>m.team==member.team).ToList().FindIndex(m=>m.id==member.id);
                r.validReturns=progressStrokes.Returns(member.team*2+index); r.validServes=progressStrokes.Serves(member.team*2+index);
                r.scores=teamGameScores.Select(s=>new GameScore(member.team==0?s.own:s.opponent,member.team==0?s.opponent:s.own,true)).ToList();
                if(!Rules.gameFinished) r.scores.Add(new GameScore(Rules.score[member.team],Rules.score[1-member.team],false));
                completed.Add(new OnlineMatchResult {onlineId=member.id,result=r});
            }
            var envelope=new ProgressionEnvelope {json=JsonConvert.SerializeObject(completed)};
            ProgressionResults.Value=envelope;
            ResultsAvailable(default,envelope);
            _=MultiplayerApp.Instance.PersistProgressionResultsAsync(envelope.json);
        }
        void ResultsAvailable(ProgressionEnvelope old,ProgressionEnvelope value)
        { if(Application.isPlaying) MultiplayerApp.Instance.ApplyOnlineProgressionResults(value.json); }
        public void RecordLocalExit(bool forfeit,string reason)
        {
            if(!Started.Value || Finished.Value || !ProgressionRuntime.Exists) return;
            BeginLocalProgression();
            if(localProgression==null) return;
            var r=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(localProgression));
            int team=Roster.Find(MultiplayerApp.Instance.PlayerId).team;
            r.endedUtcTicks=DateTime.UtcNow.Ticks; r.outcome=forfeit?MatchOutcome.Lost:MatchOutcome.Interrupted;
            r.localForfeit=forfeit; r.reason=reason; r.gamesWon=team==0?GamesA.Value:GamesB.Value; r.gamesLost=team==0?GamesB.Value:GamesA.Value;
            var completed=JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores.Value.json??"[]");
            r.scores=completed.Select(s=>new GameScore(team==0?s.own:s.opponent,team==0?s.opponent:s.own,true)).ToList();
            r.scores.Add(new GameScore(team==0?ScoreA.Value:ScoreB.Value,team==0?ScoreB.Value:ScoreA.Value,false));
            ProgressionRuntime.Instance.Apply(r);
        }
        void SnapshotPendingProgression()
        {
            if(!Started.Value || Finished.Value || !ProgressionRuntime.Exists || Roster==null) return;
            var runtime=ProgressionRuntime.Instance; var pending=runtime.Player.pendingMatch;
            if(pending==null || pending.id!=Roster.matchId) return;
            var member=Roster.Find(MultiplayerApp.Instance.PlayerId); if(member==null) return;
            var snapshot=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(pending)); int team=member.team;
            snapshot.gamesWon=team==0?GamesA.Value:GamesB.Value; snapshot.gamesLost=team==0?GamesB.Value:GamesA.Value;
            var completed=JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores.Value.json??"[]")??new List<GameScore>();
            snapshot.scores=completed.Select(s=>new GameScore(team==0?s.own:s.opponent,team==0?s.opponent:s.own,true)).ToList();
            if(!BetweenGames.Value) snapshot.scores.Add(new GameScore(team==0?ScoreA.Value:ScoreB.Value,team==0?ScoreB.Value:ScoreA.Value,false));
            runtime.BeginMatch(snapshot);
        }
        void OnProgressionQuit() { if(IsSpawned) RequestLeave(); }
    }
}
