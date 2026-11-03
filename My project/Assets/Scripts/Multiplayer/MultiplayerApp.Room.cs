#if PICKLEVERSE_LEGACY_NGO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Multiplayer.Services;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        public async Task PlayAsync(string requestedProfile)
        {
            modeNavigation.Open();
            int page=navigationEpoch;
            await InitializeAsync(requestedProfile);
            if(leavingModePage||page!=navigationEpoch)return;
            await LeaveRoomAsync(); Mode = PlayMode.Singles; roomData = new RoomData();
            SetState(FlowState.WaitingRoom, "Phòng 1vs1 — bấm Tìm trận khi sẵn sàng.");
        }
        public async Task SelectModeAsync(PlayMode mode)
        {
            if (State != FlowState.WaitingRoom || Custom || (Room != null && Room.PlayerCount > 1)) throw new InvalidOperationException("Hủy tìm trận hoặc rời nhóm trước khi đổi chế độ.");
            if (mode == Mode) return;
            await LeaveRoomAsync(); Mode = mode; roomData = new RoomData { mode = mode };
            if (mode == PlayMode.Doubles) await CreateRoomAsync(false, mode);
            SetState(FlowState.WaitingRoom, "Phòng " + (mode == PlayMode.Singles ? "1vs1" : "2vs2"));
        }
        public async Task SwitchRoomAsync(PlayMode mode, bool custom)
        {
            if (State != FlowState.WaitingRoom || Searching) throw new InvalidOperationException("Hủy tìm trận trước khi đổi phòng.");
            if (custom && Custom && Mode == mode) return;
            if (!custom && !Custom && Mode == mode && mode == PlayMode.Doubles && Room != null) return;
            // A captain selecting Singles explicitly disbands the party as requested by this UI.
            if (!custom && mode == PlayMode.Singles && Room != null && IsCaptain && !Custom)
            {
                var disbanded=Room; DetachRoom(); Room=null;
                // Deleting the party informs every guest and avoids transferring
                // ownership to a kicked member still present in the SDK cache.
                await disbanded.AsHost().DeleteAsync();
            }
            await LeaveRoomAsync();
            Mode = mode; roomData = new RoomData { mode = mode, custom = custom };
            if (custom || mode == PlayMode.Doubles) await CreateRoomAsync(custom, mode);
            if (custom) await SetTeamAsync(PlayerId, 0);
            SetState(FlowState.WaitingRoom, custom ? "Đã tạo phòng riêng. Chia sẻ mã để mời bạn bè." : "Phòng " + (mode == PlayMode.Singles ? "1vs1" : "2vs2"));
        }
        public async Task UpdateNameAsync(string name)
        {
            if (Searching || State != FlowState.WaitingRoom) throw new InvalidOperationException("Chỉ đổi tên khi đang ở phòng chờ.");
            name = identity.ResolveName(name);
            if (Members().Any(m => m.id != PlayerId && string.Equals(m.name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Tên này đã có trong phòng. Hãy chọn tên khác.");
            DisplayName = name;
            if (Room != null) { Room.CurrentPlayer.SetProperty("name", new PlayerProperty(name)); await Room.SaveCurrentPlayerDataAsync(); }
            Changed?.Invoke();
        }
        Dictionary<string,PlayerProperty> PlayerProperties() => new Dictionary<string,PlayerProperty>
        {
            ["name"] = new PlayerProperty(DisplayName), ["gender"] = new PlayerProperty(Gender),
            ["team"] = new PlayerProperty("-1"), ["ready"] = new PlayerProperty("0"),
            ["cancel"] = new PlayerProperty(""), ["qos"] = new PlayerProperty(""), ["group"] = new PlayerProperty(Room?.Id ?? PlayerId),
            ["protocol"] = new PlayerProperty(MultiplayerSceneConfiguration.ProtocolVersion.ToString()),
            ["elo"] = new PlayerProperty(LocalElo.ToString())
        };
        public async Task CreateCustomAsync(PlayMode mode)
        {
            if (Searching) throw new InvalidOperationException("Hủy tìm trận trước.");
            await LeaveRoomAsync(); Mode = mode; await CreateRoomAsync(true, mode);
            await SetTeamAsync(PlayerId, 0); SetState(FlowState.WaitingRoom, "Đã tạo phòng riêng. Chia sẻ mã để mời bạn bè.");
        }
        async Task CreateRoomAsync(bool custom, PlayMode mode)
        {
            int page=navigationEpoch;
            roomData = new RoomData { custom = custom, mode = mode };
            var options = new SessionOptions { Name = custom ? "PickleVerse Custom" : "PickleVerse Duo", Type = "room", IsPrivate = true,
                MaxPlayers = custom ? 2 * (int)mode : 2, PlayerProperties = PlayerProperties(),
                SessionProperties = new Dictionary<string,SessionProperty> { ["room"] = new SessionProperty(JsonConvert.SerializeObject(roomData), VisibilityPropertyOptions.Member),
                    ["protocol"] = new SessionProperty(MultiplayerSceneConfiguration.ProtocolVersion.ToString(), VisibilityPropertyOptions.Member) } };
            var created=await MultiplayerService.Instance.CreateSessionAsync(options);
            if(leavingModePage||page!=navigationEpoch){await DiscardLateRoom(created);throw new OperationCanceledException("Đã quay lại chọn chế độ.");}
            AttachRoom(created);
        }
        public async Task JoinRoomAsync(string code)
        {
            int page=navigationEpoch;
            if (Searching) throw new InvalidOperationException("Hủy tìm trận trước.");
            MultiplayerSceneConfiguration.Validate();
            await LeaveRoomAsync();
            var joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), new JoinSessionOptions { Type = "room", PlayerProperties = PlayerProperties() });
            if(leavingModePage||page!=navigationEpoch){await DiscardLateRoom(joined);throw new OperationCanceledException("Đã quay lại chọn chế độ.");}
            // Validate before subscribing: a Changed event from an incompatible room
            // must never start network scene synchronization in this build.
            if (!joined.Properties.TryGetValue("protocol", out var protocol) || protocol.Value != MultiplayerSceneConfiguration.ProtocolVersion.ToString())
            { await joined.LeaveAsync(); throw new InvalidOperationException("Phòng dùng bản ván khác. Hãy dùng cùng bản multiplayer mới trên mọi máy."); }
            if (joined.Players.Any(m => m.Id != PlayerId && string.Equals(Prop(m,"name"), DisplayName, StringComparison.OrdinalIgnoreCase)))
            { await joined.LeaveAsync(); throw new InvalidOperationException("Tên này đã có trong phòng. Hãy đổi tên rồi tham gia lại."); }
            var data=joined.Properties.TryGetValue("room",out var property)?JsonConvert.DeserializeObject<RoomData>(property.Value):null;
            // The captain may start as soon as this player joins the SDK session,
            // before JoinSessionByCodeAsync finishes on this machine. An assigned
            // participant must still be allowed to attach and connect to that match.
            bool assignedToMatch = data?.phase == "match" && data.roster?.Find(PlayerId) != null;
            if (data==null || (data.phase != "idle" && !assignedToMatch && (data.custom || data.phase=="match")))
            { await joined.LeaveAsync(); throw new InvalidOperationException("Phòng đang tìm hoặc đang chơi."); }
            AttachRoom(joined); ReadRoom();
            Mode = roomData.mode;
            if(roomData.phase=="idle") SetState(FlowState.WaitingRoom, "Đã tham gia phòng.");
            else SetNotice("Đã vào nhóm; chủ nhóm đang tìm trận.");
        }
        void AttachRoom(ISession session)
        {
            Room = session;
            nextPoll=Time.realtimeSinceStartupAsDouble+4;
            Room.Changed += RoomChanged;
            Room.RemovedFromSession += RemovedFromRoom;
            Room.Deleted += RemovedFromRoom;
            Room.SessionHostChanged += RoomHostChanged;
            Room.PlayerHasLeft += RoomPlayerLeft;
        }
        void DetachRoom()
        {
            if (Room == null) return;
            Room.Changed -= RoomChanged; Room.RemovedFromSession -= RemovedFromRoom; Room.Deleted -= RemovedFromRoom;
            Room.SessionHostChanged -= RoomHostChanged; Room.PlayerHasLeft -= RoomPlayerLeft;
        }
        void RoomChanged()
        {
            if(resolvingReady || ReadyCheckActive)return;
            if (!quitting) ReadRoom();
            if(!quitting && Room!=null && IsCaptain && Searching && !cancelling && Room.Players.Any(p=>Prop(p,"cancel")==observedSearch)) _=Safe(CancelSearchAsync());
            Changed?.Invoke();
        }
        void RemovedFromRoom()
        {
            // A party and its assigned match are separate sessions. Losing the party
            // during ready check must not discard the accepting player's match decision.
            bool keepMatch=Match!=null && (ReadyCheckActive || courtLoading || State==FlowState.InMatch || State==FlowState.Results);
            DetachRoom();Room=null;roomData=new RoomData{mode=Mode,custom=keepMatch && Roster?.custom==true};
            if(keepMatch){Changed?.Invoke();return;}
            CancelLocal();SetState(FlowState.WaitingRoom,"Phòng đã đóng hoặc bạn đã được mời ra.");
        }
        async void RoomHostChanged(string id)
        {
            if(resolvingReady || ReadyCheckActive) return;
            if (Custom) { await Safe(LeaveRoomAsync()); SetState(FlowState.WaitingRoom,"Chủ phòng custom đã rời."); }
            else if (IsCaptain && Searching) await Safe(CancelSearchAsync());
        }
        async void RoomPlayerLeft(string id)
        {
            if(resolvingReady || ReadyCheckActive) return;
            if (IsCaptain && Searching) await Safe(CancelSearchAsync());
            if (Custom && IsCaptain && State==FlowState.WaitingRoom && roomData.teams.Remove(id))
            {
                roomData.teamVersions.Remove(id); roomData.handledTeamRequests.Remove(id);
                await Safe(SaveRoomAsync());
            }
            Changed?.Invoke();
        }
        void ReadRoom()
        {
            if(leavingModePage || resolvingReady || ReadyCheckActive)return;
            if (Room == null || !Room.Properties.TryGetValue("room",out var value)) return;
            var data = JsonConvert.DeserializeObject<RoomData>(value.Value);
            if (data == null) return;
            if (data.revision < roomData.revision || (IsCaptain && data.revision == roomData.revision)) return;
            int previousVersion=roomData.teamVersions.TryGetValue(PlayerId,out var previous)?previous:0;
            roomData = data; Mode = data.mode;
            if(data.custom && data.teamVersions.TryGetValue(PlayerId,out var version) && version!=previousVersion)
                Notice="Đội của bạn đã đổi.";
            if(data.searchId == cancelledSearch && data.phase != "idle") return;
            if (State == FlowState.InMatch || State == FlowState.Reconnecting || State == FlowState.Results) return;
            if (data.phase == "idle")
            {
                if (Searching && !IsCaptain) { CancelLocal(); SetState(FlowState.WaitingRoom, data.notice); }
                return;
            }
            if (data.searchId != observedSearch)
            {
                CancelLocal(); observedSearch = data.searchId;
                double elapsed = Math.Max(0,(DateTime.UtcNow.Ticks - data.startedUtcTicks)/(double)TimeSpan.TicksPerSecond);
                clock.Start(Time.realtimeSinceStartupAsDouble,elapsed);
                searchCancellation = new CancellationTokenSource();
            }
            if (!IsCaptain && data.phase != "match") SetState(clock.CanSubmit(Time.realtimeSinceStartupAsDouble) ? FlowState.Searching : FlowState.Delay,"Chủ nhóm đang tìm trận.");
            if (data.phase == "match" && !ignoredMatches.Contains(data.matchId) && !connecting && publishedMatch != data.matchId && data.roster != null)
            { publishedMatch = data.matchId; _ = Safe(ConnectMatchAsync(data.roster, operation)); }
        }
        public List<RoomMember> Members()
        {
            if (Room == null) return new List<RoomMember> { new RoomMember { id=PlayerId,name=DisplayName,gender=Gender,team=0,group=PlayerId,elo=LocalElo } };
            return Room.Players.Select(p => new RoomMember { id=p.Id,name=Prop(p,"name","Player"),gender=Prop(p,"gender","Male"),
                team=roomData.custom?(roomData.teams.TryGetValue(p.Id,out var t)?t:-1):0, elo=int.TryParse(Prop(p,"elo"),out var rating)?rating:100,
                ready=Prop(p,"ready","0")=="1" && Prop(p,"readyEpoch")==roomData.readyEpoch.ToString() && roomData.teamVersions.TryGetValue(p.Id,out var version) && Prop(p,"readyVersion")==version.ToString(),group=Room.Id }).ToList();
        }
        public static string Prop(IReadOnlyPlayer p,string key,string fallback="") => p.Properties.TryGetValue(key,out var value) ? value.Value : fallback;
        public async Task SetTeamAsync(string id,int team)
        {
            if (!Custom || State != FlowState.WaitingRoom || (id != PlayerId && !IsCaptain)) throw new InvalidOperationException("Không được đổi đội lúc này.");
            if (team < 0 || team > 1) throw new InvalidOperationException("Đội không hợp lệ.");
            var members=Members(); int previousTeam=members.Find(m=>m.id==id)?.team??-1;
            if(previousTeam==team) return;
            bool full=members.Count(p=>p.team==team && p.id!=id)>=(int)Mode;
            if(!IsCaptain)
            {
                Room.CurrentPlayer.SetProperty("teamRequest",new PlayerProperty(team.ToString()));
                Room.CurrentPlayer.SetProperty("teamRequestId",new PlayerProperty(Guid.NewGuid().ToString("N")));
                await Room.SaveCurrentPlayerDataAsync(); SetNotice("Đang yêu cầu đổi đội…"); return;
            }
            if(full)
            {
                var swap=members.First(p=>p.team==team&&p.id!=id);
                roomData.teams[swap.id]=previousTeam;
                roomData.teamVersions[swap.id]=(roomData.teamVersions.TryGetValue(swap.id,out var swappedVersion)?swappedVersion:0)+1;
            }
            roomData.teams[id]=team;
            roomData.teamVersions[id]=(roomData.teamVersions.TryGetValue(id,out var version)?version:0)+1;
            roomData.notice = full ? "Đã hoán đổi đội." : "Đã đổi đội."; await SaveRoomAsync();
            Changed?.Invoke();
        }
        public async Task KickAsync(string id)
        {
            if (!Custom || !IsCaptain || State!=FlowState.WaitingRoom || id==PlayerId) throw new InvalidOperationException("Không được mời người này ra.");
            var currentRoom=Room;
            await currentRoom.AsHost().RemovePlayerAsync(id);
            // RemovePlayerAsync does not update the Sessions player cache. Refresh
            // before enabling Start so a kicked player cannot enter the roster.
            await Task.Delay(1100);
            if(Room!=currentRoom) return;
            await currentRoom.RefreshAsync();
            roomData.teams.Remove(id); roomData.teamVersions.Remove(id); roomData.handledTeamRequests.Remove(id);
            roomData.notice="Đã mời người chơi ra khỏi phòng.";
            await SaveRoomAsync(); SetNotice(roomData.notice);
        }

        public async Task StartCustomAsync()
        {
            if(!Custom || !IsCaptain || State!=FlowState.WaitingRoom) return;
            MultiplayerSceneConfiguration.Validate();
            var members=Members();
            if (members.Any(m => m.team < 0) || members.Count(m => m.team == 0) < 1 || members.Count(m => m.team == 1) < 1 ||
                members.Count(m => m.team == 0) > (int)Mode || members.Count(m => m.team == 1) > (int)Mode)
                throw new InvalidOperationException("Mỗi đội cần ít nhất một người, tối đa hai người. Không cần Sẵn sàng.");
            CancelLocal(); searchCancellation=new CancellationTokenSource(); observedSearch=Guid.NewGuid().ToString("N");
            roomData.roster=new MatchRoster { matchId="custom_"+Guid.NewGuid().ToString("N"),mode=Mode,members=members,custom=true };
            roomData.phase="match"; roomData.matchId=roomData.roster.matchId; roomData.searchId=observedSearch; roomData.startedUtcTicks=DateTime.UtcNow.Ticks;
            // Readiness is consumed when the captain commits this match. Every peer observes
            // the new epoch before connecting, so returning guests cannot reuse stale readiness.
            roomData.readyEpoch++;
            Room.AsHost().IsLocked=true; publishedMatch=roomData.matchId; await SaveRoomAsync(); await ConnectMatchAsync(roomData.roster,operation);
        }

        public async Task LeaveRoomAsync()
        {
            if(Searching) await CancelSearchAsync();
            var old=Room;
            if(old!=null)
            {
                if(old.IsHost && roomData.custom) await old.AsHost().DeleteAsync();
                else { if(old.IsHost && old.PlayerCount>1) { old.AsHost().Host=old.Players.First(p=>p.Id!=PlayerId).Id; await old.AsHost().SavePropertiesAsync(); } await old.LeaveAsync(); }
            }
            if(Room==old){DetachRoom();Room=null;}
            roomData=new RoomData(); measured.Clear();
        }
        static Task DiscardLateRoom(ISession session)=>session.IsHost?session.AsHost().DeleteAsync():session.LeaveAsync();
        readonly ModeNavigationGuard modeNavigation=new ModeNavigationGuard();
        bool leavingModePage=>modeNavigation.Leaving;
        int navigationEpoch=>modeNavigation.Epoch;
        public async Task ReturnToModeSelectionAsync()
        {
            await modeNavigation.ReturnAsync(()=>{cancelledSearch=observedSearch;CancelLocal();},
                ()=>Searching?CancelSearchAsync():Task.CompletedTask,LeaveMatchAsync,LeaveRoomAsync,
                ()=>{OpenDefaultRoomOnLoad=false;SetState(FlowState.Menu,"Đã rời phòng.");SceneManager.LoadScene("ModeSelection");});
        }
    }
}

#endif
