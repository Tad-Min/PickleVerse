using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Multiplayer;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game")]
public sealed class GameMatchesController(IGameMatchService matches) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("ranked/room-ready")]
    public Task<RankedSearchTicket> PublishRoom(RankedSearchRequest request) => matches.PublishRankedRoom(Player, request);
    [HttpPost("ranked/poll")]
    public Task<RankedSearchTicket> Poll(RankedSearchRequest request) => matches.PollRankedSearch(Player, request);
    [HttpPost("ranked/search")]
    public Task<RankedSearchTicket> Search(RankedSearchRequest request) => matches.BeginRankedSearch(Player, request);
    [HttpPost("ranked/cancel")]
    public Task<RankedSearchTicket> CancelSearch(RankedSearchRequest request) => matches.CancelRankedSearch(Player, request);
    [HttpPost("ranked/bot")]
    public Task<RoomMember> Bot(RankedBotRequest request) => matches.AssignRankedBot(Player, request);
    [HttpPost("match/bot/start")]
    public Task<GameSnapshot> StartBot(MatchStartRequest request) => matches.StartBot(Player, request);
    [HttpPost("match/bot/complete")]
    public Task<GameSnapshot> FinishBot(MatchFinishRequest request) => matches.FinishBot(Player, request);
    [HttpPost("match/online/offer")]
    public Task<OnlineMatchTicket> OfferOnline(OnlineMatchRequest request) => matches.OfferOnline(Player, request);
    [HttpPost("match/online/prepare")]
    public Task<OnlineMatchTicket> PrepareOnline(OnlineMatchRequest request) => matches.PrepareOnline(Player, request);
    [HttpPost("match/online/start")]
    public Task<OnlineMatchTicket> StartOnline(OnlineMatchRequest request) => matches.StartOnline(Player, request);
    [HttpPost("match/online/report")]
    public Task<GameSnapshot> ReportOnline(MatchFinishRequest request) => matches.ReportOnline(Player, request);
    [HttpPost("match/online/leave")]
    public Task<GameSnapshot> LeaveOnline(OnlineMatchCommand request) => matches.LeaveOnline(Player, request);
    [HttpPost("match/online/heartbeat")]
    public Task<GameSnapshot> HeartbeatOnline(OnlineMatchCommand request) => matches.HeartbeatOnline(Player, request);
}
