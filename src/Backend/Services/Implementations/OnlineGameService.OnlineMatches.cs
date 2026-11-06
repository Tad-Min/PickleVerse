using Entities.Models;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;
using Entities.Exceptions;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    sealed class OnlineMatch
    {
        public string authorityMode = "shared";
        public string ratingPolicyVersion = EloPolicy.LegacyVersion;
        public MatchRoster roster = new();
        public string session = "", host = "", phase = "PREPARING", searchId = "";
        public long startedUtcTicks;
        public Dictionary<string, MatchResult> results = [];
    }
    sealed class Participant
    {
        public Guid id;
        public bool accepted;
        public MatchResult? result;
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
            || string.IsNullOrWhiteSpace(input.Session) || input.Session.Length > 100 || r.region?.Length > 30
            || !string.IsNullOrEmpty(input.SearchId) && !Guid.TryParse(input.SearchId, out _))
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
    async Task<List<Participant>> Participants(Guid match)
    {
        var record = await _unitOfWork.Game.Match(m => m.Matchid == match) ?? throw new InvalidOperationException("Missing match.");
        return record.Gamematchplayers.OrderBy(p => p.Playerid).Select(p => new Participant
        {
            id = p.Playerid, accepted = record.Startedat != null, lastSeen = p.Lastseenat,
            result = p.Reportedat != null || p.Finalizedat != null ? RelationalMatch.Result(record, p) : null
        }).ToList();
    }
    static OnlineMatch OnlineRecord(Gamematch record) => new()
    {
        authorityMode = StatusWire.Format(record.Authoritymode), ratingPolicyVersion = record.Ratingpolicyversion, roster = RelationalMatch.Roster(record),
        session = record.Photonsession ?? "", searchId = record.Searchid?.ToString("N") ?? "",
        host = record.Gamematchplayers.SingleOrDefault(p => p.Isreplaywriter)?.Playerid.ToString("N") ?? "",
        phase = StatusWire.Format(record.Status), startedUtcTicks = record.Startedat?.Ticks ?? 0
    };
    async Task SaveOnline(Guid match,OnlineMatch stored)
    {
        var now=Now;DateTime? started=stored.startedUtcTicks>0?new DateTime(stored.startedUtcTicks,DateTimeKind.Utc):null;
        await _unitOfWork.Game.SaveOnline(match, StatusWire.Parse<MatchStatus>(stored.phase), started, now);
    }

    // A match advisory lock serializes consent/results. Profile locks always use UUID order,
    // including across different matches, so wallet and progression transactions cannot race.
    async Task<T> OnlineTransaction<T>(Guid caller, Guid match, OnlineMatchRequest? create,
        Func<OnlineMatch, Dictionary<Guid, Session>, List<Participant>, Task<T>> action)
    {
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        await _unitOfWork.Game.Lock("online-match:"+match.ToString("N"));
        var saved=await _unitOfWork.Game.Match(m=>m.Matchid==match && (m.Kind=="RankedSingles" || m.Kind=="MatchedDoubles" || m.Kind=="Custom"));
        OnlineMatch stored;
        if (saved == null)
        {
            if (create == null)
                throw new AppException("Không tìm thấy trận online.", HttpStatusCode.NotFound);
            stored = new OnlineMatch { authorityMode = settings.Dedicated.Enabled ? "dedicated" : "shared", ratingPolicyVersion = settings.HybridAi.RatingPolicyVersion, roster = GameJson.Read<MatchRoster>(GameJson.Write(create.Roster)), session = create.Session, host = create.HostPlayerId, searchId = create.SearchId };
        }
        else stored = OnlineRecord(saved);
        if (RankedBotAccounts.Contains(caller.ToString("N")) || !stored.roster.members.Any(m => MatchId(m.id) == caller))
            throw new AppException("Bạn không thuộc trận này.", HttpStatusCode.Forbidden);
        if (create != null && (RosterKey(stored.roster) != RosterKey(create.Roster) || stored.session != create.Session || stored.host != create.HostPlayerId))
            throw Conflict("Đội hình hoặc phòng Photon của trận đã thay đổi.");
        if (create != null && !stored.roster.custom && !stored.roster.RankedBot && !string.IsNullOrEmpty(create.SearchId))
        {
            var assigned = await matchmaking.Get(caller, MatchId(create.SearchId), false);
            if (assigned.state != MatchmakingSearchStatus.Matched || assigned.matchId != match.ToString("N") || assigned.session != stored.session || assigned.host != stored.host
                || assigned.roster == null || RosterKey(assigned.roster) != RosterKey(stored.roster))
                throw Conflict("Phòng không khớp đội hình API đã ghép.");
        }
        int botElo = 0;
        if (stored.roster.RankedBot && stored.phase == "PREPARING")
        {
            var bot = stored.roster.members.Single(m => m.bot);
            var assigned = await matchmaking.Get(MatchId(stored.host), MatchId(stored.searchId), false);
            if (!await matchmaking.OwnsBot(bot.id, assigned.searchId) || assigned.state != MatchmakingSearchStatus.Bot || assigned.matchId != match.ToString("N") || assigned.session != stored.session
                || assigned.roster?.members.SingleOrDefault(m => m.bot)?.id != bot.id || Milliseconds < assigned.started + settings.Matchmaking.BotFallbackSeconds * 1000L)
                throw Conflict("Lượt tìm trận chưa đủ hạn, đã hủy hoặc đối thủ dự phòng không khớp.");
            botElo = assigned.botElo;
        }
        if (saved == null)
            foreach (var id in stored.roster.members.Select(m => MatchId(m.id)).Order()) await _unitOfWork.Users.LockUserAsync(id);
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
                if (await _unitOfWork.Game.AccountStatus(id) != UserStatus.Active)
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
            _unitOfWork.Game.AddMatch(new Gamematch {Matchid=match,Kind=kind,Status=StatusWire.Parse<MatchStatus>(stored.phase),Ratingpolicyversion=stored.ratingPolicyVersion,
                Authoritymode=StatusWire.Parse<MatchAuthorityMode>(stored.authorityMode),
                Mode=(short)stored.roster.mode,Photonsession=stored.session,Searchid=string.IsNullOrEmpty(stored.searchId)?null:Guid.Parse(stored.searchId),
                Region=stored.roster.region,Night=stored.roster.night,Venue=(short)stored.roster.venue,Createdat=Now,Updatedat=Now});
            for(short index=0;index<stored.roster.members.Count;index++)
            {
                var member=stored.roster.members[index];
                _unitOfWork.Game.AddParticipant(RelationalMatch.Seat(match,member,member.id==stored.host,Now,index));
            }
            await _unitOfWork.SaveChangesAsync();
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
        catch (Exception error) when (PersistenceErrors.IsCacheUnavailable(error))
        {
            // SQL already committed the result. Transient reservations/cache expire or are reconciled on retry.
            System.Diagnostics.Trace.TraceWarning("Redis cleanup after durable online match {0}: {1}", match, error.GetType().Name);
        }
        return result;
    }
    static MatchKind OnlineKind(MatchRoster r) => r.custom ? MatchKind.Custom : r.mode == PlayMode.Singles ? MatchKind.RankedSingles : MatchKind.MatchedDoubles;
    static OnlineMatchTicket Ticket(OnlineMatch m, List<Participant> participants)
    {
        foreach (var member in m.roster.members)
            member.ready = participants.Single(p => p.id == MatchId(member.id)).accepted;
        return new() { authorityMode = m.authorityMode, roster = m.roster, allAccepted = participants.All(p => p.accepted), phase = m.phase };
    }
    MatchResult OnlineResult(OnlineMatch match, RoomMember member) => new()
    {
        ratingPolicyVersion = match.ratingPolicyVersion,
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
            if (await _unitOfWork.Game.MatchCreated(match) < Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds))
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
            if (await _unitOfWork.Game.MatchCreated(match) < Now.AddSeconds(-settings.Matchmaking.ReadyConfirmationSeconds))
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
            var now=Now;
            await _unitOfWork.Game.TouchParticipants(match, null, now);
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
            if (await _unitOfWork.Game.IsDedicated(match)) throw new Entities.Exceptions.AppException("Chỉ máy chủ thi đấu được xác nhận kết quả trận này.", System.Net.HttpStatusCode.Forbidden);
            ValidateReport(m, id, input);
            var self = participants.Single(p => p.id == id);
            int team = m.roster.Find(id.ToString("N")).team;
            string fingerprint = ResultFingerprint(input, team);
            if (self.result != null)
            {
                var old = self.result;
                if (ResultFingerprint(old,team) != fingerprint || old.validReturns != input.validReturns || old.validServes != input.validServes)
                    throw Conflict("Bạn đã gửi một kết quả khác cho trận này.");
                return players[id].Snapshot();
            }
            if (m.phase != "PLAYING")
                throw Conflict("Trận không còn nhận kết quả.");
            if (participants.Any(p => p.result != null && ResultFingerprint(p.result,m.roster.Find(p.id.ToString("N")).team) != fingerprint))
                throw Conflict("Kết quả hai bên chưa thống nhất; chưa cấp thưởng.");
            var canonical = OnlineResult(m, m.roster.Find(id.ToString("N")));
            canonical.outcome = input.outcome;
            canonical.gamesWon = input.gamesWon;
            canonical.gamesLost = input.gamesLost;
            canonical.scores = input.scores;
            canonical.points = input.points;
            canonical.validReturns = input.validReturns;
            canonical.validServes = input.validServes;
            self.result = canonical;
            var reportedAt=Now;
            // One shared score candidate, normalized to team A. It becomes canonical
            // only when all participant fingerprints agree and the match is FINISHED.
            if (!participants.Any(p => p.result != null && p.id != id))
                await _unitOfWork.Game.SaveScores(match,team==0?canonical.scores:canonical.scores.Select(g=>new GameScore(g.opponent,g.own,g.completed)).ToList());
            await SaveSubmittedResult(match,id,canonical,reportedAt);
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
                participant.result = mirror;
                var botId=MatchId(bot.id);
                await SaveSubmittedResult(match,botId,mirror,reportedAt);
            }
            if (participants.All(p => p.result != null))
            {
                m.results = participants.ToDictionary(p => p.id.ToString("N"), p => p.result!);
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
            await FinalizeParticipant(id,result);
        }
        var a = m.results[m.roster.members.First(x => x.team == 0).id];
        await _unitOfWork.Game.SaveScores(match,a.scores);
        await FinalizeMatch(match);
        foreach(var member in m.roster.members)
            await RecordRating(MatchId(member.id),m.results[member.id]);
        m.phase = "FINISHED";
        await SaveOnline(match, m);
        await replays.PublishAvailable(match);
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
            {
                if (await _unitOfWork.Game.IsDedicated(match))
                    throw new AppException("Hãy gửi yêu cầu rời trận tới máy chủ thi đấu.", HttpStatusCode.Forbidden);
                await EndOnline(match, m, players, m.roster.Find(id.ToString("N")).team, "Người chơi chủ động rời trận.", [id]);
            }
            return players[id].Snapshot();
        });
    }
    async Task<bool> ExpireDisconnected(Guid match, OnlineMatch m, Dictionary<Guid, Session> players, List<Participant> participants)
    {
        if (m.phase != "PLAYING")
            return false;
        if (await _unitOfWork.Game.IsDedicated(match)) return false; // The server and lease monitor own disconnect decisions.
        var stale = participants.Where(p => !RankedBotAccounts.Contains(p.id.ToString("N")) && p.lastSeen < Now.AddSeconds(-settings.OnlineMatch.DisconnectTimeoutSeconds) && p.result == null).Select(p => p.id).ToHashSet();
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
            {
                var now=Now;
                await _unitOfWork.Game.TouchParticipants(match, id, now);
            }
            return players[id].Snapshot();
        });
    }
    async Task ReconcileOnline(Guid id)
    {
        var pending = await _unitOfWork.Game.PendingOnline(id);
        if (pending.HasValue)
            await OnlineTransaction(id, pending.Value, null, async (m, players, participants) => await ExpireDisconnected(pending.Value, m, players, participants));
    }
}
