using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Services.Exceptions;
using Services.Interfaces;
using Services.Security;

namespace Services.Implementations;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(configuration["Smtp:Username"]) ||
            string.IsNullOrWhiteSpace(configuration["Smtp:Password"]) ||
            !MailboxAddress.TryParse(configuration["Smtp:FromEmail"], out _))
            throw new AppException("Máy chủ chưa được cấu hình gửi email. Vui lòng cấu hình SMTP Gmail tại backend.", HttpStatusCode.ServiceUnavailable);
    }

    public async Task SendVerificationCodeAsync(string email, string code)
    {
        EnsureConfigured();
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(configuration["Smtp:FromName"] ?? "PickleVerse", configuration["Smtp:FromEmail"]!));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Mã xác thực tài khoản PickleVerse";
        message.Body = new TextPart("plain")
        {
            Text = $"Mã xác thực PickleVerse của bạn: {code}\nMã có hiệu lực {EmailOtpPolicy.LifetimeMinutes} phút và chỉ dùng một lần.\nNếu bạn không đăng ký, hãy bỏ qua email này."
        };
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var client = new SmtpClient { Timeout = 15000 };
            await client.ConnectAsync(configuration["Smtp:Host"] ?? "smtp.gmail.com",
                int.TryParse(configuration["Smtp:Port"], out var port) ? port : 587, SecureSocketOptions.StartTls, timeout.Token);
            await client.AuthenticateAsync(configuration["Smtp:Username"]!, configuration["Smtp:Password"]!, timeout.Token);
            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (Exception ex)
        {
            // SMTP diagnostics can contain credentials/message content; log only the type.
            logger.LogWarning("SMTP verification delivery failed ({ErrorType})", ex.GetType().Name);
            throw new AppException("Chưa gửi được email xác thực. Vui lòng thử gửi lại mã sau ít nhất 60 giây.", HttpStatusCode.ServiceUnavailable);
        }
    }
}
