using PickleVerse.Progression;
using PickleVerse.Multiplayer;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public async Task CompleteDedicated(Guid match, List<DedicatedPlayerResult> input)
    {
        if (input == null || input.Count == 0 || input.Any(x => x == null)) throw Bad("Kết quả máy chủ không hợp lệ.");
        var record = await _unitOfWork.Game.Match(m => m.Matchid == match && m.Authoritymode == "dedicated") ?? throw Bad("Không tìm thấy trận dedicated.");
        var caller = record.Gamematchplayers.First(p => !RankedBotAccounts.Contains(p.Playerid.ToString("N"))).Playerid;
        await OnlineTransaction(caller, match, null, async (m, players, participants) =>
        {
            if (m.phase == "FINISHED") return true;
            if (m.phase != "PLAYING" || input.Count != m.roster.Count || input.Select(x => x.onlineId).Distinct().Count() != input.Count ||
                input.Any(x => x.result == null || x.onlineId != x.result.playerId || x.result.id != match.ToString("N")) ||
                !input.Select(x => x.onlineId).ToHashSet().SetEquals(m.roster.members.Select(x => x.id))) throw Bad("Kết quả máy chủ không khớp đội hình.");
            var forfeits = input.Where(x => x.result.localForfeit).Select(x => MatchId(x.onlineId)).ToHashSet();
            if (forfeits.Count > 0 || input.Any(x => x.result.outcome == MatchOutcome.Interrupted))
            {
                var teams = m.roster.members.Where(x => forfeits.Contains(MatchId(x.id))).Select(x => x.team).Distinct().ToList();
                await EndOnline(match, m, players, teams.Count == 1 ? teams[0] : null, "Máy chủ kết thúc trận do rời trận hoặc mất kết nối.", forfeits);
                return true;
            }
            string? fingerprint = null;
            m.results.Clear();
            foreach (var entry in input)
            {
                var id = MatchId(entry.onlineId); ValidateReport(m, id, entry.result);
                int team = m.roster.Find(entry.onlineId).team;
                string current = ResultFingerprint(entry.result, team);
                if (fingerprint != null && fingerprint != current) throw Bad("Điểm hai đội không khớp.");
                fingerprint = current;
                var result = OnlineResult(m, m.roster.Find(entry.onlineId));
                result.outcome = entry.result.outcome; result.gamesWon = entry.result.gamesWon; result.gamesLost = entry.result.gamesLost;
                result.scores = entry.result.scores; result.points = result.scores.Sum(g => g.own);
                result.validReturns = entry.result.validReturns; result.validServes = entry.result.validServes;
                m.results[entry.onlineId] = result;
                await SaveSubmittedResult(match, id, result, Now);
            }
            await FinishOnline(match, m, players);
            return true;
        });
    }
    public async Task InterruptDedicated(Guid match)
    {
        var record = await _unitOfWork.Game.Match(m => m.Matchid == match && m.Authoritymode == "dedicated" && m.Status == "PLAYING");
        if (record == null) return;
        var player = record.Gamematchplayers.First(p => !RankedBotAccounts.Contains(p.Playerid.ToString("N"))).Playerid;
        await OnlineTransaction(player, match, null, async (m, players, participants) =>
        {
            if (m.phase == "PLAYING") await EndOnline(match, m, players, null, "Máy chủ thi đấu mất kết nối; trận bị gián đoạn.", []);
            return true;
        });
    }
}
