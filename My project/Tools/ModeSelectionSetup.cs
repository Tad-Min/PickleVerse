using System;
using System.IO;
using System.Linq;
using PickleVerse.FrontEnd;
using PickleVerse.LocalMatch;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

// Run through Unity CLI run_script. All scenes/assets are authored by the Editor.
public static class ModeSelectionSetup
{
    public const string ScenePath = "Assets/Scenes/HomeScene/ModeSelection.unity";
    const string MenuPath = "Assets/Scenes/HomeScene/Menu.unity";
    const string BotPath = "Assets/Scenes/BotMatch.unity";
    const string Content = "Assets/FrontEndContent";
    static TMP_FontAsset font;
    static readonly Color Ink = Hex("0B1823"), Surface = Hex("152B35"), Light = Hex("EFF7F5"), Muted = Hex("A3B9C1"), Lime = Hex("C9F573"), Mint = Hex("73DCC4");

    public static object Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring scenes.");
        if (File.Exists(ScenePath)) throw new InvalidOperationException("ModeSelection already exists; edit its hierarchy instead of rebuilding.");
        font = CreateFont();
        var day = Preview("Day"); var night = Preview("Night");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Mode Selection Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink; camera.orthographic = true;
        var events = new GameObject("Mode Selection EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        var canvasGo = new GameObject("Mode Selection Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
        var controller = canvasGo.AddComponent<ModeSelectionMenu>();
        var root = Rect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one); controller.safeArea = root;
        Block("TopAccent",root,new Vector2(0,.992f),Vector2.one,Mint);
        var back = Button("Back",root,new Vector2(.045f,.90f),new Vector2(.145f,.968f),Surface);
        Label("Label",back.transform,"←  Quay lại",Vector2.zero,Vector2.one,18,Light,TextAlignmentOptions.Center);
        UnityEventTools.AddPersistentListener(back.onClick,controller.Back);
        Label("Brand",root,"PICKLEVERSE",new Vector2(.79f,.908f),new Vector2(.955f,.95f),20,Mint,TextAlignmentOptions.Right,FontStyles.Bold);
        controller.breadcrumb=Label("Breadcrumb",root,"CHƠI  /  CHỌN CHẾ ĐỘ",new Vector2(.165f,.909f),new Vector2(.62f,.947f),15,Muted,TextAlignmentOptions.Left);
        controller.title=Label("Title",root,"Chọn cách bạn vào sân",new Vector2(.055f,.799f),new Vector2(.95f,.876f),42,Light,TextAlignmentOptions.Left,FontStyles.Bold);
        controller.subtitle=Label("Subtitle",root,"Một trận đấu mới bắt đầu từ đây.",new Vector2(.055f,.748f),new Vector2(.95f,.792f),20,Muted,TextAlignmentOptions.Left);
        controller.dayPreview=day;controller.nightPreview=night;
        BuildModes(root,controller,day);
        BuildBot(root,controller,day,night);
        Label("Footer",root,"Chọn bằng chuột hoặc cảm ứng  ·  Enter để tiếp tục  ·  Esc để quay lại",new Vector2(.055f,.046f),new Vector2(.79f,.085f),16,Muted,TextAlignmentOptions.Left);
        Label("FooterBrand",root,"PLAY  /  LEARN  /  COMPETE",new Vector2(.80f,.046f),new Vector2(.95f,.085f),13,Mint,TextAlignmentOptions.Right);
        Block("FooterLine",root,new Vector2(.055f,.104f),new Vector2(.945f,.106f),Hex("26434C"));
        var loading=Block("LoadingOverlay",canvasGo.transform,Vector2.zero,Vector2.one,new Color(.025f,.07f,.10f,.98f));
        loading.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;controller.loadingOverlay=loading.gameObject;
        Label("Brand",loading,"PICKLEVERSE",new Vector2(.2f,.54f),new Vector2(.8f,.61f),34,Mint,TextAlignmentOptions.Center,FontStyles.Bold);
        controller.loadingLabel=Label("LoadingLabel",loading,"Đang chuẩn bị sân…",new Vector2(.2f,.42f),new Vector2(.8f,.50f),23,Light,TextAlignmentOptions.Center);
        loading.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene,ScenePath);
        UpdateBotHud();
        UpdateBuildLists();
        VerifyMenu();
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        return new {scene=ScenePath,modeCards=3,botDifficulties=3,environments=2,menuWired=true};
    }

    public static string RepairBuildLists() { UpdateBuildLists(); return "Build scene paths and GUIDs updated through BuildProfile.scenes."; }

    static void BuildModes(RectTransform root,ModeSelectionMenu ui,Texture2D day)
    {
        var page=Rect("ModePage",root,new Vector2(.055f,.175f),new Vector2(.945f,.71f));page.gameObject.AddComponent<CanvasGroup>();ui.modePage=page.gameObject;
        var layout=page.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=22;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;
        var bot=ModeCard("BotMode",page,true,"Đấu với bot","Luyện phản xạ và combo\ntrong một trận đấu đơn.","1V1  /  CHƠI LOCAL");ui.botButton=bot;
        var art=Picture("CourtPreview",bot.transform,day,new Vector2(.003f,.49f),new Vector2(.997f,.997f));
        Block("ArtShade",art.transform,Vector2.zero,Vector2.one,new Color(.015f,.06f,.07f,.15f));
        Badge("Available",bot.transform,"CHƠI NGAY",new Vector2(.06f,.84f),new Vector2(.49f,.92f),Lime,Ink);
        Label("Action",bot.transform,"Thiết lập trận  →",new Vector2(.065f,.05f),new Vector2(.94f,.15f),21,Lime,TextAlignmentOptions.Left,FontStyles.Bold);
        UnityEventTools.AddPersistentListener(bot.onClick,ui.OpenBot);
        var multiplayer=ModeCard("MultiplayerMode",page,false,"Multiplayer","Đánh đơn hoặc đánh đôi\ncùng những người chơi khác.","1V1  /  2V2");
        CourtIcon(multiplayer.transform,new Vector2(.09f,.55f),new Vector2(.91f,.97f),true);
        Badge("ComingSoon",multiplayer.transform,"SẮP CÓ",new Vector2(.06f,.84f),new Vector2(.40f,.92f),Hex("29404B"),Muted);
        Label("Pending",multiplayer.transform,"Đang chuẩn bị",new Vector2(.065f,.05f),new Vector2(.94f,.15f),18,Muted,TextAlignmentOptions.Left);
        var training=ModeCard("TrainingMode",page,false,"Training","Luyện từng kỹ thuật\ntheo các bài tập riêng.","HỌC  /  LUYỆN TẬP");
        CourtIcon(training.transform,new Vector2(.09f,.55f),new Vector2(.91f,.97f),false);
        Badge("ComingSoon",training.transform,"SẮP CÓ",new Vector2(.06f,.84f),new Vector2(.40f,.92f),Hex("29404B"),Muted);
        Label("Pending",training.transform,"Đang chuẩn bị",new Vector2(.065f,.05f),new Vector2(.94f,.15f),18,Muted,TextAlignmentOptions.Left);
    }

    static UnityEngine.UI.Button ModeCard(string name,Transform parent,bool active,string title,string description,string tag)
    {
        var b=Button(name,parent,Vector2.zero,Vector2.one,Surface);b.interactable=active;
        b.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth=1;
        var outline=b.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectDistance=new Vector2(1.5f,-1.5f);outline.effectColor=active?Mint:Hex("29414B");
        Label("ModeTag",b.transform,tag,new Vector2(.065f,.424f),new Vector2(.94f,.48f),13,active?Mint:Muted,TextAlignmentOptions.Left,FontStyles.Bold);
        Label("ModeTitle",b.transform,title,new Vector2(.065f,.30f),new Vector2(.94f,.41f),30,Light,TextAlignmentOptions.Left,FontStyles.Bold);
        Label("Description",b.transform,description,new Vector2(.065f,.17f),new Vector2(.94f,.295f),18,Muted,TextAlignmentOptions.TopLeft);
        return b;
    }

    static void BuildBot(RectTransform root,ModeSelectionMenu ui,Texture2D day,Texture2D night)
    {
        var page=Rect("BotSetupPage",root,new Vector2(.055f,.16f),new Vector2(.945f,.71f));page.gameObject.AddComponent<CanvasGroup>();ui.botPage=page.gameObject;
        var hero=Block("CourtHero",page,Vector2.zero,new Vector2(.47f,1),Surface);
        ui.courtPreview=Picture("CourtPreview",hero,day,new Vector2(0,.31f),Vector2.one);
        Badge("ModeBadge",hero,"ĐẤU VỚI BOT",new Vector2(.05f,.845f),new Vector2(.46f,.944f),Lime,Ink);
        Label("CourtTitle",hero,"Sân của bạn. Nhịp đấu của bạn.",new Vector2(.06f,.17f),new Vector2(.94f,.27f),23,Light,TextAlignmentOptions.Left,FontStyles.Bold);
        Label("Rules",hero,"Đấu đơn 1v1  ·  Thắng 2/3 game\n11 điểm, cách 2  ·  Không cần kết nối mạng",new Vector2(.06f,.045f),new Vector2(.94f,.155f),16,Muted,TextAlignmentOptions.TopLeft);
        var setup=Rect("Setup",page,new Vector2(.515f,0),Vector2.one);
        Label("DifficultyHeading",setup,"01   ĐỘ KHÓ",new Vector2(0,.918f),Vector2.one,18,Mint,TextAlignmentOptions.Left,FontStyles.Bold);
        var row=Rect("DifficultyChoices",setup,new Vector2(0,.724f),new Vector2(1,.902f));
        var layout=row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=12;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;
        ui.difficultyButtons=new UnityEngine.UI.Button[3];ui.difficultySelected=new TMP_Text[3];
        string[] names={"Dễ","Vừa","Khó"};
        for(int i=0;i<3;i++)
        {
            var b=Choice("Difficulty"+i,row,names[i]);b.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().flexibleWidth=1;
            ui.difficultyButtons[i]=b;
            Label("Name",b.transform,names[i],new Vector2(.04f,.41f),new Vector2(.96f,.94f),24,Light,TextAlignmentOptions.Center,FontStyles.Bold);
            ui.difficultySelected[i]=Label("Selected",b.transform,"ĐÃ CHỌN",new Vector2(.02f,.075f),new Vector2(.98f,.35f),12,Lime,TextAlignmentOptions.Center,FontStyles.Bold);
            ui.difficultySelected[i].gameObject.SetActive(i==1);
            UnityEventTools.AddIntPersistentListener(b.onClick,ui.SetDifficulty,i);
        }
        ui.difficultyDescription=Label("DifficultyDescription",setup,"Vừa  ·  Nhịp đấu cân bằng, phù hợp để luyện combo.",new Vector2(0,.64f),new Vector2(1,.712f),15,Muted,TextAlignmentOptions.TopLeft);
        Label("EnvironmentHeading",setup,"02   BỐI CẢNH SÂN",new Vector2(0,.559f),new Vector2(1,.625f),18,Mint,TextAlignmentOptions.Left,FontStyles.Bold);
        ui.environmentButtons=new UnityEngine.UI.Button[2];ui.environmentSelected=new TMP_Text[2];
        for(int i=0;i<2;i++)
        {
            var b=Choice(i==0?"DayChoice":"NightChoice",setup,i==0?"Ban ngày":"Ban đêm");
            var rect=(RectTransform)b.transform;rect.anchorMin=new Vector2(i==0?0:.515f,.245f);rect.anchorMax=new Vector2(i==0?.485f:1,.55f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            Picture("Preview",b.transform,i==0?day:night,new Vector2(.012f,.34f),new Vector2(.988f,.98f));
            Label("Name",b.transform,i==0?"Ban ngày":"Ban đêm",new Vector2(.045f,.035f),new Vector2(.61f,.305f),17,Light,TextAlignmentOptions.Left,FontStyles.Bold);
            ui.environmentSelected[i]=Label("Selected",b.transform,"ĐÃ CHỌN",new Vector2(.61f,.035f),new Vector2(.96f,.305f),10,Lime,TextAlignmentOptions.Right,FontStyles.Bold);
            ui.environmentSelected[i].gameObject.SetActive(i==0);ui.environmentButtons[i]=b;
            if(i==0)UnityEventTools.AddPersistentListener(b.onClick,ui.SelectDay);else UnityEventTools.AddPersistentListener(b.onClick,ui.SelectNight);
        }
        ui.summary=Label("MatchSummary",setup,"BOT VỪA   /   BAN NGÀY",new Vector2(0,.163f),new Vector2(.64f,.217f),14,Muted,TextAlignmentOptions.Left,FontStyles.Bold);
        Label("Format",setup,"ĐẤU ĐƠN 1V1",new Vector2(.64f,.163f),new Vector2(1,.217f),13,Muted,TextAlignmentOptions.Right);
        ui.startButton=Button("StartBotMatch",setup,Vector2.zero,new Vector2(1,.145f),Lime);
        Label("Label",ui.startButton.transform,"VÀO SÂN  →",Vector2.zero,Vector2.one,23,Ink,TextAlignmentOptions.Center,FontStyles.Bold);
        UnityEventTools.AddPersistentListener(ui.startButton.onClick,ui.StartBot);
        page.gameObject.SetActive(false);
    }

    static UnityEngine.UI.Button Choice(string name,Transform parent,string label)
    {
        var button=Button(name,parent,Vector2.zero,Vector2.one,Surface);
        var outline=button.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectDistance=new Vector2(2,-2);outline.effectColor=Hex("385760");
        return button;
    }

    static void UpdateBotHud()
    {
        var scene=EditorSceneManager.OpenScene(BotPath,OpenSceneMode.Single);
        var hud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MatchHud>(true)).Single();
        foreach(var t in hud.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="EnvironmentChoice" || t.name.StartsWith("BotLevel",StringComparison.Ordinal)).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
        var modal=(RectTransform)hud.modal.transform;modal.sizeDelta=new Vector2(760,370);
        hud.modalBody.rectTransform.anchoredPosition=new Vector2(0,6);hud.modalBody.rectTransform.sizeDelta=new Vector2(710,235);
        ((RectTransform)hud.continueButton.transform).anchoredPosition=new Vector2(-245,20);
        ((RectTransform)hud.restartButton.transform).anchoredPosition=new Vector2(0,20);
        var existing=modal.Find("ChooseMode");
        var button=existing?existing.GetComponent<UnityEngine.UI.Button>():Button("ChooseMode",modal,Vector2.zero,Vector2.one,Hex("173B44"));
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(245,20);rect.sizeDelta=new Vector2(210,40);
        if(!existing)Label("Label",rect,"Chọn chế độ",Vector2.zero,Vector2.one,18,Light,TextAlignmentOptions.Center);
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
        UnityEventTools.AddPersistentListener(button.onClick,hud.ChooseMode);hud.modeButton=button;
        EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    static void UpdateBuildLists()
    {
        var entry=new[]{MenuPath,ScenePath,BotPath};
        var remaining=EditorBuildSettings.scenes.Where(s=>!entry.Contains(s.path));
        EditorBuildSettings.scenes=entry.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(remaining).ToArray();
        var profile=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>("Assets/Settings/BuildProfiles/MultiplayerWindows.asset");
        if(!profile)return;
        profile.scenes=entry.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(profile.scenes.Where(s=>!entry.Contains(s.path)).Select(s=>new EditorBuildSettingsScene(s.path,s.enabled))).ToArray();
        EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
    }

    static void VerifyMenu()
    {
        var scene=EditorSceneManager.OpenScene(MenuPath,OpenSceneMode.Single);
        var button=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).Single(b=>b.name=="BtnPlay");
        if(button.onClick.GetPersistentEventCount()<1 || button.onClick.GetPersistentMethodName(0)!="Play")throw new InvalidOperationException("Home Play listener is missing.");
    }

    static Texture2D Preview(string name)
    {
        string path=Content+"/Artwork/BotCourt_"+name+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static TMP_FontAsset CreateFont()
    {
        const string path=Content+"/Mode Vietnamese Font.asset";Directory.CreateDirectory(Content);AssetDatabase.Refresh();
        var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing)return existing;
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/LocalMatchContent/Arial.ttf");
        var asset=TMP_FontAsset.CreateFontAsset(source,48,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
        var chars=Enumerable.Range(32,95).Concat(Enumerable.Range(160,96)).Concat(Enumerable.Range(0x1EA0,90)).Concat(new[]{0x102,0x103,0x110,0x111,0x128,0x129,0x1A0,0x1A1,0x1AF,0x1B0,0x2022,0x2026,0x2190,0x2192}).Select(c=>(char)c).ToArray();
        asset.TryAddCharacters(new string(chars));asset.atlasPopulationMode=AtlasPopulationMode.Static;asset.name="Mode Vietnamese Font";
        AssetDatabase.CreateAsset(asset,path);
        foreach(var texture in asset.atlasTextures){texture.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(texture,asset);}
        asset.material.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(asset.material,asset);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    static void CourtIcon(Transform parent,Vector2 min,Vector2 max,bool multiplayer)
    {
        var art=Block("Illustration",parent,min,max,Hex("10242E"));
        var court=Block("Court",art,new Vector2(.14f,.13f),new Vector2(.86f,.76f),Hex("235050"));
        Color line=Hex("628D89");
        Block("TopLine",court,new Vector2(0,.986f),Vector2.one,line);Block("BottomLine",court,Vector2.zero,new Vector2(1,.014f),line);
        Block("LeftLine",court,Vector2.zero,new Vector2(.01f,1),line);Block("RightLine",court,new Vector2(.99f,0),Vector2.one,line);
        Block("Net",court,new Vector2(0,.49f),new Vector2(1,.51f),line);Block("KitchenLeft",court,new Vector2(0,.32f),new Vector2(1,.334f),line);Block("KitchenRight",court,new Vector2(0,.666f),new Vector2(1,.68f),line);
        Block("Center",court,new Vector2(.495f,0),new Vector2(.505f,1),line);
        var sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        foreach(var pos in multiplayer?new[]{new Vector2(.25f,.22f),new Vector2(.73f,.28f),new Vector2(.26f,.72f),new Vector2(.72f,.77f)}:new[]{new Vector2(.42f,.23f),new Vector2(.55f,.73f)})
        {
            var dot=Block("Player",court,pos-new Vector2(.025f,.035f),pos+new Vector2(.025f,.035f),Mint);dot.GetComponent<UnityEngine.UI.Image>().sprite=sprite;
        }
        if(!multiplayer)Label("Target",court,"+",new Vector2(.12f,.54f),new Vector2(.32f,.86f),30,Lime,TextAlignmentOptions.Center,FontStyles.Bold);
    }

    static void Badge(string name,Transform parent,string text,Vector2 min,Vector2 max,Color background,Color foreground)
    {
        var badge=Block(name,parent,min,max,background);Label("Label",badge,text,Vector2.zero,Vector2.one,13,foreground,TextAlignmentOptions.Center,FontStyles.Bold);
    }

    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }

    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
    }

    static UnityEngine.UI.RawImage Picture(string name,Transform parent,Texture texture,Vector2 min,Vector2 max)
    {
        var frame=Rect(name+"Frame",parent,min,max);frame.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var rect=Rect(name,frame,Vector2.zero,Vector2.one);var image=rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();image.texture=texture;image.raycastTarget=false;
        var fit=rect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=(float)texture.width/texture.height;return image;
    }

    static TMP_Text Label(string name,Transform parent,string value,Vector2 min,Vector2 max,float size,Color color,TextAlignmentOptions alignment,FontStyles style=FontStyles.Normal)
    {
        var rect=Rect(name,parent,min,max);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=value;label.fontSize=size;label.color=color;label.alignment=alignment;label.fontStyle=style;label.raycastTarget=false;return label;
    }

    static UnityEngine.UI.Button Button(string name,Transform parent,Vector2 min,Vector2 max,Color background)
    {
        var rect=Block(name,parent,min,max,background);rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.selectedColor=new Color(1.10f,1.10f,1.10f);colors.pressedColor=new Color(.85f,.92f,.90f);colors.disabledColor=Color.white;colors.fadeDuration=.12f;button.colors=colors;return button;
    }

    static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
}
