using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.UnitOfWork;
using Services.DTOs;
using Services.Interfaces;

namespace Services.Implementations
{
    public class FriendService : IFriendService
    {
        private readonly IUnitOfWork _unitOfWork;

        public FriendService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<bool> SendFriendRequestAsync(Guid senderId, Guid receiverId)
        {
            if (senderId == receiverId) throw new InvalidOperationException("Không thể tự gửi kết bạn cho chính mình!");

            bool isBlocked = await _unitOfWork.Friends.IsBlockedAsync(senderId, receiverId);
            if (isBlocked) throw new InvalidOperationException("Không thể gửi kết bạn với người chơi này!");

            bool alreadyFriends = await _unitOfWork.Friends.AreFriendsAsync(senderId, receiverId);
            if (alreadyFriends) throw new InvalidOperationException("Hai người đã là bạn bè!");

            var existingRequest = await _unitOfWork.Friends.GetPendingFriendRequestAsync(senderId, receiverId);
            if (existingRequest != null) return true;

            var friendRequest = new Friendrequest
            {
                Senderid = senderId,
                Receiverid = receiverId,
                Status = "PENDING",
                Createdat = DateTime.UtcNow
            };

            await _unitOfWork.Friends.AddFriendRequestAsync(friendRequest);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RespondFriendRequestAsync(Guid receiverId, RespondFriendRequestDto request)
        {
            var fr = await _unitOfWork.Friends.GetFriendRequestAsync(request.RequestId, receiverId);
            if (fr == null || fr.Status != "PENDING") return false;

            fr.Respondedat = DateTime.UtcNow;

            if (request.Accept)
            {
                fr.Status = "ACCEPTED";

                var u1 = fr.Senderid.CompareTo(fr.Receiverid) < 0 ? fr.Senderid : fr.Receiverid;
                var u2 = fr.Senderid.CompareTo(fr.Receiverid) < 0 ? fr.Receiverid : fr.Senderid;

                var friendship = new Friendship
                {
                    Userid1 = u1,
                    Userid2 = u2,
                    Createdat = DateTime.UtcNow
                };

                await _unitOfWork.Friends.AddFriendshipAsync(friendship);
            }
            else
            {
                fr.Status = "DECLINED";
            }

            await _unitOfWork.Friends.UpdateFriendRequestAsync(fr);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<List<FriendDto>> GetFriendsListAsync(Guid userId)
        {
            var friendships = await _unitOfWork.Friends.GetFriendshipsAsync(userId);
            var friendIds = friendships.Select(f => f.Userid1 == userId ? f.Userid2 : f.Userid1).ToList();

            var result = new List<FriendDto>();
            foreach (var friendId in friendIds)
            {
                var profile = await _unitOfWork.Profiles.GetByUserIdAsync(friendId);
                if (profile != null)
                {
                    result.Add(new FriendDto
                    {
                        FriendId = profile.Userid,
                        PlayerName = profile.Playername,
                        Avatar = profile.Avatar,
                        Level = profile.Level,
                        Elo = profile.Elo
                    });
                }
            }

            return result;
        }

        public async Task<bool> BlockUserAsync(Guid userId, Guid blockedUserId)
        {
            if (userId == blockedUserId) return false;

            var friendship = await _unitOfWork.Friends.GetFriendshipAsync(userId, blockedUserId);
            if (friendship != null)
            {
                await _unitOfWork.Friends.RemoveFriendshipAsync(friendship);
            }

            if (!await _unitOfWork.Friends.IsUserBlockedAsync(userId, blockedUserId))
            {
                var block = new Blockeduser
                {
                    Userid = userId,
                    Blockeduserid = blockedUserId,
                    Createdat = DateTime.UtcNow
                };
                await _unitOfWork.Friends.AddBlockedUserAsync(block);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UnfriendAsync(Guid userId, Guid friendId)
        {
            var friendship = await _unitOfWork.Friends.GetFriendshipAsync(userId, friendId);
            if (friendship == null) return false;

            await _unitOfWork.Friends.RemoveFriendshipAsync(friendship);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
    }
}
