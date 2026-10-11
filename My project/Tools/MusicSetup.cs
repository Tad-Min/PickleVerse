using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PickleVerse.Audio;

public static class MusicSetup
{
    static TMP_FontAsset font;
    static Sprite circle;
    static readonly Color Ink=new Color(.015f,.040f,.055f),Panel=new Color(.032f,.077f,.10f),Mint=new Color(.42f,.86f,.77f),Muted=new Color(.53f,.67f,.71f);
    static readonly string[] audio={
        "Apollo On The Run - Echo Electronic NCS - Copyright Free Music.mp3",
        "BEKSY. - Stars in the Sky Electronic Pop NCS - Copyright Free Music.mp3",
        "JVNA - Taking It Slow Future Bass NCS - Copyright Free Music.mp3",
        "Rameses B - Keep You Phonk NCS - Copyright Free Music.mp3"};
    static readonly string[] discs={"Đĩa Nhạc Apollo on the sun.png","Đĩa Nhạc Vũ Trụ Stars in the Sky.png","Đĩa Nhạc Taking It Slow Future Bass NCS.png","Đĩa Nhạc Keep you.png"};
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first.");
        const string libraryPath="Assets/Resources/Audio/Front End Music.asset";
        if(AssetDatabase.LoadAssetAtPath<MusicLibrary>(libraryPath))throw new Exception("Music is already installed. Edit existing assets.");
        Directory.CreateDirectory("../UnityWork/Music");Directory.CreateDirectory("Assets/Audio/MusicUI");AssetDatabase.Refresh();
        var before=audio.Select(file=>ReadClip("Assets/Audio/Sound/"+file)).ToArray();
        File.WriteAllText("../UnityWork/Music/import-before.json",Newtonsoft.Json.JsonConvert.SerializeObject(before,Newtonsoft.Json.Formatting.Indented));
        foreach(string file in audio)
        {
            string path="Assets/Audio/Sound/"+file;var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono=false;importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;settings.preloadAudioData=false;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
        foreach(string file in discs)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Audio/Đĩa_nhạc/"+file);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        }
        var titles=new[]{"Echo","Stars in the Sky","Taking It Slow","Keep You"};var artists=new[]{"Apollo On The Run","BEKSY.","JVNA","Rameses B"};var genres=new[]{"Electronic","Electronic Pop","Future Bass","Phonk"};
        var colors=new[]{new Color(.30f,.68f,1),new Color(.86f,.48f,1),new Color(.33f,.87f,.84f),new Color(1,.38f,.44f)};
        var tracks=Enumerable.Range(0,4).Select(i=>new MusicTrack{id="music."+new[]{"echo","stars","slow","keep"}[i],title=titles[i],artist=artists[i],genre=genres[i],accent=colors[i],clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Sound/"+audio[i]),disc=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Audio/Đĩa_nhạc/"+discs[i])}).ToArray();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalMatchContent/Match Vietnamese Font.asset");circle=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var work=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);UnityEngine.SceneManagement.SceneManager.SetActiveScene(work);
        GameObject prefab;
        try{prefab=BuildUI(tracks[0]);}
        finally{UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);EditorSceneManager.CloseScene(work,true);}
        var library=ScriptableObject.CreateInstance<MusicLibrary>();library.tracks=tracks;library.uiPrefab=prefab;AssetDatabase.CreateAsset(library,libraryPath);AssetDatabase.SaveAssetIfDirty(library);
        UpdateSettingsNote();
        var after=audio.Select(file=>ReadClip("Assets/Audio/Sound/"+file)).ToArray();File.WriteAllText("../UnityWork/Music/import-after.json",Newtonsoft.Json.JsonConvert.SerializeObject(after,Newtonsoft.Json.Formatting.Indented));
        return new{tracks=after,prefab=AssetDatabase.GetAssetPath(prefab),musicResource=libraryPath};
    }
    static object ReadClip(string path)
    {
        var c=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var i=(AudioImporter)AssetImporter.GetAtPath(path);var settings=i.defaultSampleSettings;
        return new{path,c.length,c.channels,c.frequency,load=settings.loadType.ToString(),compression=settings.compressionFormat.ToString(),settings.quality,preload=settings.preloadAudioData,background=i.loadInBackground};
    }
    static GameObject BuildUI(MusicTrack first)
    {
        var root=Rect("Music UI",null,Vector2.zero,Vector2.one);var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;root.gameObject.AddComponent<GraphicRaycaster>();
        var scaler=root.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var ui=root.gameObject.AddComponent<MusicUI>();ui.canvas=canvas;ui.safeArea=Rect("Safe Area",root,Vector2.zero,Vector2.one);
        ui.mini=Block("Now Playing",ui.safeArea,new Vector2(.045f,.21f),new Vector2(.045f,.21f),new Color(Ink.r,Ink.g,Ink.b,.92f),true);ui.mini.pivot=new Vector2(0,.5f);ui.mini.sizeDelta=new Vector2(286,48);
        ui.miniButton=ui.mini.gameObject.AddComponent<Button>();ui.miniButton.targetGraphic=ui.mini.GetComponent<Image>();
        var small=Rect("Spinning Disc",ui.mini,new Vector2(0,.5f),new Vector2(0,.5f));small.pivot=new Vector2(.5f,.5f);small.sizeDelta=new Vector2(38,38);small.anchoredPosition=new Vector2(24,0);ui.miniDisc=small.gameObject.AddComponent<Image>();ui.miniDisc.sprite=first.disc;ui.miniDisc.preserveAspect=true;ui.miniDisc.raycastTarget=false;
        var text=Rect("Caption",ui.mini,Vector2.zero,Vector2.one);text.offsetMin=new Vector2(52,3);text.offsetMax=new Vector2(-10,-3);ui.miniTextRoot=text.gameObject;
        ui.miniState=Text("State",text,"ĐANG PHÁT",new Vector2(0,.55f),Vector2.one,10,Mint,true);
        ui.miniViewport=Rect("Title Viewport",text,Vector2.zero,new Vector2(1,.57f));ui.miniViewport.gameObject.AddComponent<RectMask2D>();
        ui.miniTitle=Text("Marquee",ui.miniViewport,"Echo • Apollo On The Run",Vector2.zero,Vector2.one,15,Color.white);
        ui.miniCaption=ui.miniTitle.rectTransform;ui.miniCaption.anchorMin=new Vector2(0,0);ui.miniCaption.anchorMax=new Vector2(0,1);ui.miniCaption.pivot=new Vector2(0,.5f);ui.miniCaption.sizeDelta=new Vector2(300,0);ui.miniTitle.textWrappingMode=TextWrappingModes.NoWrap;ui.miniTitle.overflowMode=TextOverflowModes.Overflow;
        var panel=Block("Music Player",ui.safeArea,Vector2.zero,Vector2.one,Ink,true);ui.panel=panel.gameObject;
        Text("Heading",panel,"ÂM NHẠC",new Vector2(.055f,.905f),new Vector2(.30f,.973f),36,Color.white,true);
        Text("Brand",panel,"PICKLEVERSE  /  PLAYLIST",new Vector2(.31f,.924f),new Vector2(.69f,.963f),14,Muted);
        ui.closeButton=Button("Close",panel,"ĐÓNG",new Vector2(.865f,.916f),new Vector2(.95f,.972f),Panel,Color.white);
        Block("Rule",panel,new Vector2(.055f,.885f),new Vector2(.95f,.888f),new Color(.16f,.32f,.37f));
        ui.libraryCount=Text("Library Count",panel,"THƯ VIỆN  /  4 BÀI",new Vector2(.055f,.805f),new Vector2(.31f,.85f),14,Mint,true);
        Text("Library Hint",panel,"Chọn một bài để phát",new Vector2(.055f,.13f),new Vector2(.31f,.172f),15,Muted);
        Text("Loop Hint",panel,"PHÁT LẦN LƯỢT  •  LẶP PLAYLIST",new Vector2(.055f,.067f),new Vector2(.32f,.112f),12,Mint);
        var scroll=Rect("Playlist Scroll",panel,new Vector2(.055f,.18f),new Vector2(.315f,.79f));var view=Block("Viewport",scroll,Vector2.zero,Vector2.one,Color.white,true);view.gameObject.AddComponent<Mask>().showMaskGraphic=false;
        var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.viewport=view;sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;
        ui.playlistContent=Rect("Content",view,new Vector2(0,1),Vector2.one);ui.playlistContent.pivot=new Vector2(.5f,1);
        var layout=ui.playlistContent.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
        ui.playlistContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;sr.content=ui.playlistContent;
        ui.rowPrefab=TrackRow();
        var stage=Rect("Record Stage",panel,new Vector2(.35f,.28f),new Vector2(.70f,.805f));
        var holder=Rect("Disc Square",stage,Vector2.zero,Vector2.one);holder.gameObject.AddComponent<AspectRatioFitter>().aspectMode=AspectRatioFitter.AspectMode.FitInParent;holder.GetComponent<AspectRatioFitter>().aspectRatio=1;
        var glow=Block("Record Halo",holder,Vector2.zero,Vector2.one,new Color(.13f,.34f,.4f,.20f));glow.GetComponent<Image>().sprite=circle;
        ui.discTransform=Rect("Rotating Record",holder,new Vector2(.04f,.04f),new Vector2(.96f,.96f));ui.disc=ui.discTransform.gameObject.AddComponent<Image>();ui.disc.sprite=first.disc;ui.disc.preserveAspect=true;ui.disc.raycastTarget=false;
        ui.title=Text("Track Title",panel,first.title,new Vector2(.335f,.20f),new Vector2(.715f,.275f),34,Color.white,true,TextAlignmentOptions.Center);ui.title.enableAutoSizing=true;ui.title.fontSizeMin=22;ui.title.fontSizeMax=34;ui.title.textWrappingMode=TextWrappingModes.NoWrap;
        ui.artist=Text("Artist",panel,first.artist,new Vector2(.335f,.16f),new Vector2(.715f,.205f),19,Muted,false,TextAlignmentOptions.Center);
        var info=Block("Track Info",panel,new Vector2(.745f,.28f),new Vector2(.95f,.76f),Panel);
        ui.accent=Block("Accent",info,Vector2.zero,new Vector2(.014f,1),first.accent).GetComponent<Image>();
        Text("Info Mark",info,"TRẢI NGHIỆM CỦA BẠN",new Vector2(.085f,.83f),new Vector2(.93f,.94f),12,Mint,true);
        ui.genre=Text("Genre",info,first.genre.ToUpperInvariant(),new Vector2(.085f,.61f),new Vector2(.94f,.83f),26,Color.white,true);ui.genre.enableAutoSizing=true;ui.genre.fontSizeMin=18;ui.genre.fontSizeMax=26;
        ui.status=Text("Status",info,"Tự động phát tiếp • Nhạc dừng khi vào trận đấu",new Vector2(.085f,.33f),new Vector2(.94f,.59f),17,Muted);
        ui.settingsButton=Button("Audio Settings",info,"ÂM LƯỢNG / CÀI ĐẶT",new Vector2(.085f,.09f),new Vector2(.93f,.25f),new Color(.08f,.20f,.22f),Mint,13);
        ui.elapsed=Text("Elapsed",panel,"0:00",new Vector2(.35f,.13f),new Vector2(.45f,.16f),12,Muted);
        ui.duration=Text("Duration",panel,"2:50",new Vector2(.63f,.13f),new Vector2(.70f,.16f),12,Muted,false,TextAlignmentOptions.Right);
        ui.progress=Progress(panel);
        ui.previousButton=Button("Previous",panel,"TRƯỚC",new Vector2(.35f,.030f),new Vector2(.43f,.095f),Panel,Muted,13);
        ui.playButton=Button("Play Pause",panel,"TẠM DỪNG",new Vector2(.448f,.028f),new Vector2(.61f,.103f),Mint,Ink,16);ui.playLabel=ui.playButton.GetComponentInChildren<TMP_Text>();
        ui.nextButton=Button("Next",panel,"SAU",new Vector2(.63f,.030f),new Vector2(.71f,.095f),Panel,Muted,13);
        panel.gameObject.SetActive(false);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Audio/MusicUI/Music Player.prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab;
    }
    static MusicTrackRow TrackRow()
    {
        var root=Block("Music Track",null,Vector2.zero,Vector2.one,Panel,true);var element=root.gameObject.AddComponent<LayoutElement>();element.preferredHeight=94;element.minHeight=94;
        var row=root.gameObject.AddComponent<MusicTrackRow>();row.background=root.GetComponent<Image>();row.button=root.gameObject.AddComponent<Button>();row.button.targetGraphic=row.background;
        row.line=Block("Selected Edge",root,Vector2.zero,new Vector2(.012f,1),Mint).GetComponent<Image>();
        row.disc=Rect("Disc",root,new Vector2(.025f,.13f),new Vector2(.255f,.87f)).gameObject.AddComponent<Image>();row.disc.preserveAspect=true;row.disc.raycastTarget=false;
        row.title=Text("Title",root,"Title",new Vector2(.29f,.56f),new Vector2(.96f,.92f),19,Color.white,true);row.title.enableAutoSizing=true;row.title.fontSizeMin=13;row.title.fontSizeMax=19;
        row.artist=Text("Artist",root,"Artist",new Vector2(.29f,.32f),new Vector2(.96f,.60f),13,Muted);
        row.state=Text("State",root,"PHÁT →",new Vector2(.29f,.085f),new Vector2(.96f,.32f),10,Mint,true);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Audio/MusicUI/Music Track.prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab.GetComponent<MusicTrackRow>();
    }
    static Slider Progress(Transform parent)
    {
        var root=Block("Progress",parent,new Vector2(.35f,.088f),new Vector2(.70f,.14f),Color.clear,true);var slider=root.gameObject.AddComponent<Slider>();
        var bg=Block("Track",root,new Vector2(0,.46f),new Vector2(1,.54f),new Color(.13f,.26f,.30f),true);
        var fill=Block("Fill",bg,Vector2.zero,Vector2.one,Mint);slider.fillRect=fill;
        var area=Rect("Handle Area",root,new Vector2(0,.5f),new Vector2(1,.5f));
        var handle=Block("Handle",area,new Vector2(0,0),new Vector2(0,1),Color.white,true);handle.sizeDelta=new Vector2(12,12);handle.GetComponent<Image>().sprite=circle;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;return slider;
    }
    static void UpdateSettingsNote()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Settings.unity",OpenSceneMode.Additive);
        var note=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).First(t=>t.name=="Note"&&t.text.Contains("Nhạc nền"));
        note.text="Nhạc nền phát ở các màn giao diện và tự dừng khi vào trận. Hiệu ứng trận đấu có âm lượng riêng.";
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
    }
    static Button Button(string name,Transform parent,string label,Vector2 min,Vector2 max,Color bg,Color fg,int size=16)
    {var r=Block(name,parent,min,max,bg,true);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();Text("Label",r,label,new Vector2(.04f,0),new Vector2(.96f,1),size,fg,true,TextAlignmentOptions.Center);return b;}
    static TMP_Text Text(string name,Transform parent,string label,Vector2 min,Vector2 max,int size,Color color,bool bold=false,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=label;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;if(bold)t.fontStyle=FontStyles.Bold;return t;}
    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool raycast=false)
    {var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return r;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;if(parent)r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
}
