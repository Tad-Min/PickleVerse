using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace PickleVerse.Editor
{
    public static class PhotonCloudCheck
    {
        [Serializable] public sealed class Report
        {
            public bool completed,passed,firstConnected,secondConnected;
            public int firstPlayers,secondPlayers;
            public string region,error;
        }
        public static Report Result { get; private set; }
        public static async Task RunAsync()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
            Result=new Report();NetworkRunner first=null,second=null;
            float timeScale=Time.timeScale;Time.timeScale=1;
            try
            {
                first=new GameObject("Photon Cloud Check A").AddComponent<NetworkRunner>();
                var settings=PhotonAppSettings.Global.AppSettings.GetCopy();
                string room="pkv-check-"+Guid.NewGuid().ToString("N");
                var joined=await first.StartGame(new StartGameArgs{GameMode=GameMode.Shared,SessionName=room,
                    PlayerCount=2,IsVisible=false,CustomPhotonAppSettings=settings});
                Result.firstConnected=joined.Ok;
                if(!joined.Ok)throw new InvalidOperationException("First connection: "+joined.ShutdownReason);
                settings=settings.GetCopy();settings.FixedRegion=first.SessionInfo.Region;
                Result.region=settings.FixedRegion;
                second=new GameObject("Photon Cloud Check B").AddComponent<NetworkRunner>();
                joined=await second.StartGame(new StartGameArgs{GameMode=GameMode.Shared,SessionName=room,
                    PlayerCount=2,IsVisible=false,CustomPhotonAppSettings=settings});
                Result.secondConnected=joined.Ok;
                if(!joined.Ok)throw new InvalidOperationException("Second connection: "+joined.ShutdownReason);
                double until=Time.realtimeSinceStartupAsDouble+20;
                while((first.ActivePlayers.Count()!=2||second.ActivePlayers.Count()!=2)&&Time.realtimeSinceStartupAsDouble<until)await Task.Delay(100);
                Result.firstPlayers=first.ActivePlayers.Count();Result.secondPlayers=second.ActivePlayers.Count();
                Result.passed=Result.firstPlayers==2&&Result.secondPlayers==2&&first.SessionInfo.Name==second.SessionInfo.Name;
            }
            catch(Exception error){Result.error=error.Message;}
            finally
            {
                if(second){await second.Shutdown();if(second)UnityEngine.Object.Destroy(second.gameObject);}
                if(first){await first.Shutdown();if(first)UnityEngine.Object.Destroy(first.gameObject);}
                Time.timeScale=timeScale;Result.completed=true;
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/verification/photon-cloud-check.json")),JsonUtility.ToJson(Result,true));
            }
        }
    }
}
