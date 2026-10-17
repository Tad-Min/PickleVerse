using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using PickleVerse.Settings;

namespace PickleVerse.Multiplayer
{
    // Stays above the room UI while the staging session gathers explicit decisions.
    public sealed class MatchReadyOverlay:MonoBehaviour
    {
        RectTransform root,safeArea;
        TMP_Text title,subtitle,countdown,seats,hint;
        UnityEngine.UI.Image progress;
        UnityEngine.UI.Button accept,cancel;
        bool wasVisible;
        void Build()
        {
            var theme=ScreenUiTheme.Current;var font=theme?theme.font:FindAnyObjectByType<TMP_Text>()?.font??TMP_Settings.defaultFontAsset;
            root=MatchPresentationUi.Rect("MatchFound",transform,Vector2.zero,Vector2.one);
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=180;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=1;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            MatchPresentationUi.Image("Dim",root,Vector2.zero,Vector2.one,new Color(.006f,.015f,.03f,.9f),true);
            safeArea=MatchPresentationUi.Rect("SafeArea",root,Vector2.zero,Vector2.one);
            var card=MatchPresentationUi.Image("Card",safeArea,new Vector2(.20f,.15f),new Vector2(.8f,.85f),MatchPresentationUi.Ink,true).rectTransform;
            if(theme){var surface=card.GetComponent<UnityEngine.UI.Image>();surface.material=theme.panel;surface.color=Color.white;}
            card.gameObject.AddComponent<UiPanelReveal>();
            MatchPresentationUi.Image("GoldEdge",card,new Vector2(0,.985f),Vector2.one,MatchPresentationUi.Gold);
            title=MatchPresentationUi.Label("Title",card,font,"ĐÃ TÌM THẤY TRẬN",36,new Vector2(.04f,.77f),new Vector2(.96f,.91f),MatchPresentationUi.Gold);
            subtitle=MatchPresentationUi.Label("Mode",card,font,"",20,new Vector2(.05f,.68f),new Vector2(.95f,.77f),Color.white);
            countdown=MatchPresentationUi.Label("Countdown",card,font,"20",60,new Vector2(.35f,.44f),new Vector2(.65f,.65f),Color.white);
            seats=MatchPresentationUi.Label("Players",card,font,"",22,new Vector2(.06f,.35f),new Vector2(.94f,.44f),new Color(.5f,.9f,.8f));
            var track=MatchPresentationUi.Image("TimerTrack",card,new Vector2(.09f,.30f),new Vector2(.91f,.315f),new Color(.12f,.2f,.27f));
            progress=MatchPresentationUi.Image("Remaining",track.transform,Vector2.zero,Vector2.one,MatchPresentationUi.Gold);
            accept=MatchPresentationUi.Button("Accept",card,font,"CHẤP NHẬN",new Vector2(.09f,.15f),new Vector2(.62f,.27f),true);
            cancel=MatchPresentationUi.Button("Cancel",card,font,"HỦY",new Vector2(.65f,.15f),new Vector2(.91f,.27f),false);
            ScreenUiTheme.ButtonStyle(accept,true);ScreenUiTheme.ButtonStyle(cancel);
            hint=MatchPresentationUi.Label("Hint",card,font,"",16,new Vector2(.05f,.02f),new Vector2(.95f,.13f),new Color(.65f,.73f,.8f));
            accept.onClick.AddListener(()=>{var app=MultiplayerApp.Instance;if(app)_=app.AcceptMatchAsync();});
            cancel.onClick.AddListener(()=>{var app=MultiplayerApp.Instance;if(app)_=app.DeclineMatchAsync();});
            root.gameObject.SetActive(false);
        }
        void Update()
        {
            var app=MultiplayerApp.Instance;
            bool visible=app && app.ReadyCheckActive;
            if(!root){if(!visible)return;Build();}
            root.gameObject.SetActive(visible);
            if(!visible){wasVisible=false;return;}
            MatchPresentationUi.SafeArea(safeArea);
            if(!wasVisible){EventSystem.current?.SetSelectedGameObject(accept.gameObject);wasVisible=true;}
            subtitle.text=app.Mode==PlayMode.Doubles?"ĐẤU ĐÔI / 2v2":"ĐẤU ĐƠN / 1v1";
            countdown.text=Mathf.CeilToInt(app.ReadySeconds).ToString("00");
            seats.text=$"{app.ReadyCount} / {app.Roster.Count} ĐÃ XÁC NHẬN";
            progress.rectTransform.anchorMax=new Vector2(app.ReadySeconds/MatchReadyPolicy.Seconds,1);
            accept.interactable=app.CanAcceptMatch;cancel.interactable=!app.ReadyCheckBusy&&!app.ReadyCheckAccepted;
            accept.GetComponentInChildren<TMP_Text>().text=app.ReadyCheckAccepted?"ĐÃ CHẤP NHẬN":app.ReadyCheckBusy?"ĐANG GỬI…":"CHẤP NHẬN";
            hint.text=app.ReadyCheckAccepted?"Chờ mọi người xác nhận • Nếu trận bị hủy, bạn sẽ tự tìm tiếp.":app.CanAcceptMatch?"Xác nhận trong 20 giây • Hủy để dừng tìm trận.":"Đang kết nối những người chơi đã tìm thấy…";
        }
    }
}
