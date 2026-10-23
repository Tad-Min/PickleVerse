using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Services.Exceptions;
using Services.Interfaces;
using Services.Configuration;

namespace Services.Implementations;

public sealed class SmtpEmailSender(ServerSettings settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(settings.Smtp.Username) ||
            string.IsNullOrWhiteSpace(settings.Smtp.Password) ||
            !MailboxAddress.TryParse(settings.Smtp.FromEmail, out _))
            throw new AppException("Máy chủ chưa được cấu hình gửi email. Vui lòng cấu hình SMTP Gmail tại backend.", HttpStatusCode.ServiceUnavailable);
    }

    public async Task SendVerificationCodeAsync(string email, string code)
    {
        EnsureConfigured();
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.Smtp.FromName, settings.Smtp.FromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Mã xác thực tài khoản PickleVerse";
        message.Body = new TextPart("plain")
        {
            Text = $"Mã xác thực PickleVerse của bạn: {code}\nMã có hiệu lực {settings.Otp.LifetimeMinutes} phút và chỉ dùng một lần.\nNếu bạn không đăng ký, hãy bỏ qua email này."
        };
        var stage = "connect/starttls";
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, SecureSocketOptions.StartTls, timeout.Token);
            stage = "authenticate";
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password, timeout.Token);
            stage = "send";
            await client.SendAsync(message, timeout.Token);
            logger.LogInformation("SMTP server accepted verification email.");
            stage = "disconnect";
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (Exception ex)
        {
            // Do not log the exception message: SMTP diagnostics may contain secrets or message content.
            logger.LogWarning("SMTP verification delivery failed at {Stage} ({ErrorType}, StatusCode={StatusCode})",
                stage, ex.GetType().Name, ex is SmtpCommandException smtp ? (int)smtp.StatusCode : (int?)null);
            throw new AppException($"Chưa gửi được email xác thực. Vui lòng thử gửi lại mã sau ít nhất {settings.Otp.ResendCooldownSeconds} giây.", HttpStatusCode.ServiceUnavailable);
        }
    }
}
