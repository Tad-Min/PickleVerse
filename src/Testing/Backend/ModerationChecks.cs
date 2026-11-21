using System.IdentityModel.Tokens.Jwt;
using System.Net;
using API.Filters;
using API.Middlewares;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Services.Implementations;

static class ModerationChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var fixture = new FunctionalFixture(options); var actor = await fixture.Player("Admin"); var player = await fixture.Player(); var other = await fixture.Player();
        var clock = new TestClock(); var config = TestSettings.Configuration(); var settings = new Services.Configuration.ServerSettings(config);
        async Task<T> Call<T>(Func<ModerationService, Task<T>> action)
        { using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); return await action(new ModerationService(unit, clock)); }
        async Task<T> Auth<T>(Func<AuthService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db);
            return await action(new AuthService(unit, new ProfileService(unit, NullLogger<ProfileService>.Instance, settings, clock), config, new CapturedMail(), clock, settings));
        }
        async Task Reject(Func<Task> action, HttpStatusCode expected, string name)
        { bool rejected = false; try { await action(); } catch (AppException e) { rejected = e.StatusCode == expected; } check(rejected, name); }
        string email = player.ToString("N") + "@example.invalid";
        Task<AuthResponse> Login() => Auth(s => s.LoginAsync(new LoginRequest { Email = email, Password = "TestPass!234" }));
        var original = await Login();
        check(original.Success && original.AccessScope == SessionScope.Game, "active account receives a game session");
        await Reject(() => Call(s => s.Apply(other, new() { UserId = player, Reason = "Vi phạm fixture" })), HttpStatusCode.Forbidden, "player cannot issue sanctions");
        await Reject(() => Call(s => s.Apply(actor, new() { UserId = player, Restriction = UserStatus.Suspended, Reason = "Thiếu thời hạn" })), HttpStatusCode.BadRequest, "suspension requires an expiry");
        var suspended = await Call(s => s.Apply(actor, new() { UserId = player, Restriction = UserStatus.Suspended, Reason = "Ngôn ngữ không phù hợp", ExpiresAt = clock.Now.UtcDateTime.AddHours(1) }));
        check(!await Auth(s => s.ValidateSessionAsync(player, 0)), "sanction invalidates the old access token version");
        check(!(await Auth(s => s.RefreshTokenAsync(new() { RefreshToken = original.RefreshToken! }))).Success, "sanction revokes old refresh tokens");
        var restricted = await Login(); var jwt = new JwtSecurityTokenHandler().ReadJwtToken(restricted.AccessToken);
        int version = int.Parse(jwt.Claims.Single(c => c.Type == "auth_version").Value);
        check(restricted.Success && restricted.AccessScope == SessionScope.Appeals && jwt.Claims.Any(c => c.Type == "access_scope" && c.Value == "appeals"), "restricted account can authenticate for appeals");
        check(await Auth(s => s.ValidateSessionAsync(player, version, "appeals")) && !await Auth(s => s.ValidateSessionAsync(player, version, "game")), "restricted session is invalid for the game scope");
        bool reached = false; var middleware = new RestrictedSessionMiddleware(_ => { reached = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new("access_scope", "appeals"), new(System.Security.Claims.ClaimTypes.Role, "Admin")], "test")) };
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        check(context.Response.StatusCode == 403 && !reached, "scope middleware blocks all unmarked endpoints even with an admin role claim");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowRestrictedSessionAttribute()), "appeals"));
        await middleware.InvokeAsync(context);
        check(reached, "explicitly marked appeals endpoint accepts a restricted session");
        await Reject(() => Call(s => s.SubmitAppeal(other, new() { SanctionId = suspended.SanctionId, Statement = "Tôi muốn xem lại quyết định" })), HttpStatusCode.NotFound, "appeal cannot access another player's sanction");
        var request = new AppealRequest { SanctionId = suspended.SanctionId, Statement = "Tôi đề nghị kiểm tra lại bằng chứng trận đấu." };
        var appeals = await Task.WhenAll(Call(s => s.SubmitAppeal(player, request)), Call(s => s.SubmitAppeal(player, request)));
        check(appeals[0].AppealId == appeals[1].AppealId, "concurrent appeal retry creates one case");
        await Reject(() => Call(s => s.SubmitAppeal(player, new() { SanctionId = suspended.SanctionId, Statement = "Nội dung khác không ghi đè khiếu nại" })), HttpStatusCode.Conflict, "duplicate appeal cannot overwrite evidence");
        var second = await Call(s => s.Apply(actor, new() { UserId = player, Reason = "Quyết định độc lập thứ hai" }));
        await Call(s => s.ReviewAppeal(actor, appeals[0].AppealId, new() { Status = AppealStatus.Accepted, Reason = "Bằng chứng không đủ xác nhận" }));
        using (var db = new PickleVerseContext(options))
            check((await db.Users.SingleAsync(u => u.Userid == player)).Status == UserStatus.Banned
                && (await db.UserSanctions.SingleAsync(x => x.SanctionId == suspended.SanctionId)).RevokedAt != null, "accepted appeal revokes only its linked decision");
        await Reject(() => Call(s => s.ReviewAppeal(actor, appeals[0].AppealId, new() { Status = AppealStatus.Declined, Reason = "Thay kết luận cũ" })), HttpStatusCode.Conflict, "final appeal decision is immutable");
        await Call(async s => { await s.Revoke(actor, player, "Đã xem xét và thu hồi hạn chế"); return true; });
        check((await Login()).AccessScope == SessionScope.Game, "unban restores game login");
        await Call(s => s.Apply(actor, new() { UserId = player, Restriction = UserStatus.Suspended, Reason = "Tạm ngưng có thời hạn", ExpiresAt = clock.Now.UtcDateTime.AddMinutes(5) }));
        clock.Now = clock.Now.AddMinutes(6);
        check((await Login()).AccessScope == SessionScope.Game, "expired suspension restores rights without deleting its decision");
        using (var db = new PickleVerseContext(options))
            check(await db.UserSanctions.CountAsync(x => x.UserId == player) == 3 && await db.AdminAudits.CountAsync(x => x.TargetId == player) == 5,
                "sanctions, revocation and appeal retain durable audit history");
        Guid match = Guid.NewGuid();
        using (var db = new PickleVerseContext(options))
        {
            db.Gamematches.Add(new Gamematch { Matchid = match, Kind = "Custom", Status = MatchStatus.Playing, Authoritymode = MatchAuthorityMode.Dedicated,
                Createdat = clock.Now.UtcDateTime, Updatedat = clock.Now.UtcDateTime });
            db.Gamematchplayers.AddRange(new Gamematchplayer { Matchid = match, Playerid = player, Team = 0, Lastseenat = clock.Now.UtcDateTime },
                new Gamematchplayer { Matchid = match, Playerid = other, Team = 1, Lastseenat = clock.Now.UtcDateTime });
            db.Reportplayerinmatches.Add(new Reportplayerinmatch { Reporterid = other, Reportedplayerid = player, Matchid = match,
                Reason = "Bằng chứng tố cáo fixture", Createdat = clock.Now.UtcDateTime, Updatedat = clock.Now.UtcDateTime });
            await db.SaveChangesAsync();
        }
        var review = new ReviewReportRequest { ReporterId = other, ReportedPlayerId = player, MatchId = match, Status = ReportStatus.Accepted,
            BanReportedPlayer = true, BanReason = "Đã xác nhận bằng chứng", DecisionReason = "Đã xem replay và kết luận" };
        await Call(async s => { await s.ReviewReport(actor, review); return true; });
        using (var db = new PickleVerseContext(options))
            check(await db.DedicatedOperations.AnyAsync(x => x.MatchId == match) && await db.UserSanctions.AnyAsync(x => x.ReportMatchId == match && x.ReporterId == other)
                && await db.Reportplayerinmatches.AnyAsync(x => x.Matchid == match && x.Status == ReportStatus.Accepted && x.ReviewerId == actor),
                "report decision, sanction and durable stop command commit together");
        await Reject(() => Call(async s => { await s.ReviewReport(actor, review); return true; }), HttpStatusCode.Conflict, "final report cannot issue a second sanction");
    }
}
