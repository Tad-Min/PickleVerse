using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed class PhotonRoomState : NetworkBehaviour,IPhotonMessageTarget
    {
        [Networked,Capacity(4096)] public string Payload {get;set;}
        PhotonRoomData initial,cached;
        string parsed,rendered;
        public PhotonRoomData Data
        {
            get{if(cached==null||parsed!=Payload){parsed=Payload;cached=string.IsNullOrEmpty(parsed)?new PhotonRoomData():JsonConvert.DeserializeObject<PhotonRoomData>(parsed);}return cached;}
        }
        public void Initialize(PhotonRoomData data){initial=data;}
        public override void Spawned()
        {
            transform.SetParent(null,true);
            Runner.MakeDontDestroyOnLoad(gameObject);
            if(HasStateAuthority&&initial!=null)Save(initial);
            PhotonSession.Bind(this);
        }
        public override void Render()
        {
            if(rendered==Payload)return;rendered=Payload;PhotonSession.Find(Runner)?.Notify();
        }
        public override void FixedUpdateNetwork()
        {
            if(!HasStateAuthority)return;
            var data=Data;
            if(data.authority!=Runner.LocalPlayer.RawEncoded)
            {
                data.authority=Runner.LocalPlayer.RawEncoded;
                if(data.phase=="playing"||data.phase=="loading"){data.phase="interrupted";data.notice="Mất kết nối với máy điều phối trận.";}
                Save(data);
            }
            if(!data.party&&data.phase=="ready"&&Runner.SimulationTime>=data.deadline)
            {data.phase="rejected";data.notice="Có người chưa xác nhận tham gia.";Save(data);}
            CheckAllConnected();
        }
        public void PlayerGone(PlayerRef player)
        {
            if(!Object||!Object.IsValid||!HasStateAuthority)return;
            var data=Data;var seat=data.seats.FirstOrDefault(s=>s.peer==player.RawEncoded);if(seat==null)return;
            if(data.party){data.seats.Remove(seat);data.matchSession="";data.phase="idle";}
            else if(data.phase=="ready"||data.phase=="loading")
            {data.phase="rejected";data.notice="Có người rời trận trước khi vào sân.";}
            else if(data.phase=="searching")data.seats.RemoveAll(s=>s.member.group==seat.member.group);
            else seat.peer=0;
            Save(data);
        }
        bool Captain(PlayerRef source)=>source==Object.StateAuthority;
        void Send(string kind,object data)=>PhotonSession.Find(Runner).Send(Object.StateAuthority,Object,kind,data);
        public void Reserve(string json)=>Send("reserve",json);
        public void Ready(bool accept)=>Send("ready",accept);
        public void Command(string kind,string value)=>Send("command",new[]{kind,value});
        public void Team(string id,int team)=>Send("team",new TeamCommand{id=id,team=team});
        public void Kick(string id)=>Send("kick",id);
        public void ExpandSearch(bool expanded)
        {if(HasStateAuthority&&!Data.party)Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string,SessionProperty>{{"expanded",expanded}});}
        [Serializable]sealed class TeamCommand {public string id;public int team;}
        public void ReceivePhotonMessage(PlayerRef source,string kind,string payload)
        {
            if(kind=="reject")
            {if(source==Object.StateAuthority)PhotonSession.Find(Runner)?.Reject(Runner.LocalPlayer.RawEncoded,JsonConvert.DeserializeObject<string>(payload));return;}
            if(!HasStateAuthority)return;
            if(kind=="reserve")ReserveFrom(JsonConvert.DeserializeObject<string>(payload),source);
            else if(kind=="ready")ReadyFrom(JsonConvert.DeserializeObject<bool>(payload),source);
            else if(kind=="command"){var command=JsonConvert.DeserializeObject<string[]>(payload);if(command?.Length==2)CommandFrom(command[0],command[1],source);}
            else if(kind=="team"){var command=JsonConvert.DeserializeObject<TeamCommand>(payload);if(command!=null)TeamFrom(command.id,command.team,source);}
            else if(kind=="kick")KickFrom(JsonConvert.DeserializeObject<string>(payload),source);
        }
        void Save(PhotonRoomData data)
        {
            data.revision++;Payload=JsonConvert.SerializeObject(data);cached=data;parsed=Payload;
            bool full=data.seats.Count>=2*(int)data.mode;
            if(!data.party)
            {
                Runner.SessionInfo.IsVisible=!data.custom&&data.phase=="searching"&&!full;
                Runner.SessionInfo.IsOpen=data.phase=="searching"||data.phase=="playing"&&data.seats.Any(s=>!s.member.bot&&s.peer==0);
                int cap=(int)data.mode;
                Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string,SessionProperty>{
                    {"fit1",data.seats.Count(s=>s.member.team==0)<cap||data.seats.Count(s=>s.member.team==1)<cap},
                    {"fit2",cap==2&&(data.seats.All(s=>s.member.team!=0)||data.seats.All(s=>s.member.team!=1))},
                    {"stage",data.phase=="searching"?0:1}});
            }
            PhotonSession.Find(Runner)?.Notify();
        }
        void ReserveFrom(string json,PlayerRef source)
        {
            var data=Data;
            if(json==null||json.Length>4096)return;
            var requested=JsonConvert.DeserializeObject<List<RoomMember>>(json);
            string identity=Runner.GetPlayerUserId(source);
            if(requested==null||requested.Count<1||requested.Count>(data.party?1:data.custom?2*(int)data.mode:2)
                ||requested.All(m=>m.id!=identity)||requested.Select(m=>m.id).Distinct().Count()!=requested.Count
                ||requested.Any(m=>m.bot||RankedBotAccounts.Contains(m.id)||!Guid.TryParse(m.id,out _)||string.IsNullOrWhiteSpace(m.name)||m.name.Length>18))
            {Reject(source,"Thông tin người chơi không hợp lệ.");return;}
            var existing=data.seats.FirstOrDefault(s=>s.member.id==identity);
            if(existing!=null)
            {
                if(existing.peer!=0&&existing.peer!=source.RawEncoded){Reject(source,"Tài khoản đã có trong phòng trên kết nối khác.");return;}
                existing.peer=source.RawEncoded;Save(data);return;
            }
            if(data.phase!="idle"&&data.phase!="searching"){Reject(source,"Phòng đã bắt đầu xác nhận trận.");return;}
            int capacity=data.party?(data.custom?2*(int)data.mode:2):data.expectedPlayers>0?data.expectedPlayers:2*(int)data.mode;
            if(data.seats.Count+requested.Count>capacity){Reject(source,"Phòng đã đủ người.");return;}
            int team=-1;
            bool preserveTeams=!data.party&&data.custom;
            if(preserveTeams&&(requested.Any(m=>m.team<0||m.team>1)||requested.Count(m=>m.team==0)>(int)data.mode||requested.Count(m=>m.team==1)>(int)data.mode))
            {Reject(source,"Đội hình phòng riêng không hợp lệ.");return;}
            if(!preserveTeams&&(!data.party||data.custom))
            {
                int a=data.seats.Count(s=>s.member.team==0),b=data.seats.Count(s=>s.member.team==1),cap=(int)data.mode;
                team=a+requested.Count<=cap&&(a<=b||b+requested.Count>cap)?0:b+requested.Count<=cap?1:-1;
                if(team<0){Reject(source,"Không còn đủ chỗ trong cùng đội cho nhóm.");return;}
            }
            string group=requested.First(m=>m.id==identity).group;
            if(requested.Any(member=>data.seats.Any(s=>s.member.id==member.id))){Reject(source,"Nhóm có thành viên đã ở trong trận.");return;}
            foreach(var member in requested)
            {
                member.group=group;if(!preserveTeams)member.team=team;member.bot=false;member.ready=false;
                data.seats.Add(new PhotonSeat{member=member,peer=member.id==identity?source.RawEncoded:0});
            }
            if(!data.party&&data.seats.Count==capacity&&data.seats.All(s=>s.peer!=0))BeginReady(data);
            Save(data);
        }
        public void CheckAllConnected()
        {
            if(!HasStateAuthority)return;
            var data=Data;
            if(!data.party&&data.phase=="searching"&&data.seats.Count==(data.expectedPlayers>0?data.expectedPlayers:2*(int)data.mode)&&data.seats.All(s=>s.peer!=0))
            {BeginReady(data);Save(data);}
        }
        void BeginReady(PhotonRoomData data)
        {data.phase="ready";data.deadline=Runner.SimulationTime+MatchReadyPolicy.Seconds;foreach(var seat in data.seats)seat.decision=seat.member.bot?ReadyDecision.Accepted:ReadyDecision.Pending;}
        public bool TryBeginRankedFallback()
        {
            if(!HasStateAuthority||Data.party||Data.custom||Data.mode!=PlayMode.Singles||Data.phase!="searching"||Data.seats.Count!=1||Data.seats[0].member.bot||Data.seats[0].peer==0)return false;
            Data.phase="assigning-bot";Save(Data);return true;
        }
        public void CompleteRankedFallback(RoomMember bot)
        {
            if(!HasStateAuthority||Data.phase!="assigning-bot"||Data.seats.Count!=1||!bot.bot||!RankedBotAccounts.Contains(bot.id))throw new InvalidOperationException("Phòng không còn nhận đối thủ dự phòng.");
            bot.team=1-Data.seats[0].member.team;Data.seats.Add(new PhotonSeat{member=bot,peer=0});
            BeginReady(Data);Save(Data);
        }
        public void ResumeHumanSearch(){if(HasStateAuthority&&Data.phase=="assigning-bot"){Data.phase="searching";Save(Data);}}
        void Reject(PlayerRef player,string reason)=>PhotonSession.Find(Runner).Send(player,Object,"reject",reason);
        void ReadyFrom(bool accept,PlayerRef source)
        {
            var data=Data;if(data.party||data.phase!="ready")return;
            var seat=data.seats.FirstOrDefault(s=>s.peer==source.RawEncoded);if(seat==null||seat.decision!=ReadyDecision.Pending)return;
            seat.decision=accept?ReadyDecision.Accepted:ReadyDecision.Declined;
            if(!accept){data.phase="rejected";data.notice="Có người từ chối tham gia.";}
            else if(data.seats.All(s=>s.decision==ReadyDecision.Accepted))data.phase="loading";
            Save(data);
        }
        void CommandFrom(string kind,string value,PlayerRef source)
        {
            var data=Data;
            if(!data.seats.Any(s=>s.peer==source.RawEncoded))return;
            if(kind=="cancel"&&data.party){data.phase="idle";data.matchSession="";data.notice="Một người đã hủy tìm trận.";Save(data);return;}
            if(!Captain(source))return;
            if(kind=="search"&&data.party){data.phase="searching";Runner.SessionInfo.IsOpen=false;}
            else if(kind=="match"&&data.party){data.matchSession=value;}
            else if(kind=="idle"&&data.party){data.phase="idle";data.matchSession="";Runner.SessionInfo.IsOpen=true;}
            else if(kind=="close"&&data.party){data.phase="closed";data.notice="Chủ phòng đã đóng nhóm.";}
            else if(kind=="playing"&&!data.party&&data.phase=="loading")data.phase="playing";
            else return;
            Save(data);
        }
        void TeamFrom(string player,int team,PlayerRef source)
        {
            var data=Data;if(!data.party||!data.custom||data.phase!="idle"||team<0||team>1)return;
            var seat=data.seats.FirstOrDefault(s=>s.member.id==player);
            if(seat==null||(!Captain(source)&&seat.peer!=source.RawEncoded)||data.seats.Count(s=>s!=seat&&s.member.team==team)>=(int)data.mode)return;
            seat.member.team=team;Save(data);
        }
        void KickFrom(string player,PlayerRef source)
        {
            var data=Data;if(!data.party||!Captain(source)||player==Runner.GetPlayerUserId(source))return;
            var seat=data.seats.FirstOrDefault(s=>s.member.id==player);if(seat==null)return;
            data.seats.Remove(seat);Reject(PlayerRef.FromEncoded(seat.peer),"Bạn đã được mời rời phòng.");Save(data);
        }
    }
}
