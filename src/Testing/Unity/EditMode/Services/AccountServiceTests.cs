using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Services.Utilities;

namespace PickleVerse.Tests.Services
{
    public sealed class AccountServiceTests
    {
        static readonly Guid UserId = Guid.Parse("dca3d53b-7aba-443f-afbe-7c18277dccde");
        static ApiHttpResponse Ok(object value) => new ApiHttpResponse(200, JsonConvert.SerializeObject(value));
        static BackendPlayerProfile Profile(string name = "Online Player") => new BackendPlayerProfile { UserId = UserId, PlayerName = name, Level = 3, Elo = 250 };
        static AuthResponse Tokens(string token = "access-one") => new AuthResponse { Success = true, AccessToken = token, RefreshToken = "refresh-" + token, Profile = Profile(), OtpLifetimeMinutes=10, OtpResendCooldownSeconds=60 };

        [Test]
        public async Task RestrictedLoginUsesItsProfileWithoutCallingBlockedProfileEndpoint()
        {
            var tokens=Tokens();tokens.AccessScope=SessionAccessScope.Appeals;
            var paths=new System.Collections.Generic.List<string>();
            var fake=new FakeTransport { Handler=(m,p,j,t,c)=> { paths.Add(p);return Task.FromResult(Ok(tokens)); } };
            var account=new AccountService(new ApiClient(fake));await account.LoginAsync("restricted@example.invalid","example-only");
            Assert.That(account.IsSignedIn,Is.True);Assert.That(account.IsRestricted,Is.True);
            Assert.That(paths,Has.Count.EqualTo(1));Assert.That(paths[0],Does.EndWith("/login"));
            account.Clear();Assert.That(account.IsRestricted,Is.False);
        }

        [TestCase("0")]
        [TestCase("999")]
        [TestCase("\"unknown\"")]
        public void SessionScopeRejectsInvalidWireValues(string value)
        { Assert.Throws<JsonSerializationException>(()=>JsonConvert.DeserializeObject<SessionAccessScope>(value)); }

        [Test]
        public void SessionScopeUsesTheCanonicalServerCodes()
        {
            Assert.That(JsonConvert.SerializeObject(SessionAccessScope.Game),Is.EqualTo("\"game\""));
            Assert.That(JsonConvert.SerializeObject(SessionAccessScope.Appeals),Is.EqualTo("\"appeals\""));
            Assert.That(JsonConvert.DeserializeObject<UserAccountStatus>("\"SUSPENDED\""),Is.EqualTo(UserAccountStatus.Suspended));
        }

        [Test]
        public async Task OtpDisplayTimingComesFromApiResponses()
        {
            var registration=Tokens();registration.OtpLifetimeMinutes=2;registration.OtpResendCooldownSeconds=3;
            var fake=new FakeTransport { Handler=(m,p,j,t,c)=>Task.FromResult(p.EndsWith("/register")?Ok(registration):
                Ok(new ResendOtpResponse{Code="verification_requested",OtpLifetimeMinutes=4,OtpResendCooldownSeconds=7})) };
            var account=new AccountService(new ApiClient(fake));
            await account.RegisterAsync(new RegisterRequest{Email="otp@example.invalid",Password="example-only",ConfirmPassword="example-only"});
            Assert.That(account.OtpLifetimeMinutes,Is.EqualTo(2));Assert.That(account.OtpResendCooldownSeconds,Is.EqualTo(3));
            await account.ResendOtpAsync("otp@example.invalid");
            Assert.That(account.OtpLifetimeMinutes,Is.EqualTo(4));Assert.That(account.OtpResendCooldownSeconds,Is.EqualTo(7));
        }
        sealed class FakeTransport : IApiTransport
        {
            public Func<string, string, string, string, CancellationToken, Task<ApiHttpResponse>> Handler;
            public Task<ApiHttpResponse> SendAsync(string method, string path, string json, string token, CancellationToken cancellation) => Handler(method, path, json, token, cancellation);
        }

        [TestCase("http://localhost:8080", true, true)]
        [TestCase("http://localhost:8080", false, false)]
        [TestCase("http://example.com", true, false)]
        [TestCase("https://example.com", false, true)]
        [TestCase("https://user:password@example.com", false, false)]
        public void ApiUrlRequiresHttpsOutsideLocalDevelopment(string url, bool development, bool accepted)
        {
            if (accepted) Assert.That(ApiUrl.Build(url, "api/Profiles/me", development), Does.EndWith("/api/Profiles/me"));
            else Assert.Throws<ArgumentException>(() => ApiUrl.Build(url, "api/Profiles/me", development));
        }

        [TestCase("http://tad-min.io.vn:8080", true, true, true)]
        [TestCase("http://tad-min.io.vn:8080/", true, true, true)]
        [TestCase("http://tad-min.io.vn:8080", true, false, false)]
        [TestCase("http://tad-min.io.vn:8080", false, true, false)]
        [TestCase("http://tad-min.io.vn:8080", false, false, false)]
        public void RemoteHttpRequiresDevelopmentAndExplicitOptIn(string url, bool development, bool allowRemoteHttp, bool accepted)
        {
            if (accepted)
                Assert.That(ApiUrl.Build(url, "api/Auth/register", development, allowRemoteHttp),
                    Is.EqualTo("http://tad-min.io.vn:8080/api/Auth/register"));
            else Assert.Throws<ArgumentException>(() => ApiUrl.Build(url, "api/Auth/register", development, allowRemoteHttp));
        }

        [Test]
        public async Task RegistrationNeverCreatesASignedInSession()
        {
            string body = null;
            var fake = new FakeTransport { Handler = (m, p, j, t, c) => { body = j; return Task.FromResult(Ok(Tokens())); } };
            var account = new AccountService(new ApiClient(fake));
            await account.RegisterAsync(new RegisterRequest { Email = "test@example.invalid", Password = "example-only", ConfirmPassword = "example-only", IsMale = false });
            Assert.That(account.IsSignedIn, Is.False);
            var payload = JObject.Parse(body);
            Assert.That(payload["email"].Value<string>(), Is.EqualTo("test@example.invalid"));
            Assert.That(payload["password"].Value<string>(), Is.EqualTo("example-only"));
            Assert.That(payload["confirmPassword"].Value<string>(), Is.EqualTo("example-only"));
            Assert.That(payload["isMale"].Value<bool>(), Is.False);
            Assert.That(payload.Properties().Select(p => p.Name), Is.EquivalentTo(new[] { "email", "password", "confirmPassword", "isMale" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ActivityClosesBeforeLogoutOrAccountSwitch(bool switchAccount)
        {
            bool closed = false;
            var fake = new FakeTransport { Handler = (m,p,j,t,c) =>
            {
                if (p == "api/Auth/logout") Assert.That(closed, Is.True);
                return Task.FromResult(p == "api/Profiles/me" ? Ok(Profile()) : Ok(Tokens()));
            }};
            var account = new AccountService(new ApiClient(fake));
            await account.LoginAsync("first@example.invalid", "example-only");
            account.BeforeSessionEnd = () => { Assert.That(account.IsSignedIn, Is.True); closed = true; return Task.CompletedTask; };
            if (switchAccount) await account.LoginAsync("second@example.invalid", "example-only");
            else await account.LogoutAsync();
            Assert.That(closed, Is.True);
            Assert.That(account.IsSignedIn, Is.EqualTo(switchAccount));
        }

        [Test]
        public async Task ActivityFlushesWaitForThePreviousCheckpoint()
        {
            var checkpoint = new TaskCompletionSource<ApiHttpResponse>();
            var firstClose = new TaskCompletionSource<ApiHttpResponse>();
            var closing = new TaskCompletionSource<bool>();
            int closes = 0;
            var api = new ApiClient(new FakeTransport { Handler = (m, path, json, token, cancellation) =>
            {
                if (path == "api/Auth/login") return Task.FromResult(Ok(Tokens()));
                if (path == "api/Profiles/me") return Task.FromResult(Ok(Profile()));
                if (path == "api/Game/activity/open") return Task.FromResult(Ok(new { }));
                if (!JObject.Parse(json).Value<bool>("close")) return checkpoint.Task;
                if (++closes != 1) return Task.FromResult(Ok(new { }));
                closing.SetResult(true);
                return firstClose.Task;
            }});
            var account = new AccountService(api);
            await account.LoginAsync("first@example.invalid", "example-only");
            using var activity = new AppActivitySession(account, api, () => 30);
            var update = activity.FlushAsync();
            var first = activity.FlushAsync(true);
            var second = activity.FlushAsync(true);
            Assert.That(closes, Is.Zero);
            checkpoint.SetResult(Ok(new { totalSeconds = 30, elapsedSeconds = 30 }));
            await closing.Task;
            Assert.That(closes, Is.EqualTo(1));
            firstClose.SetResult(Ok(new { totalSeconds = 30, elapsedSeconds = 30 }));
            await Task.WhenAll(update, first, second);
            Assert.That(closes, Is.EqualTo(2));
            Assert.That(activity.Seconds, Is.EqualTo(30));
        }

        [Test]
        public async Task ActivityQueuedCloseDoesNotFollowAnAccountChange()
        {
            var oldCheckpoint = new TaskCompletionSource<ApiHttpResponse>();
            var newOwner = Guid.NewGuid();
            var payloads = new System.Collections.Generic.List<JObject>();
            var api = new ApiClient(new FakeTransport { Handler = (m, path, json, token, cancellation) =>
            {
                if (path == "api/Auth/login")
                {
                    bool second = JObject.Parse(json).Value<string>("email").StartsWith("second");
                    return Task.FromResult(Ok(Tokens(second ? "second-token" : "first-token")));
                }
                if (path == "api/Profiles/me")
                {
                    var profile = Profile();
                    if (token == "second-token") profile.UserId = newOwner;
                    return Task.FromResult(Ok(profile));
                }
                if (path == "api/Game/activity/open") return Task.FromResult(Ok(new { }));
                payloads.Add(JObject.Parse(json));
                return payloads.Count == 1 ? oldCheckpoint.Task : Task.FromResult(Ok(new { totalSeconds = 7, elapsedSeconds = 0 }));
            }});
            var account = new AccountService(api);
            await account.LoginAsync("first@example.invalid", "example-only");
            using var activity = new AppActivitySession(account, api, () => 30);
            var update = activity.FlushAsync();
            var close = activity.FlushAsync(true);
            account.Clear();
            await account.LoginAsync("second@example.invalid", "example-only");
            oldCheckpoint.SetResult(Ok(new { totalSeconds = 30, elapsedSeconds = 30 }));
            await Task.WhenAll(update, close);
            Assert.That(payloads, Has.Count.EqualTo(1));

            await activity.FlushAsync();
            Assert.That(payloads, Has.Count.EqualTo(2));
            Assert.That(payloads[1].Value<string>("sessionId"), Is.Not.EqualTo(payloads[0].Value<string>("sessionId")));
            Assert.That(payloads[1].Value<bool>("close"), Is.False);
            Assert.That(activity.Seconds, Is.EqualTo(7));
        }

        [Test]
        public async Task LoginLoadsAuthenticatedProfileInsteadOfTrustingLocalData()
        {
            string bearer = null;
            var fake = new FakeTransport { Handler = (m, p, j, t, c) =>
            { if (p.EndsWith("/login")) return Task.FromResult(Ok(Tokens())); bearer = t; return Task.FromResult(Ok(Profile("Server Name"))); } };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("a@example.invalid", "example-only");
            Assert.That(account.Profile.PlayerName, Is.EqualTo("Server Name")); Assert.That(bearer, Is.EqualTo("access-one"));
        }

        [TestCase("email_already_verified")]
        [TestCase("verification_requested")]
        public async Task ResendPreservesServerStatusWithoutSigningIn(string status)
        {
            var fake = new FakeTransport { Handler = (method, path, json, token, cancellation) =>
            {
                Assert.That(method, Is.EqualTo("POST"));
                Assert.That(path, Is.EqualTo("api/Auth/resend-otp"));
                Assert.That(JObject.Parse(json)["email"].Value<string>(), Is.EqualTo("test@example.invalid"));
                Assert.That(token, Is.Null);
                return Task.FromResult(Ok(new { code = status, message = "Server response", otpLifetimeMinutes=10, otpResendCooldownSeconds=60 }));
            } };
            var account = new AccountService(new ApiClient(fake));
            var result = await account.ResendOtpAsync("test@example.invalid");
            Assert.That(result.Code, Is.EqualTo(status));
            Assert.That(result.Message, Is.EqualTo("Server response"));
            Assert.That(account.IsSignedIn, Is.False);
        }

        [Test]
        public async Task ConcurrentUnauthorizedRequestsShareOneRefresh()
        {
            bool expired = false; int refreshes = 0;
            var gate = new TaskCompletionSource<ApiHttpResponse>();
            var fake = new FakeTransport { Handler = (m, p, j, t, c) =>
            {
                if (p.EndsWith("/login")) return Task.FromResult(Ok(Tokens()));
                if (p.EndsWith("/refresh-token")) { refreshes++; return gate.Task; }
                return Task.FromResult(expired && t == "access-one" ? new ApiHttpResponse(401, "{}") : Ok(Profile()));
            } };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("a@example.invalid", "example-only"); expired = true;
            var first = account.ReloadProfileAsync(); var second = account.ReloadProfileAsync();
            Assert.That(refreshes, Is.EqualTo(1)); gate.SetResult(Ok(Tokens("access-two")));
            await Task.WhenAll(first, second); Assert.That(account.IsSignedIn, Is.True);
        }

        [Test]
        public async Task ClearingSessionWhileLoginIsPendingPreventsResurrection()
        {
            var gate = new TaskCompletionSource<ApiHttpResponse>();
            var fake = new FakeTransport { Handler = (m, p, j, t, c) => p.EndsWith("/login") ? gate.Task : Task.FromResult(Ok(Profile())) };
            var account = new AccountService(new ApiClient(fake)); var login = account.LoginAsync("a@example.invalid", "example-only");
            account.Clear(); gate.SetResult(Ok(Tokens()));
            try { await login; Assert.Fail("The old login must be cancelled."); } catch (OperationCanceledException) { }
            Assert.That(account.IsSignedIn, Is.False); Assert.That(account.Profile, Is.Null);
        }

        [Test]
        public async Task FailedRefreshClearsCredentials()
        {
            bool expired = false;
            var fake = new FakeTransport { Handler = (m, p, j, t, c) => Task.FromResult(
                p.EndsWith("/login") ? Ok(Tokens()) : expired ? new ApiHttpResponse(401, "{}") : Ok(Profile())) };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("a@example.invalid", "example-only"); expired = true;
            try { await account.ReloadProfileAsync(); Assert.Fail(); } catch (ApiException) { }
            Assert.That(account.IsSignedIn, Is.False);
        }

        [Test]
        public async Task LogoutRevokesTheRotatedRefreshToken()
        {
            int logouts = 0; string revoked = null;
            var fake = new FakeTransport { Handler = (m, p, j, t, c) =>
            {
                if (p.EndsWith("/login")) return Task.FromResult(Ok(Tokens()));
                if (p.EndsWith("/refresh-token")) return Task.FromResult(Ok(Tokens("access-two")));
                if (p.EndsWith("/logout"))
                { logouts++; revoked = JObject.Parse(j)["refreshToken"].Value<string>(); return Task.FromResult(logouts == 1 ? new ApiHttpResponse(401, "{}") : Ok(new ApiMessage())); }
                return Task.FromResult(Ok(Profile()));
            } };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("a@example.invalid", "example-only"); await account.LogoutAsync();
            Assert.That(revoked, Is.EqualTo("refresh-access-two")); Assert.That(account.IsSignedIn, Is.False);
        }

        [Test]
        public async Task PendingLogoutDoesNotClearANewerLogin()
        {
            var logoutGate = new TaskCompletionSource<ApiHttpResponse>();
            var fake = new FakeTransport { Handler = (m, p, j, t, c) =>
                p.EndsWith("/logout") ? logoutGate.Task : Task.FromResult(p.EndsWith("/login") ? Ok(Tokens()) : Ok(Profile())) };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("first@example.invalid", "example-only");
            var oldLogout = account.LogoutAsync();
            await account.LoginAsync("second@example.invalid", "example-only");
            logoutGate.SetResult(Ok(new ApiMessage())); await oldLogout;
            Assert.That(account.IsSignedIn, Is.True);
        }

        [Test]
        public async Task UnauthorizedRetryFromOldSessionDoesNotClearANewerLogin()
        {
            bool expired = false; int logins = 0;
            var retryGate = new TaskCompletionSource<ApiHttpResponse>();
            var fake = new FakeTransport { Handler = (m, p, j, t, c) =>
            {
                if (p.EndsWith("/login")) return Task.FromResult(Ok(Tokens(++logins == 1 ? "access-one" : "access-three")));
                if (p.EndsWith("/refresh-token")) return Task.FromResult(Ok(Tokens("access-two")));
                if (t == "access-two") return retryGate.Task;
                return Task.FromResult(expired && t == "access-one" ? new ApiHttpResponse(401, "{}") : Ok(Profile()));
            } };
            var account = new AccountService(new ApiClient(fake)); await account.LoginAsync("first@example.invalid", "example-only");
            expired = true; var staleRequest = account.ReloadProfileAsync();
            await account.LoginAsync("second@example.invalid", "example-only"); retryGate.SetResult(new ApiHttpResponse(401, "{}"));
            try { await staleRequest; Assert.Fail(); } catch (OperationCanceledException) { }
            Assert.That(account.IsSignedIn, Is.True);
        }

        [Test]
        public void ProblemDetailsNeverDisplaysDevelopmentStackTraces()
        {
            var error = ApiClient.ParseError(new ApiHttpResponse(500, "{\"title\":\"Không thể hoàn tất yêu cầu\",\"detail\":\"private stack trace\"}"));
            Assert.That(error.Message, Is.EqualTo("Không thể hoàn tất yêu cầu")); Assert.That(error.Message, Does.Not.Contain("private"));
        }
    }
}
