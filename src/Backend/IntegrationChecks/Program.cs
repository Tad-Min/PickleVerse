using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Implementations;
using Services.Interfaces;
using Services.Security;
using Services.Exceptions;

// Uses an isolated temporary database on the dev PostgreSQL server. No real email is sent.
var connection = Environment.GetEnvironmentVariable("PKV_TEST_POSTGRES")
    ?? throw new InvalidOperationException("Set PKV_TEST_POSTGRES to a dev PostgreSQL connection string.");
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
if (args.Contains("--check-docker-smtp"))
{
    await UnityFixture.CheckDockerSmtpAsync(connection, root);
    return;
}
if (args.Contains("--check-admin-authorization"))
{
    await UnityFixture.CheckAdminAuthorizationAsync(connection, root);
    return;
}
var photonFixtureArg=args.FirstOrDefault(a=>a.StartsWith("--photon-fixture",StringComparison.Ordinal));
if (args.Contains("--unity-fixture") || args.Contains("--cleanup-unity-fixture") || photonFixtureArg!=null)
{
    string suffix=photonFixtureArg?["--photon-fixture".Length..]??"";
    if(suffix!=""&&suffix!="-2"&&suffix!="-3")throw new InvalidOperationException("Invalid fixture suffix.");
    await UnityFixture.RunAsync(connection, root, args.Contains("--cleanup-unity-fixture"),photonFixtureArg!=null,suffix);
    return;
}
var database = "pkv_auth_checks_" + Guid.NewGuid().ToString("N");
var adminBuilder = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", Pooling = false };
await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
await admin.OpenAsync();
await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin)) await create.ExecuteNonQueryAsync();
var testBuilder = new NpgsqlConnectionStringBuilder(connection) { Database = database, Pooling = false };
var options = new DbContextOptionsBuilder<PickleVerseContext>().UseNpgsql(testBuilder.ConnectionString).Options;
var mail = new CapturedMail();
var time = new TestClock();
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Jwt:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)) }).Build();
int checks = 0;
void Check(bool passed, string name) { if (!passed) throw new Exception("FAILED: " + name); checks++; Console.WriteLine("PASS: " + name); }
async Task<T> Call<T>(Func<AuthService, Task<T>> action)
{
    using var context = new PickleVerseContext(options); using var uow = new UnitOfWork(context);
    return await action(new AuthService(uow, new ProfileService(uow, NullLogger<ProfileService>.Instance), config, mail, time));
}
var generatedNames = new HashSet<string>(StringComparer.Ordinal);
async Task<string> Register(string email, string label, bool isMale = true)
{
    var result = await Call(s => s.RegisterAsync(new RegisterRequest { Email = email, Password = "IntegrationPass!23", ConfirmPassword = "IntegrationPass!23", IsMale = isMale }));
    Check(result.Success && result.AccessToken == null, "register requires verification: " + label);
    using var context = new PickleVerseContext(options);
    var profile = await context.Playerprofiles.Include(p => p.User).SingleAsync(p => p.User.Email == email);
    Check(System.Text.RegularExpressions.Regex.IsMatch(profile.Playername, "^player[0-9a-f]{12}$") && generatedNames.Add(profile.Playername), "server generates a distinct default player name: " + label);
    Check(profile.Ismale == isMale, "registration preserves selected gender: " + label);
    return profile.Playername;
}
Task<bool> Verify(string email, string code) => Call(s => s.VerifyEmailAsync(new VerifyEmailRequest { Email = email, Token = code }));
Task<AuthResponse> Login(string email) => Call(s => s.LoginAsync(new LoginRequest { Email = email, Password = "IntegrationPass!23" }));
Task<AuthResponse> Refresh(string token) => Call(s => s.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = token }));
async Task Resend(string email) => await Call(async s => { await s.ResendOtpAsync(new ResendOtpRequest { Email = email }); return true; });
try
{
    await using (var setup = new NpgsqlConnection(testBuilder.ConnectionString))
    {
        await setup.OpenAsync();
        foreach (var script in new[] { "Database.sql", "DevAuthSetup.sql" })
        {
            await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(Path.Combine(root, "src", script)), setup);
            await command.ExecuteNonQueryAsync();
        }
    }
    if (args.Contains("--check-online-game") || args.Contains("--check-online-multiplayer") || args.Contains("--check-online-social"))
    {
        await using var fixture = new NpgsqlConnection(testBuilder.ConnectionString);
        await fixture.OpenAsync();
        await using var command = new NpgsqlCommand("""
            WITH inserted AS (INSERT INTO users(email,passwordhash,roleid)
                SELECT 'race-check@example.invalid','test-account-cannot-login',roleid FROM roles WHERE rolename='Player' RETURNING userid)
            INSERT INTO playerprofiles(userid,playername) SELECT userid,'GameCheck' FROM inserted;
            """,fixture);
        await command.ExecuteNonQueryAsync();
        if(args.Contains("--check-online-social"))await SocialChecks.Run(options,Check);
        else if(args.Contains("--check-online-multiplayer"))
        {
            await using var testContext=new PickleVerseContext(options);
            var id=await testContext.Users.Where(u=>u.Email=="race-check@example.invalid").Select(u=>u.Userid).SingleAsync();
            await OnlineMatchChecks.Run(options,time,Check,id);
            await SocialChecks.Run(options,Check);
        }
        else await GameChecks.RunAsync(options, time, Check, root);
        Console.WriteLine($"SUCCESS: {checks} online game checks; disposable PostgreSQL database removed.");
        return;
    }
    bool ValidRegistration(RegisterRequest request) => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
        request, new System.ComponentModel.DataAnnotations.ValidationContext(request), new List<System.ComponentModel.DataAnnotations.ValidationResult>(), validateAllProperties: true);
    Check(ValidRegistration(new RegisterRequest { Email = "validation@example.invalid", Password = "IntegrationPass!23", ConfirmPassword = "IntegrationPass!23", IsMale = false }), "registration accepts the four-field contract without a player name");
    Check(!ValidRegistration(new RegisterRequest { Email = "validation@example.invalid", Password = "IntegrationPass!23", ConfirmPassword = "DifferentPass!23" }), "registration validation rejects mismatched confirmation");
    Check(!ValidRegistration(new RegisterRequest { Email = "validation@example.invalid", Password = "IntegrationPass!23" }), "registration validation requires confirmation");
    var invalidRegistration = await Call(s => s.RegisterAsync(new RegisterRequest { Email = "invalid@example.invalid", Password = "IntegrationPass!23", ConfirmPassword = "DifferentPass!23" }));
    Check(!invalidRegistration.Success && invalidRegistration.Code == "password_confirmation_mismatch", "service rejects mismatched confirmation");
    using (var context = new PickleVerseContext(options))
        Check(!await context.Users.AnyAsync() && mail.Codes.Count == 0, "invalid confirmation creates no account or OTP");
    const string email = "online-check@example.invalid";
    string defaultName = await Register(email, "AuthCheck", isMale: false); string code = mail.Codes[email];
    Check((await Login(email)).Code == "email_not_verified", "unverified login rejected");
    Check(!await Verify("other@example.invalid", code), "OTP bound to email");
    Check(!await Verify(email, "not-six-digits"), "malformed OTP rejected");
    Check(await Verify(email, code), "valid OTP accepted");
    Check(!await Verify(email, code), "OTP replay rejected");
    var login = await Login(email);
    Check(login.Success && login.Profile?.PlayerName == defaultName && !login.Profile.IsMale, "login returns generated name and selected gender from server profile");
    using (var context = new PickleVerseContext(options))
    {
        Check(context.Database.GetDbConnection().Database == database, "DbContext preserves injected Docker/test connection");
        var otp = await context.Verificationemails.SingleAsync();
        Check(otp.Token.Length == 64 && otp.Token != code, "OTP is stored as keyed hash");
        Check((await context.Refreshtokens.SingleAsync()).Tokenhash != login.RefreshToken, "refresh token is stored as hash");
    }
    var refreshed = await Refresh(login.RefreshToken!);
    Check(refreshed.Success && refreshed.RefreshToken != login.RefreshToken, "refresh rotates token");
    Check(!(await Refresh(login.RefreshToken!)).Success, "rotated token cannot be reused");
    await Call(s => s.LogoutAsync(login.Profile!.UserId, refreshed.RefreshToken!));
    Check(!(await Refresh(refreshed.RefreshToken!)).Success, "logout revokes refresh token");

    var beforeBan = await Login(email);
    using (var context = new PickleVerseContext(options))
    {
        var user = await context.Users.SingleAsync(); user.Status = "SUSPENDED"; await context.SaveChangesAsync();
    }
    Check((await Login(email)).Code == "account_inactive", "suspended login rejected");
    Check((await Refresh(beforeBan.RefreshToken!)).Code == "account_inactive", "suspended refresh rejected");

    // Force one real unique-key collision; PostgreSQL sequence increments survive a rollback.
    using (var context = new PickleVerseContext(options))
        await context.Database.ExecuteSqlRawAsync("""
            CREATE SEQUENCE registration_name_collision;
            CREATE FUNCTION collide_default_name_once() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF nextval('registration_name_collision') = 1 THEN
                    SELECT playername INTO NEW.playername FROM playerprofiles LIMIT 1;
                END IF;
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER registration_name_collision BEFORE INSERT ON playerprofiles
                FOR EACH ROW EXECUTE FUNCTION collide_default_name_once();
            """);
    const string lockedEmail = "locked-check@example.invalid";
    await Register(lockedEmail, "LockCheck"); var lockedCode = mail.Codes[lockedEmail];
    await using (var collision = new NpgsqlConnection(testBuilder.ConnectionString))
    {
        await collision.OpenAsync();
        await using var attempts = new NpgsqlCommand("SELECT last_value FROM registration_name_collision", collision);
        Check((long)(await attempts.ExecuteScalarAsync())! == 2L, "default-name collision rolls back and retries with a new name");
        await using var cleanup = new NpgsqlCommand("DROP TRIGGER registration_name_collision ON playerprofiles; DROP FUNCTION collide_default_name_once(); DROP SEQUENCE registration_name_collision;", collision);
        await cleanup.ExecuteNonQueryAsync();
    }
    for (int i = 0; i < EmailOtpPolicy.MaxAttempts; i++) Check(!await Verify(lockedEmail, lockedCode == "000000" ? "111111" : "000000"), "incorrect OTP attempt " + (i + 1));
    Check(!await Verify(lockedEmail, lockedCode), "five failures lock the OTP");
    bool cooldown = false;
    try { await Resend(lockedEmail); } catch (AppException ex) { cooldown = ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests; }
    Check(cooldown, "resend cooldown enforced");
    time.Now = time.Now.AddSeconds(61); await Resend(lockedEmail);
    Check(await Verify(lockedEmail, mail.Codes[lockedEmail]), "resend issues a usable OTP after lockout");

    const string expiredEmail = "expired-check@example.invalid";
    await Register(expiredEmail, "ExpiryCheck"); time.Now = time.Now.AddMinutes(11);
    Check(!await Verify(expiredEmail, mail.Codes[expiredEmail]), "expired OTP rejected");

    const string raceEmail = "race-check@example.invalid";
    await Register(raceEmail, "RaceCheck");
    var racing = await Task.WhenAll(Verify(raceEmail, mail.Codes[raceEmail]), Verify(raceEmail, mail.Codes[raceEmail]));
    Check(racing.Count(x => x) == 1, "concurrent verification consumes OTP once");
    var raceLogin = await Login(raceEmail);
    var refreshRace = await Task.WhenAll(Refresh(raceLogin.RefreshToken!), Refresh(raceLogin.RefreshToken!));
    Check(refreshRace.Count(x => x.Success) == 1, "concurrent refresh consumes token once");
    if (Environment.GetEnvironmentVariable("PKV_RUN_SMTP_TEST") == "1")
    {
        var smtp = File.ReadAllLines(Path.Combine(root, ".env")).Where(l => l.Contains('=') && !l.StartsWith('#'))
            .Select(l => l.Split('=', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        foreach (var entry in new[] { ("SMTP_USERNAME", "Smtp:Username"), ("SMTP_APP_PASSWORD", "Smtp:Password"), ("SMTP_FROM_EMAIL", "Smtp:FromEmail") })
            config[entry.Item2] = smtp.GetValueOrDefault(entry.Item1);
        mail.LiveSender = new SmtpEmailSender(config, NullLogger<SmtpEmailSender>.Instance);
        mail.Recipient = config["Smtp:Username"]!;
        await Register(mail.Recipient, "GmailCheck");
        Check(await Verify(mail.Recipient, mail.Codes[mail.Recipient]), "real Gmail SMTP accepted OTP; verification succeeds");
        Check((await Login(mail.Recipient)).Success, "real-email flow permits login after OTP");
    }
    await GameChecks.RunAsync(options, time, Check, root);
    Console.WriteLine($"SUCCESS: {checks} checks; real PostgreSQL; captured email adapter; temporary database removed.");
}
finally
{
    await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
    await drop.ExecuteNonQueryAsync();
}

sealed class CapturedMail : IEmailSender
{
    public Dictionary<string, string> Codes { get; } = new();
    public IEmailSender? LiveSender { get; set; }
    public string? Recipient { get; set; }
    public void EnsureConfigured() { }
    public async Task SendVerificationCodeAsync(string email, string code)
    {
        if (email == Recipient && LiveSender != null) await LiveSender.SendVerificationCodeAsync(email, code);
        Codes[email] = code;
    }
}
sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

static class UnityFixture
{
    public static async Task CheckDockerSmtpAsync(string connection, string root)
    {
        var settings = File.ReadAllLines(Path.Combine(root, ".env")).Where(l => l.Contains('=') && !l.StartsWith('#'))
            .Select(l => l.Split('=', 2)).ToDictionary(p => p[0].Trim(), p => p[1].Trim());
        string email = settings["SMTP_USERNAME"].ToLowerInvariant();
        var options = new DbContextOptionsBuilder<PickleVerseContext>().UseNpgsql(connection).Options;
        using var context = new PickleVerseContext(options);
        if (await context.Users.AnyAsync(u => u.Email == email)) throw new InvalidOperationException("The SMTP recipient already has an account; refusing to modify it.");
        var createdAfter = DateTime.UtcNow.AddSeconds(-2);
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:8080"), Timeout = TimeSpan.FromSeconds(35) };
            using var body = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                email, password, confirmPassword = password, isMale = true
            }), System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync("/api/Auth/register", body);
            response.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!json.RootElement.GetProperty("success").GetBoolean()) throw new Exception("Docker OTP registration failed.");
            Console.WriteLine("PASS: Docker API registration returned 200 after Gmail SMTP accepted the real OTP.");
        }
        finally
        {
            var created = await context.Users.Include(u => u.Playerprofile).SingleOrDefaultAsync(u => u.Email == email && u.Createdat >= createdAfter && u.Playerprofile != null && u.Playerprofile.Playername.StartsWith("player"));
            if (created != null) { context.Users.Remove(created); await context.SaveChangesAsync(); }
            Console.WriteLine("Temporary Docker SMTP test account removed; no existing account was modified.");
        }
    }

    public static async Task CheckAdminAuthorizationAsync(string connection, string root)
    {
        var fixture = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(root, "artifacts", "unity-account-fixture.json")))!;
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:8080"), Timeout = TimeSpan.FromSeconds(15) };
        using var anonymous = await http.GetAsync("/api/Admin/users");
        if ((int)anonymous.StatusCode != 401) throw new Exception("Anonymous admin access must return 401.");
        async Task<int> AuthenticatedStatus()
        {
            using var body = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { email = fixture["email"], password = fixture["password"] }), System.Text.Encoding.UTF8, "application/json");
            using var login = await http.PostAsync("/api/Auth/login", body); login.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
            using var response = await http.GetAsync("/api/Admin/users");
            return (int)response.StatusCode;
        }
        if (await AuthenticatedStatus() != 403) throw new Exception("Player admin access must return 403.");
        var options = new DbContextOptionsBuilder<PickleVerseContext>().UseNpgsql(connection).Options;
        using var context = new PickleVerseContext(options); var id = Guid.Parse(fixture["userId"]);
        var user = await context.Users.SingleAsync(u => u.Userid == id); int originalRole = user.Roleid;
        try
        {
            user.Roleid = (await context.Roles.SingleAsync(r => r.Rolename == "Admin")).Roleid; await context.SaveChangesAsync();
            if (await AuthenticatedStatus() != 200) throw new Exception("Admin access must return 200.");
        }
        finally { user.Roleid = originalRole; await context.SaveChangesAsync(); }
        Console.WriteLine("PASS: Admin authorization HTTP 401 (anonymous), 403 (Player), 200 (Admin); fixture role restored.");
    }

    public static async Task RunAsync(string connection, string root, bool cleanup,bool photon=false,string fixtureSuffix="")
    {
        var path = Path.Combine(root, "artifacts", photon?$"photon-account-fixture{fixtureSuffix}.json":"unity-account-fixture.json");
        var options = new DbContextOptionsBuilder<PickleVerseContext>().UseNpgsql(connection).Options;
        using var context = new PickleVerseContext(options);
        if (cleanup)
        {
            if (!File.Exists(path)) return;
            var fixture = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(path))!;
            var id = Guid.Parse(fixture["userId"]);
            if(!fixture["email"].StartsWith("unity-smoke-",StringComparison.Ordinal)||!fixture["email"].EndsWith("@example.invalid",StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to clean up a non-fixture account.");
            var sql=new Services.Game.GameSql(context);
            if(!await context.Users.AnyAsync(u=>u.Userid==id&&u.Email==fixture["email"]))throw new InvalidOperationException("Fixture identity does not match the database.");
            var keys=await sql.Query("SELECT objectkey FROM onlinereplays WHERE playerid=@id UNION SELECT objectkey FROM onlinereplaychunks WHERE playerid=@id",r=>r.GetString(0),Services.Game.GameSql.P("id",id));
            var storageConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                {["Storage:ServiceUrl"]="http://localhost:19000",["Storage:AccessKey"]="minioadmin",["Storage:SecretKey"]="minioadmin",["Storage:BucketName"]="pickleverse"}).Build();
            var storage=new StorageService(storageConfig,NullLogger<StorageService>.Instance);
            foreach(var key in keys)
            {
                if(!key.StartsWith($"replays/{id:N}/",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected fixture object key.");
                if(!await storage.DeleteFileAsync(key))throw new InvalidOperationException("Fixture object deletion failed.");
            }
            await using var cleanupTx=await context.Database.BeginTransactionAsync();
            var matches=await sql.Query("SELECT matchid FROM playermatchhistory WHERE playerid=@id UNION SELECT matchid FROM gamematches WHERE ownerid=@id UNION SELECT matchid FROM gamematchplayers WHERE playerid=@id",r=>r.GetGuid(0),Services.Game.GameSql.P("id",id));
            await sql.Run("DELETE FROM playermatchhistory WHERE playerid=@id",Services.Game.GameSql.P("id",id));
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE userid = {id} AND email = {fixture["email"]}");
            // Preserve a match if any other participant/history remains, including real players.
            foreach(var match in matches)
            {
                await sql.Run("DELETE FROM gamematches WHERE matchid=@match AND ownerid IS NULL AND NOT EXISTS(SELECT 1 FROM gamematchplayers WHERE matchid=@match) AND NOT EXISTS(SELECT 1 FROM playermatchhistory WHERE matchid=@match)",Services.Game.GameSql.P("match",match));
                await sql.Run("DELETE FROM matchhistory WHERE matchid=@match AND NOT EXISTS(SELECT 1 FROM playermatchhistory WHERE matchid=@match)",Services.Game.GameSql.P("match",match));
            }
            await cleanupTx.CommitAsync();
            File.Delete(path); Console.WriteLine("Unity smoke-test account removed."); return;
        }
        if (File.Exists(path)) throw new InvalidOperationException("Clean up the previous Unity fixture first.");
        using var uow = new UnitOfWork(context);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N") }).Build();
        var mail = new CapturedMail();
        var auth = new AuthService(uow, new ProfileService(uow, NullLogger<ProfileService>.Instance), config, mail, TimeProvider.System);
        var suffix = Guid.NewGuid().ToString("N");
        var email = "unity-smoke-" + suffix + "@example.invalid";
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var result = await auth.RegisterAsync(new RegisterRequest { Email = email, Password = password, ConfirmPassword = password, IsMale = false });
        if (!result.Success || !await auth.VerifyEmailAsync(new VerifyEmailRequest { Email = email, Token = mail.Codes[email] })) throw new Exception("Fixture registration failed.");
        var user = await context.Users.Include(u => u.Playerprofile).SingleAsync(u => u.Email == email);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        { ["email"] = email, ["password"] = password, ["userId"] = user.Userid.ToString(), ["playerName"] = user.Playerprofile!.Playername }));
        Console.WriteLine("Temporary Unity smoke-test account created; credentials stored only in ignored artifacts file.");
    }
}
