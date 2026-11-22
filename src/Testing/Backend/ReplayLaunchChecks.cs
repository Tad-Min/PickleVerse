using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using API.Controllers;
using API.Filters;
using API.Middlewares;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Game;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using PickleVerse.Progression;
using PickleVerse.Replays;
using PickleVerse.Services.Api;
using Repositories.Caching;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Implementations;
using Services.Interfaces;

static class ReplayLaunchChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var clock = new TestClock(); var fixture = new FunctionalFixture(options);
        Guid admin = await fixture.Player("Admin"), player = await fixture.Player();
        var storage = new TestReplayStorage();
        var grants = new ReplayLaunchRepository(TestRedis.Connection, TestRedis.Settings);
        MatchReplayService Replays(IUnitOfWork unit) => new(unit, storage, NullLogger<MatchReplayService>.Instance);
        async Task<T> Launch<T>(Func<ReplayLaunchService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db);
            return await action(new ReplayLaunchService(unit, Replays(unit), grants, clock));
        }
        async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db); var settings = TestSettings.Load();
            return await action(new OnlineGameService(unit, new GameCatalogService(unit, settings), clock, settings,
                TestRedis.State, TestRedis.Leaderboard, Replays(unit)));
        }
        async Task Reject(Func<Task> action, HttpStatusCode status, string name)
        {
            bool rejected = false; try { await action(); } catch (AppException error) { rejected = error.StatusCode == status; }
            check(rejected, name);
        }
        async Task ChangeAdmin(Action<Entities.Models.User> change)
        {
            using var db = new PickleVerseContext(options); change(await db.Users.SingleAsync(u => u.Userid == admin)); await db.SaveChangesAsync();
        }
        var result = (await Game(s => s.StartBot(player, new() { RequestId = Guid.NewGuid().ToString("N"), Kind = MatchKind.BotPractice }))).player.pendingMatch;
        Guid match = Guid.Parse(result.id);
        var metadata = new ReplayMetadata { playerId = player.ToString("N"), matchId = result.id, duration = 1,
            actors = [new() { id = player.ToString("N"), name = "Player", team = 0, visualId = "Male", bones = [] },
                      new() { id = result.opponents[0].id, name = "Bot", team = 1, visualId = "Female", bones = [] }] };
        using (var db = new PickleVerseContext(options))
        {
            using var unit = new UnitOfWork(db); var replays = Replays(unit);
            await replays.Save(player, match, metadata);
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, true))
                gzip.Write(Encoding.UTF8.GetBytes(GameJson.Write(new ReplayChunk { frames = [new() { time = 0, ball = [0, 1, 0], actors = [new() { root = new float[10], bones = [] }, new() { root = new float[10], bones = [] }] }] })));
            await replays.Upload(player, match, 0, new() { Data = Convert.ToBase64String(buffer.ToArray()) });
            metadata.complete = true; await replays.Save(player, match, metadata);
        }
        result.outcome = MatchOutcome.Won; result.gamesWon = 2; result.scores = [new(11, 0, true), new(11, 0, true)];
        await Game(s => s.FinishBot(player, new() { RequestId = Guid.NewGuid().ToString("N"), Result = result }));
        async Task<(string code, ReplayLaunchLink link)> Issue(int version = 0)
        {
            var link = await Launch(s => s.Issue(admin, version, match));
            check(ReplayWebLink.TryParse(link.Url, out var parsed) && parsed.MatchId == match.ToString("N"), "issued web link binds to the actual Unity URI parser");
            ReplayWebLink.TryParse(link.Url, out var value); return (value.Code, link);
        }
        await Reject(() => Launch(s => s.Issue(player, 0, match)), HttpStatusCode.Forbidden, "non-Admin cannot issue viewing tickets");
        await Reject(() => Launch(s => s.Issue(admin, 9, match)), HttpStatusCode.Forbidden, "stale Admin epoch cannot issue a ticket");
        await Reject(() => Launch(s => s.Issue(admin, 0, Guid.NewGuid())), HttpStatusCode.NotFound, "unavailable replay cannot receive a ticket");
        var issued = await Issue();
        check(issued.link.ExpiresAt == clock.Now.UtcDateTime.AddMinutes(2), "launch code expires after two minutes");
        var races = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        { try { return await Launch(s => s.Redeem(issued.code)); } catch (AppException e) when (e.StatusCode == HttpStatusCode.Unauthorized) { return null; } }));
        check(races.Count(v => v != null) == 1, "eight concurrent redeems have exactly one winner in Redis");
        var view = races.Single(v => v != null)!;
        check(view.Grant != issued.code && view.ExpiresAt == clock.Now.UtcDateTime.AddHours(2), "view grant differs from launch code and is bounded to two hours");
        check((await Launch(s => s.Read(view.Grant, match))).matchId == result.id && (await Launch(s => s.Chunk(view.Grant, match, 0))).Data.Length > 0,
            "read-only grant accesses metadata and actual replay chunks");
        await Reject(() => Launch(s => s.Redeem(issued.code)), HttpStatusCode.Unauthorized, "consumed launch code cannot be used again");
        await Reject(() => Launch(s => s.Read(issued.code, match)), HttpStatusCode.Unauthorized, "launch code cannot act as a viewing grant");
        await Reject(() => Launch(s => s.Read(view.Grant, Guid.NewGuid())), HttpStatusCode.Forbidden, "view grant cannot access another match");
        await Reject(() => Launch(s => s.Chunk(new string('a', 64), match, 0)), HttpStatusCode.Unauthorized, "unknown viewing grant is rejected");
        await Reject(() => Launch(s => s.Chunk(view.Grant, match, 1)), HttpStatusCode.NotFound, "view grant cannot read an absent chunk");
        var retry = await Issue(); storage.FailReads = true;
        bool failed = false; try { await Launch(s => s.Redeem(retry.code)); } catch (IOException) { failed = true; }
        storage.FailReads = false;
        check(failed && await grants.ReadCode(retry.code) != null && (await Launch(s => s.Redeem(retry.code))).MatchId == match,
            "temporary storage failure does not consume a launch ticket");
        var expired = await Issue(); clock.Now = clock.Now.AddMinutes(3);
        await Reject(() => Launch(s => s.Redeem(expired.code)), HttpStatusCode.Unauthorized, "expired launch ticket is rejected even before Redis eviction");
        clock.Now = clock.Now.AddHours(2);
        await Reject(() => Launch(s => s.Read(view.Grant, match)), HttpStatusCode.Unauthorized, "expired viewing grant is rejected");
        clock.Now = DateTimeOffset.UtcNow;
        await ChangeAdmin(u => u.Authversion++);
        await Reject(() => Launch(s => s.Read(view.Grant, match)), HttpStatusCode.Forbidden, "Admin login replacement revokes existing viewing grants");
        var revoked = await Issue(1);
        await ChangeAdmin(u => u.Status = UserStatus.Banned);
        await Reject(() => Launch(s => s.Redeem(revoked.code)), HttpStatusCode.Forbidden, "banned Admin cannot redeem an issued ticket");
        await ChangeAdmin(u => u.Status = UserStatus.Active);
        var current = await Launch(s => s.Redeem(revoked.code));
        using (var db = new PickleVerseContext(options))
        {
            int role = await db.Roles.Where(r => r.Rolename == "Player").Select(r => r.Roleid).SingleAsync();
            await ChangeAdmin(u => u.Roleid = role);
        }
        await Reject(() => Launch(s => s.Chunk(current.Grant, match, 0)), HttpStatusCode.Forbidden, "demotion revokes viewing permission on every chunk read");
        using (var db = new PickleVerseContext(options))
        {
            int role = await db.Roles.Where(r => r.Rolename == "Admin").Select(r => r.Roleid).SingleAsync();
            await ChangeAdmin(u => u.Roleid = role);
        }
        await Http(options, storage, clock, admin, player, match, check);
    }

    static async Task Http(DbContextOptions<PickleVerseContext> options, TestReplayStorage storage, TimeProvider clock,
        Guid admin, Guid player, Guid match, Action<bool, string> check)
    {
        var config = TestSettings.Configuration(); var settings = TestSettings.Load();
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddScoped(_ => new PickleVerseContext(options));
        builder.Services.AddScoped<IUnitOfWork>(sp => new UnitOfWork(sp.GetRequiredService<PickleVerseContext>()));
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton(new ReplayLaunchRepository(TestRedis.Connection, TestRedis.Settings));
        builder.Services.AddScoped<IMatchReplayService>(sp => new MatchReplayService(sp.GetRequiredService<IUnitOfWork>(), storage, NullLogger<MatchReplayService>.Instance));
        builder.Services.AddScoped<IReplayLaunchService, ReplayLaunchService>();
        builder.Services.AddScoped<IAuthService>(sp =>
        {
            var unit = sp.GetRequiredService<IUnitOfWork>();
            return new AuthService(unit, new ProfileService(unit, NullLogger<ProfileService>.Instance, settings, clock), config, new CapturedMail(), clock, settings);
        });
        builder.Services.AddControllers(o => o.Filters.Add<ApiResponseFilter>()).AddApplicationPart(typeof(ReplayLaunchController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.IncludeFields = true);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"] ?? "PickleVerseAPI", ValidAudience = config["Jwt:Audience"] ?? "PickleVerseClient",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)) };
            o.Events = new JwtBearerEvents { OnTokenValidated = async context =>
            {
                var user = context.Principal;
                if (!Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || !int.TryParse(user?.FindFirstValue("auth_version"), out int version)
                    || !await context.HttpContext.RequestServices.GetRequiredService<IAuthService>().ValidateSessionAsync(id, version, user?.FindFirstValue("access_scope") ?? "game")) context.Fail("Expired login");
            } };
        });
        builder.Services.AddAuthorization(); builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); builder.Services.AddProblemDetails();
        builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("auth", limiter => { limiter.PermitLimit = 20; limiter.Window = TimeSpan.FromMinutes(1); }));
        await using var app = builder.Build(); app.UseExceptionHandler(); app.UseRouting(); app.UseAuthentication();
        app.UseMiddleware<RestrictedSessionMiddleware>(); app.UseRateLimiter(); app.UseAuthorization(); app.MapControllers(); await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) }; var api = new ApiClient(new Transport(http));
        Task<AuthResponse> Login(Guid id) => api.SendAsync<AuthResponse>("POST", "api/Auth/login", new { Email = id.ToString("N") + "@example.invalid", Password = "TestPass!234" });
        var adminLogin = await Login(admin); var playerLogin = await Login(player);
        async Task Status(string method, string path, string? token, HttpStatusCode expected, string name, object? body = null)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request); check(response.StatusCode == expected, name);
        }
        await Status("POST", $"api/replay-launch/{match}", null, HttpStatusCode.Unauthorized, "HTTP issue requires an authenticated Admin");
        await Status("POST", $"api/replay-launch/{match}", playerLogin.AccessToken, HttpStatusCode.Forbidden, "HTTP issue rejects a Player JWT");
        var link = await api.SendAsync<ReplayLaunchLink>("POST", $"api/replay-launch/{match}", token: adminLogin.AccessToken);
        check(ReplayWebLink.TryParse(link.Url, out var parsed), "Unity ApiClient decodes actual replay-launch HTTP response");
        ReplayWebLink.TryParse(link.Url, out var selected);
        var access = await api.SendAsync<ReplayLaunchView>("POST", "api/replay-launch/redeem", new { code = selected.Code });
        check(access.MatchId == match && access.Metadata.actors.Count == 2, "anonymous redemption returns scoped access and replay metadata");
        check((await api.SendAsync<ReplayMetadata>("GET", $"api/replay-launch/view/{match}", token: access.Grant)).matchId == match.ToString("N"),
            "opaque bearer grant survives JWT middleware on anonymous view endpoint");
        check((await api.SendAsync<ReplayChunkUpload>("GET", $"api/replay-launch/view/{match}/chunks/0", token: access.Grant)).Data.Length > 0,
            "actual Unity ApiClient can download a replay chunk with a viewing grant");
        await Status("POST", "api/replay-launch/redeem", null, HttpStatusCode.Unauthorized, "HTTP rejects repeated redemption", new { code = selected.Code });
        await Status("POST", "api/replay-launch/redeem", null, HttpStatusCode.BadRequest, "HTTP validates malformed launch code", new { code = "bad" });
        await Status("GET", $"api/replay-launch/view/{Guid.NewGuid()}/chunks/0", access.Grant, HttpStatusCode.Forbidden, "HTTP enforces match scope on chunks");
        await Status("GET", $"api/replay-launch/view/{match}", playerLogin.AccessToken, HttpStatusCode.Unauthorized, "game JWT cannot be mistaken for replay viewing grant");
        await Status("GET", "api/Auth/session", access.Grant, HttpStatusCode.Unauthorized, "replay grant cannot become a login session");
        await Status("GET", "api/Auth/session", playerLogin.AccessToken, HttpStatusCode.OK, "replay viewing preserves existing player account session");
        await Status("GET", "api/Auth/session", adminLogin.AccessToken, HttpStatusCode.OK, "replay launch preserves the issuing web Admin session");
        await Login(admin);
        await Status("GET", $"api/replay-launch/view/{match}/chunks/0", access.Grant, HttpStatusCode.Forbidden, "HTTP checks issuer revocation again for each replay chunk");
        await app.StopAsync();
    }
    sealed class Transport(HttpClient http) : IApiTransport
    {
        public async Task<ApiHttpResponse> SendAsync(string method, string path, string? json, string? token, CancellationToken cancellation)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, cancellation);
            return new ApiHttpResponse((long)response.StatusCode, await response.Content.ReadAsStringAsync(cancellation));
        }
    }
}
