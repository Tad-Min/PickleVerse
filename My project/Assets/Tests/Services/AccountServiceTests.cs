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
