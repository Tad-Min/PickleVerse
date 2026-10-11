using System;
using System.Collections;
using System.IO;
using System.Linq;
using PickleVerse.Settings;
using UnityEngine;

namespace PickleVerse.Audio
{
    // Opt-in development diagnostic; inert in ordinary game launches.
    public sealed class SfxDeviceCheck : MonoBehaviour
    {
        string report;
        [Serializable] sealed class Reading{public string sound;public float peak,firstObservedMs;}
        [Serializable] sealed class Result{public bool passed;public int dspBuffer,buffers,rate;public Reading[] readings;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartCheck()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            var arg=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--pkv-sfx-report="));
            if(arg==null)return;var root=new GameObject("SFX Device Check");DontDestroyOnLoad(root);root.AddComponent<SfxDeviceCheck>().report=arg.Substring("--pkv-sfx-report=".Length);
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            var saved=GameSettings.Current.Copy();var test=saved.Copy();test.master=test.effects=1;SettingsRuntime.Apply(test);
            var result=new Result{readings=new Reading[8],passed=true};AudioSettings.GetDSPBufferSize(out result.dspBuffer,out result.buffers);result.rate=AudioSettings.outputSampleRate;
            var library=Resources.Load<SfxLibrary>("Audio/Match SFX");var samples=new float[512];
            for(int i=0;i<8;i++)
            {
                var sound=(GameSfx)i;SfxPlayer.StopAll();var camera=Camera.main;var position=camera?camera.transform.position:Vector3.zero;
                SfxPlayer.Play(sound,position);
                var source=UnityEngine.Object.FindObjectsByType<AudioSource>().FirstOrDefault(s=>s.isPlaying&&s.clip==library.Clip(sound));
                var reading=new Reading{sound=sound.ToString(),firstObservedMs=-1};result.readings[i]=reading;
                if(!source){result.passed=false;continue;}
                double start=Time.realtimeSinceStartupAsDouble;source.GetOutputData(samples,0);
                for(int poll=0;poll<5;poll++)
                {
                    yield return new WaitForSecondsRealtime(.02f);source.GetOutputData(samples,0);float peak=samples.Max(x=>Mathf.Abs(x));reading.peak=Mathf.Max(reading.peak,peak);
                    if(peak>.00001f&&reading.firstObservedMs<0)reading.firstObservedMs=(float)((Time.realtimeSinceStartupAsDouble-start)*1000);
                }
                if(reading.peak<=.00001f)result.passed=false;
            }
            SfxPlayer.StopAll();SettingsRuntime.Apply(saved);Directory.CreateDirectory(Path.GetDirectoryName(report));File.WriteAllText(report,JsonUtility.ToJson(result,true));Application.Quit(result.passed?0:1);
        }
    }
}
