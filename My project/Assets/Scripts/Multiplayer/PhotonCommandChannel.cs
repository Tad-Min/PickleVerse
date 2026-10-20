using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Newtonsoft.Json;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>Each peer owns its outbox and acknowledgements. Commands stay replicated until acknowledged.</summary>
    public sealed class PhotonCommandChannel:NetworkBehaviour
    {
        [Networked,Capacity(4096)]public string Outbox{get;set;}
        [Networked,Capacity(512)]public string Acknowledgements{get;set;}
        [Networked]public Vector2 Movement{get;set;}
        [Networked]public NetworkBool Running{get;set;}
        [Networked]public uint InputSequence{get;set;}
        public static PhotonCommandChannel For(NetworkRunner runner,PlayerRef peer)=>channels.FirstOrDefault(c=>c&&c.Runner==runner&&c.Object.IsValid&&c.Object.StateAuthority==peer);
        public void Input(uint sequence,Vector2 movement,bool running){if(!HasStateAuthority)return;InputSequence=sequence;Movement=movement;Running=running;}
        static readonly List<PhotonCommandChannel> channels=new List<PhotonCommandChannel>();
        readonly Queue<Envelope> pending=new Queue<Envelope>();
        readonly Dictionary<int,int> received=new Dictionary<int,int>();
        [Serializable]sealed class Envelope{public int destination,sequence;public PhotonMessage message;}
        int sequence;
        Envelope sending;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){channels.Clear();}
        public override void Spawned()
        {
            transform.SetParent(null,true);Runner.MakeDontDestroyOnLoad(gameObject);channels.Add(this);
            if(HasStateAuthority)PhotonSession.Find(Runner)?.BindChannel(this);
        }
        public override void Despawned(NetworkRunner runner,bool hasState){channels.Remove(this);}
        public void Enqueue(PlayerRef destination,PhotonMessage message)
        {
            if(!HasStateAuthority)throw new InvalidOperationException("Không sở hữu kênh lệnh.");
            if(pending.Count>=64)throw new InvalidOperationException("Quá nhiều lệnh đang chờ xác nhận.");
            pending.Enqueue(new Envelope{destination=destination.RawEncoded,sequence=++sequence,message=message});
        }
        public override void FixedUpdateNetwork()
        {
            if(!HasStateAuthority)return;
            if(sending!=null)
            {
                var receiver=channels.FirstOrDefault(c=>c&&c.Runner==Runner&&c.Object.IsValid&&c.Object.StateAuthority.RawEncoded==sending.destination);
                var ack=receiver&&!string.IsNullOrEmpty(receiver.Acknowledgements)?JsonConvert.DeserializeObject<Dictionary<int,int>>(receiver.Acknowledgements):null;
                if(ack!=null&&ack.TryGetValue(Runner.LocalPlayer.RawEncoded,out int value)&&value>=sending.sequence
                    ||!Runner.ActivePlayers.Any(p=>p.RawEncoded==sending.destination))sending=null;
            }
            if(sending==null&&pending.Count>0){sending=pending.Dequeue();Outbox=JsonConvert.SerializeObject(sending);}
        }
        public override void Render()
        {
            if(!HasStateAuthority)return;
            foreach(var channel in channels.ToArray())
            {
                if(!channel||channel.Runner!=Runner||!channel.Object.IsValid||string.IsNullOrEmpty(channel.Outbox))continue;
                Envelope message;
                try{message=JsonConvert.DeserializeObject<Envelope>(channel.Outbox);}catch(JsonException){continue;}
                if(message==null||message.destination!=Runner.LocalPlayer.RawEncoded||message.message==null)continue;
                int source=channel.Object.StateAuthority.RawEncoded;
                if(received.TryGetValue(source,out int done)&&done>=message.sequence)continue;
                if(!PhotonSession.Find(Runner).Deliver(channel.Object.StateAuthority,message.message))continue;
                received[source]=message.sequence;Acknowledgements=JsonConvert.SerializeObject(received);
            }
        }
    }
}
