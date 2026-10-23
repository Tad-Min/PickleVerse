using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Game;

namespace API.Controllers;

[ApiController, Authorize, Route("api/Game")]
public sealed class GameController(OnlineGameService game,GameCatalogService catalogs) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [AllowAnonymous,HttpPost("activity/open")] public Task<AppActivityReceipt> OpenActivity(AppActivityRequest request) => game.OpenActivity(request);
    [HttpPost("activity/checkpoint")] public Task<AppActivityReceipt> Activity(AppActivityRequest request) => game.Activity(Player,request);
    [HttpPost("ranked/room-ready")] public Task<RankedSearchTicket> PublishRoom(RankedSearchRequest request) => game.PublishRankedRoom(Player,request);
    [HttpPost("ranked/poll")] public Task<RankedSearchTicket> Poll(RankedSearchRequest request) => game.PollRankedSearch(Player,request);
    [HttpPost("ranked/search")] public Task<RankedSearchTicket> Search(RankedSearchRequest request) => game.BeginRankedSearch(Player,request);
    [HttpPost("ranked/cancel")] public Task<RankedSearchTicket> CancelSearch(RankedSearchRequest request) => game.CancelRankedSearch(Player,request);
    [HttpPost("ranked/bot")] public Task<PickleVerse.Multiplayer.RoomMember> Bot(RankedBotRequest request) => game.AssignRankedBot(Player,request);
    [HttpGet("catalog")] public Task<GameCatalog> Catalog() => catalogs.Load();
    [HttpGet("state")] public Task<GameSnapshot> State() => game.Read(Player);
    [HttpPost("claim")] public Task<GameSnapshot> Claim(GameCommand request) => game.Claim(Player,request);
    [HttpPost("purchase")] public Task<GameSnapshot> Purchase(GameCommand request) => game.Purchase(Player,request);
    [HttpPost("equip")] public Task<GameSnapshot> Equip(GameCommand request) => game.Equip(Player,request);
    [HttpPost("tutorial")] public Task<GameSnapshot> Tutorial(TutorialUpdateRequest request) => game.Tutorial(Player,request);
    [HttpPost("training/start")] public Task<GameSnapshot> Start(StartTrainingRequest request) => game.StartTraining(Player,request);
    [HttpPost("training/checkpoint")] public Task<GameSnapshot> Checkpoint(TrainingUpdateRequest request) => game.UpdateTraining(Player,request,false);
    [HttpPost("training/complete")] public Task<GameSnapshot> Complete(TrainingUpdateRequest request) => game.UpdateTraining(Player,request,true);
    [HttpPost("match/bot/start")] public Task<GameSnapshot> StartBot(MatchStartRequest request) => game.StartBot(Player,request);
    [HttpPost("match/bot/complete")] public Task<GameSnapshot> FinishBot(MatchFinishRequest request) => game.FinishBot(Player,request);
    [HttpPost("match/online/offer")] public Task<OnlineMatchTicket> OfferOnline(OnlineMatchRequest request) => game.OfferOnline(Player,request);
    [HttpPost("match/online/prepare")] public Task<OnlineMatchTicket> PrepareOnline(OnlineMatchRequest request) => game.PrepareOnline(Player,request);
    [HttpPost("match/online/start")] public Task<OnlineMatchTicket> StartOnline(OnlineMatchRequest request) => game.StartOnline(Player,request);
    [HttpPost("match/online/report")] public Task<GameSnapshot> ReportOnline(MatchFinishRequest request) => game.ReportOnline(Player,request);
    [HttpPost("match/online/leave")] public Task<GameSnapshot> LeaveOnline(OnlineMatchCommand request) => game.LeaveOnline(Player,request);
    [HttpPost("match/online/heartbeat")] public Task<GameSnapshot> HeartbeatOnline(OnlineMatchCommand request) => game.HeartbeatOnline(Player,request);
}
