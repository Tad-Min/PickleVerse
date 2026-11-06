using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game")]
public sealed class GameTrainingController(IGameTrainingService training) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("training/sync")]
    public Task<TrainingSyncResult> SyncTraining(TrainingSyncRequest request) => training.SyncTraining(Player, request);
    [HttpPost("training/start")]
    public Task<GameSnapshot> Start(StartTrainingRequest request) => training.StartTraining(Player, request);
    [HttpPost("training/checkpoint")]
    public Task<GameSnapshot> Checkpoint(TrainingUpdateRequest request) => training.UpdateTraining(Player, request, false);
    [HttpPost("training/complete")]
    public Task<GameSnapshot> Complete(TrainingUpdateRequest request) => training.UpdateTraining(Player, request, true);
}
