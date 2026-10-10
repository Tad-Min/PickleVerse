using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using PickleVerse.Shop;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;

public static class LobbyNavigationValidation
{
    sealed class Memory:IEconomyStorage{public string json;public string Read()=>json;public void Write(string value){json=value;}}
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Ready(string name)
    {
        float deadline=Time.realtimeSinceStartup+8;
        while(SceneManager.GetActiveScene().name!=name||UnityEngine.Object.FindAnyObjectByType<LobbyNavigation>()?.Busy==true)
        {if(Time.realtimeSinceStartup>deadline)throw new Exception("Navigation not ready: "+name);await Task.Delay(10);}
        await Task.Delay(30);
    }
    public static async Task<object> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play first.");checks.Clear();
        var original=PlayerEconomy.Service;var prefs=PlayerPrefs.GetString(LocalEconomyStorage.Key,"");var graphics=PlayerPrefs.GetString(GameSettings.PreferenceKey,"");
        var state=new PlayerEconomyState{tokenpad=1000,elite=350};state.owned.Add("paddle.gemstone");
        var demo=new LocalPlayerEconomy(PlayerEconomy.Catalog,new Memory{json=JsonUtility.ToJson(state)});PlayerEconomy.Configure(demo);
        try
        {
            SceneManager.LoadScene("Menu");await Task.Delay(300);var menu=SceneManager.GetActiveScene();var home=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
            home.animateAtmosphere=false;home.ApplyAtmosphere(1);var sky=RenderSettings.skybox;var originalLights=home.floodlights.Select(l=>l.enabled).ToArray();
            var destinations=new[]{"Shop","Collection","Collection","Collection"};
            for(int pass=0;pass<destinations.Length;pass++)
            {
                string destination=destinations[pass];
                float dance=home.DanceTime,cycle=home.CycleElapsed;
                var launcher=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ShopLauncher>(true)).First(l=>l.name==(destination=="Shop"?"BtnStore":"BtnCollection"));
                if(destination=="Shop"){launcher.OpenShop();launcher.OpenShop();}else{launcher.OpenCollection();launcher.OpenCollection();}
                await Ready(destination);var ui=UnityEngine.Object.FindAnyObjectByType<ShopScreen>();
                Check(destination+" keeps one loaded lobby",menu.IsValid()&&menu.isLoaded&&SceneManager.sceneCount==2);
                Check(destination+" pauses lobby simulation",!home.enabled&&Mathf.Abs(home.DanceTime-dance)<.05f&&home.CycleElapsed==cycle);
                Check(destination+" disables lobby rendering and lights",menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).All(r=>!r.enabled)&&menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).All(l=>!l.enabled));
                Check(destination+" has one active camera/input/listener",UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.enabled)==1);
                if(destination=="Shop")await demo.PurchaseAsync("paddle.thot",ShopCurrency.Elite,"navigation-fixture");
                int item=new[]{0,1,0,2}[pass];
                ui.OpenProduct(item);if(destination=="Collection")ui.eliteBuy.onClick.Invoke();
                ui.GoLobby();ui.GoLobby();await Ready("Menu");
                var current=UnityEngine.Object.FindAnyObjectByType<HomeLobbyPresentation>();
                Check(destination+" reuses exact lobby instance",current==home&&SceneManager.GetActiveScene()==menu&&SceneManager.sceneCount==1);
                Check(destination+" restores sky, floodlights and dance",RenderSettings.skybox==sky&&current.enabled&&current.floodlights.Select(l=>l.enabled).SequenceEqual(originalLights)&&current.DanceTime>=dance);
                Check(destination+" leaves no active preview camera or duplicate input",UnityEngine.Object.FindObjectsByType<Camera>().Count(c=>c.enabled)==1&&UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.enabled)==1&&UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.enabled)==1);
                var wallet=await demo.ReadAsync();var bar=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WalletBar>(true)).Single();
                Check(destination+" refreshes wallet on return",bar.tokenpad.text==wallet.tokenpad.ToString("N0")&&bar.elite.text==wallet.elite.ToString("N0"));
                var appearance=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PaddleAppearance>(true)).Single();
                Check(destination+" refreshes selected paddle and default renderer",appearance.CurrentId==wallet.equippedPaddle&&appearance.GetComponent<Renderer>().enabled==(wallet.equippedPaddle==ShopCatalog.DefaultPaddleId)&&(wallet.equippedPaddle==ShopCatalog.DefaultPaddleId||appearance.SkinVisual.GetComponentsInChildren<Renderer>().All(r=>r.enabled)));
            }
            SettingsOverlay.Open(home);await Task.Delay(250);Check("Settings still recognizes lobby host",SettingsOverlay.IsOpen&&ReferenceEquals(SettingsOverlay.Host,home));SettingsOverlay.Close();await Task.Delay(150);
            SceneManager.LoadScene("ModeSelection");await Task.Delay(250);Check("Leaving front-end releases suspended lobby",!menu.isLoaded&&!home&&SceneManager.sceneCount==1);
            SceneManager.LoadScene("Shop");await Task.Delay(250);UnityEngine.Object.FindAnyObjectByType<ShopScreen>().GoLobby();await Ready("Menu");
            Check("Direct Shop start can return without a cached lobby",SceneManager.GetActiveScene().name=="Menu"&&SceneManager.sceneCount==1);
            Check("No real wallet or graphics edits",PlayerPrefs.GetString(LocalEconomyStorage.Key,"")==prefs&&PlayerPrefs.GetString(GameSettings.PreferenceKey,"")==graphics);
            var report=new{passed=true,count=checks.Count,checks};Directory.CreateDirectory("../UnityWork/LobbyPerformance");File.WriteAllText("../UnityWork/LobbyPerformance/navigation-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
        }
        finally{PlayerEconomy.Configure(original);if(SettingsOverlay.IsOpen)SettingsOverlay.Close();SceneManager.LoadScene("Menu");}
    }
}
