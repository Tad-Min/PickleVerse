using System.Linq;
using NUnit.Framework;
using PickleVerse.Progression;
using PickleVerse.Shop;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class CatalogMergeTests
    {
        static ShopCatalog Catalog()
        {
            var catalog = ScriptableObject.CreateInstance<ShopCatalog>();
            catalog.paddles = new[] { new PaddleOffer { id = "paddle.a", title = "A" }, new PaddleOffer { id = "paddle.b", title = "B" } };
            return catalog;
        }

        // An administrator adds "paddle.new" in the CMS, or the server sells a skin of a newer build: every other
        // player must still load the profile and the shop.
        [Test]
        public void ItemsWithoutAModelInThisBuildAreSkipped()
        {
            var catalog = Catalog();
            Assert.DoesNotThrow(() => OnlinePlayerEconomy.Merge(catalog, new[]
            {
                new OnlineCosmeticOffer { id = "paddle.a", title = "Vợt A", elitePrice = 800 },
                new OnlineCosmeticOffer { id = "paddle.new", title = "Vợt mới", elitePrice = 5 },
                new OnlineCosmeticOffer { id = "" }, new OnlineCosmeticOffer { id = null },
            }));
            Assert.That(catalog.paddles.Select(p => p.id), Is.EqualTo(new[] { "paddle.a" }), "only items known to both sides stay");
            Assert.That(catalog.paddles[0].title, Is.EqualTo("Vợt A"));
            Assert.That(catalog.paddles[0].elitePrice, Is.EqualTo(800));
        }

        [Test]
        public void ItemsTheServerDoesNotSellAreHidden()
        {
            var catalog = Catalog();
            OnlinePlayerEconomy.Merge(catalog, new[] { new OnlineCosmeticOffer { id = "paddle.b", title = "B2" } });
            Assert.That(catalog.paddles.Select(p => p.id), Is.EqualTo(new[] { "paddle.b" }));
        }

        // The game ships a skin before it is sold; an admin publishes it while players are online.
        [Test]
        public void ASkinPublishedLaterAppearsWithoutRestartingTheGame()
        {
            var catalog = Catalog();
            OnlinePlayerEconomy.Merge(catalog, new[] { new OnlineCosmeticOffer { id = "paddle.a", title = "A" } });
            Assert.That(catalog.paddles.Select(p => p.id), Is.EqualTo(new[] { "paddle.a" }));
            Assert.That(catalog.Find("paddle.b"), Is.Null, "not sold yet");

            OnlinePlayerEconomy.Merge(catalog, new[] { new OnlineCosmeticOffer { id = "paddle.a", title = "A" }, new OnlineCosmeticOffer { id = "paddle.b", title = "Mới", elitePrice = 900 } });
            Assert.That(catalog.paddles.Select(p => p.id), Is.EqualTo(new[] { "paddle.a", "paddle.b" }));
            Assert.That(catalog.Find("paddle.b").title, Is.EqualTo("Mới"));
            Assert.That(catalog.Find("paddle.b").elitePrice, Is.EqualTo(900));
        }
    }
}
