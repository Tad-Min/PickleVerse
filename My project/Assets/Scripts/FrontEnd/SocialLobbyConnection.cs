#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PickleVerse.Services;

namespace PickleVerse.FrontEnd
{
    // SignalR JSON over WebSocket. All senders share one gate and the receive loop owns the frame buffer.
    internal sealed class SocialLobbyConnection : IDisposable
    {
        const char RecordSeparator = '\u001e';
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly CancellationToken stop;
        readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        readonly Dictionary<string, TaskCompletionSource<bool>> pending = new Dictionary<string, TaskCompletionSource<bool>>();
        ClientWebSocket socket;
        bool ready;
        public event Action<string, JArray> Received;
        public event Action Connected;
        public bool Ready => ready && socket?.State == WebSocketState.Open;

        public SocialLobbyConnection()
        {
            stop = lifetime.Token;
        }
        public async Task Run()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var connect = CancellationTokenSource.CreateLinkedTokenSource(stop);
                    connect.CancelAfter(TimeSpan.FromSeconds(10));
                    using var connection = await SocialTransport.ConnectLobby(connect.Token);
                    socket = connection;
                    await Send("{\"protocol\":\"json\",\"version\":1}", connect.Token);
                    await ReceiveLoop(connection);
                }
                catch (Exception) when (!stop.IsCancellationRequested) { }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
                finally
                {
                    ready = false;
                    socket = null;
                    foreach (var call in pending.Values)
                        call.TrySetException(new InvalidOperationException("Mất kết nối với sảnh."));
                    pending.Clear();
                }
                if (!stop.IsCancellationRequested)
                {
                    try { await Task.Delay(3000, stop); }
                    catch (OperationCanceledException) { }
                }
            }
        }
        async Task ReceiveLoop(ClientWebSocket connection)
        {
            var bytes = new byte[4096];
            var frame = new List<byte>(4096);
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(stop);
            idle.CancelAfter(TimeSpan.FromSeconds(20));
            while (connection.State == WebSocketState.Open)
            {
                var part = await connection.ReceiveAsync(new ArraySegment<byte>(bytes), idle.Token);
                if (part.MessageType == WebSocketMessageType.Close) return;
                idle.CancelAfter(TimeSpan.FromSeconds(40));
                for (int i = 0; i < part.Count; i++)
                {
                    if (bytes[i] != RecordSeparator)
                    {
                        frame.Add(bytes[i]);
                        if (frame.Count > 16384) throw new InvalidOperationException("Phản hồi sảnh vượt giới hạn.");
                        continue;
                    }
                    var message = JObject.Parse(Encoding.UTF8.GetString(frame.ToArray()));
                    frame.Clear();
                    if (!ready)
                    {
                        if (message["error"] != null) return;
                        ready = true;
                        Connected?.Invoke();
                        _ = PublishPresence(connection);
                    }
                    else if ((int?)message["type"] == 1)
                        Received?.Invoke((string)message["target"], message["arguments"] as JArray);
                    else if ((int?)message["type"] == 3 && pending.TryGetValue((string)message["invocationId"] ?? "", out var call))
                    {
                        var error = (string)message["error"];
                        if (error == null) call.TrySetResult(true);
                        else call.TrySetException(new InvalidOperationException(error));
                    }
                    else if ((int?)message["type"] == 7)
                        return;
                }
            }
        }
        async Task PublishPresence(ClientWebSocket connection)
        {
            try
            {
                while (!stop.IsCancellationRequested && connection == socket && Ready)
                {
                    await Invoke("Presence");
                    await Task.Delay(12000, stop);
                }
            }
            catch { if (connection == socket) connection.Abort(); }
        }
        public async Task Invoke(string target, params object[] arguments)
        {
            if (!Ready) throw new InvalidOperationException("Đang kết nối dịch vụ thì thầm.");
            string id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(id, completion);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var registration = timeout.Token.Register(() => completion.TrySetCanceled());
            try
            {
                await Send(JsonConvert.SerializeObject(new { type = 1, invocationId = id, target, arguments }), timeout.Token);
                await completion.Task;
            }
            finally { pending.Remove(id); }
        }
        async Task Send(string json, CancellationToken cancellation)
        {
            var connection = socket;
            await sendLock.WaitAsync(cancellation);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json + RecordSeparator);
                await connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation);
            }
            finally { sendLock.Release(); }
        }
        public void Dispose()
        {
            lifetime.Cancel();
            socket?.Abort();
            lifetime.Dispose();
        }
    }
}
#endif
