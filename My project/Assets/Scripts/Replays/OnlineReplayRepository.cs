using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Replays
{
    public interface IOnlineReplayRepository : IReplayRepository
    {
        Task<ReplayMetadata> ReadAsync(string player, string match);
        Task SaveAsync(ReplayMetadata metadata);
        Task WriteChunkAsync(ReplayMetadata metadata, ReplayChunk chunk);
        Task<ReplayChunk> ReadChunkAsync(ReplayMetadata metadata, ReplayChunkInfo chunk);
        Task RefreshAsync();
    }
    /// <summary>Only transient metadata is cached. Recording chunks are uploaded to the authenticated API.</summary>
    public sealed class OnlineReplayRepository : IOnlineReplayRepository
    {
        readonly Dictionary<string, ReplayMetadata> headers = new Dictionary<string, ReplayMetadata>();
        int session = -1;
        AccountService Account => AccountRuntime.Ensure().Account;
        void Guard(string player)
        {
            if (!Account.IsSignedIn || Account.Profile.UserId.ToString("N") != player)
                throw new InvalidOperationException("Replay không thuộc tài khoản đang đăng nhập.");
            if (session != Account.SessionVersion)
            {
                headers.Clear();
                session = Account.SessionVersion;
            }
        }
        static string Route(string match) => "api/Game/replays/" + Guid.Parse(match).ToString("N");
        public ReplayMetadata Read(string player, string match)
        {
            Guard(player);
            return headers.TryGetValue(match, out var value) ? value : null;
        }
        public async Task RefreshAsync()
        {
            Guard(Account.Profile?.UserId.ToString("N"));
            var list = await Account.SendAuthorizedAsync<List<ReplayMetadata>>("GET", "api/Game/replays");
            headers.Clear();
            foreach (var metadata in list)
                headers[metadata.matchId] = metadata;
        }
        public async Task<ReplayMetadata> ReadAsync(string player, string match)
        {
            Guard(player);
            var metadata = await Account.SendAuthorizedAsync<ReplayMetadata>("GET", Route(match));
            headers[match] = metadata;
            return metadata;
        }
        public async Task SaveAsync(ReplayMetadata metadata)
        {
            Guard(metadata.playerId);
            var saved = await Retry(() => Account.SendAuthorizedAsync<ReplayMetadata>("PUT", Route(metadata.matchId), metadata));
            headers[metadata.matchId] = saved;
            metadata.chunks = saved.chunks;
            metadata.duration = saved.duration;
            metadata.startedUtcTicks = saved.startedUtcTicks;
            metadata.complete = saved.complete;
        }
        sealed class ChunkData { public string data; }
        public async Task WriteChunkAsync(ReplayMetadata metadata, ReplayChunk chunk)
        {
            if (chunk.frames.Count == 0)
                return;
            Guard(metadata.playerId);
            int index = metadata.chunks.Count;
            string json = JsonUtility.ToJson(chunk);
            string payload = await Task.Run(() =>
            {
                using (var bytes = new MemoryStream())
                {
                    using (var gzip = new GZipStream(bytes, System.IO.Compression.CompressionLevel.Fastest, true))
                    {
                        var raw = Encoding.UTF8.GetBytes(json);
                        gzip.Write(raw, 0, raw.Length);
                    }
                    return Convert.ToBase64String(bytes.ToArray());
                }
            });
            var info = await Retry(() => Account.SendAuthorizedAsync<ReplayChunkInfo>("PUT", Route(metadata.matchId) + "/chunks/" + index, new ChunkData { data = payload }));
            metadata.chunks.Add(info);
            metadata.duration = info.end;
            headers[metadata.matchId] = metadata;
        }
        public async Task<ReplayChunk> ReadChunkAsync(ReplayMetadata metadata, ReplayChunkInfo info)
        {
            Guard(metadata.playerId);
            int index = metadata.chunks.FindIndex(c => c.file == info.file);
            if (index < 0)
                throw new InvalidDataException("Không tìm thấy chunk replay.");
            var payload = await Account.SendAuthorizedAsync<ChunkData>("GET", Route(metadata.matchId) + "/chunks/" + index);
            string json = await Task.Run(() =>
            {
                using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(payload.data)), CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip))
                    return reader.ReadToEnd();
            });
            var chunk = JsonUtility.FromJson<ReplayChunk>(json);
            if (chunk?.frames == null || chunk.frames.Count == 0 || chunk.frames.Count > 120)
                throw new InvalidDataException("Snapshot replay không hợp lệ.");
            return chunk;
        }
        static async Task<T> Retry<T>(Func<Task<T>> command)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await command();
                }
                catch (Services.Api.ApiException error) when (attempt < 2 && (error.StatusCode >= 500 || error.StatusCode <= 0))
                {
                    await Task.Delay((attempt + 1) * 500);
                }
            }
        }
        // Synchronous file adapters remain available for isolated format tests only.
        public void Save(ReplayMetadata metadata) => throw new InvalidOperationException("Await SaveAsync for online replays.");
        public void WriteChunk(ReplayMetadata metadata, ReplayChunk chunk) => throw new InvalidOperationException("Await WriteChunkAsync for online replays.");
        public ReplayChunk ReadChunk(ReplayMetadata metadata, ReplayChunkInfo info) => throw new InvalidOperationException("Await ReadChunkAsync for online replays.");
        public void RecoverAndPrune(string player, IEnumerable<string> retained)
        {
            Guard(player);
        }
    }
}
