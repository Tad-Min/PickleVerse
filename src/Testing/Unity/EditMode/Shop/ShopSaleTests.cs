using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PickleVerse.Shop;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class ShopSaleTests
    {
        static PaddleOffer Offer(int tokenpad, int elite) => new PaddleOffer { id = "paddle.a", title = "A", tokenpadPrice = tokenpad, elitePrice = elite };
        static PlayerEconomyState Wallet(int tokenpad, int elite, params string[] owned) => new PlayerEconomyState { tokenpad = tokenpad, elite = elite, owned = owned.ToList() };

        // The admin page publishes only the skins of PaddleSkins.cs. A model that is missing there can never be sold; a
        // code without a model would be skipped by the game.
        [Test]
        public void ServerKnowsEverySkinThatHasAModel()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath, "../../src/Backend/Entities/Game/PaddleSkins.cs"));
            if (!File.Exists(source))
                Assert.Ignore("No backend source next to this project.");
            var server = Regex.Matches(File.ReadAllText(source), "new\\(\"(paddle\\.[^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value);
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
            Assert.That(server, Is.EquivalentTo(catalog.paddles.Select(p => p.id)));
        }

        [TestCase(900, 250, "900 TOKENPAD  /  250 ELITE TOKEN")]
        [TestCase(0, 250, "250 ELITE TOKEN")]
        [TestCase(900, 0, "900 TOKENPAD")]
        [TestCase(0, 0, "NGỪNG BÁN")]
        public void CardShowsOnlyTheCurrenciesThatHaveAPrice(int tokenpad, int elite, string expected)
        {
            Assert.That(ShopProductCard.Price(Offer(tokenpad, elite)), Is.EqualTo(expected));
        }

        [TestCase(900, 250, true, true, "MUA • 250 ELITE TOKEN")]
        [TestCase(0, 250, false, true, "MUA • 250 ELITE TOKEN")]
        [TestCase(900, 0, true, false, "")]
        [TestCase(0, 0, false, true, "NGỪNG BÁN")]
        public void DetailsShowABuyButtonPerCurrency(int tokenpad, int elite, bool tokenpadShown, bool eliteShown, string eliteLabel)
        {
            var buttons = new ShopBuyButtons(Offer(tokenpad, elite), false);
            Assert.That(buttons.tokenpad, Is.EqualTo(tokenpadShown));
            Assert.That(buttons.elite, Is.EqualTo(eliteShown));
            if (tokenpadShown)
                Assert.That(buttons.tokenpadLabel, Is.EqualTo("MUA • 900 TOKENPAD"));
            if (eliteShown)
                Assert.That(buttons.eliteLabel, Is.EqualTo(eliteLabel));
        }

        [TestCase(900, 250)]
        [TestCase(900, 0)]
        [TestCase(0, 0)]
        public void AnOwnedSkinOffersNoPurchase(int tokenpad, int elite)
        {
            var offer = Offer(tokenpad, elite);
            var wallet = Wallet(5000, 5000, offer.id);
            var buttons = new ShopBuyButtons(offer, true);
            Assert.IsFalse(buttons.tokenpad);
            Assert.IsTrue(buttons.elite);
            Assert.That(buttons.eliteLabel, Is.EqualTo("ĐÃ SỞ HỮU"));
            Assert.IsFalse(ShopBuyButtons.CanBuy(offer, wallet, ShopCurrency.Tokenpad));
            Assert.IsFalse(ShopBuyButtons.CanBuy(offer, wallet, ShopCurrency.Elite));
            Assert.IsNull(ShopBuyButtons.Problem(offer, wallet));
        }

        [Test]
        public void ACurrencyWithoutAPriceCannotBeSpent()
        {
            var rich = Wallet(5000, 5000);
            Assert.IsFalse(ShopBuyButtons.CanBuy(Offer(900, 0), rich, ShopCurrency.Elite), "0 ELITE TOKEN is not a price");
            Assert.IsTrue(ShopBuyButtons.CanBuy(Offer(900, 0), rich, ShopCurrency.Tokenpad));
            Assert.IsFalse(ShopBuyButtons.CanBuy(Offer(0, 800), rich, ShopCurrency.Tokenpad));
            Assert.IsTrue(ShopBuyButtons.CanBuy(Offer(0, 800), rich, ShopCurrency.Elite));
            Assert.IsFalse(ShopBuyButtons.CanBuy(Offer(0, 800), Wallet(5000, 799), ShopCurrency.Elite));
        }

        [Test]
        public void TheReasonMatchesWhatThePlayerCanDo()
        {
            Assert.IsNull(ShopBuyButtons.Problem(Offer(900, 250), Wallet(900, 0)), "one affordable currency is enough");
            Assert.That(ShopBuyButtons.Problem(Offer(900, 250), Wallet(0, 0)), Does.Contain("Tokenpad"));
            Assert.That(ShopBuyButtons.Problem(Offer(900, 0), Wallet(0, 9999)), Does.Contain("Tokenpad"));
            Assert.That(ShopBuyButtons.Problem(Offer(0, 800), Wallet(9999, 0)), Does.Contain("ELITE TOKEN"));
            Assert.That(ShopBuyButtons.Problem(Offer(0, 0), Wallet(9999, 9999)), Does.Contain("ngừng bán"));
        }

        [Test]
        public void DiscontinuedSkinsComeAfterEverythingThatIsSold()
        {
            var c = ScriptableObject.CreateInstance<ShopCatalog>();
            try
            {
                c.paddles = new[]
                {
                    new PaddleOffer { id = "new but stopped", releaseOrder = 9 }, new PaddleOffer { id = "old", releaseOrder = 1, tokenpadPrice = 900 },
                    new PaddleOffer { id = "recent", releaseOrder = 5, elitePrice = 800 },
                };
                Assert.That(c.NewestFirst(), Is.EqualTo(new[] { 2, 1, 0 }));
            }
            finally
            {
                Object.DestroyImmediate(c);
            }
        }
    }
}
