using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using PickleVerse.Shop;

namespace PickleVerse.Tests
{
    public sealed class LocalEconomyTests
    {
        sealed class Memory : IEconomyStorage
        {
            public string json="";public bool fail;
            public string Read()=>json;
            public void Write(string value){if(fail)throw new IOException("Disk unavailable");json=value;}
        }
        ShopCatalog catalog;Memory memory;LocalPlayerEconomy service;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.paddles=new[]{new PaddleOffer{id="gem",elitePrice=650},new PaddleOffer{id="thot",tokenpadPrice=900,elitePrice=250}};
            memory=new Memory();service=new LocalPlayerEconomy(catalog,memory);
        }
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(catalog);}
        [Test] public async Task DemoBalanceIsSeededOnce()
        {await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");service=new LocalPlayerEconomy(catalog,memory);var s=await service.ReadAsync();Assert.That(s.elite,Is.EqualTo(350));Assert.That(s.tokenpad,Is.EqualTo(1000));}
        [TestCase(ShopCurrency.Tokenpad,100,1000)] [TestCase(ShopCurrency.Elite,1000,750)]
        public async Task ThotSupportsEitherCurrency(ShopCurrency currency,int tokenpad,int elite)
        {var r=await service.PurchaseAsync("thot",currency,"one");Assert.That(r.Success);Assert.That(r.wallet.tokenpad,Is.EqualTo(tokenpad));Assert.That(r.wallet.elite,Is.EqualTo(elite));Assert.That(r.wallet.owned,Does.Contain("thot"));}
        [Test] public async Task GemstoneRejectsTokenpad()
        {var r=await service.PurchaseAsync("gem",ShopCurrency.Tokenpad,"one");Assert.That(r.status,Is.EqualTo(PurchaseStatus.NotAllowed));Assert.That(r.wallet.tokenpad,Is.EqualTo(1000));Assert.That(r.wallet.owned,Is.Empty);}
        [Test] public async Task InsufficientFundsLeaveStateUntouched()
        {memory.json=JsonUtility.ToJson(new PlayerEconomyState{tokenpad=10,elite=20});service=new LocalPlayerEconomy(catalog,memory);string before=memory.json;var r=await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");Assert.That(r.status,Is.EqualTo(PurchaseStatus.InsufficientFunds));Assert.That(memory.json,Is.EqualTo(before));}
        [Test] public async Task RequestReplayDoesNotDebitTwice()
        {await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");var r=await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");Assert.That(r.Success);Assert.That(r.wallet.elite,Is.EqualTo(350));Assert.That(r.wallet.owned.Count,Is.EqualTo(1));}
        [Test] public async Task ReplayCannotChangeItemOrCurrency()
        {await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");var r=await service.PurchaseAsync("thot",ShopCurrency.Tokenpad,"one");Assert.That(r.status,Is.EqualTo(PurchaseStatus.InvalidRequest));Assert.That(r.wallet.tokenpad,Is.EqualTo(1000));}
        [Test] public async Task OwnedItemCannotBeBoughtWithOtherCurrency()
        {await service.PurchaseAsync("thot",ShopCurrency.Tokenpad,"one");var r=await service.PurchaseAsync("thot",ShopCurrency.Elite,"two");Assert.That(r.status,Is.EqualTo(PurchaseStatus.AlreadyOwned));Assert.That(r.wallet.elite,Is.EqualTo(1000));}
        [Test] public async Task InvalidItemAndCurrencyCannotSpend()
        {Assert.That((await service.PurchaseAsync("missing",ShopCurrency.Elite,"one")).status,Is.EqualTo(PurchaseStatus.InvalidItem));Assert.That((await service.PurchaseAsync("gem",(ShopCurrency)999,"two")).status,Is.EqualTo(PurchaseStatus.InvalidRequest));Assert.That((await service.ReadAsync()).elite,Is.EqualTo(1000));}
        [Test] public async Task FailedSaveDoesNotMutateMemoryState()
        {memory.fail=true;try{await service.PurchaseAsync("gem",ShopCurrency.Elite,"one");Assert.Fail("Expected a write failure");}catch(IOException){}Assert.That((await service.ReadAsync()).elite,Is.EqualTo(1000));Assert.That((await service.ReadAsync()).owned,Is.Empty);}
        [Test] public async Task RewardsAreIdempotentAndNeverGrantElite()
        {Assert.That(await service.GrantMatchRewardAsync("bot:one",false,true));Assert.That(await service.GrantMatchRewardAsync("bot:one",false,true),Is.False);Assert.That(await service.GrantMatchRewardAsync("online:two",true,true));var s=await service.ReadAsync();Assert.That(s.tokenpad,Is.EqualTo(1250));Assert.That(s.elite,Is.EqualTo(1000));}
        [Test] public async Task RewardSaveFailureDoesNotMarkItClaimed()
        {memory.fail=true;try{await service.GrantMatchRewardAsync("bot:one",false,true);Assert.Fail();}catch(IOException){}memory.fail=false;Assert.That(await service.GrantMatchRewardAsync("bot:one",false,true));}
        [Test] public async Task SnapshotIsIsolatedFromCallerChanges()
        {var s=await service.ReadAsync();s.elite=9999;s.owned.Add("gem");var actual=await service.ReadAsync();Assert.That(actual.elite,Is.EqualTo(1000));Assert.That(actual.owned,Is.Empty);}
    }
}
