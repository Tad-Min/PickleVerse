#if !UNITY_SERVER
using System;
using PickleVerse.Progression;
using PickleVerse.Services;
using PickleVerse.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickleVerse.FrontEnd
{
    /// <summary>Account choice, cached training entry and durable-sync feedback.</summary>
    public sealed class TrainingAccessOverlay : MonoBehaviour
    {
        GameObject viewRoot;
        RectTransform loginEntry, banner, modal;
        TMP_Text status, title, detail;
        UnityEngine.UI.Button newcomer, experienced, previous, next, close, sync, failures;
        TMP_FontAsset font;
        bool busy, showingFailures;
        int failureIndex;
        string tutorialOwner, lastMessage;
        float messageUntil;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => new GameObject("Training Access").AddComponent<TrainingAccessOverlay>();
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += Loaded;
        }
        void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode==LoadSceneMode.Additive && scene!=SceneManager.GetActiveScene()) return;
            FrontEndModalInput.Release(this);if(viewRoot)Destroy(viewRoot);viewRoot=null;
            if(scene.name!="Login" && scene.name!="Menu" && scene.name!="Training" && scene.name!="Tutorial")return;
            font=RuntimeForms.Font();viewRoot=RuntimeForms.Canvas("Training Access View",1400);SceneManager.MoveGameObjectToScene(viewRoot,scene);Build();
        }
        void Build()
        {
            loginEntry = Box("Offline Entry", viewRoot.transform, new Vector2(1,0), new Vector2(1,0), new Vector2(350,58), new Vector2(-28,28), new Color(.06f,.11f,.14f,1));
            Button("Open Offline",loginEntry,"Luyện tập ngoại tuyến",()=> {
                var runtime = ProgressionRuntime.Instance;
                if (runtime.TryEnterOffline()) SceneLoading.Load("Menu");
            }, new Vector2(0,0),new Vector2(1,1));
            banner = Box("Training Sync",viewRoot.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(1120,66),new Vector2(0,-90),new Color(.04f,.1f,.13f,.97f));
            status = Text("Status",banner,"",18,new Vector2(.02f,.08f),new Vector2(.68f,.92f));
            sync = Button("Sync",banner,"Đồng bộ",Sync,new Vector2(.7f,.15f),new Vector2(.84f,.85f));
            failures = Button("Failures",banner,"Chi tiết",()=> { showingFailures=true;failureIndex=0; },new Vector2(.85f,.15f),new Vector2(.98f,.85f));
            modal = Box("Onboarding",viewRoot.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(720,390),Vector2.zero,new Color(.035f,.07f,.095f,1));
            // Full-screen raycast cover belongs to this modal, while its content remains centred.
            var cover = new GameObject("Cover",typeof(RectTransform),typeof(UnityEngine.UI.Image)); cover.transform.SetParent(viewRoot.transform,false);
            var coverRect=(RectTransform)cover.transform; coverRect.anchorMin=Vector2.zero;coverRect.anchorMax=Vector2.one;coverRect.offsetMin=coverRect.offsetMax=Vector2.zero;
            cover.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.78f); modal.SetParent(cover.transform,false);
            modal.anchorMin=modal.anchorMax=new Vector2(.5f,.5f);modal.pivot=new Vector2(.5f,.5f);modal.anchoredPosition=Vector2.zero;
            title=Text("Title",modal,"Làm quen với PickleVerse",32,new Vector2(.06f,.77f),new Vector2(.94f,.94f));
            detail=Text("Detail",modal,"",22,new Vector2(.06f,.32f),new Vector2(.94f,.74f));
            newcomer=Button("Newcomer",modal,"Người mới",()=>Choose(true),new Vector2(.06f,.08f),new Vector2(.48f,.25f));
            experienced=Button("Experienced",modal,"Đã thành thạo",()=>Choose(false),new Vector2(.52f,.08f),new Vector2(.94f,.25f));
            previous=Button("Previous",modal,"Trước",()=>failureIndex--,new Vector2(.06f,.08f),new Vector2(.3f,.25f));
            next=Button("Next",modal,"Sau",()=>failureIndex++,new Vector2(.34f,.08f),new Vector2(.58f,.25f));
            close=Button("Close",modal,"Đóng",()=>showingFailures=false,new Vector2(.64f,.08f),new Vector2(.94f,.25f));
            cover.SetActive(false); loginEntry.gameObject.SetActive(false); banner.gameObject.SetActive(false);
        }
        void Update()
        {
            if(!viewRoot)return;
            var runtime=ProgressionRuntime.Instance;if (!runtime) return;
            string scene=SceneManager.GetActiveScene().name;
            loginEntry.gameObject.SetActive(scene=="Login" && runtime.HasOfflineProfile && !SceneLoading.IsBlocking);
            bool playable=scene=="Menu" || scene=="Training" || scene=="Tutorial";
            bool needsChoice=scene=="Menu" && runtime.Ready && runtime.Snapshot.catalog.contentSchemaVersion>=2 && string.IsNullOrEmpty(runtime.Snapshot.experienceChoice);
            bool showModal=playable && !SceneLoading.IsBlocking && (needsChoice || showingFailures);
            if (showModal && !FrontEndModalInput.IsOwnedBy(this)) showModal=FrontEndModalInput.TryAcquire(this);
            modal.parent.gameObject.SetActive(showModal);
            if (!showModal) FrontEndModalInput.Release(this);
            if (needsChoice)
            {
                title.text="Bạn đã quen với pickleball chưa?";
                if (!busy) detail.text="Người mới sẽ được hướng dẫn luật và điều khiển. Nếu đã thành thạo, bạn có thể vào sảnh ngay.";
            }
            else if (showingFailures && runtime.SyncFailures.Count>0)
            {
                failureIndex=Mathf.Clamp(failureIndex,0,runtime.SyncFailures.Count-1);
                var failure=runtime.SyncFailures[failureIndex];title.text="Chi tiết đồng bộ · "+(failureIndex+1)+"/"+runtime.SyncFailures.Count;
                var lesson=failure.attempt?.definition ?? TrainingCurriculum.Find(failure.attempt?.lessonId);
                detail.text=(failure.attempt!=null?(lesson?.title??"Lượt luyện tập")+"\n":"")+failure.message;
                previous.interactable=failureIndex>0;next.interactable=failureIndex+1<runtime.SyncFailures.Count;
            }
            else if (showingFailures) { title.text="Đồng bộ tiến trình";detail.text=runtime.SyncMessage??"Không có lượt bị từ chối."; }
            newcomer.gameObject.SetActive(needsChoice);experienced.gameObject.SetActive(needsChoice);
            previous.gameObject.SetActive(!needsChoice);next.gameObject.SetActive(!needsChoice);close.gameObject.SetActive(!needsChoice);
            newcomer.interactable=experienced.interactable=!busy;
            if (runtime.SyncMessage!=lastMessage) { lastMessage=runtime.SyncMessage;messageUntil=Time.unscaledTime+10; }
            bool visible=playable && runtime.Ready && (runtime.IsOffline || runtime.PendingOperations>0 || runtime.SyncFailures.Count>0 || !string.IsNullOrEmpty(lastMessage)&&Time.unscaledTime<messageUntil);
            banner.gameObject.SetActive(visible && !SceneLoading.IsBlocking);
            status.text=runtime.IsOffline?"Đang luyện tập ngoại tuyến · "+runtime.PendingOperations+" mục chờ đồng bộ":runtime.PendingOperations>0?runtime.PendingOperations+" mục chờ xác nhận · XP và sao đang tạm tính":runtime.SyncMessage??"Tiến trình đã đồng bộ.";
            sync.GetComponentInChildren<TMP_Text>().text=AccountRuntime.Ensure().Account.IsSignedIn?"Đồng bộ":"Đăng nhập";
            failures.interactable=runtime.SyncFailures.Count>0 || !string.IsNullOrEmpty(runtime.SyncMessage);
            if (scene=="Menu" && runtime.Ready && !showModal && !SceneLoading.IsBlocking && runtime.Snapshot.experienceChoice=="NEW" && !runtime.Snapshot.tutorial.completed && !runtime.Snapshot.tutorial.skipped)
            {
                string owner=runtime.Player.id+":"+AccountRuntime.Ensure().Account.SessionVersion;
                if (tutorialOwner!=owner) { tutorialOwner=owner;SceneLoading.Load("Tutorial"); }
            }
        }
        async void Choose(bool isNew)
        {
            if (busy) return;busy=true;detail.text="Đang lưu lựa chọn…";
            try
            {
                await ProgressionRuntime.Instance.SetExperienceAsync(isNew);
                if (!this) return;
                if (isNew) { tutorialOwner=ProgressionRuntime.Instance.Player.id+":"+AccountRuntime.Ensure().Account.SessionVersion;FrontEndModalInput.Release(this);SceneLoading.Load("Tutorial"); }
            }
            catch (Exception error) { if (this) detail.text=error.Message; }
            finally { busy=false; }
        }
        async void Sync()
        {
            if (!AccountRuntime.Ensure().Account.IsSignedIn) { ProgressionRuntime.Instance.ExitOffline();SceneLoading.Load("Login");return; }
            try { await ProgressionRuntime.Instance.SyncOfflineAsync(); }
            catch (Exception error) { Debug.LogWarning("[Training] Sync pending ("+error.GetType().Name+")."); }
        }
        RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;
            go.GetComponent<UnityEngine.UI.Image>().color=color;return rect;
        }
        TMP_Text Text(string name,Transform parent,string value,int size,Vector2 min,Vector2 max)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.font=font;text.fontSize=size;text.color=Color.white;text.richText=false;text.raycastTarget=false;
            text.textWrappingMode=TextWrappingModes.Normal;text.alignment=TextAlignmentOptions.Midline;return text;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,string value,UnityEngine.Events.UnityAction action,Vector2 min,Vector2 max)
        {
            var rect=Box(name,parent,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero,new Color(.08f,.32f,.34f,1));
            rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(action);
            var label=Text("Label",rect,value,22,new Vector2(.04f,.04f),new Vector2(.96f,.96f));label.alignment=TextAlignmentOptions.Center;return button;
        }
        void OnDestroy() { SceneManager.sceneLoaded-=Loaded;FrontEndModalInput.Release(this);if(viewRoot)Destroy(viewRoot); }
    }
}
#endif
