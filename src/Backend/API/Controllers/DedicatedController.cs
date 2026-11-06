using System.Security.Claims;
using Entities.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Progression;
using PickleVerse.Replays;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/Game/dedicated")]
public sealed class DedicatedController(IDedicatedServerService server) : ControllerBase
{
    [Authorize, HttpPost("ticket")]
    public Task<DedicatedTicket> Ticket(DedicatedCommand request) => server.Ticket(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request);
}

[ApiController]
[Route("api/Server")]
public sealed class DedicatedWorkerController(IDedicatedServerService server) : ControllerBase
{
    void Authenticate()
    {
        if (!server.Authenticate(Request.Headers["X-Game-Server-Key"].ToString()))
            throw new Entities.Exceptions.AppException("Không có quyền truy cập máy chủ.", System.Net.HttpStatusCode.Unauthorized);
    }
    [HttpPost("poll")] public async Task<DedicatedAllocation?> Poll([FromHeader(Name = "X-Game-Worker")] string workerId) { Authenticate(); return await server.Poll(workerId); }
    [HttpPost("ready")] public Task<DedicatedHeartbeat> Ready(DedicatedCommand request) { Authenticate(); return server.Renew(request, true); }
    [HttpPost("heartbeat")] public Task<DedicatedHeartbeat> Heartbeat(DedicatedCommand request) { Authenticate(); return server.Renew(request, false); }
    [HttpPost("operations/ack")] public Task Acknowledge(DedicatedCommand request) { Authenticate(); return server.Acknowledge(request); }
    [HttpPost("admission")] public Task AuthorizeAdmission(DedicatedAdmissionCommand request) { Authenticate(); return server.AuthorizeAdmission(request); }
    [HttpPost("complete")] public Task Complete(DedicatedCompletion request) { Authenticate(); return server.Complete(request); }
    [HttpPost("release")] public Task Release(DedicatedCommand request) { Authenticate(); return server.Release(request); }
    [HttpPost("{lease}/telemetry")] public Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch) { Authenticate(); return server.Telemetry(lease, batch); }
    [HttpPost("{lease}/replay")] public Task<ReplayMetadata> Replay(string lease, ReplayMetadata metadata) { Authenticate(); return server.SaveReplay(lease, metadata); }
    [HttpPost("{lease}/replay/chunks/{index:int}")] public Task<ReplayChunkInfo> Chunk(string lease, int index, ReplayChunkUpload chunk) { Authenticate(); return server.UploadReplay(lease, index, chunk); }
}
