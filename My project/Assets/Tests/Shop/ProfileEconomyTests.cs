using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Progression;
using PickleVerse.Shop;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class ProfileEconomyTests
    {
        string root;
        LocalPlayerStore store;
        ShopCatalog catalog;
        ProfileEconomyStorage storage;
        [SetUp] public void Setup()
        {
            root=Path.Combine(Path.GetTempPath(),"PKV-Currency-"+Guid.NewGuid().ToString("N"));
            store=new LocalPlayerStore(root);store.Open("First");
            store.Transaction(p=>{CurrencyPolicy.Migrate(p);p.coins=1000;p.gems=800;});
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();
            catalog.paddles=new[]{new PaddleOffer{id="skin",tokenpadPrice=100,elitePrice=300}};
            storage=new ProfileEconomyStorage(()=>store.Current,store.Transaction);
        }
        [TearDown] public void Cleanup(){store.Dispose();UnityEngine.Object.DestroyImmediate(catalog);Directory.Delete(root,true);}
        LocalPlayerEconomy Shop()=>new LocalPlayerEconomy(catalog,storage);
        [Test] public void LegacyGameplayEliteIsRemovedOnceWithoutLosingProgress()
        {
            var p=new PlayerProfile{coins=123,gems=999,elo=700,level=4,totalXp=80};
            Assert.That(CurrencyPolicy.Migrate(p));Assert.That(p.gems,Is.Zero);
            Assert.That(p.coins,Is.EqualTo(123));Assert.That(p.elo,Is.EqualTo(700));Assert.That(p.totalXp,Is.EqualTo(80));
            p.gems=25;Assert.That(CurrencyPolicy.Migrate(p),Is.False);Assert.That(p.gems,Is.EqualTo(25));
        }
        [Test] public void AllGameplayRewardsIgnoreLegacyEliteAmounts()
        {
            var p=store.Current;ProgressionEngine.Grant(p,new RewardBundle(20,900,35));
            Assert.That(p.gems,Is.EqualTo(800));Assert.That(p.coins,Is.EqualTo(1020));Assert.That(p.totalXp,Is.EqualTo(35));
        }
        [Test] public async Task ShopDebitsTheSameBalanceThatProgressionRewardsCredit()
        {
            ProgressionEngine.Grant(store.Current,new RewardBundle(250,5,0));store.Save();
            var result=await Shop().PurchaseAsync("skin",ShopCurrency.Tokenpad,"purchase");
            Assert.That(result.Success);Assert.That(store.Current.coins,Is.EqualTo(1150));Assert.That((await Shop().ReadAsync()).tokenpad,Is.EqualTo(1150));
            Assert.That(store.Current.gems,Is.EqualTo(800));
        }
        [Test] public async Task PurchaseAndMatchRewardStayIdempotentAfterReload()
        {
            await Shop().PurchaseAsync("skin",ShopCurrency.Elite,"buy");await Shop().GrantMatchRewardAsync("match",true,true);
            store.Dispose();store=new LocalPlayerStore(root);store.Open("First");
            await Shop().PurchaseAsync("skin",ShopCurrency.Elite,"buy");Assert.That(await Shop().GrantMatchRewardAsync("match",true,true),Is.False);
            Assert.That(store.Current.coins,Is.EqualTo(1150));Assert.That(store.Current.gems,Is.EqualTo(500));Assert.That((await Shop().ReadAsync()).Owns("skin"));
        }
        [Test] public async Task SwitchingPlayersSeparatesWalletAndInventory()
        {
            await Shop().PurchaseAsync("skin",ShopCurrency.Tokenpad,"buy");store.Switch("Second");CurrencyPolicy.Migrate(store.Current);
            var fresh=await Shop().ReadAsync();Assert.That(fresh.tokenpad,Is.Zero);Assert.That(fresh.elite,Is.Zero);Assert.That(fresh.Owns("skin"),Is.False);
            store.Switch("First");Assert.That((await Shop().ReadAsync()).tokenpad,Is.EqualTo(900));Assert.That((await Shop().ReadAsync()).Owns("skin"));
        }
        [Test] public void LegacyShopImportKeepsItemsAndTokenpadButRemovesFreeElite()
        {
            var legacy=new PlayerEconomyState{tokenpad=123,elite=1000,equippedPaddle="skin"};legacy.owned.Add("skin");
            Assert.That(ProfileEconomyStorage.ImportLegacy(store.Current,legacy));Assert.That(ProfileEconomyStorage.ImportLegacy(store.Current,legacy),Is.False);
            var imported=JsonUtility.FromJson<PlayerEconomyState>(store.Current.shopData);
            Assert.That(imported.tokenpad,Is.EqualTo(1123));Assert.That(imported.elite,Is.EqualTo(800));Assert.That(imported.Owns("skin"));
        }
        [Test] public void CorruptInventoryCannotResetTheWallet()
        {
            store.Current.shopData="invalid";
            Assert.Throws<ArgumentException>(()=>Shop());Assert.That(store.Current.coins,Is.EqualTo(1000));Assert.That(store.Current.gems,Is.EqualTo(800));
        }
    }
}
