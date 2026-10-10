using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PickleVerse.Shop;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class PaddleEquipmentTests
    {
        sealed class Memory:IEconomyStorage
        {
            public string json="";public bool fail;
            public string Read()=>json;
            public void Write(string value){if(fail)throw new IOException("Save failed");json=value;}
        }
        ShopCatalog catalog;Memory memory;LocalPlayerEconomy service;
        [SetUp] public void Setup()
        {
            catalog=ScriptableObject.CreateInstance<ShopCatalog>();catalog.defaultPaddle=new PaddleOffer{id=ShopCatalog.DefaultPaddleId};
            catalog.paddles=new[]{new PaddleOffer{id="gem",elitePrice=650},new PaddleOffer{id="thot",tokenpadPrice=900,elitePrice=250}};
            memory=new Memory();service=new LocalPlayerEconomy(catalog,memory);
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(catalog);}
        [Test] public async Task DefaultIsAlwaysOwnedWithoutPurchase()
        {var state=await service.ReadAsync();Assert.That(state.Owns(ShopCatalog.DefaultPaddleId));Assert.That(state.equippedPaddle,Is.EqualTo(ShopCatalog.DefaultPaddleId));Assert.That((await service.PurchaseAsync(ShopCatalog.DefaultPaddleId,ShopCurrency.Elite,"default")).status,Is.EqualTo(PurchaseStatus.AlreadyOwned));}
        [Test] public async Task UnownedAndUnknownCannotEquip()
        {string before=memory.json;Assert.That(await service.EquipPaddleAsync("gem"),Is.EqualTo(EquipStatus.NotOwned));Assert.That(await service.EquipPaddleAsync("unknown"),Is.EqualTo(EquipStatus.InvalidItem));Assert.That(memory.json,Is.EqualTo(before));}
        [Test] public async Task EquipPersistsAndDoesNotSpendMoney()
        {
            await service.PurchaseAsync("gem",ShopCurrency.Elite,"buy");var before=await service.ReadAsync();
            Assert.That(await service.EquipPaddleAsync("gem"),Is.EqualTo(EquipStatus.Success));service=new LocalPlayerEconomy(catalog,memory);var after=await service.ReadAsync();
            Assert.That(after.equippedPaddle,Is.EqualTo("gem"));Assert.That(after.elite,Is.EqualTo(before.elite));Assert.That(after.tokenpad,Is.EqualTo(before.tokenpad));Assert.That(after.purchases.Count,Is.EqualTo(1));
            Assert.That(await service.EquipPaddleAsync("gem"),Is.EqualTo(EquipStatus.AlreadyEquipped));Assert.That(await service.EquipPaddleAsync(ShopCatalog.DefaultPaddleId),Is.EqualTo(EquipStatus.Success));
        }
        [Test] public async Task LegacyWalletKeepsBalancesOwnershipAndReceipts()
        {
            memory.json="{\"version\":1,\"tokenpad\":432,\"elite\":350,\"owned\":[\"gem\"],\"rewardedMatches\":[\"match1\"],\"purchases\":[{\"requestId\":\"receipt1\",\"itemId\":\"gem\",\"currency\":1}]}";
            service=new LocalPlayerEconomy(catalog,memory);var state=await service.ReadAsync();Assert.That(state.equippedPaddle,Is.EqualTo(ShopCatalog.DefaultPaddleId));Assert.That(state.tokenpad,Is.EqualTo(432));Assert.That(state.elite,Is.EqualTo(350));Assert.That(state.owned,Does.Contain("gem"));Assert.That(state.rewardedMatches,Does.Contain("match1"));Assert.That(state.purchases[0].requestId,Is.EqualTo("receipt1"));
        }
        [TestCase("unknown")] [TestCase("thot")] public async Task InvalidSavedEquipmentFallsBack(string id)
        {memory.json=JsonUtility.ToJson(new PlayerEconomyState{tokenpad=432,elite=350,equippedPaddle=id});service=new LocalPlayerEconomy(catalog,memory);Assert.That((await service.ReadAsync()).equippedPaddle,Is.EqualTo(ShopCatalog.DefaultPaddleId));}
        [Test] public async Task SaveFailureKeepsPreviousEquipment()
        {await service.PurchaseAsync("gem",ShopCurrency.Elite,"buy");memory.fail=true;try{await service.EquipPaddleAsync("gem");Assert.Fail();}catch(IOException){}Assert.That((await service.ReadAsync()).equippedPaddle,Is.EqualTo(ShopCatalog.DefaultPaddleId));}
        [Test] public void DefaultBelongsToCollectionOnly()
        {Assert.That(catalog.CollectionItems.Length,Is.EqualTo(3));Assert.That(catalog.paddles.Length,Is.EqualTo(2));Assert.That(catalog.CollectionItems[0].id,Is.EqualTo(ShopCatalog.DefaultPaddleId));}
    }
}
