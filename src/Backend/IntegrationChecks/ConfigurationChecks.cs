using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PickleVerse;
using PickleVerse.Multiplayer;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Configuration;
using Entities.DTOs;
using Entities.Exceptions;
using Services.Implementations;
using static Repositories.Data.PostgresCommands;

static class ConfigurationChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, TestClock clock, Action<bool, string> check)
    {
        // JWT NumericDate has whole-second precision; keep the fake clock deterministic.
        clock.Now = DateTimeOffset.FromUnixTimeSeconds(clock.Now.ToUnixTimeSeconds());
        var defaults = TestSettings.Load();
        check(defaults.Otp == new ServerSettings.OtpSettings(10, 60, 5) && defaults.Authentication == new ServerSettings.AuthenticationSettings(2, 14),
            "appsettings contains the requested OTP and token lifetimes");
        check(defaults.Matchmaking == new ServerSettings.MatchmakingSettings(360, 120) && defaults.OnlineMatch == new ServerSettings.OnlineMatchSettings(35, 8),
            "appsettings contains 360/120 second matchmaking and 35/8 second online timing");
        var expected = defaults.CreateCatalog();
        check(expected.winXp == 10 && expected.lossXp == 5 && expected.dailyBonus.coins == 200 && expected.dailyBonus.xp == 20
            && expected.weeklyBonus.coins == 1000 && expected.weeklyBonus.xp == 50 && expected.matchRewards.botWinReward == 100
            && expected.matchRewards.botLossReward == 50 && expected.matchRewards.onlineWinReward == 150 && expected.matchRewards.onlineLossReward == 75,
            "all requested progression rewards are loaded from appsettings");
        var overrides = new Dictionary<string, string?>
        {
            ["Otp:LifetimeMinutes"] = "2",
            ["Otp:ResendCooldownSeconds"] = "3",
            ["Otp:MaxAttempts"] = "2",
            ["Authentication:AccessTokenHours"] = "3",
            ["Authentication:RefreshTokenDays"] = "4",
            ["Matchmaking:BotFallbackSeconds"] = "7",
            ["Matchmaking:ReadyConfirmationSeconds"] = "6",
            ["OnlineMatch:DisconnectTimeoutSeconds"] = "9",
            ["OnlineMatch:HeartbeatSeconds"] = "2",
            ["Progression:winXp"] = "77",
            ["Progression:matchRewards:onlineWinReward"] = "222",
            ["Smtp:Username"] = "private-smtp-user",
            ["Smtp:Password"] = "private-smtp-password",
            ["Smtp:FromEmail"] = "sender@example.invalid"
        };
        var config = new ConfigurationBuilder().AddConfiguration(TestSettings.Configuration()).AddInMemoryCollection(overrides).Build();
        var settings = new ServerSettings(config);
        var mail = new CapturedMail();
        async Task<T> Auth<T>(Func<AuthService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options);
            using var uow = new UnitOfWork(db);
            return await action(new AuthService(uow, new ProfileService(uow, NullLogger<ProfileService>.Instance), config, mail, clock, settings));
        }
        async Task<T> Game<T>(Func<OnlineGameService, Task<T>> action)
        {
            await using var db = new PickleVerseContext(options);
            var sql = new PostgresCommands(db);var sqlData = new UnitOfWork(db);
            return await action(new OnlineGameService(sqlData, new GameCatalogService(sqlData, settings), clock, settings, TestRedis.State, TestRedis.Leaderboard, new MatchReplayService(sqlData, TestReplayStorage.Shared, Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchReplayService>.Instance)));
        }
        async Task Reject(Func<Task> action, HttpStatusCode status, string name)
        {
            bool rejected = false;
            try
            {
                await action();
            }
            catch (AppException e)
            {
                if (e.StatusCode != status)
                    throw;
                rejected = true;
            }
            check(rejected, name);
        }
        const string email = "config-check@example.invalid", password = "ConfigurationPass!23";
        async Task Register(string address)
        {
            var result = await Auth(s => s.RegisterAsync(new RegisterRequest { Email = address, Password = password, ConfirmPassword = password }));
            if (!result.Success)
                throw new Exception(result.Message);
        }
        await Register(email);
        Guid player;
        using (var db = new PickleVerseContext(options))
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            player = user.Userid;
            var otp = await db.Verificationemails.SingleAsync(v => v.Userid == player);
            check(otp.Expiresat - otp.Createdat == TimeSpan.FromMinutes(2), "OTP expiration follows a non-default configuration");
            var sql = new PostgresCommands(db);var sqlData = new UnitOfWork(db);
            check(await sql.One("SELECT count(*) FROM information_schema.tables WHERE table_schema='public'", r => r.GetInt64(0)) == 38
                && await sql.One("SELECT count(*) FROM information_schema.columns WHERE table_schema='public'", r => r.GetInt64(0)) == 265,
                "fresh database has 38 tables and 267 columns without duplicated participant data");
        }
        clock.Now = clock.Now.AddSeconds(2);
        await Reject(() => Auth(s => s.ResendOtpAsync(new() { Email = email })), HttpStatusCode.TooManyRequests, "resend uses the configured cooldown");
        clock.Now = clock.Now.AddSeconds(1);
        await Auth(s => s.ResendOtpAsync(new() { Email = email }));
        string code = mail.Codes[email], wrong = code == "000000" ? "111111" : "000000";
        for (int i = 0; i < 2; i++) await Auth(s => s.VerifyEmailAsync(new() { Email = email, Token = wrong }));
        check(!await Auth(s => s.VerifyEmailAsync(new() { Email = email, Token = code })), "configured two-attempt OTP limit rejects a correct code after lockout");
        clock.Now = clock.Now.AddSeconds(3);
        await Auth(s => s.ResendOtpAsync(new() { Email = email }));
        check(await Auth(s => s.VerifyEmailAsync(new() { Email = email, Token = mail.Codes[email] })), "resend permits a fresh OTP after the configured cooldown");
        var login = await Auth(s => s.LoginAsync(new() { Email = email, Password = password }));
        check(login.Success && new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).ValidTo == clock.Now.UtcDateTime.AddHours(3),
            "access token expiration follows a non-default configured lifetime");
        using (var db = new PickleVerseContext(options))
            check((await db.Refreshtokens.SingleAsync(t => t.Userid == player)).Expiresat == clock.Now.UtcDateTime.AddDays(4),
                "refresh token expiration follows a non-default configured lifetime");
        await Register("expired-config@example.invalid");
        clock.Now = clock.Now.AddMinutes(2);
        check(!await Auth(s => s.VerifyEmailAsync(new() { Email = "expired-config@example.invalid", Token = mail.Codes["expired-config@example.invalid"] })),
            "OTP expires at the configured boundary");
        var state = await Game(s => s.Read(player));
        string json = GameJson.Write(state.catalog);
        check(state.catalog.winXp == 77 && state.catalog.matchRewards.onlineWinReward == 222 && state.catalog.matchmaking.botFallbackSeconds == 7
            && state.catalog.matchmaking.readyConfirmationSeconds == 6 && state.catalog.onlineMatch.heartbeatSeconds == 2,
            "API catalog publishes configured gameplay values without a database settings table");
        check(!json.Contains("private-smtp", StringComparison.Ordinal) && !json.Contains("accessTokenHours", StringComparison.Ordinal)
            && !json.Contains("resendCooldownSeconds", StringComparison.Ordinal), "Unity catalog excludes SMTP credentials and private authentication settings");
        async Task<OnlineMatchRequest> Ranked()
        {
            var search = Guid.NewGuid();
            var ticket = await Game(s => s.BeginRankedSearch(player, new() { SearchId = search }));
            check(ticket.remainingSeconds == 7, "ranked deadline follows a non-default configured value");
            var assignment = new RankedBotRequest { SearchId = search, MatchId = Guid.NewGuid(), Session = "config-" + Guid.NewGuid().ToString("N") };
            clock.Now = clock.Now.AddSeconds(6);
            await Reject(() => Game(s => s.AssignRankedBot(player, assignment)), HttpStatusCode.Conflict, "bot fallback is rejected before the configured deadline");
            clock.Now = clock.Now.AddSeconds(1);
            var bot = await Game(s => s.AssignRankedBot(player, assignment));
            return new()
            {
                SearchId = search.ToString("N"),
                Session = assignment.Session,
                HostPlayerId = player.ToString("N"),
                Roster = new() { matchId = assignment.MatchId.ToString("N"), mode = PlayMode.Singles, region = "asia", members = [new() { id = player.ToString("N"), team = 0 }, bot] }
            };
        }
        var request = await Ranked();
        await Game(s => s.PrepareOnline(player, request));
        clock.Now = clock.Now.AddSeconds(7);
        await Reject(() => Game(s => s.StartOnline(player, request)), HttpStatusCode.Conflict, "ready confirmation expires using the configured timeout");
        await Game(s => s.CancelRankedSearch(player, new() { SearchId = Guid.Parse(request.SearchId) }));
        request = await Ranked();
        await Game(s => s.PrepareOnline(player, request));
        await Game(s => s.StartOnline(player, request));
        clock.Now = clock.Now.AddSeconds(10);
        state = await Game(s => s.Read(player));
        check(state.player.pendingMatch == null && state.player.history.Any(h => h.id == request.Roster.matchId),
            "disconnect reconciliation uses the non-default configured timeout");
        bool invalid = false;
        try
        {
            _ = new ServerSettings(new ConfigurationBuilder().AddConfiguration(config)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OnlineMatch:HeartbeatSeconds"] = "9" }).Build());
        }
        catch (InvalidOperationException)
        {
            invalid = true;
        }
        check(invalid, "startup rejects a heartbeat interval that cannot meet the disconnect timeout");
    }
}
