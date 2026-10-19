using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PickleVerse.Replays;
using PickleVerse.Settings;
using Newtonsoft.Json;
public sealed class ReplayVenueFrameProbe:MonoBehaviour
{
 public bool recording;public readonly List<float> frames=new List<float>();void Update(){if(recording)frames.Add(Time.unscaledDeltaTime*1000);}
}
public static class ReplayVenuePerformance
{
 const string Report="../UnityWork/NewStadiums20261019/replay-performance.json";
 static Task task;
 public static object Start(){if(!Application.isPlaying)throw new Exception("Play first");task=Run();return new{started=true};}
 public static object Verify(){if(!Application.isPlaying)throw new Exception("Play first");task=Run(true);return new{started=true};}
 static async Task<object> Capture(ReplayVenueFrameProbe p)
 {await Task.Delay(1500);p.frames.Clear();p.recording=true;await Task.Delay(6000);p.recording=false;var a=p.frames.OrderBy(v=>v).ToArray();return new{frames=a.Length,meanFps=1000/a.Average(),p95Ms=a[(int)((a.Length-1)*.95)],maxMs=a.Last(),scale=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale};}
 static async Task Run(bool verify=false)
 {
  string report=verify?"../UnityWork/NewStadiums20261019/replay-verification.json":Report;File.WriteAllText(report,"{\"status\":\"running\"}");var probe=new GameObject("Replay Venue Probe").AddComponent<ReplayVenueFrameProbe>();UnityEngine.Object.DontDestroyOnLoad(probe.gameObject);
  string priorPlayer=ReplayLaunch.PlayerId,priorMatch=ReplayLaunch.MatchId;var priorReturn=ReplayLaunch.Return;
  try
  {
   string root=Path.Combine(Application.persistentDataPath,"Replays");var data=Directory.GetFiles(root,"metadata.json",SearchOption.AllDirectories).Select(f=>JsonUtility.FromJson<ReplayMetadata>(File.ReadAllText(f))).Where(m=>m.duration>10&&m.chunks.Count>0&&m.actors.Count>=2).OrderByDescending(m=>m.startedUtcTicks).First();ReplayLaunch.Select(data.playerId,data.matchId);
   SceneManager.LoadScene("Replay");await Task.Delay(1000);var view=UnityEngine.Object.FindAnyObjectByType<ReplayView>();view.Seek(0);if(verify&&!view.GetComponent<MatchRenderBudget>())throw new Exception("Production replay budget missing");object before=verify?null:await Capture(probe);view.Seek(0);if(!view.GetComponent<MatchRenderBudget>())view.gameObject.AddComponent<MatchRenderBudget>();var after=await Capture(probe);
   File.WriteAllText(report,JsonConvert.SerializeObject(new{status="passed",actors=data.actors.Count,view.CachedFrames,before,after,scope=verify?"Production Awake installs budget; actual saved replay playback":"Same existing recording in Editor, before/after presentation resolution budget"},Formatting.Indented));
  }
  catch(Exception ex){File.WriteAllText(report,JsonConvert.SerializeObject(new{status="failed",error=ex.ToString()},Formatting.Indented));Debug.LogException(ex);}
  finally{SceneManager.LoadScene("Menu");await Task.Delay(300);ReplayLaunch.PlayerId=priorPlayer;ReplayLaunch.MatchId=priorMatch;ReplayLaunch.Return=priorReturn;UnityEngine.Object.Destroy(probe.gameObject);}
 }
}
