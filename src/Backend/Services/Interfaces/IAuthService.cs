using System;
using System.Threading.Tasks;
using Services.DTOs;

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
    }
}
