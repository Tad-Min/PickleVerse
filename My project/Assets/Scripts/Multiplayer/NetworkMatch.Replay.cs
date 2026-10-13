using System.Linq;
using PickleVerse.Replays;
using PickleVerse.Shop;
using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        ReplayRecorder replay;
        [Unity.Netcode.Rpc(Unity.Netcode.SendTo.ClientsAndHost)]
        void ReplayEventRpc(string kind,string detail){replay?.Event(kind,detail);}
        void EnsureReplay()
        {
            if(replay||!Started.Value||Finished.Value||Roster==null||!PickleVerse.Progression.ProgressionRuntime.Exists)return;
            var actors=NetworkPlayer.All.Where(p=>p&&p.IsSpawned&&p.Actor).OrderBy(p=>p.Identity.Value.ToString()).ToArray();
            if(actors.Length!=Roster.Count)return;
            replay=gameObject.AddComponent<ReplayRecorder>();
            replay.Begin(new ReplayMetadata{playerId=PickleVerse.Progression.ProgressionRuntime.Instance.Player.id,matchId=Roster.matchId,startedUtcTicks=StartedUtc.Value},
                actors.Select(p=>new ReplayActorSource{root=p.Actor.transform,paddle=p.Actor.paddle?p.Actor.paddle.transform:null,id=p.Identity.Value.ToString(),name=Roster.Find(p.Identity.Value.ToString())?.name,team=p.Team.Value,visualId=p.Female.Value?"Female":"Male",paddleId=p.PaddleSkin.Value.ToString()}).ToArray(),
                ()=>Body?Body.transform.position:BallPosition,
                ()=>new ReplayFrame{scoreA=ScoreA.Value,scoreB=ScoreB.Value,gamesA=GamesA.Value,gamesB=GamesB.Value,status=Status.Value.ToString()},visualBall:Body?Body.transform:null);
        }
    }
}
