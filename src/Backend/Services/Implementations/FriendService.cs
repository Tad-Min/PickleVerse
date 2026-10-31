using Repositories.UnitOfWork;
using Entities.DTOs;
using Services.Implementations;
using Services.Interfaces;

namespace Services.Implementations;

// Retain existing HTTP contracts while sharing the locked mutations used by the online menu.
public sealed class FriendService(IUnitOfWork unitOfWork, IOnlineSocialService online) : IFriendService
{
    public async Task<bool> SendFriendRequestAsync(Guid senderId, Guid receiverId)
    {
        await online.Act(senderId, new SocialActionRequest { PlayerId = receiverId, Action = "request" });
        return true;
    }
    public async Task<bool> RespondFriendRequestAsync(Guid receiverId, RespondFriendRequestDto request)
    {
        var sender = await unitOfWork.Game.FriendRequestSender(receiverId, request.RequestId);
        if (!sender.HasValue)
            return false;
        await online.Act(receiverId, new SocialActionRequest { PlayerId = sender.Value, RequestId = request.RequestId, Action = request.Accept ? "accept" : "decline" });
        return true;
    }
    public async Task<bool> UnfriendAsync(Guid userId, Guid friendId)
    {
        await online.Act(userId, new SocialActionRequest { PlayerId = friendId, Action = "remove" });
        return true;
    }
    public async Task<bool> BlockUserAsync(Guid userId, Guid blockedUserId)
    {
        await online.Act(userId, new SocialActionRequest { PlayerId = blockedUserId, Action = "block" });
        return true;
    }
    public async Task<List<FriendDto>> GetFriendsListAsync(Guid userId)
    {
        var friendships = await unitOfWork.Friends.GetFriendshipsAsync(userId);
        var result = new List<FriendDto>();
        foreach (var friendship in friendships)
        {
            var id = friendship.Userid1 == userId ? friendship.Userid2 : friendship.Userid1;
            var p = await unitOfWork.Profiles.GetByUserIdAsync(id);
            if (p != null)
                result.Add(new FriendDto { FriendId = p.Userid, PlayerName = p.Playername, Avatar = p.Avatar, Level = p.Level, Elo = p.Elo });
        }
        return result;
    }
}
