using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface IFriendRepository : IGenericRepository<Friendship>
    {
        Task<bool> IsBlockedAsync(Guid userA, Guid userB);
        Task<bool> AreFriendsAsync(Guid user1, Guid user2);
        Task<Friendship?> GetFriendshipAsync(Guid user1, Guid user2);
        Task<List<Friendship>> GetFriendshipsAsync(Guid userId);
        Task AddFriendshipAsync(Friendship friendship);
        Task RemoveFriendshipAsync(Friendship friendship);
        Task<Friendrequest?> GetFriendRequestAsync(int requestId, Guid receiverId);
        Task<Friendrequest?> GetPendingFriendRequestAsync(Guid senderId, Guid receiverId);
        Task AddFriendRequestAsync(Friendrequest request);
        Task UpdateFriendRequestAsync(Friendrequest request);
        Task<bool> IsUserBlockedAsync(Guid userId, Guid blockedUserId);
        Task AddBlockedUserAsync(Blockeduser blockedUser);
    }
}
