using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Settings;

public static class LoadingSceneSetup
{
    const string Content="Assets/FrontEndContent/Loading",Prefab="Assets/Resources/Loading/LoadingScreen.prefab",ScenePath="Assets/Scenes/UI/Loading.unity";
    static TMP_FontAsset font;
    static RectTransform Rect(string name,Transform parent,float x,float y,float right,float top)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(right,top);r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static UnityEngine.UI.Image Image(string name,Transform parent,float x,float y,float right,float top,Color color)
    {var r=Rect(name,parent,x,y,right,top);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;}
    static TMP_Text Text(string name,Transform parent,string value,float size,float x,float y,float right,float top,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var r=Rect(name,parent,x,y,right,top);var text=r.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;return text;
    }
    static void Import(string path,bool alpha,int size)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Missing artwork: "+path);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaSource=alpha?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;importer.alphaIsTransparency=alpha;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=size;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.CompressedHQ;
        var desktop=new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=size,format=alpha?TextureImporterFormat.DXT5:TextureImporterFormat.BC7,compressionQuality=70};importer.SetPlatformTextureSettings(desktop);
        foreach(string platform in new[]{"Android","iPhone"})importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name=platform,overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_6x6,compressionQuality=70});
        importer.SaveAndReimport();
    }
    public static object Run()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        Directory.CreateDirectory(Content+"/Ball");Directory.CreateDirectory("Assets/Resources/Loading");
        foreach(var path in Directory.GetFiles("../UnityWork/Loading20261019","ball-roll-*.png"))File.Copy(path,Content+"/Ball/"+Path.GetFileName(path),true);
        AssetDatabase.Refresh();
        var photos=Directory.GetFiles(Content+"/Artwork","*.jpg").OrderBy(p=>p).ToArray();var frames=Directory.GetFiles(Content+"/Ball","ball-roll-*.png").OrderBy(p=>p).ToArray();
        if(photos.Length!=3||frames.Length!=16)throw new Exception("Expected three photos and sixteen GLB ball frames");
        foreach(var p in photos)Import(p.Replace('\\','/'),false,2048);foreach(var p in frames)Import(p.Replace('\\','/'),true,128);
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Menu Vietnamese Bold.asset");if(!font)throw new Exception("Vietnamese font missing");
        var work=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(work);
        GameObject prefab;
        try
        {
            var root=new GameObject("LoadingScreen",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster),typeof(CanvasGroup));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var view=root.AddComponent<LoadingScreenView>();view.group=root.GetComponent<CanvasGroup>();
            view.pictures=photos.Select(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'))).ToArray();view.ballFrames=frames.Select(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'))).ToArray();
            view.pictureCaptions=new[]{"01  /  GIỜ VÀNG TRÊN SÂN","02  /  SẴN SÀNG ĐỐI ĐẦU","03  /  MỖI CÚ ĐÁNH LÀ MỘT CƠ HỘI"};
            var art=Rect("CinematicArtwork",root.transform,0,0,1,1);view.artwork=art.gameObject.AddComponent<UnityEngine.UI.RawImage>();view.artwork.texture=view.pictures[0];view.artwork.raycastTarget=true;
            var next=Rect("ArtworkCrossfade",root.transform,0,0,1,1);view.nextArtwork=next.gameObject.AddComponent<UnityEngine.UI.RawImage>();view.nextArtwork.raycastTarget=false;view.nextArtwork.color=new Color(1,1,1,0);
            var shade=Rect("BottomShade",root.transform,0,0,1,.53f).gameObject.AddComponent<LoadingShade>();shade.raycastTarget=false;
            var upper=Rect("TopShade",root.transform,0,.74f,1,1).gameObject.AddComponent<LoadingShade>();upper.bottom=Color.clear;upper.top=new Color(.018f,.035f,.07f,.64f);upper.raycastTarget=false;
            var safe=Rect("SafeArea",root.transform,0,0,1,1);safe.gameObject.AddComponent<UiSafeArea>();
            Text("Brand",safe,"PICKLEVERSE",26,.05f,.865f,.45f,.95f,ScreenUiTheme.Text);Image("BrandLine",safe,.05f,.858f,.108f,.861f,ScreenUiTheme.Gold);
            view.caption=Text("PhotoCaption",safe,view.pictureCaptions[0],13,.50f,.87f,.95f,.94f,ScreenUiTheme.Gold,TextAlignmentOptions.Right);
            var panel=Rect("LoadingConsole",safe,.04f,.035f,.96f,.235f);
            var surface=panel.gameObject.AddComponent<UnityEngine.UI.Image>();surface.raycastTarget=false;surface.color=Color.white;surface.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/FrontEndContent/AnimeUI/Dock.mat");if(!surface.material)surface.color=new Color(.05f,.08f,.14f,.92f);
            Image("HeaderAccent",panel,.025f,.84f,.067f,.855f,ScreenUiTheme.Gold);
            view.destination=Text("Destination",panel,"SÂN ĐẤU ĐANG CHỜ BẠN",18,.025f,.49f,.80f,.83f,ScreenUiTheme.Text);
            view.status=Text("Status",panel,"Chuẩn bị vào sân",13,.025f,.29f,.80f,.55f,ScreenUiTheme.Muted);
            view.percentage=Text("Percentage",panel,"0%",31,.81f,.38f,.97f,.87f,ScreenUiTheme.Gold,TextAlignmentOptions.Right);
            view.track=Rect("ProgressTrack",panel,.027f,.20f,.973f,.225f);var rail=view.track.gameObject.AddComponent<UnityEngine.UI.Image>();rail.color=new Color(.33f,.41f,.52f,.8f);rail.raycastTarget=false;
            var railUnder=Image("TrackShadow",view.track,0,-1,1,2,new Color(.03f,.05f,.10f,.7f));railUnder.transform.SetAsFirstSibling();
            view.fill=Rect("ProgressGold",view.track,0,0,0,1);var fill=view.fill.gameObject.AddComponent<UnityEngine.UI.Image>();fill.color=ScreenUiTheme.Gold;fill.raycastTarget=false;
            var sparkle=Image("LeadingGlow",view.fill,.96f,-.5f,1,1.5f,new Color(1,.94f,.78f,.85f));
            view.ball=Rect("RollingPickleball",view.track,0,.5f,0,.5f);view.ball.pivot=new Vector2(.5f,.5f);view.ball.sizeDelta=new Vector2(44,44);view.ballImage=view.ball.gameObject.AddComponent<UnityEngine.UI.RawImage>();view.ballImage.raycastTarget=false;view.ballImage.texture=view.ballFrames[0];
            view.tip=Text("CourtTip",panel,"Giữ nhịp di chuyển. Đón bóng. Tỏa sáng.",11,.025f,.015f,.97f,.18f,ScreenUiTheme.Muted,TextAlignmentOptions.Right);
            prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefab);UnityEngine.Object.DestroyImmediate(root);
        }
        finally{EditorSceneManager.CloseScene(work,true);}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);PrefabUtility.InstantiatePrefab(prefab,scene);EditorSceneManager.SaveScene(scene,ScenePath);
        Register();
        AssetDatabase.SaveAssetIfDirty(prefab);EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");
        return new{scene=ScenePath,prefab=Prefab,photos=photos.Length,ballFrames=frames.Length,sourceBytes=photos.Sum(p=>new FileInfo(p).Length)+frames.Sum(p=>new FileInfo(p).Length)};
    }
    public static object Register()
    {
        var list=EditorBuildSettings.scenes.ToList();if(list.All(s=>s.path!=ScenePath))list.Add(new EditorBuildSettingsScene(ScenePath,true));else list.First(s=>s.path==ScenePath).enabled=true;EditorBuildSettings.scenes=list.ToArray();
        // Unity 6 may use an overriding Build Profile. Register in both lists.
        var global=typeof(EditorBuildSettings).GetProperty("globalScenes",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        if(global!=null&&global.CanWrite){var shared=((EditorBuildSettingsScene[])global.GetValue(null)).ToList();if(shared.All(s=>s.path!=ScenePath))shared.Add(new EditorBuildSettingsScene(ScenePath,true));global.SetValue(null,shared.ToArray());}
        var active=UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();if(active){EditorUtility.SetDirty(active);AssetDatabase.SaveAssetIfDirty(active);}
        return "Loading registered globally and in active build profile";
    }
}
