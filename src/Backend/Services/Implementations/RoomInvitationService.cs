using Repositories.UnitOfWork;
using Services.Interfaces;
using Presence = Entities.Game.LobbyPresence;
using StoredInvitation = Entities.Game.StoredRoomInvitation;
using System.Net;
using System.Text.RegularExpressions;
using PickleVerse.Multiplayer;
using Services.Configuration;
using Entities.Exceptions;

namespace Services.Implementations;

public sealed class RoomInvitationService : IRoomInvitationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LobbyRepository lobby;
    private readonly TimeProvider clock;

    public RoomInvitationService(IUnitOfWork unitOfWork, LobbyRepository lobby, TimeProvider clock)
    {
        _unitOfWork = unitOfWork;
        this.lobby = lobby;
        this.clock = clock;
    }

    public const int PresenceSeconds = 20, InvitationSeconds = 60;
    long Now => clock.GetUtcNow().ToUnixTimeSeconds();
    static AppException Bad(string message, HttpStatusCode status = HttpStatusCode.Conflict) => new(message, status);
    Task<T?> Read<T>(string key) where T : class => lobby.Read<T>(key);
    async Task<Presence?> Online(string player)
    {
        var p = await Read<Presence>("presence:" + player);
        return p != null && p.ExpiresAt > Now ? p : null;
    }
    Task<bool> Friends(Guid a, Guid b) => _unitOfWork.Game.CanInvite(a, b);
    Task<bool> InMatch(Guid player) => _unitOfWork.Game.InMatch(player);
    static int Capacity(Presence p) => p.Value.Custom ? 2 * p.Value.Mode : 2;
    async Task<Presence?> Room(string code)
    {
        var room = await Read<Presence>("room:" + code);
        if (room == null || room.ExpiresAt <= Now)
            return null;
        var owner = await Online(room.PlayerId);
        return owner != null && owner.Value.State == "lobby" && owner.Value.IsHost && owner.Value.RoomCode == code
            && owner.Value.SessionId == room.Value.SessionId ? room : null;
    }
    public async Task<LobbyPresenceReply> Publish(Guid self, LobbyPresenceRequest input)
    {
        if (input.RoomCode == null || input.Region == null || !Guid.TryParse(input.SessionId, out _) || input.State is not ("lobby" or "busy") || input.Mode is not (1 or 2)
            || input.RoomCode.Length > 64 || input.Region.Length > 30 || input.Members == null || input.Members.Length > 4
            || input.Members.Any(x => !Guid.TryParse(x, out _)))
            throw Bad("Trạng thái lobby không hợp lệ.", HttpStatusCode.BadRequest);
        input.RoomCode = input.RoomCode.Trim().ToUpperInvariant();
        input.Region = input.Region.Trim().ToLowerInvariant();
        if (input.RoomCode.Length > 0 && (!Regex.IsMatch(input.RoomCode, "^[A-Z0-9-]{6,64}$") || !Regex.IsMatch(input.Region, "^[a-z0-9-]{1,30}$")))
            throw Bad("Thông tin phòng không hợp lệ.", HttpStatusCode.BadRequest);
        input.Members = input.Members.Select(x => Guid.Parse(x).ToString("N")).Distinct().ToArray();
        if (input.RoomCode.Length > 0 && (!input.Members.Contains(self.ToString("N")) || input.Members.Length > (input.Custom ? 2 * input.Mode : 2)))
            throw Bad("Đội hình phòng không hợp lệ.", HttpStatusCode.BadRequest);
        if (await InMatch(self))
            input.State = "busy";
        var presence = new Presence { PlayerId = self.ToString("N"), Value = input, ExpiresAt = Now + PresenceSeconds };
        await lobby.StorePresence(presence, PresenceSeconds);
        if (input.IsHost && input.RoomCode.Length > 0)
        {
            bool registered = await lobby.RegisterRoom(presence, Now, PresenceSeconds);
            if (!registered)
                throw Bad("Mã phòng đang được một chủ phòng khác sử dụng.", HttpStatusCode.Forbidden);
        }
        return new() { Invitations = input.State == "lobby" ? await Inbox(self) : [] };
    }
    async Task<RoomInvitation[]> Inbox(Guid self)
    {
        var ids = await lobby.InvitationIds(self, Now);
        var result = new List<RoomInvitation>();
        foreach (var id in ids)
        {
            var stored = await Read<StoredInvitation>("invite:" + id);
            if (stored == null || stored.Value.State != "pending" || stored.Value.ExpiresAt <= Now)
                continue;
            if (await ValidRoom(stored) != null && await Friends(Guid.Parse(stored.Value.SenderId), self))
                result.Add(stored.Value);
        }
        return result.ToArray();
    }
    async Task<Presence?> ValidRoom(StoredInvitation invite)
    {
        var sender = await Online(invite.Value.SenderId);
        var room = await Room(invite.Value.RoomCode);
        return sender != null && sender.Value.State == "lobby" && sender.Value.SessionId == invite.SenderSession
            && sender.Value.RoomCode == invite.Value.RoomCode && room != null && room.PlayerId == invite.OwnerId
            && room.Value.SessionId == invite.OwnerSession && room.Value.Members.Contains(invite.Value.SenderId)
            && room.Value.Members.Length < Capacity(room) ? room : null;
    }
    public async Task<InvitableFriendsPage> List(Guid self, string? query, int offset)
    {
        query = (query ?? "").Trim();
        if (query.Length > 64 || offset < 0 || offset > 100000)
            throw Bad("Bộ lọc không hợp lệ.", HttpStatusCode.BadRequest);
        var friends = await _unitOfWork.Game.InvitableFriends(self, query, offset);
        var sender = await Online(self.ToString("N"));
        var room = sender == null ? null : await Room(sender.Value.RoomCode);
        foreach (var friend in friends.Take(50))
        {
            var presence = await Online(friend.Id);
            var pendingId = await lobby.PendingPair(self, friend.Id);
            var pending = pendingId == null ? null : await Read<StoredInvitation>("invite:" + pendingId);
            friend.Pending = pending?.Value.State == "pending" && pending.Value.ExpiresAt > Now;
            bool inRoom = room?.Value.Members.Contains(friend.Id) == true;
            friend.Status = presence == null ? "offline" : inRoom ? "in_room" : presence.Value.State == "lobby" ? "lobby" : "busy";
            friend.CanInvite = sender?.Value.State == "lobby" && room != null && room.Value.Members.Contains(self.ToString("N"))
                && room.Value.Members.Length < Capacity(room) && presence?.Value.State == "lobby" && !inRoom && !friend.Pending;
        }
        return new() { Players = friends.Take(50).ToArray(), HasMore = friends.Count > 50, Offset = offset };
    }
    public async Task<RoomInvitation> Send(Guid self, SendRoomInvitationRequest input)
    {
        if (!Guid.TryParse(input.PlayerId, out var target) || target == self)
            throw Bad("Chọn một người bạn khác.", HttpStatusCode.BadRequest);
        if (!await Friends(self, target))
            throw Bad("Chỉ có thể mời người trong danh sách bạn bè.", HttpStatusCode.Forbidden);
        var sender = await Online(self.ToString("N"));
        var receiver = await Online(target.ToString("N"));
        var room = sender == null ? null : await Room(sender.Value.RoomCode);
        if (sender?.Value.State != "lobby" || receiver?.Value.State != "lobby" || await InMatch(target))
            throw Bad("Người chơi hiện không ở lobby hoặc đang bận.");
        if (room == null || !room.Value.Members.Contains(self.ToString("N")))
            throw Bad("Phòng đã đóng hoặc chưa sẵn sàng.");
        if (room.Value.Members.Length >= Capacity(room) || room.Value.Members.Contains(target.ToString("N")))
            throw Bad("Phòng đã đầy hoặc người chơi đã ở trong phòng.");
        string name = await _unitOfWork.Game.PlayerName(self) ?? "Người chơi";
        var invitation = new RoomInvitation
        {
            Id = Guid.NewGuid().ToString("N"),
            SenderId = self.ToString("N"),
            SenderName = name,
            RoomCode = room.Value.RoomCode,
            Region = room.Value.Region,
            Mode = room.Value.Mode,
            Custom = room.Value.Custom,
            ExpiresAt = Now + InvitationSeconds
        };
        var stored = new StoredInvitation { Value = invitation, SenderSession = sender.Value.SessionId, OwnerId = room.PlayerId, OwnerSession = room.Value.SessionId, ReceiverId = target.ToString("N") };
        string id = await lobby.StoreInvitation(self, target, stored, Now, InvitationSeconds);
        if (id == "cooldown")
            throw Bad("Vui lòng chờ vài giây trước khi mời lại.", HttpStatusCode.TooManyRequests);
        if (id == "full")
            throw Bad("Người chơi đang có quá nhiều lời mời. Hãy thử lại sau.");
        return (await Read<StoredInvitation>("invite:" + id))!.Value;
    }
    public async Task<RoomInvitation> Respond(Guid self, string id, bool accept)
    {
        if (!Guid.TryParseExact(id, "N", out _))
            throw Bad("Lời mời không hợp lệ.", HttpStatusCode.BadRequest);
        var stored = await Read<StoredInvitation>("invite:" + id) ?? throw Bad("Lời mời đã hết hạn.", HttpStatusCode.Gone);
        if (stored.ReceiverId != self.ToString("N"))
            throw Bad("Lời mời không thuộc tài khoản này.", HttpStatusCode.Forbidden);
        if (stored.Value.ExpiresAt <= Now)
            throw Bad("Lời mời đã hết hạn.", HttpStatusCode.Gone);
        if (stored.Value.State == (accept ? "accepted" : "declined"))
            return stored.Value;
        if (stored.Value.State != "pending")
            throw Bad("Lời mời đã được xử lý.", HttpStatusCode.Gone);
        if (accept)
        {
            var presence = await Online(self.ToString("N"));
            if (presence?.Value.State != "lobby" || await InMatch(self))
                throw Bad("Chỉ có thể nhận lời mời khi đang ở lobby.");
            if (!await Friends(Guid.Parse(stored.Value.SenderId), self) || await ValidRoom(stored) == null)
                throw Bad("Phòng đã đóng, đã đầy hoặc lời mời không còn hiệu lực.");
        }
        string state = accept ? "accepted" : "declined";
        var changed = await lobby.Respond(self, id, state, Now);
        if (changed == null)
            throw Bad("Lời mời đã được xử lý hoặc đã hết hạn.", HttpStatusCode.Gone);
        return changed.Value;
    }
}
