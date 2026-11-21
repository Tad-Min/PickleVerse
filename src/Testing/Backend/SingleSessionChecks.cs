using System.IdentityModel.Tokens.Jwt;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Services.Configuration;
using Services.Implementations;

static class SingleSessionChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var player = await new FunctionalFixture(options).Player();
        var other = await new FunctionalFixture(options).Player();
        var clock = new TestClock();
        var config = TestSettings.Configuration();
        var settings = new ServerSettings(config);
        async Task<T> Auth<T>(Func<AuthService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options);
            using var unit = new UnitOfWork(db);
            return await action(new AuthService(unit, new ProfileService(unit, NullLogger<ProfileService>.Instance, settings, clock),
                config, new CapturedMail(), clock, settings));
        }
        static int Version(AuthResponse response) => int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken)
            .Claims.Single(c => c.Type == "auth_version").Value);
        Task<AuthResponse> Login(Guid? id = null, string password = "TestPass!234") => Auth(s => s.LoginAsync(new()
            { Email = (id ?? player).ToString("N") + "@example.invalid", Password = password }));
        Task<bool> Valid(AuthResponse response, Guid? id = null) => Auth(s => s.ValidateSessionAsync(id ?? player, Version(response)));
        Task<AuthResponse> Refresh(AuthResponse response) => Auth(s => s.RefreshTokenAsync(new() { RefreshToken = response.RefreshToken! }));

        var a = await Login();
        var unaffected = await Login(other);
        check(a.Success && await Valid(a), "first device receives a valid account session");
        var b = await Login();
        check(Version(b) == Version(a) + 1 && !await Valid(a) && await Valid(b), "second login replaces the first access token");
        check(!(await Refresh(a)).Success, "replaced device cannot recover with its old refresh token");
        check(await Valid(unaffected, other), "login takeover leaves other accounts signed in");
        check(!(await Login(password: "wrong-password")).Success && await Valid(b), "failed login does not revoke the active device");

        await Auth(s => s.LogoutAsync(player, a.RefreshToken!));
        check(await Valid(b), "delayed logout from the replaced device cannot end the new session");
        var refreshed = await Refresh(b);
        check(refreshed.Success && Version(refreshed) == Version(b) && await Valid(refreshed), "refresh rotates credentials within the same login epoch");
        check(!(await Refresh(b)).Success, "rotated refresh token cannot be replayed");
        await Auth(s => s.LogoutAsync(player, b.RefreshToken!));
        check(await Valid(refreshed), "delayed logout with a rotated token cannot revoke current credentials");
        await Auth(s => s.LogoutAsync(player, unaffected.RefreshToken!));
        check(await Valid(refreshed) && await Valid(unaffected, other), "logout cannot use another account's refresh token");

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Login()));
        var validity = await Task.WhenAll(concurrent.Select(response => Valid(response)));
        check(concurrent.All(r => r.Success) && concurrent.Select(Version).Distinct().Count() == 4 && validity.Count(v => v) == 1,
            "concurrent logins serialize to exactly one valid device");
        var latest = concurrent.Single(r => Version(r) == concurrent.Max(Version));
        check(await Valid(latest) && !await Valid(refreshed), "last committed login owns the account");
        using (var db = new PickleVerseContext(options))
            check(await db.Refreshtokens.CountAsync(t => t.Userid == player && !t.Isrevoked) == 1,
                "only the latest device keeps an active refresh token in PostgreSQL");

        var refreshRace = Refresh(latest);
        var loginRace = Login();
        var rotated = await refreshRace;
        var winner = await loginRace;
        check(await Valid(winner) && !await Valid(latest) && (!rotated.Success || !await Valid(rotated)),
            "login racing refresh cannot resurrect the replaced device");
        await Auth(s => s.LogoutAsync(player, winner.RefreshToken!));
        check(!await Valid(winner) && !(await Refresh(winner)).Success, "logout revokes both access and refresh credentials");
        await Auth(s => s.LogoutAsync(player, winner.RefreshToken!));
        var afterLogout = await Login();
        check(await Valid(afterLogout), "repeated logout remains safe and a later login succeeds");
        await SingleSessionHttpChecks.Run(options, check);
    }
}
