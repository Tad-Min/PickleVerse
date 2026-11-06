using Entities.DTOs;
using Entities.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services.Implementations;
using Services.Interfaces;

static class PasswordRecoveryChecks
{
    public static async Task Run(DbContextOptions<PickleVerseContext> options, Action<bool, string> check)
    {
        var config = TestSettings.Configuration(); var clock = new TestClock(); var mail = new RecoveryMail();
        async Task<T> Call<T>(Func<AuthService, Task<T>> action)
        {
            using var db = new PickleVerseContext(options); using var unit = new UnitOfWork(db);
            return await action(new AuthService(unit, new ProfileService(unit, NullLogger<ProfileService>.Instance, new(config), clock), config, mail, clock, new(config)));
        }
        const string email = "recovery@pickleverse.invalid", original = "Original!23", replacement = "Replacement!23";
        await Call(a => a.RegisterAsync(new() { Email = email, Password = original, ConfirmPassword = original }));
        check(await Call(a => a.VerifyEmailAsync(new() { Email = email, Token = mail.Verification })), "verify fixture account");
        var signedIn = await Call(a => a.LoginAsync(new() { Email = email, Password = original }));
        var user = signedIn.Profile!.UserId;
        var unknown = await Call(a => a.ForgotPasswordAsync(new() { Email = "unknown@pickleverse.invalid" }));
        check(mail.ResetCount == 0, "unknown email never sends recovery mail");
        var sent = await Call(a => a.ForgotPasswordAsync(new() { Email = email }));
        check(sent.Message == unknown.Message && sent.Code == unknown.Code && sent.OtpLifetimeMinutes == unknown.OtpLifetimeMinutes, "uniform recovery response");
        check(mail.ResetCount == 1 && mail.Reset.Length == 6, "recovery sends six-digit code");
        await Call(a => a.ForgotPasswordAsync(new() { Email = email }));
        check(mail.ResetCount == 1, "per-account resend cooldown");
        async Task<bool> Reset(string code)
        {
            try { return await Call(async a => { await a.ResetPasswordAsync(new() { Email = email, Token = code, Password = replacement, ConfirmPassword = replacement }); return true; }); }
            catch (AppException) { return false; }
        }
        check(!await Call(a => a.VerifyEmailAsync(new() { Email = email, Token = mail.Reset })), "recovery code cannot verify email");
        string wrong = mail.Reset == "000000" ? "999999" : "000000";
        for (int i = 0; i < 5; i++) check(!await Reset(wrong), "invalid recovery attempt " + i);
        check(!await Reset(mail.Reset), "attempt limit also rejects the correct code");
        clock.Now = clock.Now.AddMinutes(1.1);
        await Call(a => a.ForgotPasswordAsync(new() { Email = email }));
        clock.Now = clock.Now.AddMinutes(11);
        check(!await Reset(mail.Reset), "expired recovery code rejected");
        await Call(a => a.ForgotPasswordAsync(new() { Email = email }));
        string current = mail.Reset;
        var race = await Task.WhenAll(Reset(current), Reset(current));
        check(race.Count(ok => ok) == 1, "concurrent resets consume the challenge once");
        check(!await Reset(current), "consumed recovery code cannot be replayed");
        check(!await Call(a => a.ValidateSessionAsync(user, 0)) && await Call(a => a.ValidateSessionAsync(user, 1)), "old access-token version revoked");
        check(!(await Call(a => a.RefreshTokenAsync(new() { RefreshToken = signedIn.RefreshToken! }))).Success, "all old refresh tokens revoked");
        check(!(await Call(a => a.LoginAsync(new() { Email = email, Password = original }))).Success, "old password rejected");
        check((await Call(a => a.LoginAsync(new() { Email = email, Password = replacement }))).Success, "new password signs in");
        using var final = new PickleVerseContext(options);
        check(await final.Verificationemails.Where(v => v.Userid == user).AllAsync(v => v.Isverified), "recovery preserves verified email state");
    }
    sealed class RecoveryMail : IEmailSender
    {
        public string Verification = "", Reset = ""; public int ResetCount;
        public void EnsureConfigured() { }
        public Task SendVerificationCodeAsync(string email, string code) { Verification = code; return Task.CompletedTask; }
        public Task SendPasswordResetCodeAsync(string email, string code) { Reset = code; ResetCount++; return Task.CompletedTask; }
    }
}
