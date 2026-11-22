using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse;
using PickleVerse.Multiplayer;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game")]
public sealed class GameMatchesController(IGameMatchService matches, IMembershipService membership) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    async Task<T> MemberAction<T>(Func<Task<T>> action, params Guid[] otherPlayers)
    {
        await membership.Require([Player, .. otherPlayers]);
        return await action();
    }

    static Guid[] RosterPlayers(OnlineMatchRequest request) => request.Roster?.members?
        .Take(4).Where(member => member != null && Guid.TryParse(member.id, out _) && !RankedBotAccounts.Contains(member.id))
        .Select(member => Guid.Parse(member.id)).ToArray() ?? [];

    [HttpPost("ranked/room-ready")]
    public Task<RankedSearchTicket> PublishRoom(RankedSearchRequest request) => MemberAction(() => matches.PublishRankedRoom(Player, request));
    [HttpPost("ranked/poll")]
    public Task<RankedSearchTicket> Poll(RankedSearchRequest request) => MemberAction(() => matches.PollRankedSearch(Player, request));
    [HttpPost("ranked/search")]
    public Task<RankedSearchTicket> Search(RankedSearchRequest request) =>
        MemberAction(() => matches.BeginRankedSearch(Player, request), request.Members?.Take(2).ToArray() ?? []);
    [HttpPost("ranked/cancel")]
    public Task<RankedSearchTicket> CancelSearch(RankedSearchRequest request) => matches.CancelRankedSearch(Player, request);
    [HttpPost("ranked/bot")]
    public Task<RoomMember> Bot(RankedBotRequest request) => MemberAction(() => matches.AssignRankedBot(Player, request));
    [HttpPost("match/bot/start")]
    public Task<GameSnapshot> StartBot(MatchStartRequest request) => matches.StartBot(Player, request);
    [HttpPost("match/bot/complete")]
    public Task<GameSnapshot> FinishBot(MatchFinishRequest request) => matches.FinishBot(Player, request);
    [HttpPost("match/online/offer")]
    public Task<OnlineMatchTicket> OfferOnline(OnlineMatchRequest request) => MemberAction(() => matches.OfferOnline(Player, request), RosterPlayers(request));
    [HttpPost("match/online/prepare")]
    public Task<OnlineMatchTicket> PrepareOnline(OnlineMatchRequest request) => MemberAction(() => matches.PrepareOnline(Player, request), RosterPlayers(request));
    [HttpPost("match/online/start")]
    public Task<OnlineMatchTicket> StartOnline(OnlineMatchRequest request) => MemberAction(() => matches.StartOnline(Player, request), RosterPlayers(request));
    [HttpPost("match/online/report")]
    public Task<GameSnapshot> ReportOnline(MatchFinishRequest request) => matches.ReportOnline(Player, request);
    [HttpPost("match/online/leave")]
    public Task<GameSnapshot> LeaveOnline(OnlineMatchCommand request) => matches.LeaveOnline(Player, request);
    [HttpPost("match/online/heartbeat")]
    public Task<GameSnapshot> HeartbeatOnline(OnlineMatchCommand request) => matches.HeartbeatOnline(Player, request);
}
