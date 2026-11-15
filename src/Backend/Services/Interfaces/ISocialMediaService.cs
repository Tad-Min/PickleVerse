using Entities.DTOs;

namespace Services.Interfaces;

public interface ISocialMediaService
{
    Task<SocialMediaReceipt> Upload(Guid actor, Stream input, long length, CancellationToken cancellation);
    Task<SocialVideo> Open(Guid viewer, long postId, string? range, CancellationToken cancellation);
    Task Cleanup(CancellationToken cancellation);
}

public interface ISocialObjectStorage
{
    Task WriteVideo(string key, Stream input, CancellationToken cancellation);
    Task<(Stream Stream, bool IsPartial)> ReadVideo(string key, long start, long end, bool partial, CancellationToken cancellation);
    Task<bool> DeleteFileAsync(string key);
}
