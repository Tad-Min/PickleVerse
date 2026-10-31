using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface IAiCoachingService
    {
        Task<List<MasteryDto>> GetMasteryStatusAsync(Guid playerId);
        Task<RecommendationResponse> GetRecommendedDrillsAsync(Guid playerId);
        Task<bool> LogTechniqueErrorAsync(Guid playerId, LogTechniqueErrorRequest request);
    }
}
