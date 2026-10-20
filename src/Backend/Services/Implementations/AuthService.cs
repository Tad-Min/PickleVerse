using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Exceptions;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProfileService _profileService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUnitOfWork unitOfWork,
            IProfileService profileService,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _profileService = profileService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                // 1. Kiểm tra Email
                var existingUser = await _unitOfWork.Users.GetByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return new AuthResponse { Success = false, Message = "Email này đã được đăng ký!" };
                }

                // 2. Lấy Role mặc định là Player
                var playerRole = await _unitOfWork.Users.GetRoleByNameAsync("Player");
                var roleId = playerRole?.Roleid ?? 1;

                // 3. Băm mật khẩu
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                var newUser = new User
                {
                    Userid = Guid.NewGuid(),
                    Email = request.Email,
                    Passwordhash = passwordHash,
                    Roleid = roleId,
                    Status = "ACTIVE",
                    Createdat = DateTime.UtcNow
                };

                var newProfile = new Playerprofile
                {
                    Userid = newUser.Userid,
                    Playername = request.PlayerName,
                    Avatar = "initial",
                    Elo = 100,
                    Ismale = request.IsMale,
                    Timeplayedseconds = 0,
                    Level = 1,
                    Exp = 0,
                    Winstreak = 0,
                    Daystreak = 1,
                    Wincount = 0,
                    Losecount = 0
                };

                // 4. Sinh mã OTP kích hoạt 6 số
                var otpToken = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
                var verification = new Verificationemail
                {
                    Userid = newUser.Userid,
                    Email = request.Email,
                    Token = otpToken,
                    Createdat = DateTime.UtcNow,
                    Expiresat = DateTime.UtcNow.AddMinutes(10),
                    Isverified = true
                };

                await _unitOfWork.Users.CreateAsync(newUser);
                await _unitOfWork.Profiles.AddProfileAsync(newProfile);
                await _unitOfWork.Users.AddVerificationEmailAsync(verification);
                await _unitOfWork.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Đăng ký tài khoản thành công!",
                    Profile = await _profileService.GetMyProfileAsync(newUser.Userid)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng ký tài khoản cho email {Email}", request.Email);
                throw new AppException("Đăng ký tài khoản thất bại do lỗi hệ thống.", ex);
            }
        }

        public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request)
        {
            try
            {
                var record = await _unitOfWork.Users.GetValidVerificationTokenAsync(Guid.Empty, request.Token);
                if (record == null)
                    return false;

                record.Isverified = true;
                await _unitOfWork.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác thực email");
                throw new AppException("Xác thực email thất bại.", ex);
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);

                if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Passwordhash))
                {
                    return new AuthResponse { Success = false, Message = "Email hoặc mật khẩu không chính xác!" };
                }

                if (user.Status == "BANNED")
                {
                    return new AuthResponse { Success = false, Message = "Tài khoản của bạn đã bị cấm do vi phạm quy định!" };
                }

                // Sinh AccessToken & RefreshToken
                var accessToken = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshTokenString();

                var rtEntity = new Refreshtoken
                {
                    Userid = user.Userid,
                    Tokenhash = refreshToken,
                    Createdat = DateTime.UtcNow,
                    Expiresat = DateTime.UtcNow.AddDays(14),
                    Isrevoked = false
                };

                await _unitOfWork.Users.AddRefreshTokenAsync(rtEntity);
                await _unitOfWork.SaveChangesAsync();

                return new AuthResponse
                {
                    Success = true,
                    Message = "Đăng nhập thành công!",
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    Profile = await _profileService.GetMyProfileAsync(user.Userid)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng nhập cho email {Email}", request.Email);
                throw new AppException("Đăng nhập thất bại.", ex);
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            try
            {
                var rt = await _unitOfWork.Users.GetRefreshTokenAsync(request.RefreshToken);

                if (rt == null || rt.Isrevoked || rt.Expiresat < DateTime.UtcNow)
                {
                    return new AuthResponse { Success = false, Message = "Refresh Token không hợp lệ hoặc đã hết hạn!" };
                }

                var user = await _unitOfWork.Users.GetByIdWithRoleAndProfileAsync(rt.Userid);
                if (user == null)
                {
                    return new AuthResponse { Success = false, Message = "Không tìm thấy thông tin tài khoản!" };
                }

                var newAccessToken = GenerateJwtToken(user);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Gia hạn phiên thành công!",
                    AccessToken = newAccessToken,
                    RefreshToken = rt.Tokenhash,
                    Profile = await _profileService.GetMyProfileAsync(user.Userid)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi làm mới token");
                throw new AppException("Gia hạn phiên thất bại.", ex);
            }
        }

        public async Task<bool> LogoutAsync(Guid userId, string refreshToken)
        {
            try
            {
                var rt = await _unitOfWork.Users.GetRefreshTokenAsync(refreshToken);
                if (rt != null && rt.Userid == userId)
                {
                    await _unitOfWork.Users.RevokeRefreshTokenAsync(rt);
                    await _unitOfWork.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng xuất cho UserId {UserId}", userId);
                throw new AppException("Đăng xuất thất bại.", ex);
            }
        }

        private string GenerateJwtToken(User user)
        {
            var secretKey = _configuration["Jwt:Key"] ?? _configuration["Jwt:SecretKey"] ?? "PickleVerse_Super_Secret_Key_2026_DotNet10_Advanced_Capstone!";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Userid.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.Rolename ?? "Player")
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "PickleVerseAPI",
                audience: _configuration["Jwt:Audience"] ?? "PickleVerseClient",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshTokenString()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
