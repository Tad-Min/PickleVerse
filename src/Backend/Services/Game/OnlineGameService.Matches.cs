using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    sealed class StoredMatch
    {
        public MatchResult result = new();
        public string fingerprint = "";
    }
    public Task<GameSnapshot> StartBot(Guid id, MatchStartRequest request) => Execute(id, request.RequestId, "bot-start", request, async s =>
    {
        if (request.Kind != MatchKind.BotPractice)
            throw Bad("Rank phải tìm trận online và chờ đủ 6 phút để được cấp đối thủ dự phòng.");
        if (s.Player.pendingMatch is { } pending)
        {
            if (!pending.Bot)
                throw Conflict("Bạn cần hoàn tất hoặc rời trận online hiện tại trước.");
            var old = await sql.One("SELECT m.data::text FROM gamematches m WHERE m.matchid=@match AND m.kind='BotPractice' AND m.status='STARTED' AND EXISTS(SELECT 1 FROM gamematchplayers p WHERE p.matchid=m.matchid AND p.playerid=@player)", r => r.GetString(0), P("match", Guid.Parse(pending.id)), P("player", id));
            if (old != null)
            {
                var abandoned = GameJson.Read<StoredMatch>(old);
                abandoned.result.localForfeit = true;
                abandoned.result.outcome = MatchOutcome.Lost;
                abandoned.result.reason = "Bắt đầu trận mới khi trận trước chưa hoàn tất.";
                abandoned.result.endedUtcTicks = Now.Ticks;
                await RecordMatch(s, id, abandoned, request.RequestId);
            }
        }
        int opponent = request.Kind == MatchKind.RankedBot ? EloPolicy.BotElo(s.Player.elo, s.Player.rankLossStreak) : 100;
        var bot = await sql.One("""
            SELECT p.userid,p.playername FROM playerprofiles p JOIN users u ON u.userid=p.userid
            WHERE p.userid=ANY(@bots) AND u.status='ACTIVE' ORDER BY random() LIMIT 1
            """, r => new OpponentInfo { id = r.GetGuid(0).ToString("N"), name = r.GetString(1), elo = opponent },
            P("bots", RankedBotAccounts.Ids.Select(Guid.Parse).ToArray()))
            ?? throw Conflict("Chưa có hồ sơ bot khả dụng.");
        var match = new MatchResult
        {
            ratingPolicyVersion = settings.HybridAi.RatingPolicyVersion,
            id = Guid.NewGuid().ToString("N"),
            playerId = s.Player.id,
            startedUtcTicks = Now.Ticks,
            ownElo = s.Player.elo,
            opponentElo = opponent,
            kind = request.Kind
        };
        match.opponents.Add(bot);
        await sql.Run("INSERT INTO gamematches(matchid,kind,data) VALUES(@match,@kind,@data::jsonb)",
            P("match", Guid.Parse(match.id)), P("kind", match.kind.ToString()), P("data", GameJson.Write(new StoredMatch { result = match })));
        await sql.Run("INSERT INTO gamematchplayers(matchid,playerid,team,accepted,lastseenat) VALUES(@match,@player,0,true,@now),(@match,@bot,1,true,@now)",
            P("match", Guid.Parse(match.id)), P("player", id), P("bot", Guid.Parse(bot.id)), P("now", Now));
        s.Player.pendingMatch = match;
    });
    public Task<GameSnapshot> FinishBot(Guid id, MatchFinishRequest request) => Execute(id, request.RequestId, "bot-complete", request, async s =>
    {
        var input = request.Result;
        if (!Guid.TryParse(input.id, out var matchId))
            throw Bad("Mã trận không hợp lệ.");
        var row = await sql.One("SELECT m.data::text,m.status FROM gamematches m WHERE m.matchid=@match AND m.kind='BotPractice' AND EXISTS(SELECT 1 FROM gamematchplayers p WHERE p.matchid=m.matchid AND p.playerid=@player)", r => new { data = r.GetString(0), status = r.GetString(1) }, P("match", matchId), P("player", id)) ?? throw Bad("Trận chưa được tạo cho tài khoản này.");
        string fingerprint = GameJson.Payload(new { input.outcome, input.localForfeit, input.gamesWon, input.gamesLost, input.points, input.validReturns, input.validServes, input.scores });
        var stored = GameJson.Read<StoredMatch>(row.data);
        if (RankedBotAccounts.Contains(id.ToString("N")) || stored.result.playerId != id.ToString("N"))
            throw Bad("Kết quả trận bot không thuộc tài khoản này.");
        if (row.status == "FINISHED")
        {
            if (stored.fingerprint != fingerprint)
                throw Conflict("Trận đã có kết quả khác.");
            return;
        }
        if (s.Player.pendingMatch?.id != stored.result.id)
            throw Conflict("Trận hiện tại đã thay đổi.");
        if (input.kind != stored.result.kind || input.playerId != s.Player.id || !Enum.IsDefined(input.outcome)
            || input.gamesWon < 0 || input.gamesWon > 2 || input.gamesLost < 0 || input.gamesLost > 2
            || input.scores == null || input.scores.Count > 3 || input.scores.Any(g => g == null || g.own < 0 || g.opponent < 0 || g.own > 100000 || g.opponent > 100000)
            || input.points < 0 || input.points > 300000 || input.validReturns < 0 || input.validReturns > 1000000 || input.validServes < 0 || input.validServes > 1000000)
            throw Bad("Kết quả trận không hợp lệ.");
        if (!input.localForfeit && input.outcome != MatchOutcome.Interrupted)
        {
            if (input.scores.Any(g => !g.completed || Math.Max(g.own, g.opponent) < 11
                    || (Math.Max(g.own, g.opponent) == 11 ? Math.Min(g.own, g.opponent) > 9 : Math.Abs(g.own - g.opponent) != 2))
                || input.scores.Count(g => g.own > g.opponent) != input.gamesWon || input.scores.Count(g => g.own < g.opponent) != input.gamesLost
                || input.gamesWon == input.gamesLost || Math.Max(input.gamesWon, input.gamesLost) != 2
                || (input.outcome == MatchOutcome.Won) != (input.gamesWon == 2))
                throw Bad("Tỉ số chưa đáp ứng luật thắng 2/3 game, 11 điểm cách 2.");
        }
        if (input.localForfeit && input.outcome != MatchOutcome.Lost)
            throw Bad("Bỏ trận phải được ghi nhận là thua.");
        var result = stored.result;
        result.endedUtcTicks = Now.Ticks;
        result.outcome = input.outcome;
        result.localForfeit = input.localForfeit;
        result.gamesWon = input.gamesWon;
        result.gamesLost = input.gamesLost;
        result.points = input.points;
        result.validReturns = input.validReturns;
        result.validServes = input.validServes;
        result.scores = input.scores;
        result.reason = input.localForfeit ? "Chủ động rời trận." : input.outcome == MatchOutcome.Interrupted ? "Trận gián đoạn." : "";
        stored.fingerprint = fingerprint;
        await RecordMatch(s, id, stored, request.RequestId);
    });
    async Task RecordMatch(Session s, Guid id, StoredMatch stored, string command)
    {
        var result = stored.result;
        if (!s.Engine.Apply(s.Player, result))
            throw Conflict("Trận đã được xử lý.");
        if (!result.localForfeit && result.outcome != MatchOutcome.Interrupted && result.Eligible)
        {
            var rewards = s.catalog.matchRewards;
            int coins = result.Bot ? (result.outcome == MatchOutcome.Won ? rewards.botWinReward : rewards.botLossReward)
                : (result.outcome == MatchOutcome.Won ? rewards.onlineWinReward : rewards.onlineLossReward);
            ProgressionEngine.Grant(s.Player, new RewardBundle(coins, 0, 0));
        }
        await sql.Run("UPDATE gamematches SET status='FINISHED',data=@data::jsonb,updatedat=@now WHERE matchid=@match",
            P("data", GameJson.Write(stored)), P("now", Now), P("match", Guid.Parse(result.id)));
        await sql.Run("INSERT INTO matchhistory(matchid,createdat,matchtype,teamascore,teambscore) VALUES(@match,@now,@kind,@own,@opponent) ON CONFLICT DO NOTHING",
            P("match", Guid.Parse(result.id)), P("now", Now), P("kind", result.kind.ToString()), P("own", result.gamesWon), P("opponent", result.gamesLost));
        await sql.Run("INSERT INTO playermatchhistory(playerid,matchid,status,isteama,receivedelo) VALUES(@player,@match,@status,true,@elo) ON CONFLICT DO NOTHING",
            P("player", id), P("match", Guid.Parse(result.id)), P("status", result.outcome == MatchOutcome.Won ? "WIN" : result.outcome == MatchOutcome.Lost ? "LOSE" : "INTERRUPTED"), P("elo", result.eloDelta));
        await RecordRating(id, result);
        await replays.PublishAvailable(Guid.Parse(result.id));
    }
    async Task RecordRating(Guid player, MatchResult result)
    {
        if (!result.Ranked || result.outcome == MatchOutcome.Interrupted)
            return;
        await sql.Run("INSERT INTO ratingledger(playerid,matchid,beforeelo,afterelo,opponentelo,delta,policyversion) VALUES(@player,@match,@before,@after,@opponent,@delta,@policy) ON CONFLICT DO NOTHING",
            P("player", player), P("match", Guid.Parse(result.id)), P("before", result.ownElo), P("after", result.ownElo + result.eloDelta),
            P("opponent", result.opponentElo), P("delta", result.eloDelta), P("policy", result.ratingPolicyVersion));
    }
}
