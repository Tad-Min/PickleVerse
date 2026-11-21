using System;
using System.Linq;
using UnityEngine;
using PickleVerse.LocalMatch;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        public object LiveStatus()
        {
            if (!IsSpawned || !IsServer) return null;
            double now = Time.realtimeSinceStartupAsDouble;
            return new
            {
                scoreA = ScoreA, scoreB = ScoreB, gamesA = GamesA, gamesB = GamesB,
                paused = Paused, started = Started, finished = Finished,
                players = Roster.members.Select(m => new
                {
                    id = m.id, connected = m.bot || players.TryGetValue(m.id, out var p) && p && p.Connected,
                    reconnectUntil = reservations.TryGetValue(m.id, out var until) ? (DateTime?)DateTime.UtcNow.AddSeconds(Math.Max(0, until - now)) : null
                }).ToArray()
            };
        }
        public void InterruptFromServer(string reason)
        {
            if (!IsSpawned || !IsServer) return;
            Body.isKinematic = true; ballVelocity = Vector3.zero; Paused = true;
            WinningTeam = -1;
            Status = "Trận đấu bị gián đoạn. Không tính Elo hoặc thưởng.\n" + (reason == null ? "" : reason.Substring(0,Math.Min(120,reason.Length)));
            Finished = true;
            replay?.Event("Interrupted", reason ?? "");
            replay?.Finish(false);
            AnnouncementRpc("TRẬN ĐẤU BỊ GIÁN ĐOẠN", "Không tính Elo hoặc thưởng", (int)AnnouncementTone.Fault, 3f);
        }
    }
}
