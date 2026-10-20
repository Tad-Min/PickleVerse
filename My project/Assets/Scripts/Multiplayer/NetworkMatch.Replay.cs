using System.Linq;
using PickleVerse.Replays;
using PickleVerse.Shop;
using UnityEngine;
namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        ReplayRecorder replay;
        void ReplayEventEffect(string kind,string detail)
        {
            if(kind=="Miss" && !IsServer)
                NetworkPlayer.All.FirstOrDefault(p=>p.Identity.ToString()==detail)?.Actor?.MissContact();
            replay?.Event(kind,detail);
        }
        void EnsureReplay()
        {
            if(replay||!Started||Finished||Roster==null||!PickleVerse.Progression.ProgressionRuntime.Exists)return;
            var actors=NetworkPlayer.All.Where(p=>p&&p.IsSpawned&&p.Actor).OrderBy(p=>p.Identity.ToString()).ToArray();
            if(actors.Length!=Roster.Count)return;
            replay=gameObject.AddComponent<ReplayRecorder>();
            replay.Begin(new ReplayMetadata{playerId=PickleVerse.Progression.ProgressionRuntime.Instance.Player.id,matchId=Roster.matchId,startedUtcTicks=StartedUtc},
                actors.Select(p=>new ReplayActorSource{root=p.Actor.transform,paddle=p.Actor.paddle?p.Actor.paddle.transform:null,id=p.Identity.ToString(),name=Roster.Find(p.Identity.ToString())?.name,team=p.Team,visualId=p.Female?"Female":"Male",paddleId=p.PaddleSkin.ToString()}).ToArray(),
                ()=>Body?Body.transform.position:BallPosition,
                ()=>new ReplayFrame{scoreA=ScoreA,scoreB=ScoreB,gamesA=GamesA,gamesB=GamesB,status=Status.ToString()},visualBall:Body?Body.transform:null);
        }
    }
}
