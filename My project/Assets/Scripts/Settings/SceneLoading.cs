using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.Settings
{
    // The screen survives Single loads, including NGO-owned scene operations.
    // No network scene is ever loaded through Unity's local SceneManager here.
    public sealed class SceneLoading:MonoBehaviour
    {
        static SceneLoading instance;
        LoadingScreenView view;AsyncOperation tracked;Coroutine flow;
        bool blocking,external,finishing;string destination;float progress;
        bool ownsPriority;UnityEngine.ThreadPriority originalPriority;
        public static bool IsBlocking=>instance&&instance.blocking;
        public static bool IsExternal=>instance&&instance.external&&instance.blocking;
        public static bool LocalRenderReady{get;private set;}
        public static string Destination=>instance?instance.destination:null;
        public static float Progress=>instance?instance.progress:0;
        public static LoadingScreenView View=>instance?instance.view:null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){instance=null;LocalRenderReady=false;}
        static SceneLoading Ensure()
        {
            if(instance)return instance;
            var go=new GameObject("Scene Loading Flow");DontDestroyOnLoad(go);instance=go.AddComponent<SceneLoading>();return instance;
        }
        public static void Attach(LoadingScreenView screen)
        {
            var runner=Ensure();if(runner.view&&runner.view!=screen){Destroy(screen.gameObject);return;}
            runner.view=screen;
        }
        public static void Track(AsyncOperation operation){if(IsBlocking)instance.tracked=operation;}
        public static bool Load(string scene,Action request=null,Func<bool> pageReady=null)
        {
            if(IsBlocking||!Application.CanStreamedLevelBeLoaded(scene))return false;
            var runner=Ensure();runner.Begin(scene,false);runner.flow=runner.StartCoroutine(runner.Local(scene,request,pageReady));return true;
        }
        void Begin(string scene,bool network)
        {
            destination=scene;external=network;blocking=true;finishing=false;LocalRenderReady=false;tracked=null;progress=0;
            if(network){originalPriority=Application.backgroundLoadingPriority;ownsPriority=true;Application.backgroundLoadingPriority=UnityEngine.ThreadPriority.Low;}
        }
        IEnumerator Show(bool loadScene)
        {
            if(!view&&loadScene&&Application.CanStreamedLevelBeLoaded("Loading"))
            {
                var loading=SceneManager.LoadSceneAsync("Loading",LoadSceneMode.Additive);while(!loading.isDone)yield return null;
                var empty=SceneManager.GetSceneByName("Loading");if(empty.IsValid()&&empty.isLoaded)yield return SceneManager.UnloadSceneAsync(empty);
            }
            if(!view)
            {
                var prefab=Resources.Load<LoadingScreenView>("Loading/LoadingScreen");
                if(!prefab){Debug.LogError("Loading screen prefab is missing.");blocking=false;yield break;}
                Instantiate(prefab);
            }
            view.Begin(destination);Set(0,"Chuẩn bị vào sân");yield return null;yield return null;
        }
        IEnumerator Local(string scene,Action request,Func<bool> ready)
        {
            yield return Show(true);if(!blocking)yield break;
            var previousPriority=Application.backgroundLoadingPriority;Application.backgroundLoadingPriority=UnityEngine.ThreadPriority.Low;
            try
            {
                if(request==null)
                {
                    tracked=SceneManager.LoadSceneAsync(scene);tracked.allowSceneActivation=false;
                    while(tracked.progress<.9f){Set(.8f*tracked.progress/.9f,"Đang tải sân và nhân vật");yield return null;}
                    Set(.8f,"Đang chuẩn bị hình ảnh");tracked.allowSceneActivation=true;while(!tracked.isDone)yield return null;
                }
                else
                {
                    request();
                    while(SceneManager.GetActiveScene().name!=scene||ready!=null&&!ready())
                    {Set(tracked!=null?.8f*Mathf.Clamp01(tracked.progress/.9f):.1f,"Đang tải sân và nhân vật");yield return null;}
                }
                yield return Warm(scene,null);
                yield return Finish();
            }
            finally{Application.backgroundLoadingPriority=previousPriority;}
        }
        public static void BeginExternal(string scene)
        {
            if(IsBlocking&&instance.external&&instance.destination==scene)return;
            if(IsBlocking)return;
            var runner=Ensure();runner.Begin(scene,true);runner.flow=runner.StartCoroutine(runner.Show(false));
        }
        public static void ExternalProgress(float amount,string text){if(IsExternal)instance.Set(amount,text);}
        public static IEnumerator PrepareNetworkCourt(Func<bool> synchronized)
        {
            if(!IsExternal)BeginExternal("MultiplayerCourt");
            while(IsExternal&&!instance.view)yield return null;
            if(IsExternal)yield return instance.Warm("MultiplayerCourt",synchronized);
        }
        IEnumerator Warm(string scene,Func<bool> synchronized)
        {
            var gate=new SceneWarmup();Set(.82f,"Đang ổn định hình ảnh");yield return null;
            // Keep the destination cameras rendering under the opaque overlay.
            // UI and gameplay remain blocked; this warms real materials/avatars.
            while(blocking&&SceneManager.GetActiveScene().name==scene)
            {
                yield return null;gate.Observe(Time.unscaledDeltaTime);
                bool networkReady=synchronized==null||synchronized();
                Set(.82f+.14f*gate.Progress,networkReady?"Đang ổn định hình ảnh":"Đang đồng bộ người chơi");
                if(gate.Ready(GameSettings.Current.fps)&&networkReady)break;
                // Re-evaluate a moving window rather than revealing on a timer.
            }
            if(!blocking)yield break;LocalRenderReady=true;
            Set(.97f,external?"Chờ tất cả người chơi sẵn sàng":"Sân đấu đã sẵn sàng");
        }
        public static void CompleteExternal(){if(IsExternal&&!instance.finishing)instance.flow=instance.StartCoroutine(instance.Finish());}
        IEnumerator Finish()
        {
            if(finishing)yield break;finishing=true;Set(1,"Sẵn sàng vào sân");
            while(view&&view.DisplayedProgress<.9999f)yield return null;
            yield return new WaitForSecondsRealtime(.18f);
            for(float t=0;t<.25f;t+=Time.unscaledDeltaTime){if(view)view.group.alpha=1-Mathf.SmoothStep(0,1,t/.25f);yield return null;}
            Cleanup();
        }
        void Set(float amount,string text){progress=Mathf.Max(progress,Mathf.Clamp01(amount));if(view)view.SetProgress(progress,text);}
        void Update()
        {if(external&&blocking&&!LocalRenderReady&&tracked!=null&&!tracked.isDone)Set(.1f+.7f*Mathf.Clamp01(tracked.progress/.9f),"Đang tải sân và nhân vật");}
        public static void CancelExternal(){if(IsExternal)instance.Cancel();}
        void Cancel(){StopAllCoroutines();Cleanup();}
        void Cleanup(){blocking=external=finishing=false;tracked=null;flow=null;destination=null;if(ownsPriority){Application.backgroundLoadingPriority=originalPriority;ownsPriority=false;}if(view)Destroy(view.gameObject);view=null;}
        void OnEnable(){SceneManager.activeSceneChanged+=ActiveSceneChanged;}
        void OnDisable(){SceneManager.activeSceneChanged-=ActiveSceneChanged;if(ownsPriority)Application.backgroundLoadingPriority=originalPriority;if(instance==this)instance=null;}
        void ActiveSceneChanged(Scene previous,Scene current)
        {if(external&&blocking&&previous.name==destination&&current.name!=destination)Cancel();}
    }
}
