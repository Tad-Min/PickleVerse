using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public struct DedicatedInput : INetworkInput
    {
        public Vector2 Movement;
        public NetworkBool Running;
        public uint Sequence;
    }
    public sealed partial class PhotonSession
    {
        PhotonRoomData dedicatedData;
        int reliableSequence;
        public bool IsDedicated => dedicatedData != null;
        public async Task StartDedicatedAsync(string session, string region, MatchRoster roster, ushort port, Func<byte[], bool> admission)
        {
            PrepareDedicated(roster);
            Callbacks.Admission = admission;
            var scenes = new NetworkSceneInfo();
            scenes.AddSceneRef(SceneRef.FromPath(Services.MultiplayerSceneConfiguration.CourtPath), LoadSceneMode.Single);
            var result = await Runner.StartGame(new StartGameArgs { GameMode = GameMode.Server,
                SessionName = session, PlayerCount = roster.HumanCount, IsVisible = false, Address = NetAddress.Any(port),
                Config = ConfigForSession(true),
                CustomPhotonAppSettings = Settings(region), SceneManager = Runner.gameObject.AddComponent<PhotonSceneManager>(), Scene = scenes });
            if (!result.Ok) throw new InvalidOperationException("Không khởi động được máy chủ Fusion: " + result.ShutdownReason);
            sessionName = session; sessionRegion = region;
        }
        public async Task JoinDedicatedAsync(string session, string region, string player, string token, MatchRoster roster)
        {
            PrepareDedicated(roster); Runner.ProvideInput = true;
            Callbacks.Input = (runner, input) => input.Set(NetworkPlayer.All.FirstOrDefault(p => p && p.Runner == runner && p.IsLocalHuman)?.CaptureDedicatedInput() ?? default);
            var result = await Runner.StartGame(new StartGameArgs { GameMode = GameMode.Client, SessionName = session,
                EnableClientSessionCreation = false, AuthValues = new Photon.Realtime.AuthenticationValues(player),
                ConnectionToken = Convert.FromBase64String(token), CustomPhotonAppSettings = Settings(region),
                Config = ConfigForSession(true),
                SceneManager = Runner.gameObject.AddComponent<PhotonSceneManager>() });
            if (!result.Ok) throw new InvalidOperationException("Không kết nối được máy chủ thi đấu. Vui lòng thử lại.");
            sessionName = session; sessionRegion = region;
        }
        public void PrepareDedicated(MatchRoster roster)
        {
            dedicatedData = new PhotonRoomData { phase = "loading", matchId = roster.matchId, mode = roster.mode,
                custom = roster.custom, night = roster.night, venue = roster.venue, assignment = roster,
                seats = roster.members.Select(m => new PhotonSeat { member = m, decision = ReadyDecision.Accepted }).ToList() };
            Callbacks.Reliable = (source, bytes) =>
            {
                try { var message = JsonConvert.DeserializeObject<PhotonMessage>(Encoding.UTF8.GetString(bytes)); if (message != null) Deliver(source, message); }
                catch (JsonException) { }
            };
        }
        void SendDedicated(PlayerRef destination, PhotonMessage message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            if (bytes.Length > 16384) throw new InvalidOperationException("Lệnh mạng vượt giới hạn.");
            if (Runner.IsServer && destination == PlayerRef.None) { Deliver(PlayerRef.None, message); return; }
            var key = ReliableKey.FromInts(0x504b56, ++reliableSequence);
            if (Runner.IsServer) Runner.SendReliableDataToPlayer(destination, key, bytes);
            else Runner.SendReliableDataToServer(key, bytes);
        }
        public void MarkDedicatedPlaying() { if (dedicatedData != null) dedicatedData.phase = "playing"; }
        public static NetworkProjectConfig ConfigForSession(bool dedicated)
        {
            var config = NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            config.PrefabTable = NetworkProjectConfig.Global.PrefabTable;
            if (dedicated)
            {
                config.Simulation.InputDataWordCount = Math.Max(config.Simulation.InputDataWordCount, 8);
                config.InvokeRenderMode = NetworkProjectConfig.InvokeRenderModes.Always;
            }
            return config;
        }
    }
}
