using Repositories.UnitOfWork;
using Services.Interfaces;
using System.Net;
using Entities.Exceptions;

namespace Services.Implementations;

public sealed class OnlineSocialService : IOnlineSocialService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly LeaderboardCache leaderboard;

    public OnlineSocialService(IUnitOfWork unitOfWork, LeaderboardCache leaderboard)
    {
        _unitOfWork = unitOfWork;
        this.leaderboard = leaderboard;
    }

    public async Task<OnlineSocialPage> Read(Guid self, string view, string? query, int offset)
    {
        if (view is not ("friends" or "directory" or "leaderboard"))
            throw new AppException("Danh sách không hợp lệ.", HttpStatusCode.BadRequest);
        query = (query ?? "").Trim();
        if (query.Length > 64 || offset < 0 || offset > 10000000)
            throw new AppException("Bộ lọc không hợp lệ.", HttpStatusCode.BadRequest);
        try { return await _unitOfWork.Game.SocialPage(self, view, query, offset, view == "leaderboard" ? leaderboard : null); }
        catch (Exception error) when (view == "leaderboard" && PersistenceErrors.IsCacheUnavailable(error))
        { return await _unitOfWork.Game.SocialPage(self, view, query, offset); }
    }
    public async Task Act(Guid self, SocialActionRequest input)
    {
        Guid target = input.PlayerId;
        if (target == Guid.Empty || target == self)
            throw new AppException("Chọn người chơi khác.", HttpStatusCode.BadRequest);
        if (input.Action is not ("request" or "accept" or "decline" or "remove" or "block" or "unblock"))
            throw new AppException("Thao tác không hợp lệ.", HttpStatusCode.BadRequest);
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        var users = await _unitOfWork.Game.LockPlayers(self, target);
        if (users != 2)
            throw new AppException("Không tìm thấy người chơi.", HttpStatusCode.NotFound);
        bool blocked = await _unitOfWork.Game.Blocked(self, target);
        bool friend = await _unitOfWork.Game.Friends(self, target);
        if (input.Action is "request" or "accept" or "decline")
        {
            if (blocked)
                throw new AppException("Không thể kết bạn với người chơi này.", HttpStatusCode.Conflict);
            bool active = await _unitOfWork.Game.ActiveVerified(target);
            if (!active)
                throw new AppException("Người chơi chưa sẵn sàng nhận lời mời.", HttpStatusCode.BadRequest);
        }
        if (input.Action == "request" && !friend)
        {
            var pending = await _unitOfWork.Game.PendingIncoming(self, target);
            if (pending > 0)
                throw new AppException("Người chơi đã gửi lời mời cho bạn. Hãy chấp nhận hoặc từ chối.", HttpStatusCode.Conflict);
            await _unitOfWork.Game.SendFriendRequest(self, target);
        }
        else if (input.Action is "accept" or "decline")
        {
            string status = input.Action == "accept" ? "ACCEPTED" : "DECLINED";
            int updated = await _unitOfWork.Game.RespondFriendRequest(self, target, input.RequestId, status);
            if (updated == 0)
            {
                bool same = await _unitOfWork.Game.FriendRequestHasStatus(self, target, input.RequestId, status);
                if (!same)
                    throw new AppException("Lời mời không thuộc tài khoản này hoặc đã thay đổi.", HttpStatusCode.Conflict);
            }
            if (input.Action == "accept")
                await _unitOfWork.Game.AddFriendship(self, target);
        }
        else if (input.Action is "remove" or "block")
        {
            await _unitOfWork.Game.RemoveFriendship(self, target);
            await _unitOfWork.Game.DeclineRequests(self, target);
            if (input.Action == "block")
                await _unitOfWork.Game.Block(self, target);
        }
        else if (input.Action == "unblock")
            await _unitOfWork.Game.Unblock(self, target);
        await tx.CommitAsync();
    }
}
