using Entities.Models;
using PickleVerse;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;

namespace Services.Mapping;

// DTOs are assembled from scalar columns and relations, never deserialized from a match document.
public static class RelationalMatch
{
    public static RoomMember Member(Gamematch m, Gamematchplayer p) => new()
    {
        id = p.Playerid.ToString("N"), name = p.Player.Playername, elo = p.Elobefore, team = p.Team,
        gender = p.Player.Ismale ? "Male" : "Female",
        paddleId = p.Player.Playercosmetics.Where(c => c.Equipped).OrderBy(c => c.Cosmeticid).Select(c => c.Cosmetic.Itemcode).FirstOrDefault() ?? "paddle.default",
        group = p.Groupid!, bot = RankedBotAccounts.Contains(p.Playerid.ToString("N")), ready = m.Startedat != null
    };
    public static Gamematchplayer Seat(Guid match, RoomMember member, bool writer, DateTime now, short index = 0) => new()
    {
        Matchid = match, Playerid = Guid.Parse(member.id), Team = (short)member.team,
        Elobefore = member.elo, Groupid = member.group,
        Rosterindex = index, Isreplaywriter = writer, Lastseenat = now
    };
    public static MatchRoster Roster(Gamematch m) => new()
    {
        matchId = m.Matchid.ToString("N"), mode = (PlayMode)m.Mode, custom = m.Kind == "Custom",
        region = m.Region!, night = m.Night, venue = (CourtVenue)m.Venue,
        members = m.Gamematchplayers.OrderBy(p => p.Rosterindex).ThenBy(p => p.Playerid).Select(p => Member(m, p)).ToList()
    };
    public static MatchResult Result(Gamematch m, Gamematchplayer p, bool includeReport = true)
    {
        var opponents = m.Gamematchplayers.Where(x => x.Team != p.Team).OrderBy(x => x.Rosterindex).ThenBy(x => x.Playerid).ToList();
        var scores = includeReport ? m.Matchscores.OrderBy(s => s.Gamenumber)
            .Select(s => new GameScore(p.Team == 0 ? s.Teamascore : s.Teambscore,
                p.Team == 0 ? s.Teambscore : s.Teamascore, s.Completed)).ToList() : [];
        bool finalized = p.Finalizedat != null;
        int won = scores.Count(s => s.completed && s.own > s.opponent);
        int lost = scores.Count(s => s.completed && s.own < s.opponent);
        return new MatchResult
        {
            id = m.Matchid.ToString("N"), playerId = p.Playerid.ToString("N"),
            kind = Enum.Parse<MatchKind>(m.Kind), ratingPolicyVersion = m.Ratingpolicyversion,
            startedUtcTicks = m.Startedat?.Ticks ?? 0, endedUtcTicks = finalized ? m.Endedat?.Ticks ?? 0 : 0,
            ownElo = p.Elobefore, opponentElo = opponents.Count == 0 ? EloPolicy.Minimum : (int)Math.Round(opponents.Average(x => x.Elobefore)),
            opponents = opponents.Select(x => new OpponentInfo { id = x.Playerid.ToString("N"), name = x.Player.Playername, elo = x.Elobefore }).ToList(),
            outcome = finalized ? p.Outcome == "WIN" ? MatchOutcome.Won : p.Outcome == "LOSE" ? MatchOutcome.Lost : MatchOutcome.Interrupted
                : !includeReport || p.Reportedat == null ? default : won > lost ? MatchOutcome.Won : MatchOutcome.Lost,
            localForfeit = finalized && p.Localforfeit, eloDelta = finalized ? m.Ratingledgers.SingleOrDefault(r => r.Playerid == p.Playerid)?.Delta ?? 0 : 0,
            reason = includeReport ? p.Endreason : "", gamesWon = won, gamesLost = lost, scores = scores,
            points = scores.Sum(s => s.own), validReturns = includeReport ? p.Validreturns : 0, validServes = includeReport ? p.Validserves : 0
        };
    }
}
