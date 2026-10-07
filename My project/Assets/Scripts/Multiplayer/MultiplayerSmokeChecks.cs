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
                await app.SelectModeAsync(PlayMode.Doubles); File.WriteAllText(file+".code",app.Room.Code);
                await Wait(()=>app.Members().Count==2,"Bạn chưa vào nhóm");
                if(app.CanChangeMode) throw new Exception("Nhóm đôi chưa khóa chuyển 1vs1");
                bool rejected=false; try { await app.SelectModeAsync(PlayMode.Singles); } catch(InvalidOperationException) { rejected=true; }
                if(!rejected) throw new Exception("API cho nhóm đôi đổi 1vs1");
                await app.FindAsync();
                await Wait(()=>app.State==FlowState.WaitingRoom&&File.Exists(file+".cancelled"),"Bạn hủy nhưng chủ nhóm vẫn tìm");
                if(app.Members().Count!=2 || app.Mode!=PlayMode.Doubles) throw new Exception("Hủy không giữ nhóm");
                await app.FindAsync();
                await Wait(()=>app.Members().Count==1 && app.State==FlowState.WaitingRoom,"Bạn rời nhưng tìm chưa hủy");
                if(!app.CanChangeMode) throw new Exception("Chưa mở khóa 1vs1 sau khi bạn rời");
                await app.SelectModeAsync(PlayMode.Singles);
            }
            else
            {
                await Wait(()=>File.Exists(file+".code"),"Không có mã nhóm"); await app.JoinRoomAsync(File.ReadAllText(file+".code"));
                await Wait(()=>app.State==FlowState.Delay,"Chủ nhóm chưa bắt đầu");
                if(app.CanChangeMode) throw new Exception("Khách nhóm đôi đổi được 1vs1");
                await app.CancelSearchAsync(); File.WriteAllText(file+".cancelled","guest cancelled whole party");
                await Wait(()=>app.State==FlowState.Delay,"Không nhận lượt tìm mới");
                await app.LeaveRoomAsync();
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
                File.WriteAllText(file+".code",app.Room.Code); await Wait(()=>app.Members().Count==2,"Khách chưa vào");
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
            if(behavior=="hostLeaves")
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
            await Wait(()=>app.State==FlowState.Connecting,"Chưa tìm được đối thủ",100);
            if(cancel)
            {
                await app.CancelSearchAsync(); await Task.Delay(4500);
                if(app.State!=FlowState.WaitingRoom||app.Network.IsListening) throw new Exception("Kết quả muộn khởi động trận đã hủy");
                await app.FindAsync(); if(app.Elapsed!="00:00") throw new Exception("Lượt tìm mới không đặt lại đồng hồ");
            }
            else
            {
                string elapsed=app.Elapsed;
                await Wait(()=>app.State==FlowState.Searching,"Người còn chờ chưa tự tìm lại");
                if(app.Elapsed=="00:00"||string.CompareOrdinal(app.Elapsed,elapsed)<0) throw new Exception("Tự tìm lại đặt lại đồng hồ");
            }
        }
        public static async Task CustomAdministration(MultiplayerApp app,bool host,string file)
        {
            if(host)
            {
                await app.CreateCustomAsync(PlayMode.Singles); File.WriteAllText(file+".code",app.Room.Code); await app.ReadyAsync();
                await Wait(()=>app.Members().Count==2&&app.Members().All(m=>m.ready),"Custom chưa đủ người sẵn sàng");
                string guest=app.Members().First(m=>m.id!=app.PlayerId).id;
                await app.SetTeamAsync(guest,0);
                if(app.Members().Any(m=>m.ready)) throw new Exception("Hoán đổi đội chưa xóa Sẵn sàng");
                await Wait(()=>File.Exists(file+".swapack"),"Khách chưa nhận đội mới/Sẵn sàng mới");
                bool rejected=false; try { await app.StartCustomAsync(); } catch(InvalidOperationException) { rejected=true; }
                if(!rejected) throw new Exception("Bắt đầu được khi chưa sẵn sàng");
                await app.KickAsync(guest);
                await Wait(()=>File.Exists(file+".kickack"),"Khách chưa bị mời ra");
                await app.ReadyAsync();
                await Wait(()=>app.Members().Count==2&&app.Members().All(m=>m.ready),"Chưa sẵn sàng sau khi vào lại");
                await app.StartCustomAsync();
            }
            else
            {
                await Wait(()=>File.Exists(file+".code"),"Chưa có mã custom"); string code=File.ReadAllText(file+".code");
                await app.JoinRoomAsync(code);
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team>=0,"Chưa được phân đội"); await app.ReadyAsync();
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team==0&&!app.Members().First(m=>m.id==app.PlayerId).ready,"Chưa nhận đổi đội");
                File.WriteAllText(file+".swapack","new team received; readiness reset");
                await Wait(()=>app.Room==null,"Chưa nhận mời ra"); File.WriteAllText(file+".kickack","kick received");
                await Task.Delay(1200); await app.JoinRoomAsync(code);
                await Wait(()=>app.Members().First(m=>m.id==app.PlayerId).team>=0,"Chưa được phân đội lại"); await app.ReadyAsync();
            }
        }
    }
}
