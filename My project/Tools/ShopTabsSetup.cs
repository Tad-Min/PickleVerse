using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PickleVerse.Shop;

public static class ShopTabsSetup
{
    static TMP_FontAsset font;
    static readonly Color Panel=new Color(.045f,.077f,.105f),Muted=new Color(.56f,.66f,.70f),Gold=new Color(.85f,.72f,.43f),Mint=new Color(.43f,.86f,.77f);
    public static object Upgrade()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Shop.unity");
        Directory.CreateDirectory("../UnityWork/ShopTabs");EditorSceneManager.SaveScene(scene,"../UnityWork/ShopTabs/Shop.before.unity",true);
        var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();if(ui.browser)throw new InvalidOperationException("Tabs already exist; edit their hierarchy.");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LocalMatchContent/Match Vietnamese Font.asset");
        var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        if(catalog.paddles.All(p=>p.releaseOrder==0))for(int i=0;i<catalog.paddles.Length;i++)catalog.paddles[i].releaseOrder=catalog.paddles.Length-i;
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        var browser=ui.safeArea.gameObject.AddComponent<ShopBrowser>();browser.screen=ui;ui.browser=browser;
        ui.preview.modelIds=catalog.paddles.Select(p=>p.id).ToArray();
        var detail=Rect("Product Detail",ui.safeArea,Vector2.zero,Vector2.one);browser.detailRoot=detail.gameObject;
        string[] details={"PreviewPanel","Gestures","ProductTitle","Collection","Description","Ownership","PurchaseActions","Status"};
        foreach(string name in details){var t=ui.safeArea.Find(name);if(t)t.SetParent(detail,false);}
        Place(detail.Find("PreviewPanel"),new Vector2(.04f,.19f),new Vector2(.63f,.77f));
        Place(detail.Find("Gestures"),new Vector2(.04f,.145f),new Vector2(.63f,.183f));
        Place(ui.title.transform,new Vector2(.67f,.65f),new Vector2(.96f,.76f));ui.title.fontSize=38;ui.title.enableAutoSizing=true;ui.title.fontSizeMin=24;ui.title.fontSizeMax=38;
        Place(ui.collection.transform,new Vector2(.67f,.765f),new Vector2(.96f,.805f));
        Place(ui.description.transform,new Vector2(.67f,.44f),new Vector2(.96f,.63f));ui.description.fontSize=19;
        Place(ui.ownership.transform,new Vector2(.67f,.37f),new Vector2(.96f,.425f));
        Place(ui.tokenpadBuy.transform.parent,new Vector2(.67f,.27f),new Vector2(.96f,.355f));
        Place(ui.status.transform,new Vector2(.67f,.14f),new Vector2(.96f,.25f));
        browser.detailBack=Button("Detail Back",detail,"",new Vector2(.04f,.795f),new Vector2(.49f,.864f),Panel,Color.white,browser.CloseDetails);browser.detailBreadcrumb=browser.detailBack.GetComponentInChildren<TMP_Text>();
        var controls=Rect("Preview Controls",detail,new Vector2(.04f,.067f),new Vector2(.63f,.13f));
        Button("Zoom Out",controls,"−",Vector2.zero,new Vector2(.15f,1),Panel,Color.white,ui.preview.ZoomOut);
        Button("Zoom In",controls,"+",new Vector2(.17f,0),new Vector2(.32f,1),Panel,Color.white,ui.preview.ZoomIn);
        Button("Reset View",controls,"ĐẶT LẠI GÓC",new Vector2(.35f,0),new Vector2(.71f,1),Panel,Mint,ui.preview.ResetView);
        Text("Zoom Hint",controls,"ZOOM GẦN / XA",new Vector2(.74f,0),Vector2.one,13,Muted);
        var legacy=Rect("Legacy Card Templates",ui.safeArea,Vector2.zero,Vector2.one);
        foreach(string name in new[]{"Card0","Card1","Eyebrow","CatalogHeading"}){var t=ui.safeArea.Find(name);if(t)t.SetParent(legacy,false);}legacy.gameObject.SetActive(false);
        var browse=Rect("Shop Browse",ui.safeArea,Vector2.zero,Vector2.one);browser.browseRoot=browse.gameObject;
        browser.signatureTab=Button("Signature Tab",browse,"SIGNATURE / THỊNH HÀNH",new Vector2(.04f,.815f),new Vector2(.325f,.874f),Panel,Color.white,browser.ShowSignature);
        browser.allTab=Button("All Tab",browse,"TẤT CẢ SẢN PHẨM",new Vector2(.345f,.815f),new Vector2(.62f,.874f),Panel,Color.white,browser.ShowAll);
        browser.signatureLine=Block("Selected",browser.signatureTab.transform,new Vector2(0,0),new Vector2(1,.06f),Gold).GetComponent<Image>();
        browser.allLine=Block("Selected",browser.allTab.transform,new Vector2(0,0),new Vector2(1,.06f),Mint).GetComponent<Image>();
        var signature=Rect("Signature Page",browse,new Vector2(.04f,.05f),new Vector2(.96f,.79f));browser.signaturePage=signature.gameObject;
        var hero=Block("Newest Product",signature,new Vector2(0,.405f),Vector2.one,new Color(.055f,.085f,.115f),true);browser.heroButton=hero.gameObject.AddComponent<Button>();browser.heroButton.targetGraphic=hero.GetComponent<Image>();
        Block("Gold Edge",hero,Vector2.zero,new Vector2(.006f,1),Gold);
        Text("New Badge",hero,"MỚI NHẤT",new Vector2(.045f,.835f),new Vector2(.45f,.965f),14,Gold,true);
        browser.heroCollection=Text("Hero Collection",hero,"",new Vector2(.045f,.70f),new Vector2(.48f,.825f),16,Muted);
        browser.heroTitle=Text("Hero Title",hero,"GEMSTONE",new Vector2(.045f,.48f),new Vector2(.51f,.705f),42,Color.white,true);
        browser.heroDescription=Text("Hero Description",hero,"",new Vector2(.045f,.25f),new Vector2(.47f,.49f),18,Muted);
        browser.heroPrice=Text("Hero Price",hero,"650 Elite",new Vector2(.045f,.085f),new Vector2(.47f,.245f),19,Gold,true);
        browser.heroImage=Rect("Hero Image",hero,new Vector2(.52f,.025f),new Vector2(.96f,.98f)).gameObject.AddComponent<Image>();browser.heroImage.preserveAspect=true;browser.heroImage.raycastTarget=false;
        Text("Hero Action",hero,"XEM VẬT PHẨM  →",new Vector2(.69f,.02f),new Vector2(.98f,.13f),13,Color.white,true);
        Text("Recent Heading",signature,"RA MẮT GẦN ĐÂY",new Vector2(0,.34f),new Vector2(1,.405f),15,Muted,true);
        browser.signatureContent=Rect("Recent Products",signature,Vector2.zero,new Vector2(1,.33f));browser.signatureGrid=browser.signatureContent.gameObject.AddComponent<GridLayoutGroup>();browser.signatureGrid.spacing=new Vector2(18,0);browser.signatureGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;browser.signatureGrid.constraintCount=3;
        var all=Rect("All Products Page",browse,new Vector2(.04f,.065f),new Vector2(.96f,.79f));browser.allPage=all.gameObject;
        Text("All Heading",all,"KHÁM PHÁ TOÀN BỘ BỘ SƯU TẬP",new Vector2(0,.92f),new Vector2(.61f,1),24,Color.white,true);
        browser.search=Search(all,new Vector2(.66f,.91f),Vector2.one);
        browser.totalCount=Text("Count",all,"2 SẢN PHẨM",new Vector2(0,.855f),new Vector2(.80f,.91f),14,Muted);
        var scroll=Rect("Product Scroll",all,Vector2.zero,new Vector2(1,.84f));browser.allScroll=scroll.gameObject.AddComponent<ScrollRect>();browser.allScroll.horizontal=false;browser.allScroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Block("Viewport",scroll,Vector2.zero,new Vector2(.98f,1),Color.white,true);viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;browser.allScroll.viewport=viewport;
        browser.allContent=Rect("Content",viewport,new Vector2(0,1),Vector2.one);browser.allContent.pivot=new Vector2(.5f,1);browser.allContent.sizeDelta=Vector2.zero;
        browser.allGrid=browser.allContent.gameObject.AddComponent<GridLayoutGroup>();browser.allGrid.spacing=new Vector2(18,18);browser.allGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;browser.allGrid.constraintCount=4;browser.allGrid.padding=new RectOffset(0,0,0,8);
        browser.allContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;browser.allScroll.content=browser.allContent;
        var bar=Block("Scrollbar",scroll,new Vector2(.989f,0),Vector2.one,Panel,true);var handle=Block("Handle",bar,Vector2.zero,Vector2.one,Mint,true);var sb=bar.gameObject.AddComponent<Scrollbar>();sb.handleRect=handle;sb.targetGraphic=handle.GetComponent<Image>();sb.direction=Scrollbar.Direction.BottomToTop;browser.allScroll.verticalScrollbar=sb;browser.allScroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        browser.emptyLabel=Text("Empty",all,"Không tìm thấy vật phẩm phù hợp.",new Vector2(.05f,.44f),new Vector2(.95f,.6f),22,Muted);browser.emptyLabel.gameObject.SetActive(false);
        browser.cardPrefab=CardPrefab();
        var first=catalog.paddles[catalog.NewestFirst()[0]];browser.heroTitle.text=first.title;browser.heroCollection.text=first.collection;browser.heroDescription.text=first.description;browser.heroPrice.text=ShopProductCard.Price(first);browser.heroImage.sprite=first.thumbnail;
        detail.gameObject.SetActive(false);all.gameObject.SetActive(false);browser.allLine.enabled=false;ui.preview.previewCamera.enabled=false;ui.confirmation.transform.SetAsLastSibling();
        EditorUtility.SetDirty(ui);EditorUtility.SetDirty(browser);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,tabs=2,detailReused=true};
    }
    static ShopProductCard CardPrefab()
    {
        var root=Block("Product Tile",null,Vector2.zero,Vector2.one,Panel,true);root.sizeDelta=new Vector2(270,250);var card=root.gameObject.AddComponent<ShopProductCard>();card.button=root.gameObject.AddComponent<Button>();card.button.targetGraphic=root.GetComponent<Image>();
        card.accent=Block("Accent",root,Vector2.zero,new Vector2(1,.015f),Gold).GetComponent<Image>();
        card.collection=Text("Collection",root,"COLLECTION",new Vector2(.055f,.9f),new Vector2(.96f,.98f),12,Muted,true);
        card.thumbnail=Rect("Thumbnail",root,new Vector2(.10f,.42f),new Vector2(.90f,.91f)).gameObject.AddComponent<Image>();card.thumbnail.preserveAspect=true;card.thumbnail.raycastTarget=false;
        card.title=Text("Title",root,"ITEM",new Vector2(.055f,.285f),new Vector2(.96f,.43f),23,Color.white,true);card.title.enableAutoSizing=true;card.title.fontSizeMin=16;card.title.fontSizeMax=23;
        card.price=Text("Price",root,"",new Vector2(.055f,.14f),new Vector2(.96f,.29f),14,Gold);card.price.enableAutoSizing=true;card.price.fontSizeMin=11;card.price.fontSizeMax=14;
        card.action=Text("Action",root,"XEM CHI TIẾT  →",new Vector2(.055f,.025f),new Vector2(.96f,.145f),12,Mint,true);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/ShopContent/Prefabs/ProductTile.prefab");UnityEngine.Object.DestroyImmediate(root.gameObject);return prefab.GetComponent<ShopProductCard>();
    }
    static TMP_InputField Search(Transform parent,Vector2 min,Vector2 max)
    {
        var root=Block("Search",parent,min,max,Panel,true);var input=root.gameObject.AddComponent<TMP_InputField>();var viewport=Rect("TextArea",root,new Vector2(.04f,.08f),new Vector2(.96f,.92f));viewport.gameObject.AddComponent<RectMask2D>();
        var text=Text("Text",viewport,"",Vector2.zero,Vector2.one,17,Color.white);var hint=Text("Placeholder",viewport,"Tìm tên vật phẩm…",Vector2.zero,Vector2.one,16,Muted);
        input.textViewport=viewport;input.textComponent=text;input.placeholder=hint;input.fontAsset=font;input.lineType=TMP_InputField.LineType.SingleLine;input.characterLimit=64;return input;
    }
    static Button Button(string name,Transform parent,string label,Vector2 min,Vector2 max,Color color,Color foreground,UnityAction action)
    {var root=Block(name,parent,min,max,color,true);var button=root.gameObject.AddComponent<Button>();button.targetGraphic=root.GetComponent<Image>();Text("Label",root,label,new Vector2(.035f,0),new Vector2(.965f,1),17,foreground,true,TextAlignmentOptions.Center);UnityEventTools.AddPersistentListener(button.onClick,action);return button;}
    static TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,float size,Color color,bool bold=false,TextAlignmentOptions align=TextAlignmentOptions.Left)
    {var r=Rect(name,parent,min,max);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;if(bold)t.fontStyle=FontStyles.Bold;return t;}
    static RectTransform Block(string name,Transform parent,Vector2 min,Vector2 max,Color color,bool raycast=false){var r=Rect(name,parent,min,max);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return r;}
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.gameObject.layer=5;if(parent)r.SetParent(parent,false);r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
    static void Place(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
}
