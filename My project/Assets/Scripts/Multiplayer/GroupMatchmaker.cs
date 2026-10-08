using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Qos;

namespace PickleVerse.Multiplayer
{
    /// <summary>SDK's session matchmaking accepts one player; this adapter supports a whole duo ticket.</summary>
    public sealed class GroupMatchmaker
    {
        public async Task<List<QosResult>> MeasureAsync(string queue, int maxLatency)
        {
            var all = await QosService.Instance.GetAllServersAsync();
            var servers = all.Where(s => s.Annotations.MatchmakerQueueName != null && s.Annotations.MatchmakerQueueName.Contains(queue) && s.Annotations.RelayRegionId?.Count > 0).ToList();
            if (servers.Count == 0) servers = all.Where(s => s.Annotations.RelayRegionId?.Count > 0).ToList();
            if (servers.Count == 0) throw new InvalidOperationException("Không lấy được các vùng kết nối.");
            var measurements = await QosService.Instance.GetQosResultsAsync(servers);
            var results = measurements.Where(q => q.Item2.AverageLatencyMs >= 0 && q.Item2.AverageLatencyMs <= maxLatency && q.Item2.PacketLossPercent < .1f)
                .GroupBy(q => q.Item1.Annotations.RelayRegionId[0])
                .Select(g => new QosResult(g.Key, g.Average(q => q.Item2.PacketLossPercent), g.Average(q => q.Item2.AverageLatencyMs),
                    new Dictionary<string, List<string>> { ["MatchmakerPoolId"] = g.First().Item1.Annotations.MatchmakerPoolId ?? new List<string>() }))
                .OrderBy(q => q.Latency).ToList();
            if (results.Count == 0) throw new InvalidOperationException($"Không có vùng kết nối dưới {maxLatency} ms. Hãy thử lại.");
            return results;
        }
        public async Task<StoredMatchmakingResults> FindAsync(string queue, List<RoomMember> members, Dictionary<string,List<QosResult>> qos, CancellationToken token)
        {
            string ticket = null;
            try
            {
                token.ThrowIfCancellationRequested();
                var common=qos[members[0].id].Select(q=>q.RegionId).Where(region=>members.All(m=>qos[m.id].Any(q=>q.RegionId==region)))
                    .OrderBy(region=>members.Max(m=>qos[m.id].First(q=>q.RegionId==region).Latency)).ToList();
                if(common.Count==0) throw new InvalidOperationException("Nhóm không có vùng kết nối chung dưới giới hạn ping.");
                string selectedRegion=common[0];
                var players = members.Select(m => new Player(m.id, new Dictionary<string,object> { ["name"] = m.name, ["gender"] = m.gender, ["group"] = m.group, ["region"]=selectedRegion,
                    ["protocol"]=Services.MultiplayerSceneConfiguration.ProtocolVersion.ToString() }, qos[m.id].Where(q=>q.RegionId==selectedRegion).ToList())).ToList();
                var response = await MatchmakerService.Instance.CreateTicketAsync(players, new CreateTicketOptions(queue));
                ticket = response.Id;
                token.ThrowIfCancellationRequested(); // Handles cancellation while CreateTicketAsync was in flight.
                while (true)
                {
                    await Task.Delay(1000, token);
                    var status = await MatchmakerService.Instance.GetTicketAsync(ticket);
                    token.ThrowIfCancellationRequested();
                    var assignment = status.Value as MatchIdAssignment;
                    if (assignment == null) continue;
                    if (assignment.Status == MatchIdAssignment.StatusOptions.Failed || assignment.Status == MatchIdAssignment.StatusOptions.Timeout)
                        throw new TimeoutException("Chưa tìm được trận phù hợp. Hãy thử lại.");
                    if (assignment.Status == MatchIdAssignment.StatusOptions.Found)
                        return await MatchmakerService.Instance.GetMatchmakingResultsAsync(assignment.MatchId);
                }
            }
            finally
            {
                if (ticket != null)
                {
                    try { await MatchmakerService.Instance.DeleteTicketAsync(ticket); }
                    catch (Exception e) { UnityEngine.Debug.LogWarning("Ticket cleanup: " + e.Message); }
                }
            }
        }
        public static MatchRoster Roster(string matchId, string region, List<Team> teams, List<Player> players, PlayMode mode)
        {
            var result = new MatchRoster { matchId = matchId, region = region, mode = mode };
            if (teams == null || teams.Count != 2) throw new InvalidOperationException("Kết quả ghép thiếu hai đội.");
            for (int t = 0; t < teams.Count; t++)
                foreach (string id in teams[t].PlayerIds)
                {
                    var player = players.Find(p => p.Id == id);
                    var data = player?.CustomData?.GetAs<Dictionary<string,string>>() ?? new Dictionary<string,string>();
                    if (!data.TryGetValue("protocol", out var protocol) || protocol != Services.MultiplayerSceneConfiguration.ProtocolVersion.ToString())
                        throw new InvalidOperationException("Ghép phải bản game khác. Cần cập nhật cùng bản multiplayer mới trên mọi máy.");
                    result.members.Add(new RoomMember { id = id, team = t, name = data.TryGetValue("name", out var name) ? name : "Player", gender = data.TryGetValue("gender", out var gender) ? gender : "Male", group = data.TryGetValue("group", out var group) ? group : id });
                }
            if (!result.IsValid()) throw new InvalidOperationException("Kết quả ghép sai số người hoặc đội.");
            if(string.IsNullOrEmpty(result.region) && players.Count>0)
            {
                var data=players[0].CustomData.GetAs<Dictionary<string,string>>();
                if(data.TryGetValue("region",out var selected)) result.region=selected;
            }
            foreach (var group in result.members.GroupBy(m => m.group))
                if (group.Select(p => p.team).Distinct().Count() != 1) throw new InvalidOperationException("Hệ thống ghép đã tách nhóm bạn.");
            return result;
        }
    }
}
