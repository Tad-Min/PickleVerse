using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PickleVerse.Progression;
using PickleVerse.Replays;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        [Serializable]
        sealed class MatchTicket
        {
            public MatchRoster roster;
            public bool allAccepted, accepted, partyAccepted;
            public string phase, authorityMode = "shared";
            public double readyRemainingSeconds;
        }
        string onlineAuthorityMode = "shared";
        MatchRoster canonicalRoster;
        MatchTicket readyTicket;
        object stagingRequest;
        double serverReadyUntil, nextReadyPoll;
        bool declineRequested, partyConfirmed;
        async Task<MatchTicket> ReadReadyAsync(bool accept)
        {
            var request = stagingRequest;
            int generation = operation;
            string route = accept ? "prepare" : "offer";
            var ticket = await AccountRuntime.Ensure().Account.SendAuthorizedAsync<MatchTicket>("POST", "api/Game/match/online/" + route, request);
            if (generation != operation || stagingRequest != request)
                throw new OperationCanceledException();
            // A status poll already in flight when Accept returned must not undo the decision.
            if (!accept && readyTicket?.accepted == true && ticket.phase == "PREPARING" && !ticket.accepted)
                return readyTicket;
            readyTicket = ticket;
            partyConfirmed |= ticket.partyAccepted;
            canonicalRoster = ticket.roster;
            onlineAuthorityMode = ticket.authorityMode;
            Roster = canonicalRoster;
            serverReadyUntil = Time.realtimeSinceStartupAsDouble + ticket.readyRemainingSeconds;
            return ticket;
        }
        readonly Dictionary<string, Task> resultWrites = new Dictionary<string, Task>();
        bool heartbeatBusy;
        double nextHeartbeat;
        void TickOnlineHeartbeat()
        {
            var runtime = ProgressionRuntime.Instance;
            var session = Match;
            if (heartbeatBusy || !runtime.Ready || session == null || Time.realtimeSinceStartupAsDouble < nextHeartbeat)
                return;
            var pending = runtime.Player.pendingMatch;
            string matchId = session.IsDedicated && State == FlowState.InMatch ? Roster?.matchId
                : pending?.Bot == false ? pending.id : null;
            if (string.IsNullOrEmpty(matchId))
                return;
            heartbeatBusy = true;
            nextHeartbeat = Time.realtimeSinceStartupAsDouble + runtime.Snapshot.catalog.onlineMatch.heartbeatSeconds;
            _ = HeartbeatAsync(session, matchId);
        }
        async Task HeartbeatAsync(PhotonSession session, string matchId)
        {
            int generation = operation;
            var account = AccountRuntime.Ensure().Account;
            int accountSession = account.SessionVersion;
            bool IsCurrent() => this && !quitting && !closing && !reconnecting && Match == session &&
                operation == generation && account.SessionVersion == accountSession;
            try
            {
                var snapshot = await ProgressionRuntime.Instance.CommandAsync("match/online/heartbeat",
                    new { MatchId = matchId, RequestId = Guid.NewGuid().ToString("N") });
                // Photon Relay may still keep the runner alive after the worker's lease has expired.
                if (!IsCurrent() || !session.IsDedicated || State != FlowState.InMatch) return;
                var match = NetworkMatch.Instance;
                if (match && match.Runner == session.Runner && match.Finished) return;
                var interrupted = snapshot.player.history.FirstOrDefault(result =>
                    result.id == matchId && result.outcome == MatchOutcome.Interrupted);
                if (interrupted != null)
                    await ReturnToRoomAsync(string.IsNullOrWhiteSpace(interrupted.reason)
                        ? "Máy chủ thi đấu mất kết nối. Trận đã gián đoạn." : interrupted.reason);
            }
            catch (Exception error)
            {
                if (IsCurrent())
                    SetNotice("Chưa đồng bộ trạng thái trận: " + error.Message);
            }
            finally
            {
                heartbeatBusy = false;
            }
        }
        object OnlineRequest() => new
        {
            SearchId = rankedSearchId,
            Roster = Match.Data.Roster(Match.Runner.SessionInfo.Region),
            Session = Match.Id,
            HostPlayerId = Match.Runner.GetPlayerUserId(Match.Lobby.Object.StateAuthority)
        };
        async Task StartOnlineMatchAsync()
        {
            var request = stagingRequest;
            int generation = operation;
            var ticket = await AccountRuntime.Ensure().Account.SendAuthorizedAsync<MatchTicket>("POST", "api/Game/match/online/start", request);
            if (generation != operation || stagingRequest != request)
                throw new OperationCanceledException();
            readyTicket = ticket;
            canonicalRoster = ticket.roster;
            onlineAuthorityMode = ticket.authorityMode;
            Roster = canonicalRoster;
        }
        async Task ApplyOnlineResultAsync(NetworkMatch match, string json)
        {
            var results = JsonConvert.DeserializeObject<List<OnlineMatchResult>>(json);
            var entry = results?.SingleOrDefault(r => r.onlineId == PlayerId);
            var own = entry?.result;
            if (own == null || own.id != Roster?.matchId)
                throw new InvalidOperationException("Kết quả không thuộc trận hiện tại.");
            own.playerId = PlayerId;
            if (Match?.IsDedicated == true && entry.replayReady.HasValue && ReplayStorage.Repository is OnlineReplayRepository replays)
                replays.RecordFinalization(own.playerId, own.id, entry.replayReady.Value);
            if (!resultWrites.TryGetValue(own.id, out var task) || task.IsFaulted || task.IsCanceled)
            {
                task = SaveOnlineResultAsync(own);
                resultWrites[own.id] = task;
            }
            if (!match.IsServer && own.outcome is MatchOutcome.Won or MatchOutcome.Lost)
                ShowResult(own.id, match.Finished ? match.Status.ToString() : "Trận đã kết thúc.", own.outcome, match);
            await task;
        }
        async Task SaveOnlineResultAsync(MatchResult result)
        {
            try
            {
                if (Match?.IsDedicated == true)
                    await ProgressionRuntime.Instance.ReloadAsync();
                else if (result.localForfeit)
                    await ProgressionRuntime.Instance.CommandAsync("match/online/leave", new { MatchId = result.id, RequestId = Guid.NewGuid().ToString("N") });
                else if (result.scores.All(g => g.completed) && Math.Max(result.gamesWon, result.gamesLost) == 2)
                    await ProgressionRuntime.Instance.CommandAsync("match/online/report", new { Result = result, RequestId = Guid.NewGuid().ToString("N") });
                else await ProgressionRuntime.Instance.ReloadAsync();
                for (int i = 0; i < 10 && ProgressionRuntime.Instance.Player.pendingMatch?.id == result.id; i++)
                {
                    await Task.Delay(1000);
                    await ProgressionRuntime.Instance.ReloadAsync();
                }
            }
            catch (Exception error)
            {
                SetNotice("Chưa lưu kết quả trực tuyến: " + error.Message);
                throw;
            }
        }
        internal void ApplyOnlineProgressionResults(NetworkMatch match, string json)
        {
            if (!string.IsNullOrEmpty(json) && Match?.Runner == match.Runner)
                _ = Safe(ApplyOnlineResultAsync(match, json));
        }

        internal MultiplayerResultScreen ResultScreen { get; private set; }
        MultiplayerCinematic outcome;

        MultiplayerCinematic CaptureOutcomeCourt(NetworkMatch match)
        {
            var court = MultiplayerCourtUI.Instance;
            return match && match.IsSpawned && Match?.Runner == match.Runner && court && court.gameObject.scene == match.gameObject.scene &&
                court.cinematic && court.cinematic.TryCaptureOutcome(match, this) ? court.cinematic : null;
        }

        internal void ShowResult(string matchId, string text, MatchOutcome result, NetworkMatch match = null,
            MultiplayerCinematic retainedCourt = null)
        {
            if (closing || quitting || State == FlowState.Results)
                return;
#if !UNITY_SERVER
            if (result is MatchOutcome.Won or MatchOutcome.Lost)
                outcome = retainedCourt ? retainedCourt : CaptureOutcomeCourt(match);
            var court = outcome ? outcome.GetComponent<MultiplayerCourtUI>() : MultiplayerCourtUI.Instance;
            ResultScreen = MultiplayerResultScreen.Create(this, matchId, court);
            if (outcome)
                outcome.PresentOutcome(result == MatchOutcome.Won, text);
#endif
            SetState(FlowState.Results, text);
#if !UNITY_SERVER
            if (!outcome)
                ResultScreen.Open();
#endif
        }

        internal void CloseOutcome()
        {
            if (outcome)
                outcome.CloseOutcome();
            outcome = null;
        }

        async Task KeepFinishedResultAsync()
        {
            // Fusion can remove the court as soon as the worker has stored the result.
            var screen = ResultScreen;
            if (screen)
            {
                screen.MarkDisconnected();
                if (!outcome || !outcome.isActiveAndEnabled || !outcome.HasOutcome)
                    screen.Open();
                _ = RefreshDisconnectedResultAsync(screen.MatchId, screen);
            }
            await LeaveMatchAsync();
        }

        static MatchResult SavedMatchResult(string matchId) => ProgressionRuntime.Exists
            ? ProgressionRuntime.Instance.Player.history.Find(result => result.id == matchId) : null;

        async Task RefreshDisconnectedResultAsync(string matchId, MultiplayerResultScreen screen = null)
        {
            var account = AccountRuntime.Ensure().Account;
            int version = account.SessionVersion;
            bool IsCurrent() => this && account.IsSignedIn && account.SessionVersion == version &&
                (ReferenceEquals(screen, null) || screen && ResultScreen == screen);
            var runtime = ProgressionRuntime.Instance;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(screen ? 20 : 60));
            try
            {
                while (IsCurrent() && SavedMatchResult(matchId) == null)
                {
                    try
                    {
                        await runtime.ReloadAsync(timeout.Token);
                        if (!screen && runtime.Player.pendingMatch?.id != matchId) return;
                    }
                    catch (Exception error) when (error is ApiException or InvalidOperationException) { }
                    if (SavedMatchResult(matchId) == null)
                        await Task.Delay(screen ? 1500 : 5000, timeout.Token);
                }
            }
            catch (OperationCanceledException) { }
            if (IsCurrent() && screen && SavedMatchResult(matchId) == null)
                screen.ShowSyncFailure();
        }
        async Task ForfeitOnlineMatchAsync()
        {
            if (Match?.IsDedicated == true) return; // RequestLeave goes to the authoritative simulation.
            if (Roster == null || !ProgressionRuntime.Instance.Ready || ProgressionRuntime.Instance.Player.pendingMatch?.id != Roster.matchId)
                return;
            await ProgressionRuntime.Instance.CommandAsync("match/online/leave", new { MatchId = Roster.matchId, RequestId = Guid.NewGuid().ToString("N") });
        }
    }
}
