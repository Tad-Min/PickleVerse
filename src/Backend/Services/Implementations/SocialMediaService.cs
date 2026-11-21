using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Entities.DTOs;
using Entities.Exceptions;
using Entities.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repositories.UnitOfWork;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class SocialMediaService(IUnitOfWork unit, ISocialContentService content, ISocialObjectStorage storage,
    IConfiguration config, TimeProvider clock, ILogger<SocialMediaService> logger) : ISocialMediaService
{
    public const int MaxVideoBytes = 250_000_000;
    static string Key(Guid id) => $"social/video/{id:N}.mp4";
    static readonly SemaphoreSlim Uploads = new(4, 4);

    public async Task<SocialMediaReceipt> Upload(Guid actor, Stream input, long length, CancellationToken cancellation)
    {
        if (length is < 1 or > MaxVideoBytes)
            throw new AppException("Chọn MP4 tối đa 250 MB.", HttpStatusCode.RequestEntityTooLarge);
        if (!await Uploads.WaitAsync(0, cancellation))
            throw new AppException("Đang có nhiều video được tải lên. Vui lòng thử lại.", HttpStatusCode.TooManyRequests);

        Guid id = Guid.NewGuid();
        string path = Path.Combine(Path.GetTempPath(), $"pkv-video-{id:N}.mp4");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            await using var spool = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] buffer = new byte[64 * 1024];
            long copied = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, deadline.Token)) != 0)
            {
                copied += read;
                if (copied > length) throw new BadRequestException("Dung lượng video không hợp lệ.");
                await spool.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
            }
            if (copied != length) throw new BadRequestException("Video tải lên chưa đầy đủ.");
            await spool.FlushAsync(deadline.Token);
            await ValidateVideo(path, deadline.Token);
            spool.Position = 0;

            // A durable draft exists before S3 receives bytes. Cleanup can retry after a process or network failure.
            var draft = new SocialMedia { MediaId = id, OwnerId = actor, Bytes = length, CreatedAt = clock.GetUtcNow().UtcDateTime };
            unit.Game.AddSocialMedia(draft);
            await unit.SaveChangesAsync();
            try
            {
                await storage.WriteVideo(Key(id), spool, deadline.Token);
                draft.Ready = true;
                await unit.SaveChangesAsync();
            }
            catch
            {
                draft.Deleted = true;
                await unit.SaveChangesAsync();
                throw;
            }
            return new(id, length);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new AppException("Đã hết thời gian tải hoặc kiểm tra video.", HttpStatusCode.RequestTimeout);
        }
        finally
        {
            Uploads.Release();
            DeleteSpool(path);
        }
    }

    async Task ValidateVideo(string path, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var start = new ProcessStartInfo(config["Social:FFprobePath"] ?? "ffprobe")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (string argument in new[] { "-v", "error", "-protocol_whitelist", "file", "-f", "mov", "-i", path,
            "-show_entries", "format=duration:format_tags=major_brand:stream=codec_name,codec_type,duration:stream_disposition=attached_pic",
            "-of", "json" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start ffprobe.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var json = await output;
            await errors;
            if (process.ExitCode == 0)
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var format = root.GetProperty("format");
                var streams = root.GetProperty("streams").EnumerateArray().ToArray();
                bool nonMp4 = !format.TryGetProperty("tags", out var tags) || !tags.TryGetProperty("major_brand", out var brand) ||
                    brand.GetString()?.Trim() is null or "qt" or "M4A" or "mjp2";
                if (!nonMp4 && DurationAllowed(format) &&
                    streams.Count(stream => IsStream(stream, "video", "h264") &&
                        (!stream.TryGetProperty("disposition", out var disposition) || disposition.GetProperty("attached_pic").GetInt32() == 0)) == 1 &&
                    streams.All(stream => (IsStream(stream, "video", "h264") || IsStream(stream, "audio", "aac")) &&
                        (!stream.TryGetProperty("duration", out _) || DurationAllowed(stream))))
                    return;
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        throw new BadRequestException("Video phải là MP4 H.264, âm thanh AAC nếu có, dài tối đa 15 phút.");
    }
    static bool DurationAllowed(JsonElement item) => item.TryGetProperty("duration", out var field) &&
        double.TryParse(field.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds is > 0 and <= 900;
    static bool IsStream(JsonElement stream, string type, string codec) =>
        stream.TryGetProperty("codec_type", out var kind) && kind.GetString() == type &&
        stream.TryGetProperty("codec_name", out var format) && format.GetString() == codec;

    public async Task<SocialVideo> Open(Guid viewer, long postId, string? range, CancellationToken cancellation)
    {
        var id = await content.VideoMedia(viewer, postId);
        var media = await unit.Game.SocialMediaById(id);
        if (media is not { Ready: true, Deleted: false }) throw new NotFoundException("Video không còn sẵn sàng.");
        var (start, end, partial) = ByteRange(range, media.Bytes);
        var source = await storage.ReadVideo(Key(id), start, end, partial, cancellation);
        return new(source.Stream, media.Bytes, start, end, partial, source.IsPartial ? 0 : start);
    }
    static (long Start, long End, bool Partial) ByteRange(string? range, long length)
    {
        if (string.IsNullOrWhiteSpace(range)) return (0, length - 1, false);
        var invalid = new AppException("Khoảng byte video không hợp lệ.", HttpStatusCode.RequestedRangeNotSatisfiable);
        if (!range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) throw invalid;
        var parts = range[6..].Split('-');
        if (parts.Length != 2) throw invalid;
        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long suffix) || suffix <= 0) throw invalid;
            return (length - Math.Min(suffix, length), length - 1, true);
        }
        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long first) || first >= length) throw invalid;
        if (parts[1].Length == 0) return (first, length - 1, true);
        if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long last) || last < first) throw invalid;
        return (first, Math.Min(last, length - 1), true);
    }

    public async Task Cleanup(CancellationToken cancellation)
    {
        var spoolCutoff = clock.GetUtcNow().UtcDateTime.AddMinutes(-30);
        foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), "pkv-video-*.mp4", SearchOption.TopDirectoryOnly))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            if (Guid.TryParseExact(name["pkv-video-".Length..], "N", out _) && File.GetLastWriteTimeUtc(path) < spoolCutoff)
                DeleteSpool(path);
        }
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-24);
        foreach (Guid id in await unit.Game.ExpiredSocialMedia(cutoff))
        {
            cancellation.ThrowIfCancellationRequested();
            await using var transaction = await unit.Game.BeginTransactionAsync(cancellationToken: cancellation);
            await unit.Game.Lock("social-media:" + id);
            var media = await unit.Game.SocialMediaById(id);
            if (media == null || !media.Deleted && media.CreatedAt >= cutoff || await unit.Game.MediaUsed(id, 0)) continue;
            if (!await storage.DeleteFileAsync(Key(id))) continue;
            unit.Game.RemoveSocialMedia(media);
            await unit.SaveChangesAsync();
            await transaction.CommitAsync(cancellation);
        }
    }

    void DeleteSpool(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not delete a social video spool ({ErrorType})", error.GetType().Name);
        }
    }
}
