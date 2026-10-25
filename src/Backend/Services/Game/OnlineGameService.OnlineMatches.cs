using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Services.Exceptions;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    sealed class OnlineMatch
    {
        public MatchRoster roster = new();
        public string session = "", host = "", phase = "PREPARING", searchId = "";
        public long startedUtcTicks;
        public Dictionary<string, MatchResult> results = [];
    }
    sealed class Participant
    {
        public Guid id;
        public bool accepted;
        public string? report, fingerprint;
        public DateTime lastSeen;
    }
    static Guid MatchId(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw Bad("Mã trận không hợp lệ.");
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static string RosterKey(MatchRoster roster) => GameJson.Payload(new
    {
        match = MatchId(roster.matchId),
        roster.mode,
        roster.custom,
        roster.night,
        roster.venue,
        members = roster.members.OrderBy(m => m.id, StringComparer.Ordinal).Select(m => new { id = MatchId(m.id), m.team })
    });
    static void ValidateRoster(Guid caller, OnlineMatchRequest input)
    {
        var r = input.Roster;
        if (r == null || r.members == null || r.members.Count is < 2 or > 4 || r.members.Any(m => m == null || m.bot != RankedBotAccounts.Contains(m.id))
            || !Enum.IsDefined(r.mode) || !Enum.IsDefined(r.venue) || !r.IsValid()
            || r.members.Select(m => MatchId(m.id)).Distinct().Count() != r.members.Count
            || !r.members.Any(m => MatchId(m.id) == caller) || !r.members.Any(m => MatchId(m.id) == MatchId(input.HostPlayerId))
            || string.IsNullOrWhiteSpace(input.Session) || input.Session.Length > 100 || r.region?.Length > 30)
            throw Bad("Đội hình trận online không hợp lệ.");
        MatchId(r.matchId);
        if (r.RankedBot && (MatchId(input.HostPlayerId) != caller || RankedBotAccounts.Contains(caller.ToString("N")) || !Guid.TryParse(input.SearchId, out _)))
            throw Bad("Đối thủ dự phòng phải được cấp từ lượt tìm trận online.");
        // Use the same UUID representation as authenticated Unity profiles.
        foreach (var member in r.members)
            member.id = MatchId(member.id).ToString("N");
        input.HostPlayerId = MatchId(input.HostPlayerId).ToString("N");
        r.matchId = MatchId(r.matchId).ToString("N");
    }
    async Task<List<Participant>> Participants(Guid match) => await sql.Query(
        "SELECT playerid,accepted,report::text,fingerprint,lastseenat FROM gamematchplayers WHERE matchid=@match ORDER BY playerid",
        r => new Participant { id = r.GetGuid(0), accepted = r.GetBoolean(1), report = r.IsDBNull(2) ? null : r.GetString(2), fingerprint = r.IsDBNull(3) ? null : r.GetString(3), lastSeen = r.GetDateTime(4) }, P("match", match));
    async Task SaveOnline(Guid match, OnlineMatch stored) => await sql.Run(
        "UPDATE gamematches SET data=@data::jsonb,status=@status,updatedat=@now WHERE matchid=@match",
        P("data", GameJson.Write(stored)), P("status", stored.phase == "FINISHED" ? "FINISHED" : stored.phase == "CANCELLED" ? "CANCELLED" : "STARTED"), P("now", Now), P("match", match));

    // A match advisory lock serializes consent/results. Profile locks always use UUID order,
    // including across different matches, so wallet and progression transactions cannot race.
    async Task<T> OnlineTransaction<T>(Guid caller, Guid match, OnlineMatchRequest? create,
        Func<OnlineMatch, Dictionary<Guid, Session>, List<Participant>, Task<T>> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await sql.Run("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", P("key", "online-match:" + match.ToString("N")));
        var saved = await sql.One("SELECT data::text FROM gamematches WHERE matchid=@match AND kind IN ('RankedSingles','MatchedDoubles','Custom')", r => r.GetString(0), P("match", match));
        OnlineMatch stored;
        if (saved == null)
        {
            if (create == null)
                throw new AppException("Không tìm thấy trận online.", HttpStatusCode.NotFound);
            stored = new OnlineMatch { roster = GameJson.Read<MatchRoster>(GameJson.Write(create.Roster)), session = create.Session, host = create.HostPlayerId, searchId = create.SearchId };
        }
        else stored = GameJson.Read<OnlineMatch>(saved);
        if (RankedBotAccounts.Contains(caller.ToString("N")) || !stored.roster.members.Any(m => MatchId(m.id) == caller))
            throw new AppException("Bạn không thuộc trận này.", HttpStatusCode.Forbidden);
        if (create != null && (RosterKey(stored.roster) != RosterKey(create.Roster) || stored.session != create.Session || stored.host != create.HostPlayerId))
            throw Conflict("Đội hình hoặc phòng Photon của trận đã thay đổi.");
        if (create != null && !stored.roster.custom && !stored.roster.RankedBot && !string.IsNullOrEmpty(create.SearchId))
        {
            var assigned = await matchmaking.Get(caller, MatchId(create.SearchId), false);
            if (assigned.state != "MATCHED" || assigned.matchId != match.ToString("N") || assigned.session != stored.session || assigned.host != stored.host
                || assigned.roster == null || RosterKey(assigned.roster) != RosterKey(stored.roster))
                throw Conflict("Phòng không khớp đội hình API đã ghép.");
        }
        int botElo = 0;
        if (stored.roster.RankedBot && stored.phase == "PREPARING")
        {
            var bot = stored.roster.members.Single(m => m.bot);
            var assigned = await matchmaking.Get(MatchId(stored.host), MatchId(stored.searchId), false);
            if (!await matchmaking.OwnsBot(bot.id, assigned.searchId) || assigned.state != "BOT" || assigned.matchId != match.ToString("N") || assigned.session != stored.session
                || assigned.roster?.members.SingleOrDefault(m => m.bot)?.id != bot.id || Milliseconds < assigned.started + settings.Matchmaking.BotFallbackSeconds * 1000L)
                throw Conflict("Lượt tìm trận chưa đủ hạn, đã hủy hoặc đối thủ dự phòng không khớp.");
            botElo = assigned.botElo;
        }
        var catalog = await catalogs.Load();
        var players = new Dictionary<Guid, Session>();
        foreach (var id in stored.roster.members.Select(m => MatchId(m.id)).Order())
        {
            var s = await Load(id, catalog);
            players.Add(id, s);
            if (saved == null)
            {
                if (s.Player.pendingMatch != null)
                    throw Conflict("Một người chơi vẫn còn trận chưa hoàn tất.");
                if (await sql.One("SELECT status FROM users WHERE userid=@id", r => r.GetString(0), P("id", id)) != "ACTIVE")
                    throw Bad("Người chơi không còn hoạt động.");
                var member = stored.roster.members.Single(m => MatchId(m.id) == id);
                member.name = s.Player.playerName;
                member.elo = member.bot ? botElo : s.Player.elo;
                member.gender = s.profile.Ismale ? "Male" : "Female";
                member.paddleId = s.inventory.equippedPaddle;
                member.ready = false;
            }
        }
        if (saved == null)
        {
            string kind = OnlineKind(stored.roster).ToString();
            await sql.Run("INSERT INTO gamematches(matchid,ownerid,kind,data,createdat,updatedat) VALUES(@match,@owner,@kind,@data::jsonb,@now,@now)",
                P("match", match), P("owner", MatchId(stored.host)), P("kind", kind), P("data", GameJson.Write(stored)), P("now", Now));
            foreach (var member in stored.roster.members)
                await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,lastseenat) VALUES(@match,@player,@team,@now)", P("match", match), P("player", MatchId(member.id)), P("team", member.team), P("now", Now));
        }
        var participants = await Participants(match);
        var result = await action(stored, players, participants);
        await tx.CommitAsync();
        try
        {
            if (stored.phase is "FINISHED" or "CANCELLED")
                await matchmaking.ReleaseMatch(stored.roster);
            if (stored.phase == "FINISHED")
                await leaderboard.Invalidate();
        }
        catch (StackExchange.Redis.RedisException error)
        {
            // SQL already committed the result. Transient reservations/cache expire or are reconciled on retry.
            System.Diagnostics.Trace.TraceWarning("Redis cleanup after durable online match {0}: {1}", match, error.GetType().Name);
        }
        return result;
    }
    static MatchKind OnlineKind(MatchRoster r) => r.custom ? MatchKind.Custom : r.mode == PlayMode.Singles ? MatchKind.RankedSingles : MatchKind.MatchedDoubles;
    static OnlineMatchTicket Ticket(OnlineMatch m, List<Participant> participants) => new() { roster = m.roster, allAccepted = participants.All(p => p.accepted), phase = m.phase };
    MatchResult OnlineResult(OnlineMatch match, RoomMember member) => new()
    {
        id = match.roster.matchId,
        playerId = member.id,
        kind = OnlineKind(match.roster),
        startedUtcTicks = match.startedUtcTicks,
        ownElo = member.elo,
        opponentElo = (int)Math.Round(match.roster.members.Where(m => m.team != member.team).Average(m => m.elo)),
        opponents = match.roster.members.Where(m => m.team != member.team).Select(m => new OpponentInfo { id = m.id, name = m.name, elo = m.elo }).ToList()
    };
    async Task<OnlineMatchTicket> ConsentOnline(Guid id, OnlineMatchRequest request, bool accept)
    {
        ValidateRoster(id, request);
        var match = MatchId(request.Roster.matchId);
        return await OnlineTransaction(id, match, request, async (m, players, participants) =>
        {
            if (m.phase != "PREPARING")
                throw Conflict("Trận không còn nhận xác nhận.");
            if (await sql.One("SELECT createdat FROM gamematches WHERE matchid=@match", r => r.GetDateTime(0), P("match", match)) < Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds))
                throw Conflict("Lượt xác nhận đã hết hạn.");
            var ready = await matchmaking.Consent(m.roster.matchId, Hash(RosterKey(m.roster) + m.session + m.host),
                m.roster.members.Select(x => x.id).ToArray(), m.roster.members.Where(x => x.bot).Select(x => x.id).ToArray(),
                accept ? id.ToString("N") : null, Milliseconds, settings.Matchmaking.ReadyConfirmationSeconds);
            foreach (var p in participants)
                p.accepted = ready.Accepted.Contains(p.id.ToString("N"));
            var ticket = Ticket(m, participants);
            ticket.readyRemainingSeconds = Math.Max(0, (ready.Deadline - Milliseconds) / 1000d);
            return ticket;
        });
    }
    public Task<OnlineMatchTicket> OfferOnline(Guid id, OnlineMatchRequest request) => ConsentOnline(id, request, false);
    public Task<OnlineMatchTicket> PrepareOnline(Guid id, OnlineMatchRequest request) => ConsentOnline(id, request, true);
    public Task<OnlineMatchTicket> StartOnline(Guid id, OnlineMatchRequest request)
    {
        ValidateRoster(id, request);
        var match = MatchId(request.Roster.matchId);
        return OnlineTransaction(id, match, null, async (m, players, participants) =>
        {
            if (RosterKey(m.roster) != RosterKey(request.Roster) || m.session != request.Session || m.host != request.HostPlayerId)
                throw Conflict("Phòng hoặc đội hình không khớp xác nhận.");
            if (m.phase == "PLAYING")
                return Ticket(m, participants);
            if (m.phase != "PREPARING")
                throw Conflict("Trận không còn nhận xác nhận.");
            var ready = await matchmaking.Consent(m.roster.matchId, Hash(RosterKey(m.roster) + m.session + m.host),
                m.roster.members.Select(x => x.id).ToArray(), m.roster.members.Where(x => x.bot).Select(x => x.id).ToArray(), null, Milliseconds, settings.Matchmaking.ReadyConfirmationSeconds);
            if (!ready.AllAccepted)
                throw Conflict("Mọi người phải xác nhận trước khi tải sân.");
            foreach (var p in participants)
                p.accepted = true;
            if (await sql.One("SELECT createdat FROM gamematches WHERE matchid=@match", r => r.GetDateTime(0), P("match", match)) < Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds))
                throw Conflict("Lượt xác nhận đã hết hạn.");
            if (players.Values.Any(s => s.Player.pendingMatch != null))
                throw Conflict("Một người chơi vẫn còn trận chưa hoàn tất.");
            m.phase = "PLAYING";
            m.startedUtcTicks = Now.Ticks;
            foreach (var member in m.roster.members)
            {
                var s = players[MatchId(member.id)];
                s.Player.pendingMatch = OnlineResult(m, member);
                DailyStreakEngine.Ensure(s.Player, Now);
                s.Engine.RefreshPeriods(s.Player, Now);
                await Persist(s, MatchId(member.id), "online-start:" + m.roster.matchId, "online-start");
            }
            await sql.Run("UPDATE gamematchplayers SET accepted=true,lastseenat=@now WHERE matchid=@match", P("now", Now), P("match", match));
            await SaveOnline(match, m);
            return Ticket(m, participants);
        });
    }
    static string ResultFingerprint(MatchResult r, int team) => Hash(GameJson.Payload(new
    {
        a = team == 0 ? r.gamesWon : r.gamesLost,
        b = team == 0 ? r.gamesLost : r.gamesWon,
        scores = r.scores.Select(g => new { a = team == 0 ? g.own : g.opponent, b = team == 0 ? g.opponent : g.own })
    }));
    static void ValidateReport(OnlineMatch match, Guid id, MatchResult r)
    {
        if (r == null || MatchId(r.id) != MatchId(match.roster.matchId) || MatchId(r.playerId) != id || r.kind != OnlineKind(match.roster)
            || r.outcome is not (MatchOutcome.Won or MatchOutcome.Lost) || r.localForfeit
            || r.gamesWon is < 0 or > 2 || r.gamesLost is < 0 or > 2 || Math.Max(r.gamesWon, r.gamesLost) != 2 || r.gamesWon == r.gamesLost
            || (r.outcome == MatchOutcome.Won) != (r.gamesWon == 2) || r.scores == null || r.scores.Count is < 2 or > 3
            || r.scores.Any(g => g == null || !g.completed || g.own is < 0 or > 100000 || g.opponent is < 0 or > 100000
                || Math.Max(g.own, g.opponent) < 11 || (Math.Max(g.own, g.opponent) == 11 ? Math.Min(g.own, g.opponent) > 9 : Math.Abs(g.own - g.opponent) != 2))
            || r.scores.Count(g => g.own > g.opponent) != r.gamesWon || r.scores.Count(g => g.own < g.opponent) != r.gamesLost
            || r.points < 0 || r.points != r.scores.Sum(g => g.own) || r.validReturns is < 0 or > 1000000 || r.validServes is < 0 or > 1000000)
            throw Bad("Kết quả trận online không hợp lệ; rời trận phải dùng API rời trận.");
        if (r.scores.Take(r.scores.Count - 1).Count(g => g.own > g.opponent) >= 2 || r.scores.Take(r.scores.Count - 1).Count(g => g.own < g.opponent) >= 2)
            throw Bad("Trận đã kết thúc trước game cuối được gửi.");
    }
    public Task<GameSnapshot> ReportOnline(Guid id, MatchFinishRequest request)
    {
        var input = request.Result;
        var match = MatchId(input.id);
        return OnlineTransaction(id, match, null, async (m, players, participants) =>
        {
            ValidateReport(m, id, input);
            var self = participants.Single(p => p.id == id);
            int team = m.roster.Find(id.ToString("N")).team;
            string fingerprint = ResultFingerprint(input, team);
            if (self.report != null)
            {
                var old = GameJson.Read<MatchResult>(self.report);
                if (self.fingerprint != fingerprint || old.validReturns != input.validReturns || old.validServes != input.validServes)
                    throw Conflict("Bạn đã gửi một kết quả khác cho trận này.");
                return players[id].Snapshot();
            }
            if (m.phase != "PLAYING")
                throw Conflict("Trận không còn nhận kết quả.");
            if (participants.Any(p => p.fingerprint != null && p.fingerprint != fingerprint))
                throw Conflict("Kết quả hai bên chưa thống nhất; chưa cấp thưởng.");
            var canonical = OnlineResult(m, m.roster.Find(id.ToString("N")));
            canonical.outcome = input.outcome;
            canonical.gamesWon = input.gamesWon;
            canonical.gamesLost = input.gamesLost;
            canonical.scores = input.scores;
            canonical.points = input.points;
            canonical.validReturns = input.validReturns;
            canonical.validServes = input.validServes;
            self.report = GameJson.Write(canonical);
            self.fingerprint = fingerprint;
            await sql.Run("UPDATE gamematchplayers SET report=@report::jsonb,fingerprint=@fingerprint,lastseenat=@now WHERE matchid=@match AND playerid=@player", P("report", self.report), P("fingerprint", fingerprint), P("now", Now), P("match", match), P("player", id));
            // Only a server-assigned virtual seat can be completed by mirroring the
            // human's score. Human-v-human matches still require both authenticated reports.
            foreach (var bot in m.roster.members.Where(x => x.bot))
            {
                var mirror = OnlineResult(m, bot);
                mirror.outcome = canonical.outcome == MatchOutcome.Won ? MatchOutcome.Lost : MatchOutcome.Won;
                mirror.gamesWon = canonical.gamesLost;
                mirror.gamesLost = canonical.gamesWon;
                mirror.scores = canonical.scores.Select(g => new GameScore(g.opponent, g.own, g.completed)).ToList();
                mirror.points = mirror.scores.Sum(g => g.own);
                var participant = participants.Single(p => p.id == MatchId(bot.id));
                participant.report = GameJson.Write(mirror);
                participant.fingerprint = fingerprint;
                await sql.Run("UPDATE gamematchplayers SET report=@report::jsonb,fingerprint=@fingerprint,lastseenat=@now WHERE matchid=@match AND playerid=@bot", P("report", participant.report), P("fingerprint", fingerprint), P("now", Now), P("match", match), P("bot", MatchId(bot.id)));
            }
            if (participants.All(p => p.report != null))
            {
                m.results = participants.ToDictionary(p => p.id.ToString("N"), p => GameJson.Read<MatchResult>(p.report!));
                await FinishOnline(match, m, players);
            }
            return players[id].Snapshot();
        });
    }
    async Task FinishOnline(Guid match, OnlineMatch m, Dictionary<Guid, Session> players)
    {
        if (m.phase == "FINISHED")
            return;
        foreach (var member in m.roster.members)
        {
            var id = MatchId(member.id);
            var s = players[id];
            var result = m.results[member.id];
            result.endedUtcTicks = Now.Ticks;
            if (s.Player.pendingMatch?.id != m.roster.matchId)
                throw Conflict("Tiến trình trận của người chơi đã thay đổi.");
            if (!s.Engine.Apply(s.Player, result))
                throw Conflict("Kết quả đã được xử lý.");
            if (!result.localForfeit && result.outcome != MatchOutcome.Interrupted && result.Eligible)
                ProgressionEngine.Grant(s.Player, new RewardBundle(result.outcome == MatchOutcome.Won ? s.catalog.matchRewards.onlineWinReward : s.catalog.matchRewards.onlineLossReward, 0, 0));
            await Persist(s, id, "online-result:" + m.roster.matchId, "online-result");
        }
        var a = m.results[m.roster.members.First(x => x.team == 0).id];
        await sql.Run("INSERT INTO matchhistory(matchid,createdat,matchtype,teamascore,teambscore) VALUES(@match,@now,@kind,@a,@b) ON CONFLICT DO NOTHING",
            P("match", match), P("now", Now), P("kind", OnlineKind(m.roster).ToString()), P("a", a.gamesWon), P("b", a.gamesLost));
        foreach (var member in m.roster.members)
        {
            var r = m.results[member.id];
            await sql.Run("INSERT INTO playermatchhistory(playerid,matchid,status,isteama,receivedelo) VALUES(@player,@match,@status,@team,@elo) ON CONFLICT DO NOTHING",
                P("player", MatchId(member.id)), P("match", match), P("status", r.outcome == MatchOutcome.Won ? "WIN" : r.outcome == MatchOutcome.Lost ? "LOSE" : "INTERRUPTED"), P("team", member.team == 0), P("elo", r.eloDelta));
        }
        m.phase = "FINISHED";
        await SaveOnline(match, m);
    }
    async Task EndOnline(Guid match, OnlineMatch m, Dictionary<Guid, Session> players, int? loser, string reason, HashSet<Guid> forfeits)
    {
        m.results = m.roster.members.ToDictionary(x => x.id, x =>
        {
            var r = OnlineResult(m, x);
            r.outcome = loser == null ? MatchOutcome.Interrupted : x.team == loser ? MatchOutcome.Lost : MatchOutcome.Won;
            r.localForfeit = forfeits.Contains(MatchId(x.id));
            r.reason = reason;
            return r;
        });
        await FinishOnline(match, m, players);
    }
    public Task<GameSnapshot> LeaveOnline(Guid id, OnlineMatchCommand request)
    {
        var match = MatchId(request.MatchId);
        return OnlineTransaction(id, match, null, async (m, players, participants) =>
        {
            if (m.phase == "PREPARING")
            {
                m.phase = "CANCELLED";
                await SaveOnline(match, m);
            }
            else if (m.phase == "PLAYING")
                await EndOnline(match, m, players, m.roster.Find(id.ToString("N")).team, "Người chơi chủ động rời trận.", [id]);
            return players[id].Snapshot();
        });
    }
    async Task<bool> ExpireDisconnected(Guid match, OnlineMatch m, Dictionary<Guid, Session> players, List<Participant> participants)
    {
        if (m.phase != "PLAYING")
            return false;
        var stale = participants.Where(p => !RankedBotAccounts.Contains(p.id.ToString("N")) && p.lastSeen < Now.AddSeconds(-settings.OnlineMatch.DisconnectTimeoutSeconds) && p.report == null).Select(p => p.id).ToHashSet();
        if (stale.Count == 0)
            return false;
        var disconnectedTeams = m.roster.members.Where(x => stale.Contains(MatchId(x.id))).Select(x => x.team).Distinct().ToList();
        await EndOnline(match, m, players, disconnectedTeams.Count == 1 ? disconnectedTeams[0] : null, "Hết thời gian kết nối lại trận.", disconnectedTeams.Count == 1 ? stale : []);
        return true;
    }
    public Task<GameSnapshot> HeartbeatOnline(Guid id, OnlineMatchCommand request)
    {
        var match = MatchId(request.MatchId);
        return OnlineTransaction(id, match, null, async (m, players, participants) =>
        {
            if (!await ExpireDisconnected(match, m, players, participants) && m.phase == "PLAYING")
                await sql.Run("UPDATE gamematchplayers SET lastseenat=@now WHERE matchid=@match AND playerid=@player", P("now", Now), P("match", match), P("player", id));
            return players[id].Snapshot();
        });
    }
    async Task ReconcileOnline(Guid id)
    {
        var pending = await sql.One("SELECT m.matchid FROM gamematches m JOIN gamematchplayers p USING(matchid) WHERE p.playerid=@player AND m.data->>'phase'='PLAYING' ORDER BY m.createdat LIMIT 1", r => (Guid?)r.GetGuid(0), P("player", id));
        if (pending.HasValue)
            await OnlineTransaction(id, pending.Value, null, async (m, players, participants) => await ExpireDisconnected(pending.Value, m, players, participants));
    }
}
