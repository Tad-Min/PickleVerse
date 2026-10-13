using System;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed partial class BotMatch
    {
        StrokeProgressTracker progressStrokes = new StrokeProgressTracker();
        readonly System.Random botAccuracyRandom = new System.Random();
        MatchResult progressionResult;
        bool progressionCompleted;
        void BeginProgressionMatch()
        {
            progressStrokes = new StrokeProgressTracker();
            if (!Application.isPlaying) return;
            var runtime = ProgressionRuntime.Instance; var player = runtime.Player;
            progressionCompleted = false;
            bool ranked = RankedBotLaunch.Active;
            if(ranked && botDifficulty==3)botDifficulty=1;
            if(botDifficulty==3&&!ranked){behaviorBot=GetComponent<BhTreeBot>()??gameObject.AddComponent<BhTreeBot>();behaviorBot.Initialize(this,player.elo);}
            progressionResult = new MatchResult
            {
                id = Guid.NewGuid().ToString("N"), playerId = player.id, startedUtcTicks = DateTime.UtcNow.Ticks,
                ownElo = player.elo, opponentElo = ranked ? RankedBotLaunch.OpponentElo : 100,
                kind = ranked ? MatchKind.RankedBot : MatchKind.BotPractice
            };
            progressionResult.opponents.Add(new OpponentInfo { id = "BOT", name = ranked ? "Bot rank" : "Bot", elo = progressionResult.opponentElo });
            runtime.BeginMatch(progressionResult);
            BeginReplay();
            ProgressionRuntime.Quitting -= OnProgressionQuit; ProgressionRuntime.Quitting += OnProgressionQuit;
        }
        void CaptureProgressionGame()
        {
            if (progressionResult == null || progressionResult.scores.Count >= Rules.GameNumber) return;
            progressionResult.scores.Add(new GameScore(Rules.Points[0], Rules.Points[1], true));
        }
        void CompleteProgressionMatch(bool forfeit)
        {
            if (progressionResult == null || progressionCompleted || !ProgressionRuntime.Exists) return;
            progressionCompleted = true;
            replay?.Event("MatchEnd",forfeit?"Forfeit":"Complete");
            replay?.Finish(!forfeit);
            progressionResult.outcome = forfeit ? MatchOutcome.Lost : Rules.Winner == 0 ? MatchOutcome.Won : MatchOutcome.Lost;
            progressionResult.localForfeit = forfeit;
            progressionResult.reason = forfeit ? "Chủ động rời hoặc chơi lại khi trận chưa kết thúc" : "";
            progressionResult.endedUtcTicks = DateTime.UtcNow.Ticks;
            progressionResult.gamesWon = Rules.Games[0]; progressionResult.gamesLost = Rules.Games[1];
            progressionResult.validReturns = progressStrokes.Returns(0); progressionResult.validServes = progressStrokes.Serves(0);
            if (forfeit && !Rules.GameFinished) progressionResult.scores.Add(new GameScore(Rules.Points[0], Rules.Points[1], false));
            ProgressionRuntime.Instance.Apply(progressionResult); RankedBotLaunch.Clear();
        }
        void OnProgressionQuit() { CompleteProgressionMatch(true); }
        void OnDestroy()
        {
            ProgressionRuntime.Quitting -= OnProgressionQuit;
            if (Application.isPlaying) CompleteProgressionMatch(true);
        }
    }
}
