using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Replays;
using PickleVerse.Services.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace PickleVerse.Tests
{
    public sealed class WebReplayTests
    {
        const string Match = "879f2b5bd5eb4c7e979020a3ee98ee79";
        static string Code => new string('a', 64);
        static string Url => "pickleverse://replay/" + Match + "?code=" + Code;
        [Test] public void ValidLinkSelectsOnlyItsReplay()
        {
            Assert.That(ReplayWebLink.TryParse(Url, out var link), Is.True);
            Assert.That(link.MatchId, Is.EqualTo(Match)); Assert.That(link.Code, Is.EqualTo(Code));
        }
        [TestCase("javascript:alert(1)")]
        [TestCase("https://replay/")]
        [TestCase("pickleverse://replay/../")]
        [TestCase("pickleverse://replay:80/")]
        [TestCase("pickleverse://user@replay/")]
        [TestCase("pickleverse://replay.evil/")]
        public void RejectsOtherProtocolsAndAuthorities(string prefix)
            => Assert.That(ReplayWebLink.TryParse(prefix + Match + "?code=" + Code, out _), Is.False);
        [Test] public void RejectsAmbiguousOrExtendedLinks()
        {
            foreach (var uri in new[] { " " + Url, Url + " ", Url + "#x", Url + "&code=" + Code, Url + "&api=https://other.invalid", Url + "\n",
                Url.Replace(Match, "foo/../" + Match), Url.Replace(Code, "short"), Url.Replace(Code, new string('A', 64)), Url.Replace(Match, "../outside") })
                Assert.That(ReplayWebLink.TryParse(uri, out _), Is.False, uri.Substring(0, Math.Min(uri.Length, 64)));
        }
        sealed class Transport : IApiTransport
        {
            public int Calls;
            public ReplayMetadata Metadata;
            public string Payload;
            public Task<ApiHttpResponse> SendAsync(string method, string path, string json, string token, CancellationToken cancellation)
            {
                Assert.That(method, Is.EqualTo("GET")); Assert.That(json, Is.Null); Assert.That(token, Is.EqualTo(Code));
                Assert.That(path.StartsWith("api/replay-launch/view/" + Match), Is.True);
                Calls++;
                return Task.FromResult(new ApiHttpResponse(200, "{\"success\":true,\"data\":" +
                    (path.EndsWith("/chunks/0") ? "{\"data\":\"" + Payload + "\"}" : JsonUtility.ToJson(Metadata)) + "}"));
            }
        }
        static ReplayMetadata Header() => new ReplayMetadata { matchId = Match, actors = new System.Collections.Generic.List<ReplayActorInfo> {
            new ReplayActorInfo(), new ReplayActorInfo() }, chunks = new System.Collections.Generic.List<ReplayChunkInfo> { new ReplayChunkInfo { file = "0.json.gz" } } };
        static WebReplayAccess Access(ReplayMetadata metadata) => new WebReplayAccess { matchId = Match, grant = Code, expiresAt = DateTime.UtcNow.AddHours(1), metadata = metadata };
        [UnityTest] public IEnumerator ScopedRepositoryReadsReplayWithoutAccountAuthentication()
        {
            var read = CheckScopedRead();
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 10;
            while (!read.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(read.IsCompleted, Is.True, "Scoped replay read did not finish.");
            read.GetAwaiter().GetResult();
        }

        static async Task CheckScopedRead()
        {
            var header = Header(); var transport = new Transport { Metadata = header };
            using (var bytes = new MemoryStream())
            {
                using (var gzip = new GZipStream(bytes, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    byte[] raw = Encoding.UTF8.GetBytes("{\"frames\":[{\"time\":0,\"ball\":[1,2,3]}]}"); gzip.Write(raw, 0, raw.Length);
                }
                transport.Payload = Convert.ToBase64String(bytes.ToArray());
            }
            var api = new ApiClient(transport);
            var wire = await new ApiClient(new AccessTransport(header)).SendAsync<WebReplayAccess>("POST", "api/replay-launch/redeem");
            Assert.That(wire.expiresAt, Is.GreaterThan(DateTime.UtcNow));
            using (var repo = new WebReplayRepository(api, wire, Match))
            {
                var metadata = await repo.ReadAsync("existing-player-is-not-used", Match);
                var chunk = await repo.ReadChunkAsync(metadata, metadata.chunks[0]);
                Assert.That(chunk.frames[0].ball, Is.EqualTo(new float[] { 1, 2, 3 }));
                Assert.That(transport.Calls, Is.EqualTo(2));
                Assert.Throws<InvalidOperationException>(() => repo.Save(metadata));
                Assert.Throws<InvalidOperationException>(() => repo.WriteChunk(metadata, chunk));
                Assert.Throws<InvalidDataException>(() => repo.Read("", Guid.NewGuid().ToString("N")));
            }
        }
        sealed class AccessTransport : IApiTransport
        {
            readonly ReplayMetadata metadata;
            public AccessTransport(ReplayMetadata metadata) { this.metadata = metadata; }
            public Task<ApiHttpResponse> SendAsync(string method, string path, string json, string token, CancellationToken cancellation)
                => Task.FromResult(new ApiHttpResponse(200, "{\"success\":true,\"data\":{\"matchId\":\"" + Match + "\",\"grant\":\"" + Code +
                    "\",\"expiresAt\":\"" + DateTime.UtcNow.AddHours(1).ToString("O") + "\",\"metadata\":" + JsonUtility.ToJson(metadata) + "}}"));
        }
        [Test] public void ExpiredWrongMatchAndDisposedGrantsCannotRead()
        {
            var header = Header(); var api = new ApiClient(new Transport()); var access = Access(header);
            access.expiresAt = DateTime.UtcNow.AddSeconds(-1);
            Assert.Throws<InvalidDataException>(() => new WebReplayRepository(api, access, Match));
            Assert.Throws<InvalidDataException>(() => new WebReplayRepository(api, Access(header), Guid.NewGuid().ToString("N")));
            var repo = new WebReplayRepository(api, Access(header), Match); repo.Dispose();
            Assert.Throws<ApiException>(() => repo.Read("", Match));
        }
        [Test] public void LeavingExternalReplayClearsCapabilityAndCancelsPresentation()
        {
            var repo = new WebReplayRepository(new ApiClient(new Transport()), Access(Header()), Match);
            ReplayLaunch.SelectExternal(Match, Task.FromResult<IOnlineReplayRepository>(repo), () => { });
            var ready = ReplayLaunch.PresentationReady;
            ReplayLaunch.Select("player", "local-match");
            Assert.That(ready.Task.IsCanceled, Is.True); Assert.That(ReplayLaunch.IsExternal, Is.False);
            Assert.Throws<ApiException>(() => repo.Read("", Match));
        }
    }
}
