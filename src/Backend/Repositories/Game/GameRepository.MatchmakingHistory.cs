using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Caching;

namespace Repositories.Game;
public sealed partial class GameRepository
{
    public Task<MatchmakingSearchRecord?> SearchRecord(Guid search) => db.MatchmakingSearches.AsNoTracking().SingleOrDefaultAsync(x=>x.SearchId==search);
    public Task<List<MatchmakingSearchRecord>> StaleSearches(DateTime cutoff) => db.MatchmakingSearches.AsNoTracking()
        .Where(x=>x.State==MatchmakingSearchStatus.Searching && x.LastSeenAt<cutoff).OrderBy(x=>x.LastSeenAt).Take(100).ToListAsync();
    public Task RecordSearch(QueueEntry entry,int leaseSeconds)
    {
        var state=entry.state;bool cancelledAfter=state is MatchmakingSearchStatus.Cancelled or MatchmakingSearchStatus.TimedOut && entry.assigned>0;
        if(cancelledAfter)state=entry.roster?.RankedBot==true?MatchmakingSearchStatus.Bot:MatchmakingSearchStatus.Matched;
        DateTime started=DateTimeOffset.FromUnixTimeMilliseconds(entry.started).UtcDateTime,last=DateTimeOffset.FromUnixTimeMilliseconds(entry.lastSeen).UtcDateTime;
        DateTime? ended=state==MatchmakingSearchStatus.Searching?null:entry.assigned>0?DateTimeOffset.FromUnixTimeMilliseconds(entry.assigned).UtcDateTime
            :entry.state==MatchmakingSearchStatus.TimedOut?last.AddSeconds(leaseSeconds):last;
        long? wait=ended==null?null:Math.Max(0,(long)(ended.Value-started).TotalMilliseconds);
        Guid? match=Guid.TryParse(entry.matchId,out var id)?id:null;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO matchmakingsearches(searchid,playerid,mode,region,protocol,state,startedat,lastseenat,endedat,waitmilliseconds,matchid,cancelledafterassignment)
            VALUES({Guid.Parse(entry.searchId)},{Guid.Parse(entry.playerId)},{entry.mode},{entry.region},{entry.protocol},{StatusWire.Format(state)},
                {started},{last},{ended},{wait},{match},{cancelledAfter})
            ON CONFLICT(searchid) DO UPDATE SET lastseenat=GREATEST(matchmakingsearches.lastseenat,EXCLUDED.lastseenat),
                state=CASE WHEN matchmakingsearches.state='SEARCHING' THEN EXCLUDED.state ELSE matchmakingsearches.state END,
                endedat=COALESCE(matchmakingsearches.endedat,EXCLUDED.endedat),waitmilliseconds=COALESCE(matchmakingsearches.waitmilliseconds,EXCLUDED.waitmilliseconds),
                matchid=COALESCE(matchmakingsearches.matchid,EXCLUDED.matchid),
                cancelledafterassignment=matchmakingsearches.cancelledafterassignment OR EXCLUDED.cancelledafterassignment
            WHERE matchmakingsearches.playerid=EXCLUDED.playerid
            """);
    }
    public Task TimeoutMissingSearch(Guid search,DateTime cutoff,int leaseSeconds) => db.MatchmakingSearches
        .Where(x=>x.SearchId==search && x.State==MatchmakingSearchStatus.Searching && x.LastSeenAt<cutoff)
        .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.State,MatchmakingSearchStatus.TimedOut).SetProperty(x=>x.EndedAt,x=>x.LastSeenAt.AddSeconds(leaseSeconds))
            .SetProperty(x=>x.WaitMilliseconds,x=>(long)(x.LastSeenAt.AddSeconds(leaseSeconds)-x.StartedAt).TotalMilliseconds));
}
