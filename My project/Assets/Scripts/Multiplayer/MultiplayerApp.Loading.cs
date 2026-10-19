using System.Collections;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;

namespace PickleVerse.Multiplayer
{
    public sealed partial class MultiplayerApp
    {
        NetworkSceneManager observedSceneManager;
        public bool CourtSceneSynchronized{get;private set;}=true;
        void TrackLoadingSceneEvents()
        {
            var manager=Network?Network.SceneManager:null;if(manager==observedSceneManager)return;
            if(observedSceneManager!=null)observedSceneManager.OnSceneEvent-=LoadingSceneEvent;
            observedSceneManager=manager;
            if(manager!=null)manager.OnSceneEvent+=LoadingSceneEvent;
        }
        IEnumerator LoadCourtWithCover()
        {
            TrackLoadingSceneEvents();CourtSceneSynchronized=false;SceneLoading.BeginExternal("MultiplayerCourt");
            yield return null;yield return null;
            var result=Network.SceneManager.LoadScene("MultiplayerCourt",LoadSceneMode.Single);
            if(result!=SceneEventProgressStatus.Started){SceneLoading.CancelExternal();courtLoading=false;SetNotice("Chưa thể tải sân. Hãy thử kết nối lại.");}
        }
        void LoadingSceneEvent(SceneEvent sceneEvent)
        {
            // Synchronization events do not carry a court SceneName on reconnect.
            if(sceneEvent.SceneEventType==SceneEventType.SynchronizeComplete&&sceneEvent.ClientId==Network.LocalClientId)
            {CourtSceneSynchronized=true;return;}
            if(sceneEvent.SceneName!="MultiplayerCourt")return;
            switch(sceneEvent.SceneEventType)
            {
                case SceneEventType.Load:
                    CourtSceneSynchronized=false;SceneLoading.BeginExternal("MultiplayerCourt");
                    SceneLoading.Track(sceneEvent.AsyncOperation);
                    SceneLoading.ExternalProgress(.1f,"Đang tải sân và nhân vật");break;
                case SceneEventType.LoadComplete:
                    if(sceneEvent.ClientId==Network.LocalClientId)SceneLoading.ExternalProgress(.8f,"Đang chuẩn bị hình ảnh");break;
                case SceneEventType.LoadEventCompleted:
                    CourtSceneSynchronized=true;break;
            }
        }
        void OnDestroy()
        {if(observedSceneManager!=null)observedSceneManager.OnSceneEvent-=LoadingSceneEvent;if(Instance==this)SceneLoading.CancelExternal();}
    }
}
