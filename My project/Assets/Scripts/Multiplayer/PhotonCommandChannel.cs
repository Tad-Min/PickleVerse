using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>Each peer owns its outbox and acknowledgements. Commands stay replicated until acknowledged.</summary>
    public sealed class PhotonCommandChannel : NetworkBehaviour
    {
        [Networked]
        [Capacity(4096)]
        public string Outbox { get; set; }
        [Networked]
        [Capacity(512)]
        public string Acknowledgements { get; set; }
        [Networked]
        public Vector2 Movement { get; set; }
        [Networked]
        public NetworkBool Running { get; set; }
        [Networked]
        public uint InputSequence { get; set; }
        public static PhotonCommandChannel For(NetworkRunner runner, PlayerRef peer) => channels.FirstOrDefault(c => c && c.Runner == runner && c.Object.IsValid && c.Object.StateAuthority == peer);
        public void Input(uint sequence, Vector2 movement, bool running)
        {
            if (!HasStateAuthority)
                return;
            InputSequence = sequence;
            Movement = movement;
            Running = running;
        }
        static readonly List<PhotonCommandChannel> channels = new List<PhotonCommandChannel>();
        readonly List<Envelope> pending = new List<Envelope>();
        readonly Dictionary<int, int> received = new Dictionary<int, int>();
        [Serializable]
        sealed class Envelope { public int destination, sequence; public PhotonMessage message; }
        int sequence;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            channels.Clear();
        }
        public override void Spawned()
        {
            transform.SetParent(null, true);
            Runner.MakeDontDestroyOnLoad(gameObject);
            channels.Add(this);
            if (HasStateAuthority)
                PhotonSession.Find(Runner)?.BindChannel(this);
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            channels.Remove(this);
        }
        public void Enqueue(PlayerRef destination, PhotonMessage message)
        {
            if (!HasStateAuthority)
                throw new InvalidOperationException("Không sở hữu kênh lệnh.");
            if (pending.Count >= 64)
                throw new InvalidOperationException("Quá nhiều lệnh đang chờ xác nhận.");
            pending.Add(new Envelope { destination = destination.RawEncoded, sequence = ++sequence, message = message });
        }
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
            // Shared-mode proxies do not receive FixedUpdateNetwork. Collect the local
            // player's input on this peer-owned object, then simulate it on the match authority.
            NetworkPlayer.All.FirstOrDefault(p => p && p.IsSpawned && p.Runner == Runner && p.IsLocalHuman)?.CollectLocalInput(Runner.DeltaTime);
            foreach (var destination in pending.Select(e => e.destination).Distinct().ToArray())
            {
                var receiver = channels.FirstOrDefault(c => c && c.Runner == Runner && c.Object.IsValid && c.Object.StateAuthority.RawEncoded == destination);
                var ack = receiver && !string.IsNullOrEmpty(receiver.Acknowledgements) ? JsonConvert.DeserializeObject<Dictionary<int, int>>(receiver.Acknowledgements) : null;
                bool active = Runner.ActivePlayers.Any(p => p.RawEncoded == destination);
                int acknowledged = ack != null && ack.TryGetValue(Runner.LocalPlayer.RawEncoded, out int value) ? value : 0;
                pending.RemoveAll(e => e.destination == destination && (!active || e.sequence <= acknowledged));
            }
            var batch = new List<Envelope>();
            foreach (var item in pending)
            {
                batch.Add(item);
                if (JsonConvert.SerializeObject(batch).Length > 4000)
                {
                    batch.RemoveAt(batch.Count - 1);
                    break;
                }
                if (batch.Count == 8)
                    break;
            }
            string next = JsonConvert.SerializeObject(batch);
            if (Outbox != next)
                Outbox = next;
        }
        public override void Render()
        {
            if (!HasStateAuthority)
                return;
            foreach (var channel in channels.ToArray())
            {
                if (!channel || channel.Runner != Runner || !channel.Object.IsValid || string.IsNullOrEmpty(channel.Outbox))
                    continue;
                List<Envelope> batch;
                try
                {
                    batch = JsonConvert.DeserializeObject<List<Envelope>>(channel.Outbox);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (batch == null)
                    continue;
                int source = channel.Object.StateAuthority.RawEncoded;
                foreach (var message in batch)
                {
                    if (message == null || message.destination != Runner.LocalPlayer.RawEncoded || message.message == null)
                        continue;
                    if (received.TryGetValue(source, out int done) && done >= message.sequence)
                        continue;
                    if (!PhotonSession.Find(Runner).Deliver(channel.Object.StateAuthority, message.message))
                        break;
                    received[source] = message.sequence;
                }
                string ack = JsonConvert.SerializeObject(received);
                if (Acknowledgements != ack)
                    Acknowledgements = ack;
            }
        }
    }
}
