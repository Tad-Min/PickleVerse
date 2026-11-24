using NUnit.Framework;
using PickleVerse.LocalMatch;
using PickleVerse.Shop;
using PickleVerse.VFX;
using UnityEngine;

namespace PickleVerse.Tests.VFX
{
    public sealed class PaddleSkinVfxTests
    {
        GameObject ball;
        Material standard, gem;
        PaddleSkinVfxProfile profile;
        PaddleSkinVfxLibrary library;

        [SetUp]
        public void SetUp()
        {
            ball = new GameObject("Ball");
            standard = new Material(Shader.Find("Sprites/Default"));
            gem = new Material(Shader.Find("Sprites/Default"));
            profile = ScriptableObject.CreateInstance<PaddleSkinVfxProfile>();
            profile.skinId = "paddle.gemstone";
            profile.trailMaterial = gem;
            profile.trailTime = .4f;
            profile.trailWidth = .085f;
            library = ScriptableObject.CreateInstance<PaddleSkinVfxLibrary>();
            library.profiles = new[] { profile };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ball);
            Object.DestroyImmediate(standard);
            Object.DestroyImmediate(gem);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(library);
        }

        [Test]
        public void LibraryFindsOnlyTheSkinsWithAnEffect()
        {
            Assert.That(library.Find("paddle.gemstone"), Is.SameAs(profile));
            Assert.That(library.Find(ShopCatalog.DefaultPaddleId), Is.Null);
            Assert.That(library.Find(null), Is.Null);
            Assert.That(library.Find(""), Is.Null);
        }

        [Test]
        public void AHitterWithoutAPaddleSkinCountsAsTheStandardPaddle()
        {
            Assert.That(BallSkinVfx.SkinOf(null), Is.EqualTo(ShopCatalog.DefaultPaddleId));
            var actor = new GameObject("Actor").AddComponent<MatchActor>();
            try { Assert.That(BallSkinVfx.SkinOf(actor), Is.EqualTo(ShopCatalog.DefaultPaddleId)); }
            finally { Object.DestroyImmediate(actor.gameObject); }
        }

        [Test]
        public void SkinTrailIsUsedAfterItsHitAndTheStandardTrailComesBack()
        {
            var trail = ball.AddComponent<TrailRenderer>();
            trail.sharedMaterial = standard;
            trail.time = .2f;
            trail.widthMultiplier = .05f;
            var fx = BallSkinVfx.Attach(ball, trail);

            fx.Apply(profile);
            Assert.That(fx.ActiveSkin, Is.EqualTo("paddle.gemstone"));
            Assert.That(trail.sharedMaterial, Is.SameAs(gem));
            Assert.That(trail.time, Is.EqualTo(.4f).Within(1e-5f));
            Assert.That(trail.widthMultiplier, Is.EqualTo(.085f).Within(1e-5f));

            fx.Apply(null); // the opponent hits it with a normal paddle
            Assert.That(fx.ActiveSkin, Is.Null);
            Assert.That(trail.sharedMaterial, Is.SameAs(standard));
            Assert.That(trail.time, Is.EqualTo(.2f).Within(1e-5f));
            Assert.That(trail.widthMultiplier, Is.EqualTo(.05f).Within(1e-5f));
            Assert.That(BallSkinVfx.Attach(ball, trail), Is.SameAs(fx), "registering again reuses the same effect");
        }

        [Test]
        public void ShippedLibraryGivesTheGemstonePaddleItsEffect()
        {
            var shipped = Resources.Load<PaddleSkinVfxLibrary>(PaddleSkinVfxLibrary.ResourcePath);
            Assert.That(shipped, Is.Not.Null, "Resources/VFX/PaddleSkinVfxLibrary is missing");
            var gemstone = shipped.Find("paddle.gemstone");
            Assert.That(gemstone, Is.Not.Null);
            Assert.That(gemstone.trailMaterial, Is.Not.Null);
            Assert.That(gemstone.ballEffect, Is.Not.Null);
            Assert.That(gemstone.impactEffect, Is.Not.Null);
            Assert.That(gemstone.ballEffect.transform.Find("Glitter Shell"), Is.Not.Null);
            Assert.That(shipped.Find(ShopCatalog.DefaultPaddleId), Is.Null, "the standard paddle keeps the standard trail");
        }
    }
}
