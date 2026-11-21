#if !UNITY_SERVER
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PickleVerse.Services.Api;
using PickleVerse.Services.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace PickleVerse.Services
{
    public static class SocialTransport
    {
        public static string Url(string path)
        {
            var settings = AccountRuntime.Ensure().Settings;
            return ApiUrl.Build(settings.baseUrl, path, Application.isEditor || Debug.isDebugBuild, settings.allowRemoteHttpInDevelopment);
        }

        public static Task<Guid> UploadVideo(string file, IProgress<float> progress, CancellationToken cancellation)
        {
            var account = AccountRuntime.Ensure().Account;
            return account.WithAccessTokenAsync(async token =>
            {
                using var request = new UnityWebRequest(Url("api/Game/social/video"), UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerFile(file),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 900,
                    redirectLimit = 0
                };
                request.SetRequestHeader("Authorization", "Bearer " + token);
                request.SetRequestHeader("Content-Type", "video/mp4");
                var pending = request.SendWebRequest();
                while (!pending.isDone)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        request.Abort();
                        cancellation.ThrowIfCancellationRequested();
                    }
                    progress.Report(Mathf.Max(0, request.uploadProgress));
                    await Task.Delay(50);
                }
                cancellation.ThrowIfCancellationRequested();
                if (request.result != UnityWebRequest.Result.Success)
                    throw ApiClient.ParseError(new ApiHttpResponse(request.responseCode, request.downloadHandler.text));
                return JObject.Parse(request.downloadHandler.text)["data"]["mediaId"].ToObject<Guid>();
            }, cancellation);
        }

        public static async Task<ClientWebSocket> ConnectLobby(CancellationToken cancellation)
        {
            var account = AccountRuntime.Ensure().Account;
            await account.SendAuthorizedAsync<PickleVerse.Services.Api.BackendPlayerProfile>("GET", "api/Profiles/me", cancellation: cancellation);
            return await account.WithAccessTokenAsync(async token =>
            {
                var uri = new UriBuilder(Url("api/Game/social/hub"));
                uri.Scheme = uri.Scheme == "https" ? "wss" : "ws";
                var socket = new ClientWebSocket();
                socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
                try
                {
                    await socket.ConnectAsync(uri.Uri, cancellation);
                    return socket;
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }, cancellation);
        }
    }
}
#endif
