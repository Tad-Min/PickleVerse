using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed partial class BotMatch
    {
        StrokeProgressTracker progressStrokes = new StrokeProgressTracker();
        readonly System.Random botAccuracyRandom = new System.Random();
        readonly BotContactChance rankedBotContact = new BotContactChance();
        MatchResult progressionResult;
        bool progressionCompleted;
        Task beginningProgression=Task.CompletedTask,savingProgression=Task.CompletedTask;
        string startRequestId,finishRequestId;
        MatchResult resultToSave;
        bool startingProgression;
        public bool BotMissedContact(MatchActor actor) => actor.playerId == 1 &&
            progressionResult?.kind == MatchKind.RankedBot && rankedBotContact.Missed(Rules.Stroke);
        bool TryRankedBotContact(MatchActor actor)
        {
            if(actor.playerId != 1 || progressionResult?.kind != MatchKind.RankedBot) return true;
            bool hit=rankedBotContact.Resolve(botAccuracyRandom,progressionResult.opponentElo,Rules.Stroke);
            if(!hit) { actor.MissContact(); replay?.Event("Miss",actor.playerId.ToString()); }
            return hit;
        }
        void BeginProgressionMatch()
        {
            progressStrokes = new StrokeProgressTracker();
            if (!Application.isPlaying) return;
            var runtime = ProgressionRuntime.Instance; var player = runtime.Player;
            progressionCompleted = false;
            bool ranked = RankedBotLaunch.Active;
            if(ranked && botDifficulty==3)botDifficulty=1;
            if(botDifficulty==3&&!ranked){behaviorBot=GetComponent<BhTreeBot>()??gameObject.AddComponent<BhTreeBot>();behaviorBot.Initialize(this,player.elo);}
            progressionResult=null;startRequestId=Guid.NewGuid().ToString("N");
            beginningProgression=StartProgressionAsync(ranked?MatchKind.RankedBot:MatchKind.BotPractice);
            ProgressionRuntime.Quitting -= OnProgressionQuit; ProgressionRuntime.Quitting += OnProgressionQuit;
        }
        async Task StartProgressionAsync(MatchKind kind)
        {
            startingProgression=true;
            try
            {
                await savingProgression;
                var snapshot=await ProgressionRuntime.Instance.StartBotAsync(kind,startRequestId);
                if(!this)return;
                progressionResult=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(snapshot.player.pendingMatch));
                if(progressionResult==null)throw new InvalidOperationException("Máy chủ chưa xác nhận mã trận.");
                resultToSave=null;finishRequestId=null;BeginReplay();
            }
            catch(Exception error){if(this)Message="Chưa tạo được trận online: "+error.Message+" • Bấm Chơi lại để thử lại.";throw;}
            finally{if(this)startingProgression=false;}
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
            resultToSave=JsonConvert.DeserializeObject<MatchResult>(JsonConvert.SerializeObject(progressionResult));
            finishRequestId=Guid.NewGuid().ToString("N");
            savingProgression=SaveProgressionAsync();RankedBotLaunch.Clear();
        }
        async Task SaveProgressionAsync()
        {
            var payload=resultToSave;string request=finishRequestId;
            if(payload==null)return;
            for(int attempt=0;;attempt++)
            {
                try{await ProgressionRuntime.Instance.FinishBotAsync(payload,request);return;}
                catch(Exception error)
                {
                    if(this)Message="Chưa lưu được kết quả online: "+error.Message+" • Chơi lại hoặc Thoát sẽ thử gửi lại.";
                    if(attempt>=2)throw;
                    await Task.Delay(1000*(attempt+1));
                }
            }
        }
        async Task FlushProgressionAsync()
        {
            try{await beginningProgression;}catch{ }
            if(resultToSave!=null&&(savingProgression.IsFaulted||savingProgression.IsCanceled))savingProgression=SaveProgressionAsync();
            await savingProgression;
        }
        void OnProgressionQuit() { CompleteProgressionMatch(true); }
        void OnDestroy()
        {
            ProgressionRuntime.Quitting -= OnProgressionQuit;
            if (Application.isPlaying) CompleteProgressionMatch(true);
        }
    }
}
