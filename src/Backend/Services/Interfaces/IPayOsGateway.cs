using System.Text.Json;
using Entities.DTOs;

namespace Services.Interfaces;

public interface IPayOsGateway
{
    Task<PayOsCheckout> Create(long orderCode, int amount, DateTime expiresAt, CancellationToken cancellation);
    Task<PayOsPayment> Read(long orderCode, CancellationToken cancellation);
    PayOsNotification? VerifyWebhook(JsonElement body);
}
