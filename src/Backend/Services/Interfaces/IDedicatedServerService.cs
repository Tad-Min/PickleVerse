using Entities.Game;
using PickleVerse.Progression;
using PickleVerse.Replays;

namespace Services.Interfaces;

public interface IDedicatedServerService
{
    bool Authenticate(string key);
    Task<DedicatedTicket> Ticket(Guid player, DedicatedCommand request);
    Task<DedicatedAllocation?> Poll(string workerId);
    Task Renew(DedicatedCommand request, bool ready);
    Task Complete(DedicatedCompletion request);
    Task Release(DedicatedCommand request);
    Task InterruptExpired();
    Task<TelemetryReceipt> Telemetry(string lease, TelemetryBatch batch);
    Task<ReplayMetadata> SaveReplay(string lease, ReplayMetadata metadata);
    Task<ReplayChunkInfo> UploadReplay(string lease, int index, ReplayChunkUpload chunk);
}
