using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        [Serializable] sealed class DedicatedTicket
        {
            public int authVersion;
            public DedicatedTicketState state;
            public string session, region, connectionToken;
            public MatchRoster roster;
        }
        async Task ConnectDedicatedAsync(string matchId, CancellationToken cancellation = default)
        {
            var account = AccountRuntime.Ensure().Account;
            int expectedOperation = operation;
            int expectedSession = account.SessionVersion;
            bool IsCurrent() => operation == expectedOperation && account.SessionVersion == expectedSession;
            double deadline = Time.realtimeSinceStartupAsDouble + 120;
            DedicatedTicket ticket;
            do
            {
                if (!IsCurrent()) throw new OperationCanceledException();
                ticket = await account.SendAuthorizedAsync<DedicatedTicket>("POST", "api/Game/dedicated/ticket", new { MatchId = matchId }, cancellation);
                if (ticket.state == DedicatedTicketState.Ready) break;
                SetNotice("Đang chuẩn bị máy chủ thi đấu…");
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Máy chủ thi đấu chưa sẵn sàng. Vui lòng tìm lại trận.");
                await Task.Delay(500, cancellation);
            } while (true);
            if (Match != null) await LeaveMatchAsync();
            if (!IsCurrent()) throw new OperationCanceledException();
            cancellation.ThrowIfCancellationRequested();
            var session = PhotonSession.Create("Dedicated Match");
            Roster = canonicalRoster = ticket.roster;
            Mode = Roster.mode;
            session.PrepareDedicated(Roster);
            AttachMatch(session);
            courtLoading = true;
            connectingUntil = Time.realtimeSinceStartupAsDouble + 120;
            SceneLoading.BeginExternal("MultiplayerCourt");
            try
            {
                await session.JoinDedicatedAsync(ticket.session, ticket.region, PlayerId, ticket.connectionToken, ticket.authVersion, Roster, cancellation);
                if (!IsCurrent() || Match != session) throw new OperationCanceledException();
                if (reconnecting)
                    await PhotonSession.WaitAsync(() => !IsCurrent() || Match != session || !session.Runner || !session.Runner.IsRunning ||
                        NetworkPlayer.All.Any(player => player && player.Runner == session.Runner && player.IsLocalHuman),
                        "Chưa nhận lại quyền điều khiển.", cancellation: cancellation);
                if (!IsCurrent()) throw new OperationCanceledException();
                if (Match != session || !session.Runner || !session.Runner.IsRunning)
                    throw new InvalidOperationException("Mất kết nối máy chủ thi đấu.");
            }
            catch
            {
                if (Match == session) await LeaveMatchAsync();
                throw;
            }
            Debug.Log($"[Dedicated] Client match={matchId} connected via {session.Runner.CurrentConnectionType}");
        }

#if !UNITY_SERVER
        public async Task ResumeDedicatedMatchAsync(string matchId)
        {
            if (reconnecting || Match != null || Room != null || State != FlowState.Menu) return;
            reconnecting = true;
            int generation = operation;
            try
            {
                await RejoinDedicatedAsync(matchId, generation, fromMenu: true);
            }
            finally
            {
                reconnecting = false;
            }
        }
#endif

        async Task RejoinDedicatedAsync(string matchId, int generation, bool fromMenu)
        {
            var account = AccountRuntime.Ensure().Account;
            int version = account.SessionVersion;
            bool IsCurrent() => this && !quitting && operation == generation && account.IsSignedIn && account.SessionVersion == version;
            if (!IsCurrent()) return;
            SetState(FlowState.Reconnecting, "Đang kết nối lại trận đang dở…");
            try
            {
                await InitializeAsync(PlayerId);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.reconnectSeconds));
                while (IsCurrent() && !timeout.IsCancellationRequested)
                {
                    if (fromMenu && SceneManager.GetActiveScene().name == "Menu")
                    {
                        SceneLoading.BeginExternal("MultiplayerCourt");
                        SceneLoading.ExternalProgress(.05f, Notice);
                    }
                    try
                    {
                        await ConnectDedicatedAsync(matchId, cancellation: timeout.Token);
                        return;
                    }
                    catch (ApiException error) when (error.StatusCode is 400 or 401 or 403 or 404 or 409 or 410)
                    {
                        break;
                    }
                    catch (Exception error) when (error is ApiException or InvalidOperationException or TimeoutException)
                    {
                        await Task.Delay(1000, timeout.Token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogWarning("[Dedicated] Could not resume match: " + error.GetType().Name);
            }
            if (!IsCurrent()) return;

            // The worker may have ended before the last Photon snapshot reached this client.
            if (!fromMenu)
            {
                if (SavedMatchResult(matchId) == null)
                {
                    using var resultTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await ProgressionRuntime.Instance.ReloadAsync(resultTimeout.Token); }
                    catch (Exception error) when (error is ApiException or InvalidOperationException or OperationCanceledException) { }
                }
                if (!IsCurrent()) return;
                if (SavedMatchResult(matchId)?.outcome is MatchOutcome.Won or MatchOutcome.Lost)
                {
                    SceneLoading.CancelExternal();
                    ShowResult(matchId, "Trận đã kết thúc.");
                    if (ResultScreen) ResultScreen.Open();
                    return;
                }
            }

            const string message = "Không thể kết nối lại trận. Kết quả sẽ được cập nhật khi máy chủ xử lý xong.";
            if (fromMenu && SceneManager.GetActiveScene().name == "Menu")
            {
                SceneLoading.CancelExternal();
                SetState(FlowState.Menu, message);
            }
            else await ReturnToRoomAsync(message);
            _ = RefreshDisconnectedResultAsync(matchId);
        }
        public void AttachDedicatedServer(PhotonSession session, MatchRoster roster)
        {
            clientIdentities.Clear();
            Roster = canonicalRoster = roster;
            Match = session;
            session.PeerLeft += (runner, peer) =>
            {
                if (Match != session) return;
                NetworkMatch.Instance?.ReserveDisconnected((ulong)peer.RawEncoded);
                RemoveClientIdentity((ulong)peer.RawEncoded);
            };
        }
        public void RegisterDedicatedPeer(Fusion.PlayerRef peer, string player, DedicatedPlayerSessions.Connection previous = null)
        {
            if (Match?.Runner == null || !Match.Runner.IsServer)
                throw new InvalidOperationException("Only the dedicated server can admit a player.");
            if (previous != null)
            {
                // Reserve before assigning the new peer. A late disconnect cannot pause the replacement.
                NetworkMatch.Instance?.ReserveDisconnected(previous.Peer);
                RemoveClientIdentity(previous.Peer);
                Match.Runner.Disconnect(Fusion.PlayerRef.FromEncoded((int)previous.Peer));
            }
            clientIdentities[(ulong)peer.RawEncoded] = player;
            PeerConnected?.Invoke((ulong)peer.RawEncoded);
        }
    }
}
