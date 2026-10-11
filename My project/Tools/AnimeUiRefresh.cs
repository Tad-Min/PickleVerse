using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;
using PickleVerse.Audio;
using PickleVerse.Shop;
using PickleVerse.FrontEnd;
using Image=UnityEngine.UI.Image;
using Button=UnityEngine.UI.Button;

// Preserve existing buttons, listeners, serialized references and prefab GUIDs.
public static class AnimeUiRefresh
{
    const string Content="Assets/FrontEndContent/AnimeUI";
    static readonly Color White=Hex("F4F6FC"),Muted=Hex("A5B3CC"),Gold=Hex("E7CDA0"),Mint=Hex("8CE8D8"),Navy=Hex("101B30");
    static TMP_FontAsset font;
    static Material glass,dock,tab,selected,action,mintWallet,goldWallet,backdrop;
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");
        var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);
        Directory.CreateDirectory(Content);Directory.CreateDirectory("../UnityWork/AnimeUI/Before");AssetDatabase.Refresh();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FrontEndContent/Mode Vietnamese Font.asset");
        if(!Shader.Find("PickleVerse/UI/Arena Glass"))throw new Exception("Wait for Arena Glass shader import.");
        glass=Material("Glass",Hex("1D2B45",.96f),Hex("0A1428",.94f),Hex("93B0DB",.42f));
        dock=Material("Dock",Hex("1B2740",.93f),Hex("091123",.98f),Hex("AFBDD7",.35f));
        tab=Material("Tab",Hex("223149",.60f),Hex("0E182C",.76f),Hex("92ADC8",.27f));
        selected=Material("Selected",Hex("34405D",.98f),Hex("152239",.97f),Gold);
        action=Material("Gold Action",Hex("F3E4C8"),Hex("D9BE90"),Hex("FFF5D9"));
        mintWallet=Material("Tokenpad",Hex("193746",.96f),Hex("101E33",.96f),Hex("8CE8D8",.55f));
        goldWallet=Material("Elite",Hex("3B3442",.96f),Hex("1C2032",.96f),Hex("E7CDA0",.62f));
        backdrop=Material("Music Background",Hex("18253F"),Hex("080F20"),Hex("7291C0",.16f));
        StyleMusic();
        foreach(var path in new[]{"Assets/Scenes/UI/Menu.unity","Assets/Scenes/Shop.unity","Assets/Scenes/Collection.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);Backup(scene);
            if(scene.name=="Menu")StyleMenu();
            foreach(var wallet in UnityEngine.Object.FindObjectsByType<WalletBar>())StyleWallet(wallet);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        StyleModeSelection();
        EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
        return new{menu="Assets/Scenes/UI/Menu.unity",music="Assets/Audio/MusicUI/Music Player.prefab",walletScenes=3,modeTutorial=true};
    }
    static void StyleMenu()
    {
        var lobby=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();var safe=lobby.safeArea;
        var head=(RectTransform)safe.Find("Head");Place(head,0,.855f,1,1);Glass(head.Find("HeaderBackdrop"),dock);
        var name=(RectTransform)head.Find("NameTag");Place(name,.035f,.19f,.272f,.86f);Glass(name,glass);
        var avatar=(RectTransform)name.Find("ImgAvatar");Place(avatar,.046f,.17f,.208f,.83f);avatar.GetComponent<Image>().color=Gold;
        var initial=avatar.GetComponentInChildren<TMP_Text>();initial.color=Navy;initial.fontSize=25;
        var orbit=Glyph("Portrait Ring",name,ArenaGlyph.Orbit,Gold,.025f,.045f,.230f,.955f);orbit.stroke=.028f;
        Style(name.Find("TxtName").GetComponent<TMP_Text>(),null,.275f,.40f,.97f,.91f,20,White);
        Style(name.Find("TxtLevel").GetComponent<TMP_Text>(),"PICKLEVERSE / LOCAL",.275f,.11f,.97f,.39f,10,Muted);
        Style(head.Find("Brand").GetComponent<TMP_Text>(),"PICKLEVERSE",.31f,.37f,.59f,.82f,26,White,TextAlignmentOptions.Center);
        Label("Brand Caption",head,"PLAY YOUR STYLE",.31f,.16f,.59f,.39f,10,Gold,TextAlignmentOptions.Center);
        Glyph("Brand Star",head,ArenaGlyph.Star,Gold,.31f,.16f,.335f,.36f);
        var oldCurrency=head.Find("Currency");
        lobby.timeLabel.transform.SetParent(safe,false);
        var clock=Block("Atmosphere Badge",safe,.807f,.165f,.959f,.212f,glass);
        Style(lobby.timeLabel,null,.25f,.08f,.96f,.92f,12,Muted,TextAlignmentOptions.Center);lobby.timeLabel.transform.SetParent(clock,false);
        Place(lobby.timeLabel.rectTransform,.25f,.08f,.96f,.92f);Glyph("Atmosphere",clock,ArenaGlyph.Star,Gold,.05f,.12f,.22f,.88f);
        oldCurrency.gameObject.SetActive(false);
        var body=(RectTransform)safe.Find("Body");Place(body,.045f,.18f,.955f,.84f);
        var welcome=(RectTransform)body.Find("WelcomeCard");Place(welcome,0,.30f,.345f,.91f);Glass(welcome,glass);
        var rule=Block("Welcome Edge",welcome,.075f,.88f,.40f,.886f,null,Gold);rule.SetAsFirstSibling();
        Style(welcome.Find("Eyebrow").GetComponent<TMP_Text>(),"ĐẠI SẢNH  /  PICKLEVERSE",.075f,.895f,.94f,.97f,11,Gold);
        Style(welcome.Find("WelcomeTitle").GetComponent<TMP_Text>(),"Vào sân.\nTỏa sáng.",.075f,.525f,.945f,.855f,46,White);
        Style(welcome.Find("WelcomeBody").GetComponent<TMP_Text>(),"Tìm nhịp đấu của bạn.\nLuyện kỹ thuật, thể hiện phong cách.",.075f,.35f,.94f,.51f,16,Muted);
        var play=lobby.playButton;Place((RectTransform)play.transform,.075f,.13f,.925f,.30f);StyleButton(play,action,Navy);
        var playText=play.GetComponentInChildren<TMP_Text>();Style(playText,"CHƠI NGAY",.09f,0,.78f,1,22,Navy,TextAlignmentOptions.Center);
        var arrow=Glyph("Play Arrow",play.transform,ArenaGlyph.Arrow,Navy,.79f,.26f,.93f,.74f);Motion(play,arrow.rectTransform);
        Style(welcome.Find("PlayHint").GetComponent<TMP_Text>(),"Chọn chế độ  •  Enter",.075f,.025f,.925f,.11f,12,Muted,TextAlignmentOptions.Center);
        var backdropRect=(RectTransform)safe.Find("NavBackdrop");Place(backdropRect,0,0,1,.151f);Glass(backdropRect,dock);
        var nav=(RectTransform)safe.Find("Nav");Place(nav,.038f,.027f,.962f,.127f);
        var layout=nav.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();layout.spacing=8;
        var keys=new[]{"BtnHome","BtnProgression","BtnCareer","BtnCollection","BtnStore","BtnSetting"};
        var labels=new[]{"ĐẠI SẢNH","TIẾN TRÌNH","SỰ NGHIỆP","BỘ SƯU TẬP","CỬA HÀNG","CÀI ĐẶT"};
        var symbols=new[]{ArenaGlyph.Home,ArenaGlyph.Progress,ArenaGlyph.Career,ArenaGlyph.Collection,ArenaGlyph.Shop,ArenaGlyph.Settings};
        for(int i=0;i<keys.Length;i++)
        {
            var button=nav.Find(keys[i]).GetComponent<Button>();StyleButton(button,i==0?selected:tab,i==0?Gold:White);
            var text=button.GetComponentInChildren<TMP_Text>();Style(text,labels[i],.035f,.03f,.965f,.39f,13,button.interactable?(i==0?Gold:White):Muted,TextAlignmentOptions.Center);
            var icon=Glyph("Nav Icon",button.transform,symbols[i],button.interactable?(i==0?Gold:White):new Color(.45f,.53f,.65f),.40f,.42f,.60f,.88f);
            var feedback=Motion(button,icon.rectTransform);feedback.keepFocus=i==0;
        }
        var pending=safe.Find("PendingFeatures");if(pending)pending.gameObject.SetActive(false);
        var wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Place((RectTransform)wallet.transform,.627f,.895f,.965f,.975f);
    }
    static void StyleWallet(WalletBar wallet)
    {
        for(int i=0;i<2;i++)
        {
            var amount=i==0?wallet.tokenpad:wallet.elite;var panel=(RectTransform)amount.transform.parent;Glass(panel,i==0?mintWallet:goldWallet);
            Style(panel.Find("CurrencyName").GetComponent<TMP_Text>(),i==0?"TOKENPAD":"ELITE TOKEN",.075f,.58f,.73f,.89f,10,i==0?Mint:Gold);
            Style(amount,null,.075f,.14f,.735f,.64f,24,White);amount.enableAutoSizing=true;amount.fontSizeMin=16;amount.fontSizeMax=24;amount.textWrappingMode=TextWrappingModes.NoWrap;
            Place((RectTransform)panel.Find("CoinIcon"),.785f,.15f,.965f,.85f);
            Block("Coin Separator",panel,.763f,.2f,.767f,.8f,null,new Color(i==0?Mint.r:Gold.r,i==0?Mint.g:Gold.g,i==0?Mint.b:Gold.b,.28f));
        }
    }
    static void StyleModeSelection()
    {
        var s=EditorSceneManager.OpenScene("Assets/Scenes/UI/ModeSelection.unity");Backup(s);
        var ui=UnityEngine.Object.FindAnyObjectByType<ModeSelectionMenu>();
        var button=ui.safeArea.Find("Replay Basic Tutorial").GetComponent<Button>();ui.tutorialButton=button;
        Place((RectTransform)button.transform,.055f,.104f,.945f,.163f);StyleButton(button,glass,White);
        Style(button.GetComponentInChildren<TMP_Text>(),"TUTORIAL  /  HƯỚNG DẪN CƠ BẢN",.065f,.17f,.50f,.87f,16,White);
        Label("Tutorial Description",button.transform,"Làm quen sân, luật và phím đánh bóng",.49f,.16f,.925f,.85f,14,Muted,TextAlignmentOptions.Right);
        var book=Glyph("Tutorial Icon",button.transform,ArenaGlyph.Book,Gold,.012f,.18f,.049f,.82f);Motion(button,book.rectTransform);
        Glyph("Tutorial Arrow",button.transform,ArenaGlyph.Arrow,Gold,.95f,.25f,.98f,.75f);
        EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
    }
    static void StyleMusic()
    {
        string rowPath="Assets/Audio/MusicUI/Music Track.prefab";var root=PrefabUtility.LoadPrefabContents(rowPath);
        try
        {
            var row=root.GetComponent<MusicTrackRow>();Glass(root.transform,glass);
            row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=98;
            row.GetComponent<UnityEngine.UI.LayoutElement>().minHeight=98;
            Place(row.disc.rectTransform,.08f,.12f,.29f,.88f);
            row.number=Label("Track Number",root.transform,"01",.02f,.34f,.075f,.68f,10,Gold);
            Style(row.title,null,.33f,.60f,.97f,.90f,18,White);row.title.enableAutoSizing=true;row.title.fontSizeMin=13;row.title.fontSizeMax=18;
            Style(row.artist,null,.33f,.34f,.97f,.62f,12,Muted);
            Style(row.state,null,.33f,.11f,.78f,.34f,10,Mint);
            row.duration=Label("Track Length",root.transform,"0:00",.79f,.11f,.96f,.34f,10,Muted,TextAlignmentOptions.Right);
            Place(row.line.rectTransform,0,.08f,.008f,.92f);
            StyleButton(row.button,glass,White);Motion(row.button,row.disc.rectTransform);
            PrefabUtility.SaveAsPrefabAsset(root,rowPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        string path="Assets/Audio/MusicUI/Music Player.prefab";root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui=root.GetComponent<MusicUI>();Glass(ui.mini,glass);ui.mini.sizeDelta=new Vector2(310,58);
            ui.miniDisc.rectTransform.sizeDelta=new Vector2(44,44);ui.miniDisc.rectTransform.anchoredPosition=new Vector2(31,0);
            var caption=(RectTransform)ui.miniTextRoot.transform;caption.offsetMin=new Vector2(62,7);caption.offsetMax=new Vector2(-28,-7);
            ui.miniState.color=Gold;ui.miniState.fontSize=9;ui.miniTitle.fontSize=14;ui.miniTitle.color=White;
            Glyph("Mini Open",ui.mini,ArenaGlyph.Arrow,Gold,.915f,.30f,.98f,.70f);StyleButton(ui.miniButton,glass,White);Motion(ui.miniButton,null);
            var panel=ui.panel.transform;Glass(panel,backdrop);panel.GetComponent<Image>().raycastTarget=true;
            Style(panel.Find("Heading").GetComponent<TMP_Text>(),"Âm nhạc",.055f,.895f,.30f,.966f,38,White);
            Style(panel.Find("Brand").GetComponent<TMP_Text>(),"PICKLEVERSE  /  SOUND ROOM",.31f,.915f,.71f,.958f,13,Gold);
            StyleButton(ui.closeButton,tab,White);Style(ui.closeButton.GetComponentInChildren<TMP_Text>(),"ĐÓNG",.18f,0,.78f,1,13,White,TextAlignmentOptions.Center);
            var close=Glyph("Close Icon",ui.closeButton.transform,ArenaGlyph.Close,White,.76f,.24f,.95f,.76f);Motion(ui.closeButton,close.rectTransform);
            panel.Find("Rule").GetComponent<Image>().color=new Color(Gold.r,Gold.g,Gold.b,.32f);
            var library=Block("Library Surface",panel,.045f,.127f,.331f,.856f,glass);library.SetAsFirstSibling();
            Style(ui.libraryCount,null,.064f,.79f,.315f,.846f,13,Gold);
            var scroll=ui.playlistContent.parent.parent as RectTransform;Place(scroll,.062f,.198f,.315f,.774f);
            ui.playlistContent.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=8;
            Style(panel.Find("Library Hint").GetComponent<TMP_Text>(),"Chọn giai điệu của bạn",.064f,.136f,.315f,.186f,13,Muted);
            Style(panel.Find("Loop Hint").GetComponent<TMP_Text>(),"PHÁT LIÊN TỤC  /  LẶP DANH SÁCH",.064f,.082f,.33f,.12f,10,Gold);
            var stage=(RectTransform)panel.Find("Record Stage");Place(stage,.35f,.30f,.73f,.83f);
            var holder=stage.Find("Disc Square");holder.Find("Record Halo").GetComponent<Image>().color=new Color(.33f,.47f,.75f,.14f);
            var orbit=Glyph("Vinyl Orbit",holder,ArenaGlyph.RecordFrame,new Color(Gold.r,Gold.g,Gold.b,.48f),-.085f,-.085f,1.085f,1.085f);orbit.stroke=.0035f;
            Glyph("Vinyl Diamond",panel,ArenaGlyph.Star,Gold,.355f,.70f,.38f,.745f);
            ui.trackIndex=Label("Studio Index",panel,"01  /  LOBBY RADIO",.387f,.852f,.70f,.882f,10,Muted,TextAlignmentOptions.Center);
            Style(ui.title,null,.35f,.215f,.73f,.30f,37,White,TextAlignmentOptions.Center);ui.title.enableAutoSizing=true;ui.title.fontSizeMin=22;ui.title.fontSizeMax=37;
            Style(ui.artist,null,.35f,.166f,.73f,.215f,17,Muted,TextAlignmentOptions.Center);
            var info=(RectTransform)panel.Find("Track Info");Place(info,.765f,.265f,.95f,.76f);Glass(info,glass);
            Style(info.Find("Info Mark").GetComponent<TMP_Text>(),"GIAI ĐIỆU CỦA BẠN",.09f,.81f,.94f,.93f,11,Gold);
            Style(ui.genre,null,.09f,.63f,.94f,.81f,27,White);ui.genre.enableAutoSizing=true;
            Style(ui.status,null,.09f,.30f,.94f,.60f,16,Muted);
            StyleButton(ui.settingsButton,tab,White);Motion(ui.settingsButton,null);
            Label("Listening Hint",panel,"Giữ nhịp trước khi vào sân.",.765f,.18f,.95f,.238f,14,Muted);
            Style(ui.elapsed,null,.365f,.12f,.455f,.159f,11,Muted);Style(ui.duration,null,.65f,.12f,.716f,.159f,11,Muted,TextAlignmentOptions.Right);
            Place((RectTransform)ui.progress.transform,.365f,.108f,.716f,.151f);
            ui.progress.fillRect.GetComponent<Image>().color=Gold;ui.progress.handleRect.sizeDelta=new Vector2(16,16);
            Place((RectTransform)ui.previousButton.transform,.371f,.027f,.441f,.091f);StyleButton(ui.previousButton,tab,White);ui.previousButton.GetComponentInChildren<TMP_Text>().text="";
            var previous=Glyph("Previous Icon",ui.previousButton.transform,ArenaGlyph.Previous,White,.30f,.20f,.70f,.80f);Motion(ui.previousButton,previous.rectTransform);
            Place((RectTransform)ui.nextButton.transform,.648f,.027f,.718f,.091f);StyleButton(ui.nextButton,tab,White);ui.nextButton.GetComponentInChildren<TMP_Text>().text="";
            var next=Glyph("Next Icon",ui.nextButton.transform,ArenaGlyph.Next,White,.30f,.20f,.70f,.80f);Motion(ui.nextButton,next.rectTransform);
            Place((RectTransform)ui.playButton.transform,.46f,.024f,.626f,.095f);StyleButton(ui.playButton,action,Navy);Style(ui.playLabel,null,.24f,0,.96f,1,14,Navy,TextAlignmentOptions.Center);
            ui.playSymbol=Glyph("Playback Icon",ui.playButton.transform,ArenaGlyph.Pause,Navy,.08f,.21f,.23f,.79f);Motion(ui.playButton,ui.playSymbol.rectTransform);
            ui.rowPrefab=AssetDatabase.LoadAssetAtPath<MusicTrackRow>(rowPath);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static Material Material(string name,Color top,Color bottom,Color edge)
    {
        string path=Content+"/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!value){value=new Material(Shader.Find("PickleVerse/UI/Arena Glass")){name=name};AssetDatabase.CreateAsset(value,path);}
        value.SetColor("_Top",top);value.SetColor("_Bottom",bottom);value.SetColor("_Border",edge);value.SetFloat("_Cut",.045f);EditorUtility.SetDirty(value);AssetDatabase.SaveAssetIfDirty(value);return value;
    }
    static void Backup(Scene scene){string path="../UnityWork/AnimeUI/Before/"+scene.name+".unity";if(!File.Exists(path))EditorSceneManager.SaveScene(scene,path,true);}
    static void Glass(Transform transform,Material material){var image=transform.GetComponent<Image>();bool blocks=image&&image.raycastTarget;if(!image)image=transform.gameObject.AddComponent<Image>();image.sprite=null;image.material=material;image.color=Color.white;image.raycastTarget=blocks||transform.GetComponent<Button>()!=null;}
    static void StyleButton(Button button,Material material,Color text)
    {Glass(button.transform,material);button.targetGraphic=button.GetComponent<Image>();var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.12f,1.12f,1.12f);colors.selectedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.77f,.84f,.93f);colors.disabledColor=new Color(.44f,.48f,.57f,.68f);colors.fadeDuration=.12f;button.colors=colors;}
    static UiButtonMotion Motion(Button button,RectTransform icon)
    {var motion=button.GetComponent<UiButtonMotion>()??button.gameObject.AddComponent<UiButtonMotion>();motion.symbol=icon;var focus=Block("Focus Edge",button.transform,.075f,.014f,.925f,.035f,null,new Color(Gold.r,Gold.g,Gold.b,0));motion.focus=focus.GetComponent<Image>();return motion;}
    static UiLineIcon Glyph(string name,Transform parent,ArenaGlyph glyph,Color color,float x,float y,float right,float top)
    {var r=Rect(name,parent,x,y,right,top);if(!r.GetComponent<CanvasRenderer>())r.gameObject.AddComponent<CanvasRenderer>();var value=r.GetComponent<UiLineIcon>()??r.gameObject.AddComponent<UiLineIcon>();value.glyph=glyph;value.color=color;value.raycastTarget=false;return value;}
    static RectTransform Block(string name,Transform parent,float x,float y,float right,float top,Material material,Color? color=null)
    {var r=Rect(name,parent,x,y,right,top);var image=r.GetComponent<Image>()??r.gameObject.AddComponent<Image>();image.material=material;image.color=color??Color.white;image.raycastTarget=false;return r;}
    static TMP_Text Label(string name,Transform parent,string value,float x,float y,float right,float top,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {var r=Rect(name,parent,x,y,right,top);var text=r.GetComponent<TMP_Text>()??r.gameObject.AddComponent<TextMeshProUGUI>();Style(text,value,x,y,right,top,size,color,align);return text;}
    static void Style(TMP_Text text,string value,float x,float y,float right,float top,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {Place(text.rectTransform,x,y,right,top);if(value!=null)text.text=value;text.font=font;text.fontSize=size;text.enableAutoSizing=false;text.fontStyle=text.name=="WelcomeBody"||text.name=="Artist"||text.name=="Status"||text.name=="Tutorial Description"?FontStyles.Normal:FontStyles.Bold;text.color=color;text.alignment=align;text.raycastTarget=false;}
    static RectTransform Rect(string name,Transform parent,float x,float y,float right,float top)
    {var child=parent.Find(name);var r=child?(RectTransform)child:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();if(!child)r.SetParent(parent,false);r.gameObject.layer=5;Place(r,x,y,right,top);return r;}
    static void Place(RectTransform r,float x,float y,float right,float top){r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
    static Color Hex(string hex,float alpha=1){ColorUtility.TryParseHtmlString("#"+hex,out var result);result.a=alpha;return result;}
}
