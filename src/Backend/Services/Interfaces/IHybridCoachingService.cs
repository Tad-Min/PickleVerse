using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Multiplayer;
using PickleVerse.Replays;
namespace Services.Interfaces;

public interface IHybridCoachingService
{
    Task<TelemetryReceipt> Ingest(Guid player, TelemetryBatch batch);
    Task<CoachingSummary> Summary(Guid player);
    Task<Entities.DTOs.RecommendationResponse> LegacyRecommendation(Guid player);
    Task<string> BuildRequest(Guid batch);
}
