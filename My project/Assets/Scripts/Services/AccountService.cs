using System;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Services.Api;

namespace PickleVerse.Services
{
    /// <summary>Owns online credentials in memory. Never persists passwords or tokens.</summary>
    public sealed class AccountService
    {
        readonly ApiClient api;
        string accessToken, refreshToken;
        int generation;
        Task refreshTask;
        public BackendPlayerProfile Profile { get; private set; }
        public int OtpLifetimeMinutes { get; private set; }
        public int OtpResendCooldownSeconds { get; private set; }
        public bool IsSignedIn => !string.IsNullOrEmpty(accessToken) && Profile != null;
        public int SessionVersion => generation;
        public event Action Changed;
        public Func<Task> BeforeSessionEnd { get; set; }
        public AccountService(ApiClient api)
        {
            this.api = api;
        }

        public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellation = default)
        {
            var result = await api.SendAsync<AuthResponse>("POST", "api/Auth/register", request, cancellation: cancellation);
            if (!result.Success)
                throw new ApiException(400, result.Message, result.Code);
            ApplyOtpPolicy(result.OtpLifetimeMinutes, result.OtpResendCooldownSeconds);
        }
        public Task<ApiMessage> VerifyEmailAsync(string email, string code, CancellationToken cancellation = default) =>
            api.SendAsync<ApiMessage>("POST", "api/Auth/verify-email", new VerifyEmailRequest { Email = email, Token = code }, cancellation: cancellation);
        public async Task<ResendOtpResponse> ResendOtpAsync(string email, CancellationToken cancellation = default)
        {
            var result = await api.SendAsync<ResendOtpResponse>("POST", "api/Auth/resend-otp", new EmailRequest { Email = email }, cancellation: cancellation);
            if (result.Code != "email_already_verified")
                ApplyOtpPolicy(result.OtpLifetimeMinutes, result.OtpResendCooldownSeconds);
            return result;
        }
        void ApplyOtpPolicy(int minutes, int cooldown)
        {
            if (minutes <= 0 || cooldown <= 0)
                throw new ApiException(502, "Máy chủ chưa trả đủ cấu hình xác thực email.", "invalid_response");
            OtpLifetimeMinutes = minutes;
            OtpResendCooldownSeconds = cooldown;
        }

        public async Task LoginAsync(string email, string password, CancellationToken cancellation = default)
        {
            if (IsSignedIn && BeforeSessionEnd != null)
                await BeforeSessionEnd();
            Clear();
            int attempt = generation;
            var result = await api.SendAsync<AuthResponse>("POST", "api/Auth/login", new LoginRequest { Email = email, Password = password }, cancellation: cancellation);
            ValidateTokens(result);
            var profile = await api.SendAsync<BackendPlayerProfile>("GET", "api/Profiles/me", token: result.AccessToken, cancellation: cancellation);
            CheckGeneration(attempt);
            ValidateProfile(profile);
            accessToken = result.AccessToken;
            refreshToken = result.RefreshToken;
            Profile = profile;
            Changed?.Invoke();
        }
        public async Task<BackendPlayerProfile> ReloadProfileAsync(CancellationToken cancellation = default)
        {
            int version = generation;
            var profile = await AuthorizedAsync<BackendPlayerProfile>("GET", "api/Profiles/me", null, cancellation);
            CheckGeneration(version);
            ValidateProfile(profile);
            Profile = profile;
            Changed?.Invoke();
            return profile;
        }
        public async Task UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellation = default)
        {
            int version = generation;
            await AuthorizedAsync<ApiMessage>("PUT", "api/Profiles/me", request, cancellation);
            CheckGeneration(version);
            await ReloadProfileAsync(cancellation);
        }
        public async Task LogoutAsync(CancellationToken cancellation = default)
        {
            int version = generation;
            try
            {
                if (IsSignedIn && BeforeSessionEnd != null)
                    await BeforeSessionEnd();
                if (IsSignedIn)
                    await AuthorizedAsync<ApiMessage>("POST", "api/Auth/logout", new RefreshTokenRequest { RefreshToken = refreshToken }, cancellation);
            }
            finally
            {
                if (version == generation)
                    Clear();
            }
        }
        public void Clear()
        {
            generation++;
            accessToken = refreshToken = null;
            Profile = null;
            refreshTask = null;
            Changed?.Invoke();
        }
        public async Task<T> SendAuthorizedAsync<T>(string method, string path, object body = null, CancellationToken cancellation = default)
        {
            int version = generation;
            var result = await AuthorizedAsync<T>(method, path, body, cancellation);
            CheckGeneration(version);
            return result;
        }
        async Task<T> AuthorizedAsync<T>(string method, string path, object body, CancellationToken cancellation)
        {
            if (!IsSignedIn)
                throw new ApiException(401, "Vui lòng đăng nhập để tiếp tục.");
            int version = generation;
            string sentToken = accessToken;
            try
            {
                return await api.SendAsync<T>(method, path, body, sentToken, cancellation);
            }
            catch (ApiException error) when (error.StatusCode == 401)
            {
                CheckGeneration(version);
                if (sentToken == accessToken)
                {
                    if (refreshTask == null || refreshTask.IsCompleted)
                        refreshTask = RefreshCoreAsync(version);
                    await refreshTask;
                }
                CheckGeneration(version);
                cancellation.ThrowIfCancellationRequested();
                // Refresh rotates the server refresh token. Logout must revoke the new one.
                if (path == "api/Auth/logout")
                    body = new RefreshTokenRequest { RefreshToken = refreshToken };
                try
                {
                    return await api.SendAsync<T>(method, path, body, accessToken, cancellation);
                }
                catch (ApiException retryError) when (retryError.StatusCode == 401)
                {
                    CheckGeneration(version);
                    Clear();
                    throw;
                }
            }
        }
        async Task RefreshCoreAsync(int version)
        {
            try
            {
                var result = await api.SendAsync<AuthResponse>("POST", "api/Auth/refresh-token", new RefreshTokenRequest { RefreshToken = refreshToken });
                CheckGeneration(version);
                ValidateTokens(result);
                accessToken = result.AccessToken;
                refreshToken = result.RefreshToken;
            }
            catch (ApiException error) when (error.StatusCode == 401 || error.StatusCode == 403)
            {
                if (version == generation)
                    Clear();
                throw;
            }
        }
        void CheckGeneration(int version)
        {
            if (version != generation)
                throw new OperationCanceledException("Phiên tài khoản đã thay đổi.");
        }
        static void ValidateTokens(AuthResponse result)
        {
            if (!result.Success || string.IsNullOrEmpty(result.AccessToken) || string.IsNullOrEmpty(result.RefreshToken))
                throw new ApiException(401, result.Message ?? "Phiên đăng nhập không hợp lệ.", result.Code);
        }
        static void ValidateProfile(BackendPlayerProfile profile)
        {
            if (profile.UserId == Guid.Empty || string.IsNullOrWhiteSpace(profile.PlayerName) || profile.Level < 1)
                throw new ApiException(200, "Hồ sơ máy chủ không hợp lệ.", "invalid_profile");
        }
    }
}
