using System.Net;
using Services.Exceptions;
using static Services.Game.GameSql;

namespace Services.Game;

public sealed partial class OnlineGameService
{
    public async Task<AppActivityReceipt> OpenActivity(AppActivityRequest request)
    {
        ValidateActivity(request);
        // The first request is sent at app boot, before login. Startup/retry time is
        // retained in memory by Unity; later checkpoints are bounded by this server clock.
        await sql.Run("INSERT INTO appactivitysessions(sessionid,startedat) VALUES(@session,@start) ON CONFLICT DO NOTHING",
            P("session", request.SessionId), P("start", Now.AddSeconds(-request.ElapsedSeconds)));
        return new AppActivityReceipt();
    }
    static void ValidateActivity(AppActivityRequest request)
    {
        if (request.SessionId == Guid.Empty || request.ElapsedSeconds is < 0 or > 604800)
            throw Bad("Phiên hoạt động không hợp lệ.");
    }
    public async Task<AppActivityReceipt> Activity(Guid id, AppActivityRequest request)
    {
        ValidateActivity(request);
        await using var tx = await db.Database.BeginTransactionAsync();
        var total = await sql.One("SELECT timeplayedseconds FROM playerprofiles WHERE userid=@id FOR UPDATE", r => (long?)r.GetInt64(0), P("id", id))
            ?? throw new AppException("Không tìm thấy hồ sơ.", HttpStatusCode.NotFound);
        var session = await sql.One("SELECT playerid,startedat,elapsedseconds,closed FROM appactivitysessions WHERE sessionid=@session FOR UPDATE",
            r => new { player = r.IsDBNull(0) ? (Guid?)null : r.GetGuid(0), start = r.GetDateTime(1), elapsed = r.GetInt64(2), closed = r.GetBoolean(3) }, P("session", request.SessionId))
            ?? throw Bad("Phiên hoạt động chưa được mở.");
        if (session.player.HasValue && session.player != id)
            throw new AppException("Phiên hoạt động thuộc tài khoản khác.", HttpStatusCode.Forbidden);
        long elapsed = session.elapsed;
        if (!session.closed)
        {
            elapsed = Math.Max(elapsed, Math.Min(request.ElapsedSeconds, (long)Math.Max(0, (Now - session.start).TotalSeconds)));
            var through = session.start.AddSeconds(elapsed);
            // Serializing on the profile and crediting only uncovered time prevents
            // retries and overlapping app instances from adding the same interval twice.
            var from = session.start.AddSeconds(session.elapsed);
            var covered = await sql.Query("SELECT startedat,creditedthrough FROM appactivitysessions WHERE playerid=@id AND creditedthrough>@from AND startedat<@through ORDER BY startedat",
                r => new { start = r.GetDateTime(0), end = r.GetDateTime(1) }, P("id", id), P("from", from), P("through", through));
            var cursor = from;
            double uncovered = 0;
            foreach (var interval in covered)
            {
                var start = interval.start < from ? from : interval.start;
                var end = interval.end > through ? through : interval.end;
                if (start > cursor)
                    uncovered += (start - cursor).TotalSeconds;
                if (end > cursor)
                    cursor = end;
            }
            if (through > cursor)
                uncovered += (through - cursor).TotalSeconds;
            total += Math.Max(0, (long)uncovered);
            await sql.Run("UPDATE appactivitysessions SET playerid=@id,elapsedseconds=@elapsed,creditedthrough=@through,closed=@closed WHERE sessionid=@session",
                P("id", id), P("elapsed", elapsed), P("through", through), P("closed", request.Close), P("session", request.SessionId));
            await sql.Run("UPDATE playerprofiles SET timeplayedseconds=@total WHERE userid=@id", P("total", total), P("id", id));
        }
        await tx.CommitAsync();
        return new AppActivityReceipt { totalSeconds = total, elapsedSeconds = elapsed };
    }
}
