using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Progression;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        [Serializable]sealed class MatchTicket{public MatchRoster roster;public bool allAccepted;public string phase;}
        MatchRoster canonicalRoster;
        readonly Dictionary<string,Task> resultWrites=new Dictionary<string,Task>();
        bool heartbeatBusy;
        double nextHeartbeat;
        void TickOnlineHeartbeat()
        {
            var runtime=ProgressionRuntime.Instance;var pending=runtime.Ready?runtime.Player.pendingMatch:null;
            if(heartbeatBusy||pending==null||pending.Bot||Match==null||Time.realtimeSinceStartupAsDouble<nextHeartbeat)return;
            heartbeatBusy=true;nextHeartbeat=Time.realtimeSinceStartupAsDouble+8;_=HeartbeatAsync(pending.id);
        }
        async Task HeartbeatAsync(string matchId)
        {
            try{await ProgressionRuntime.Instance.CommandAsync("match/online/heartbeat",new{MatchId=matchId,RequestId=Guid.NewGuid().ToString("N")});}
            catch(Exception error){if(!quitting)SetNotice("Chưa đồng bộ trạng thái trận: "+error.Message);}
            finally{heartbeatBusy=false;}
        }
        object OnlineRequest()=>new{Roster=Match.Data.Roster(Match.Runner.SessionInfo.Region),Session=Match.Id,
            HostPlayerId=Match.Runner.GetPlayerUserId(Match.Lobby.Object.StateAuthority)};
        async Task PrepareOnlineMatchAsync()
        {
            var match=Match;
            var ticket=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<MatchTicket>("POST","api/Game/match/online/prepare",OnlineRequest());
            if(Match!=match)throw new OperationCanceledException();canonicalRoster=ticket.roster;Roster=canonicalRoster;
        }
        async Task StartOnlineMatchAsync()
        {
            var match=Match;
            var ticket=await AccountRuntime.Ensure().Account.SendAuthorizedAsync<MatchTicket>("POST","api/Game/match/online/start",OnlineRequest());
            if(Match!=match)throw new OperationCanceledException();canonicalRoster=ticket.roster;Roster=canonicalRoster;await ProgressionRuntime.Instance.ReloadAsync();
        }
        public Task PersistProgressionResultsAsync(string json)
        {
            var results=JsonConvert.DeserializeObject<List<OnlineMatchResult>>(json);
            var own=results?.SingleOrDefault(r=>r.onlineId==PlayerId)?.result;
            if(own==null||own.id!=Roster?.matchId)throw new InvalidOperationException("Kết quả không thuộc trận hiện tại.");
            own.playerId=PlayerId;
            if(!resultWrites.TryGetValue(own.id,out var task)||task.IsFaulted||task.IsCanceled){task=SaveOnlineResultAsync(own);resultWrites[own.id]=task;}
            return task;
        }
        async Task SaveOnlineResultAsync(MatchResult result)
        {
            try
            {
                if(result.localForfeit)
                    await ProgressionRuntime.Instance.CommandAsync("match/online/leave",new{MatchId=result.id,RequestId=Guid.NewGuid().ToString("N")});
                else if(result.scores.All(g=>g.completed)&&Math.Max(result.gamesWon,result.gamesLost)==2)
                    await ProgressionRuntime.Instance.CommandAsync("match/online/report",new{Result=result,RequestId=Guid.NewGuid().ToString("N")});
                else await ProgressionRuntime.Instance.ReloadAsync();
                for(int i=0;i<10&&ProgressionRuntime.Instance.Player.pendingMatch?.id==result.id;i++)
                {await Task.Delay(1000);await ProgressionRuntime.Instance.ReloadAsync();}
            }
            catch(Exception error){SetNotice("Chưa lưu kết quả online: "+error.Message);throw;}
        }
        public bool ApplyOnlineProgressionResults(string json)
        {
            if(string.IsNullOrEmpty(json))return false;
            _=Safe(PersistProgressionResultsAsync(json));return true;
        }
        async Task ForfeitOnlineMatchAsync()
        {
            if(Roster==null||!ProgressionRuntime.Instance.Ready||ProgressionRuntime.Instance.Player.pendingMatch?.id!=Roster.matchId)return;
            await ProgressionRuntime.Instance.CommandAsync("match/online/leave",new{MatchId=Roster.matchId,RequestId=Guid.NewGuid().ToString("N")});
        }
    }
}
