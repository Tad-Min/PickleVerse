using System;
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
        static AuthResponse Tokens(string token = "access-one") => new AuthResponse { Success = true, AccessToken = token, RefreshToken = "refresh-" + token, Profile = Profile() };
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

        [Test]
        public async Task RegistrationNeverCreatesASignedInSession()
        {
            string body = null;
            var fake = new FakeTransport { Handler = (m, p, j, t, c) => { body = j; return Task.FromResult(Ok(Tokens())); } };
            var account = new AccountService(new ApiClient(fake));
            await account.RegisterAsync(new RegisterRequest { Email = "test@example.invalid", Password = "example-only", PlayerName = "Player" });
            Assert.That(account.IsSignedIn, Is.False);
            Assert.That(JObject.Parse(body)["email"].Value<string>(), Is.EqualTo("test@example.invalid"));
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
