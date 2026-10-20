using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Services.DTOs;

namespace Services.Interfaces
{
    public interface IFriendService
    {
        Task<bool> SendFriendRequestAsync(Guid senderId, Guid receiverId);
        Task<bool> RespondFriendRequestAsync(Guid receiverId, RespondFriendRequestDto request);
        Task<List<FriendDto>> GetFriendsListAsync(Guid userId);
        Task<bool> BlockUserAsync(Guid userId, Guid blockedUserId);
        Task<bool> UnfriendAsync(Guid userId, Guid friendId);
    }
}
