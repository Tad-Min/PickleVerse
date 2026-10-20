using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Multiplayer;
using UnityEngine;

namespace PickleVerse.Editor
{
    public static class PhotonReadyCheck
    {
        [Serializable] public sealed class Report
        {public bool completed,passed,rosterReplicated,waitsForBoth,loadsAfterBoth;public string error,stage,firstMessage,secondMessage,firstIdentity,secondIdentity,diagnostic;}
        public static Report Result {get;private set;}
        public static async Task RunAsync()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            Result=new Report();PhotonSession first=null,second=null;float scale=Time.timeScale;Time.timeScale=1;
            try
            {
                string a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N"),room="pkv-ready-"+Guid.NewGuid().ToString("N");
                var prefab=Resources.Load<GameObject>("Photon/Session State");
                if(!prefab)throw new InvalidOperationException("Missing Photon session prefab.");
                first=PhotonSession.Create("Photon Ready Check A");
                Result.stage="start A";
                await first.StartAsync(room,a,null,new PhotonRoomData{mode=PickleVerse.Multiplayer.PlayMode.Singles,phase="searching"},prefab);
                Result.stage="reserve A";Result.firstIdentity=first.Runner.GetPlayerUserId(first.Runner.LocalPlayer);
                await first.ReserveAsync(new List<RoomMember>{new RoomMember{id=a,name="Ready A",group=a,gender="Male"}});
                second=PhotonSession.Create("Photon Ready Check B");
                Result.stage="join B";
                await second.JoinAsync(room,b,first.Runner.SessionInfo.Region,false);
                Result.stage="reserve B";Result.secondIdentity=second.Runner.GetPlayerUserId(second.Runner.LocalPlayer);
                await second.ReserveAsync(new List<RoomMember>{new RoomMember{id=b,name="Ready B",group=b,gender="Female"}});
                await PhotonSession.WaitAsync(()=>first.Data.phase=="ready"&&second.Data.phase=="ready","Both clients did not enter ready check.");
                Result.rosterReplicated=first.Data.seats.Count==2&&second.Data.seats.Count==2;
                first.Lobby.Ready(true);
                await PhotonSession.WaitAsync(()=>second.Data.seats.Exists(s=>s.member.id==a&&s.decision==ReadyDecision.Accepted),"First acceptance did not replicate.");
                Result.waitsForBoth=first.Data.phase=="ready"&&second.Data.phase=="ready";
                second.Lobby.Ready(true);
                await PhotonSession.WaitAsync(()=>first.Data.phase=="loading"&&second.Data.phase=="loading","Both acceptances did not release loading.");
                Result.loadsAfterBoth=true;Result.passed=Result.rosterReplicated&&Result.waitsForBoth&&Result.loadsAfterBoth;
            }
            catch(Exception error){Result.error=error.Message;}
            finally
            {
                Result.firstMessage=first?.LastMessage;Result.secondMessage=second?.LastMessage;
                Result.diagnostic=Newtonsoft.Json.JsonConvert.SerializeObject(new[]{first,second}.Where(s=>s!=null).Select(s=>new{master=s.IsHost,local=s.Runner.LocalPlayer.RawEncoded,authority=s.Lobby?s.Lobby.Object.StateAuthority.RawEncoded:0,seats=s.Data.seats,trace=s.MessageTrace,rejected=s.Rejected,remoteIds=s.Runner.ActivePlayers.Select(p=>new{peer=p.RawEncoded,id=s.Runner.GetPlayerUserId(p)})}));
                if(second!=null)await second.LeaveAsync();if(first!=null)await first.LeaveAsync();
                Time.timeScale=scale;Result.completed=true;
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/verification/photon-ready-check.json")),JsonUtility.ToJson(Result,true));
            }
        }
    }
}
