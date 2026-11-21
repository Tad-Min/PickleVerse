using Entities.DTOs;

namespace Services.Interfaces;

public interface IProgressResetService
{
    Task<ProgressResetPreview> Preview(Guid player, ProgressResetSelection selection);
    Task<ProgressResetReceipt> Execute(Guid actor, Guid player, ExecuteProgressReset request);
}
