using Fusion;
using Newtonsoft.Json.Linq;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkMatch
    {
        string renderedResults;
        public override void Render()
        {
            if(renderedStarted!=Started){renderedStarted=Started;StartChanged(!Started,Started);}
            if(renderedFinished!=Finished){renderedFinished=Finished;ResultChanged(!Finished,Finished);}
            if(renderedResults!=ProgressionResults){renderedResults=ProgressionResults;ResultsAvailable(null,ProgressionResults);}
        }
        void Send(string kind)=>PhotonSession.Find(Runner).Send(Object.StateAuthority,Object,kind,new object[0]);
        void Broadcast(string kind,params object[] values){foreach(var peer in Runner.ActivePlayers)PhotonSession.Find(Runner).Send(peer,Object,kind,values);}
        void CourtReadyRpc()=>Send("court-ready");
        void CourtRevealedRpc()=>Send("court-revealed");
        void LeaveRpc()=>Send("leave");
        void MatchSoundRpc(int sound)=>Broadcast("sound",sound);
        void AnnouncementRpc(string title,string detail,int tone,float duration)=>Broadcast("announce",title,detail,tone,duration);
        void ReplayEventRpc(string kind,string detail)=>Broadcast("replay-event",kind,detail);
        public void ReceivePhotonMessage(PlayerRef source,string kind,string payload)
        {
            var a=JArray.Parse(payload);
            if(HasStateAuthority&&MultiplayerApp.Instance.ClientIdentities.ContainsKey((ulong)source.RawEncoded)&&a.Count==0)
            {
                if(kind=="court-ready")CourtReadyFrom(source);
                else if(kind=="court-revealed")CourtRevealedFrom(source);
                else if(kind=="leave")LeaveFrom(source);
            }
            if(source!=Object.StateAuthority)return;
            if(kind=="sound"&&a.Count==1)SoundEffect(a[0].Value<int>());
            else if(kind=="announce"&&a.Count==4)AnnouncementEffect(a[0].Value<string>(),a[1].Value<string>(),a[2].Value<int>(),a[3].Value<float>());
            else if(kind=="replay-event"&&a.Count==2)ReplayEventEffect(a[0].Value<string>(),a[1].Value<string>());
        }
    }
}
