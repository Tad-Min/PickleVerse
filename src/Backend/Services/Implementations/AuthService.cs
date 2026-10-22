using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Exceptions;
using Services.Interfaces;
using Services.Security;

namespace Services.Implementations;

public sealed class AuthService(IUnitOfWork unitOfWork, IProfileService profiles,
    IConfiguration configuration, IEmailSender emailSender, TimeProvider clock) : IAuthService
{
    string SigningKey => configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
    DateTime UtcNow => clock.GetUtcNow().UtcDateTime;
    static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    static bool Active(User user) => !PickleVerse.Multiplayer.RankedBotAccounts.Contains(user.Userid.ToString("N")) && string.Equals(user.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase);
    static string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    static AuthResponse Failure(string message, string code) => new() { Message = message, Code = code };
    static string DefaultPlayerName() => "player" + RandomNumberGenerator.GetHexString(12, lowercase: true);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfirmPassword) ||
            !string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return Failure("Mật khẩu xác nhận chưa khớp.", "password_confirmation_mismatch");
        var email = NormalizeEmail(request.Email);
        if (await unitOfWork.Users.GetByEmailAsync(email) != null)
            return Failure("Email đã được đăng ký. Nếu chưa xác thực, hãy dùng chức năng gửi lại mã.", "email_exists");
        emailSender.EnsureConfigured();
        var role = await unitOfWork.Users.GetRoleByNameAsync("Player")
            ?? throw new AppException("Máy chủ chưa khởi tạo vai trò Player.", HttpStatusCode.ServiceUnavailable);
        var user = new User
        {
            Userid = Guid.NewGuid(), Email = email, Passwordhash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Roleid = role.Roleid, Status = "ACTIVE", Createdat = UtcNow
        };
        var profile = new Playerprofile
        {
            Userid = user.Userid, Playername = DefaultPlayerName(), Avatar = "initial",
            Elo = 100, Ismale = request.IsMale, Level = 1, Daystreak = 1
        };
        var code = EmailOtpPolicy.Generate();
        unitOfWork.Users.PrepareCreate(user);
        await unitOfWork.Profiles.AddProfileAsync(profile);
        await unitOfWork.Users.AddVerificationEmailAsync(NewVerification(user, code));
        for (int attempt = 0; ; attempt++)
        {
            await unitOfWork.BeginTransactionAsync();
            try
            {
                await unitOfWork.CommitTransactionAsync();
                break;
            }
            catch (DbUpdateException ex) when (attempt < 2 && ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "playerprofiles_playername_key" })
            {
                await unitOfWork.RollbackTransactionAsync();
                profile.Playername = DefaultPlayerName();
            }
            catch { await unitOfWork.RollbackTransactionAsync(); throw; }
        }
        // Failed delivery leaves an unverified account that resend-otp can recover.
        await emailSender.SendVerificationCodeAsync(email, code);
        return new AuthResponse { Success = true, Code = "verification_required", Message = "Đã đăng ký. Hãy nhập mã được gửi tới email để kích hoạt tài khoản." };
    }

    Verificationemail NewVerification(User user, string code) => new()
    {
        Userid = user.Userid, Email = user.Email, Token = EmailOtpPolicy.Hash(user.Userid, code, SigningKey),
        Createdat = UtcNow, Expiresat = UtcNow.AddMinutes(EmailOtpPolicy.LifetimeMinutes), Isverified = false
    };

    public async Task<bool> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user == null || !Active(user)) return false;
        await unitOfWork.BeginTransactionAsync();
        try
        {
            await unitOfWork.Users.LockUserAsync(user.Userid);
            var record = await unitOfWork.Users.GetLatestVerificationEmailAsync(user.Userid);
            if (record == null || !EmailOtpPolicy.CanVerify(record.Expiresat, record.Isverified, record.Failedattempts, UtcNow))
            { await unitOfWork.RollbackTransactionAsync(); return false; }
            bool matches = EmailOtpPolicy.Matches(user.Userid, request.Token, record.Token, SigningKey);
            if (matches) record.Isverified = true;
            else record.Failedattempts++;
            await unitOfWork.CommitTransactionAsync();
            return matches;
        }
        catch { await unitOfWork.RollbackTransactionAsync(); throw; }
    }

    public async Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request)
    {
        var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user == null || !Active(user)) return new ResendOtpResponse();
        await unitOfWork.BeginTransactionAsync();
        string code;
        try
        {
            await unitOfWork.Users.LockUserAsync(user.Userid);
            var latest = await unitOfWork.Users.GetLatestVerificationEmailAsync(user.Userid);
            if (latest?.Isverified == true)
            {
                await unitOfWork.RollbackTransactionAsync();
                return new ResendOtpResponse
                {
                    Code = "email_already_verified",
                    Message = "Email đã được xác thực. Hãy đăng nhập bằng email và mật khẩu đã đăng ký."
                };
            }
            if (latest != null && latest.Createdat.AddSeconds(EmailOtpPolicy.ResendCooldownSeconds) > UtcNow)
                throw new AppException("Vui lòng chờ ít nhất 60 giây trước khi gửi lại mã.", HttpStatusCode.TooManyRequests);
            emailSender.EnsureConfigured();
            code = EmailOtpPolicy.Generate();
            await unitOfWork.Users.AddVerificationEmailAsync(NewVerification(user, code));
            await unitOfWork.CommitTransactionAsync();
        }
        catch { await unitOfWork.RollbackTransactionAsync(); throw; }
        await emailSender.SendVerificationCodeAsync(user.Email, code);
        return new ResendOtpResponse();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email));
        if (user == null || PickleVerse.Multiplayer.RankedBotAccounts.Contains(user.Userid.ToString("N")) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Passwordhash))
            return Failure("Email hoặc mật khẩu không chính xác.", "invalid_credentials");
        var rejected = await RejectIneligibleUserAsync(user);
        if (rejected != null) return rejected;
        var refresh = NewRefreshToken(user.Userid);
        await unitOfWork.Users.AddRefreshTokenAsync(refresh.Entity);
        await unitOfWork.SaveChangesAsync();
        return await SignedInAsync(user, refresh.Raw);
    }

    async Task<AuthResponse?> RejectIneligibleUserAsync(User user)
    {
        if (!Active(user)) return Failure("Tài khoản đang bị khóa hoặc tạm ngưng.", "account_inactive");
        var verification = await unitOfWork.Users.GetLatestVerificationEmailAsync(user.Userid);
        return verification?.Isverified != true
            ? Failure("Email chưa được xác thực. Hãy nhập OTP hoặc yêu cầu gửi lại mã.", "email_not_verified") : null;
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var hash = HashRefreshToken(request.RefreshToken);
        var snapshot = await unitOfWork.Users.GetRefreshTokenAsync(hash);
        if (snapshot == null) return Failure("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", "invalid_session");
        await unitOfWork.BeginTransactionAsync();
        try
        {
            await unitOfWork.Users.LockUserAsync(snapshot.Userid);
            var token = await unitOfWork.Users.GetRefreshTokenAsync(hash);
            if (token == null || token.Isrevoked || token.Expiresat <= UtcNow)
            { await unitOfWork.RollbackTransactionAsync(); return Failure("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.", "invalid_session"); }
            var user = await unitOfWork.Users.GetByIdWithRoleAndProfileAsync(token.Userid);
            var rejected = user == null ? Failure("Không tìm thấy tài khoản.", "invalid_session") : await RejectIneligibleUserAsync(user);
            if (rejected != null) { await unitOfWork.RollbackTransactionAsync(); return rejected; }
            var next = NewRefreshToken(token.Userid);
            await unitOfWork.Users.RevokeRefreshTokenAsync(token);
            await unitOfWork.Users.AddRefreshTokenAsync(next.Entity);
            await unitOfWork.CommitTransactionAsync();
            return await SignedInAsync(user!, next.Raw);
        }
        catch { await unitOfWork.RollbackTransactionAsync(); throw; }
    }

    public async Task<bool> LogoutAsync(Guid userId, string refreshToken)
    {
        var token = await unitOfWork.Users.GetRefreshTokenAsync(HashRefreshToken(refreshToken));
        if (token != null && token.Userid == userId)
        {
            await unitOfWork.Users.RevokeRefreshTokenAsync(token);
            await unitOfWork.SaveChangesAsync();
        }
        return true;
    }

    (string Raw, Refreshtoken Entity) NewRefreshToken(Guid userId)
    {
        string raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, new Refreshtoken
        { Userid = userId, Tokenhash = HashRefreshToken(raw), Createdat = UtcNow, Expiresat = UtcNow.AddDays(14) });
    }

    async Task<AuthResponse> SignedInAsync(User user, string refreshToken) => new()
    {
        Success = true, Message = "Đăng nhập thành công.", AccessToken = GenerateJwtToken(user),
        RefreshToken = refreshToken, Profile = await profiles.GetMyProfileAsync(user.Userid)
    };

    string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Userid.ToString()), new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role?.Rolename ?? "Player")
        };
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"] ?? "PickleVerseAPI",
            configuration["Jwt:Audience"] ?? "PickleVerseClient", claims,
            expires: UtcNow.AddHours(2), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
