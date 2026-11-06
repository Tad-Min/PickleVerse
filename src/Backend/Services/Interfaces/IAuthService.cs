using System;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<bool> VerifyEmailAsync(VerifyEmailRequest request);
        Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
        Task<bool> LogoutAsync(Guid userId, string refreshToken);
        Task<ResendOtpResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task ResetPasswordAsync(ResetPasswordRequest request);
        Task<bool> ValidateSessionAsync(Guid userId, int version, string scope = "game");
    }
}
