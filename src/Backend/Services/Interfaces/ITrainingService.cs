using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.DTOs;

namespace Services.Interfaces
{
    public interface ITrainingService
    {
        Task<List<LessonDto>> GetLessonsAsync(Guid playerId);
        Task<SubmitDrillResponse> SubmitDrillResultAsync(Guid playerId, SubmitDrillRequest request);
    }
}
