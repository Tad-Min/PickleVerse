using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using PickleVerse.Replays;
namespace Services.Interfaces;

public interface IOnlineGameService
{
    Task<AppActivityReceipt> OpenActivity(AppActivityRequest request);
    Task<AppActivityReceipt> Activity(Guid id, AppActivityRequest request);
    Task<GameSnapshot> Read(Guid id);
    Task<GameSnapshot> Claim(Guid id, GameCommand request);
    Task<GameSnapshot> Tutorial(Guid id, TutorialUpdateRequest request);
    Task<GameSnapshot> Purchase(Guid id, GameCommand request);
    Task<GameSnapshot> Equip(Guid id, GameCommand request);
    Task<GameSnapshot> LegacyClaim(Guid id, int definition, bool achievement, string requestId);
    Task<GameSnapshot> LegacyCosmetic(Guid id, int cosmetic, bool purchase, bool equip, string requestId);
    Task<List<LessonDto>> LegacyLessons(Guid id);
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
    Task<GameSnapshot> StartTraining(Guid id, StartTrainingRequest request);
    Task<GameSnapshot> UpdateTraining(Guid id, TrainingUpdateRequest request, bool complete);
}
