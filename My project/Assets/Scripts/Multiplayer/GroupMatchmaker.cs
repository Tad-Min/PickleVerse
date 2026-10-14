using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Qos;

namespace PickleVerse.Multiplayer
{
    public sealed class ConnectionQualityException : InvalidOperationException
    {
        public ConnectionQualityException(string message) : base(message) { }
    }

    public sealed class MatchmakingAssignmentException : InvalidOperationException
    {
        public MatchmakingAssignmentException(string queue, string serverMessage)
            : base($"Máy chủ từ chối vé ghép trận {queue}: {serverMessage ?? "Không có thông tin lỗi."}") { }
    }

    /// <summary>SDK's session matchmaking accepts one player; this adapter supports a whole duo ticket.</summary>
    public sealed class GroupMatchmaker
    {
        const int MeasurementAttempts = 3;
        const double MaximumPacketLoss = .1;
        // The SDK discovery cache and QoS runner do not support concurrent measurements.
        static readonly SemaphoreSlim MeasurementGate = new SemaphoreSlim(1, 1);
        readonly Func<string, Task<List<QosResult>>> measure;
        readonly Func<int, CancellationToken, Task> delay;
        readonly Action<string> log;
        readonly Action<string> warning;
        readonly Func<string, Task> deleteTicket;
        readonly Func<long> elapsedMilliseconds;
        readonly SemaphoreSlim creationGate=new SemaphoreSlim(1,1);
        readonly SemaphoreSlim deletionGate=new SemaphoreSlim(1,1);
        readonly System.Diagnostics.Stopwatch ticketClock=System.Diagnostics.Stopwatch.StartNew();
        long lastCreationMs=-1050;
        long lastDeletionMs=-1100;

        public GroupMatchmaker() : this(MeasureOnceAsync) { }
        public GroupMatchmaker(Func<string, Task<List<QosResult>>> measure,
            Func<int, CancellationToken, Task> delay = null, Action<string> log = null,
            Func<string, Task> deleteTicket = null, Func<long> elapsedMilliseconds = null, Action<string> warning = null)
        {
            this.measure = measure ?? throw new ArgumentNullException(nameof(measure));
            this.delay = delay ?? Task.Delay;
            this.log = log ?? (message => UnityEngine.Debug.Log(message));
            this.warning = warning ?? (message => UnityEngine.Debug.LogWarning(message));
            this.deleteTicket = deleteTicket ?? (ticket => MatchmakerService.Instance.DeleteTicketAsync(ticket));
            this.elapsedMilliseconds = elapsedMilliseconds ?? (() => ticketClock.ElapsedMilliseconds);
        }

        public async Task<List<QosResult>> MeasureAsync(string queue, int maxLatency, CancellationToken token = default)
        {
            await MeasurementGate.WaitAsync(token);
            try
            {
                List<QosResult> last = null;
                for (int attempt = 1; attempt <= MeasurementAttempts; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    last = await measure(queue) ?? new List<QosResult>();
                    token.ThrowIfCancellationRequested();
                    var accepted = last.Where(q => Reachable(q) && q.Latency.Value <= maxLatency && q.PacketLoss.Value < MaximumPacketLoss)
                        .GroupBy(q => q.RegionId)
                        .Select(g => new QosResult(g.Key, g.Average(q => q.PacketLoss.Value), g.Average(q => q.Latency.Value), g.First().Annotations))
                        .OrderBy(q => q.Latency).ToList();
                    log($"QoS {queue}, lần {attempt}/{MeasurementAttempts}, giới hạn {maxLatency} ms: " +
                        string.Join("; ", last.Select(Describe)) + $". Đạt: {accepted.Count} vùng.");
                    if (accepted.Count > 0) return accepted;
                    if (attempt < MeasurementAttempts) await delay(1000, token);
                }
                throw new ConnectionQualityException(FailureMessage(last, maxLatency));
            }
            finally { MeasurementGate.Release(); }
        }

        static bool Reachable(QosResult q) => q != null && !string.IsNullOrWhiteSpace(q.RegionId) &&
            q.Latency.HasValue && q.Latency.Value >= 0 && q.Latency.Value < int.MaxValue &&
            q.PacketLoss.HasValue && q.PacketLoss.Value >= 0 && q.PacketLoss.Value < 1;

        static string Describe(QosResult q)
        {
            if (!Reachable(q)) return (q?.RegionId ?? "Vùng không xác định") + "=không có phản hồi hợp lệ";
            return q.RegionId + "=" + q.Latency.Value.ToString("0", CultureInfo.InvariantCulture) + " ms, mất gói " +
                (q.PacketLoss.Value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        static string FailureMessage(List<QosResult> results, int maxLatency)
        {
            var best = results.Where(Reachable).OrderBy(q => q.Latency).FirstOrDefault();
            if (best == null)
                return $"Không nhận được phản hồi đo kết nối sau {MeasurementAttempts} lần. Kiểm tra kết nối mạng rồi thử lại.";
            if (best.Latency.Value <= maxLatency)
                return $"Mất gói quá cao sau {MeasurementAttempts} lần đo: {Describe(best)} (yêu cầu mất gói dưới 10%). Hãy thử lại.";
            return $"Không có vùng kết nối đạt giới hạn {maxLatency} ms sau {MeasurementAttempts} lần đo. Vùng gần nhất: {Describe(best)}. Hãy thử lại.";
        }

        static async Task<List<QosResult>> MeasureOnceAsync(string queue)
        {
            var all = await QosService.Instance.GetAllServersAsync();
            var servers = all.Where(s => s.Annotations != null && s.Annotations.MatchmakerQueueName != null && s.Annotations.MatchmakerQueueName.Contains(queue) && s.Annotations.RelayRegionId?.Count > 0).ToList();
            if (servers.Count == 0) servers = all.Where(s => s.Annotations?.RelayRegionId?.Count > 0).ToList();
            if (servers.Count == 0) return new List<QosResult>();
            var measurements = await QosService.Instance.GetQosResultsAsync(servers);
            return measurements.Where(q => q.Item1.Annotations?.RelayRegionId?.Count > 0 && q.Item2 != null)
                .Select(q => new QosResult(q.Item1.Annotations.RelayRegionId[0], q.Item2.PacketLossPercent, q.Item2.AverageLatencyMs,
                    new Dictionary<string, List<string>> { ["MatchmakerPoolId"] = q.Item1.Annotations.MatchmakerPoolId ?? new List<string>() })).ToList();
        }
        public async Task<StoredMatchmakingResults> FindAsync(string queue, List<RoomMember> members, Dictionary<string,List<QosResult>> qos, CancellationToken token, List<string> pairingKeys = null)
        {
            string ticket = null;
            try
            {
                token.ThrowIfCancellationRequested();
                var common=qos[members[0].id].Select(q=>q.RegionId).Where(region=>members.All(m=>qos[m.id].Any(q=>q.RegionId==region)))
                    .OrderBy(region=>members.Max(m=>qos[m.id].First(q=>q.RegionId==region).Latency)).ToList();
                if(common.Count==0) throw new InvalidOperationException("Nhóm không có vùng kết nối chung dưới giới hạn ping.");
                string selectedRegion=common[0];
                var players = members.Select(m => {
                    var data = new Dictionary<string,object> { ["name"]=m.name, ["gender"]=m.gender, ["group"]=m.group, ["region"]=selectedRegion,
                        ["protocol"]=Services.MultiplayerSceneConfiguration.ProtocolVersion.ToString(), ["elo"]=m.elo };
                    if (pairingKeys != null) data["pairingKeys"] = pairingKeys;
                    return new Player(m.id, data, qos[m.id].Where(q=>q.RegionId==selectedRegion).ToList());
                }).ToList();
                // A priority change can split a UTC phase near its start. Keep successive
                // CreateTicket requests within Unity's one-request-per-second allowance.
                await creationGate.WaitAsync(token);
                try
                {
                    int wait=(int)Math.Max(0,lastCreationMs+1050-elapsedMilliseconds());
                    if(wait>0) await delay(wait,token);
                    token.ThrowIfCancellationRequested(); lastCreationMs=elapsedMilliseconds();
                    var response=await MatchmakerService.Instance.CreateTicketAsync(players,new CreateTicketOptions(queue)); ticket=response.Id;
                }
                finally { creationGate.Release(); }
                token.ThrowIfCancellationRequested(); // Handles cancellation while CreateTicketAsync was in flight.
                while (true)
                {
                    await Task.Delay(1000, token);
                    var status = await MatchmakerService.Instance.GetTicketAsync(ticket);
                    token.ThrowIfCancellationRequested();
                    var assignment = status.Value as MatchIdAssignment;
                    if (assignment == null) continue;
                    ValidateAssignment(assignment, queue);
                    if (assignment.Status == MatchIdAssignment.StatusOptions.Found)
                        return await MatchmakerService.Instance.GetMatchmakingResultsAsync(assignment.MatchId);
                }
            }
            finally
            {
                if (ticket != null)
                {
                    await DeleteTicketAsync(ticket);
                }
            }
        }
        public static void ValidateAssignment(MatchIdAssignment assignment, string queue)
        {
            if (assignment == null) return;
            if (assignment.Status == MatchIdAssignment.StatusOptions.Failed)
                throw new MatchmakingAssignmentException(queue, assignment.Message);
            if (assignment.Status == MatchIdAssignment.StatusOptions.Timeout)
                throw new TimeoutException("Chưa tìm được trận phù hợp. Hãy thử lại.");
        }
        // Cleanup must finish even when the search token was cancelled. Unity allows
        // one deletion per second; adjacent search phases can otherwise leave a live ticket.
        public async Task DeleteTicketAsync(string ticket)
        {
            if (string.IsNullOrEmpty(ticket)) return;
            await deletionGate.WaitAsync();
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    int wait = (int)Math.Max(0, lastDeletionMs + 1100 - elapsedMilliseconds());
                    if (wait > 0) await delay(wait, CancellationToken.None);
                    lastDeletionMs = elapsedMilliseconds();
                    try { await deleteTicket(ticket); return; }
                    catch (MatchmakerServiceException e) when (e.Reason == MatchmakerExceptionReason.EntityNotFound || e.Reason == MatchmakerExceptionReason.Gone)
                    { return; }
                    catch (MatchmakerServiceException e) when (e.Reason == MatchmakerExceptionReason.RateLimited && attempt < 3)
                    { await delay(1100 * (1 << attempt), CancellationToken.None); }
                    catch (Exception e) { warning("Ticket cleanup: " + e.Message); return; }
                }
            }
            finally { deletionGate.Release(); }
        }
        public static MatchRoster Roster(string matchId, string region, List<Team> teams, List<Player> players, PlayMode mode)
        {
            var result = new MatchRoster { matchId = matchId, region = region, mode = mode };
            if (teams == null || teams.Count != 2) throw new InvalidOperationException("Kết quả ghép thiếu hai đội.");
            for (int t = 0; t < teams.Count; t++)
                foreach (string id in teams[t].PlayerIds)
                {
                    var player = players.Find(p => p.Id == id);
                    var data = player?.CustomData?.GetAs<TicketPlayerData>() ?? new TicketPlayerData();
                    if (data.protocol != Services.MultiplayerSceneConfiguration.ProtocolVersion.ToString())
                        throw new InvalidOperationException("Ghép phải bản game khác. Cần cập nhật cùng bản multiplayer mới trên mọi máy.");
                    result.members.Add(new RoomMember { id=id, team=t, name=data.name??"Player", gender=data.gender??"Male", group=data.group??id, elo=data.elo });
                }
            if (!result.IsValid()) throw new InvalidOperationException("Kết quả ghép sai số người hoặc đội.");
            if(string.IsNullOrEmpty(result.region) && players.Count>0)
            {
                result.region=players[0].CustomData.GetAs<TicketPlayerData>().region;
            }
            foreach (var group in result.members.GroupBy(m => m.group))
                if (group.Select(p => p.team).Distinct().Count() != 1) throw new InvalidOperationException("Hệ thống ghép đã tách nhóm bạn.");
            return result;
        }
        [Serializable] public sealed class TicketPlayerData
        {
            public string name, gender, group, region, protocol;
            public int elo=100;
            public List<string> pairingKeys;
        }
    }
}
