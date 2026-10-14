using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using PickleVerse.Multiplayer;

// Two isolated signed-in players exercise real private Sessions and Relay in one Editor.
// No public matchmaking tickets, profile currency, or Elo are changed by this probe.
public static class ReadyCheckTransportValidation
{
    static async Task Refresh(ISession session)
    {
        for(int attempt=0;;attempt++)
        {
            try {await session.RefreshAsync();return;}
            catch(Exception e)when(attempt<3&&e.Message.Contains("429")){await Task.Delay(1100*(attempt+1));}
        }
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first");
        Directory.CreateDirectory("../UnityWork/ReadyCheckFix");
        var checks=new List<object>();Action<string,bool> check=(name,ok)=>{checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);};
        string key=Guid.NewGuid().ToString("N").Substring(0,10),matchId="ReadyQA"+Guid.NewGuid().ToString("N");
        var services=new IUnityServices[2];var managers=new NetworkManager[2];var sessions=new ISession[2];var options=new SessionOptions[2];bool running=true;
        try
        {
            for(int i=0;i<2;i++)
            {
                services[i]=UnityServices.CreateServices("Ready"+key+i);
                await services[i].InitializeAsync(new InitializationOptions().SetProfile("Ready"+key+i).SetEnvironmentName("pickleverse-test"));
                await services[i].GetAuthenticationService().SignInAnonymouslyAsync();
                var go=new GameObject("Ready QA Network "+i);managers[i]=go.AddComponent<NetworkManager>();
                var transport=go.AddComponent<UnityTransport>();
                managers[i].NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=false,ProtocolVersion=PickleVerse.Multiplayer.Services.MultiplayerSceneConfiguration.ProtocolVersion};
                var handler=(INetworkHandler)Activator.CreateInstance(typeof(MultiplayerApp).Assembly.GetType("PickleVerse.Multiplayer.SessionNetworkHandler"),new object[]{managers[i],new Func<bool>(()=>running)});
                options[i]=new SessionOptions{Name="Ready QA",Type="match",IsPrivate=true,MaxPlayers=2,PlayerProperties=new Dictionary<string,PlayerProperty>{["readyDecision"]=new PlayerProperty("pending")}}.WithNetworkHandler(handler);

            }
            sessions=await Task.WhenAll(services[0].GetMultiplayerService().CreateOrJoinSessionAsync(matchId,options[0]),services[1].GetMultiplayerService().CreateOrJoinSessionAsync(matchId,options[1]));
            for(int i=0;i<2;i++)check("player "+i+" joins confirmation session without transport",sessions[i]!=null&&!managers[i].IsListening&&sessions[i].Network.State==NetworkState.Stopped);
            check("two independent authenticated players",services[0].GetAuthenticationService().PlayerId!=services[1].GetAuthenticationService().PlayerId);
            for(int i=0;i<2;i++)await Refresh(sessions[i]);
            check("both see the same session and two players",sessions[0].Id==sessions[1].Id&&sessions[0].Players.Count==2&&sessions[1].Players.Count==2);
            for(int i=0;i<2;i++)
            {
                sessions[i].CurrentPlayer.SetProperty("readyDecision",new PlayerProperty("accepted"));await sessions[i].SaveCurrentPlayerDataAsync();
                check("player "+i+" accepts before gameplay connection",!managers[i].IsListening);
                await Task.Delay(1100);
            }
            int host=sessions[0].IsHost?0:1,guest=1-host;
            await Refresh(sessions[host]);
            check("host receives both decisions",System.Linq.Enumerable.All(sessions[host].Players,p=>MultiplayerApp.Prop(p,"readyDecision")=="accepted"));
            sessions[host].AsHost().SetProperty("readyPhase",new SessionProperty("go",VisibilityPropertyOptions.Member));await sessions[host].AsHost().SavePropertiesAsync();
            await MatchRelayConnection.StartAsync(null,o=>sessions[host].AsHost().Network.StartRelayNetworkAsync(o),()=>running);
            check("host starts transport after confirmation",managers[host].IsHost&&managers[host].IsListening);
            double deadline=Time.realtimeSinceStartupAsDouble+35,nextRefresh=0;
            while(!managers[guest].IsConnectedClient&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(Time.realtimeSinceStartupAsDouble>=nextRefresh){nextRefresh=Time.realtimeSinceStartupAsDouble+2;await Refresh(sessions[guest]);}
                await Task.Delay(100);
            }
            check("guest joins real Relay and NGO",managers[guest].IsConnectedClient&&managers[host].ConnectedClients.Count==2);
            var report=new{passed=true,count=checks.Count,checks,scope="Two actual private MPS sessions, separate Authentication registries and production SessionNetworkHandler over Relay; no public Matchmaker or two physical machines."};
            File.WriteAllText("../UnityWork/ReadyCheckFix/live-transport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        catch(Exception e)
        {
            var report=new{passed=false,checks,error=MatchRelayConnection.Describe(e),type=e.GetBaseException().GetType().Name};
            File.WriteAllText("../UnityWork/ReadyCheckFix/live-transport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally
        {
            running=false;
            for(int i=1;i>=0;i--)if(sessions[i]!=null)
            {try{if(sessions[i].IsHost)await sessions[i].AsHost().DeleteAsync();else await sessions[i].LeaveAsync();}catch{}}
            foreach(var manager in managers)if(manager){manager.Shutdown();UnityEngine.Object.Destroy(manager.gameObject);}
            foreach(var service in services)if(service!=null && service.State==ServicesInitializationState.Initialized)
            {try{service.GetAuthenticationService().SignOut(true);}catch{}}
        }
    }
}
