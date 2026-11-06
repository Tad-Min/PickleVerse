using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game")]
public sealed class GameController(IOnlineGameService game, IGameCatalogService catalogs) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost("onboarding")] public Task<GameSnapshot> Onboarding(OnboardingRequest request) => game.Onboarding(Player, request);
    [HttpGet("catalog")]
    public Task<GameCatalog> Catalog() => catalogs.Load();
    [HttpGet("state")]
    public Task<GameSnapshot> State() => game.Read(Player);
    [HttpPost("claim")]
    public Task<GameSnapshot> Claim(GameCommand request) => game.Claim(Player, request);
    [HttpPost("purchase")]
    public Task<GameSnapshot> Purchase(GameCommand request) => game.Purchase(Player, request);
    [HttpPost("equip")]
    public Task<GameSnapshot> Equip(GameCommand request) => game.Equip(Player, request);
    [HttpPost("tutorial")]
    public Task<GameSnapshot> Tutorial(TutorialUpdateRequest request) => game.Tutorial(Player, request);
}
