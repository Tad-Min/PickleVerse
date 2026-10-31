using PickleVerse.Progression;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    async Task FinalizeParticipant(Guid player,MatchResult result)
    {
        var match=Guid.Parse(result.id);var now=Now;
        var outcome=result.outcome==MatchOutcome.Won?"WIN":result.outcome==MatchOutcome.Lost?"LOSE":"INTERRUPTED";
        var count=await _unitOfWork.Game.FinalizeParticipant(match, player, outcome, result.localForfeit, result.validReturns, result.validServes, result.reason, now);
        if(count!=1)throw Conflict("Kết quả của người tham gia đã được xử lý hoặc không tồn tại.");
    }

    async Task FinalizeMatch(Guid match)
    {
        var now=Now;
        var count=await _unitOfWork.Game.FinalizeMatch(match, now);
        if(count!=1)throw Conflict("Trận đã kết thúc hoặc đã bị hủy.");
    }

    async Task SaveSubmittedResult(Guid match,Guid player,MatchResult result,DateTime at) =>
        await _unitOfWork.Game.SaveSubmittedResult(match, player, result.validReturns, result.validServes, at);
}
