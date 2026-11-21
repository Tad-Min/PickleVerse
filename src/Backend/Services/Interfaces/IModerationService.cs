using Entities.DTOs;
using Entities.Models;

namespace Services.Interfaces;

public interface IModerationService
{
    Task<List<UserSanction>> Sanctions(Guid? user);
    Task<List<SanctionAppeal>> Appeals(Guid? user);
    Task<UserSanction> Apply(Guid actor, SanctionRequest request);
    Task Revoke(Guid actor, Guid user, string reason);
    Task ReviewReport(Guid actor, ReviewReportRequest request);
    Task<SanctionAppeal> SubmitAppeal(Guid user, AppealRequest request);
    Task<SanctionAppeal> ReviewAppeal(Guid actor, Guid appeal, AppealReviewRequest request);
    Task<DedicatedOperationRecord> StopMatch(Guid actor, Guid match, string reason);
    Task<DedicatedOperationRecord?> StopOperation(Guid match);
    Task Expire();
}
