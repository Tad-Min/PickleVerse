using Entities.Models;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public Task<GameSnapshot> StartBot(Guid id, MatchStartRequest request) => Execute(id, request.RequestId, "bot-start", request, async s =>
    {
        if (request.Kind != MatchKind.BotPractice)
            throw Bad("Rank phải tìm trận online và chờ đủ 6 phút để được cấp đối thủ dự phòng.");
        if (s.Player.pendingMatch is { } pending)
        {
            if (!pending.Bot)
                throw Conflict("Bạn cần hoàn tất hoặc rời trận online hiện tại trước.");
            var pendingId=Guid.Parse(pending.id);
            var old=await _unitOfWork.Game.Match(m=>m.Matchid==pendingId && m.Kind=="BotPractice" && m.Status=="PLAYING" && m.Gamematchplayers.Any(p=>p.Playerid==id && p.Isreplaywriter));
            if (old != null)
            {
                var abandoned = RelationalMatch.Result(old,old.Gamematchplayers.Single(p=>p.Playerid==id));
                abandoned.localForfeit = true;
                abandoned.outcome = MatchOutcome.Lost;
                abandoned.reason = "Bắt đầu trận mới khi trận trước chưa hoàn tất.";
                abandoned.endedUtcTicks = Now.Ticks;
                await RecordMatch(s, id, abandoned);
            }
        }
        int opponent = request.Kind == MatchKind.RankedBot ? EloPolicy.BotElo(s.Player.elo, s.Player.rankLossStreak) : 100;
        var bots=RankedBotAccounts.Ids.Select(Guid.Parse).ToArray();
        var botProfile=await _unitOfWork.Game.PracticeBot(bots)
            ?? throw Conflict("Chưa có hồ sơ bot khả dụng.");
        var bot=new OpponentInfo {id=botProfile.Userid.ToString("N"),name=botProfile.Playername,elo=opponent};
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
        var matchId=Guid.Parse(match.id);
        _unitOfWork.Game.AddMatch(new Gamematch {Matchid=matchId,Kind=match.kind.ToString(),Status="PLAYING",Ratingpolicyversion=match.ratingPolicyVersion,
            Mode=1,Createdat=Now,Updatedat=Now,Startedat=Now});
        var ownSeat=RelationalMatch.Seat(matchId,new RoomMember {id=s.Player.id,name=s.Player.playerName,elo=s.Player.elo,
            gender=s.profile.Ismale?"Male":"Female",paddleId=s.inventory.equippedPaddle,team=0},true,Now);
        var botSeat=RelationalMatch.Seat(matchId,new RoomMember {id=bot.id,name=bot.name,elo=bot.elo,
            gender=botProfile.Ismale?"Male":"Female",team=1,bot=true},false,Now,1);
        _unitOfWork.Game.AddParticipant(ownSeat);
        _unitOfWork.Game.AddParticipant(botSeat);
        await _unitOfWork.SaveChangesAsync();
        s.Player.pendingMatch = match;
    });
    public Task<GameSnapshot> FinishBot(Guid id, MatchFinishRequest request) => Execute(id, request.RequestId, "bot-complete", request, async s =>
    {
        var input = request.Result;
        if (!Guid.TryParse(input.id, out var matchId))
            throw Bad("Mã trận không hợp lệ.");
        var row=await _unitOfWork.Game.Match(m=>m.Matchid==matchId && m.Kind=="BotPractice" && m.Gamematchplayers.Any(p=>p.Playerid==id && p.Isreplaywriter))
            ?? throw Bad("Trận chưa được tạo cho tài khoản này.");
        var participant=row.Gamematchplayers.Single(p=>p.Playerid==id);
        var result=RelationalMatch.Result(row,participant);
        if (RankedBotAccounts.Contains(id.ToString("N")))
            throw Bad("Kết quả trận bot không thuộc tài khoản này.");
        if (row.Status == "FINISHED")
        {
            if (BotResultFingerprint(result) != BotResultFingerprint(input))
                throw Conflict("Trận đã có kết quả khác.");
            return;
        }
        if (s.Player.pendingMatch?.id != result.id)
            throw Conflict("Trận hiện tại đã thay đổi.");
        if (input.kind != result.kind || input.playerId != s.Player.id || !Enum.IsDefined(input.outcome)
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
        result.endedUtcTicks = Now.Ticks;
        result.outcome = input.outcome;
        result.localForfeit = input.localForfeit;
        result.gamesWon = input.scores.Count(g => g.completed && g.own > g.opponent);
        result.gamesLost = input.scores.Count(g => g.completed && g.own < g.opponent);
        result.points = input.scores.Sum(g => g.own);
        result.validReturns = input.validReturns;
        result.validServes = input.validServes;
        result.scores = input.scores;
        result.reason = input.localForfeit ? "Chủ động rời trận." : input.outcome == MatchOutcome.Interrupted ? "Trận gián đoạn." : "";
        await RecordMatch(s, id, result);
    });
    static string BotResultFingerprint(MatchResult result) => Hash(GameJson.Payload(new
        { result.outcome, result.localForfeit, result.validReturns, result.validServes, result.scores }));
    async Task RecordMatch(Session s, Guid id, MatchResult result)
    {
        if (!s.Engine.Apply(s.Player, result))
            throw Conflict("Trận đã được xử lý.");
        if (!result.localForfeit && result.outcome != MatchOutcome.Interrupted && result.Eligible)
        {
            var rewards = s.catalog.matchRewards;
            int coins = result.Bot ? (result.outcome == MatchOutcome.Won ? rewards.botWinReward : rewards.botLossReward)
                : (result.outcome == MatchOutcome.Won ? rewards.onlineWinReward : rewards.onlineLossReward);
            ProgressionEngine.Grant(s.Player, new RewardBundle(coins, 0, 0));
        }
        await _unitOfWork.Game.SaveScores(Guid.Parse(result.id),result.scores);
        await FinalizeParticipant(id,result);
        var bot=new MatchResult {id=result.id,playerId=result.opponents[0].id,kind=result.kind,ratingPolicyVersion=result.ratingPolicyVersion,
            startedUtcTicks=result.startedUtcTicks,endedUtcTicks=result.endedUtcTicks,ownElo=result.opponentElo,opponentElo=result.ownElo,
            outcome=result.outcome==MatchOutcome.Interrupted?MatchOutcome.Interrupted:result.outcome==MatchOutcome.Won?MatchOutcome.Lost:MatchOutcome.Won,
            gamesWon=result.gamesLost,gamesLost=result.gamesWon,reason=result.reason,
            opponents=[new(){id=result.playerId,name=s.Player.playerName,elo=result.ownElo}],
            scores=result.scores.Select(g=>new GameScore(g.opponent,g.own,g.completed)).ToList()};
        bot.points=bot.scores.Sum(g=>g.own);
        await FinalizeParticipant(Guid.Parse(bot.playerId),bot);
        await FinalizeMatch(Guid.Parse(result.id));
        await RecordRating(id, result);
        await replays.PublishAvailable(Guid.Parse(result.id));
    }
    async Task RecordRating(Guid player, MatchResult result)
    {
        if (!result.Ranked || result.outcome == MatchOutcome.Interrupted)
            return;
        await _unitOfWork.Game.RecordRating(player, Guid.Parse(result.id), result.ownElo, result.eloDelta, result.opponentElo, result.ratingPolicyVersion);
    }
}
