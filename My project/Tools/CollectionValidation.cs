using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using PickleVerse.Shop;
using PickleVerse.LocalMatch;
using UnityEditor;

public static class CollectionValidation
{
    sealed class Memory:IEconomyStorage { public string json;public string Read()=>json;public void Write(string value){json=value;} }
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Load(string scene){SceneManager.LoadScene(scene);await Task.Delay(300);Canvas.ForceUpdateCanvases();}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        var original=PlayerEconomy.Service;string realJson=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");
        var state=new PlayerEconomyState{tokenpad=1000,elite=350};state.owned.Add("paddle.gemstone");
        var memory=new Memory{json=JsonUtility.ToJson(state)};var demo=new LocalPlayerEconomy(PlayerEconomy.Catalog,memory);
        PlayerEconomy.Configure(demo);
        try
        {
            await Load("Collection");var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();var browser=ui.browser;
            Check("Collection includes default and both skins",ui.collectionMode&&ui.Items.Length==3&&ui.Items[0].id==ShopCatalog.DefaultPaddleId);
            Check("Collection opens grid",browser.Tab==1&&!browser.Inspecting&&browser.allPage.activeSelf&&!browser.signaturePage.activeSelf);
            var cards=browser.allContent.GetComponentsInChildren<ShopProductCard>();
            Check("Owned skin is bright and unowned dim",cards.Single(c=>c.Index==1).thumbnail.color.r>.9f&&cards.Single(c=>c.Index==2).thumbnail.color.r<.35f);
            Check("Default is equipped initially",cards.Single(c=>c.Index==0).action.text.Contains("ĐANG SỬ DỤNG"));
            ui.OpenProduct(2);Check("Unowned preview available but equip locked",browser.Inspecting&&!ui.eliteBuy.interactable&&ui.elitePrice.text=="SỬ DỤNG"&&ui.ownership.text=="CHƯA SỞ HỮU");
            ui.EquipSelected();Check("Locked skin does not change equipment",(await demo.ReadAsync()).equippedPaddle==ShopCatalog.DefaultPaddleId);
            ui.Back();ui.OpenProduct(1);ui.preview.autoRotate=false;
            Check("Owned detail keeps shop title and description",ui.title.text==PlayerEconomy.Catalog.paddles[0].title&&ui.description.text==PlayerEconomy.Catalog.paddles[0].description);
            Check("Fullscreen 3D presentation reused",browser.detailPresentation.Showing&&ui.preview.previewCamera.targetTexture==null&&((RectTransform)browser.detailPresentation.fullScreenInput.transform).anchorMax==Vector2.one);
            var rotation=ui.preview.pivot.localRotation;ui.preview.Rotate(360,0);Check("Full 360 rotation",Quaternion.Angle(rotation,ui.preview.pivot.localRotation)<.03f);
            float distance=ui.preview.previewCamera.transform.localPosition.magnitude;ui.preview.ZoomIn();Check("Zoom changes camera distance",ui.preview.previewCamera.transform.localPosition.magnitude<distance);
            Check("Equip button bottom right",((RectTransform)ui.eliteBuy.transform.parent).anchorMin.x>.74f&&((RectTransform)ui.eliteBuy.transform.parent).anchorMax.y<.15f);
            ui.eliteBuy.onClick.Invoke();await Task.Delay(100);
            Check("Button equips owned skin",(await demo.ReadAsync()).equippedPaddle=="paddle.gemstone"&&ui.elitePrice.text=="ĐANG SỬ DỤNG"&&!ui.eliteBuy.interactable);
            var reload=new LocalPlayerEconomy(PlayerEconomy.Catalog,memory);Check("Equipment survives storage reload",(await reload.ReadAsync()).equippedPaddle=="paddle.gemstone");
            ui.Back();Check("Grid reflects equipped skin",browser.allContent.GetComponentsInChildren<ShopProductCard>().Single(c=>c.Index==1).action.text.Contains("ĐANG SỬ DỤNG"));
            browser.search.text="thot";Check("Collection search",browser.VisibleProducts.SequenceEqual(new[]{2}));browser.search.text="";
            await demo.PurchaseAsync("paddle.thot",ShopCurrency.Elite,"fixture-thot");
            Check("New ownership immediately brightens card",browser.allContent.GetComponentsInChildren<ShopProductCard>().Single(c=>c.Index==2).thumbnail.color==Color.white);
            await demo.EquipPaddleAsync("paddle.gemstone");
            await Load("Menu");var home=UnityEngine.Object.FindObjectsByType<PaddleAppearance>().Single(a=>a.name=="Home Paddle");
            Check("Menu displays equipped premium model",home.CurrentId=="paddle.gemstone"&&home.SkinVisual&&home.GetComponent<Renderer>().enabled==false);
            Check("Menu dance remains empty-handed",UnityEngine.Object.FindAnyObjectByType<PickleVerse.FrontEnd.HomeLobbyPresentation>().femaleVisual.GetComponentsInChildren<MeshFilter>().All(m=>!m.name.Contains("Paddle")));
            var launch=UnityEngine.Object.FindObjectsByType<ShopLauncher>().Single(l=>l.name=="BtnCollection");launch.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();await Task.Delay(300);
            Check("Menu Collection button navigates",SceneManager.GetActiveScene().name=="Collection");
            await Load("BotMatch");var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();if(match.opening)match.opening.Skip();match.Restart();
            var appearance=match.human.paddle.GetComponent<PaddleAppearance>();var face=match.human.paddleFace;
            var pose=match.human.paddle.transform.localPosition;var angle=match.human.paddle.transform.localRotation;var scale=match.human.paddle.transform.localScale;
            Check("BotMatch human holds equipped skin",appearance.CurrentId=="paddle.gemstone"&&appearance.SkinVisual);
            Check("Bot paddle remains unchanged",!match.bot.paddle.GetComponent<PaddleAppearance>());
            foreach(string id in new[]{"paddle.thot",ShopCatalog.DefaultPaddleId,"paddle.gemstone"})
            {
                await demo.EquipPaddleAsync(id);await Task.Delay(50);
                Check("Swap "+id+" preserves socket and contact",appearance.CurrentId==id&&match.human.paddleFace==face&&match.human.paddle.transform.localPosition==pose&&match.human.paddle.transform.localRotation==angle&&match.human.paddle.transform.localScale==scale);
            }
            Check("Cosmetic has no added colliders or lights",appearance.SkinVisual.GetComponentsInChildren<Collider>().Length==0&&appearance.SkinVisual.GetComponentsInChildren<Light>().Length==0);
            // Test the male socket using the existing animated actor, without changing the bot's saved prefab.
            var male=PaddleAppearance.Attach(match.bot.paddle);male.Apply("paddle.thot");Check("Male socket accepts same gameplay skin",male.CurrentId=="paddle.thot"&&male.SkinVisual);male.Apply(ShopCatalog.DefaultPaddleId);
            match.autoPlayer=true;for(int i=0;i<1200;i++)match.StepSimulation(1f/120,Vector2.zero,false);
            Check("Rally simulation still runs with premium",match.TotalHits>0&&float.IsFinite(match.human.PaddleHeadTop));match.autoPlayer=false;
            await Load("Shop");ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();Check("Shop retains Signature and only sale items",!ui.collectionMode&&ui.browser.Tab==0&&ui.Items.Length==2);
            ui.OpenProduct(1);Check("Shop purchase UI unchanged",ui.title.text=="THOT"&&ui.elitePrice.text=="ĐÃ SỞ HỮU");
            Check("Real player wallet and selection untouched",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==realJson);
            var report=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/Collection");File.WriteAllText("../UnityWork/Collection/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{PlayerEconomy.Configure(original);await Load("Collection");}
    }
}
