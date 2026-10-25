using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class FriendsController : ControllerBase
    {
        private readonly IFriendService _friendService;

        public FriendsController(IFriendService friendService)
        {
            _friendService = friendService;
        }

        private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }

        [HttpPost("request")]
        public async Task<IActionResult> SendFriendRequest([FromBody] SendFriendRequestDto request)
        {
            var senderId = GetCurrentUserId();
            var success = await _friendService.SendFriendRequestAsync(senderId, request.ReceiverId);
            if (!success)
                return BadRequest(new { message = "Gửi lời mời kết bạn thất bại (đã kết bạn hoặc người này bị chặn)." });
            return Ok(new { message = "Đã gửi lời mời kết bạn!" });
        }

        [HttpPost("respond")]
        public async Task<IActionResult> RespondFriendRequest([FromBody] RespondFriendRequestDto request)
        {
            var userId = GetCurrentUserId();
            var success = await _friendService.RespondFriendRequestAsync(userId, request);
            if (!success)
                return BadRequest(new { message = "Xử lý yêu cầu kết bạn thất bại." });
            return Ok(new { message = request.Accept ? "Đã chấp nhận lời mời kết bạn!" : "Đã từ chối lời mời kết bạn." });
        }

        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var userId = GetCurrentUserId();
            var list = await _friendService.GetFriendsListAsync(userId);
            return Ok(list);
        }

        [HttpDelete("{friendId}")]
        public async Task<IActionResult> RemoveFriend(Guid friendId)
        {
            var userId = GetCurrentUserId();
            var success = await _friendService.UnfriendAsync(userId, friendId);
            if (!success)
                return BadRequest(new { message = "Hủy kết bạn thất bại." });
            return Ok(new { message = "Đã hủy kết bạn thành công." });
        }

        [HttpPost("block/{targetUserId}")]
        public async Task<IActionResult> BlockUser(Guid targetUserId)
        {
            var userId = GetCurrentUserId();
            var success = await _friendService.BlockUserAsync(userId, targetUserId);
            if (!success)
                return BadRequest(new { message = "Chặn người dùng thất bại." });
            return Ok(new { message = "Đã chặn người dùng này thành công." });
        }
    }
}
