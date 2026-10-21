#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace PickleVerse.Multiplayer
{
    // Public MPS INetworkHandler integration keeps the metadata session alive during a
    // transport restart. A new Relay allocation is required after a dropped connection.
    internal sealed class SessionNetworkHandler : INetworkHandler
    {
        readonly NetworkManager network;
        readonly Func<bool> valid;
        static SessionNetworkHandler active;
        Task starting;
        int epoch;
        public SessionNetworkHandler(NetworkManager manager,Func<bool> current) { network=manager; valid=current; }
        public Task StartAsync(NetworkConfiguration configuration)
        {
            if(starting!=null&&!starting.IsCompleted) return starting;
            if(!valid()) return Task.CompletedTask;
            if(active==this&&network.IsListening&&(network.IsHost||network.IsConnectedClient)) return Task.CompletedTask;
            return starting=StartOnce(configuration,epoch);
        }
        async Task StartOnce(NetworkConfiguration configuration,int generation)
        {
            if(configuration.Type!=NetworkType.Relay) throw new InvalidOperationException("PickleVerse requires Relay.");
            while(network.ShutdownInProgress) await Task.Delay(50);
            if(generation!=epoch||!valid()) return;
            active=this;
            network.GetComponent<UnityTransport>().SetRelayServerData(configuration.RelayServerData);
            bool host=configuration.Role!=NetworkRole.Client;
            if(!(host?network.StartHost():network.StartClient())) throw new InvalidOperationException("Không khởi động được kết nối gameplay.");
            if(host) return;
            double deadline=Time.realtimeSinceStartupAsDouble+30;
            while(valid()&&network.IsListening&&!network.IsConnectedClient&&Time.realtimeSinceStartupAsDouble<deadline) await Task.Delay(50);
            if(generation!=epoch||!valid()) return;
            if(!network.IsConnectedClient) throw new TimeoutException("Không kết nối được host.");
        }
        public async Task StopAsync()
        {
            epoch++;
            if(active!=this) return;
            if(network.IsListening&&!network.ShutdownInProgress) network.Shutdown();
            while(network.ShutdownInProgress) await Task.Delay(50);
            if(active==this) active=null;
        }
        public async Task ResumeAsync(ISession session,Func<bool> stillActive)
        {
            // MPS 2.3.3 publishes its Relay join code in session network metadata.
            // Keep this adapter in one place when upgrading the pinned SDK version.
            if(!session.Properties.TryGetValue("_session_network",out var metadata)) throw new InvalidOperationException("Thiếu thông tin nối lại trận.");
            string code=JObject.Parse(metadata.Value).Value<string>("RelayJoinCode");
            if(string.IsNullOrEmpty(code)) throw new InvalidOperationException("Thiếu mã kết nối Relay.");
            var allocation=await RelayService.Instance.JoinAllocationAsync(code);
            if(!stillActive()) throw new OperationCanceledException();
            await StopAsync();
            if(!stillActive()) throw new OperationCanceledException();
            network.GetComponent<UnityTransport>().SetRelayServerData(allocation.ToRelayServerData("dtls"));
            active=this;
            if(!network.StartClient()) throw new InvalidOperationException("Không khởi động được kết nối lại.");
        }
    }
}
#endif
