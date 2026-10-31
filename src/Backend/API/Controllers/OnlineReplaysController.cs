using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Replays;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Authorize]
[Route("api/Game/replays")]
public sealed class OnlineReplaysController(IMatchReplayService replays) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public Task<List<ReplayMetadata>> List() => replays.List(Player, User.IsInRole("Admin"));
    [HttpGet("{match:guid}")]
    public Task<ReplayMetadata> Read(Guid match) => replays.Read(Player, match, User.IsInRole("Admin"));
    [HttpPut("{match:guid}")]
    public Task<ReplayMetadata> Save(Guid match, ReplayMetadata input) => replays.Save(Player, match, input);
    [HttpPut("{match:guid}/chunks/{index:int}")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public Task<ReplayChunkInfo> Upload(Guid match, int index, ReplayChunkUpload input) => replays.Upload(Player, match, index, input);
    [HttpGet("{match:guid}/chunks/{index:int}")]
    public Task<ReplayChunkUpload> Chunk(Guid match, int index) => replays.Chunk(Player, match, index, User.IsInRole("Admin"));
}
