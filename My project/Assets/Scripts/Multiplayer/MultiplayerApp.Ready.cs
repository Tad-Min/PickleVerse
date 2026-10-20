#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        ReadyDecision localReady;
        bool readyApproved, resolvingReady, savingReady, tickingReady, initializingReady;
        long readyDeadline;
        public bool ReadyCheckActive => Match != null && State == FlowState.Connecting && !Custom && !readyApproved && !resolvingReady;
        public bool ReadyCheckAccepted => localReady == ReadyDecision.Accepted;
        public bool ReadyCheckBusy => savingReady || resolvingReady;
        public float ReadySeconds => readyDeadline == 0 ? MatchReadyPolicy.Seconds : Mathf.Max(0, (float)((readyDeadline - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond));
        // Confirm the match session first. Gameplay transport is deliberately started later.
        public bool CanAcceptMatch => ReadyCheckActive && ReadySeconds > 0 && !ReadyCheckBusy && !ReadyCheckAccepted;
        public int ReadyCount => ReadyDecisions().Values.Count(d => d == ReadyDecision.Accepted) + (Roster?.members.Count(p => p.bot) ?? 0);
        Dictionary<string, ReadyDecision> ReadyDecisions()
        {
            var result = new Dictionary<string, ReadyDecision>();
            if (Match != null) foreach (var p in Match.Players)
                result[p.Id] = Prop(p, "readyDecision") == "accepted" ? ReadyDecision.Accepted : Prop(p, "readyDecision") == "declined" ? ReadyDecision.Declined : ReadyDecision.Pending;
            if (!string.IsNullOrEmpty(PlayerId)) result[PlayerId] = localReady;
            return result;
        }
        async Task InitializeReadyAsync()
        {
            initializingReady = true;
            try
            {
                localReady = ReadyDecision.Pending; readyDeadline = 0; readyApproved = Custom;
                if (!GetComponent<MatchReadyOverlay>()) gameObject.AddComponent<MatchReadyOverlay>();
                if (Match.IsHost)
                {
                    var host = Match.AsHost();
                    host.SetProperty("readyPhase", new SessionProperty(Custom ? "go" : "waiting", VisibilityPropertyOptions.Member));
                    host.SetProperty("readyDeadline", new SessionProperty("0", VisibilityPropertyOptions.Member));
                    await host.SavePropertiesAsync();
                }
                // A late joining client can already see the host's countdown.
                if(Match!=null && Match.Properties.TryGetValue("readyDeadline",out var deadline))long.TryParse(deadline.Value,out readyDeadline);
            }
            finally { initializingReady = false; }
        }
        public async Task AcceptMatchAsync()
        {
            if (!CanAcceptMatch) return;
            var session = Match; int generation = operation;
            savingReady = true; Changed?.Invoke();
            try
            {
                session.CurrentPlayer.SetProperty("readyDecision", new PlayerProperty("accepted"));
                await session.SaveCurrentPlayerDataAsync();
                if (session == Match && generation == operation) { localReady = ReadyDecision.Accepted; SetNotice("Đã chấp nhận • Chờ mọi người xác nhận…"); }
            }
            catch (Exception e) { SetNotice("Chưa gửi được xác nhận. Hãy thử lại: " + e.Message); }
            finally { savingReady = false; Changed?.Invoke(); }
        }
        public async Task DeclineMatchAsync()
        {
            if (!ReadyCheckActive || savingReady || ReadyCheckAccepted) return;
            localReady = ReadyDecision.Declined; savingReady = true;
            var session = Match;
            try
            {
                session.CurrentPlayer.SetProperty("readyDecision", new PlayerProperty("declined"));
                await Safe(session.SaveCurrentPlayerDataAsync());
                if (session.IsHost) await RejectReadyOnHostAsync(session);
                else await ResolveReadyAsync("Bạn đã hủy tham gia. Bấm Tìm trận để tìm lại.");
            }
            finally { savingReady = false; Changed?.Invoke(); }
        }
        async Task TickReadyAsync(bool allPresent)
        {
            if (initializingReady || tickingReady || resolvingReady || readyApproved || Match == null || Custom) return;
            tickingReady = true; var session = Match;
            try
            {
                if (session.Properties.TryGetValue("readyDeadline", out var deadline)) long.TryParse(deadline.Value, out readyDeadline);
                string phase = session.Properties.TryGetValue("readyPhase", out var p) ? p.Value : "waiting";
                if (phase == "rejected") { await ResolveReadyAsync("Có người chưa sẵn sàng. Đang tìm trận khác…"); return; }
                if (phase == "go") { ApproveReady(); return; }
                if (!session.IsHost)
                {
                    // A missed Lobby event is covered by the existing refresh and disconnect paths.
                    if (readyDeadline > 0 && ReadySeconds <= 0 && localReady == ReadyDecision.Pending) await DeclineMatchAsync();
                    return;
                }
                if (readyDeadline == 0 && allPresent)
                {
                    readyDeadline = DateTime.UtcNow.AddSeconds(MatchReadyPolicy.Seconds).Ticks;
                    session.AsHost().SetProperty("readyDeadline", new SessionProperty(readyDeadline.ToString(), VisibilityPropertyOptions.Member));
                    await session.AsHost().SavePropertiesAsync(); Changed?.Invoke();
                    if (session != Match) return;
                }
                var outcome = MatchReadyPolicy.Evaluate(Roster, ReadyDecisions(), readyDeadline > 0 && ReadySeconds <= 0, allPresent);
                if (outcome == ReadyOutcome.Rejected) await RejectReadyOnHostAsync(session);
                else if (outcome == ReadyOutcome.Approved)
                {
                    session.AsHost().SetProperty("readyPhase", new SessionProperty("go", VisibilityPropertyOptions.Member));
                    await session.AsHost().SavePropertiesAsync();
                    if (session == Match) { ApproveReady(); }
                }
            }
            finally { tickingReady = false; }
        }
        void ApproveReady()
        {if(readyApproved)return;readyApproved=true;connectionDeadline=Time.realtimeSinceStartupAsDouble+45;Changed?.Invoke();}
        async Task RejectReadyOnHostAsync(ISession session)
        {
            session.AsHost().SetProperty("readyPhase", new SessionProperty("rejected", VisibilityPropertyOptions.Member));
            await Safe(session.AsHost().SavePropertiesAsync());
            // Publish the rejection before closing Relay. Peers also recover through Deleted/Disconnected.
            await Task.Delay(1200);
            if (session == Match) await ResolveReadyAsync("Có người chưa sẵn sàng. Đang tìm trận khác…");
        }
        async Task ResolveReadyAsync(string reason)
        {
            if (resolvingReady || readyApproved || Match == null) return;
            resolvingReady = true;
            bool resume = MatchReadyPolicy.ResumeSearch(localReady);
            var mode = Mode;
            try
            {
                if (Roster != null) ignoredMatches.Add(Roster.matchId);
                CancelLocal(); intentionalShutdown = true;
                await LeaveMatchAsync();
                // Each accepting player remains queued; a decliner cannot cancel an accepting teammate.
                // Split the old party because it can contain the decliner. Doubles permits solo tickets.
                SetState(FlowState.WaitingRoom, reason);
                DetachRoom(); // An SDK RemovedFromSession event from our own leave must not cancel the new search.
                await LeaveRoomAsync(); Mode = mode;
                ExitCourt();
                if (resume && !leavingModePage && !quitting)
                { await FindAsync(); SetNotice("Trận chưa đủ xác nhận • Đang tìm đối thủ mới…"); }
                else SetState(FlowState.WaitingRoom,"Đã hủy tham gia. Bấm Tìm trận để tìm lại.");
            }
            finally { resolvingReady = false; localReady = ReadyDecision.Pending; readyDeadline = 0; Changed?.Invoke(); }
        }
        bool RecoverReady(string reason)
        {
            if (resolvingReady) return true;
            if (!ReadyCheckActive) return false;
            _ = Safe(ResolveReadyAsync(reason)); return true;
        }
    }
}

#endif
