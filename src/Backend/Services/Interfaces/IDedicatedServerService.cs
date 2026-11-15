using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Replays;

namespace Services.Interfaces;

public interface IDedicatedServerService
{
    bool Authenticate(string key);
    Task<DedicatedTicket> Ticket(Guid player, DedicatedCommand request);
    Task<DedicatedAllocation?> Poll(string workerId, int protocol);
    Task<DedicatedHeartbeat> Renew(DedicatedCommand request, bool ready, string worker);
    Task Acknowledge(DedicatedCommand request, string worker);
    Task AuthorizeAdmission(DedicatedAdmissionCommand request, string worker);
    Task<List<Entities.DTOs.LiveMatchDto>> LiveMatches();
    Task Complete(DedicatedCompletion request, string worker);
    Task Release(DedicatedCommand request, string worker);
    Task InterruptExpired();
    Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch, string worker);
    Task<ReplayMetadata> SaveReplay(string lease, ReplayMetadata metadata, string worker);
    Task<ReplayChunkInfo> UploadReplay(string lease, int index, ReplayChunkUpload chunk, string worker);
}
