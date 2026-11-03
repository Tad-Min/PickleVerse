using System;
using System.Threading.Tasks;
using PickleVerse.Services;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        [Serializable] sealed class DedicatedTicket
        {
            public string state, session, region, connectionToken;
            public MatchRoster roster;
        }
        async Task ConnectDedicatedAsync(MatchRoster roster)
        {
            int expectedOperation = operation;
            double deadline = Time.realtimeSinceStartupAsDouble + (reconnecting ? Settings.reconnectSeconds : 120);
            DedicatedTicket ticket;
            do
            {
                if (operation != expectedOperation) throw new OperationCanceledException();
                ticket = await AccountRuntime.Ensure().Account.SendAuthorizedAsync<DedicatedTicket>("POST", "api/Game/dedicated/ticket", new { MatchId = roster.matchId });
                if (ticket.state == "ready") break;
                if (ticket.state == "busy" && !roster.custom && !reconnecting)
                    deadline = Time.realtimeSinceStartupAsDouble + 120;
                SetNotice(ticket.state == "busy" ? "Máy chủ đang bận • Đang chờ lượt thi đấu…" : "Đang chuẩn bị máy chủ thi đấu…");
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Máy chủ thi đấu chưa sẵn sàng. Vui lòng tìm lại trận.");
                await Task.Delay(500);
            } while (true);
            await LeaveMatchAsync();
            if (operation != expectedOperation) throw new OperationCanceledException();
            var session = PhotonSession.Create("Dedicated Match");
            Roster = canonicalRoster = ticket.roster;
            session.PrepareDedicated(Roster);
            AttachMatch(session);
            courtLoading = true; CourtSceneSynchronized = false;
            PickleVerse.Settings.SceneLoading.BeginExternal("MultiplayerCourt");
            try { await session.JoinDedicatedAsync(ticket.session, ticket.region, PlayerId, ticket.connectionToken, Roster); }
            catch { await LeaveMatchAsync(); throw; }
            Debug.Log("[Dedicated] Client connected via " + session.Runner.CurrentConnectionType);
        }
#if UNITY_SERVER
        public void AttachDedicatedServer(PhotonSession session, MatchRoster roster)
        {
            ClientIdentities.Clear();
            Roster = canonicalRoster = roster;
            Match = session;
            session.PeerLeft += (runner, peer) =>
            {
                NetworkMatch.Instance?.ReserveDisconnected((ulong)peer.RawEncoded);
                ClientIdentities.Remove((ulong)peer.RawEncoded);
            };
            CourtSceneSynchronized = true;
        }
        public void RegisterDedicatedPeer(Fusion.PlayerRef peer, string player)
        {
            ClientIdentities[(ulong)peer.RawEncoded] = player;
            PeerConnected?.Invoke((ulong)peer.RawEncoded);
        }
#endif
    }
}
