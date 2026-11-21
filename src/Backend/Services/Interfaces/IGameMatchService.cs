using Entities.Game;
using PickleVerse.Multiplayer;

namespace Services.Interfaces;

public interface IGameMatchService
{
    Task<GameSnapshot> StartBot(Guid id, MatchStartRequest request);
    Task<GameSnapshot> FinishBot(Guid id, MatchFinishRequest request);
    Task<OnlineMatchTicket> OfferOnline(Guid id, OnlineMatchRequest request);
    Task<OnlineMatchTicket> PrepareOnline(Guid id, OnlineMatchRequest request);
    Task<OnlineMatchTicket> StartOnline(Guid id, OnlineMatchRequest request);
    Task<GameSnapshot> ReportOnline(Guid id, MatchFinishRequest request);
    Task<GameSnapshot> LeaveOnline(Guid id, OnlineMatchCommand request);
    Task<GameSnapshot> HeartbeatOnline(Guid id, OnlineMatchCommand request);
    Task<RankedSearchTicket> BeginRankedSearch(Guid id, RankedSearchRequest request);
    Task<RankedSearchTicket> PollRankedSearch(Guid id, RankedSearchRequest request);
    Task<RankedSearchTicket> PublishRankedRoom(Guid id, RankedSearchRequest request);
    Task<RankedSearchTicket> CancelRankedSearch(Guid id, RankedSearchRequest request);
    Task<RoomMember> AssignRankedBot(Guid id, RankedBotRequest request);
}
