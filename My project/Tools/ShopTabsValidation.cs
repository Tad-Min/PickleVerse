using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ShopTabsValidation
{
    sealed class Memory : IEconomyStorage{string data="";public string Read()=>data;public void Write(string json){data=json;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool ok){checks.Add(new{name,passed=ok});if(!ok)throw new Exception(name);}
    static async Task Wait(){await Task.Delay(180);Canvas.ForceUpdateCanvases();}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        var catalog=PlayerEconomy.Catalog;var originals=catalog.paddles;var service=PlayerEconomy.Service;var stored=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        try
        {
            PlayerEconomy.Configure(new LocalPlayerEconomy(catalog,new Memory()));SceneManager.LoadScene("Shop");await Wait();
            var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();var b=ui.browser;
            Check("Signature opens by default",b.Tab==0&&b.signaturePage.activeSelf&&!b.allPage.activeSelf&&!b.Inspecting);
            Check("Preview camera rests while browsing",!ui.preview.previewCamera.enabled);
            b.heroButton.onClick.Invoke();await Wait();Check("Featured item opens dedicated 3D details",b.Inspecting&&ui.Selected==catalog.NewestFirst()[0]&&ui.preview.previewCamera.enabled);
            ui.preview.autoRotate=false;var q=ui.preview.pivot.localRotation;ui.preview.Rotate(360,0);Check("360 rotation returns to same orientation",Quaternion.Angle(q,ui.preview.pivot.localRotation)<.02f);
            float zoom=ui.preview.zoom;ui.preview.ZoomIn();Check("Zoom closer changes camera distance",ui.preview.zoom<zoom);ui.preview.ZoomOut();Check("Zoom out restores distance",Mathf.Abs(ui.preview.zoom-zoom)<.001f);
            var pointer=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,2),delta=new Vector2(30,10)};ui.preview.OnScroll(pointer);Check("Wheel zoom works",ui.preview.zoom<zoom);float yaw=ui.preview.yaw;ui.preview.OnBeginDrag(pointer);ui.preview.OnDrag(pointer);ui.preview.OnEndDrag(pointer);Check("Pointer drag rotates item",ui.preview.yaw!=yaw);
            ui.Back();Check("Back from detail returns to Signature",b.Tab==0&&!b.Inspecting&&b.browseRoot.activeSelf);
            b.ShowAll();await Wait();Check("All tab renders every catalog product",b.VisibleProducts.Length==catalog.paddles.Length&&b.allContent.rect.height>0);
            Check("Viewport stencil can render child cards",b.allScroll.viewport.GetComponent<UnityEngine.UI.Image>().color.a>.99f&&!b.allScroll.viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic);
            b.search.text="thot";await Wait();Check("Search filters by product title",b.VisibleProducts.Length==1&&catalog.paddles[b.VisibleProducts[0]].title=="THOT");b.search.text="not-a-product";await Wait();Check("Search empty state is visible",b.VisibleProducts.Length==0&&b.emptyLabel.gameObject.activeSelf);b.search.text="";
            var many=Enumerable.Range(0,30).Select(i=>{var source=originals[i%2];return new PaddleOffer{id="qa."+i,title="QA SKIN "+i,description="QA only",collection="QA COLLECTION",elitePrice=30,tokenpadPrice=90,releaseOrder=i+10,previewPrefab=source.previewPrefab,thumbnail=source.thumbnail,gemstone=source.gemstone};}).ToArray();
            catalog.paddles=many;PlayerEconomy.Configure(new LocalPlayerEconomy(catalog,new Memory()));SceneManager.LoadScene("Shop");await Wait();ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();b=ui.browser;
            Check("Signature promotes newest item with 30 products",b.heroTitle.text=="QA SKIN 29"&&b.signatureContent.GetComponentsInChildren<ShopProductCard>().Length==3);
            b.ShowAll();await Wait();Check("Grid supports 30 products",b.VisibleProducts.Length==30&&b.allContent.GetComponentsInChildren<ShopProductCard>().Length==30);
            Check("Large catalog has scrollable content",b.allContent.rect.height>b.allScroll.viewport.rect.height);
            b.allScroll.verticalNormalizedPosition=.3f;Canvas.ForceUpdateCanvases();float position=b.allScroll.verticalNormalizedPosition;
            var tile=b.allContent.GetComponentsInChildren<ShopProductCard>().Single(c=>c.Index==17);tile.button.onClick.Invoke();await Wait();Check("Any catalog product opens correct 3D item",ui.Selected==17&&b.Inspecting&&ui.preview.models[17]&&ui.title.text=="QA SKIN 17");
            Check("Models are instantiated on demand",ui.preview.models.Count(m=>m)!=30);
            ui.Back();await Wait();Check("Returning restores All tab and scroll",b.Tab==1&&Mathf.Abs(b.allScroll.verticalNormalizedPosition-position)<.015f);
            b.ShowSignature();b.ShowAll();await Wait();Check("Tab switching does not duplicate product cards",b.allContent.GetComponentsInChildren<ShopProductCard>().Length==30);
            ui.OpenProduct(17);ui.BuyElite();Check("Existing purchase confirmation is retained",ui.confirmation.activeSelf);ui.CancelPurchase();var before=await PlayerEconomy.Service.ReadAsync();ui.BuyElite();ui.ConfirmPurchase();await Wait();var after=await PlayerEconomy.Service.ReadAsync();Check("Buying from details spends correct amount once",after.elite==before.elite-30&&after.owned.Contains("qa.17"));
            Check("Real demo wallet is untouched",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==stored);
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/ShopTabs");File.WriteAllText("../UnityWork/ShopTabs/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{catalog.paddles=originals;PlayerEconomy.Configure(service);SceneManager.LoadScene("Shop");await Wait();}
    }
}
