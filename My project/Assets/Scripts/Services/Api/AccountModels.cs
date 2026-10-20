using System;

namespace PickleVerse.Services.Api
{
    [Serializable] public sealed class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string PlayerName { get; set; }
        public bool IsMale { get; set; } = true;
    }
    [Serializable] public sealed class LoginRequest { public string Email { get; set; } public string Password { get; set; } }
    [Serializable] public sealed class VerifyEmailRequest { public string Email { get; set; } public string Token { get; set; } }
    [Serializable] public sealed class EmailRequest { public string Email { get; set; } }
    [Serializable] public sealed class RefreshTokenRequest { public string RefreshToken { get; set; } }
    [Serializable] public sealed class ApiMessage { public string Message { get; set; } }
    [Serializable] public sealed class AuthResponse
    {
        public bool Success { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public BackendPlayerProfile Profile { get; set; }
    }
    [Serializable] public sealed class BackendPlayerProfile
    {
        public Guid UserId { get; set; }
        public string PlayerName { get; set; }
        public string Avatar { get; set; }
        public int Elo { get; set; }
        public bool IsMale { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int WinStreak { get; set; }
        public int DayStreak { get; set; }
        public int WinCount { get; set; }
        public int LoseCount { get; set; }
        public double WinRate { get; set; }
        public long TimePlayedSeconds { get; set; }
        public string TimePlayedFormatted { get; set; }
    }
    [Serializable] public sealed class UpdateProfileRequest
    {
        public string PlayerName { get; set; }
        public string Avatar { get; set; }
        public bool? IsMale { get; set; }
    }
    [Serializable] public sealed class SyncTimeRequest { public long PlayedSeconds { get; set; } }
}
