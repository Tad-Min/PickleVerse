using Repositories.UnitOfWork;
using System.Text.Json;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Amazon.S3;
using Microsoft.Extensions.Logging;
using PickleVerse.Replays;
using Entities.Exceptions;
using Services.Interfaces;

namespace Services.Implementations;


// One shared recording per match. Only its published S3 manifest key is indexed in SQL.
public sealed class MatchReplayService : IMatchReplayService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageService storage;
    private readonly ILogger<MatchReplayService> logger;

    public MatchReplayService(IUnitOfWork unitOfWork, IStorageService storage, ILogger<MatchReplayService> logger)
    {
        _unitOfWork = unitOfWork;
        this.storage = storage;
        this.logger = logger;
    }

    static AppException Bad(string message) => new(message, HttpStatusCode.BadRequest);
    static AppException Missing() => new("Không tìm thấy replay của trận đấu này.", HttpStatusCode.NotFound);
    public static string ManifestKey(Guid match) => $"replays/{match:N}/manifest.json";
    static string ChunkFile(int index, string hash) => $"{index:D6}-{hash}.json.gz";
    static string ChunkKey(Guid match, ReplayChunkInfo chunk) => $"replays/{match:N}/{chunk.file}";
    async Task Lock(Guid match) => await _unitOfWork.Game.Lock("replay:" + match.ToString("N"));
    async Task<DateTime> GuardWriter(Guid player, Guid match, bool trustedServer = false)
    {
        if (!trustedServer && await _unitOfWork.Game.IsDedicated(match))
            throw new AppException("Replay trận dedicated do máy chủ ghi.", HttpStatusCode.Forbidden);
        var access=await _unitOfWork.Game.ReplayWriter(player, match) ?? throw Missing();
        if(access.Status is not (MatchStatus.Playing or MatchStatus.Finished))throw Missing();
        if (!access.writer)
            throw new AppException("Chỉ máy điều phối được ghi replay chung của trận.", HttpStatusCode.Forbidden);
        return access.started;
    }
    async Task<ReplayMetadata?> LoadManifest(Guid match)
    {
        await using var stream = await storage.GetFileAsync(ManifestKey(match));
        if (stream == null) return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var header = GameJson.Read<ReplayMetadata>(await reader.ReadToEndAsync());
        if (!Guid.TryParse(header.matchId, out var id) || id != match || header.chunks == null || header.actors == null) throw Bad("Manifest replay không hợp lệ.");
        return header;
    }
    async Task StoreManifest(Guid match, ReplayMetadata header)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(GameJson.Write(header)));
        await storage.PutFileAsync(stream, ManifestKey(match), "application/json");
    }
    Task Index(Guid match) => _unitOfWork.Game.IndexReplay(match, ManifestKey(match));

    // Recording can finish before the game result. Publishing is retried at match
    // completion and on subsequent recording writes. Storage outages cannot undo rewards.
    public async Task PublishAvailable(Guid match)
    {
        ReplayMetadata? header;
        try { header = await LoadManifest(match); }
        catch (Exception error) when (error is IOException or HttpRequestException or AmazonS3Exception)
        { logger.LogWarning(error, "Replay publication deferred for {MatchId}", match); return; }
        if (header?.chunks.Count > 0) await Index(match);
    }
    public async Task<List<ReplayMetadata>> List(Guid player, bool admin = false)
    {
        var matches=await _unitOfWork.Game.ReplayMatches(player, admin);
        var headers = await Task.WhenAll(matches.Select(LoadManifest));
        var ready = headers.Where(h => h?.chunks.Count > 0).Select(h => h!).ToList();
        foreach (var header in ready) await Index(Guid.Parse(header.matchId));
        return ready;
    }
    public async Task<ReplayMetadata> Read(Guid player, Guid match, bool admin = false)
    {
        var canonical=ManifestKey(match);
        var key=await _unitOfWork.Game.ReplayKey(player, match, admin, canonical);
        if (key != ManifestKey(match)) throw Missing();
        var header = await LoadManifest(match) ?? throw Missing();
        if (header.chunks.Count == 0) throw Missing();
        await Index(match);
        return header;
    }
    public async Task<ReplayMetadata> Save(Guid player, Guid match, ReplayMetadata input, bool trustedServer = false)
    {
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        await Lock(match);
        var started = await GuardWriter(player, match, trustedServer);
        if (input.version != 1 || input.sampleRate != 30 || !Guid.TryParse(input.playerId, out var recorder) || recorder != player
            || !Guid.TryParse(input.matchId, out var id) || id != match || input.actors == null || input.actors.Count is < 2 or > 4
            || input.actors.Any(a => a == null || !Guid.TryParse(a.id, out _) || a.bones == null || a.bones.Length > 1024 || a.bones.Any(b => b == null || b.Length > 1000)
                || a.visualId is not ("Male" or "Female") || a.team is < 0 or > 1 || a.name?.Length > 100 || a.paddlePath?.Length > 1000)) throw Bad("Metadata replay không hợp lệ.");
        var seats = await _unitOfWork.Game.ReplaySeats(match);
        if (input.actors.Select(a => Guid.Parse(a.id)).Distinct().Count() != seats.Count || input.actors.Count != seats.Count
            || input.actors.Any(a => !seats.Contains((Guid.Parse(a.id), a.team)))) throw Bad("Đội hình replay không khớp người tham gia trận.");
        var previous = await LoadManifest(match);
        if (previous != null && (previous.playerId != player.ToString("N") || GameJson.Write(previous.actors) != GameJson.Write(input.actors) || previous.night != input.night))
            throw Bad("Không được thay nguồn ghi hoặc đội hình replay đã ghi.");
        var header = previous ?? input;
        header.playerId = player.ToString("N"); header.matchId = match.ToString("N"); header.startedUtcTicks = started.Ticks;
        header.chunks = previous?.chunks ?? [];
        header.duration = header.chunks.LastOrDefault()?.end ?? 0;
        header.complete |= input.complete;
        await StoreManifest(match, header);
        if (header.chunks.Count > 0) await Index(match);
        await tx.CommitAsync();
        return header;
    }
    public async Task<ReplayChunkInfo> Upload(Guid player, Guid match, int index, ReplayChunkUpload input, bool trustedServer = false)
    {
        if (index < 0 || index >= 10000 || input.Data == null || input.Data.Length > 2800000) throw Bad("Chunk replay vượt giới hạn.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input.Data); } catch (FormatException) { throw Bad("Chunk replay không hợp lệ."); }
        ReplayChunk chunk;
        try
        {
            await using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            await using var decoded = new MemoryStream();
            var buffer = new byte[8192]; int size;
            while ((size = await gzip.ReadAsync(buffer)) > 0)
            { if (decoded.Length + size > 8 * 1024 * 1024) throw Bad("Chunk replay giải nén vượt giới hạn."); await decoded.WriteAsync(buffer.AsMemory(0, size)); }
            chunk = GameJson.Read<ReplayChunk>(Encoding.UTF8.GetString(decoded.ToArray()).TrimStart('\uFEFF'));
        }
        catch (InvalidDataException) { throw Bad("Chunk replay không giải nén được."); }
        catch (System.Text.Json.JsonException) { throw Bad("Nội dung chunk replay không hợp lệ."); }
        if (chunk.frames == null || chunk.frames.Count is < 1 or > 120 || chunk.events == null || chunk.events.Count > 512
            || chunk.frames.Any(f => f == null || !double.IsFinite(f.time) || f.time < 0 || f.time > 21600 || f.ball?.Length != 3 || f.ball.Any(v => !float.IsFinite(v)))) throw Bad("Snapshot replay không hợp lệ.");
        await using var tx = await _unitOfWork.Game.BeginTransactionAsync();
        await Lock(match); await GuardWriter(player, match, trustedServer);
        var header = await LoadManifest(match) ?? throw Missing();
        if (chunk.frames.Any(f => f.actors == null || f.actors.Length != header.actors.Count || f.actors.Where((a, i) => a == null || a.root?.Length != 10 || a.bones?.Length != header.actors[i].bones.Length * 10 || a.root.Any(v => !float.IsFinite(v)) || a.bones.Any(v => !float.IsFinite(v))).Any())) throw Bad("Pose replay không hợp lệ.");
        for (int i = 1; i < chunk.frames.Count; i++) if (chunk.frames[i].time < chunk.frames[i - 1].time) throw Bad("Thời gian replay không tăng dần.");
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (index < header.chunks.Count)
        {
            var existing = header.chunks[index];
            if (existing.contentHash != hash) throw Bad("Chunk này đã được lưu với nội dung khác.");
            await Index(match); await tx.CommitAsync(); return existing;
        }
        if (header.complete || index != header.chunks.Count || header.chunks.LastOrDefault()?.end > chunk.frames[0].time) throw Bad("Replay đã đóng hoặc thứ tự chunk không hợp lệ.");
        var info = new ReplayChunkInfo { file = ChunkFile(index, hash), contentHash = hash, start = chunk.frames[0].time, end = chunk.frames[^1].time };
        await using var stream = new MemoryStream(bytes);
        await storage.PutFileAsync(stream, ChunkKey(match, info), "application/gzip");
        header.chunks.Add(info); header.duration = info.end;
        await StoreManifest(match, header); await Index(match); await tx.CommitAsync();
        return info;
    }
    public async Task<ReplayChunkUpload> Chunk(Guid player, Guid match, int index, bool admin = false)
    {
        var header = await Read(player, match, admin);
        if (index < 0 || index >= header.chunks.Count) throw Missing();
        var info = header.chunks[index];
        if (info.contentHash?.Length != 64 || info.file != ChunkFile(index, info.contentHash)) throw Bad("Manifest chunk không hợp lệ.");
        await using var stream = await storage.GetFileAsync(ChunkKey(match, info)) ?? throw Missing();
        await using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes);
        var data = bytes.ToArray();
        if (Convert.ToHexString(SHA256.HashData(data)) != info.contentHash) throw Bad("Dữ liệu replay không khớp hash.");
        return new ReplayChunkUpload { Data = Convert.ToBase64String(data) };
    }
}
