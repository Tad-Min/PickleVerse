using System.Linq;
using NUnit.Framework;
using PickleVerse.ArtDirection;
using PickleVerse.LocalMatch;
using PickleVerse.Rules;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.ArtDirection.Tests
{
    /// <summary>Art Bible (Doc/ArtBible) rules that the code and assets must keep.</summary>
    public sealed class ArtDirectionTests
    {
        const string LooksPath = "Assets/Resources/ArtDirection/ArtDirectionLooks.asset";

        static ArtDirectionLooks Looks => AssetDatabase.LoadAssetAtPath<ArtDirectionLooks>(LooksPath);

        [TestCase(0, false, 0)]
        [TestCase(1, false, 1)]
        [TestCase(2, false, 2)]
        [TestCase(7, false, 2)]
        [TestCase(-3, false, 0)]
        [TestCase(2, true, 0)]
        public void QualityTierFollowsPresetAndMobile(int graphics, bool mobile, int expected) =>
            Assert.AreEqual(expected, ArtDirectionRuntime.QualityTier(graphics, mobile));

        [Test]
        public void PostAntialiasingOnlyWhenMsaaIsOff()
        {
            Assert.AreEqual(AntialiasingMode.None, ArtDirectionRuntime.PostAntialiasing(2, 4));
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, ArtDirectionRuntime.PostAntialiasing(2, 1));
            Assert.AreEqual(AntialiasingMode.FastApproximateAntialiasing, ArtDirectionRuntime.PostAntialiasing(1, 1));
            Assert.AreEqual(AntialiasingMode.None, ArtDirectionRuntime.PostAntialiasing(0, 1));
        }

        [Test]
        public void AtmosphereFadesWithNightWeight()
        {
            Assert.AreEqual(1f, AtmosphereNightResponse.Multiplier(1, .12f, 0), 1e-5);
            Assert.AreEqual(.12f, AtmosphereNightResponse.Multiplier(1, .12f, 1), 1e-5);
            Assert.AreEqual(.56f, AtmosphereNightResponse.Multiplier(1, .12f, .5f), 1e-5);
            Assert.AreEqual(.12f, AtmosphereNightResponse.Multiplier(1, .12f, 3), 1e-5);
        }

        [TestCase("Menu", "Sunset District")]
        [TestCase("BotMatch", "Sunset District")]
        [TestCase("MultiplayerCourt", "Sunset District")]
        [TestCase("Training", "Academy Arena")]
        [TestCase("Tutorial", "Academy Arena")]
        [TestCase("Replay", "Academy Arena")]
        [TestCase("Shop", "Showroom")]
        [TestCase("Collection", "Showroom")]
        public void EveryThreeDimensionalSceneHasALook(string scene, string look)
        {
            var found = Looks.Find(scene);
            Assert.IsNotNull(found, scene);
            Assert.AreEqual(look, found.name);
            Assert.IsTrue(found.postProcess);
            Assert.IsNotNull(found.profile);
        }

        [TestCase("Login")]
        [TestCase("Loading")]
        [TestCase("ModeSelection")]
        [TestCase("PlayMultiplayer")]
        [TestCase("Settings")]
        [TestCase("")]
        [TestCase(null)]
        public void UiScenesAreNeverTonemapped(string scene) => Assert.IsNull(Looks.Find(scene));

        [Test]
        public void LooksUseNeutralTonemappingAndRestrainedEffects()
        {
            foreach (var look in Looks.looks)
                foreach (var profile in new[] { look.profile, look.nightProfile }.Where(p => p))
                {
                    Assert.IsTrue(profile.TryGet<Tonemapping>(out var tone), profile.name);
                    Assert.AreEqual(TonemappingMode.Neutral, tone.mode.value, profile.name);
                    Assert.IsTrue(profile.TryGet<Bloom>(out var bloom), profile.name);
                    Assert.LessOrEqual(bloom.intensity.value, .5f, profile.name + ": bloom must stay subtle");
                    Assert.IsTrue(profile.TryGet<Vignette>(out var vignette), profile.name);
                    Assert.LessOrEqual(vignette.intensity.value, .3f, profile.name + ": vignette must stay subtle");
                    Assert.IsFalse(profile.Has<DepthOfField>(), profile.name + ": depth of field is cinematic-only");
                }
            Assert.IsTrue(Looks.cinematicProfile.TryGet<DepthOfField>(out var dof));
            Assert.AreEqual(DepthOfFieldMode.Gaussian, dof.mode.value);
            Assert.IsTrue(Looks.lowQualityProfile.TryGet<Bloom>(out var low));
            Assert.AreEqual(0f, low.intensity.value);
        }

        [Test]
        public void WarmKeyAndCoolShadowPalette()
        {
            foreach (var key in new[] { MatchEnvironmentLighting.GoldenHourSunColor, MatchEnvironmentLighting.ArenaSkylightColor, StadiumEnvironment.FloodlightColor })
                Assert.IsTrue(key.r >= key.g && key.g > key.b, "key lights are warm: " + key);
            foreach (var look in Looks.looks)
                Assert.IsTrue(look.shadowTint.b > look.shadowTint.r && look.shadowTint.a > 0 && look.shadowTint.a < .5f, look.name + ": cool, partial shadow tint");
            Assert.Greater(MatchEnvironmentLighting.ArenaSkylight.eulerAngles.x, 45f, "arena key comes from above the membrane roof");
        }

        [Test]
        public void StylizedLitShaderCompilesForTheBatcher()
        {
            var shader = Shader.Find("PickleVerse/Stylized Lit");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported);
            Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error));
            Assert.AreEqual(5, shader.passCount, "ForwardLit, ShadowCaster, DepthOnly, DepthNormals, Meta");
        }

        [Test]
        public void StandardMaterialSetsAreComplete()
        {
            foreach (var set in new[] { "Court", "Metal", "Wood", "Stone", "Fabric" })
                foreach (var map in new[] { "BaseColor", "Normal", "Mask" })
                {
                    string path = $"Assets/Textures/ArtDirection/{set}/T_{set}_{map}.png";
                    Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(path), path);
                    if (map == "Normal")
                        Assert.AreEqual(TextureImporterType.NormalMap, ((TextureImporter)AssetImporter.GetAtPath(path)).textureType, path);
                    if (map == "Mask")
                        Assert.IsFalse(((TextureImporter)AssetImporter.GetAtPath(path)).sRGBTexture, path + " is linear data");
                }
            var shader = Shader.Find("PickleVerse/Stylized Lit");
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials/ArtDirection" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m.shader.name.StartsWith("PickleVerse/FX/")) continue;
                Assert.AreEqual(shader, m.shader, m.name);
            }
        }

        [Test]
        public void CourtFeatureMatchesTheRulesCourt()
        {
            foreach (var path in new[] { "Assets/EnvironmentContent/SunsetDistrict/Materials/SD_Acrylic.mat", "Assets/LocalMatchContent/StadiumEnhanced/Materials/EN_CourtCompetition.mat" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsTrue(m.IsKeywordEnabled("_COURT"), path);
                var p = m.GetVector("_CourtParams");
                Assert.AreEqual(0f, p.x, 1e-3);
                Assert.AreEqual(0f, p.y, 1e-3);
                Assert.AreEqual(Court.HalfWidth, p.z, .01f, path);
                Assert.AreEqual(Court.HalfLength, p.w, .01f, path);
                Assert.AreEqual(Court.Kitchen, m.GetFloat("_KitchenDepth"), .01f, path);
            }
        }

        [Test]
        public void AudienceUsesItsLightModelAtGameplayDistance()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Audience/Professional.prefab");
            var groups = prefab.GetComponentsInChildren<LODGroup>(true).Where(g => g.GetLODs().Length == 2 && g.GetLODs()[0].renderers.FirstOrDefault() is SkinnedMeshRenderer).ToArray();
            Assert.IsNotEmpty(groups);
            foreach (var g in groups)
            {
                var lods = g.GetLODs();
                Assert.GreaterOrEqual(lods[0].screenRelativeTransitionHeight, .5f, g.name);
                Assert.IsTrue(lods[1].renderers.OfType<SkinnedMeshRenderer>().All(r => r.quality == SkinQuality.Bone2), g.name);
            }
        }
    }
}
