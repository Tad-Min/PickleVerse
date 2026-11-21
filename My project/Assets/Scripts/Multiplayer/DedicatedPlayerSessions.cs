#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Multiplayer
{
    // Used on the server's main thread. Every callback is fenced by both login epoch and peer.
    public sealed class DedicatedPlayerSessions
    {
        public sealed class Connection
        {
            public readonly string Player;
            public readonly int AuthVersion;
            public readonly ulong Peer;
            public Connection(string player, int authVersion, ulong peer)
            { Player = player; AuthVersion = authVersion; Peer = peer; }
        }
        readonly Dictionary<string, Connection> players = new Dictionary<string, Connection>();
        public Connection Find(string player) => players.TryGetValue(player, out var value) ? value : null;
        public Connection[] Snapshot() => players.Values.ToArray();
        public object CreateHeartbeat(string leaseId, object live) => new
        {
            LeaseId = leaseId,
            Live = live,
            // JSON GUIDs use the D form expected by the API, while roster IDs stay N.
            Sessions = players.Values.Select(p => new
            {
                PlayerId = Guid.ParseExact(p.Player, "N"),
                AuthVersion = p.AuthVersion
            }).ToArray()
        };
        public bool TryAdmit(string player, int authVersion, ulong peer, out Connection previous)
        {
            previous = Find(player);
            if (authVersion < 0 || (previous != null && previous.AuthVersion >= authVersion)) return false;
            players[player] = new Connection(player, authVersion, peer);
            return true;
        }
        public void RemovePeer(ulong peer)
        {
            var current = players.Values.FirstOrDefault(p => p.Peer == peer);
            if (current != null) players.Remove(current.Player);
        }
        public Connection TakeExpired(string player, int authVersion)
        {
            var current = Find(player);
            if (current == null || current.AuthVersion != authVersion) return null;
            players.Remove(player);
            return current;
        }
        public void Clear() => players.Clear();
    }
}
