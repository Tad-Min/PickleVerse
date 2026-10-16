using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using TMPro;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using Image=UnityEngine.UI.Image;
using Button=UnityEngine.UI.Button;

public static class RefreshLobbyUi
{
    const string Atlas="Assets/FrontEndContent/LobbyRefresh/Lobby_UI_Atlas.png",Theme="Assets/FrontEndContent/LobbyRefresh/Lobby UI Theme.asset";
    static PlayerProgressionMenu ui;
    static LobbyUiTheme theme;
    static void Bounds(RectTransform r,float x0,float y0,float x1,float y1)
    {r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.pivot=new Vector2(.5f,.5f);r.offsetMin=r.offsetMax=Vector2.zero;}
    static RectTransform Rect(string name,Transform parent)
    {
        var found=parent.Find(name);if(found)return (RectTransform)found;
        return ui.Rect(name,parent,Vector2.zero,Vector2.one);
    }
    static Image Picture(string name,Transform parent,Sprite sprite,float x0=0,float y0=0,float x1=1,float y1=1)
    {
        var rect=Rect(name,parent);Bounds(rect,x0,y0,x1,y1);
        var image=rect.GetComponent<Image>();if(!image)image=rect.gameObject.AddComponent<Image>();
        image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;image.type=Image.Type.Simple;
        return image;
    }
    static void Hide(Transform parent,string name){var t=parent.Find(name);if(t)t.gameObject.SetActive(false);}
    static void Skin(RectTransform rect,Sprite sprite,Color color,bool raycast=false)
    {
        var image=rect.GetComponent<Image>();if(!image)image=rect.gameObject.AddComponent<Image>();
        image.sprite=sprite;image.material=null;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=raycast;
    }
    static void StyleButton(Button button,Sprite icon,string caption,bool home=false)
    {
        Skin((RectTransform)button.transform,theme.panel,home?new Color(.76f,.89f,.98f):new Color(.52f,.64f,.80f,.96f),true);
        var old=button.GetComponent<UiButtonMotion>();if(old)old.enabled=false;
        foreach(Transform t in button.transform)
            if(!t.GetComponent<TMP_Text>()&&!t.name.StartsWith("Refresh"))t.gameObject.SetActive(false);
        var text=button.GetComponentInChildren<TMP_Text>(true);text.gameObject.SetActive(true);text.text=caption;text.color=new Color(.96f,.96f,.93f);
        text.alignment=TextAlignmentOptions.Center;text.enableAutoSizing=true;text.fontSizeMin=10;text.fontSizeMax=12;text.fontSize=12;
        Bounds(text.rectTransform,.03f,.025f,.97f,.37f);
        var symbol=Picture("Refresh Icon",button.transform,icon,.33f,.38f,.67f,.96f);symbol.preserveAspect=true;
        var edge=Picture("Refresh Accent",button.transform,null,.15f,.025f,.85f,.055f);edge.color=new Color(.32f,.95f,.82f,0);
        var clip=Rect("Refresh Shine",button.transform);Bounds(clip,0,0,1,1);
        if(!clip.GetComponent<UnityEngine.UI.RectMask2D>())clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        var shine=Picture("Sweep",clip,theme.sweep);shine.rectTransform.anchorMin=shine.rectTransform.anchorMax=new Vector2(.5f,.5f);
        shine.rectTransform.sizeDelta=new Vector2(35,130);shine.color=new Color(1,1,1,0);
        var feedback=button.GetComponent<LobbyUiFeedback>();if(!feedback)feedback=button.gameObject.AddComponent<LobbyUiFeedback>();
        feedback.symbol=symbol.rectTransform;feedback.accent=edge;feedback.keepFocus=home;feedback.sweep=shine.rectTransform;feedback.sweepImage=shine;
        button.interactable=true;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.selectedColor=Color.white;colors.pressedColor=new Color(.8f,.91f,.93f);button.colors=colors;
    }
    static AnimationCurve Curve(JToken keys)
    {return new AnimationCurve(keys.Select(k=>new Keyframe((float)k[0],(float)k[1])).ToArray());}
    public static object ImportArt()
    {
        AssetDatabase.ImportAsset(Atlas,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=false;importer.maxTextureSize=1024;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
        foreach(var target in new[]{"Standalone","Android","iPhone"})
        {
            var platform=importer.GetPlatformTextureSettings(target);platform.name=target;platform.overridden=true;platform.maxTextureSize=1024;
            platform.format=target=="Standalone"?TextureImporterFormat.BC7:TextureImporterFormat.ASTC_6x6;importer.SetPlatformTextureSettings(platform);
        }
        importer.SaveAndReimport();
        var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var editor=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(editor==null)throw new Exception("Sprite edit capability unavailable; no slicing performed.");
        var capability=editor.GetEditCapability();
        foreach(var cap in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditBorder,EEditCapability.EditPivot})
            if(!capability.HasCapability(cap))throw new Exception("Sprite capability not supported: "+cap);
        var doc=JObject.Parse(File.ReadAllText("Tools/Blender/lobby-ui-manifest.json"));
        var prior=provider.GetSpriteRects();
        var rects=doc["sprites"].Select(item=>
        {
            string name=(string)item["name"];bool panel=name=="Panel"||name=="Play";float inset=panel?8:0;
            return new SpriteRect{name=name,spriteID=prior.FirstOrDefault(r=>r.name==name)?.spriteID??GUID.Generate(),
                rect=new Rect((float)item["x"]+inset,(float)item["y"]+inset,256-inset*2,256-inset*2),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=panel?Vector4.one*24:Vector4.zero};
        }).ToArray();
        provider.SetSpriteRects(rects);
        var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(names==null)throw new Exception("Sprite name/file ID provider unavailable; no apply.");
        names.SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s=>s.name);
        theme=AssetDatabase.LoadAssetAtPath<LobbyUiTheme>(Theme);
        if(!theme){theme=ScriptableObject.CreateInstance<LobbyUiTheme>();AssetDatabase.CreateAsset(theme,Theme);}
        theme.panel=sprites["Panel"];theme.play=sprites["Play"];theme.paddle=sprites["Paddle"];theme.ball=sprites["Ball"];theme.impact=sprites["Impact"];theme.sweep=sprites["Sweep"];theme.avatarRing=sprites["AvatarRing"];theme.social=sprites["Social"];theme.arrow=sprites["Arrow"];
        theme.navigation=new[]{"Home","Progress","Career","Missions","Collection","Shop","Settings"}.Select(n=>sprites[n]).ToArray();
        var motion=doc["motion"];theme.paddleAngle=Curve(motion["paddle"]);theme.paddleX=Curve(motion["paddleX"]);theme.ballX=Curve(motion["ballX"]);theme.ballY=Curve(motion["ballY"]);theme.strikeDuration=(float)motion["duration"];theme.impactTime=(float)motion["impactTime"];
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
        return new{sprites=sprites.Count,bytes=new FileInfo(Atlas).Length};
    }
    public static object Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Author in Edit mode.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/UI/Menu.unity")throw new Exception("Menu must be active.");
        ui=UnityEngine.Object.FindAnyObjectByType<PlayerProgressionMenu>();theme=AssetDatabase.LoadAssetAtPath<LobbyUiTheme>(Theme);
        if(!ui||!theme)throw new Exception("Missing Menu or imported theme.");
        Undo.RegisterFullObjectHierarchyUndo(ui.gameObject,"Refresh lobby UI");
        var safe=(RectTransform)ui.transform.Find("HomeSafeArea");var head=safe.Find("Head");var nav=(RectTransform)safe.Find("Nav");
        Hide(head,"HeaderBackdrop");Hide(head,"Brand");Hide(head,"Brand Caption");Hide(head,"Brand Star");Hide(head,"BtnEditPlayer");Hide(safe,"NavBackdrop");
        var headLayout=head.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();if(headLayout)headLayout.enabled=false;
        var shell=ui.GetComponent<HomeMenuShell>();if(!shell)shell=ui.gameObject.AddComponent<HomeMenuShell>();shell.theme=theme;shell.safe=safe;
        shell.profile=(RectTransform)(head.Find("NameTag")??safe.Find("NameTag"));shell.profile.SetParent(safe,false);
        Skin(shell.profile,theme.panel,new Color(.61f,.74f,.89f,.98f),true);
        Hide(shell.profile,"Portrait Ring");
        var ring=Picture("Refresh Avatar Ring",shell.profile,theme.avatarRing,.025f,.08f,.225f,.92f);ring.preserveAspect=true;
        var avatar=(RectTransform)shell.profile.Find("ImgAvatar");Bounds(avatar,.045f,.18f,.205f,.82f);
        var aspect=avatar.GetComponent<UnityEngine.UI.AspectRatioFitter>();if(aspect)aspect.enabled=false;
        Bounds((RectTransform)avatar.Find("Initial"),0,0,1,1);
        Bounds(ui.menuName.rectTransform,.265f,.42f,.95f,.89f);ui.menuName.enableAutoSizing=true;ui.menuName.fontSizeMin=14;
        Bounds(ui.menuLevel.rectTransform,.265f,.13f,.95f,.42f);ui.menuLevel.enableAutoSizing=true;ui.menuLevel.fontSizeMin=9;
        shell.wallet=(RectTransform)safe.Find("Wallet");
        foreach(Transform card in shell.wallet)Skin((RectTransform)card,theme.panel,new Color(.55f,.73f,.81f,.98f));
        var welcome=safe.Find("Body/WelcomeCard");var play=(RectTransform)welcome.Find("BtnPlay");
        if(!play)play=(RectTransform)safe.Find("BtnPlay");
        play.SetParent(safe,false);shell.play=play;shell.playButton=play.GetComponent<Button>();
        welcome.gameObject.SetActive(false);
        ui.missionsButton.transform.SetParent(nav,false);ui.missionsButton.transform.SetSiblingIndex(3);
        var horizontal=nav.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();if(horizontal)Undo.DestroyObjectImmediate(horizontal);
        var grid=nav.GetComponent<UnityEngine.UI.GridLayoutGroup>();if(!grid)grid=nav.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();shell.navigation=grid;shell.nav=nav;
        var homeButton=nav.Find("BtnHome");if(homeButton)Undo.DestroyObjectImmediate(homeButton.gameObject);
        var buttons=nav.GetComponentsInChildren<Button>(true);string[] captions={"TIẾN TRÌNH","LỊCH SỬ","NHIỆM VỤ","BỘ SƯU TẬP","CỬA HÀNG","CÀI ĐẶT"};
        for(int i=0;i<buttons.Length;i++)StyleButton(buttons[i],theme.navigation[i+1],captions[i]);
        StyleButton(shell.playButton,theme.paddle,"CHƠI NGAY");
        Skin(play,theme.play,Color.white,true);
        var playColors=shell.playButton.colors;playColors.disabledColor=Color.white;shell.playButton.colors=playColors;
        var label=shell.playButton.GetComponentInChildren<TMP_Text>();label.color=new Color(.04f,.10f,.14f);Bounds(label.rectTransform,.06f,.25f,.94f,.82f);label.fontSizeMin=18;label.fontSizeMax=26;shell.playTitle=label;
        var paddle=play.Find("Refresh Icon") as RectTransform;paddle.anchorMin=paddle.anchorMax=new Vector2(.17f,.55f);paddle.sizeDelta=Vector2.one*172;paddle.anchoredPosition=Vector2.zero;shell.paddle=paddle;
        var ball=Picture("Refresh Ball",play,theme.ball);ball.rectTransform.anchorMin=ball.rectTransform.anchorMax=new Vector2(.325f,.80f);ball.rectTransform.sizeDelta=new Vector2(30,30);ball.rectTransform.anchoredPosition=Vector2.zero;shell.ball=ball.rectTransform;
        var flash=Picture("Refresh Impact",play,theme.impact);flash.rectTransform.anchorMin=flash.rectTransform.anchorMax=new Vector2(.325f,.80f);flash.rectTransform.sizeDelta=new Vector2(80,80);flash.rectTransform.anchoredPosition=Vector2.zero;flash.color=new Color(1,1,1,0);shell.flash=flash;
        var hint=play.Find("Refresh Hint")?.GetComponent<TMP_Text>();
        if(!hint)hint=ui.Text("Refresh Hint",play,"ENTER",10,new Color(.17f,.31f,.34f),new Vector2(.38f,.11f),new Vector2(.90f,.31f),Vector2.zero,Vector2.zero,TextAlignmentOptions.Center);
        shell.playHint=hint;
        for(int i=shell.playButton.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(shell.playButton.onClick,i);
        UnityEventTools.AddPersistentListener(shell.playButton.onClick,shell.Play);
        var social=ui.GetComponent<OfflineSocialMenu>();social.drawerMode=true;shell.social=social;
        var handle=(RectTransform)social.friendsButton.transform;handle.SetParent(safe,false);handle.name="Social Drawer Handle";shell.drawerHandle=handle;
        StyleButton(social.friendsButton,theme.social,"");
        var handleIcon=handle.Find("Refresh Icon") as RectTransform;Bounds(handleIcon,.10f,.42f,.90f,.80f);
        var chevron=Picture("Refresh Arrow",handle,theme.arrow,.24f,.12f,.76f,.36f);chevron.preserveAspect=true;
        social.leaderboardButton.gameObject.SetActive(false);
        Skin(social.window,theme.panel,new Color(.22f,.35f,.49f,1));
        social.window.GetComponent<UnityEngine.UI.Outline>().enabled=false;
        social.modal.GetComponent<Image>().raycastTarget=false;
        var dismiss=Rect("Dismiss",social.modal);Bounds(dismiss,0,0,1,1);dismiss.SetAsFirstSibling();
        var dim=dismiss.GetComponent<Image>();if(!dim)dim=dismiss.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.36f);dim.raycastTarget=true;social.drawerShade=dim;
        var dismissButton=dismiss.GetComponent<Button>();if(!dismissButton)dismissButton=dismiss.gameObject.AddComponent<Button>();dismissButton.targetGraphic=dim;dismissButton.transition=UnityEngine.UI.Selectable.Transition.None;
        if(dismissButton.onClick.GetPersistentEventCount()==0)UnityEventTools.AddPersistentListener(dismissButton.onClick,social.Close);
        social.modal.GetComponent<Image>().color=Color.clear;
        social.window.Find("Subtitle").GetComponent<TMP_Text>().text="BẠN BÈ  /  XẾP HẠNG ELO · CỤC BỘ";
        Bounds(social.title.rectTransform,.04f,.91f,.84f,.98f);
        Bounds((RectTransform)social.window.Find("Subtitle"),.04f,.86f,.94f,.91f);
        social.ConfigureDrawerLayout();
        shell.character=(RectTransform)safe.Find("Character Selection");shell.atmosphere=(RectTransform)safe.Find("Atmosphere Badge");
        foreach(var b in shell.character.GetComponentsInChildren<Button>())StyleButton(b,null,b==UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>().femaleButton?"NỮ":"NAM");
        // Character buttons retain text-only labels and their existing selection callbacks.
        foreach(var b in shell.character.GetComponentsInChildren<Button>()){Hide(b.transform,"Refresh Icon");Bounds(b.GetComponentInChildren<TMP_Text>().rectTransform,0,0,1,1);}
        typeof(HomeMenuShell).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(shell,null);
        EditorUtility.SetDirty(social);EditorUtility.SetDirty(ui);EditorUtility.SetDirty(shell);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{navigation=buttons.Length,scene=scene.path};
    }
}

