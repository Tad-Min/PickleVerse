using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Base;
using Repositories.Context;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class FriendRepository : GenericRepository<Friendship>, IFriendRepository
    {
        public FriendRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<bool> IsBlockedAsync(Guid userA, Guid userB)
        {
            return await _context.Blockedusers.AnyAsync(b =>
                (b.Userid == userA && b.Blockeduserid == userB) ||
                (b.Userid == userB && b.Blockeduserid == userA));
        }

        public async Task<bool> AreFriendsAsync(Guid user1, Guid user2)
        {
            var u1 = user1.CompareTo(user2) < 0 ? user1 : user2;
            var u2 = user1.CompareTo(user2) < 0 ? user2 : user1;
            return await _context.Friendships.AnyAsync(f => f.Userid1 == u1 && f.Userid2 == u2);
        }

        public async Task<Friendship?> GetFriendshipAsync(Guid user1, Guid user2)
        {
            var u1 = user1.CompareTo(user2) < 0 ? user1 : user2;
            var u2 = user1.CompareTo(user2) < 0 ? user2 : user1;
            return await _context.Friendships.FirstOrDefaultAsync(f => f.Userid1 == u1 && f.Userid2 == u2);
        }

        public async Task<List<Friendship>> GetFriendshipsAsync(Guid userId)
        {
            return await _context.Friendships
                .Where(f => f.Userid1 == userId || f.Userid2 == userId)
                .ToListAsync();
        }

        public async Task AddFriendshipAsync(Friendship friendship)
        {
            await _context.Friendships.AddAsync(friendship);
        }

        public Task RemoveFriendshipAsync(Friendship friendship)
        {
            _context.Friendships.Remove(friendship);
            return Task.CompletedTask;
        }

        public async Task<Friendrequest?> GetFriendRequestAsync(int requestId, Guid receiverId)
        {
            return await _context.Friendrequests
                .FirstOrDefaultAsync(f => f.Requestid == requestId && f.Receiverid == receiverId);
        }

        public async Task<Friendrequest?> GetPendingFriendRequestAsync(Guid senderId, Guid receiverId)
        {
            return await _context.Friendrequests
                .FirstOrDefaultAsync(f => f.Senderid == senderId && f.Receiverid == receiverId && f.Status == FriendRequestStatus.Pending);
        }

        public async Task AddFriendRequestAsync(Friendrequest request)
        {
            await _context.Friendrequests.AddAsync(request);
        }

        public Task UpdateFriendRequestAsync(Friendrequest request)
        {
            _context.Friendrequests.Update(request);
            return Task.CompletedTask;
        }

        public async Task<bool> IsUserBlockedAsync(Guid userId, Guid blockedUserId)
        {
            return await _context.Blockedusers
                .AnyAsync(b => b.Userid == userId && b.Blockeduserid == blockedUserId);
        }

        public async Task AddBlockedUserAsync(Blockeduser blockedUser)
        {
            await _context.Blockedusers.AddAsync(blockedUser);
        }
    }
}
