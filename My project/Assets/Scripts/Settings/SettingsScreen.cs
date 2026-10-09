using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace PickleVerse.Settings
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        public RectTransform safeArea, touchBoard;
        public GameObject[] pages;
        public UnityEngine.UI.Button[] tabs, presets, fpsChoices, aaChoices, bindingButtons;
        public UnityEngine.UI.Slider[] sliders;
        public TMP_Text[] sliderValues, bindingLabels;
        public UnityEngine.UI.Toggle shadowToggle, vsyncToggle, leftToggle;
        public GameObject keyboardPage, touchPage, captureOverlay, confirmOverlay;
        public TMP_Text contextLabel, status, comboHint, captureText, confirmText;
        public UnityEngine.UI.Button applyButton, resumeButton, leaveButton, signOutButton, confirmButton;
        public TouchLayoutHandle[] touchHandles;
        public Camera previewCamera;
        public SettingsData Draft { get; private set; }
        public bool Dirty { get; private set; }
        public bool Capturing => bindingAction >= 0;
        SettingsData baseline;
        int bindingAction=-1, captureFrame;
        bool refreshing, operation;
        Action confirmed;
        Rect lastSafeArea;
        Vector2 lastResolution;

        void Awake()
        {
            SettingsOverlay.AttachStandalone();
            baseline=GameSettings.Current.Copy();Draft=baseline.Copy();
            var canvas=GetComponent<Canvas>();
            var camera=Camera.main;
            if(camera&&camera!=previewCamera){if(previewCamera)previewCamera.gameObject.SetActive(false);canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.05f;}
            if(!EventSystem.current)
            {
                var events=new GameObject("Settings EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events,gameObject.scene);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            for(int i=0;i<sliders.Length;i++){int index=i;sliders[i].onValueChanged.AddListener(v=>SetSlider(index,v));}
            for(int i=0;i<tabs.Length;i++){int index=i;tabs[i].onClick.AddListener(()=>ShowTab(index));}
            for(int i=0;i<presets.Length;i++){int index=i;presets[i].onClick.AddListener(()=>SetPreset(index));}
            for(int i=0;i<fpsChoices.Length;i++){int rate=new[]{30,60,120}[i];fpsChoices[i].onClick.AddListener(()=>SetFps(rate));}
            for(int i=0;i<aaChoices.Length;i++){int samples=new[]{1,2,4}[i];aaChoices[i].onClick.AddListener(()=>SetAa(samples));}
            for(int i=0;i<bindingButtons.Length;i++){int index=i;bindingButtons[i].onClick.AddListener(()=>BeginBinding(index));}
            shadowToggle.onValueChanged.AddListener(v=>{if(!refreshing){Draft.shadows=v;Changed(true);}});
            vsyncToggle.onValueChanged.AddListener(v=>{if(!refreshing){Draft.vsync=v;Changed(true);}});
            leftToggle.onValueChanged.AddListener(v=>{if(!refreshing){Draft.leftHanded=v;Changed(false);}});
            bool game=SettingsOverlay.Host?.IsGameplay??false;
            resumeButton.gameObject.SetActive(game);leaveButton.gameObject.SetActive(game);
            signOutButton.gameObject.SetActive(SettingsOverlay.Host?.CanSignOut??false);
            contextLabel.text=game?"TRẬN ĐẤU BOT ĐANG TẠM DỪNG":"THIẾT LẬP TRẢI NGHIỆM CỦA BẠN";
            var hint=pages[0].transform.Find("PauseHint").GetComponent<TMP_Text>();
            if(!game)hint.text="Tùy chỉnh trước khi vào sân. Nút đăng xuất áp dụng cho phiên hiện tại.";
            captureOverlay.SetActive(false);confirmOverlay.SetActive(false);
            Refresh();ShowTab(0);ShowKeyboard(!Application.isMobilePlatform);ApplySafeArea();
        }

        void Start(){if(EventSystem.current)EventSystem.current.SetSelectedGameObject(tabs[0].gameObject);}
        void Update()
        {
            ApplySafeArea();var keyboard=UnityEngine.InputSystem.Keyboard.current;if(keyboard==null)return;
            if(Capturing)
            {
                if(Time.frameCount<=captureFrame+1)return;
                if(keyboard.escapeKey.wasPressedThisFrame){CancelBinding();return;}
                var key=keyboard.allKeys.FirstOrDefault(k=>k.wasPressedThisFrame);
                if(key!=null)TryAssignKey(key.keyCode);
                return;
            }
            if(keyboard.escapeKey.wasPressedThisFrame){if(confirmOverlay.activeSelf)CancelConfirm();else RequestClose();}
        }

        public void ShowTab(int tab)
        {
            if(operation||Capturing)return;
            for(int i=0;i<pages.Length;i++){pages[i].SetActive(i==tab);Tint(tabs[i],i==tab);}
        }
        public void ShowKeyboard(bool keyboard)
        {
            keyboardPage.SetActive(keyboard);touchPage.SetActive(!keyboard);
        }
        public void Keyboard() { ShowKeyboard(true); }
        public void Touch() { ShowKeyboard(false); }
        public void SetPreset(int preset){Draft.SetPreset(preset);Changed(true);}
        public void SetFps(int fps){Draft.fps=fps;Changed(true);}
        public void SetAa(int aa){Draft.msaa=aa;Changed(true);}
        public void SetSlider(int index,float value)
        {
            if(refreshing)return;
            switch(index){case 0:Draft.master=value;break;case 1:Draft.music=value;break;case 2:Draft.effects=value;break;case 3:Draft.renderScale=value;break;case 4:Draft.touchScale=value;break;case 5:Draft.touchOpacity=value;break;case 6:Draft.deadzone=value;break;case 7:Draft.sprintThreshold=value;break;}
            Changed(index<4);
        }
        void Changed(bool preview)
        {
            Dirty=true;if(preview)SettingsRuntime.Apply(Draft);status.text="Chưa lưu thay đổi";Refresh();
        }

        public void BeginBinding(int action)
        {
            if(action==14||operation)return;
            bindingAction=action;captureFrame=Time.frameCount;
            captureText.text="Nhấn phím mới cho: "+GameSettings.ActionName((GameAction)action)+"\nEsc để hủy";
            captureOverlay.SetActive(true);captureOverlay.transform.SetAsLastSibling();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        }
        public bool TryAssignKey(Key key)
        {
            if(bindingAction<0)return false;
            if(!GameSettings.TryBind(Draft,(GameAction)bindingAction,key,out var error)) {captureText.text=error+"\nChọn phím khác hoặc Esc để hủy";return false;}
            CancelBinding();Changed(false);return true;
        }
        public void CancelBinding(){bindingAction=-1;captureOverlay.SetActive(false);}
        public void MoveTouch(int index,Vector2 position)
        {
            if(Draft.leftHanded)position.x=1-position.x;
            Draft.touchPositions[index]=new Vector2(Mathf.Clamp(position.x,.05f,.95f),Mathf.Clamp(position.y,.06f,.78f));Changed(false);
        }
        public void Apply()
        {
            CancelBinding();GameSettings.Commit(Draft);baseline=GameSettings.Current.Copy();Draft=baseline.Copy();Dirty=false;status.text="Đã áp dụng và lưu";Refresh();
        }
        public void RestoreDefaults()
        {
            Confirm("Khôi phục toàn bộ cài đặt mặc định? Bạn có thể kiểm tra lại trước khi Áp dụng.",()=>{Draft=SettingsData.Defaults();Changed(true);});
        }
        public void RestoreControls()
        {
            var d=SettingsData.Defaults();Draft.keys=d.keys;Draft.touchPositions=d.touchPositions;Draft.touchScale=d.touchScale;Draft.touchOpacity=d.touchOpacity;Draft.deadzone=d.deadzone;Draft.sprintThreshold=d.sprintThreshold;Draft.leftHanded=false;Changed(false);
        }
        public void Discard(){Draft=baseline.Copy();SettingsRuntime.Apply(baseline);Dirty=false;Refresh();}
        public void RequestClose()
        {
            if(operation)return;
            if(Dirty)Confirm("Bỏ các thay đổi chưa áp dụng và đóng cài đặt?",()=>{Discard();SettingsOverlay.Close();});
            else SettingsOverlay.Close();
        }
        public void Resume()
        {
            if(Dirty)Confirm("Áp dụng thay đổi và tiếp tục trận?",()=>{Apply();SettingsOverlay.Close(true);});
            else SettingsOverlay.Close(true);
        }
        public void LeaveMatch()
        {
            Confirm("Rời trận đấu bot? Trận đang chơi sẽ kết thúc và bạn trở về đại sảnh.",()=>{Discard();SettingsOverlay.Host?.LeaveToMenu();});
        }
        public void SignOut()
        {
            Confirm("Đăng xuất phiên hiện tại? Cài đặt và dữ liệu khách được giữ trên thiết bị.",SignOutConfirmed);
        }
        async void SignOutConfirmed()
        {
            operation=true;status.text="Đang đăng xuất…";
            try { await (SettingsOverlay.Host?.SignOutAsync()??System.Threading.Tasks.Task.CompletedTask);status.text="Đã đăng xuất. Bạn đang ở chế độ khách."; }
            catch(Exception e){status.text=e.Message;}
            finally{operation=false;}
        }
        void Confirm(string message,Action action)
        {CancelBinding();confirmed=action;confirmText.text=message;confirmOverlay.SetActive(true);confirmOverlay.transform.SetAsLastSibling();}
        public void AcceptConfirm(){var action=confirmed;confirmed=null;confirmOverlay.SetActive(false);action?.Invoke();}
        public void CancelConfirm(){confirmed=null;confirmOverlay.SetActive(false);}

        void Refresh()
        {
            refreshing=true;
            float[] values={Draft.master,Draft.music,Draft.effects,Draft.renderScale,Draft.touchScale,Draft.touchOpacity,Draft.deadzone,Draft.sprintThreshold};
            for(int i=0;i<sliders.Length;i++){sliders[i].SetValueWithoutNotify(values[i]);sliderValues[i].text=(values[i]*100).ToString("0")+"%";}
            shadowToggle.SetIsOnWithoutNotify(Draft.shadows);vsyncToggle.SetIsOnWithoutNotify(Draft.vsync);leftToggle.SetIsOnWithoutNotify(Draft.leftHanded);
            for(int i=0;i<presets.Length;i++)Tint(presets[i],i==Draft.graphics);
            for(int i=0;i<fpsChoices.Length;i++)Tint(fpsChoices[i],new[]{30,60,120}[i]==Draft.fps);
            for(int i=0;i<aaChoices.Length;i++)Tint(aaChoices[i],new[]{1,2,4}[i]==Draft.msaa);
            for(int i=0;i<bindingLabels.Length;i++)bindingLabels[i].text=GameSettings.KeyName((Key)Draft.keys[i]);
            comboHint.text="Drop: "+GameSettings.KeyName(Draft.KeyFor(GameAction.Drive))+" + "+GameSettings.KeyName(Draft.KeyFor(GameAction.Dink))+"   |   Smash: "+GameSettings.KeyName(Draft.KeyFor(GameAction.Drive))+" + "+GameSettings.KeyName(Draft.KeyFor(GameAction.Volley));
            foreach(var handle in touchHandles)handle.Refresh();
            applyButton.interactable=Dirty;refreshing=false;
        }
        static void Tint(UnityEngine.UI.Button button,bool selected)
        {button.GetComponent<UnityEngine.UI.Image>().color=selected?new Color(.10f,.32f,.33f):new Color(.06f,.14f,.19f);}
        void ApplySafeArea()
        {
            var area=Screen.safeArea;var size=new Vector2(Screen.width,Screen.height);if(area==lastSafeArea&&size==lastResolution)return;
            lastSafeArea=area;lastResolution=size;
            safeArea.anchorMin=new Vector2(area.xMin/size.x,area.yMin/size.y);safeArea.anchorMax=new Vector2(area.xMax/size.x,area.yMax/size.y);safeArea.offsetMin=safeArea.offsetMax=Vector2.zero;
        }
        void OnDestroy(){if(Dirty)SettingsRuntime.Apply(baseline);SettingsOverlay.NotifyDestroyed();}
    }

}
