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
        [Networked,Capacity(4096)]public string ProgressionResults{get;set;}="";
        [Networked]public long StartedUtc{get;set;}
        [Networked,Capacity(512)]public string CompletedGameScores{get;set;}="[]";
        readonly StrokeProgressTracker progressStrokes = new StrokeProgressTracker();
        readonly HashSet<string> forfeitedPlayers = new HashSet<string>();
        readonly List<GameScore> teamGameScores = new List<GameScore>();
        readonly int[] teamPoints = new int[2];
        MatchResult localProgression;
        void BindProgression()
        {
            if(IsServer) StartedUtc=DateTime.UtcNow.Ticks;

            ProgressionRuntime.Quitting+=OnProgressionQuit;
            ResultsAvailable(default,ProgressionResults);
        }
        void UnbindProgression()
        { ProgressionRuntime.Quitting-=OnProgressionQuit; }
        MatchResult Template(RoomMember member)
        {
            var enemies=Roster.members.Where(m=>m.team!=member.team).ToList();
            return new MatchResult {
                id=Roster.matchId, startedUtcTicks=StartedUtc,
                kind=Roster.custom?MatchKind.Custom:Roster.mode==PlayMode.Singles?MatchKind.RankedSingles:MatchKind.MatchedDoubles,
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
        { if(teamGameScores.Count<Rules.GameNumber) { teamGameScores.Add(new GameScore(Rules.score[0],Rules.score[1],true)); CompletedGameScores=JsonConvert.SerializeObject(teamGameScores); } }
        void PublishProgressionResults()
        {
            if(!IsServer || !string.IsNullOrEmpty(ProgressionResults)) return;
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
            var envelope=JsonConvert.SerializeObject(completed);
            ProgressionResults=envelope;
            ResultsAvailable(default,envelope);
            _=MultiplayerApp.Instance.PersistProgressionResultsAsync(envelope);
        }
        void ResultsAvailable(string old,string value)
        { if(Application.isPlaying) MultiplayerApp.Instance.ApplyOnlineProgressionResults(value); }
        public void RecordLocalExit(bool forfeit,string reason)
        {
            if(!Started || Finished || !ProgressionRuntime.Exists) return;
            BeginLocalProgression();
            if(localProgression==null) return;
            var r=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(localProgression));
            int team=Roster.Find(MultiplayerApp.Instance.PlayerId).team;
            r.endedUtcTicks=DateTime.UtcNow.Ticks; r.outcome=forfeit?MatchOutcome.Lost:MatchOutcome.Interrupted;
            r.localForfeit=forfeit; r.reason=reason; r.gamesWon=team==0?GamesA:GamesB; r.gamesLost=team==0?GamesB:GamesA;
            var completed=JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores??"[]");
            r.scores=completed.Select(s=>new GameScore(team==0?s.own:s.opponent,team==0?s.opponent:s.own,true)).ToList();
            r.scores.Add(new GameScore(team==0?ScoreA:ScoreB,team==0?ScoreB:ScoreA,false));
            ProgressionRuntime.Instance.Apply(r);
        }
        void SnapshotPendingProgression()
        {
            if(!Started || Finished || !ProgressionRuntime.Exists || Roster==null) return;
            var runtime=ProgressionRuntime.Instance; var pending=runtime.Player.pendingMatch;
            if(pending==null || pending.id!=Roster.matchId) return;
            var member=Roster.Find(MultiplayerApp.Instance.PlayerId); if(member==null) return;
            var snapshot=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(pending)); int team=member.team;
            snapshot.gamesWon=team==0?GamesA:GamesB; snapshot.gamesLost=team==0?GamesB:GamesA;
            var completed=JsonConvert.DeserializeObject<List<GameScore>>(CompletedGameScores??"[]")??new List<GameScore>();
            snapshot.scores=completed.Select(s=>new GameScore(team==0?s.own:s.opponent,team==0?s.opponent:s.own,true)).ToList();
            if(!BetweenGames) snapshot.scores.Add(new GameScore(team==0?ScoreA:ScoreB,team==0?ScoreB:ScoreA,false));
            runtime.BeginMatch(snapshot);
        }
        void OnProgressionQuit() { if(IsSpawned) RequestLeave(); }
    }
}
