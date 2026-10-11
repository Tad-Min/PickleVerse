using System;
using System.IO;
using System.Linq;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SettingsSetup
{
    const string Path="Assets/Scenes/UI/Settings.unity";
    static TMP_FontAsset font;
    static readonly Color Ink=Hex("0B1823"),Surface=Hex("152B35"),Mint=Hex("73DCC4"),Lime=Hex("C9F573"),Muted=Hex("A8BEC7");
    public static object Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first.");
        if(File.Exists(Path))throw new InvalidOperationException("Settings already exists; edit its hierarchy.");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var preview=new GameObject("Settings Preview Camera",typeof(Camera)).GetComponent<Camera>();preview.tag="EditorOnly";preview.clearFlags=CameraClearFlags.SolidColor;preview.backgroundColor=Ink;preview.orthographic=true;
        var go=new GameObject("Settings Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=preview;canvas.planeDistance=1;canvas.sortingOrder=100;
        var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var ui=go.AddComponent<SettingsScreen>();ui.previewCamera=preview;
        Block("Dim",go.transform,Vector2.zero,Vector2.one,new Color(.015f,.04f,.055f,.97f),true);
        var safe=Rect("SafeArea",go.transform,Vector2.zero,Vector2.one);ui.safeArea=safe;
        Text("Title",safe,"CÀI ĐẶT",new Vector2(.045f,.875f),new Vector2(.7f,.96f),38,Color.white,TextAlignmentOptions.Left,true);
        ui.contextLabel=Text("Context",safe,"THIẾT LẬP TRẢI NGHIỆM CỦA BẠN",new Vector2(.045f,.825f),new Vector2(.82f,.872f),16,Muted,TextAlignmentOptions.Left);
        Button("Close",safe,"Đóng / Esc",new Vector2(.82f,.887f),new Vector2(.955f,.957f),Surface,Color.white,ui.RequestClose);
        Block("HeaderLine",safe,new Vector2(.045f,.805f),new Vector2(.955f,.808f),Mint);
        var sidebar=Rect("Tabs",safe,new Vector2(.045f,.195f),new Vector2(.235f,.778f));
        ui.tabs=new UnityEngine.UI.Button[4];string[] names={"Tổng quan","Âm thanh","Đồ họa","Điều khiển"};
        for(int i=0;i<4;i++)ui.tabs[i]=Button("Tab"+i,sidebar,names[i],new Vector2(0,.80f-i*.19f),new Vector2(1,.975f-i*.19f),Surface,Color.white,null);
        ui.pages=new GameObject[4];for(int i=0;i<4;i++){var page=Rect("Page"+i,safe,new Vector2(.275f,.20f),new Vector2(.955f,.778f));ui.pages[i]=page.gameObject;}
        ui.sliders=new UnityEngine.UI.Slider[8];ui.sliderValues=new TMP_Text[8];
        General(ui);Audio(ui);Graphics(ui);Controls(ui);
        ui.status=Text("Status",safe,"",new Vector2(.275f,.135f),new Vector2(.955f,.18f),16,Mint,TextAlignmentOptions.Left);
        Button("RestoreAll",safe,"Khôi phục mặc định",new Vector2(.045f,.042f),new Vector2(.30f,.116f),Surface,Muted,ui.RestoreDefaults);
        ui.applyButton=Button("Apply",safe,"ÁP DỤNG VÀ LƯU",new Vector2(.66f,.042f),new Vector2(.955f,.116f),Lime,Ink,ui.Apply);
        Button("Discard",safe,"Hoàn tác",new Vector2(.46f,.042f),new Vector2(.625f,.116f),Surface,Color.white,ui.Discard);
        ui.captureOverlay=Dialog("Capture",go.transform,out ui.captureText);
        Button("CancelCapture",ui.captureOverlay.transform,"Hủy / Esc",new Vector2(.37f,.14f),new Vector2(.63f,.32f),Surface,Color.white,ui.CancelBinding);
        ui.captureOverlay.SetActive(false);
        ui.confirmOverlay=Dialog("Confirm",go.transform,out ui.confirmText);
        ui.confirmButton=Button("ConfirmYes",ui.confirmOverlay.transform,"Xác nhận",new Vector2(.53f,.14f),new Vector2(.84f,.32f),Lime,Ink,ui.AcceptConfirm);
        Button("ConfirmNo",ui.confirmOverlay.transform,"Quay lại",new Vector2(.16f,.14f),new Vector2(.47f,.32f),Surface,Color.white,ui.CancelConfirm);ui.confirmOverlay.SetActive(false);
        for(int i=0;i<4;i++)ui.pages[i].SetActive(i==0);
        EditorSceneManager.SaveScene(scene,Path);
        WireMenu();WireBot();BuildLists();EditorSceneManager.OpenScene(Path,OpenSceneMode.Single);
        return new{scene=Path,tabs=4,bindings=15,touchControls=9};
    }

    static void General(SettingsScreen ui)
    {
        var p=ui.pages[0].transform;
        Text("Heading",p,"Cài đặt theo cách bạn chơi",new Vector2(0,.85f),Vector2.one,27,Color.white,TextAlignmentOptions.Left,true);
        Text("Intro",p,"Thay đổi hình ảnh và điều khiển. Các lựa chọn được lưu trên thiết bị.",new Vector2(0,.68f),new Vector2(1,.84f),19,Muted,TextAlignmentOptions.TopLeft);
        ui.resumeButton=Button("ResumeMatch",p,"TIẾP TỤC TRẬN",new Vector2(0,.45f),new Vector2(.64f,.60f),Lime,Ink,ui.Resume);
        Text("PauseHint",p,"Trận bot tự tạm dừng khi mở cài đặt. Bóng và đồng hồ giao được giữ.",new Vector2(0,.30f),new Vector2(1,.44f),17,Muted,TextAlignmentOptions.TopLeft);
        ui.leaveButton=Button("LeaveMatch",p,"Rời trận về đại sảnh",new Vector2(0,.10f),new Vector2(.64f,.25f),new Color(.30f,.12f,.13f),Color.white,ui.LeaveMatch);
        ui.signOutButton=Button("SignOut",p,"Đăng xuất",new Vector2(0,.10f),new Vector2(.64f,.25f),new Color(.30f,.12f,.13f),Color.white,ui.SignOut);
    }
    static void Audio(SettingsScreen ui)
    {
        var p=ui.pages[1].transform;
        Text("Heading",p,"Âm lượng",new Vector2(0,.87f),Vector2.one,27,Color.white,TextAlignmentOptions.Left,true);
        string[] labels={"Âm lượng tổng","Nhạc nền","Hiệu ứng"};
        for(int i=0;i<3;i++)Slider(ui,i,p,labels[i],new Vector2(0,.66f-i*.22f),new Vector2(1,.84f-i*.22f),0,1);
        Text("Note",p,"Nhạc nền phát ở các màn giao diện và tự dừng khi vào trận. Hiệu ứng trận đấu có âm lượng riêng.",new Vector2(0,0),new Vector2(1,.13f),17,Muted,TextAlignmentOptions.TopLeft);
    }
    static void Graphics(SettingsScreen ui)
    {
        var p=ui.pages[2].transform;
        Text("Heading",p,"Đồ họa và hiệu năng",new Vector2(0,.9f),Vector2.one,27,Color.white,TextAlignmentOptions.Left,true);
        ui.presets=Choices(p,"Preset",new[]{"Thấp","Cân bằng","Cao"},.72f,.875f);
        Slider(ui,3,p,"Độ nét hình ảnh",new Vector2(0,.51f),new Vector2(1,.69f),.5f,1.25f);
        Text("FpsLabel",p,"Giới hạn FPS",new Vector2(0,.4f),new Vector2(.33f,.47f),17,Muted,TextAlignmentOptions.Left);
        ui.fpsChoices=Choices(p,"Fps",new[]{"30","60","120"},.30f,.39f);
        Text("AaLabel",p,"Khử răng cưa",new Vector2(0,.19f),new Vector2(.33f,.27f),17,Muted,TextAlignmentOptions.Left);
        ui.aaChoices=Choices(p,"Aa",new[]{"Tắt","2x","4x"},.08f,.18f);
        ui.shadowToggle=Toggle("Shadows",p,"Bóng đổ",new Vector2(.48f,.40f),new Vector2(.74f,.48f));
        ui.vsyncToggle=Toggle("VSync",p,"VSync (PC)",new Vector2(.76f,.40f),new Vector2(1,.48f));
    }
    static void Controls(SettingsScreen ui)
    {
        var p=ui.pages[3].transform;
        Button("Keyboard",p,"MÁY TÍNH",new Vector2(0,.905f),new Vector2(.28f,1),Surface,Color.white,ui.Keyboard);
        Button("Touch",p,"ĐIỆN THOẠI",new Vector2(.30f,.905f),new Vector2(.60f,1),Surface,Color.white,ui.Touch);
        Button("ResetControls",p,"Đặt lại",new Vector2(.76f,.905f),Vector2.one,Surface,Muted,ui.RestoreControls);
        var keyboard=Rect("KeyboardPage",p,Vector2.zero,new Vector2(1,.875f));ui.keyboardPage=keyboard.gameObject;
        var scroll=Rect("BindingsScroll",keyboard,new Vector2(0,.105f),Vector2.one);var sc=scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();sc.horizontal=false;sc.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var view=Block("Viewport",scroll,Vector2.zero,Vector2.one,new Color(.035f,.09f,.12f,.8f),true);view.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var content=Rect("Content",view,new Vector2(0,1),Vector2.one);content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,780);
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=6;layout.padding=new RectOffset(8,8,8,8);layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        sc.content=content;sc.viewport=view;
        view.anchorMax=new Vector2(.975f,1);
        var bar=Block("ScrollBar",scroll,new Vector2(.985f,0),Vector2.one,Surface,true);
        var thumb=Block("Handle",bar,Vector2.zero,Vector2.one,Mint,true);
        var scrollbar=bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();scrollbar.handleRect=thumb;scrollbar.targetGraphic=thumb.GetComponent<UnityEngine.UI.Image>();scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        sc.verticalScrollbar=scrollbar;sc.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
        ui.bindingButtons=new UnityEngine.UI.Button[15];ui.bindingLabels=new TMP_Text[15];
        for(int i=0;i<15;i++)
        {
            var row=Rect("Binding"+i,content,Vector2.zero,Vector2.one);row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=48;
            Text("Action",row,GameSettings.ActionName((GameAction)i),new Vector2(.025f,0),new Vector2(.65f,1),18,Color.white,TextAlignmentOptions.Left);
            ui.bindingButtons[i]=Button("Key",row,"",new Vector2(.68f,.05f),new Vector2(.97f,.95f),Surface,Mint,null);
            ui.bindingLabels[i]=ui.bindingButtons[i].GetComponentInChildren<TMP_Text>();ui.bindingButtons[i].interactable=i!=14;
        }
        ui.comboHint=Text("ComboHint",keyboard,"Drop / Smash theo các phím đã chọn",Vector2.zero,new Vector2(1,.09f),15,Mint,TextAlignmentOptions.Left);
        var touch=Rect("TouchPage",p,Vector2.zero,new Vector2(1,.875f));ui.touchPage=touch.gameObject;
        var board=Block("TouchBoard",touch,new Vector2(0,.20f),new Vector2(.66f,.91f),new Color(.08f,.26f,.29f));ui.touchBoard=board;
        Text("BoardHint",board,"KÉO NÚT ĐỂ ĐỔI VỊ TRÍ",new Vector2(.04f,.83f),new Vector2(.96f,.98f),13,Muted,TextAlignmentOptions.Center);
        var circle=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        string[] buttons={"Di chuyển","Drive","Dink","Volley","Drop","Smash","Reset","Lực","Chéo"};ui.touchHandles=new TouchLayoutHandle[9];
        for(int i=0;i<9;i++)
        {
            var control=Block("TouchHandle"+i,board,new Vector2(.4f,.4f),new Vector2(.5f,.5f),new Color(.025f,.09f,.13f,.95f),true);if(i==0)control.GetComponent<UnityEngine.UI.Image>().sprite=circle;
            control.gameObject.AddComponent<CanvasGroup>();var handle=control.gameObject.AddComponent<TouchLayoutHandle>();handle.screen=ui;handle.index=i;ui.touchHandles[i]=handle;
            Text("Label",control,buttons[i],Vector2.zero,Vector2.one,i==0?11:10,Color.white,TextAlignmentOptions.Center);
        }
        var options=Rect("TouchOptions",touch,new Vector2(.70f,.05f),Vector2.one);
        string[] labels={"Kích thước nút","Độ rõ nút","Vùng chết joystick","Ngưỡng chạy"};float[] min={.75f,.35f,.02f,.7f},max={1.4f,1,.25f,.98f};
        for(int i=0;i<4;i++)Slider(ui,i+4,options,labels[i],new Vector2(0,.77f-i*.20f),new Vector2(1,.96f-i*.20f),min[i],max[i]);
        ui.leftToggle=Toggle("LeftHand",options,"Đảo tay thuận",new Vector2(0,0),new Vector2(1,.12f));
        Text("TouchNote",touch,"Kéo trực tiếp trên bản xem trước.\nBố trí này áp dụng cho điều khiển trận bot.",new Vector2(0,0),new Vector2(.66f,.17f),15,Muted,TextAlignmentOptions.Left);
    }
    static UnityEngine.UI.Button[] Choices(Transform parent,string name,string[] labels,float bottom,float top)
    {
        var result=new UnityEngine.UI.Button[labels.Length];for(int i=0;i<labels.Length;i++)result[i]=Button(name+i,parent,labels[i],new Vector2(i*.34f,bottom),new Vector2(Mathf.Min(1,i*.34f+.32f),top),Surface,Color.white,null);return result;
    }
    static void Slider(SettingsScreen ui,int index,Transform parent,string name,Vector2 min,Vector2 max,float low,float high)
    {
        var row=Rect("SliderRow"+index,parent,min,max);Text("Name",row,name,new Vector2(0,.58f),new Vector2(.8f,1),index>=4?15:19,Color.white,TextAlignmentOptions.Left);
        ui.sliderValues[index]=Text("Value",row,"100%",new Vector2(.8f,.58f),Vector2.one,16,Mint,TextAlignmentOptions.Right);
        var rect=Block("Slider",row,new Vector2(0,.10f),new Vector2(1,.48f),Color.clear,true);var slider=rect.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=low;slider.maxValue=high;
        Block("Track",rect,new Vector2(0,.4f),new Vector2(1,.6f),new Color(.20f,.33f,.39f));var area=Rect("FillArea",rect,new Vector2(0,.4f),new Vector2(1,.6f));var fill=Block("Fill",area,Vector2.zero,Vector2.one,Mint);slider.fillRect=fill;
        var handleArea=Rect("HandleArea",rect,new Vector2(0,.2f),new Vector2(1,.8f));var handle=Block("Handle",handleArea,new Vector2(.5f,0),new Vector2(.5f,1),Lime,true);handle.sizeDelta=new Vector2(20,0);handle.GetComponent<UnityEngine.UI.Image>().sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();ui.sliders[index]=slider;
    }
    static UnityEngine.UI.Toggle Toggle(string name,Transform parent,string label,Vector2 min,Vector2 max)
    {
        var row=Block(name,parent,min,max,Color.clear,true);var box=Block("Box",row,Vector2.zero,Vector2.zero,Surface,true);box.anchorMin=box.anchorMax=new Vector2(0,.5f);box.pivot=new Vector2(0,.5f);box.anchoredPosition=Vector2.zero;box.sizeDelta=new Vector2(24,24);
        var check=Block("Checked",box,new Vector2(.2f,.2f),new Vector2(.8f,.8f),Lime);
        var toggle=row.gameObject.AddComponent<UnityEngine.UI.Toggle>();toggle.targetGraphic=box.GetComponent<UnityEngine.UI.Image>();toggle.graphic=check.GetComponent<UnityEngine.UI.Image>();
        Text("Label",row,label,new Vector2(.23f,0),Vector2.one,15,Color.white,TextAlignmentOptions.Left);return toggle;
    }
    static GameObject Dialog(string name,Transform parent,out TMP_Text message)
    {
        var overlay=Block(name,parent,Vector2.zero,Vector2.one,new Color(.01f,.035f,.05f,.98f),true);
        message=Text("Message",overlay,"",new Vector2(.14f,.42f),new Vector2(.86f,.74f),26,Color.white,TextAlignmentOptions.Center);return overlay.gameObject;
    }
    static void WireMenu()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity",OpenSceneMode.Single);
        var button=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).Single(b=>b.name=="BtnSetting");
        var launcher=button.GetComponent<SettingsLauncher>();if(!launcher)launcher=button.gameObject.AddComponent<SettingsLauncher>();
        button.interactable=true;button.GetComponent<UnityEngine.UI.Image>().color=Surface;button.GetComponentInChildren<TMP_Text>().color=Mint;
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);UnityEventTools.AddPersistentListener(button.onClick,launcher.OpenSettings);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void WireBot()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity",OpenSceneMode.Single);var hud=UnityEngine.Object.FindAnyObjectByType<MatchHud>();
        var old=hud.transform.Find("Settings")??hud.transform.Find("Pause");var button=old.GetComponent<UnityEngine.UI.Button>();old.name="Settings";
        button.GetComponentInChildren<TMP_Text>().text="Esc / Cài đặt";
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);UnityEventTools.AddPersistentListener(button.onClick,hud.OpenSettings);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void BuildLists()
    {
        if(!EditorBuildSettings.scenes.Any(s=>s.path==Path))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(Path,true)}).ToArray();
        foreach(var id in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var p=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(id));if(!p.overrideGlobalScenes||p.scenes.Any(s=>s.path==Path))continue;
            p.scenes=p.scenes.Concat(new[]{new EditorBuildSettingsScene(Path,true)}).ToArray();EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
        }
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string text,Vector2 min,Vector2 max,Color background,Color foreground,UnityEngine.Events.UnityAction action)
    {
        var rect=Block(name,parent,min,max,background,true);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
        var label=Text("Label",rect,text,Vector2.zero,Vector2.one,18,foreground,TextAlignmentOptions.Center,true);
        if(action!=null)UnityEventTools.AddPersistentListener(button.onClick,action);return button;
    }
    static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment,bool bold=false)
    {
        var rect=Rect(name,parent,min,max);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;if(bold)text.fontStyle=FontStyles.Bold;return text;
    }
    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool hit=false)
    {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=hit;return rect;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
    static Color Hex(string color){ColorUtility.TryParseHtmlString("#"+color,out var value);return value;}
}
