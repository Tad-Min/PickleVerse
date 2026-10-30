using System.Collections.Concurrent;
using Services.Interfaces;

sealed class TestReplayStorage : IStorageService
{
    public static readonly TestReplayStorage Shared = new();
    public readonly ConcurrentDictionary<string, byte[]> Objects = new();
    public bool FailReads;
    public bool FailNextManifestWrite;
    public Task EnsureBucketExistsAsync() => Task.CompletedTask;
    public async Task PutFileAsync(Stream stream, string key, string contentType)
    {
        if (FailNextManifestWrite && key.EndsWith("/manifest.json", StringComparison.Ordinal))
        { FailNextManifestWrite = false; throw new IOException("Simulated manifest write failure."); }
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        Objects[key] = copy.ToArray();
    }
    public async Task<string> UploadFileAsync(Stream stream, string name, string type)
    { await PutFileAsync(stream, name, type); return name; }
    public Task<Stream?> GetFileAsync(string key)
    {
        if (FailReads) throw new IOException("Simulated storage outage.");
        return Task.FromResult(Objects.TryGetValue(key, out var bytes) ? (Stream?)new MemoryStream(bytes) : null);
    }
    public Task<bool> DeleteFileAsync(string key) => Task.FromResult(Objects.TryRemove(key, out _));
    public string GetFileUrl(string key) => throw new InvalidOperationException("Replay reads must use the authenticated API.");
}
