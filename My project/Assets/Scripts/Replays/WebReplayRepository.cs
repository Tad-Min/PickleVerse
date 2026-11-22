using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Replays
{
    [Serializable]
    public sealed class WebReplayAccess
    {
        public string matchId, grant;
        public DateTime expiresAt;
        public ReplayMetadata metadata;
    }
    // A read-only repository with its own scoped bearer capability. It never calls
    // AccountService, refreshes a login, or writes into the player's replay cache.
    public sealed class WebReplayRepository : IOnlineReplayRepository, IDisposable
    {
        readonly ApiClient api;
        readonly string match;
        readonly DateTime expires;
        string grant;
        ReplayMetadata header;
        public WebReplayRepository(ApiClient api, WebReplayAccess access, string requestedMatch)
        {
            this.api = api;
            if (!Guid.TryParse(requestedMatch, out var requested) || !Guid.TryParse(access?.matchId, out var received) ||
                requested != received || !ReplayWebLink.IsToken(access.grant) || access.expiresAt <= DateTime.UtcNow)
                throw new InvalidDataException("Vé xem replay không hợp lệ hoặc đã hết hạn.");
            match = requested.ToString("N"); grant = access.grant; expires = access.expiresAt;
            CheckHeader(access.metadata); header = access.metadata;
        }
        void Check(string id)
        {
            if (grant == null || expires <= DateTime.UtcNow)
                throw new ApiException(401, "Vé xem replay đã hết hạn. Hãy bấm Xem replay lại trên trang Admin.");
            if (!Guid.TryParse(id, out var value) || value.ToString("N") != match)
                throw new InvalidDataException("Vé xem không thuộc replay này.");
        }
        void CheckHeader(ReplayMetadata value)
        {
            Check(value?.matchId);
            if (value.chunks == null || value.chunks.Count == 0 || value.actors == null || value.actors.Count < 2 || value.actors.Count > 4)
                throw new InvalidDataException("Metadata replay không hợp lệ.");
        }
        string Route => "api/replay-launch/view/" + match;
        public ReplayMetadata Read(string player, string id) { Check(id); return header; }
        public async Task<ReplayMetadata> ReadAsync(string player, string id)
        {
            Check(id);
            var value = await api.SendAsync<ReplayMetadata>("GET", Route, token: grant);
            CheckHeader(value); return header = value;
        }
        [Serializable] sealed class ChunkData { public string data; }
        public async Task<ReplayChunk> ReadChunkAsync(ReplayMetadata metadata, ReplayChunkInfo info)
        {
            Check(metadata.matchId);
            int index = metadata.chunks.FindIndex(chunk => chunk.file == info.file);
            if (index < 0) throw new InvalidDataException("Không tìm thấy chunk replay.");
            var payload = await api.SendAsync<ChunkData>("GET", Route + "/chunks/" + index, token: grant);
            if (payload.data == null || payload.data.Length > 2800000) throw new InvalidDataException("Chunk replay vượt giới hạn.");
            string json = await Task.Run(() =>
            {
                using (var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(payload.data)), CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[8192]; int count;
                    while ((count = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + count > 8 * 1024 * 1024) throw new InvalidDataException("Chunk replay giải nén vượt giới hạn.");
                        output.Write(buffer, 0, count);
                    }
                    return Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
                }
            });
            var result = JsonUtility.FromJson<ReplayChunk>(json);
            if (result?.frames == null || result.frames.Count == 0 || result.frames.Count > 120)
                throw new InvalidDataException("Snapshot replay không hợp lệ.");
            return result;
        }
        public void Dispose() { grant = null; header = null; }
        static Exception ReadOnly() => new InvalidOperationException("Vé Admin chỉ cho phép xem replay.");
        public Task RefreshAsync() => Task.CompletedTask;
        public Task SaveAsync(ReplayMetadata metadata) => throw ReadOnly();
        public Task WriteChunkAsync(ReplayMetadata metadata, ReplayChunk chunk) => throw ReadOnly();
        public void Save(ReplayMetadata metadata) => throw ReadOnly();
        public void WriteChunk(ReplayMetadata metadata, ReplayChunk chunk) => throw ReadOnly();
        public ReplayChunk ReadChunk(ReplayMetadata metadata, ReplayChunkInfo chunk) => throw new InvalidOperationException("Await ReadChunkAsync.");
        public void RecoverAndPrune(string player, IEnumerable<string> retained) { }
    }
}
