using System.Security.Claims;
using Entities.DTOs;
using Entities.DTOs.Common;
using Entities.Game;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PickleVerse.Replays;
using Services.Implementations;
using Services.Interfaces;

namespace API.Controllers;

[ApiController, Authorize]
[Route("api/Game/social")]
public sealed class SocialContentController(ISocialContentService content, ISocialDirectService direct,
    ISocialMediaService media, ISocialSessionService sessions, IAuthService auth) : ControllerBase
{
    Guid Player => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("posts")]
    public Task<SocialPage<SocialPostItem>> Feed(long? before) => content.Feed(Player, before);
    [HttpPost("posts")]
    public Task<SocialPostItem> Create(SocialPostInput input) => content.Save(Player, null, input);
    [HttpPut("posts/{post:long}")]
    public Task<SocialPostItem> Edit(long post, SocialPostInput input) => content.Save(Player, post, input);
    [HttpDelete("posts/{post:long}")]
    public async Task<ApiResponse> Delete(long post)
    {
        await content.Delete(Player, post);
        return ApiResponse.SuccessResponse();
    }
    [HttpPut("posts/{post:long}/like")]
    public Task<SocialPostItem> Like(long post, SocialLikeInput input) => content.Like(Player, post, input.Liked);
    [HttpGet("posts/{post:long}/comments")]
    public Task<SocialPage<SocialCommentItem>> Comments(long post, long? before) => content.Comments(Player, post, before);
    [HttpPost("posts/{post:long}/comments")]
    public Task<SocialCommentItem> Comment(long post, SocialText input) => content.Comment(Player, post, input.Body);
    [HttpDelete("posts/{post:long}/comments/{comment:long}")]
    public async Task<ApiResponse> DeleteComment(long post, long comment)
    {
        await content.DeleteComment(Player, post, comment);
        return ApiResponse.SuccessResponse();
    }
    [HttpGet("posts/{post:long}/replay")]
    public Task<ReplayMetadata> Replay(long post) => content.Replay(Player, post);
    [HttpGet("posts/{post:long}/replay/chunks/{index:int}")]
    public Task<ReplayChunkUpload> Chunk(long post, int index) => content.ReplayChunk(Player, post, index);

    [HttpPost("video"), RequestSizeLimit(SocialMediaService.MaxVideoBytes)]
    public Task<SocialMediaReceipt> Upload(CancellationToken cancellation)
    {
        if (!string.Equals(Request.ContentType, "video/mp4", StringComparison.OrdinalIgnoreCase))
            throw new Entities.Exceptions.AppException("Chọn tệp MP4.", System.Net.HttpStatusCode.UnsupportedMediaType);
        return media.Upload(Player, Request.Body, Request.ContentLength ?? 0, cancellation);
    }

    [HttpPost("posts/{post:long}/video-playback")]
    public async Task<SocialPlayback> Playback(long post)
    {
        await content.VideoMedia(Player, post);
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        await sessions.GrantVideo(token, new(Player, int.Parse(User.FindFirstValue("auth_version")!), post));
        return new($"api/Game/social/posts/{post}/video?playback={token}");
    }

    [AllowAnonymous, AcceptVerbs("GET", "HEAD", Route = "posts/{post:long}/video")]
    public async Task Video(long post, string? playback, CancellationToken cancellation)
    {
        Guid viewer;
        if (User.Identity?.IsAuthenticated == true) viewer = Player;
        else
        {
            var grant = playback is { Length: 48 } && playback.All(Uri.IsHexDigit) ? await sessions.ReadVideoGrant(playback) : null;
            if (grant == null || grant.PostId != post || !await auth.ValidateSessionAsync(grant.PlayerId, grant.AuthVersion))
                throw new Entities.Exceptions.AppException("Phiên phát video đã hết hiệu lực.", System.Net.HttpStatusCode.Unauthorized);
            viewer = grant.PlayerId;
        }
        var source = await media.Open(viewer, post, Request.Headers.Range.ToString(), cancellation);
        await using var stream = source.Stream;
        Response.ContentType = "video/mp4";
        Response.ContentLength = source.End - source.Start + 1;
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.CacheControl = "private, no-store";
        if (source.Partial)
        {
            Response.StatusCode = StatusCodes.Status206PartialContent;
            Response.Headers.ContentRange = $"bytes {source.Start}-{source.End}/{source.Length}";
        }
        if (HttpMethods.IsHead(Request.Method)) return;
        if (!source.Partial)
        {
            await stream.CopyToAsync(Response.Body, cancellation);
            return;
        }
        // Some S3-compatible services return the entire object even when requested with Range.
        byte[] buffer = new byte[64 * 1024];
        long skip = source.BytesToSkip;
        while (skip > 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(skip, buffer.Length)), cancellation);
            if (read == 0) throw new EndOfStreamException();
            skip -= read;
        }
        long remaining = source.End - source.Start + 1;
        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(remaining, buffer.Length)), cancellation);
            if (read == 0) throw new EndOfStreamException();
            await Response.Body.WriteAsync(buffer.AsMemory(0, read), cancellation);
            remaining -= read;
        }
    }

    [HttpGet("conversations")]
    public Task<IReadOnlyList<SocialConversationItem>> Inbox(int offset = 0) => direct.Inbox(Player, offset);
    [HttpGet("conversation")]
    public async Task<SocialConversationLookup> Conversation(Guid player) => new(await direct.Conversation(Player, player));
    [HttpGet("conversations/{conversation:guid}/messages")]
    public Task<SocialPage<SocialMessageItem>> Messages(Guid conversation, long? before) => direct.History(Player, conversation, before);
    [HttpPut("conversations/{conversation:guid}/read")]
    public async Task<ApiResponse> Read(Guid conversation, SocialReadInput input)
    {
        await direct.MarkRead(Player, conversation, input.Through);
        return ApiResponse.SuccessResponse();
    }
    [HttpPost("messages")]
    public Task<SocialMessageItem> Send(SocialMessageInput input) => direct.Send(Player, input);
}
