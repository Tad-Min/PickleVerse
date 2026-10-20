using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Entities.Models;
using Repositories.Base;

namespace Repositories.Interfaces
{
    public interface ITrainingRepository : IGenericRepository<Lesson>
    {
        Task<List<Lesson>> GetAllLessonsWithDrillsAsync();
        Task<Lesson?> GetLessonByIdWithDrillsAsync(int lessonId);
        Task<Drill?> GetDrillByIdAsync(int drillId);
        Task<Playerlessondrill?> GetPlayerLessonDrillAsync(Guid playerId, int lessonId, int drillId);
        Task<List<Playerlessondrill>> GetPlayerLessonDrillsAsync(Guid playerId, int lessonId);
        Task<List<Playerlessondrill>> GetAllPlayerDrillsAsync(Guid playerId);
        Task AddPlayerLessonDrillAsync(Playerlessondrill pld);
        Task UpdatePlayerLessonDrillAsync(Playerlessondrill pld);
        Task<int> CreateLessonAsync(Lesson lesson);
        Task<int> CreateDrillAsync(Drill drill);
        Task LinkDrillToLessonAsync(Lessondrill ld);
    }
}
