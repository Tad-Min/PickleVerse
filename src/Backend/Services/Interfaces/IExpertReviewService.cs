using Entities.DTOs;

namespace Services.Interfaces;

public interface IExpertReviewService
{
    Task<List<ReviewableTrainingSession>> Sessions(Guid? player, int page);
    Task<ExpertSessionEvidence> Evidence(Guid batch);
    Task<ExpertReviewReceipt> Review(Guid actor, Guid batch, ExpertReviewRequest request);
    Task<ExpertDatasetFile> Export(bool tierOnly);
    Task<List<CoachingDemoSession>> DemoSessions();
    Task<CoachingDemoEvidence> DemoEvidence(string sessionId);
}
