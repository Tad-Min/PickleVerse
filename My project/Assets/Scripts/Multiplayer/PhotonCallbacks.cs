using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed class PhotonCallbacks : MonoBehaviour,INetworkRunnerCallbacks
    {
        public Action<NetworkRunner,PlayerRef> Joined,Left;
        public Action<NetworkRunner> SceneStarted,SceneDone;
        public Action<NetworkRunner,ShutdownReason> Stopped;
        public List<SessionInfo> Sessions=new List<SessionInfo>();
        public bool HasSessionList;
        public Action<PlayerRef,byte[]> Reliable;
        public void OnPlayerJoined(NetworkRunner runner,PlayerRef player)=>Joined?.Invoke(runner,player);
        public void OnPlayerLeft(NetworkRunner runner,PlayerRef player)=>Left?.Invoke(runner,player);
        public void OnShutdown(NetworkRunner runner,ShutdownReason reason)=>Stopped?.Invoke(runner,reason);
        public void OnSceneLoadStart(NetworkRunner runner)=>SceneStarted?.Invoke(runner);
        public void OnSceneLoadDone(NetworkRunner runner)=>SceneDone?.Invoke(runner);
        public void OnSessionListUpdated(NetworkRunner runner,List<SessionInfo> sessions){Sessions=sessions;HasSessionList=true;}
        public void OnObjectExitAOI(NetworkRunner runner,NetworkObject obj,PlayerRef player){}
        public void OnObjectEnterAOI(NetworkRunner runner,NetworkObject obj,PlayerRef player){}
        public void OnInput(NetworkRunner runner,NetworkInput input){}
        public void OnInputMissing(NetworkRunner runner,PlayerRef player,NetworkInput input){}
        public void OnConnectedToServer(NetworkRunner runner){}
        public void OnDisconnectedFromServer(NetworkRunner runner,NetDisconnectReason reason){}
        public void OnConnectRequest(NetworkRunner runner,NetworkRunnerCallbackArgs.ConnectRequest request,byte[] token)=>request.Accept();
        public void OnConnectFailed(NetworkRunner runner,NetAddress remoteAddress,NetConnectFailedReason reason){}
        public void OnUserSimulationMessage(NetworkRunner runner,SimulationMessagePtr message){}
        public void OnReliableDataReceived(NetworkRunner runner,PlayerRef player,ReliableKey key,ReadOnlySpan<byte> data){if(data.Length<=16384)Reliable?.Invoke(player,data.ToArray());}
        public void OnReliableDataProgress(NetworkRunner runner,PlayerRef player,ReliableKey key,float progress){}
        public void OnCustomAuthenticationResponse(NetworkRunner runner,Dictionary<string,object> data){}
        public void OnHostMigration(NetworkRunner runner,HostMigrationToken token){}
    }
}
