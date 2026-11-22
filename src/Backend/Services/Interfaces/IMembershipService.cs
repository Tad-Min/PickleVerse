using System.Text.Json;
using Entities.DTOs;

namespace Services.Interfaces;

public interface IMembershipService
{
    Task<MembershipOffer> Read(Guid player);
    Task<MembershipOffer> Checkout(Guid player, CancellationToken cancellation);
    Task<MembershipOffer> Refresh(Guid player, CancellationToken cancellation);
    Task ReceiveWebhook(JsonElement body, CancellationToken cancellation);
    Task Require(params Guid[] players);
}
