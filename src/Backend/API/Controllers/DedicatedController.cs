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
    public Task<DedicatedTicket> Ticket(DedicatedCommand request) => server.Ticket(
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), request,
        int.Parse(User.FindFirstValue("auth_version")!, System.Globalization.CultureInfo.InvariantCulture));
}

[ApiController]
[Route("api/Server")]
public sealed class DedicatedWorkerController(IDedicatedServerService server) : ControllerBase
{
    string Authenticate()
    {
        if (!server.Authenticate(Request.Headers["X-Game-Server-Key"].ToString()))
            throw new Entities.Exceptions.AppException("Không có quyền truy cập máy chủ.", System.Net.HttpStatusCode.Unauthorized);
        if (!Guid.TryParseExact(Request.Headers["X-Game-Worker"].ToString(), "N", out var worker))
            throw new Entities.Exceptions.AppException("Mã worker không hợp lệ.", System.Net.HttpStatusCode.BadRequest);
        return worker.ToString("N");
    }
    [HttpPost("poll")] public Task<DedicatedAllocation?> Poll([FromHeader(Name = "X-Game-Protocol")] int protocol) => server.Poll(Authenticate(), protocol);
    [HttpPost("ready")] public Task<DedicatedHeartbeat> Ready(DedicatedCommand request) => server.Renew(request, true, Authenticate());
    [HttpPost("heartbeat")] public Task<DedicatedHeartbeat> Heartbeat(DedicatedCommand request) => server.Renew(request, false, Authenticate());
    [HttpPost("operations/ack")] public Task Acknowledge(DedicatedCommand request) => server.Acknowledge(request, Authenticate());
    [HttpPost("admission")] public Task AuthorizeAdmission(DedicatedAdmissionCommand request) => server.AuthorizeAdmission(request, Authenticate());
    [HttpPost("complete")] public Task Complete(DedicatedCompletion request) => server.Complete(request, Authenticate());
    [HttpPost("release")] public Task Release(DedicatedCommand request) => server.Release(request, Authenticate());
    [HttpPost("{lease}/telemetry")] public Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch) => server.Telemetry(lease, batch, Authenticate());
    [HttpPost("{lease}/replay")] public Task<ReplayMetadata> Replay(string lease, ReplayMetadata metadata) => server.SaveReplay(lease, metadata, Authenticate());
    [HttpPost("{lease}/replay/chunks/{index:int}")] public Task<ReplayChunkInfo> Chunk(string lease, int index, ReplayChunkUpload chunk) => server.UploadReplay(lease, index, chunk, Authenticate());
}
