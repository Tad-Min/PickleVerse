using Entities.Game;
using PickleVerse.Progression;

namespace Services.Interfaces;

public interface IGameTrainingService
{
    Task<List<LessonDto>> LegacyLessons(Guid id);
    Task<GameSnapshot> StartTraining(Guid id, StartTrainingRequest request);
    Task<GameSnapshot> UpdateTraining(Guid id, TrainingUpdateRequest request, bool complete);
    Task<TrainingSyncResult> SyncTraining(Guid id, TrainingSyncRequest request);
}
