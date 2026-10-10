using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using PickleVerse.Shop;
using PickleVerse.Multiplayer;

public static class PaddleNetworkValidation
{
    sealed class Memory:IEconomyStorage{public string json;public string Read()=>json;public void Write(string value){json=value;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();var real=PlayerEconomy.Service;
        string stored=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");var state=new PlayerEconomyState{equippedPaddle="paddle.gemstone"};state.owned.Add("paddle.gemstone");
        PlayerEconomy.Configure(new LocalPlayerEconomy(PlayerEconomy.Catalog,new Memory{json=JsonUtility.ToJson(state)}));
        var app=MultiplayerApp.Ensure();app.enabled=false;var manager=app.Network;
        if(manager.IsListening)throw new Exception("Existing online session must not be changed.");
        manager.NetworkConfig.ConnectionApproval=false;manager.NetworkConfig.EnableSceneManagement=false;
        manager.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1",17894,"127.0.0.1");
        try
        {
            Check("Loopback host starts without cloud",manager.StartHost());await Task.Delay(120);
            var go=UnityEngine.Object.Instantiate(app.Settings.playerPrefab);var player=go.GetComponent<NetworkPlayer>();
            player.Initialize(new RoomMember{id="fixture-owner",gender="Female",team=0},0,new Vector3(0,0,-6));
            go.GetComponent<NetworkObject>().SpawnWithOwnership(manager.LocalClientId);await Task.Delay(120);
            Check("Owner publishes equipped id through server RPC",player.PaddleSkin.Value.ToString()=="paddle.gemstone");
            Check("Network female renders owned paddle",player.Actor.paddle.GetComponent<PaddleAppearance>().CurrentId=="paddle.gemstone");
            var contact=player.PaddleContact;
            // A server-authorized update follows the same NetworkVariable change callback as replicas.
            player.PaddleSkin.Value=new FixedString64Bytes("paddle.thot");await Task.Delay(50);
            Check("Replicated change updates model",player.Actor.paddle.GetComponent<PaddleAppearance>().CurrentId=="paddle.thot");
            typeof(NetworkPlayer).GetMethod("SelectPaddleServerRpc",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(player,new object[]{new FixedString64Bytes("invalid-skin")});
            Check("Unknown skin id is rejected by server",player.PaddleSkin.Value.ToString()=="paddle.thot");
            player.Teleport(new Vector3(0,0,6));Check("End change preserves skin and contact",player.PaddleContact==contact&&player.Actor.paddle.GetComponent<PaddleAppearance>().CurrentId=="paddle.thot");
            go.GetComponent<NetworkObject>().Despawn();await Task.Delay(50);
            var male=UnityEngine.Object.Instantiate(app.Settings.playerPrefab);var other=male.GetComponent<NetworkPlayer>();other.Initialize(new RoomMember{id="fixture-male",gender="Male",team=0},0,new Vector3(0,0,-6));male.GetComponent<NetworkObject>().SpawnWithOwnership(manager.LocalClientId);await Task.Delay(100);
            Check("Network male owner uses same equipped skin",!other.Female.Value&&other.Actor.paddle.GetComponent<PaddleAppearance>().CurrentId=="paddle.gemstone");male.GetComponent<NetworkObject>().Despawn();
            Check("Protocol distinguishes new replicated layout",PickleVerse.Multiplayer.Services.MultiplayerSceneConfiguration.ProtocolVersion==4);
            Check("Real wallet untouched by network test",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==stored);
            var report=new{passed=true,count=checks.Count,scope="Local NGO host and RPC/NetworkVariable callbacks; no two-client Internet test",checks};Directory.CreateDirectory("../UnityWork/Collection");File.WriteAllText("../UnityWork/Collection/network-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{if(manager.IsListening)manager.Shutdown();await Task.Delay(120);UnityEngine.Object.Destroy(manager.gameObject);UnityEngine.Object.Destroy(app.gameObject);PlayerEconomy.Configure(real);}
    }
}
