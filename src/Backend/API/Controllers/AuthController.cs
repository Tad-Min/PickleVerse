using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Entities.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            if (!result.Success)
                return Unauthorized(result);
            return Ok(result);
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            var success = await _authService.VerifyEmailAsync(request);
            if (!success)
                return BadRequest(new MessageResponse { Message = "Mã xác thực không hợp lệ hoặc đã hết hạn!" });
            return Ok(new MessageResponse { Message = "Xác thực email thành công! Tài khoản đã được kích hoạt." });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request);
            if (!result.Success)
                return Unauthorized(result);
            return Ok(result);
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            return Ok(await _authService.ResendOtpAsync(request));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            var userId = GetCurrentUserId();
            await _authService.LogoutAsync(userId, request.RefreshToken);
            return Ok(new MessageResponse { Message = "Đăng xuất thành công." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request) => Ok(await _authService.ForgotPasswordAsync(request));

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new MessageResponse { Message = "Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới." });
        }
    }
}
