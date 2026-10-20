using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Entities.Models;
using Microsoft.EntityFrameworkCore;
using Repositories.Base;
using Repositories.Context;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class TrainingRepository : GenericRepository<Lesson>, ITrainingRepository
    {
        public TrainingRepository(PickleVerseContext context) : base(context)
        {
        }

        public async Task<List<Lesson>> GetAllLessonsWithDrillsAsync()
        {
            return await _context.Lessons
                .Include(l => l.Lessondrills)
                    .ThenInclude(ld => ld.Drill)
                .OrderBy(l => l.Lessonid)
                .ToListAsync();
        }

        public async Task<Lesson?> GetLessonByIdWithDrillsAsync(int lessonId)
        {
            return await _context.Lessons
                .Include(l => l.Lessondrills)
                    .ThenInclude(ld => ld.Drill)
                .FirstOrDefaultAsync(l => l.Lessonid == lessonId);
        }

        public async Task<Drill?> GetDrillByIdAsync(int drillId)
        {
            return await _context.Drills.FindAsync(drillId);
        }

        public async Task<Playerlessondrill?> GetPlayerLessonDrillAsync(Guid playerId, int lessonId, int drillId)
        {
            return await _context.Playerlessondrills
                .FirstOrDefaultAsync(pld => pld.Playerid == playerId && pld.Lessonid == lessonId && pld.Drillid == drillId);
        }

        public async Task<List<Playerlessondrill>> GetPlayerLessonDrillsAsync(Guid playerId, int lessonId)
        {
            return await _context.Playerlessondrills
                .Where(pld => pld.Playerid == playerId && pld.Lessonid == lessonId)
                .ToListAsync();
        }

        public async Task<List<Playerlessondrill>> GetAllPlayerDrillsAsync(Guid playerId)
        {
            return await _context.Playerlessondrills
                .Where(pld => pld.Playerid == playerId)
                .ToListAsync();
        }

        public async Task AddPlayerLessonDrillAsync(Playerlessondrill pld)
        {
            await _context.Playerlessondrills.AddAsync(pld);
        }

        public Task UpdatePlayerLessonDrillAsync(Playerlessondrill pld)
        {
            _context.Playerlessondrills.Update(pld);
            return Task.CompletedTask;
        }

        public async Task<int> CreateLessonAsync(Lesson lesson)
        {
            await _context.Lessons.AddAsync(lesson);
            await _context.SaveChangesAsync();
            return lesson.Lessonid;
        }

        public async Task<int> CreateDrillAsync(Drill drill)
        {
            await _context.Drills.AddAsync(drill);
            await _context.SaveChangesAsync();
            return drill.Drillid;
        }

        public async Task LinkDrillToLessonAsync(Lessondrill ld)
        {
            await _context.Lessondrills.AddAsync(ld);
            await _context.SaveChangesAsync();
        }
    }
}
