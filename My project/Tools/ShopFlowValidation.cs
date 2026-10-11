using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Shop;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class ShopFlowValidation
{
    sealed class Memory : IEconomyStorage {public string data=JsonUtility.ToJson(new PlayerEconomyState{tokenpad=1000,elite=1000});public string Read()=>data;public void Write(string json){data=json;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Wait(Func<bool> ready){var until=DateTime.UtcNow.AddSeconds(10);while(!ready()){if(DateTime.UtcNow>until)throw new TimeoutException();await Task.Delay(40);}await Task.Delay(100);}
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();var original=PlayerEconomy.Service;
        var storage=new Memory();var mock=new LocalPlayerEconomy(PlayerEconomy.Catalog,storage);PlayerEconomy.Configure(mock);
        try
        {
            SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu");
            var wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Check("Menu wallet shows both initial balances",wallet&&wallet.tokenpad.text==1000.ToString("N0")&&wallet.elite.text==1000.ToString("N0"));
            var store=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include).First(b=>b.name=="BtnStore");store.onClick.Invoke();await Wait(()=>UnityEngine.Object.FindAnyObjectByType<ShopScreen>()!=null);
            var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();Check("Menu Store opens Shop",SceneManager.GetActiveScene().name=="Shop");
            Check("Both model meshes and thumbnails available",PlayerEconomy.Catalog.paddles.All(p=>p.previewPrefab&&p.thumbnail)&&ui.preview.models.All(m=>m.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh)));
            Check("Single listener and event system",UnityEngine.Object.FindObjectsByType<AudioListener>().Length==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1);
            Check("Coin sheen repeats once per second",Mathf.Abs(PlayerEconomy.Catalog.eliteSheen.GetFloat("_Period")-1)<.001f&&Mathf.Abs(PlayerEconomy.Catalog.tokenpadSheen.GetFloat("_Period")-1)<.001f);
            Check("Gemstone offers Elite only",!ui.tokenpadBuy.gameObject.activeSelf&&ui.eliteBuy.interactable);
            ui.SelectThot();Check("THOT offers either currency",ui.tokenpadBuy.gameObject.activeSelf&&ui.tokenpadBuy.interactable&&ui.eliteBuy.interactable);
            ui.preview.autoRotate=false;var rotation=ui.preview.pivot.localRotation;ui.preview.Rotate(360,0);Check("Full 360-degree rotation keeps orientation",Quaternion.Angle(rotation,ui.preview.pivot.localRotation)<.02f);
            float yaw=ui.preview.yaw;var pointer=new PointerEventData(EventSystem.current){delta=new Vector2(80,20)};ExecuteEvents.Execute(ui.preview.gameObject,pointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(ui.preview.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(ui.preview.gameObject,pointer,ExecuteEvents.endDragHandler);
            Check("Mouse/touch drag rotates model",ui.preview.yaw!=yaw&&!ui.preview.autoRotate);
            ui.BuyTokenpad();Check("Purchase needs confirmation and focuses cancel",ui.confirmation.activeSelf&&EventSystem.current.currentSelectedGameObject==ui.cancelBuy.gameObject);
            Check("Opening purchase does not spend",(await mock.ReadAsync()).tokenpad==1000);ui.CancelPurchase();Check("Cancel does not spend",(await mock.ReadAsync()).tokenpad==1000);
            ui.BuyTokenpad();ui.ConfirmPurchase();await Task.Delay(100);var state=await mock.ReadAsync();Check("Confirmed THOT Tokenpad purchase grants one item",state.tokenpad==100&&state.elite==1000&&state.owned.Contains("paddle.thot"));
            Check("Owned item cannot be bought again in UI",!ui.tokenpadBuy.interactable&&!ui.eliteBuy.interactable);
            ui.SelectGemstone();ui.BuyElite();ui.ConfirmPurchase();await Task.Delay(100);state=await mock.ReadAsync();Check("Gemstone Elite purchase updates other balance",state.elite==350&&state.tokenpad==100&&state.owned.Count==2);
            var reloaded=new LocalPlayerEconomy(PlayerEconomy.Catalog,storage);Check("Local reload retains balances and ownership",(await reloaded.ReadAsync()).owned.Count==2&&(await reloaded.ReadAsync()).elite==350);
            if(ui.browser&&ui.browser.Inspecting)ui.Back();ui.Back();await Wait(()=>SceneManager.GetActiveScene().name=="Menu");wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Check("Menu reflects purchases",wallet.elite.text==350.ToString("N0")&&wallet.tokenpad.text==100.ToString("N0"));
            SceneManager.LoadScene("BotMatch");await Wait(()=>UnityEngine.Object.FindAnyObjectByType<BotMatch>()!=null);var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match.SetPaused(true);if(match.opening&&match.opening.IsPlaying)match.opening.Skip();match.Restart();match.SetPaused(true);
            var rules=new SinglesRules(0);rules.PrepareServe(-1);rules.Points[0]=10;rules.Games[0]=1;rules.Fail(1,Fault.Out);typeof(BotMatch).GetProperty("Rules").SetValue(match,rules);
            var finish=typeof(BotMatch).GetMethod("FinishRally",BindingFlags.Instance|BindingFlags.NonPublic);finish.Invoke(match,null);finish.Invoke(match,null);state=await mock.ReadAsync();Check("Bot full match grants Tokenpad exactly once",state.tokenpad==200&&state.elite==350);
            await mock.GrantMatchRewardAsync("online:test",true,true);await mock.GrantMatchRewardAsync("online:test",true,true);state=await mock.ReadAsync();Check("Online reward adapter grants only Tokenpad once",state.tokenpad==350&&state.elite==350);
            var result=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/Shop");File.WriteAllText("../UnityWork/Shop/flow-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally{PlayerEconomy.Configure(original);SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu");}
    }
}
