using Entities.Models;
using Microsoft.EntityFrameworkCore;
using PickleVerse.Multiplayer;

namespace Repositories.Game;

public sealed partial class GameRepository
{
    IQueryable<Playerprofile> VerifiedPlayers => db.Playerprofiles.AsNoTracking().Where(p => p.User.Status == "ACTIVE"
        && p.User.Verificationemails.OrderByDescending(v => v.Createdat).ThenByDescending(v => v.Verificationemailid)
            .Select(v => v.Isverified).FirstOrDefault());
    public async Task<List<(Guid id, int elo)>> LeaderboardScores() => (await VerifiedPlayers.OrderByDescending(p => p.Elo).ThenBy(p => p.Userid)
        .Select(p => new { p.Userid, p.Elo }).ToListAsync()).Select(p => (p.Userid, p.Elo)).ToList();
    public async Task<OnlineSocialPage> SocialPage(Guid self, string view, string query, int offset, Repositories.Caching.LeaderboardCache? cache = null)
    {
        await using var tx = await BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var players = await VerifiedPlayers.OrderByDescending(p => p.Elo).ThenBy(p => p.Userid)
            .Select(p => new OnlineSocialPlayer { Id = p.Userid, Name = p.Playername, Elo = p.Elo, Avatar = p.Avatar, Level = p.Level }).ToListAsync();
        for (int i = 0; i < players.Count; i++) players[i].Position = i + 1;
        var friends = await db.Friendships.AsNoTracking().Where(f => f.Userid1 == self || f.Userid2 == self)
            .Select(f => f.Userid1 == self ? f.Userid2 : f.Userid1).ToListAsync();
        var blocked = await db.Blockedusers.Where(b => b.Userid == self || b.Blockeduserid == self)
            .Select(b => b.Userid == self ? b.Blockeduserid : b.Userid).ToListAsync();
        var requests = await db.Friendrequests.AsNoTracking().Where(r => r.Status == "PENDING" && (r.Senderid == self || r.Receiverid == self))
            .OrderByDescending(r => r.Createdat).ThenByDescending(r => r.Requestid).ToListAsync();
        var visible = players.Where(p => !blocked.Contains(p.Id)).ToList();
        foreach (var p in visible)
        {
            var request = requests.FirstOrDefault(r => r.Senderid == p.Id || r.Receiverid == p.Id);
            p.RequestId = request?.Requestid ?? 0;
            p.Relationship = p.Id == self ? "self" : friends.Contains(p.Id) ? "friend" : request == null ? "none" : request.Senderid == self ? "outgoing" : "incoming";
        }
        int myPosition = players.FirstOrDefault(p => p.Id == self)?.Position ?? 0;
        int playerCount = players.Count;
        int friendCount = visible.Count(p => p.Relationship == "friend");
        if (cache != null)
        {
            var key = await cache.Snapshot(this);
            var ranks = await cache.Ranks(key, visible.Select(p => p.Id).ToArray());
            var positions = ranks.ToDictionary(r => r.id, r => r.position);
            visible = visible.Where(p => positions.ContainsKey(p.Id)).ToList();
            foreach (var p in visible) p.Position = positions[p.Id];
            myPosition = await cache.Rank(key, self);
            playerCount = await cache.Count(key);
        }
        var filtered = visible.Where(p => (view != "friends" || p.Relationship is "friend" or "incoming" or "outgoing")
            && (view == "leaderboard" || p.Id != self)
            && (p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || p.Id.ToString("N").Contains(query.Replace("-", ""), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Position).ToList();
        var selected = filtered.Skip(offset).Take(50).ToList();
        await tx.CommitAsync();
        return new OnlineSocialPage { Players = selected, Total = filtered.Count, MyPosition = myPosition, PlayerCount = playerCount,
            FriendCount = friendCount, Offset = offset, HasMore = offset + selected.Count < filtered.Count };
    }
    public async Task<int> LockPlayers(Guid a, Guid b) => (await db.Playerprofiles
        .FromSqlInterpolated($"SELECT * FROM playerprofiles WHERE userid IN ({a},{b}) ORDER BY userid FOR UPDATE")
        .AsNoTracking().ToListAsync()).Count;
    public Task<bool> Blocked(Guid a, Guid b) => db.Blockedusers.AnyAsync(x => x.Userid == a && x.Blockeduserid == b || x.Userid == b && x.Blockeduserid == a);
    public Task<bool> Friends(Guid a, Guid b) => db.Friendships.AnyAsync(f => f.Userid1 == a && f.Userid2 == b || f.Userid1 == b && f.Userid2 == a);
    public Task<bool> ActiveVerified(Guid player) => VerifiedPlayers.AnyAsync(p => p.Userid == player);
    public Task<int> PendingIncoming(Guid self, Guid target) => db.Friendrequests.Where(r => r.Senderid == target && r.Receiverid == self && r.Status == "PENDING")
        .Select(r => r.Requestid).FirstOrDefaultAsync();
    public Task<Guid?> FriendRequestSender(Guid receiver, int request) => db.Friendrequests.Where(r => r.Requestid == request && r.Receiverid == receiver)
        .Select(r => (Guid?)r.Senderid).SingleOrDefaultAsync();
    public Task SendFriendRequest(Guid sender, Guid receiver) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO friendrequests(senderid,receiverid,status) VALUES({sender},{receiver},'PENDING')
        ON CONFLICT(senderid,receiverid) DO UPDATE SET requestid=nextval(pg_get_serial_sequence('friendrequests','requestid')),
            status='PENDING',createdat=CURRENT_TIMESTAMP,respondedat=NULL WHERE friendrequests.status<>'PENDING'
        """);
    public Task<int> RespondFriendRequest(Guid self, Guid target, int request, string status) => db.Friendrequests
        .Where(r => r.Requestid == request && r.Senderid == target && r.Receiverid == self && r.Status == "PENDING")
        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status).SetProperty(r => r.Respondedat, DateTime.UtcNow));
    public Task<bool> FriendRequestHasStatus(Guid self, Guid target, int request, string status) => db.Friendrequests
        .AnyAsync(r => r.Requestid == request && r.Senderid == target && r.Receiverid == self && r.Status == status);
    public Task AddFriendship(Guid a, Guid b) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO friendships(userid1,userid2) VALUES(LEAST({a},{b}),GREATEST({a},{b})) ON CONFLICT DO NOTHING
        """);
    public Task RemoveFriendship(Guid a, Guid b) => db.Friendships.Where(f => f.Userid1 == a && f.Userid2 == b || f.Userid1 == b && f.Userid2 == a).ExecuteDeleteAsync();
    public Task DeclineRequests(Guid a, Guid b) => db.Friendrequests.Where(r => r.Senderid == a && r.Receiverid == b || r.Senderid == b && r.Receiverid == a)
        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "DECLINED").SetProperty(r => r.Respondedat, DateTime.UtcNow));
    public Task Block(Guid self, Guid target) => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO blockedusers(userid,blockeduserid) VALUES({self},{target}) ON CONFLICT DO NOTHING");
    public Task Unblock(Guid self, Guid target) => db.Blockedusers.Where(b => b.Userid == self && b.Blockeduserid == target).ExecuteDeleteAsync();
    public async Task<bool> CanInvite(Guid a, Guid b) => await Friends(a, b) && !await Blocked(a, b) && await db.Users.AnyAsync(u => u.Userid == b && u.Status == "ACTIVE");
    public Task<List<InvitableFriend>> InvitableFriends(Guid self, string query, int offset) => db.Playerprofiles.AsNoTracking()
        .Where(p => p.User.Status == "ACTIVE" && p.Userid != self
            && db.Friendships.Any(f => f.Userid1 == self && f.Userid2 == p.Userid || f.Userid1 == p.Userid && f.Userid2 == self)
            && !db.Blockedusers.Any(b => b.Userid == self && b.Blockeduserid == p.Userid || b.Userid == p.Userid && b.Blockeduserid == self)
            && (p.Playername.ToLower().Contains(query.ToLower()) || p.Userid.ToString().Replace("-", "").Contains(query.ToLower().Replace("-", ""))))
        .OrderBy(p => p.Playername.ToLower()).ThenBy(p => p.Userid).Skip(offset).Take(51)
        .Select(p => new InvitableFriend { Id = p.Userid.ToString("N"), Name = p.Playername, Elo = p.Elo }).ToListAsync();
}
