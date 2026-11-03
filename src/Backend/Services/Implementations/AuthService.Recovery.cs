using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Services.Security;

namespace Services.Implementations;

public sealed partial class AuthService
{
    string RecoveryHash(Guid user, Guid challenge, string token) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(SigningKey), Encoding.UTF8.GetBytes($"password-reset:{user:D}:{challenge:D}:{token}")));

    public Task<bool> ValidateSessionAsync(Guid userId, int version) => unitOfWork.Users.IsAuthVersionValidAsync(userId, version);

    public async Task<ResendOtpResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var started = Stopwatch.StartNew();
        emailSender.EnsureConfigured();
        try
        {
            var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email));
            if (user != null)
            {
                await unitOfWork.BeginTransactionAsync();
                try
                {
                    await unitOfWork.Users.LockUserAsync(user.Userid);
                    var verified = await unitOfWork.Users.GetLatestVerificationEmailAsync(user.Userid);
                    var prior = await unitOfWork.Users.GetPasswordResetAsync(user.Userid);
                    if (Active(user) && verified?.Isverified == true &&
                        (prior == null || prior.Createdat.AddSeconds(settings.Otp.ResendCooldownSeconds) <= UtcNow))
                    {
                        string code = EmailOtpPolicy.Generate();
                        var challenge = new Passwordresetchallenge { Userid = user.Userid, Challengeid = Guid.NewGuid(),
                            Createdat = UtcNow, Expiresat = UtcNow.AddMinutes(settings.Otp.LifetimeMinutes) };
                        challenge.Tokenhash = RecoveryHash(user.Userid, challenge.Challengeid, code);
                        await unitOfWork.Users.SavePasswordResetAsync(challenge);
                        await unitOfWork.CommitTransactionAsync();
                        // Production delivery is queued; SMTP duration never identifies a registered email.
                        if (recoveryMail != null) recoveryMail.TryEnqueue(user.Email, code);
                        else await emailSender.SendPasswordResetCodeAsync(user.Email, code);
                    }
                    else await unitOfWork.RollbackTransactionAsync();
                }
                catch { await unitOfWork.RollbackTransactionAsync(); throw; }
            }
            return new() { Code = "password_reset_requested", OtpLifetimeMinutes = settings.Otp.LifetimeMinutes,
                OtpResendCooldownSeconds = settings.Otp.ResendCooldownSeconds,
                Message = "Nếu email thuộc tài khoản đã xác thực, mã đặt lại mật khẩu sẽ được gửi. Hãy kiểm tra cả thư rác." };
        }
        finally
        {
            var remaining = TimeSpan.FromMilliseconds(300) - started.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.Password.Length is < 6 or > 128 || request.Password != request.ConfirmPassword)
            throw new AppException("Mật khẩu cần từ 6 đến 128 ký tự và xác nhận phải khớp.", HttpStatusCode.BadRequest);
        var user = await unitOfWork.Users.GetByEmailAsync(NormalizeEmail(request.Email));
        static AppException Invalid() => new("Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.", HttpStatusCode.BadRequest);
        if (user == null) throw Invalid();
        await unitOfWork.BeginTransactionAsync();
        try
        {
            await unitOfWork.Users.LockUserAsync(user.Userid);
            var challenge = await unitOfWork.Users.GetPasswordResetAsync(user.Userid);
            if (!Active(user) || challenge == null || challenge.Consumed || challenge.Expiresat <= UtcNow ||
                challenge.Failedattempts >= settings.Otp.MaxAttempts)
            { await unitOfWork.RollbackTransactionAsync(); throw Invalid(); }
            bool matches = request.Token.Length == 6 && request.Token.All(char.IsAsciiDigit) &&
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(challenge.Tokenhash),
                    Encoding.ASCII.GetBytes(RecoveryHash(user.Userid, challenge.Challengeid, request.Token)));
            if (!matches)
            {
                challenge.Failedattempts++;
                await unitOfWork.CommitTransactionAsync();
                throw Invalid();
            }
            user.Passwordhash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            user.Authversion = checked(user.Authversion + 1);
            challenge.Consumed = true;
            await unitOfWork.Users.RevokeAllRefreshTokensAsync(user.Userid);
            await unitOfWork.CommitTransactionAsync();
        }
        catch { await unitOfWork.RollbackTransactionAsync(); throw; }
    }
}
