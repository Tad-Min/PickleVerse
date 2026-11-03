namespace Services.Interfaces;

public interface IEmailSender
{
    void EnsureConfigured();
    Task SendVerificationCodeAsync(string email, string code);
    Task SendPasswordResetCodeAsync(string email, string code) => SendVerificationCodeAsync(email, code);
}
