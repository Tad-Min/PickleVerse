using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed class PhotonRoomState : NetworkBehaviour, IPhotonMessageTarget
    {
        public const int PositionRequestArrivalSeconds = 8;
        const int PositionResponseSeconds = 20;

        [Networked]
        [Capacity(4096)]
        public string Payload { get; set; }
        PhotonRoomData initial, cached;
        string parsed, rendered;
        public PhotonRoomData Data
        {
            get
            {
                if (cached == null || parsed != Payload)
                {
                    parsed = Payload;
                    cached = string.IsNullOrEmpty(parsed) ? new PhotonRoomData() : JsonConvert.DeserializeObject<PhotonRoomData>(parsed);
                }
                return cached;
            }
        }
        public void Initialize(PhotonRoomData data)
        {
            initial = data;
        }
        public override void Spawned()
        {
            transform.SetParent(null, true);
            Runner.MakeDontDestroyOnLoad(gameObject);
            if (HasStateAuthority && initial != null)
            {
                if (initial.assignment != null)
                    foreach (var bot in initial.assignment.members.Where(member => member.bot))
                        initial.seats.Add(new PhotonSeat { member = bot, decision = ReadyDecision.Accepted });
                Save(initial);
            }
            PhotonSession.Bind(this);
        }
        public override void Render()
        {
            if (rendered == Payload)
                return;
            rendered = Payload;
            PhotonSession.Find(Runner)?.Notify();
        }
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
            var data = Data;
            if (data.party)
            {
                UpdateParty(data);
                return;
            }
            if (data.authority != Runner.LocalPlayer.RawEncoded)
            {
                data.authority = Runner.LocalPlayer.RawEncoded;
                if (data.phase == "playing" || data.phase == "loading")
                {
                    data.phase = "interrupted";
                    data.notice = "Mất kết nối với máy điều phối trận.";
                }
                Save(data);
            }
            CheckAllConnected();
        }
        void UpdateParty(PhotonRoomData data)
        {
            int authority = Runner.LocalPlayer.RawEncoded;
            bool changedHost = data.authority != authority;
            // Player-left can arrive before this peer owns the room state. Reconcile against
            // Fusion's committed players once the peer can write the shared room.
            int removed = data.seats.RemoveAll(seat => seat.peer != 0 &&
                !Runner.IsPlayerCommitted(PlayerRef.FromEncoded(seat.peer)));
            if (changedHost || removed > 0)
            {
                if (data.custom)
                    InvalidatePositions(data);
                data.authority = authority;
                data.phase = "idle";
                data.matchSession = "";
                data.notice = changedHost ? "Đã chuyển chủ phòng." : "Có người đã rời phòng.";
                Save(data);
            }
            else if (data.positionSwap != null && ReachedTick(data.positionSwap.expiresAtTick))
            {
                ResolvePositionSwap(data, RoomPositionSwapStatus.Expired);
                Save(data);
            }
        }
        public void PlayerGone(PlayerRef player)
        {
            if (!Object || !Object.IsValid || !HasStateAuthority)
                return;
            var data = Data;
            if (data.party)
                return;
            var seat = data.seats.FirstOrDefault(s => s.peer == player.RawEncoded);
            if (seat == null)
                return;
            if (data.phase == "ready" || data.phase == "loading")
            {
                data.phase = "rejected";
                data.notice = "Có người rời trận trước khi vào sân.";
            }
            else if (data.phase == "searching")
                data.seats.RemoveAll(s => s.member.group == seat.member.group);
            else seat.peer = 0;
            Save(data);
        }
        bool Captain(PlayerRef source) => source == Object.StateAuthority;
        void Send(string kind, object data) => PhotonSession.Find(Runner).Send(Object.StateAuthority, Object, kind, data);
        public void Reserve(string json) => Send("reserve", json);
        public void Command(string kind, string value) => Send("command", new[] { kind, value });
        public void RequestPositionSwap(string id, string targetId, int epoch) =>
            Send("position-swap", new PositionRequest { id = id, targetId = targetId, epoch = epoch, validUntilTick = DeadlineTick(PositionRequestArrivalSeconds) });
        public void RespondToPositionSwap(string id, bool accepted) =>
            Send("position-response", new PositionResponse { id = id, accepted = accepted });
        public void Kick(string id) => Send("kick", id);
        public void ExpandSearch(bool expanded)
        {
            if (HasStateAuthority && !Data.party)
                Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { "expanded", expanded } });
        }
        sealed class PositionRequest { public string id, targetId; public int epoch, validUntilTick; }
        sealed class PositionResponse { public string id; public bool accepted; }

        public void ReceivePhotonMessage(PlayerRef source, string kind, string payload)
        {
            if (kind == "reject")
            {
                if (source == Object.StateAuthority)
                    PhotonSession.Find(Runner)?.Reject(Runner.LocalPlayer.RawEncoded, JsonConvert.DeserializeObject<string>(payload));
                return;
            }
            if (!HasStateAuthority)
                return;
            if (Data.party)
                UpdateParty(Data);
            if (kind == "reserve")
                ReserveFrom(JsonConvert.DeserializeObject<string>(payload), source);
            else if (kind == "command")
            {
                var command = JsonConvert.DeserializeObject<string[]>(payload);
                if (command?.Length == 2)
                    CommandFrom(command[0], command[1], source);
            }
            else if (kind == "position-swap")
                RequestPositionSwapFrom(JsonConvert.DeserializeObject<PositionRequest>(payload), source);
            else if (kind == "position-response")
                RespondToPositionSwapFrom(JsonConvert.DeserializeObject<PositionResponse>(payload), source);
            else if (kind == "kick")
                KickFrom(JsonConvert.DeserializeObject<string>(payload), source);
        }
        void Save(PhotonRoomData data)
        {
            data.revision++;
            Payload = JsonConvert.SerializeObject(data);
            cached = data;
            parsed = Payload;
            if (data.party)
            {
                Runner.SessionInfo.IsOpen = data.phase == "idle";
            }
            else
            {
                Runner.SessionInfo.IsVisible = false;
                Runner.SessionInfo.IsOpen = data.phase == "searching" || data.phase == "playing" && data.seats.Any(s => !s.member.bot && s.peer == 0);
                int cap = (int)data.mode;
                Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>{
                    {"fit1", data.seats.Count(s=>s.member.team==0)<cap||data.seats.Count(s=>s.member.team==1)<cap},
                    {"fit2", cap==2&&(data.seats.All(s=>s.member.team!=0)||data.seats.All(s=>s.member.team!=1))},
                    {"stage", data.phase=="searching"?0:1}});
            }
            PhotonSession.Find(Runner)?.Notify();
        }
        void ReserveFrom(string json, PlayerRef source)
        {
            var data = Data;
            if (json == null || json.Length > 4096)
                return;
            var requested = JsonConvert.DeserializeObject<List<RoomMember>>(json);
            string identity = Runner.GetPlayerUserId(source);
            if (requested == null || requested.Count < 1 || requested.Count > (data.party ? 1 : data.custom ? 2 * (int)data.mode : 2)
                || requested.All(m => m.id != identity) || requested.Select(m => m.id).Distinct().Count() != requested.Count
                || requested.Any(m => m.bot || RankedBotAccounts.Contains(m.id) || !Guid.TryParse(m.id, out _) || string.IsNullOrWhiteSpace(m.name) || m.name.Length > 18))
            {
                Reject(source, "Thông tin người chơi không hợp lệ.");
                return;
            }
            if (data.assignment != null)
            {
                if (requested.Any(m => data.assignment.Find(m.id) == null))
                {
                    Reject(source, "Bạn không thuộc đội hình được ghép.");
                    return;
                }
                requested = requested.Select(m => JsonConvert.DeserializeObject<RoomMember>(JsonConvert.SerializeObject(data.assignment.Find(m.id)))).ToList();
            }
            var existing = data.seats.FirstOrDefault(s => s.member.id == identity);
            if (existing != null)
            {
                if (existing.peer != 0 && existing.peer != source.RawEncoded)
                {
                    Reject(source, "Tài khoản đã có trong phòng trên kết nối khác.");
                    return;
                }
                existing.peer = source.RawEncoded;
                Save(data);
                return;
            }
            bool acceptsNewSeats = data.phase == "idle" || data.phase == "searching" && (!data.party || !data.custom);
            if (!acceptsNewSeats)
            {
                Reject(source, "Phòng đã bắt đầu xác nhận trận.");
                return;
            }
            int capacity = Capacity(data);
            if (data.seats.Count + requested.Count > capacity)
            {
                Reject(source, "Phòng đã đủ người.");
                return;
            }
            int team = -1;
            bool preserveTeams = !data.party && (data.custom || data.assignment != null);
            if (preserveTeams && (requested.Any(m => m.team < 0 || m.team > 1) || requested.Count(m => m.team == 0) > (int)data.mode || requested.Count(m => m.team == 1) > (int)data.mode))
            {
                Reject(source, "Đội hình phòng riêng không hợp lệ.");
                return;
            }
            if (!preserveTeams && (!data.party || data.custom))
            {
                int a = data.seats.Count(s => s.member.team == 0), b = data.seats.Count(s => s.member.team == 1), cap = (int)data.mode;
                team = a + requested.Count <= cap && (a <= b || b + requested.Count > cap) ? 0 : b + requested.Count <= cap ? 1 : -1;
                if (team < 0)
                {
                    Reject(source, "Không còn đủ chỗ trong cùng đội cho nhóm.");
                    return;
                }
            }
            string group = requested.First(m => m.id == identity).group;
            if (requested.Any(member => data.seats.Any(s => s.member.id == member.id)))
            {
                Reject(source, "Nhóm có thành viên đã ở trong trận.");
                return;
            }
            foreach (var member in requested)
            {
                if (!preserveTeams)
                {
                    member.group = group;
                    member.team = team;
                }
                member.bot = false;
                member.ready = false;
                data.seats.Add(new PhotonSeat { member = member, peer = member.id == identity ? source.RawEncoded : 0 });
            }
            if (CanStartReadyCheck(data, capacity))
                BeginReady(data);
            Save(data);
        }
        public void CheckAllConnected()
        {
            if (!HasStateAuthority)
                return;
            var data = Data;
            if (CanStartReadyCheck(data, Capacity(data)))
            {
                BeginReady(data);
                Save(data);
            }
        }
        static int Capacity(PhotonRoomData data)
        {
            if (data.party)
                return data.custom ? 2 * (int)data.mode : 2;
            return data.assignment?.Count ?? (data.expectedPlayers > 0 ? data.expectedPlayers : 2 * (int)data.mode);
        }
        static bool CanStartReadyCheck(PhotonRoomData data, int capacity) => !data.party && data.phase == "searching"
            && data.seats.Count == capacity && data.seats.All(seat => seat.member.bot || seat.peer != 0);
        void BeginReady(PhotonRoomData data)
        {
            data.phase = "ready";
            foreach (var seat in data.seats)
                seat.decision = seat.member.bot ? ReadyDecision.Accepted : ReadyDecision.Pending;
        }
        public bool TryBeginRankedFallback()
        {
            if (!HasStateAuthority || Data.party || Data.custom || Data.mode != PlayMode.Singles || Data.phase != "searching" || Data.seats.Count != 1 || Data.seats[0].member.bot || Data.seats[0].peer == 0)
                return false;
            Data.phase = "assigning-bot";
            Save(Data);
            return true;
        }
        public void CompleteRankedFallback(RoomMember bot)
        {
            if (!HasStateAuthority || Data.phase != "assigning-bot" || Data.seats.Count != 1 || !bot.bot || !RankedBotAccounts.Contains(bot.id))
                throw new InvalidOperationException("Phòng không còn nhận đối thủ dự phòng.");
            bot.team = 1 - Data.seats[0].member.team;
            Data.seats.Add(new PhotonSeat { member = bot, peer = 0 });
            BeginReady(Data);
            Save(Data);
        }
        public void ResumeHumanSearch()
        {
            if (HasStateAuthority && Data.phase == "assigning-bot")
            {
                Data.phase = "searching";
                Save(Data);
            }
        }
        void Reject(PlayerRef player, string reason) => PhotonSession.Find(Runner).Send(player, Object, "reject", reason);
        void CommandFrom(string kind, string value, PlayerRef source)
        {
            var data = Data;
            if (!data.seats.Any(s => s.peer == source.RawEncoded))
                return;
            if (kind == "cancel" && data.party && (!data.custom || data.phase == "searching"))
            {
                data.phase = "idle";
                data.matchSession = "";
                data.notice = "Một người đã hủy tìm trận.";
                Save(data);
                return;
            }
            if (!Captain(source))
                return;
            if (kind == "search" && data.party && !data.custom)
            {
                data.phase = "searching";
                data.notice = "";
            }
            else if (kind == "match" && data.party && (!data.custom || data.phase == "searching"))
            {
                data.matchSession = value;
            }
            else if (kind == "idle" && data.party)
            {
                data.phase = "idle";
                data.matchSession = "";
                data.notice = "";
            }
            else if (kind == "load" && !data.party && data.phase == "ready")
                data.phase = "loading";
            else if (kind == "playing" && !data.party && data.phase == "loading")
                data.phase = "playing";
            else return;
            Save(data);
        }
        int DeadlineTick(int seconds) => (int?)TickTimer.CreateFromSeconds(Runner, seconds).TargetTick ?? Runner.Tick.Raw;
        bool ReachedTick(int deadline) => unchecked(Runner.Tick.Raw - deadline) >= 0;

        int CommittedSeat(PhotonRoomData data, string id) => data.seats.FindIndex(seat =>
            seat.member.id == id && seat.peer != 0 && Runner.IsPlayerCommitted(PlayerRef.FromEncoded(seat.peer)));

        void RequestPositionSwapFrom(PositionRequest request, PlayerRef source)
        {
            var data = Data;
            if (!data.party || !data.custom || data.phase != "idle" || request == null ||
                !Guid.TryParseExact(request.id, "N", out _) || request.epoch != data.positionEpoch ||
                ReachedTick(request.validUntilTick))
                return;
            string sender = Runner.GetPlayerUserId(source);
            int from = CommittedSeat(data, sender);
            if (from < 0 || data.seats[from].peer != source.RawEncoded || sender == request.targetId ||
                (data.positionSwap?.id == request.id && data.positionSwap.requesterId == sender) ||
                (data.positionSwapResults?.TryGetValue(sender, out var previous) == true && previous.id == request.id))
                return;
            if (CommittedSeat(data, request.targetId) < 0)
                RecordPositionResult(data, sender, request.id, RoomPositionSwapStatus.Unavailable);
            else if (data.positionSwap != null)
                RecordPositionResult(data, sender, request.id, RoomPositionSwapStatus.Busy);
            else
                data.positionSwap = new RoomPositionSwap
                {
                    id = request.id, requesterId = sender, targetId = request.targetId,
                    expiresAtTick = DeadlineTick(PositionResponseSeconds)
                };
            Save(data);
        }

        void RespondToPositionSwapFrom(PositionResponse response, PlayerRef source)
        {
            var data = Data;
            var offer = data.positionSwap;
            if (offer == null || data.phase != "idle" || response?.id != offer.id)
                return;
            int to = CommittedSeat(data, offer.targetId);
            if (to < 0 || data.seats[to].peer != source.RawEncoded)
                return;
            int from = CommittedSeat(data, offer.requesterId);
            if (from < 0)
                ResolvePositionSwap(data, RoomPositionSwapStatus.Cancelled);
            else if (!response.accepted)
                ResolvePositionSwap(data, RoomPositionSwapStatus.Declined);
            else
            {
                var requester = data.seats[from];
                var target = data.seats[to];
                // Each list index keeps its team. Swapping the entries also exchanges the order within each team.
                (requester.member.team, target.member.team) = (target.member.team, requester.member.team);
                (data.seats[from], data.seats[to]) = (target, requester);
                ResolvePositionSwap(data, RoomPositionSwapStatus.Accepted);
            }
            Save(data);
        }

        static void RecordPositionResult(PhotonRoomData data, string requester, string id, RoomPositionSwapStatus status)
        {
            data.positionSwapResults ??= new Dictionary<string, RoomPositionSwapResult>();
            data.positionSwapResults[requester] = new RoomPositionSwapResult { id = id, status = status };
        }
        static void ResolvePositionSwap(PhotonRoomData data, RoomPositionSwapStatus status)
        {
            var offer = data.positionSwap;
            data.positionSwap = null;
            RecordPositionResult(data, offer.requesterId, offer.id, status);
        }
        static void InvalidatePositions(PhotonRoomData data)
        {
            data.positionEpoch++;
            if (data.positionSwap != null)
                ResolvePositionSwap(data, RoomPositionSwapStatus.Cancelled);
            if (data.positionSwapResults != null)
                foreach (string playerId in data.positionSwapResults.Keys.ToArray())
                    if (data.seats.All(seat => seat.member.id != playerId))
                        data.positionSwapResults.Remove(playerId);
        }

        public List<RoomMember> BeginCustomMatch()
        {
            var data = Data;
            if (!HasStateAuthority || !data.party || !data.custom)
                throw new InvalidOperationException("Chỉ chủ phòng được bắt đầu trận.");
            UpdateParty(data);
            if (data.phase != "idle")
                throw new InvalidOperationException("Phòng đang chuẩn bị trận.");
            if (data.positionSwap != null)
                throw new InvalidOperationException("Hãy chờ yêu cầu đổi vị trí kết thúc.");
            var roster = data.Roster(Runner.SessionInfo.Region);
            if (!roster.IsValid())
                throw new InvalidOperationException("Mỗi đội cần ít nhất một người và không quá sức chứa của chế độ.");
            data.positionEpoch++;
            data.phase = "searching";
            data.notice = "";
            Save(data);
            return roster.members;
        }
        void KickFrom(string player, PlayerRef source)
        {
            var data = Data;
            if (!data.party || !Captain(source) || (data.custom && data.phase != "idle") || player == Runner.GetPlayerUserId(source))
                return;
            var seat = data.seats.FirstOrDefault(s => s.member.id == player);
            if (seat == null)
                return;
            Reject(PlayerRef.FromEncoded(seat.peer), "Bạn đã được mời rời phòng.");
            data.seats.Remove(seat);
            if (data.custom)
                InvalidatePositions(data);
            Save(data);
        }
    }
}
