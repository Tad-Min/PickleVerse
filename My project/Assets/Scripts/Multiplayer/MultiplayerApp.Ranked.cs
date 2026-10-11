using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        public async Task PersistProgressionResultsAsync(string json)
        {
            var session=Match;
            if(session==null || !session.IsHost) return;
            try { session.AsHost().SetProperty("progressionResults",new Unity.Services.Multiplayer.SessionProperty(json,Unity.Services.Multiplayer.VisibilityPropertyOptions.Member)); await session.AsHost().SavePropertiesAsync(); }
            catch(Exception e) { Debug.LogWarning("Lưu kết quả phiên mạng: "+e.Message); }
        }
        public bool ApplyOnlineProgressionResults(string json)
        {
            if(string.IsNullOrEmpty(json) || !ProgressionRuntime.Exists) return false;
            var results=Newtonsoft.Json.JsonConvert.DeserializeObject<List<OnlineMatchResult>>(json);
            var found=results?.Find(r=>r.onlineId==PlayerId);
            if(found==null) return false;
            var current=ProgressionRuntime.Instance.Player;
            if(found.result.id!=current.pendingMatch?.id && found.result.id!=Roster?.matchId) return false;
            found.result.playerId=ProgressionRuntime.Instance.Player.id;
            bool applied=ProgressionRuntime.Instance.Apply(found.result);
            var saved=ProgressionRuntime.Instance.Player.history.Find(r=>r.id==found.result.id);
            if(saved!=null&&saved.Eligible&&!saved.localForfeit&&saved.outcome!=MatchOutcome.Interrupted)
                PickleVerse.Shop.PlayerEconomy.Reward("online:"+saved.id,true,saved.outcome==MatchOutcome.Won);
            return applied;
        }
        async Task ResolvePendingProgressionAsync(string reason)
        {
            if(!ProgressionRuntime.Exists || ProgressionRuntime.Instance.Player.pendingMatch==null) return;
            if(Match!=null)
            {
                for(int attempt=0;attempt<3 && ProgressionRuntime.Exists && ProgressionRuntime.Instance.Player.pendingMatch!=null;attempt++)
                {
                    var session=Match;
                    if(session==null) break;
                    try { await session.RefreshAsync(); if(session.Properties.TryGetValue("progressionResults",out var result)) ApplyOnlineProgressionResults(result.Value); }
                    catch(Exception e) { Debug.LogWarning("Đọc kết quả trận: "+e.Message); break; }
                    if(ProgressionRuntime.Exists && ProgressionRuntime.Instance.Player.pendingMatch!=null && attempt<2) await Task.Delay(1100);
                }
            }
            if(!ProgressionRuntime.Exists) return;
            var pending=ProgressionRuntime.Instance.Player.pendingMatch;
            if(pending==null) return;
            if(NetworkMatch.Instance!=null) NetworkMatch.Instance.RecordLocalExit(false,reason);
            else { pending.outcome=MatchOutcome.Interrupted; pending.reason=reason; pending.endedUtcTicks=DateTime.UtcNow.Ticks; ProgressionRuntime.Instance.Apply(pending); }
        }
        async Task SearchRankedSinglesAsync(int generation, CancellationToken token)
        {
            var qos = await groupMatcher.MeasureAsync(Settings.singlesQueue,Settings.maximumLatencyMs,token);
            token.ThrowIfCancellationRequested();
            var player = ProgressionRuntime.Instance.Player;
            var members = Members();
            var measuredQos = new Dictionary<string,List<Unity.Services.Matchmaker.Models.QosResult>> { [PlayerId]=qos };
            while (generation==operation && !token.IsCancellationRequested)
            {
                bool expanded = rankedSearch.Expanded(Time.realtimeSinceStartupAsDouble);
                var utc=DateTime.UtcNow;
                double phaseSeconds=EloPolicy.TicketPhaseRemaining(utc);
                if(!expanded) phaseSeconds=Math.Min(phaseSeconds,30-rankedSearch.Elapsed(Time.realtimeSinceStartupAsDouble));
                var keys=EloPolicy.TicketKeys(player.elo,player.rankLossStreak,expanded,EloPolicy.TicketPhase(utc));
                if(keys.Count==0) { await Task.Delay(Math.Max(1,(int)Math.Ceiling(phaseSeconds*1000)),token); continue; }
                using (var stage = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    stage.CancelAfter(Math.Max(1,(int)Math.Ceiling(phaseSeconds*1000)));
                    try
                    {
                        var result = await groupMatcher.FindAsync(Settings.singlesQueue,members,measuredQos,stage.Token,keys);
                        if (generation!=operation || token.IsCancellationRequested || !rankedSearch.ClaimHuman(Time.realtimeSinceStartupAsDouble)) return;
                        clock.Stop();
                        var roster = GroupMatchmaker.Roster(result.MatchId,result.MatchProperties.Region,result.MatchProperties.Teams,result.MatchProperties.Players,Mode);
                        await ConnectMatchAsync(roster,generation);
                        return;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && generation==operation)
                    { if(rankedSearch.Expanded(Time.realtimeSinceStartupAsDouble)) SetNotice("Đang tìm đối thủ trong khoảng ±120 Elo…"); }
                }
            }
        }
        async Task StartRankedBotAsync()
        {
            CancelLocal();
            int generation=operation;
            SetState(FlowState.Connecting,"Hết 6 phút; đang chuẩn bị đối thủ vào sân…");
            await LeaveMatchAsync();
            if (generation!=operation || rankedSearch.Decision!=SearchDecision.Bot) return;
            var player=ProgressionRuntime.Instance.Player;
            string botName; do { botName=identity.ResolveName(""); } while(botName==DisplayName);
            Roster=new MatchRoster {matchId=Guid.NewGuid().ToString("N"),mode=PlayMode.Singles,
                members=new List<RoomMember> {
                    new RoomMember {id=PlayerId,name=DisplayName,gender=Gender,elo=player.elo,team=0,group=PlayerId,ready=true},
                    new RoomMember {id="BOT"+Guid.NewGuid().ToString("N").Substring(0,21),name=botName,gender=UnityEngine.Random.value<.5f?"Male":"Female",elo=EloPolicy.BotElo(player.elo,player.rankLossStreak),team=1,group="BOT",ready=true,bot=true}
                }};
            SetConnectionIdentity();
            try
            {
                var session=await Unity.Services.Multiplayer.MultiplayerService.Instance.CreateSessionAsync(MatchOptions(2,null));
                if(generation!=operation) { await session.LeaveAsync(); return; }
                Match=session;
                session.IsLocked=true;
                await ConnectedMatchAsync(generation);
                if(generation==operation) SetNotice("Đối thủ "+botName+" đã sẵn sàng; đang vào sân…");
            }
            catch(Exception)
            {
                if(generation==operation) await ReturnToRoomAsync("Chưa kết nối được trận. Hãy tìm lại.");
                throw;
            }
        }
    }
}
