using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PickleVerse.Progression;
using PickleVerse.Replays;
using PickleVerse.Services;
using PickleVerse.Services.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.FrontEnd
{
    public sealed partial class PlayerProgressionMenu
    {
        enum HistoryReplayState { Missing, Loading, Finishing, Ready, Failed, RecordingFailed }

        sealed class HistoryReplayRow
        {
            public MatchResult Match;
            public Button Watch;
            public TMP_Text Label;
            public bool LoadFailed;
        }

        readonly List<HistoryReplayRow> historyReplayRows = new List<HistoryReplayRow>();
        CancellationTokenSource replayRequests;
        Task replayRefresh = Task.CompletedTask;
        bool replayLoading, replayListFailed;
        double nextReplayCheck;

        void ResetHistoryReplayRequests(bool load)
        {
            replayRequests?.Cancel();
            replayRequests?.Dispose();
            replayRequests = load ? new CancellationTokenSource() : null;
            replayRefresh = Task.CompletedTask;
            replayLoading = load;
            replayListFailed = false;
            nextReplayCheck = 0;
        }

        void BindHistoryReplay(MatchResult match, Button watch, TMP_Text label)
        {
            var row = new HistoryReplayRow { Match = match, Watch = watch, Label = label };
            historyReplayRows.Add(row);
            UpdateHistoryReplayButton(row);
            watch.onClick.AddListener(() =>
            {
                var state = GetHistoryReplayState(row);
                if (state == HistoryReplayState.Ready)
                    PlayHistoryReplay(match);
                else if (replayRequests != null && replayRefresh.IsCompleted &&
                         (state == HistoryReplayState.Failed || state == HistoryReplayState.Finishing || state == HistoryReplayState.RecordingFailed))
                    replayRefresh = RefreshHistoryReplaysAsync(replayListFailed ? null : new[] { row });
            });
        }

        // Older workers and shared matches can publish replay after the score.
        static bool ExpectReplaySoon(MatchResult match) => !match.localForfeit && match.outcome != MatchOutcome.Interrupted
            && match.endedUtcTicks >= DateTime.UtcNow.AddMinutes(-2).Ticks;

        HistoryReplayState GetHistoryReplayState(HistoryReplayRow row)
        {
            if (replayRequests == null || !AccountRuntime.Ensure().Account.IsSignedIn) return HistoryReplayState.Missing;
            try
            {
                var repository = ReplayStorage.Repository;
                var replay = repository.Read(row.Match.playerId, row.Match.id);
                if (replay?.complete == true)
                    return replay.chunks.Count > 0 ? HistoryReplayState.Ready : HistoryReplayState.Missing;
                if (repository is OnlineReplayRepository online && online.FinalizedAvailability(row.Match.playerId, row.Match.id) is bool ready)
                    return ready ? HistoryReplayState.Ready : HistoryReplayState.RecordingFailed;
                if (replayLoading) return HistoryReplayState.Loading;
                if (replayListFailed || row.LoadFailed) return HistoryReplayState.Failed;
                return replay != null || ExpectReplaySoon(row.Match) ? HistoryReplayState.Finishing : HistoryReplayState.Missing;
            }
            catch
            {
                return HistoryReplayState.Failed;
            }
        }

        void UpdateHistoryReplayButton(HistoryReplayRow row)
        {
            var state = GetHistoryReplayState(row);
            row.Watch.interactable = state != HistoryReplayState.Missing && state != HistoryReplayState.Loading;
            row.Label.text = state switch
            {
                HistoryReplayState.Loading => "Đang tải replay…",
                HistoryReplayState.Finishing => ExpectReplaySoon(row.Match) ? "Đang hoàn thiện replay…" : "Replay chưa sẵn sàng · Làm mới",
                HistoryReplayState.Ready => "Xem replay",
                HistoryReplayState.Failed => "Tải replay thất bại · Thử lại",
                HistoryReplayState.RecordingFailed => "Lưu replay thất bại · Kiểm tra lại",
                _ => "Chưa có replay"
            };
        }

        void CheckPendingHistoryReplays()
        {
            if (replayRequests == null || !replayRefresh.IsCompleted || Time.realtimeSinceStartupAsDouble < nextReplayCheck) return;
            foreach (var row in historyReplayRows) UpdateHistoryReplayButton(row);
            var pending = historyReplayRows.Where(row => ExpectReplaySoon(row.Match) &&
                GetHistoryReplayState(row) is HistoryReplayState.Finishing or HistoryReplayState.Failed).ToArray();
            if (pending.Length > 0 && AccountRuntime.Ensure().Account.IsSignedIn)
                replayRefresh = RefreshHistoryReplaysAsync(pending);
        }

        async Task RefreshHistoryReplaysAsync(HistoryReplayRow[] pending = null)
        {
            if (!(ReplayStorage.Repository is OnlineReplayRepository replays)) return;
            var token = replayRequests.Token;
            var account = AccountRuntime.Ensure().Account;
            int session = account.SessionVersion;
            bool Current() => this && !token.IsCancellationRequested && session == account.SessionVersion;

            async Task RefreshRow(HistoryReplayRow row)
            {
                try
                {
                    await replays.ReadAsync(row.Match.playerId, row.Match.id, token);
                    row.LoadFailed = false;
                }
                catch (ApiException error) when (error.StatusCode == 404) { row.LoadFailed = false; }
                catch (OperationCanceledException) { }
                catch (Exception) { row.LoadFailed = true; }
            }

            if (pending == null)
            {
                replayLoading = true;
                foreach (var row in historyReplayRows) UpdateHistoryReplayButton(row);
                try
                {
                    await replays.RefreshAsync(token);
                    if (Current()) replayListFailed = false;
                }
                catch (OperationCanceledException) { }
                catch (Exception) { if (Current()) replayListFailed = true; }
            }
            else await Task.WhenAll(pending.Select(RefreshRow));

            if (!Current()) return;
            replayLoading = false;
            nextReplayCheck = pending == null ? 0 : Time.realtimeSinceStartupAsDouble + 2;
            foreach (var row in historyReplayRows) UpdateHistoryReplayButton(row);
        }
    }
}
