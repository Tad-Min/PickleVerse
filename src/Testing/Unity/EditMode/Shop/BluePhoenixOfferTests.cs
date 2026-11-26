using System.Linq;
using NUnit.Framework;
using PickleVerse.Shop;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.Tests
{
    public sealed class BluePhoenixOfferTests
    {
        const string Id = "paddle.bluephoenix";
        static ShopCatalog Catalog => AssetDatabase.LoadAssetAtPath<ShopCatalog>("Assets/Resources/Shop/Shop Catalog.asset");
        static PaddleOffer Offer => Catalog.Find(Id);

        [Test]
        public void IsThePremiumNewestSkin()
        {
            Assert.IsNotNull(Offer);
            Assert.IsTrue(Offer.gemstone);
            Assert.That(Offer.Price(ShopCurrency.Tokenpad), Is.EqualTo(0));
            Assert.That(Offer.Price(ShopCurrency.Elite), Is.GreaterThan(0));
            Assert.That(Catalog.paddles[Catalog.NewestFirst()[0]].id, Is.EqualTo(Id), "the newest skin leads the shop");
        }

        [Test]
        public void HasEveryModelTheGameNeeds()
        {
            Assert.IsNotNull(Offer.previewPrefab);
            Assert.IsNotNull(Offer.gameplayPrefab);
            Assert.IsNotNull(Offer.thumbnail);
            var replay = Resources.Load<GameObject>("ReplayEquipment/" + Id);
            Assert.IsNotNull(replay);
            foreach (var prefab in new[] { Offer.previewPrefab, Offer.gameplayPrefab, replay })
            {
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(2), prefab.name + ": head and grip");
                Assert.IsTrue(renderers.All(r => r.sharedMaterial && r.sharedMaterial.shader.name == "PickleVerse/Paddle/Blue Phoenix"), prefab.name);
                Assert.IsTrue(renderers.All(r => r.sharedMaterial.GetTexture("_BaseMap") && r.sharedMaterial.GetTexture("_BumpMap") && r.sharedMaterial.GetTexture("_MaskMap")),
                    prefab.name + ": colour, relief and mask maps");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), prefab.name + ": cosmetic only");
                int triangles = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => (int)(f.sharedMesh.GetIndexCount(0) / 3));
                Assert.That(triangles, Is.InRange(15000, 30000), prefab.name);
            }
        }

        [Test]
        public void FillsTheSameVolumeAsTheOtherShopPaddles()
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var f in Offer.previewPrefab.GetComponentsInChildren<MeshFilter>())
            {
                var m = f.transform.localToWorldMatrix;
                var b = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            Assert.That(bounds.size.y, Is.EqualTo(2).Within(.06f), "two units tall");
            Assert.That(bounds.size.x, Is.InRange(.85f, 1.05f));
            Assert.That(bounds.size.z, Is.InRange(.12f, .25f), "a paddle, not a slab");
            Assert.That(bounds.center.y, Is.EqualTo(0).Within(.05f));
        }

        [Test]
        public void TheFaceIsARealReliefOnBothSides()
        {
            var head = Offer.previewPrefab.GetComponentsInChildren<MeshFilter>().Single(f => f.name.Contains("Head"));
            Assert.That(head.sharedMesh.GetIndexCount(0) / 3, Is.GreaterThan(9000), "frame plus a displaced front and back");
        }

        [Test]
        public void ShopDetailsDescribeTheEffect()
        {
            Assert.IsTrue(Offer.HasEffect);
            Assert.That(Offer.effectTitle, Is.Not.Empty);
            Assert.That(Offer.effectDetails, Does.Contain("phượng hoàng").And.Contain("tro đen").And.Contain("người cầm vợt này"));
            var bullets = ShopEffectPanel.Bullets(Offer.effectDetails).Split('\n');
            Assert.That(bullets.Length, Is.EqualTo(Offer.effectDetails.Split('\n').Length));
        }
    }
}
