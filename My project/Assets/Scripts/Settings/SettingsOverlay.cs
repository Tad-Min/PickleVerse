using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace PickleVerse.Settings
{
    public interface ISettingsHost
    {
        bool IsGameplay { get; }
        bool IsPaused { get; }
        bool CanSignOut { get; }
        void SetPaused(bool paused);
        void LeaveToMenu();
        Task SignOutAsync();
    }

    public static class SettingsOverlay
    {
        public const string SceneName="Settings";
        public static bool IsOpen { get; private set; }
        public static bool Loading { get; private set; }
        public static ISettingsHost Host { get; private set; }
        static MonoBehaviour owner;
        static bool previousPause;
        static GameObject previousSelection;
        static Canvas[] coveredCanvases=Array.Empty<Canvas>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){IsOpen=Loading=false;Host=null;owner=null;coveredCanvases=Array.Empty<Canvas>();}
        public static async void Open(ISettingsHost host=null)
        {
            if(IsOpen||Loading)return;
            host=host??SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>()).OfType<ISettingsHost>().FirstOrDefault();
            Host=host;owner=host as MonoBehaviour;previousPause=host?.IsPaused??false;
            previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            coveredCanvases=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>()).Where(c=>c.enabled).ToArray();
            foreach(var canvas in coveredCanvases)canvas.enabled=false;
            host?.SetPaused(true);IsOpen=Loading=true;
            try
            {
                var load=SceneManager.LoadSceneAsync(SceneName,LoadSceneMode.Additive);
                while(!load.isDone)await Task.Yield();
            }
            catch(Exception e)
            {
                Debug.LogError("Unable to open Settings: "+e.Message);IsOpen=false;RestoreCanvases();if(owner)host?.SetPaused(previousPause);
            }
            finally{Loading=false;}
        }
        public static void AttachStandalone(){if(!IsOpen){IsOpen=true;Host=null;owner=null;}}
        public static async void Close(bool forceResume=false)
        {
            if(Loading)return;
            Loading=true;var host=Host;var context=owner;
            var scene=SceneManager.GetSceneByName(SceneName);
            if(scene.IsValid()&&scene.isLoaded)
            {
                var unload=SceneManager.sceneCount==1?SceneManager.LoadSceneAsync("Menu"):SceneManager.UnloadSceneAsync(scene);
                while(unload!=null&&!unload.isDone)await Task.Yield();
            }
            IsOpen=Loading=false;Host=null;owner=null;
            RestoreCanvases();
            if(context)host.SetPaused(forceResume?false:previousPause);
            if(EventSystem.current&&previousSelection&&previousSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        public static void NotifyDestroyed()
        {
            if(Loading)return;
            if(owner)Host?.SetPaused(previousPause);RestoreCanvases();IsOpen=false;Host=null;owner=null;
        }
        static void RestoreCanvases(){foreach(var canvas in coveredCanvases)if(canvas)canvas.enabled=true;coveredCanvases=Array.Empty<Canvas>();}
    }

}
