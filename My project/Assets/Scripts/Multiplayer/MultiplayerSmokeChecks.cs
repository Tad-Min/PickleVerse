#if PICKLEVERSE_LEGACY_NGO
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
namespace PickleVerse.Multiplayer
{
    // Only called by the opt-in smoke runner; never enabled in a normal game.
    internal static class MultiplayerSmokeChecks
    {
        public static void WriteSharedCode(string path,string code)
        {
            // Publish only a fully written code. File.Exists on Windows can become
            // true while WriteAllText still holds an exclusive write handle.
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            File.WriteAllText(temporary,code); File.Move(temporary,path);
        }
        static async Task Wait(Func<bool> condition,string message,int seconds=30)
        {
            double until=Time.realtimeSinceStartupAsDouble+seconds;
            while(!condition() && Time.realtimeSinceStartupAsDouble<until) await Task.Delay(100);
            if(!condition()) throw new TimeoutException(message);
        }
        public static async Task DuoControls(MultiplayerApp app,bool host,string file)
        {
            if(host)
            {
                await app.RoomService.SelectDoublesAsync(); WriteSharedCode(file+".code",app.Room.Code);
                await Wait(()=>app.Members().Count==2,"Bạn chưa vào nhóm");
                await app.MatchmakingService.FindAsync();
                await Wait(()=>app.State==FlowState.WaitingRoom&&File.Exists(file+".cancelled"),"Bạn hủy nhưng chủ nhóm vẫn tìm");
                if(app.Members().Count!=2 || app.Mode!=PlayMode.Doubles) throw new Exception("Hủy không giữ nhóm");
                await app.RoomService.SelectSinglesAsync();
                if(app.Mode!=PlayMode.Singles || app.Room!=null || app.Members().Count!=1) throw new Exception("Chuyển 1vs1 chưa giải tán nhóm");
                await Wait(()=>File.Exists(file+".singlesack"),"Khách chưa nhận giải tán nhóm");
            }
            else
            {
                await Wait(()=>File.Exists(file+".code"),"Không có mã nhóm"); await app.RoomService.JoinAsync(File.ReadAllText(file+".code"));
                await Wait(()=>app.State==FlowState.Delay,"Chủ nhóm chưa bắt đầu");
                await app.MatchmakingService.CancelAsync(); File.WriteAllText(file+".cancelled","guest cancelled whole party");
                await Wait(()=>app.Room==null && !app.Searching,"Khách chưa bị mời ra khi chủ nhóm chọn 1vs1");
                File.WriteAllText(file+".singlesack","party disbanded");
            }
        }
        public static async Task Reconnect(MultiplayerApp app)
        {
            string identity=app.PlayerId;
            if(app.Network.IsHost)
            {
                await Task.Delay(1500);
                var peer=app.ClientIdentities.First(p=>p.Key!=app.Network.LocalClientId);
                app.Network.DisconnectClient(peer.Key);
                await Wait(()=>NetworkMatch.Instance.Paused.Value,"Host chưa tạm dừng khi mất kết nối");
                int a=NetworkMatch.Instance.ScoreA.Value,b=NetworkMatch.Instance.ScoreB.Value;
                await Wait(()=>!NetworkMatch.Instance.Paused.Value&&app.ClientIdentities.Values.Contains(peer.Value),"Không nối lại cùng danh tính",25);
                if(a!=NetworkMatch.Instance.ScoreA.Value||b!=NetworkMatch.Instance.ScoreB.Value) throw new Exception("Điểm thay đổi trong khi tạm dừng");
            }
            else
            {
                await Wait(()=>app.State!=FlowState.InMatch,"Chưa nhận mất kết nối");
                await Wait(()=>app.State==FlowState.InMatch && NetworkPlayer.All.Any(p=>p.IsOwner),"Không khôi phục quyền điều khiển sau reconnect",25);
                if(app.PlayerId!=identity) throw new Exception("Reconnect đổi danh tính");
            }
        }
        public static async Task RoomDeparture(MultiplayerApp app,bool host,bool custom,string file)
        {
            if(host)
            {
                if(custom) await app.CreateCustomAsync(PlayMode.Singles); else await app.SelectModeAsync(PlayMode.Doubles);
                WriteSharedCode(file+".code",app.Room.Code); await Wait(()=>app.Members().Count==2,"Khách chưa vào");
                await Wait(()=>File.Exists(file+".joined"),"Khách chưa hoàn tất vào phòng");
                if(!custom) await app.FindAsync();
                await app.LeaveRoomAsync();
                if(app.Searching) throw new Exception("Chủ nhóm rời mà vẫn tìm");
            }
            else
            {
                await Wait(()=>File.Exists(file+".code"),"Thiếu mã phòng"); await app.JoinRoomAsync(File.ReadAllText(file+".code")); File.WriteAllText(file+".joined","joined");
                if(custom) await Wait(()=>app.Room==null && app.Notice.Contains("đóng"),"Custom chưa đóng khi chủ rời");
                else
                {
                    await Wait(()=>app.Room!=null&&app.IsCaptain&&app.Members().Count==1&&!app.Searching,"Chưa chuyển quyền/hủy khi chủ rời");
                    await app.SelectModeAsync(PlayMode.Singles);
                }
            }
        }
        public static async Task GameDeparture(MultiplayerApp app,string behavior)
        {
            bool host=app.Network.IsHost;
            if(behavior=="expiredReconnect")
            {
                if(host)
                {
                    await Task.Delay(1500);
                    var peer=app.ClientIdentities.First(p=>p.Key!=app.Network.LocalClientId);
                    // Only this explicitly requested smoke run refuses reconnect; normal approval is unchanged.
                    var approval=app.Network.ConnectionApprovalCallback;
                    app.Network.ConnectionApprovalCallback=(request,response)=> { response.Approved=false; response.Reason="Smoke reconnect expiry"; response.Pending=false; };
                    try
                    {
                        app.Network.DisconnectClient(peer.Key);
                        await Wait(()=>app.State==FlowState.Results,"Reconnect hết hạn chưa xử thua",45);
                        if(!NetworkMatch.Instance.Status.Value.ToString().Contains("30 giây")) throw new Exception("Sai lý do hết hạn reconnect");
                        await app.ReturnToRoomAsync("Đã kiểm tra hết hạn reconnect.");
                    }
                    finally { app.Network.ConnectionApprovalCallback=approval; }
                }
                else await Wait(()=>app.State==FlowState.WaitingRoom,"Khách chưa hết hạn reconnect",50);
                var player=PickleVerse.Progression.ProgressionRuntime.Instance.Player;
                var result=player.history.FirstOrDefault();
                if(result==null || result.outcome==PickleVerse.Progression.MatchOutcome.Interrupted || result.localForfeit==host || player.totalXp!=(host?10:0) || player.elo!=(host?520:480)) throw new Exception("Hết hạn reconnect chưa ghi đúng thua/Elo/XP");
            }
            else if(behavior=="hostLeaves")
            {
                if(host) { await Task.Delay(2000); await app.ReturnToRoomAsync("Host test đã rời."); }
                else await Wait(()=>app.State==FlowState.WaitingRoom,"Host rời nhưng khách chưa về phòng",40);
            }
            else if(behavior=="clientLeaves")
            {
                if(!host) { await Task.Delay(2000); await app.LeaveGameAsync(); }
                else { await Wait(()=>app.State==FlowState.Results,"Khách rời chưa bị xử thua"); if(!NetworkMatch.Instance.Status.Value.ToString().Contains("chủ động rời")) throw new Exception("Sai lý do kết quả"); await app.ReturnToRoomAsync("Đã kiểm tra xử thua khi rời."); }
            }
        }
        public static async Task CancelDuringConnect(MultiplayerApp app,bool cancel)
        {
            await Wait(()=>app.CanAcceptMatch,"Chưa mở xác nhận trận",100);
            if(cancel)
            {
                await Wait(()=>app.ReadyCount>=1,"Đối thủ chưa chấp nhận",15);
                await app.DeclineMatchAsync(); await Task.Delay(4500);
                if(app.State!=FlowState.WaitingRoom||app.Network.IsListening) throw new Exception("Kết quả muộn khởi động trận đã hủy");
                await app.FindAsync(); if(app.Elapsed!="00:00") throw new Exception("Lượt tìm mới không đặt lại đồng hồ");
            }
            else
            {
                await app.AcceptMatchAsync();
                await Wait(()=>app.Match==null && (app.State==FlowState.Delay || app.State==FlowState.Searching),"Người chấp nhận chưa tự tìm lại",50);
            }
        }
        public static async Task CustomAdministration(MultiplayerApp app,bool host,string file)
        {
            if(host)
            {
                await app.RoomService.SelectCustomAsync(PlayMode.Doubles); WriteSharedCode(file+".code",app.Room.Code);
                await Wait(()=>File.Exists(file+".swapack"),"Khách chưa đổi đội bằng BtnSwap");
                await Wait(()=>app.Members().Count==2 && app.Members().All(m=>m.team==0),"Host chưa nhận yêu cầu đổi đội");
                bool rejected=false; try { await app.StartCustomAsync(); } catch(InvalidOperationException) { rejected=true; }
                if(!rejected) throw new Exception("Bắt đầu được khi một đội trống");
                string guest=app.Members().First(m=>m.id!=app.PlayerId).id;
                await app.SetTeamAsync(guest,1); await app.RoomService.KickAsync(guest);
                await Wait(()=>File.Exists(file+".kickack"),"Khách chưa bị mời ra");
                await Wait(()=>app.Members().Count==2 && app.Members().Any(m=>m.team==1),"Chưa phân đội sau khi vào lại");
                if(app.Members().Any(m=>m.ready)) throw new Exception("Test này phải bắt đầu không có Ready");
                await app.RoomService.StartCustomAsync();
            }
            else
            {
                await Wait(()=>File.Exists(file+".code"),"Chưa có mã custom"); string code=File.ReadAllText(file+".code");
                await app.RoomService.JoinAsync(code);
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team==1,"Chưa được phân đội B");
                await app.RoomService.SwapTeamAsync();
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team==0,"Chưa nhận đổi đội A");
                File.WriteAllText(file+".swapack","own team swap received");
                await Wait(()=>app.Room==null,"Chưa nhận mời ra"); File.WriteAllText(file+".kickack","kick received");
                await Task.Delay(2500); await app.RoomService.JoinAsync(code);
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team>=0,"Chưa được phân đội lại");
            }
        }
    }
}


#endif
