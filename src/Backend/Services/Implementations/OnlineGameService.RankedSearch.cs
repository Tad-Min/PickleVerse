using PickleVerse;
using PickleVerse.Multiplayer;
using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    long Milliseconds => new DateTimeOffset(Now).ToUnixTimeMilliseconds();
    public async Task<RankedSearchTicket> BeginRankedSearch(Guid id, RankedSearchRequest request)
    {
        if (request.SearchId == Guid.Empty || request.Mode is not (PlayMode.Singles or PlayMode.Doubles) || request.Protocol < 0
            || string.IsNullOrWhiteSpace(request.Region) || request.Region.Length > 30 || request.Group.Length > 100)
            throw Bad("Lượt tìm trận không hợp lệ.");
        var party = (request.Members.Length == 0 ? [id] : request.Members).Distinct().Order().ToArray();
        if (!party.Contains(id) || party.Length > (int)request.Mode || party.Any(p => RankedBotAccounts.Contains(p.ToString("N"))))
            throw Bad("Nhóm tìm trận không hợp lệ.");
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        var s = await Load(id, await catalogs.Load());
        if (s.Player.pendingMatch != null)
            throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        var entry = new QueueEntry
        {
            searchId = request.SearchId.ToString("N"),
            playerId = id.ToString("N"),
            mode = (int)request.Mode,
            group = party.Length == 1 ? id.ToString("N") : request.Group,
            party = party.Select(p => p.ToString("N")).ToArray(),
            region = request.Region.Trim().ToLowerInvariant(),
            protocol = request.Protocol,
            elo = s.Player.elo,
            losses = s.Player.rankLossStreak,
            started = Milliseconds,
            lastSeen = Milliseconds,
            member = new() { id = id.ToString("N"), name = s.Player.playerName, gender = s.profile.Ismale ? "Male" : "Female", elo = s.Player.elo, paddleId = s.inventory.equippedPaddle }
        };
        if (string.IsNullOrWhiteSpace(entry.group))
            throw Bad("Thiếu mã nhóm tìm trận.");
        var previous = await matchmaking.Active(id);
        if (previous != null && previous.searchId != entry.searchId)
        {
            bool expired = previous.lastSeen < Milliseconds - matchmaking.QueueLeaseSeconds * 1000L;
            bool ended = previous.matchId.Length > 0 && await _unitOfWork.Game.MatchEnded(Guid.Parse(previous.matchId));
            if (ended || expired && (previous.matchId.Length == 0 || !await _unitOfWork.Game.WaitingOnline(Guid.Parse(previous.matchId))))
                await matchmaking.Cancel(previous);
        }
        var history=await _unitOfWork.Game.SearchRecord(request.SearchId);
        if(history!=null && (history.PlayerId!=id || await matchmaking.SearchSnapshot(request.SearchId)==null))
            throw Conflict("Mã lượt tìm trận đã được sử dụng hoặc hết hạn.");
        if(history!=null)entry.started=new DateTimeOffset(history.StartedAt).ToUnixTimeMilliseconds();
        await _unitOfWork.Game.RecordSearch(entry,matchmaking.QueueLeaseSeconds);
        await matchmaking.Begin(entry);
        await tx.CommitAsync();
        return await PollRankedSearch(id, new() { SearchId = request.SearchId });
    }
    async Task<RankedSearchTicket> QueueTicket(QueueEntry e) => new()
    {
        roomReady = await matchmaking.RoomReady(e),
        remainingSeconds = Math.Max(0, settings.Matchmaking.BotFallbackSeconds - (Milliseconds - e.started) / 1000d),
        state = e.state,
        matchId = e.matchId,
        session = e.session,
        hostPlayerId = e.host,
        region = e.region,
        roster = e.roster
    };
    public async Task<RankedSearchTicket> PollRankedSearch(Guid id, RankedSearchRequest request)
    {
        var entry = await matchmaking.Get(id, request.SearchId, false);
        long now = Milliseconds;
        long staleBefore = now - matchmaking.QueueLeaseSeconds * 1000L;
        // Leave a stable snapshot for matching while four clients are polling at once.
        // Refresh well before the lease expires so liveness still works with missed polls.
        long refreshBefore = now - Math.Min(5000L, matchmaking.QueueLeaseSeconds * 1000L / 3);
        for (int retry = 0; retry < 8; retry++)
        {
            if (entry.lastSeen > refreshBefore)
                break;
            var before = GameJson.Write(entry);
            bool expired = entry.state == MatchmakingSearchStatus.Searching && entry.lastSeen < staleBefore;
            if (expired)
                entry.state = MatchmakingSearchStatus.TimedOut;
            else
                entry.lastSeen = now;
            if (await matchmaking.Compare([before], [entry]))
            {
                if (expired)
                    throw Conflict("Lượt tìm trận đã hết hạn. Hãy tìm trận lại.");
                break;
            }
            entry = await matchmaking.Get(id, request.SearchId, false);
        }
        if (entry.state != MatchmakingSearchStatus.Searching)
        {
            if (await matchmaking.ReadyCancelled(entry.matchId))
                throw Conflict("Lượt ghép trận đã bị hủy hoặc hết hạn.");
            return await QueueTicket(entry);
        }
        var candidates = (await matchmaking.Candidates(entry, staleBefore)).Where(x => x.entry.state == MatchmakingSearchStatus.Searching && x.entry.mode == entry.mode && x.entry.region == entry.region
            && x.entry.protocol == entry.protocol && x.entry.lastSeen >= staleBefore).ToList();
        var groups = candidates.GroupBy(x => x.entry.group).Select(g => g.OrderBy(x => x.entry.playerId, StringComparer.Ordinal).ToList())
            .Where(g => g.Count == g[0].entry.party.Length && g.Select(x => x.entry.playerId).SequenceEqual(g[0].entry.party)
                && g.All(x => x.entry.party.SequenceEqual(g[0].entry.party))).OrderBy(g => g.Min(x => x.entry.started)).ToList();
        var own = groups.FirstOrDefault(g => g.Any(x => x.entry.playerId == entry.playerId));
        if (own == null)
            return await QueueTicket(await matchmaking.Get(id, request.SearchId, false));
        var seats = new List<(string raw, QueueEntry entry)>(own);
        var teams = new Dictionary<string, int> { [own[0].entry.group] = 0 };
        int cap = entry.mode;
        long fallbackAt = now - settings.Matchmaking.BotFallbackSeconds * 1000L;
        bool Fill(int at, int a, int b, int target)
        {
            if (a + b == target)
                return true;
            for (int i = at; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == own || a + b + g.Count > target)
                    continue;
                if (cap == 1)
                {
                    var other = g[0].entry;
                    if (entry.started <= fallbackAt || other.started <= fallbackAt)
                        continue;
                    if (!EloPolicy.PairingKeys(entry.elo, entry.losses, now - entry.started >= 30000)
                        .Intersect(EloPolicy.PairingKeys(other.elo, other.losses, now - other.started >= 30000)).Any())
                        continue;
                }
                int firstTeam = a > b ? 1 : 0;
                for (int choice = 0; choice < 2; choice++)
                {
                    int team = (firstTeam + choice) % 2;
                    int nextA = a + (team == 0 ? g.Count : 0);
                    int nextB = b + (team == 1 ? g.Count : 0);
                    if (nextA > cap || nextB > cap)
                        continue;
                    teams[g[0].entry.group] = team;
                    seats.AddRange(g);
                    if (Fill(i + 1, nextA, nextB, target))
                        return true;
                    seats.RemoveRange(seats.Count - g.Count, g.Count);
                    teams.Remove(g[0].entry.group);
                }
            }
            return false;
        }
        if (!Fill(0, own.Count, 0, 2 * cap))
        {
            if (entry.mode != (int)PlayMode.Doubles || entry.protocol < RankedSearchRequest.RankedDoublesProtocol
                || own.Any(x => x.entry.started > fallbackAt))
                return await QueueTicket(entry);
            // Search in descending order so any available human seat takes priority over a bot.
            for (int target = 2 * cap - 1; target >= own.Count; target--)
                if (Fill(0, own.Count, 0, target))
                    break;
        }
        var host = seats.OrderBy(x => x.entry.started).ThenBy(x => x.entry.playerId, StringComparer.Ordinal).First().entry.playerId;
        var match = Guid.NewGuid().ToString("N");
        var roster = new MatchRoster
        {
            matchId = match,
            mode = (PlayMode)cap,
            region = entry.region,
            venue = (CourtVenue)System.Security.Cryptography.RandomNumberGenerator.GetInt32(2),
            members = seats.Select(x =>
            {
                var m = GameJson.Read<RoomMember>(GameJson.Write(x.entry.member));
                m.team = teams[x.entry.group];
                m.group = x.entry.group;
                return m;
            }).ToList()
        };
        int missing = 2 * cap - roster.Count;
        if (missing > 0)
        {
            var bots = await _unitOfWork.Game.BotMembers(RankedBotAccounts.Ids.Select(Guid.Parse).ToArray(), missing);
            if (bots.Count != missing)
                return await QueueTicket(entry);
            int humanElo = (int)Math.Round(seats.Average(x => x.entry.elo));
            int humanLosses = (int)Math.Round(seats.Average(x => x.entry.losses));
            int botElo = Math.Max(EloPolicy.Minimum, EloPolicy.BotElo(humanElo, humanLosses));
            foreach (var bot in bots)
            {
                bot.team = roster.members.Count(m => m.team == 0) < cap ? 0 : 1;
                bot.elo = botElo;
                roster.members.Add(bot);
            }
        }
        foreach (var x in seats)
        {
            x.entry.state = MatchmakingSearchStatus.Matched;
            x.entry.matchId = match;
            x.entry.session = "PKV-" + match;
            x.entry.host = host;
            x.entry.roster = roster;
            x.entry.assigned = Milliseconds;
        }
        await matchmaking.Compare(seats.Select(x => x.raw).ToArray(), seats.Select(x => x.entry).ToArray());
        return await QueueTicket(await matchmaking.Get(id, request.SearchId, false));
    }
    public async Task<RankedSearchTicket> PublishRankedRoom(Guid id, RankedSearchRequest request)
    {
        var entry = await matchmaking.Get(id, request.SearchId, false);
        await matchmaking.PublishRoom(entry);
        return await QueueTicket(entry);
    }
    public async Task<RankedSearchTicket> CancelRankedSearch(Guid id, RankedSearchRequest request)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var entry = await matchmaking.SearchSnapshot(request.SearchId);
            if (entry == null || entry.playerId != id.ToString("N"))
                return new() { state = MatchmakingSearchStatus.Cancelled };
            await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
            if (Guid.TryParse(entry.matchId, out var match))
            {
                await _unitOfWork.Game.Lock("online-match:" + entry.matchId);
                if (await _unitOfWork.Game.PlayingOnline(match))
                    return await QueueTicket(entry);
            }
            if (!await matchmaking.Cancel(entry)) continue;
            if (match != Guid.Empty)
                await _unitOfWork.Game.CancelPreparation(match, Now);
            await tx.CommitAsync();
            if (leases != null && match != Guid.Empty)
                await leases.CancelReservation(entry.matchId);
            return new() { state = MatchmakingSearchStatus.Cancelled };
        }
        throw Conflict("Lượt tìm trận đang thay đổi. Vui lòng thử hủy lại.");
    }
    public async Task<RoomMember> AssignRankedBot(Guid id, RankedBotRequest request)
    {
        if (request.SearchId == Guid.Empty || request.MatchId == Guid.Empty || string.IsNullOrWhiteSpace(request.Session) || request.Session.Length > 100)
            throw Bad("Phòng dự phòng không hợp lệ.");
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        var s = await Load(id, await catalogs.Load());
        if (s.Player.pendingMatch != null)
            throw Conflict("Bạn vẫn còn trận chưa hoàn tất.");
        var search = await matchmaking.Get(id, request.SearchId, false);
        if(search.state==MatchmakingSearchStatus.Searching && search.lastSeen<Milliseconds-matchmaking.QueueLeaseSeconds*1000L)
        {
            var before=GameJson.Write(search);search.state=MatchmakingSearchStatus.TimedOut;await matchmaking.Compare([before],[search]);
            throw Conflict("Lượt tìm trận đã hết hạn. Hãy tìm trận lại.");
        }
        if (search.mode != 1 || Milliseconds < search.started + settings.Matchmaking.BotFallbackSeconds * 1000L)
            throw Conflict($"Chưa đủ {settings.Matchmaking.BotFallbackSeconds} giây tìm đối thủ thật.");
        if (search.state == MatchmakingSearchStatus.Bot)
        {
            if (search.matchId != request.MatchId.ToString("N") || search.session != request.Session)
                throw Conflict("Bot đã được cấp cho phòng khác.");
            return search.roster!.members.Single(m => m.bot);
        }
        if (search.state != MatchmakingSearchStatus.Searching)
            throw Conflict("Lượt tìm trận đã có đối thủ thật.");
        var available = await _unitOfWork.Game.AvailableBots(RankedBotAccounts.Ids.Select(Guid.Parse).ToArray(), Now.AddSeconds(-settings.Matchmaking.ReadyWindowSeconds));
        foreach (var bot in available)
        {
            if (await matchmaking.BotBusy(bot.ToString("N")))
                continue;
            var member = await _unitOfWork.Game.BotMember(bot);
            if (member == null) continue;
            member.elo = Math.Max(EloPolicy.Minimum, EloPolicy.BotElo(s.Player.elo, s.Player.rankLossStreak));
            var before = GameJson.Write(search);
            search.state = MatchmakingSearchStatus.Bot;
            search.matchId = request.MatchId.ToString("N");
            search.session = request.Session;
            search.host = id.ToString("N");
            search.assigned = Milliseconds;
            search.botElo = member!.elo;
            var own = GameJson.Read<RoomMember>(GameJson.Write(search.member));
            own.team = 0;
            search.roster = new() { matchId = search.matchId, mode = PlayMode.Singles, region = search.region, members = [own, member] };
            if (await matchmaking.Compare([before], [search], bot.ToString("N"), 900))
            {
                await tx.CommitAsync();
                return member;
            }
            search = await matchmaking.Get(id, request.SearchId, false);
            if (search.state != MatchmakingSearchStatus.Searching)
                throw Conflict("Lượt tìm trận vừa được xử lý. Vui lòng thử lại.");
        }
        throw Conflict("Các đối thủ dự phòng đang bận. Đang tiếp tục tìm trận.");
    }
}
