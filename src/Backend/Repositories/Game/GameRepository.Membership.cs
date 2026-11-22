using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    public Task<bool> HasMembership(Guid player) =>
        db.MembershipOrders.AnyAsync(order => order.PlayerId == player && order.PaidAt != null);

    public Task<int> CountMembers(Guid[] players) => db.MembershipOrders
        .Where(order => players.Contains(order.PlayerId) && order.PaidAt != null)
        .Select(order => order.PlayerId).Distinct().CountAsync();

    public Task<MembershipOrder?> CurrentMembership(Guid player) => db.MembershipOrders.AsNoTracking()
        .Where(order => order.PlayerId == player && (order.PaidAt != null || order.Status == MembershipOrder.Pending))
        .OrderByDescending(order => order.PaidAt != null)
        .FirstOrDefaultAsync();

    public Task<MembershipOrder?> PendingMembership(Guid player, bool tracking = false) =>
        (tracking ? db.MembershipOrders : db.MembershipOrders.AsNoTracking())
            .SingleOrDefaultAsync(order => order.PlayerId == player && order.Status == MembershipOrder.Pending);

    public Task<MembershipOrder?> MembershipOrderByCode(long code, bool tracking = false) =>
        (tracking ? db.MembershipOrders : db.MembershipOrders.AsNoTracking()).SingleOrDefaultAsync(order => order.OrderCode == code);

    public void AddMembershipOrder(MembershipOrder order) => db.MembershipOrders.Add(order);

    public async Task<bool> ReserveMembershipCheck(long code, DateTime now, TimeSpan interval)
    {
        var cutoff = now - interval;
        return await db.MembershipOrders.Where(order => order.OrderCode == code && order.Status == MembershipOrder.Pending &&
            (order.LastCheckedAt == null || order.LastCheckedAt <= cutoff))
            .ExecuteUpdateAsync(update => update.SetProperty(order => order.LastCheckedAt, now)) == 1;
    }
}
