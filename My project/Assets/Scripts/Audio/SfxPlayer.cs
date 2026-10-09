using System;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Audio
{
    /// <summary>Preloaded, bounded voices shared by every scene. No allocation/loading on impact.</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        static SfxPlayer instance;
        const int SpatialVoices=12, RefereeVoices=4;
        AudioSource[] sources;
        GameSfx[] playing;
        double[] started;
        float[] gains;
        SfxLibrary library;
        readonly System.Random pitchRandom=new System.Random(27311);
        public static event Action<GameSfx,Vector3> Emitted;
        public static int[] PlayCounts { get; private set; } = new int[8];
        public static bool Ready => instance && instance.library;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){instance=null;PlayCounts=new int[8];Emitted=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if(instance || Application.isBatchMode)return;
            var root=new GameObject("SFX Audio");root.AddComponent<SfxPlayer>();DontDestroyOnLoad(root);
        }
        void Awake()
        {
            if(instance&&instance!=this){Destroy(gameObject);return;}instance=this;
            library=Resources.Load<SfxLibrary>("Audio/Match SFX");
            if(!library){Debug.LogError("Match SFX library is missing.");return;}
            foreach(var clip in library.clips)if(clip && clip.loadState!=AudioDataLoadState.Loaded)clip.LoadAudioData();
            sources=new AudioSource[SpatialVoices+RefereeVoices];playing=new GameSfx[sources.Length];started=new double[sources.Length];gains=new float[sources.Length];
            for(int i=0;i<sources.Length;i++)
            {
                var voice=new GameObject("SFX Voice "+i);voice.transform.SetParent(transform,false);
                var source=voice.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;source.dopplerLevel=0;
                source.spatialBlend=i<SpatialVoices?1:0;source.rolloffMode=AudioRolloffMode.Logarithmic;source.minDistance=6;source.maxDistance=40;source.reverbZoneMix=0;
                sources[i]=source;
            }
            SettingsRuntime.AudioLevelsChanged+=LevelsChanged;
            SceneManager.activeSceneChanged+=SceneChanged;
        }
        static bool Global(GameSfx sound)=>sound==GameSfx.Countdown||sound==GameSfx.FaultWhistle||sound==GameSfx.GameWhistle;
        static float Gain(GameSfx sound)=>sound==GameSfx.Footstep?.42f:sound==GameSfx.ShoeSkid?.65f:sound==GameSfx.Countdown?.28f:sound==GameSfx.GameWhistle?.6f:sound==GameSfx.FaultWhistle?.8f:.9f;
        static int Priority(GameSfx sound)=>Global(sound)?32:sound==GameSfx.Footstep||sound==GameSfx.ShoeSkid?180:64;
        public static void Play(GameSfx sound,Vector3 position=default)
        {
            if(!instance||instance.sources==null||!instance.library)return;
            var clip=instance.library.Clip(sound);if(!clip||clip.loadState!=AudioDataLoadState.Loaded)return;
            instance.Emit(sound,position,clip);
        }
        void Emit(GameSfx sound,Vector3 position,AudioClip clip)
        {
            if(sound==GameSfx.FaultWhistle||sound==GameSfx.GameWhistle)Stop(GameSfx.Countdown);
            if(sound==GameSfx.GameWhistle)Stop(GameSfx.FaultWhistle);
            int begin=Global(sound)?SpatialVoices:0,end=Global(sound)?sources.Length:SpatialVoices,index=-1;
            for(int i=begin;i<end;i++)if(!sources[i].isPlaying){index=i;break;}
            if(index<0)
            {
                for(int i=begin;i<end;i++)if(sources[i].priority>=Priority(sound)&&(index<0||started[i]<started[index]))index=i;
                if(index<0)return;
            }
            var source=sources[index];source.Stop();source.transform.position=position;source.clip=clip;source.priority=Priority(sound);
            source.pitch=Global(sound)?1:.97f+(float)pitchRandom.NextDouble()*.06f;gains[index]=Gain(sound);source.volume=gains[index]*SettingsRuntime.EffectsVolume;
            playing[index]=sound;started[index]=AudioSettings.dspTime;source.Play();PlayCounts[(int)sound]++;Emitted?.Invoke(sound,position);
        }
        public static void Stop(GameSfx sound)
        {if(instance?.sources==null)return;for(int i=0;i<instance.sources.Length;i++)if(instance.playing[i]==sound)instance.sources[i].Stop();}
        public static void StopAll(){if(instance?.sources==null)return;foreach(var source in instance.sources)if(source)source.Stop();}
        void LevelsChanged(float master,float music,float effects){if(sources==null)return;for(int i=0;i<sources.Length;i++)sources[i].volume=gains[i]*effects;}
        void SceneChanged(Scene prior,Scene next){StopAll();}
        void OnApplicationFocus(bool focused){if(!focused)StopAll();}
        void OnDestroy()
        {if(instance!=this)return;SettingsRuntime.AudioLevelsChanged-=LevelsChanged;SceneManager.activeSceneChanged-=SceneChanged;instance=null;}
    }
}
