using System;
using System.Collections;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Audio
{
    /// <summary>One persistent stereo playlist, at most two streaming voices during a crossfade.</summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance {get;private set;}
        public MusicLibrary Library {get;private set;}
        public int CurrentIndex {get;private set;}
        public MusicTrack Track=>Library&&Library.tracks.Length>0?Library.tracks[CurrentIndex]:null;
        public bool Allowed {get;private set;}
        public bool UserPaused {get;private set;}
        public bool Loading {get;private set;}
        public bool Playing=>Allowed&&!UserPaused&&!appPaused&&sources!=null&&sources[primary].isPlaying;
        public string Error {get;private set;}="";
        public float Position=>sources!=null&&sources[primary].clip==Track?.clip?sources[primary].time:savedPosition;
        public float Duration=>Track?.clip?Track.clip.length:0;
        public event Action Changed;
        AudioSource[] sources;
        float[] weights=new float[2];
        int primary;
        float musicVolume,savedPosition;
        bool fading,appPaused;
        double scheduledAt;
        MusicUI view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {SceneManager.activeSceneChanged-=SceneChanged;Instance=null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap(){if(!Application.isBatchMode)SceneManager.activeSceneChanged+=SceneChanged;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitialScene(){if(!Application.isBatchMode)SceneChanged(default,SceneManager.GetActiveScene());}
        static void SceneChanged(Scene before,Scene next)
        {
            if(MusicScenePolicy.Allows(next.name)&&!Instance)
                new GameObject("Front End Music").AddComponent<MusicPlayer>();
            if(Instance)Instance.ApplyScene(next.name);
        }
        void Awake()
        {
            if(Instance&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            Library=Resources.Load<MusicLibrary>("Audio/Front End Music");
            if(!Library||Library.tracks.Length==0){Debug.LogError("Front-end music library missing.");return;}
            musicVolume=GameSettings.Current.music;
            sources=new AudioSource[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Music Voice "+i);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();
                source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;source.reverbZoneMix=0;source.priority=128;source.volume=0;
                sources[i]=source;
            }
            SettingsRuntime.AudioLevelsChanged+=LevelsChanged;
            if(Library.uiPrefab){var ui=Instantiate(Library.uiPrefab,transform,false);view=ui.GetComponent<MusicUI>();view.Bind(this);}
        }
        void ApplyScene(string scene)
        {
            bool wasAllowed=Allowed;Allowed=MusicScenePolicy.Allows(scene);
            if(!Allowed)
            {
                savedPosition=Position;StopAllCoroutines();Loading=fading=false;
                if(sources!=null)foreach(var source in sources){source.Stop();source.clip=null;}
                weights[0]=weights[1]=0;
            }
            else if(!wasAllowed&&!UserPaused&&sources!=null)StartTrack(CurrentIndex,false);
            if(view)view.ApplyScene(scene);
            Changed?.Invoke();
        }
        public void Select(int index)
        {if(!Allowed||index<0||index>=Library.tracks.Length)return;UserPaused=false;StartTrack(index,true);}
        public void Next(){Select(MusicScenePolicy.Wrap(CurrentIndex+1,Library.tracks.Length));}
        public void Previous(){Select(MusicScenePolicy.Wrap(CurrentIndex-1,Library.tracks.Length));}
        public void TogglePause()
        {
            if(!Allowed||sources==null)return;
            if(!UserPaused)
            {
                savedPosition=Position;StopAllCoroutines();Loading=fading=false;UserPaused=true;
                for(int i=0;i<2;i++)if(i==primary)sources[i].Pause();else{sources[i].Stop();sources[i].clip=null;}
                weights[primary]=1;weights[1-primary]=0;ApplyLevels();
            }
            else
            {
                UserPaused=false;
                if(sources[primary].clip==Track.clip){sources[primary].UnPause();scheduledAt=AudioSettings.dspTime;}
                else StartTrack(CurrentIndex,false);
            }
            Changed?.Invoke();
        }
        public void Seek(float fraction)
        {
            if(!Allowed||Loading||Duration<=0)return;
            savedPosition=Mathf.Clamp01(fraction)*Mathf.Max(0,Duration-.15f);
            if(sources[primary].clip==Track.clip)sources[primary].time=savedPosition;
            Changed?.Invoke();
        }
        void StartTrack(int index,bool restart)
        {
            StopAllCoroutines();Loading=true;fading=false;Error="";
            if(restart)savedPosition=0;CurrentIndex=index;
            sources[1-primary].Stop();sources[1-primary].clip=null;weights[1-primary]=0;weights[primary]=sources[primary].isPlaying?1:0;
            ApplyLevels();Changed?.Invoke();StartCoroutine(PrepareAndPlay());
        }
        IEnumerator PrepareAndPlay()
        {
            var clip=Track.clip;
            if(!clip){Fail("Không tìm thấy bài nhạc.");yield break;}
            if(clip.loadState!=AudioDataLoadState.Loaded)clip.LoadAudioData();
            float deadline=Time.realtimeSinceStartup+10;
            while(clip.loadState!=AudioDataLoadState.Loaded)
            {
                if(clip.loadState==AudioDataLoadState.Failed||Time.realtimeSinceStartup>deadline){Fail("Không thể tải bài nhạc. Hãy chọn bài khác.");yield break;}
                yield return null;
            }
            if(!Allowed||UserPaused)yield break;
            int outgoing=primary,incoming=sources[primary].clip?1-primary:primary;
            var next=sources[incoming];next.clip=clip;next.time=Mathf.Clamp(savedPosition,0,Mathf.Max(0,clip.length-.15f));
            weights[incoming]=0;ApplyLevels();scheduledAt=AudioSettings.dspTime+.04;next.PlayScheduled(scheduledAt);
            while(AudioSettings.dspTime<scheduledAt)yield return null;
            primary=incoming;Loading=false;fading=true;Changed?.Invoke();
            float elapsed=0;
            while(elapsed<Library.crossfade)
            {
                elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/Library.crossfade);
                if(outgoing!=incoming)weights[outgoing]=1-t;weights[incoming]=t;ApplyLevels();yield return null;
            }
            weights[incoming]=1;
            if(outgoing!=incoming){sources[outgoing].Stop();sources[outgoing].clip=null;weights[outgoing]=0;}
            fading=false;ApplyLevels();Changed?.Invoke();
        }
        void Fail(string error)
        {Loading=false;UserPaused=true;Error=error;foreach(var source in sources){source.Stop();source.clip=null;}weights[0]=weights[1]=0;Changed?.Invoke();}
        void Update()
        {
            if(Allowed&&!UserPaused&&!appPaused&&!Loading&&!fading&&sources!=null&&sources[primary].clip&&!sources[primary].isPlaying&&AudioSettings.dspTime>scheduledAt+.2)Next();
        }
        void LevelsChanged(float master,float music,float effects){musicVolume=music;ApplyLevels();Changed?.Invoke();}
        void ApplyLevels(){if(sources==null)return;for(int i=0;i<2;i++)sources[i].volume=weights[i]*Library.gain*musicVolume;}
        void OnApplicationPause(bool pause)
        {
            appPaused=pause;if(sources==null)return;
            foreach(var source in sources)if(pause)source.Pause();else if(Allowed&&!UserPaused)source.UnPause();
        }
        void OnDestroy()
        {
            if(Instance!=this)return;SettingsRuntime.AudioLevelsChanged-=LevelsChanged;Instance=null;
        }
    }
}
