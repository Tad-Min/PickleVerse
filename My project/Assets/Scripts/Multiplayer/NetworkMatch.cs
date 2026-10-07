using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    [Serializable] public struct BallFrame : INetworkSerializable,IEquatable<BallFrame>
    {
        public Vector3 position,velocity;
        public bool live;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T:IReaderWriter { serializer.SerializeValue(ref position); serializer.SerializeValue(ref velocity); serializer.SerializeValue(ref live); }
        public bool Equals(BallFrame o)=>position==o.position&&velocity==o.velocity&&live==o.live;
    }
    public sealed class NetworkMatch : NetworkBehaviour
    {
        public static NetworkMatch Instance { get; private set; }
        public NetworkVariable<FixedString4096Bytes> RosterJson=new NetworkVariable<FixedString4096Bytes>();
        public NetworkVariable<FixedString512Bytes> Status=new NetworkVariable<FixedString512Bytes>();
        public NetworkVariable<int> ScoreA=new NetworkVariable<int>(),ScoreB=new NetworkVariable<int>(),ServerTeam=new NetworkVariable<int>(),ServerIndex=new NetworkVariable<int>(),ServerNumber=new NetworkVariable<int>();
        public NetworkVariable<bool> Paused=new NetworkVariable<bool>(),Finished=new NetworkVariable<bool>(),WaitingServe=new NetworkVariable<bool>(),Started=new NetworkVariable<bool>();
        public NetworkVariable<BallFrame> Ball=new NetworkVariable<BallFrame>();
        public PickleballRules Rules { get; private set; }
        public Rigidbody Body { get; private set; }
        public MatchRoster Roster { get; private set; }
        readonly Dictionary<string,double> reservations=new Dictionary<string,double>();
        readonly Dictionary<string,NetworkPlayer> players=new Dictionary<string,NetworkPlayer>();
        readonly Dictionary<string,double> swingCooldown=new Dictionary<string,double>();
        readonly HashSet<ulong> courtReady=new HashSet<ulong>();
        double serveDeadline,rallyDeadline;
        Vector3 savedVelocity;
        bool rallyEnding;
        MatchRoster initialRoster;
        string shotLabel=""; double shotLabelUntil;

        public override void OnNetworkSpawn()
        {
            Instance=this;
            if(IsServer) RosterJson.Value=new FixedString4096Bytes(JsonConvert.SerializeObject(initialRoster??MultiplayerApp.Instance.Roster));
            Roster=JsonConvert.DeserializeObject<MatchRoster>(RosterJson.Value.ToString());
            BuildBall(); MultiplayerApp.Instance.CourtLoaded();
            Finished.OnValueChanged+=ResultChanged;
            Started.OnValueChanged+=StartChanged;
            if(IsServer)
            {
                Rules=new PickleballRules(Roster.mode);
                SpawnPlayers(); NetworkManager.OnClientConnectedCallback+=PlayerConnected;
                Body.isKinematic=true;
            }
            else StartCoroutine(ClientCamera());
        }
        public override void OnNetworkDespawn()
        {
            if(Instance==this) Instance=null; Finished.OnValueChanged-=ResultChanged; Started.OnValueChanged-=StartChanged;
            if(NetworkManager!=null) NetworkManager.OnClientConnectedCallback-=PlayerConnected;
            if(Body!=null) Destroy(Body.gameObject);
        }
        public void SetRoster(MatchRoster roster) { initialRoster=roster; }
        void BuildBall()
        {
            var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name="NetworkPickleball"; ball.transform.localScale=Vector3.one*(PickleballRules.BallRadius*2);
            var settings=MultiplayerApp.Instance.Settings;
            ball.GetComponent<Renderer>().sharedMaterial=settings.ballMaterial;
            Body=ball.AddComponent<Rigidbody>(); Body.mass=.026f; Body.linearDamping=.03f; Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            Body.isKinematic=!IsServer; Body.useGravity=IsServer;
            var physics=new PhysicsMaterial("PickleballBounce") { bounciness=.78f,staticFriction=.1f,dynamicFriction=.1f,bounceCombine=PhysicsMaterialCombine.Maximum };
            ball.GetComponent<SphereCollider>().sharedMaterial=physics;
            ball.AddComponent<MultiplayerBallContact>().match=this;
            var trail=ball.AddComponent<TrailRenderer>(); trail.time=.22f; trail.startWidth=.025f; trail.endWidth=.002f; trail.sharedMaterial=settings.trailMaterial;
        }
        void SpawnPlayers()
        {
            var app=MultiplayerApp.Instance;
            foreach(var m in Roster.members)
            {
                var client=app.ClientIdentities.First(p=>p.Value==m.id).Key;
                int index=Roster.members.Where(p=>p.team==m.team).ToList().FindIndex(p=>p.id==m.id);
                var go=Instantiate(app.Settings.playerPrefab);
                var player=go.GetComponent<NetworkPlayer>(); player.Initialize(m,index,StartingPosition(m.team,index));
                var networkObject=go.GetComponent<NetworkObject>(); networkObject.DontDestroyWithOwner=true; networkObject.SpawnAsPlayerObject(client,true);
                players[m.id]=player;
            }
            StartCoroutine(ClientCamera());
        }
        Vector3 StartingPosition(int team,int index)
        {
            float facing=PickleballRules.Facing(team);
            if(team==Rules.servingTeam && index==Rules.serverIndex) return new Vector3(Rules.serverWorldX,0,-facing*(PickleballRules.HalfLength+.45f));
            if(team!=Rules.servingTeam && index==Rules.receiverIndex) return new Vector3(-Rules.serverWorldX,0,-facing*(PickleballRules.HalfLength-.8f));
            int right=Rules.courtRight[team];
            return new Vector3((index==right?1:-1)*facing*1.5f,0,-facing*4.2f);
        }
        IEnumerator ClientCamera()
        {
            while(NetworkPlayer.All.Count<Roster.Count || NetworkPlayer.All.All(p=>!p.IsOwner)) yield return null;
            var player=NetworkPlayer.All.First(p=>p.IsOwner);
            var camera=Camera.main; if(camera!=null) (camera.GetComponent<CourtCamera>()??camera.gameObject.AddComponent<CourtCamera>()).target=player.transform;
            if(Environment.GetCommandLineArgs().Contains("-pkvSlowCourt")) yield return new WaitForSecondsRealtime(3);
            CourtReadyRpc();
            if(Started.Value) StartChanged(false,true);
            if(Finished.Value) ResultChanged(false,true);
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void CourtReadyRpc(RpcParams rpc=default)
        {
            if(Started.Value || !MultiplayerApp.Instance.ClientIdentities.ContainsKey(rpc.Receive.SenderClientId)) return;
            courtReady.Add(rpc.Receive.SenderClientId);
            if(courtReady.Count==Roster.Count) { BeginRally(); Started.Value=true; }
        }
        void StartChanged(bool old,bool started) { if(started) MultiplayerApp.Instance.MatchStarted(); }
        public void TrySwing(NetworkPlayer player,ShotIntent intent)
        {
            if(!IsServer || !Started.Value || Paused.Value || Finished.Value || !player.Connected) return;
            string id=player.Identity.Value.ToString(); double now=Time.realtimeSinceStartupAsDouble;
            if(swingCooldown.TryGetValue(id,out var until)&&now<until) return;
            bool serve=Rules.waitingServe && player.Team.Value==Rules.servingTeam && player.Index.Value==Rules.serverIndex && intent.kind==ShotKind.Groundstroke;
            if(Rules.waitingServe && !serve) return;
            if(!Rules.live && !serve) return;
            if(serve) intent.kind=ShotKind.Serve;
            Vector3 local=player.transform.InverseTransformPoint(Body.position);
            bool backhand=local.x<0;
            string state=ShotAnimationLibrary.State(intent.kind,backhand);
            var entry=MultiplayerApp.Instance.Settings.animations.Find(player.Gender,state);
            float duration=entry?.duration??.8f,contact=entry?.contactNormalized??.45f;
            swingCooldown[id]=now+duration;
            SwingClientRpc(player.NetworkObjectId,(int)intent.kind,backhand,duration);
            StartCoroutine(ContactAtMarker(player,intent,duration,contact,entry?.releaseNormalized??.25f));
        }
        [ClientRpc] void SwingClientRpc(ulong objectId,int kind,bool backhand,float duration)
        { if(NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId,out var obj)) obj.GetComponent<NetworkPlayer>().Swing((ShotKind)kind,backhand,duration); }
        IEnumerator ContactAtMarker(NetworkPlayer player,ShotIntent intent,float duration,float marker,float release)
        {
            double started=Time.realtimeSinceStartupAsDouble;
            Vector3 previousWrist=player.WristPosition;
            bool released=false;
            while(Time.realtimeSinceStartupAsDouble-started<duration*marker)
            {
                if(Paused.Value||Finished.Value||!player.Connected) yield break;
                if(intent.kind==ShotKind.Serve && !released && Time.realtimeSinceStartupAsDouble-started>=duration*release)
                {
                    released=true; Body.isKinematic=false; Body.position=player.LeftHand!=null?player.LeftHand.position:player.transform.position+player.transform.forward*.4f+Vector3.up*.9f;
                    Body.linearVelocity=Vector3.zero;
                }
                previousWrist=player.WristPosition; yield return null;
            }
            if(Paused.Value||Finished.Value||!player.Connected) yield break;
            Vector3 ball=Body.position,contact=player.ContactPosition;
            // Paddle reach is measured at the real contact marker; misses never teleport the ball to the paddle.
            if(Vector3.Distance(ball,contact)>.43f) { Status.Value=new FixedString512Bytes("Đánh hụt — cần đúng vị trí và thời điểm"); yield break; }
            bool legal;
            if(intent.kind==ShotKind.Serve)
            {
                var velocity=(player.WristPosition-previousWrist)/Mathf.Max(Time.deltaTime,.001f);
                legal=Rules.Serve(player.Team.Value,player.Index.Value,player.transform.position,ball,contact,player.WristPosition,velocity);
            }
            else legal=Rules.Hit(player.Team.Value,player.Index.Value,player.transform.position,ball,intent.kind);
            if(legal)
            {
                Body.isKinematic=false; Body.linearVelocity=LaunchVelocity(player,intent,ball); Body.angularVelocity=Vector3.zero;
                shotLabel=intent.kind==ShotKind.ResetVolley||intent.kind==ShotKind.SpeedUp||intent.kind==ShotKind.Punch?intent.kind+" — thử nghiệm":intent.kind==ShotKind.Drop && Rules.strokeCount==3?"Third-shot Drop":intent.kind==ShotKind.Groundstroke&&intent.attack?"Drive":intent.kind.ToString();
                shotLabelUntil=Time.realtimeSinceStartupAsDouble+1;
            }
            Publish();
        }
        Vector3 LaunchVelocity(NetworkPlayer player,ShotIntent intent,Vector3 ball)
        {
            float facing=PickleballRules.Facing(player.Team.Value);
            float x=intent.crossCourt?-Mathf.Sign(player.transform.position.x+.001f)*1.5f:player.transform.position.x*.3f;
            float z=4.8f,height=1.8f,speed=9;
            switch(intent.kind)
            {
                case ShotKind.Serve: x=-Mathf.Sign(Rules.serverWorldX)*1.5f; z=5.3f; height=2; speed=8; break;
                case ShotKind.Dink: z=1.7f; height=1.5f; speed=4; break;
                case ShotKind.Drop: case ShotKind.ResetVolley: z=1.6f; height=2.2f; speed=5.5f; break;
                case ShotKind.Volley: z=4; height=1.3f; speed=10; break;
                case ShotKind.Smash: z=4.8f; height=Mathf.Max(ball.y+.1f,1.6f); speed=14; break;
                case ShotKind.Punch: case ShotKind.SpeedUp: z=4.4f; height=1.5f; speed=12; break;
                default: z=5; height=intent.attack?1.5f:2; speed=intent.attack?13:10; break;
            }
            var target=new Vector3(Mathf.Clamp(x,-2.3f,2.3f),.04f,z*facing);
            // A ballistic arc with a fixed landing point. Faster attacks use a lower apex.
            float apex=Mathf.Max(height,ball.y+.15f),g=-Physics.gravity.y;
            float vy=Mathf.Sqrt(2*g*(apex-ball.y));
            float t=vy/g+Mathf.Sqrt(2*(apex-target.y)/g);
            var velocity=(target-ball)/Mathf.Max(t,.2f); velocity.y=vy;
            if(velocity.magnitude>18) velocity=velocity.normalized*18;
            return velocity;
        }
        void BeginRally()
        {
            Rules.NewRally(); rallyEnding=false;
            foreach(var player in players.Values) { player.Teleport(StartingPosition(player.Team.Value,player.Index.Value)); player.ResetInput(); }
            var server=players.Values.First(p=>p.Team.Value==Rules.servingTeam&&p.Index.Value==Rules.serverIndex);
            Body.isKinematic=true; Body.position=server.transform.position+server.transform.forward*.35f+Vector3.up*.85f;
            serveDeadline=Time.realtimeSinceStartupAsDouble+10;
            Publish();
        }
        void FixedUpdate()
        {
            if(!IsSpawned||Body==null || !Started.Value) return;
            if(!IsServer)
            {
                Body.position=Vector3.Lerp(Body.position,Ball.Value.position,1-Mathf.Exp(-22*Time.fixedDeltaTime)); return;
            }
            if(Paused.Value)
            {
                foreach(var r in reservations.ToArray()) if(Time.realtimeSinceStartupAsDouble>r.Value) { var p=Roster.Find(r.Key); Rules.Forfeit(p.team,"Mất kết nối quá 30 giây"); reservations.Clear(); Finish(); break; }
                return;
            }
            if(Finished.Value) return;
            if(Rules.waitingServe)
            {
                if(Time.realtimeSinceStartupAsDouble>serveDeadline) { Rules.Fault(Rules.servingTeam,"Quá 10 giây giao bóng"); Publish(); }
            }
            if(Rules.live && Body.position.y<-.4f) Rules.Fault(Rules.bounceCount>0?1-Rules.lastHitTeam:Rules.lastHitTeam,"Bóng ngoài sân");
            if(!Rules.live&&!Rules.waitingServe&&!rallyEnding)
            {
                rallyEnding=true; rallyDeadline=Time.realtimeSinceStartupAsDouble+1.5;
                Body.isKinematic=true; Publish();
            }
            if(rallyEnding && Time.realtimeSinceStartupAsDouble>=rallyDeadline && !Array.Exists(Rules.volleyMomentum,v=>v))
            { Rules.SettleRally(); if(Rules.complete) Finish(); else BeginRally(); }
            Ball.Value=new BallFrame { position=Body.position,velocity=Body.isKinematic?Vector3.zero:Body.linearVelocity,live=Rules.live };
            ScoreA.Value=Rules.score[0]; ScoreB.Value=Rules.score[1];
        }
        public void OnBallCollision(Collision collision)
        {
            if(!IsServer||Paused.Value||Finished.Value||Rules==null||!Rules.live) return;
            var surface=collision.collider.GetComponent<CourtSurface>();
            if(surface!=null && surface.kind==CourtSurface.Kind.Ground) Rules.Bounce(collision.GetContact(0).point);
            // Net contact is not an automatic fault; a legal net serve remains live.
            else if(surface==null) { var player=collision.collider.GetComponent<NetworkPlayer>(); if(player!=null) Rules.BodyContact(player.Team.Value); }
            Publish();
        }
        void Publish()
        {
            ScoreA.Value=Rules.score[0]; ScoreB.Value=Rules.score[1]; ServerTeam.Value=Rules.servingTeam; ServerIndex.Value=Rules.serverIndex; ServerNumber.Value=Rules.serverNumber; WaitingServe.Value=Rules.waitingServe;
            Status.Value=new FixedString512Bytes(Rules.live && Time.realtimeSinceStartupAsDouble<shotLabelUntil?shotLabel:Rules.reason);
        }
        public void ReserveDisconnected(ulong client)
        {
            if(!IsServer||Finished.Value) return;
            var app=MultiplayerApp.Instance;
            if(!app.ClientIdentities.TryGetValue(client,out var id)||!players.TryGetValue(id,out var player)) return;
            player.Connected=false; player.ResetInput();
            reservations[id]=Time.realtimeSinceStartupAsDouble+app.Settings.reconnectSeconds;
            if(!Paused.Value) savedVelocity=Body.isKinematic?Vector3.zero:Body.linearVelocity;
            Paused.Value=true; Body.isKinematic=true;
            Status.Value=new FixedString512Bytes("Tạm dừng — chờ người chơi kết nối lại, tối đa 30 giây");
            app.ClientIdentities.Remove(client);
        }
        void PlayerConnected(ulong client)
        {
            if(!IsServer) return;
            var app=MultiplayerApp.Instance;
            if(!app.ClientIdentities.TryGetValue(client,out var id)||!reservations.Remove(id)||!players.TryGetValue(id,out var player)) return;
            player.NetworkObject.ChangeOwnership(client); player.Connected=true; player.ResetInput();
            if(reservations.Count==0) { Paused.Value=false; Body.isKinematic=!Rules.live; if(Rules.live) Body.linearVelocity=savedVelocity; serveDeadline=Time.realtimeSinceStartupAsDouble+10; Publish(); }
        }
        public void RequestLeave() { LeaveRpc(); }
        public void PlayerLeftSession(string id)
        {
            var player=Roster?.Find(id);
            if(IsServer && !Finished.Value && player!=null)
            { Rules.Forfeit(player.team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Everyone)] void LeaveRpc(RpcParams rpc=default)
        {
            if(Finished.Value) return;
            if(MultiplayerApp.Instance.ClientIdentities.TryGetValue(rpc.Receive.SenderClientId,out var id))
            { Rules.Forfeit(Roster.Find(id).team,"Đội có người chủ động rời trận bị xử thua"); Finish(); }
        }
        void Finish()
        {
            Body.isKinematic=true; Publish();
            Status.Value=new FixedString512Bytes($"Đội {(Rules.winner==0?"A":"B")} thắng — {Rules.score[0]} : {Rules.score[1]}\n{Rules.reason}"); Finished.Value=true;
        }
        void ResultChanged(bool old,bool complete) { if(complete) MultiplayerApp.Instance.ShowResult(Status.Value.ToString()); }
    }
}
