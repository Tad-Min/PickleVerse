using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PickleVerse.Progression;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using PickleVerse.Shop;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CurrencyIntegrationValidation
{
    static readonly List<object> checks=new List<object>();
    static void Check(string name,bool passed){checks.Add(new{name,passed});if(!passed)throw new Exception(name);}
    static async Task Wait(Func<bool> ready){var end=DateTime.UtcNow.AddSeconds(12);while(!ready()){if(DateTime.UtcNow>end)throw new TimeoutException();await Task.Delay(50);}await Task.Delay(150);}
    public static async Task<object> Run()
    {
        checks.Clear();var runtime=ProgressionRuntime.Instance;string original=runtime.Player.id;
        long coins=runtime.Player.coins,elite=runtime.Player.gems;string test="MC"+Guid.NewGuid().ToString("N").Substring(0,20);
        try
        {
            runtime.SwitchPlayer(test,"CURRENCY TEST");var economy=PlayerEconomy.Service;
            Check("Fresh profile starts with both balances zero",(await economy.ReadAsync()).tokenpad==0&&(await economy.ReadAsync()).elite==0);
            SceneManager.LoadScene("BotMatch");await Wait(()=>UnityEngine.Object.FindAnyObjectByType<BotMatch>()!=null);
            var match=UnityEngine.Object.FindAnyObjectByType<BotMatch>();match.SetPaused(true);if(match.opening&&match.opening.IsPlaying)match.opening.Skip();match.Restart();match.SetPaused(true);
            int before=runtime.Player.history.Count;
            var rules=new SinglesRules(0);rules.PrepareServe(-1);rules.Points[0]=10;rules.Games[0]=1;rules.Fail(1,Fault.Out);
            typeof(BotMatch).GetProperty("Rules").SetValue(match,rules);
            var finish=typeof(BotMatch).GetMethod("FinishRally",BindingFlags.Instance|BindingFlags.NonPublic);finish.Invoke(match,null);finish.Invoke(match,null);
            Check("Full bot match applies XP and history only once",runtime.Player.totalXp==10&&runtime.Player.history.Count==before+1&&runtime.Player.wins==1);
            Check("Full bot match credits shared TOKENPAD once and no ELITE",runtime.Player.coins==PlayerEconomy.Catalog.botWinReward&&runtime.Player.gems==0&&(await economy.ReadAsync()).tokenpad==runtime.Player.coins);
            long balance=runtime.Player.coins;Check("Daily reward is claimable",runtime.Claim("D.Play1"));Check("Mission credits the same wallet without ELITE",(await economy.ReadAsync()).tokenpad==balance+50&&(await economy.ReadAsync()).elite==0);
            runtime.UpdateWallet(p=>ProgressionEngine.Grant(p,new RewardBundle(1000,999,0)));
            var offer=PlayerEconomy.Catalog.paddles.First(p=>p.tokenpadPrice>0);balance=runtime.Player.coins;var buy=await economy.PurchaseAsync(offer.id,ShopCurrency.Tokenpad,"purchase-"+test);
            Check("Shop purchase debits progression balance",buy.Success&&runtime.Player.coins==balance-offer.tokenpadPrice&&runtime.Player.gems==0);
            await economy.PurchaseAsync(offer.id,ShopCurrency.Tokenpad,"purchase-"+test);Check("Replayed purchase cannot debit twice",runtime.Player.coins==balance-offer.tokenpadPrice);
            Check("Bought paddle can be equipped",await economy.EquipPaddleAsync(offer.id)==EquipStatus.Success);
            string second="MC"+Guid.NewGuid().ToString("N").Substring(0,20);runtime.SwitchPlayer(second,null);var other=await economy.ReadAsync();Check("Switching profile resets displayed wallet and inventory",other.tokenpad==0&&other.elite==0&&!other.Owns(offer.id));
            runtime.SwitchPlayer(test,null);var restored=await economy.ReadAsync();Check("Returning profile restores purchase and equipment",restored.tokenpad==balance-offer.tokenpadPrice&&restored.Owns(offer.id)&&restored.equippedPaddle==offer.id);
            SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu");var wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Check("Menu displays unified balances",wallet.tokenpad.text==runtime.Player.coins.ToString("N0")&&wallet.elite.text=="0");
            UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b=>b.name=="BtnStore").onClick.Invoke();await Wait(()=>SceneManager.GetActiveScene().name=="Shop");wallet=UnityEngine.Object.FindAnyObjectByType<WalletBar>();Check("Shop displays the same balances",wallet.tokenpad.text==runtime.Player.coins.ToString("N0")&&wallet.elite.text=="0");
            LobbyNavigation.ReturnToLobby();await Wait(()=>SceneManager.GetActiveScene().name=="Menu");runtime.SwitchPlayer(original,null);Check("Real player balance is untouched by test",runtime.Player.coins==coins&&runtime.Player.gems==elite);
            var result=new{passed=true,count=checks.Count,checks};File.WriteAllText("Docs/Progression/CurrencyValidation.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
        }
        finally
        {
            if(SceneManager.GetActiveScene().name!="Menu"){SceneManager.LoadScene("Menu");await Wait(()=>SceneManager.GetActiveScene().name=="Menu");}
            runtime.SwitchPlayer(original,null);
        }
    }
}
