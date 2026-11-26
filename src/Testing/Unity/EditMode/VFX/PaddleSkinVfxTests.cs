using System.Collections.Generic;
using System.Linq;
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

        GameObject BallEffectPrefab()
        {
            var root = new GameObject("Effect");
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Glitter Shell";
            shell.transform.SetParent(root.transform, false);
            new GameObject("Glints").AddComponent<ParticleSystem>().transform.SetParent(root.transform, false);
            return root;
        }

        [Test]
        public void BallSparkleGoesOutWhenTheOpponentHitsWithAStandardPaddle()
        {
            var prefab = BallEffectPrefab();
            try
            {
                profile.ballEffect = prefab;
                var fx = BallSkinVfx.Attach(ball, ball.AddComponent<TrailRenderer>());
                fx.Apply(profile);
                fx.Advance(1, true);
                var shell = ball.transform.Find("Effect/Glitter Shell").GetComponent<Renderer>();
                var emission = ball.GetComponentInChildren<ParticleSystem>().emission;
                Assert.That(shell.enabled, Is.True);
                Assert.That(emission.enabled, Is.True);

                fx.Apply(null);
                fx.Advance(1, true);
                Assert.That(shell.enabled, Is.False, "the glitter shell must not stay on a ball hit by a standard paddle");
                Assert.That(emission.enabled, Is.False);

                fx.Apply(profile);
                fx.Advance(1, true);
                Assert.That(shell.enabled, Is.True, "and it comes back with the next Gemstone hit");
                Assert.That(ball.transform.childCount, Is.EqualTo(1), "the same instance is reused");
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void APauseKeepsTheEffectButTheNextPointStartsStandard()
        {
            var fx = BallSkinVfx.Attach(ball, ball.AddComponent<TrailRenderer>());
            fx.Apply(profile);
            fx.Advance(.1f, true);
            fx.Advance(5, false); // paused in mid-air: the ball does not move
            Assert.That(fx.ActiveSkin, Is.EqualTo("paddle.gemstone"));
            ball.transform.position += Vector3.right * 3; // carried to the next server
            fx.Advance(.1f, false);
            Assert.That(fx.ActiveSkin, Is.Null);
        }

        [Test]
        public void ShippedLibraryGivesTheDragonRubyPaddleItsDragon()
        {
            var shipped = Resources.Load<PaddleSkinVfxLibrary>(PaddleSkinVfxLibrary.ResourcePath);
            var dragon = shipped.Find("paddle.dragonruby");
            Assert.IsNotNull(dragon);
            Assert.IsNotNull(dragon.trailMaterial);
            Assert.IsNotNull(dragon.impactEffect);
            var motion = dragon.ballEffect.GetComponent<DragonBallMotion>();
            Assert.IsNotNull(motion, "the dragon follows the ball by itself");
            Assert.That(motion.renderers.Length, Is.EqualTo(3), "dragon, sine ribbon, counter ribbon");
            foreach (var r in motion.renderers)
            {
                Assert.IsNotNull(r.sharedMaterial);
                Assert.IsFalse(r.enabled, "hidden until this paddle hits the ball");
                Assert.IsNotNull(r.GetComponent<MeshFilter>().sharedMesh);
            }
            foreach (var material in dragon.ballEffect.GetComponentsInChildren<Renderer>(true))
                Assert.IsFalse(material.sharedMaterial.shader.name.StartsWith("Hidden/"), material.name);
        }

        [Test]
        public void ShippedLibraryGivesTheBluePhoenixPaddleItsPhoenix()
        {
            var shipped = Resources.Load<PaddleSkinVfxLibrary>(PaddleSkinVfxLibrary.ResourcePath);
            var phoenix = shipped.Find("paddle.bluephoenix");
            Assert.IsNotNull(phoenix);
            Assert.IsNotNull(phoenix.trailMaterial);
            Assert.IsNotNull(phoenix.impactEffect);
            var motion = phoenix.ballEffect.GetComponent<PhoenixBallMotion>();
            Assert.IsNotNull(motion, "the phoenix follows the ball by itself");
            Assert.That(motion.renderers.Length, Is.EqualTo(3), "phoenix, blue fire ribbon, black smoke ribbon");
            Assert.That(motion.renderers[0].sharedMaterial.shader.name, Is.EqualTo("PickleVerse/VFX/Phoenix Spirit"));
            foreach (var r in motion.renderers)
            {
                Assert.IsNotNull(r.sharedMaterial);
                Assert.IsFalse(r.enabled, "hidden until this paddle hits the ball");
            }
            var ash = phoenix.ballEffect.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(p => p.sharedMaterial.shader.name == "PickleVerse/VFX/Black Ash").ToArray();
            Assert.That(ash.Length, Is.GreaterThanOrEqualTo(3), "black ash and black flames along the wings and tail");
            Assert.That(motion.riders.Length, Is.EqualTo(motion.riderDistances.Length));
            foreach (var ps in phoenix.ballEffect.GetComponentsInChildren<ParticleSystem>(true).Concat(phoenix.impactEffect.GetComponentsInChildren<ParticleSystem>(true)))
                Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(150), ps.name);
            foreach (var r in phoenix.ballEffect.GetComponentsInChildren<Renderer>(true).Concat(phoenix.impactEffect.GetComponentsInChildren<Renderer>(true)))
                Assert.IsFalse(r.sharedMaterial.shader.name.StartsWith("Hidden/"), r.name + " uses a shader that did not compile or was not found");
        }

        [Test]
        public void ThePhoenixMeshKeepsItsSpineLayout()
        {
            var shipped = Resources.Load<PaddleSkinVfxLibrary>(PaddleSkinVfxLibrary.ResourcePath);
            var mesh = shipped.Find("paddle.bluephoenix").ballEffect.transform.Find("Phoenix").GetComponent<MeshFilter>().sharedMesh;
            var b = mesh.bounds;
            // x: beak at the front, tail tips 1.9 m behind; y: spread wings; z: the back, thin
            Assert.That(b.min.x, Is.InRange(-.08f, 0f), "beak");
            Assert.That(b.max.x, Is.InRange(1.6f, 2.0f), "tail");
            Assert.That(b.size.y, Is.InRange(1.2f, 1.7f), "wing span");
            Assert.That(b.size.z, Is.LessThan(.4f), "flat across the back");
            var attr = new List<Vector2>();
            mesh.GetUVs(1, attr);
            Assert.That(attr.Count, Is.EqualTo(mesh.vertexCount), "flap and ash weights");
            Assert.IsTrue(attr.Any(a => a.x > .9f) && attr.Any(a => a.x == 0), "wing tips flap, body and tail do not");
            Assert.IsTrue(attr.Any(a => a.y > .95f), "feather tips burn to ash");
            Assert.That(mesh.colors.Length + mesh.colors32.Length, Is.GreaterThan(0), "glow colour per vertex");
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
