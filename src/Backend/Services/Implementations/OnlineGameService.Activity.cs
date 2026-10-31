using System.Net;
using Entities.Exceptions;

namespace Services.Implementations;

public sealed partial class OnlineGameService
{
    public async Task<AppActivityReceipt> OpenActivity(AppActivityRequest request)
    {
        ValidateActivity(request);
        // Open before login; never replace an existing opening on a network retry.
        await matchmaking.Activity.Open(request.SessionId, clock.GetUtcNow().ToUnixTimeSeconds() - request.ElapsedSeconds);
        return new AppActivityReceipt();
    }
    static void ValidateActivity(AppActivityRequest request)
    {
        if (request.SessionId == Guid.Empty || request.ElapsedSeconds < 0)
            throw Bad("Phiên hoạt động không hợp lệ.");
        if (request.ElapsedSeconds > RedisAppActivity.MaxElapsedSeconds)
            throw RedisAppActivity.Expired();
    }
    public async Task<AppActivityReceipt> Activity(Guid id, AppActivityRequest request)
    {
        ValidateActivity(request);
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        var profile = await _unitOfWork.Game.LockProfile(id, tracking: false)
            ?? throw new AppException("Không tìm thấy hồ sơ.", HttpStatusCode.NotFound);
        long persisted = profile.Timeplayedseconds;
        long now = clock.GetUtcNow().ToUnixTimeSeconds();
        var (raw, state) = await matchmaking.Activity.Load(id, persisted, now);
        if (persisted != state.expectedTotal && persisted != state.targetTotal)
        {
            // Redis may recover an older AOF snapshot while SQL retains a newer total.
            // Fence off every old opening instead of guessing which interval was saved.
            await matchmaking.Activity.Save(id, raw, new RedisAppActivity.PlayerState
                { expectedTotal = persisted, targetTotal = persisted, notBefore = now });
            throw RedisAppActivity.Expired();
        }
        long total = state.targetTotal;
        var opening = await matchmaking.Activity.Bind(request.SessionId, id, state.generation);
        if (opening.started < state.notBefore || now - opening.started > RedisAppActivity.MaxElapsedSeconds)
            throw RedisAppActivity.Expired();
        string sessionId = request.SessionId.ToString("N");
        if (!state.sessions.TryGetValue(sessionId, out var session))
            state.sessions[sessionId] = session = new RedisAppActivity.Counter { started = opening.started };
        long elapsed = session.elapsed;
        if (!session.closed)
        {
            elapsed = Math.Max(elapsed, Math.Min(request.ElapsedSeconds, Math.Max(0, now - session.started)));
            total = checked(total + RedisAppActivity.Credit(state, session.started + session.elapsed, session.started + elapsed, now));
            session.elapsed = elapsed;
            session.closed = request.Close;
        }
        state.expectedTotal = persisted;
        state.targetTotal = total;
        // Redis first, SQL second. If SQL rolls back, a retry recovers targetTotal;
        // if SQL committed but the response was lost, it observes the same target.
        await matchmaking.Activity.Save(id, raw, state);
        await _unitOfWork.Game.SetActivityTotal(id, total);
        await tx.CommitAsync();
        return new AppActivityReceipt { totalSeconds = total, elapsedSeconds = elapsed };
    }
}
