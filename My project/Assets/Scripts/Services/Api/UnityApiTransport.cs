using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Services.Utilities;
using UnityEngine;
using UnityEngine.Networking;

namespace PickleVerse.Services.Api
{
    // Call from Unity's main thread; requests run without blocking the player loop.
    public sealed class UnityApiTransport : MonoBehaviour, IApiTransport
    {
        public BackendApiSettings Settings { get; set; }
        readonly Dictionary<UnityWebRequest, TaskCompletionSource<ApiHttpResponse>> pending = new Dictionary<UnityWebRequest, TaskCompletionSource<ApiHttpResponse>>();
        public Task<ApiHttpResponse> SendAsync(string method, string path, string json, string bearerToken, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            string url = ApiUrl.Build(Settings.baseUrl, path, Application.isEditor || Debug.isDebugBuild, Settings.allowRemoteHttpInDevelopment);
            var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer(), timeout = Settings.timeoutSeconds, redirectLimit = 0 };
            request.SetRequestHeader("Accept", "application/json");
            if (json != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                if (path == "api/AiCoaching/telemetry" && bytes.Length >= 16384)
                {
                    using (var buffer = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(buffer, System.IO.Compression.CompressionLevel.Fastest, true)) gzip.Write(bytes, 0, bytes.Length);
                        bytes = buffer.ToArray();
                    }
                    request.SetRequestHeader("Content-Encoding", "gzip");
                }
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }
            if (!string.IsNullOrEmpty(bearerToken))
                request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            var completion = new TaskCompletionSource<ApiHttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(request, completion);
            StartCoroutine(Send(request, completion, cancellation));
            return completion.Task;
        }
        IEnumerator Send(UnityWebRequest request, TaskCompletionSource<ApiHttpResponse> completion, CancellationToken cancellation)
        {
            UnityWebRequestAsyncOperation operation;
            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception)
            {
                completion.TrySetException(new ApiException(0, "Không thể kết nối API. Vui lòng kiểm tra kết nối và máy chủ."));
                pending.Remove(request);
                request.Dispose();
                yield break;
            }
            while (!operation.isDone && !cancellation.IsCancellationRequested)
                yield return null;
            if (cancellation.IsCancellationRequested)
            {
                request.Abort();
                completion.TrySetCanceled(cancellation);
            }
            else if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.DataProcessingError)
                completion.TrySetException(new ApiException(0, "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.", "connection_failed"));
            else completion.TrySetResult(new ApiHttpResponse(request.responseCode, request.downloadHandler.text));
            pending.Remove(request);
            request.Dispose();
        }
        void OnDestroy()
        {
            StopAllCoroutines();
            foreach (var item in pending)
            {
                item.Key.Abort();
                item.Value.TrySetCanceled();
                item.Key.Dispose();
            }
            pending.Clear();
        }
    }
}
