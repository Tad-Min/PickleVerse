using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using PickleVerse.Shop;

public static class ShopSceneSetup
{
    const string ScenePath="Assets/Scenes/Shop.unity", Content="Assets/ShopContent";
    static TMP_FontAsset font;
    static readonly Color Ink=new Color(.018f,.032f,.055f),Panel=new Color(.045f,.077f,.105f),Muted=new Color(.56f,.66f,.70f),Gold=new Color(.85f,.72f,.43f),Mint=new Color(.43f,.86f,.77f),Lime=new Color(.78f,.96f,.46f);
    static void Folder(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
    public static object Build()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        Directory.CreateDirectory("../UnityWork/Shop/BeforeScenes");
        foreach(var path in new[]{ScenePath,"Assets/Scenes/UI/Menu.unity"}){var backup="../UnityWork/Shop/BeforeScenes/"+System.IO.Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);}
        Folder(Content+"/Prefabs");Folder(Content+"/Materials");Folder(Content+"/Thumbnails");Folder("Assets/Resources/Shop");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalMatchContent/Match Vietnamese Font.asset");
        var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        if(!catalog){catalog=ScriptableObject.CreateInstance<ShopCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Resources/Shop/Shop Catalog.asset");}
        catalog.tokenpadIcon=Coin("Assets/Models/coin/Tokenpad.png");catalog.eliteIcon=Coin("Assets/Models/coin/Elite Token.png");
        catalog.tokenpadSheen=Sheen("Tokenpad Sheen",Mint);catalog.eliteSheen=Sheen("Elite Sheen",Gold);
        var offers=new[]{
            new PaddleOffer{id="paddle.gemstone",title="GEMSTONE",collection="ELITE COLLECTION",description="Viền đá quý, sắc vàng và mặt carbon. Ánh lấp lánh tinh tế khi xoay dưới ánh sáng.",elitePrice=650,gemstone=true,previewPrefab=Paddle("paddle-skin-gemstone-model",true)},
            new PaddleOffer{id="paddle.thot",title="THOT",collection="SIGNATURE COLLECTION",description="Hoa văn nổi bật cho phong cách thi đấu riêng. Chọn Tokenpad hoặc Elite để sở hữu.",tokenpadPrice=900,elitePrice=250,previewPrefab=Paddle("paddle-skin-thot-model",false)}
        };
        catalog.paddles=offers;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.31f,.35f);RenderSettings.fog=false;RenderSettings.skybox=null;
        var camera=new GameObject("Shop UI Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Ink;camera.cullingMask=1<<5;
        var canvasObject=new GameObject("Shop Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));canvasObject.layer=5;
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        var scaler=canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        var ui=canvasObject.AddComponent<ShopScreen>();ui.safeArea=Rect("SafeArea",canvasObject.transform,Vector2.zero,Vector2.one);
        Button("Back",ui.safeArea,"‹  ĐẠI SẢNH",new Vector2(.04f,.915f),new Vector2(.18f,.975f),Panel,Color.white,ui.Back);
        Text("Brand",ui.safeArea,"PICKLEVERSE  /  SHOP",new Vector2(.205f,.92f),new Vector2(.58f,.97f),23,Color.white,true);
        Wallet(ui.safeArea,catalog,new Vector2(.605f,.908f),new Vector2(.96f,.986f));
        Block("HeaderRule",ui.safeArea,new Vector2(.04f,.885f),new Vector2(.96f,.888f),new Color(.20f,.33f,.37f));
        Text("Eyebrow",ui.safeArea,"BỘ SƯU TẬP VỢT",new Vector2(.04f,.824f),new Vector2(.58f,.875f),24,Color.white,true);
        Text("CatalogHeading",ui.safeArea,"CHỌN PHONG CÁCH CỦA BẠN",new Vector2(.635f,.814f),new Vector2(.965f,.86f),16,Muted,true);
        var previewPanel=Block("PreviewPanel",ui.safeArea,new Vector2(.04f,.268f),new Vector2(.595f,.802f),Panel);
        var accent=Block("PreviewAccent",previewPanel,new Vector2(.03f,.02f),new Vector2(.97f,.025f),Gold);
        Text("PreviewMark",previewPanel,"360°  /  STUDIO",new Vector2(.035f,.91f),new Vector2(.65f,.99f),14,Muted);
        var imageRect=Rect("PaddleView",previewPanel,new Vector2(.03f,.04f),new Vector2(.97f,.91f));
        var raw=imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();raw.raycastTarget=true;
        var fitter=imageRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();fitter.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=1;
        var preview=imageRect.gameObject.AddComponent<PaddlePreview>();ui.preview=preview;
        CreateStudio(preview,offers,raw);
        catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");catalog.paddles=offers;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        Button("AutoRotate",previewPanel,"XOAY / DỪNG",new Vector2(.67f,.91f),new Vector2(.97f,.99f),Panel,Mint,preview.ToggleRotation);
        Text("Gestures",ui.safeArea,"Kéo chuột / chạm để xoay 360°  •  Cuộn để phóng to",new Vector2(.04f,.228f),new Vector2(.59f,.262f),14,Muted);
        ui.title=Text("ProductTitle",ui.safeArea,"GEMSTONE",new Vector2(.04f,.164f),new Vector2(.37f,.23f),36,Color.white,true);
        ui.collection=Text("Collection",ui.safeArea,"ELITE COLLECTION",new Vector2(.37f,.18f),new Vector2(.59f,.22f),13,Gold,true);
        ui.description=Text("Description",ui.safeArea,"",new Vector2(.04f,.076f),new Vector2(.59f,.16f),17,Muted);
        ui.cardBorders=new UnityEngine.UI.Image[2];ui.cardOwnership=new TMP_Text[2];ui.cardPrices=new TMP_Text[2];
        Card(ui,catalog,0,new Vector2(.635f,.48f),new Vector2(.96f,.79f));Card(ui,catalog,1,new Vector2(.635f,.165f),new Vector2(.96f,.465f));
        ui.ownership=Text("Ownership",ui.safeArea,"SKIN VỢT • DIỆN MẠO RIÊNG CỦA BẠN",new Vector2(.635f,.124f),new Vector2(.96f,.156f),12,Muted,true);
        var buy=Rect("PurchaseActions",ui.safeArea,new Vector2(.635f,.055f),new Vector2(.96f,.12f));
        ui.tokenpadBuy=Button("BuyTokenpad",buy,"",Vector2.zero,new Vector2(.48f,1),new Color(.10f,.29f,.27f),Color.white,ui.BuyTokenpad);ui.tokenpadPrice=ui.tokenpadBuy.GetComponentInChildren<TMP_Text>();
        ui.eliteBuy=Button("BuyElite",buy,"",new Vector2(.52f,0),Vector2.one,Gold,Ink,ui.BuyElite);ui.elitePrice=ui.eliteBuy.GetComponentInChildren<TMP_Text>();
        ui.status=Text("Status",ui.safeArea,"Tokenpad nhận từ trận đấu • Skin chỉ thay đổi diện mạo",new Vector2(.04f,.01f),new Vector2(.96f,.045f),14,Muted);
        Confirmation(ui);
        var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        EditorSceneManager.SaveScene(scene,ScenePath);
        catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        WireMenu(catalog);
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if(profile.overrideGlobalScenes&&!profile.scenes.Any(s=>s.path==ScenePath)){profile.scenes=profile.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        }
        EditorSceneManager.OpenScene(ScenePath);return new{scene=ScenePath,items=catalog.paddles.Length};
    }
    static Sprite Coin(string path)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Material Sheen(string name,Color tint)
    {var material=new Material(Shader.Find("PickleVerse/UI/Coin Sheen")){name=name};material.SetColor("_SweepColor",new Color(1,.96f,.80f,.40f));material.SetFloat("_Period",1);return SaveMaterial(material,name+".mat");}
    static Material SaveMaterial(Material value,string name)
    {string path=Content+"/Materials/"+name;var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    static GameObject Paddle(string name,bool gemstone)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Paddle/"+name+".glb");if(!source)throw new FileNotFoundException(name);
        var root=new GameObject(gemstone?"Gemstone Display":"THOT Display");var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Paddle";
        foreach(var c in model.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(var l in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l.gameObject);
        var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float scale=2/bounds.size.y;model.transform.localScale*=scale;model.transform.localPosition=-bounds.center*scale;
        if(gemstone)foreach(var renderer in renderers)
        {
            var original=renderer.sharedMaterial;var material=new Material(Shader.Find("PickleVerse/Paddle/Gemstone")){name="Gemstone Paddle"};
            material.SetTexture("_BaseMap",original.GetTexture("baseColorTexture"));material.SetTexture("_MetallicRoughnessMap",original.GetTexture("metallicRoughnessTexture"));material.SetFloat("_Glitter",.24f);renderer.sharedMaterial=SaveMaterial(material,"Gemstone Paddle.mat");
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Content+"/Prefabs/"+(gemstone?"Gemstone":"THOT")+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static void CreateStudio(PaddlePreview preview,PaddleOffer[] offers,UnityEngine.UI.RawImage image)
    {
        var stage=new GameObject("Preview Studio");stage.transform.position=new Vector3(1000,0,0);
        var pivot=new GameObject("Paddle Pivot").transform;pivot.SetParent(stage.transform,false);preview.pivot=pivot;preview.models=new GameObject[2];
        for(int i=0;i<2;i++){var go=(GameObject)PrefabUtility.InstantiatePrefab(offers[i].previewPrefab,pivot);Layer(go,30);preview.models[i]=go;}
        Light(stage.transform,"Key",new Vector3(-2,3,-3),new Color(1,.89f,.73f),4,LightType.Directional);
        Light(stage.transform,"Fill",new Vector3(2,1,-2),new Color(.66f,.83f,1),4,LightType.Point);
        Light(stage.transform,"Rim",new Vector3(0,2,2),new Color(.60f,1,.84f),7,LightType.Point);
        var camera=new GameObject("Paddle Preview Camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(stage.transform,false);camera.transform.localPosition=new Vector3(0,0,-4.5f);camera.transform.LookAt(pivot);camera.fieldOfView=32;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.077f,.105f,0);camera.cullingMask=1<<30;camera.nearClipPlane=.1f;camera.farClipPlane=20;
        var rt=AssetDatabase.LoadAssetAtPath<RenderTexture>(Content+"/PaddlePreview.renderTexture");if(!rt){rt=new RenderTexture(1024,1024,24){name="Paddle Preview",antiAliasing=1};AssetDatabase.CreateAsset(rt,Content+"/PaddlePreview.renderTexture");}
        preview.previewCamera=camera;camera.targetTexture=rt;image.texture=rt;
        for(int i=0;i<2;i++)
        {
            preview.models[0].SetActive(i==0);preview.models[1].SetActive(i==1);pivot.localRotation=Quaternion.Euler(-8,-18,0);camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1024,1024,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,1024,1024),0,0);texture.Apply();RenderTexture.active=previous;
            string path=Content+"/Thumbnails/"+offers[i].id+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.maxTextureSize=512;importer.mipmapEnabled=false;importer.SaveAndReimport();offers[i].thumbnail=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        preview.models[0].SetActive(true);preview.models[1].SetActive(false);
    }
    static void Light(Transform parent,string name,Vector3 position,Color color,float power,LightType type)
    {var light=new GameObject(name,typeof(Light)).GetComponent<Light>();light.transform.SetParent(parent,false);light.transform.localPosition=position;light.transform.LookAt(parent);light.type=type;light.intensity=power;light.color=color;light.range=12;light.cullingMask=1<<30;light.shadows=LightShadows.None;}
    static void Layer(GameObject root,int layer){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;}
    static void Card(ShopScreen ui,ShopCatalog catalog,int index,Vector2 min,Vector2 max)
    {
        var offer=catalog.paddles[index];var border=Block("Card"+index,ui.safeArea,min,max,index==0?Gold:new Color(.14f,.22f,.28f),true);ui.cardBorders[index]=border.GetComponent<UnityEngine.UI.Image>();
        var body=Block("Body",border,Vector2.zero,Vector2.one,Panel);body.offsetMin=Vector2.one*3;body.offsetMax=-Vector2.one*3;
        var button=border.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=ui.cardBorders[index];UnityEventTools.AddPersistentListener(button.onClick,index==0?(UnityAction)ui.SelectGemstone:ui.SelectThot);
        var picture=Rect("Thumbnail",body,new Vector2(.02f,.14f),new Vector2(.40f,.96f)).gameObject.AddComponent<UnityEngine.UI.Image>();picture.sprite=offer.thumbnail;picture.preserveAspect=true;picture.raycastTarget=false;
        Text("Collection",body,offer.collection,new Vector2(.42f,.75f),new Vector2(.96f,.91f),10,offer.gemstone?Gold:Mint,true);
        var title=Text("Name",body,offer.title,new Vector2(.42f,.52f),new Vector2(.80f,.75f),25,Color.white,true);title.enableAutoSizing=true;title.fontSizeMax=25;title.fontSizeMin=17;
        ui.cardPrices[index]=Text("Price",body,(offer.tokenpadPrice>0?offer.tokenpadPrice.ToString("N0")+" TOKENPAD  /  ":"")+offer.elitePrice.ToString("N0")+" ELITE",new Vector2(.42f,.34f),new Vector2(.97f,.51f),offer.gemstone?17:13,offer.gemstone?Gold:Mint,true);
        Text("Detail",body,offer.gemstone?"Vợt đính đá quý":"Hai cách để sở hữu",new Vector2(.42f,.21f),new Vector2(.97f,.35f),13,Muted);
        ui.cardOwnership[index]=Text("Action",body,"XEM 3D  →",new Vector2(.42f,.06f),new Vector2(.97f,.20f),14,Color.white,true);
        var icon=Rect("Currency",body,new Vector2(.81f,.49f),new Vector2(.94f,.71f)).gameObject.AddComponent<UnityEngine.UI.Image>();icon.sprite=offer.gemstone?catalog.eliteIcon:catalog.tokenpadIcon;icon.preserveAspect=true;icon.raycastTarget=false;
    }
    static void Confirmation(ShopScreen ui)
    {
        var overlay=Block("Purchase Confirmation",ui.safeArea,Vector2.zero,Vector2.one,new Color(0,0,0,.78f),true);ui.confirmation=overlay.gameObject;
        var panel=Block("Panel",overlay,new Vector2(.25f,.23f),new Vector2(.75f,.76f),Panel,true);
        Text("Eyebrow",panel,"XÁC NHẬN GIAO DỊCH",new Vector2(.07f,.86f),new Vector2(.94f,.95f),14,Gold,true);
        ui.confirmTitle=Text("Title",panel,"",new Vector2(.07f,.68f),new Vector2(.94f,.85f),30,Color.white,true);
        ui.confirmIcon=Rect("Coin",panel,new Vector2(.075f,.42f),new Vector2(.23f,.65f)).gameObject.AddComponent<UnityEngine.UI.Image>();ui.confirmIcon.preserveAspect=true;ui.confirmIcon.raycastTarget=false;
        ui.confirmBody=Text("Body",panel,"",new Vector2(.27f,.28f),new Vector2(.94f,.67f),20,Color.white);
        ui.cancelBuy=Button("Cancel",panel,"QUAY LẠI",new Vector2(.07f,.09f),new Vector2(.43f,.23f),new Color(.09f,.16f,.20f),Color.white,ui.CancelPurchase);
        ui.confirmBuy=Button("Confirm",panel,"XÁC NHẬN MUA",new Vector2(.48f,.09f),new Vector2(.94f,.23f),Lime,Ink,ui.ConfirmPurchase);overlay.gameObject.SetActive(false);
        ui.cancelBuy.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnRight=ui.confirmBuy};ui.confirmBuy.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnLeft=ui.cancelBuy};
    }
    static void Wallet(Transform parent,ShopCatalog catalog,Vector2 min,Vector2 max)
    {
        var root=Rect("Wallet",parent,min,max);var bar=root.gameObject.AddComponent<WalletBar>();
        for(int i=0;i<2;i++)
        {
            var panel=Block(i==0?"Tokenpad":"Elite",root,new Vector2(i*.52f,0),new Vector2(i*.52f+.48f,1),Panel);
            Text("CurrencyName",panel,i==0?"TOKENPAD":"ELITE TOKEN",new Vector2(.07f,.57f),new Vector2(.76f,.95f),11,i==0?Mint:Gold,true);
            var balance=Text("Balance",panel,"1,000",new Vector2(.07f,.06f),new Vector2(.74f,.59f),23,Color.white,true);if(i==0)bar.tokenpad=balance;else bar.elite=balance;
            var icon=Rect("CoinIcon",panel,new Vector2(.77f,.10f),new Vector2(.97f,.90f)).gameObject.AddComponent<UnityEngine.UI.Image>();icon.sprite=i==0?catalog.tokenpadIcon:catalog.eliteIcon;icon.material=i==0?catalog.tokenpadSheen:catalog.eliteSheen;icon.preserveAspect=true;icon.raycastTarget=false;
        }
    }
    static void WireMenu(ShopCatalog catalog)
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");var lobby=UnityEngine.Object.FindAnyObjectByType<PickleVerse.FrontEnd.HomeLobbyPresentation>();
        var button=lobby.safeArea.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="BtnStore");button.interactable=true;
        button.GetComponentInChildren<TMP_Text>().color=Color.white;button.GetComponent<UnityEngine.UI.Image>().color=new Color(.08f,.20f,.24f);
        var launch=button.GetComponent<ShopLauncher>();if(!launch)launch=button.gameObject.AddComponent<ShopLauncher>();
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);UnityEventTools.AddPersistentListener(button.onClick,launch.OpenShop);
        var existing=lobby.safeArea.Find("Wallet");if(!existing)Wallet(lobby.safeArea,catalog,new Vector2(.625f,.888f),new Vector2(.962f,.981f));
        if(lobby.timeLabel){var clock=lobby.timeLabel.rectTransform;clock.anchorMin=new Vector2(.83f,.21f);clock.anchorMax=new Vector2(.96f,.245f);clock.offsetMin=clock.offsetMax=Vector2.zero;lobby.timeLabel.fontSize=12;lobby.timeLabel.color=Mint;}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max,Color background,Color foreground,UnityAction action)
    {var rect=Block(name,parent,min,max,background,true);var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();Text("Label",rect,value,new Vector2(.035f,0),new Vector2(.965f,1),16,foreground,true,TextAlignmentOptions.Center);if(action!=null)UnityEventTools.AddPersistentListener(button.onClick,action);return button;}
    static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,float size,Color color,bool bold=false,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {var rect=Rect(name,parent,min,max);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=color;text.alignment=alignment;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;if(bold)text.fontStyle=FontStyles.Bold;return text;}
    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool hit=false)
    {var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=hit;return rect;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.gameObject.layer=5;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;}
}
