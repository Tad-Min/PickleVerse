using System.Net;
using Entities.Exceptions;
using Repositories.Interfaces;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class AppActivityService(IUnitOfWork unitOfWork, RedisAppActivity journal, TimeProvider clock) : IAppActivityService
{
    private readonly IAppActivityRepository activity = unitOfWork.Activity;

    public async Task<AppActivityReceipt> OpenActivity(AppActivityRequest request)
    {
        ValidateActivity(request);
        // Open before login; never replace an existing opening on a network retry.
        await journal.Open(request.SessionId, clock.GetUtcNow().ToUnixTimeSeconds() - request.ElapsedSeconds);
        return new AppActivityReceipt();
    }
    static void ValidateActivity(AppActivityRequest request)
    {
        if (request.SessionId == Guid.Empty || request.ElapsedSeconds < 0)
            throw new AppException("Phiên hoạt động không hợp lệ.", HttpStatusCode.BadRequest);
        if (request.ElapsedSeconds > RedisAppActivity.MaxElapsedSeconds)
            throw RedisAppActivity.Expired();
    }
    public async Task<AppActivityReceipt> Activity(Guid id, AppActivityRequest request)
    {
        ValidateActivity(request);
        await using var tx = await activity.BeginTransactionAsync();
        long persisted = await activity.LockActivityTotal(id)
            ?? throw new AppException("Không tìm thấy hồ sơ.", HttpStatusCode.NotFound);
        var checkpoint = clock.GetUtcNow();
        long now = checkpoint.ToUnixTimeSeconds();
        var (raw, state) = await journal.Load(id, persisted, now);
        if (persisted != state.expectedTotal && persisted != state.targetTotal)
        {
            // Redis may recover an older AOF snapshot while SQL retains a newer total.
            // Fence off every old opening instead of guessing which interval was saved.
            await journal.Save(id, raw, new RedisAppActivity.PlayerState
                { expectedTotal = persisted, targetTotal = persisted, notBefore = now });
            throw RedisAppActivity.Expired();
        }
        long total = state.targetTotal;
        var since = DateOnly.FromDateTime(checkpoint.UtcDateTime.AddHours(7)).AddDays(-8);
        state.daily ??= [];
        foreach (var day in await activity.ActivityDays(id, since))
        {
            string key = day.Key.ToString("yyyy-MM-dd");
            state.daily[key] = Math.Max(state.daily.GetValueOrDefault(key), day.Value);
        }
        foreach (var key in state.daily.Keys.Where(k => DateOnly.ParseExact(k, "yyyy-MM-dd") < since).ToArray())
            state.daily.Remove(key);
        var opening = await journal.Bind(request.SessionId, id, state.generation);
        if (opening.started < state.notBefore || now - opening.started > RedisAppActivity.MaxElapsedSeconds)
            throw RedisAppActivity.Expired();
        string sessionId = request.SessionId.ToString("N");
        if (!state.sessions.TryGetValue(sessionId, out var session))
            state.sessions[sessionId] = session = new RedisAppActivity.Counter { started = opening.started };
        long elapsed = session.elapsed;
        if (!session.closed)
        {
            elapsed = Math.Max(elapsed, Math.Min(request.ElapsedSeconds, Math.Max(0, now - session.started)));
            long cursor = session.started + session.elapsed;
            long through = session.started + elapsed;
            while (cursor < through)
            {
                var day = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime.AddHours(7));
                long boundary = new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7)).ToUnixTimeSeconds();
                long end = Math.Min(through, boundary);
                long credit = RedisAppActivity.Credit(state, cursor, end, now);
                total = checked(total + credit);
                string key = day.ToString("yyyy-MM-dd");
                state.daily[key] = checked(state.daily.GetValueOrDefault(key) + credit);
                cursor = end;
            }
            session.elapsed = elapsed;
            session.closed = request.Close;
        }
        state.expectedTotal = persisted;
        state.targetTotal = total;
        // Redis first, SQL second. If SQL rolls back, a retry recovers targetTotal;
        // if SQL committed but the response was lost, it observes the same target.
        await journal.Save(id, raw, state);
        await activity.SetActivityTotal(id, total);
        foreach (var day in state.daily)
            await activity.SaveActivityDay(id, DateOnly.ParseExact(day.Key, "yyyy-MM-dd"), day.Value);
        await tx.CommitAsync();
        return new AppActivityReceipt { totalSeconds = total, elapsedSeconds = elapsed };
    }
}
