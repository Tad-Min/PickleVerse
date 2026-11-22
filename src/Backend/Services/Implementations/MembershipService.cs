using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class MembershipService(IUnitOfWork unit, IPayOsGateway payos, PayOsSettings settings, TimeProvider clock) : IMembershipService
{
    public const int PriceVnd = 49_000;
    public const int EliteTokenBonus = 3_000;
    public const string UnlockMessage = "Hãy mua gói thành viên để mở khóa tính năng này.";
    static readonly TimeSpan ReconcileInterval = TimeSpan.FromSeconds(10);
    DateTime Now => clock.GetUtcNow().UtcDateTime;
    static string CheckoutLock(Guid player) => $"membership-checkout:{player:N}";

    public async Task Require(params Guid[] players)
    {
        var ids = players.Distinct().ToArray();
        if (await unit.Game.CountMembers(ids) != ids.Length) throw new ForbiddenException(UnlockMessage);
    }

    public async Task<MembershipOffer> Read(Guid player)
    {
        var order = await unit.Game.CurrentMembership(player);
        return order?.PaidAt != null ? Offer(true, null) : Offer(false, order);
    }

    public async Task<MembershipOffer> Checkout(Guid player, CancellationToken cancellation)
    {
        if (!settings.Enabled) throw new AppException("Thanh toán PayOS hiện chưa khả dụng.", HttpStatusCode.ServiceUnavailable);
        await using var tx = await unit.Game.BeginTransactionAsync(cancellationToken: cancellation);
        // Only payment work uses this lock. Calls to PayOS do not hold the wallet/profile lock.
        await unit.Game.Lock(CheckoutLock(player));
        if (await unit.Game.HasMembership(player)) return Offer(true, null);
        var existing = await unit.Game.PendingMembership(player, tracking: true);
        if (existing != null)
        {
            if (existing.ExpiresAt > Now) return Offer(false, existing);
            var status = await payos.Read(existing.OrderCode, cancellation);
            await Apply(existing, status);
            await unit.SaveChangesAsync();
            await tx.CommitAsync(cancellation);
            // Make the observed provider state durable even if a new checkout fails.
            if (existing.Status == MembershipOrder.Paid) return Offer(true, null);
            if (existing.Status == MembershipOrder.Pending)
                throw new AppException("Đơn thanh toán trước đang được kiểm tra. Hãy thử lại sau.", HttpStatusCode.Conflict);
            return await Checkout(player, cancellation);
        }

        long orderCode;
        do
        {
            // Use the numeric range that remains exact in PayOS JSON and its checkout page.
            orderCode = (long)(BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(8)) & 0x000F_FFFF_FFFF_FFFFUL) + 1;
        } while (await unit.Game.MembershipOrderByCode(orderCode) != null);
        DateTime now = Now;
        DateTime expiresAt = settings.CheckoutExpiresAt(new DateTimeOffset(now));
        var checkout = await payos.Create(orderCode, PriceVnd, expiresAt, cancellation);
        var order = new MembershipOrder
        {
            OrderCode = orderCode, PlayerId = player, PaymentLinkId = checkout.PaymentLinkId, CheckoutUrl = checkout.CheckoutUrl,
            AmountVnd = PriceVnd, EliteTokenBonus = EliteTokenBonus, CreatedAt = now, ExpiresAt = expiresAt
        };
        unit.Game.AddMembershipOrder(order);
        await unit.SaveChangesAsync();
        await tx.CommitAsync(cancellation);
        return Offer(false, order);
    }

    public async Task<MembershipOffer> Refresh(Guid player, CancellationToken cancellation)
    {
        if (!settings.Enabled) return await Read(player);
        var order = await unit.Game.PendingMembership(player);
        if (order != null && await unit.Game.ReserveMembershipCheck(order.OrderCode, Now, ReconcileInterval))
            await Reconcile(order, cancellation);
        return await Read(player);
    }

    public async Task ReceiveWebhook(JsonElement body, CancellationToken cancellation)
    {
        var eventData = payos.VerifyWebhook(body);
        if (eventData == null) return;
        var order = await unit.Game.MembershipOrderByCode(eventData.OrderCode);
        // PayOS also sends a signed sample when the merchant registers the webhook.
        if (order == null || order.PaymentLinkId != eventData.PaymentLinkId || order.Status == MembershipOrder.Paid) return;
        await Reconcile(order, cancellation);
    }

    async Task Reconcile(MembershipOrder order, CancellationToken cancellation)
    {
        // A redirect or webhook alone is not a receipt. Check the merchant order.
        var status = await payos.Read(order.OrderCode, cancellation);
        await using var tx = await unit.Game.BeginTransactionAsync(cancellationToken: cancellation);
        await unit.Game.Lock(CheckoutLock(order.PlayerId));
        var tracked = await unit.Game.MembershipOrderByCode(order.OrderCode, tracking: true);
        if (tracked == null) return;
        await Apply(tracked, status);
        await unit.SaveChangesAsync();
        await tx.CommitAsync(cancellation);
    }

    async Task Apply(MembershipOrder order, PayOsPayment payment)
    {
        if (order.Status == MembershipOrder.Paid) return;
        if (payment.OrderCode != order.OrderCode || payment.PaymentLinkId != order.PaymentLinkId || payment.Amount != order.AmountVnd)
            throw new AppException("PayOS trả về đơn hoặc số tiền không khớp.", HttpStatusCode.BadGateway);

        if (payment.Status == MembershipOrder.Paid)
        {
            if (payment.AmountPaid < order.AmountVnd || payment.AmountRemaining != 0)
                throw new AppException("PayOS chưa xác nhận đủ số tiền thanh toán.", HttpStatusCode.BadGateway);
            var profile = await unit.Game.LockProfile(order.PlayerId) ?? throw new NotFoundException("Không tìm thấy hồ sơ người chơi.");
            if (!await unit.Game.HasMembership(order.PlayerId))
            {
                var balance = (await unit.Game.Inventory(order.PlayerId)).elite;
                await unit.Game.SaveBalance(order.PlayerId, "ELITE TOKEN", checked((int)(balance + order.EliteTokenBonus)), balance,
                    $"membership:{order.OrderCode}", "lifetime-membership");
                profile.Revision++;
            }
            order.Status = MembershipOrder.Paid;
            order.PaidAt = Now;
        }
        else if (payment.Status is MembershipOrder.Cancelled or MembershipOrder.Expired)
        {
            if (payment.AmountPaid > 0)
                throw new AppException("PayOS ghi nhận đã chuyển một phần tiền. Hãy liên hệ hỗ trợ trước khi tạo đơn mới.", HttpStatusCode.Conflict);
            order.Status = payment.Status;
        }
    }

    MembershipOffer Offer(bool active, MembershipOrder? order) => new(active, settings.Enabled, PriceVnd, EliteTokenBonus,
        order == null ? null : new(order.OrderCode, order.CheckoutUrl, order.ExpiresAt));
}
