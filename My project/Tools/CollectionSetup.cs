using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using PickleVerse.Shop;

public static class CollectionSetup
{
    const string ScenePath="Assets/Scenes/Collection.unity";
    static void Place(Transform t,Vector2 min,Vector2 max){var r=(RectTransform)t;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
    public static object Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))throw new InvalidOperationException("Collection already exists.");
        Directory.CreateDirectory("../UnityWork/Collection");
        var catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        if(!File.Exists("../UnityWork/Collection/Catalog.before.asset"))File.Copy(AssetDatabase.GetAssetPath(catalog),"../UnityWork/Collection/Catalog.before.asset");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Shop.unity");var shop=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Paddle/Paddle.glb");
        if(!source)throw new Exception("Default paddle source missing.");
        var root=new GameObject("Default Paddle Display");var model=UnityEngine.Object.Instantiate(source,root.transform);model.name="Paddle";
        foreach(var c in model.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c.gameObject);
        foreach(var l in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(l.gameObject);
        var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        float size=2/bounds.size.y;model.transform.localScale*=size;model.transform.localPosition=-bounds.center*size;
        var defaultPrefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/ShopContent/Prefabs/DefaultPaddle.prefab");UnityEngine.Object.DestroyImmediate(root);
        var defaultItem=new PaddleOffer{id=ShopCatalog.DefaultPaddleId,title="PICKLEVERSE",collection="ORIGINAL COLLECTION",description="Vợt mặc định của PickleVerse. Thiết kế gọn gàng, luôn sẵn sàng cho những trận đấu đầu tiên.",previewPrefab=defaultPrefab};
        defaultItem.thumbnail=Thumbnail(shop,defaultPrefab);
        catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        var items=new[]{defaultItem}.Concat(catalog.paddles).ToArray();
        var gameplay=items.Select(Gameplay).ToArray();
        catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");catalog.defaultPaddle=defaultItem;
        for(int i=0;i<gameplay.Length;i++)catalog.CollectionItems[i].gameplayPrefab=gameplay[i];
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        // Save a native copy, then edit only the new scene. The Shop keeps its two tabs and prices.
        EditorSceneManager.SaveScene(scene,ScenePath,true);scene=EditorSceneManager.OpenScene(ScenePath);
        var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();ui.collectionMode=true;ui.name="Collection Canvas";
        ui.safeArea.Find("Brand").GetComponent<TMP_Text>().text="PICKLEVERSE  /  BỘ SƯU TẬP";
        var browser=ui.browser;browser.signatureTab.gameObject.SetActive(false);browser.signaturePage.SetActive(false);browser.allPage.SetActive(true);
        Place(browser.allTab.transform,new Vector2(.04f,.815f),new Vector2(.325f,.874f));browser.allTab.GetComponentInChildren<TMP_Text>().text="TẤT CẢ SKIN VỢT";
        browser.allLine.enabled=true;browser.allPage.transform.Find("All Heading").GetComponent<TMP_Text>().text="CHỌN PHONG CÁCH CỦA BẠN";
        browser.totalCount.text="3 SKIN VỢT • SỞ HỮU ĐỂ SỬ DỤNG";
        browser.detailRoot.SetActive(false);browser.browseRoot.SetActive(true);browser.detailBreadcrumb.text="‹  QUAY LẠI BỘ SƯU TẬP";
        ui.tokenpadBuy.gameObject.SetActive(false);ui.elitePrice.text="SỬ DỤNG";
        foreach(var cached in ui.preview.models)if(cached)UnityEngine.Object.DestroyImmediate(cached);
        ui.preview.models=Array.Empty<GameObject>();ui.preview.modelIds=Array.Empty<string>();
        ui.confirmation.SetActive(false);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        WireMenu();RegisterScene();EditorSceneManager.OpenScene(ScenePath);
        AssetDatabase.SaveAssets();
        catalog=AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        return new{scene=ScenePath,skins=catalog.CollectionItems.Select(p=>p.id).ToArray(),defaultSource="Assets/Models/Paddle/Paddle.glb"};
    }
    static GameObject Gameplay(PaddleOffer offer)
    {
        var root=new GameObject(offer.title+" Gameplay");var model=(GameObject)PrefabUtility.InstantiatePrefab(offer.previewPrefab,root.transform);
        // MatchActor's original mesh spans z=-1.33..1.09, grip at positive z, face center z=-.60.
        // All display prefabs are centered and 2 units tall. Map +Y to -Z with a uniform scale.
        model.transform.localPosition=new Vector3(0,0,-.12f);model.transform.localRotation=Quaternion.Euler(-90,0,0);model.transform.localScale=Vector3.one*1.21f;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/ShopContent/Prefabs/"+offer.title+"Gameplay.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
    }
    static Sprite Thumbnail(ShopScreen ui,GameObject prefab)
    {
        var preview=ui.preview;var camera=preview.previewCamera;foreach(var cached in preview.models)if(cached)cached.SetActive(false);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview.pivot);foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        preview.pivot.localRotation=Quaternion.Euler(-8,-18,0);camera.Render();var rt=camera.targetTexture;var previous=RenderTexture.active;
        var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=previous;
        string path="Assets/ShopContent/Thumbnails/paddle.default.png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(model);
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.maxTextureSize=512;importer.mipmapEnabled=false;importer.SaveAndReimport();
        // Restore the source Shop to its saved pose before copying it.
        for(int i=0;i<preview.models.Length;i++)if(preview.models[i])preview.models[i].SetActive(i==0);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void WireMenu()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/UI/Menu.unity");EditorSceneManager.SaveScene(scene,"../UnityWork/Collection/Menu.before.unity",true);
        var button=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Button>(true)).Single(b=>b.name=="BtnCollection");
        var launcher=button.gameObject.GetComponent<ShopLauncher>()??button.gameObject.AddComponent<ShopLauncher>();
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)UnityEventTools.RemovePersistentListener(button.onClick,i);
        UnityEventTools.AddPersistentListener(button.onClick,launcher.OpenCollection);button.interactable=true;
        button.GetComponentInChildren<TMP_Text>().text="BỘ SƯU TẬP";
        var colors=button.colors;colors.normalColor=Color.white;colors.disabledColor=new Color(.5f,.5f,.5f);button.colors=colors;
        var paddle=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Home Paddle").gameObject;
        var appearance=paddle.GetComponent<PaddleAppearance>()??paddle.AddComponent<PaddleAppearance>();
        var binding=paddle.GetComponent<EquippedPaddle>()??paddle.AddComponent<EquippedPaddle>();binding.appearance=appearance;
        paddle.transform.localScale=Vector3.one*.24f;
        // A small court-side fill keeps the displayed premium surface readable throughout the sky cycle.
        var light=new GameObject("Paddle Showcase Fill",typeof(Light)).GetComponent<Light>();light.transform.SetParent(paddle.transform.parent,false);
        light.transform.position=paddle.transform.position+new Vector3(-.3f,1.0f,-.35f);light.type=LightType.Point;light.range=1.8f;light.intensity=1.2f;light.color=new Color(1,.94f,.81f);light.shadows=LightShadows.None;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static void RegisterScene()
    {
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath))EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();
        foreach(var guid in AssetDatabase.FindAssets("t:BuildProfile"))
        {
            var profile=AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if(profile.overrideGlobalScenes&&!profile.scenes.Any(s=>s.path==ScenePath)){profile.scenes=profile.scenes.Concat(new[]{new EditorBuildSettingsScene(ScenePath,true)}).ToArray();EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);}
        }
    }
}
