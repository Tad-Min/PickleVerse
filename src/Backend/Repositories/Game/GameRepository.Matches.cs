using System.Linq.Expressions;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<Gamematch?> Match(Expression<Func<Gamematch, bool>> predicate) => MatchQueries.Query(db).SingleOrDefaultAsync(predicate);
    public void AddMatch(Gamematch match) => db.Gamematches.Add(match);
    public void AddParticipant(Gamematchplayer participant) => db.Gamematchplayers.Add(participant);
    public Task<List<Gamematch>> MatchHistory(Guid player) => MatchQueries.Query(db)
        .Where(m => m.Status == "FINISHED" && m.Gamematchplayers.Any(p => p.Playerid == player && p.Finalizedat != null))
        .OrderByDescending(m => m.Endedat).ThenByDescending(m => m.Sequence).Take(20).ToListAsync();
    public Task<List<Guid>> ProcessedMatches(Guid player) => db.Gamematchplayers.AsNoTracking()
        .Where(p => p.Playerid == player && p.Finalizedat != null && p.Match.Status == "FINISHED").Select(p => p.Matchid).ToListAsync();
    public Task<List<DateTime>> PlayedDays(Guid player, DateTime enabled) => db.Gamematchplayers.AsNoTracking()
        .Where(p => p.Playerid == player && p.Finalizedat != null && p.Match.Status == "FINISHED"
            && (p.Outcome == "WIN" || p.Outcome == "LOSE") && !p.Localforfeit && p.Match.Endedat >= enabled)
        .Select(p => p.Match.Endedat!.Value.Date).Distinct().OrderBy(d => d).ToListAsync();
    public Task<Gamematch?> PendingMatch(Guid player) => MatchQueries.Query(db)
        .Where(m => m.Status == "PLAYING" && m.Gamematchplayers.Any(p => p.Playerid == player && (m.Kind != "BotPractice" || p.Isreplaywriter)))
        .OrderByDescending(m => m.Createdat).ThenByDescending(m => m.Matchid).FirstOrDefaultAsync();
    public Task<Guid?> PendingOnline(Guid player) => db.Gamematches.AsNoTracking()
        .Where(m => m.Status == "PLAYING" && m.Kind != "BotPractice" && m.Gamematchplayers.Any(p => p.Playerid == player))
        .OrderBy(m => m.Createdat).Select(m => (Guid?)m.Matchid).FirstOrDefaultAsync();
    public Task<bool> MatchEnded(Guid match) => db.Gamematches.AnyAsync(m => m.Matchid == match && (m.Status == "FINISHED" || m.Status == "CANCELLED"));
    public Task<DateTime> MatchCreated(Guid match) => db.Gamematches.Where(m => m.Matchid == match).Select(m => m.Createdat).SingleAsync();
    public Task<bool> InMatch(Guid player, DateTime readyAfter) => db.Gamematches.AnyAsync(m => (m.Status == "PLAYING" || m.Status == "PREPARING" && m.Createdat >= readyAfter)
        && m.Gamematchplayers.Any(p => p.Playerid == player));
    public Task CancelPreparation(Guid match, DateTime now) => db.Gamematches.Where(m => m.Matchid == match && m.Status == "PREPARING")
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "CANCELLED").SetProperty(m => m.Updatedat, now));
    public Task SaveOnline(Guid match, string phase, DateTime? started, DateTime now) => db.Gamematches.Where(m => m.Matchid == match)
        .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, phase).SetProperty(m => m.Updatedat, now).SetProperty(m => m.Startedat, m => m.Startedat ?? started));
    public Task TouchParticipants(Guid match, Guid? player, DateTime now) => db.Gamematchplayers.Where(p => p.Matchid == match && (player == null || p.Playerid == player))
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Lastseenat, now));
    public Task<int> FinalizeParticipant(Guid match, Guid player, string outcome, bool forfeit, int returns, int serves, string reason, DateTime now) =>
        db.Gamematchplayers.Where(p => p.Matchid == match && p.Playerid == player && p.Finalizedat == null)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Outcome, outcome).SetProperty(p => p.Localforfeit, forfeit)
            .SetProperty(p => p.Finalizedat, now).SetProperty(p => p.Validreturns, returns).SetProperty(p => p.Validserves, serves)
            .SetProperty(p => p.Endreason, reason).SetProperty(p => p.Reportedat, p => p.Reportedat ?? now));
    public async Task<int> FinalizeMatch(Guid match, DateTime now)
    {
        var sequence = await db.Database.SqlQueryRaw<long>("""SELECT nextval('gamematches_history_seq') AS "Value" """).SingleAsync();
        return await db.Gamematches.Where(m => m.Matchid == match && m.Status == "PLAYING")
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "FINISHED").SetProperty(m => m.Endedat, now)
                .SetProperty(m => m.Updatedat, now).SetProperty(m => m.Sequence, m => m.Sequence ?? sequence));
    }
    public Task SaveSubmittedResult(Guid match, Guid player, int returns, int serves, DateTime at) =>
        db.Gamematchplayers.Where(p => p.Matchid == match && p.Playerid == player && p.Finalizedat == null)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Validreturns, returns).SetProperty(p => p.Validserves, serves)
            .SetProperty(p => p.Reportedat, at).SetProperty(p => p.Lastseenat, at));
    public Task SaveScores(Guid match, IReadOnlyList<GameScore> scores) => MatchScoreStore.Save(db, match, scores);
    public Task RecordRating(Guid player, Guid match, int before, int delta, int opponent, string policy) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO ratingledger(playerid,matchid,beforeelo,afterelo,opponentelo,delta,policyversion)
        VALUES({player},{match},{before},{before + delta},{opponent},{delta},{policy}) ON CONFLICT DO NOTHING
        """);
    public Task<Playerprofile?> PracticeBot(Guid[] bots) => db.Playerprofiles.AsNoTracking()
        .Where(p => bots.Contains(p.Userid) && p.User.Status == "ACTIVE").OrderBy(p => EF.Functions.Random()).FirstOrDefaultAsync();
    public Task<List<Guid>> AvailableBots(Guid[] bots, DateTime cutoff) => db.Playerprofiles.Where(p => bots.Contains(p.Userid)
        && !p.Gamematchplayers.Any(gp => gp.Match.Kind != "BotPractice" && (gp.Match.Status == "PLAYING" || gp.Match.Status == "PREPARING" && gp.Match.Createdat > cutoff)))
        .OrderBy(p => EF.Functions.Random()).Select(p => p.Userid).ToListAsync();
    public Task<RoomMember?> BotMember(Guid bot) => db.Playerprofiles.Where(p => p.Userid == bot).Select(p => new RoomMember
        { id = bot.ToString("N"), name = p.Playername, gender = p.Ismale ? "Male" : "Female", bot = true, team = 1, group = bot.ToString("N") }).SingleOrDefaultAsync();

    public sealed record ReplayAccess(DateTime started, string Status, bool writer);
    public Task<ReplayAccess?> ReplayWriter(Guid player, Guid match) => db.Gamematches.AsNoTracking()
        .Where(m => m.Matchid == match && m.Gamematchplayers.Any(p => p.Playerid == player))
        .Select(m => new ReplayAccess(m.Startedat ?? m.Createdat, m.Status, m.Gamematchplayers.Any(p => p.Playerid == player && p.Isreplaywriter))).SingleOrDefaultAsync();
    public Task<List<Guid>> ReplayMatches(Guid player, bool admin) => db.Gamematches.AsNoTracking()
        .Where(m => m.Status == "FINISHED" && m.Endedat != null && (admin || m.Gamematchplayers.Any(p => p.Playerid == player && p.Finalizedat != null)))
        .OrderByDescending(m => m.Endedat).ThenByDescending(m => m.Sequence).Take(20).Select(m => m.Matchid).ToListAsync();
    public Task<string?> ReplayKey(Guid player, Guid match, bool admin, string canonical) => db.Gamematches.AsNoTracking()
        .Where(m => m.Matchid == match && m.Status == "FINISHED" && m.Endedat != null && (admin || m.Gamematchplayers.Any(p => p.Playerid == player && p.Finalizedat != null)))
        .Select(m => m.Matchhistoryreplay == null ? canonical : m.Matchhistoryreplay.Objectkey).SingleOrDefaultAsync();
    public async Task<List<(Guid id, int team)>> ReplaySeats(Guid match) => (await db.Gamematchplayers.AsNoTracking()
        .Where(p => p.Matchid == match).Select(p => new { p.Playerid, p.Team }).ToListAsync()).Select(p => (p.Playerid, (int)p.Team)).ToList();
    public Task IndexReplay(Guid match, string key) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO matchhistoryreplay(matchid,objectkey)
        SELECT matchid,{key} FROM gamematches WHERE matchid={match} AND status='FINISHED' AND endedat IS NOT NULL
        ON CONFLICT(matchid) DO UPDATE SET objectkey=EXCLUDED.objectkey
        """);
}
