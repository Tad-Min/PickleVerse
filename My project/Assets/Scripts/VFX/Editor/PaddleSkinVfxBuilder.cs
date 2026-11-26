using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PickleVerse.VFX.Editor
{
    /// <summary>
    /// Builds the paddle-skin effects under Assets/VFX from their Blender-made textures (ArtSource/VFX/&lt;Skin&gt;) and
    /// registers them in Resources/VFX/PaddleSkinVfxLibrary. Menu: PickleVerse/VFX/Rebuild paddle skin effects.
    /// </summary>
    public static class PaddleSkinVfxBuilder
    {
        const string Root = "Assets/VFX";
        const string LibraryPath = "Assets/Resources/VFX/PaddleSkinVfxLibrary.asset";
        static readonly Color Diamond = new Color(.92f, .96f, 1f), Emerald = new Color(.22f, 1f, .62f), Gold = new Color(1f, .83f, .55f);

        [MenuItem("PickleVerse/VFX/Rebuild paddle skin effects")]
        public static string BuildAll()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            return Register(BuildGemstone(), BuildDragonRuby(), BuildBluePhoenix());
        }

        /// <summary>Builds only the Blue Phoenix effect and registers it, leaving the other effects' assets as they are.</summary>
        [MenuItem("PickleVerse/VFX/Rebuild Blue Phoenix effect")]
        public static string BuildBluePhoenixEffect() => Register(BuildBluePhoenix());

        static string Register(params PaddleSkinVfxProfile[] built)
        {
            var library = AssetDatabase.LoadAssetAtPath<PaddleSkinVfxLibrary>(LibraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<PaddleSkinVfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.profiles = library.profiles.Where(p => p && built.All(b => b.skinId != p.skinId)).Concat(built).ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return "paddle skin effects: " + string.Join(", ", library.profiles.Select(p => p.skinId));
        }

        static PaddleSkinVfxProfile BuildGemstone()
        {
            string dir = Root + "/PaddleSkins/Gemstone";
            Directory.CreateDirectory(dir + "/Materials");
            Directory.CreateDirectory(dir + "/Prefabs");
            var star = Texture(dir + "/Textures/T_Gemstone_StarGlint.png", TextureWrapMode.Clamp);
            var ribbon = Texture(dir + "/Textures/T_Gemstone_Ribbon.png", TextureWrapMode.Repeat);

            var trail = Material(dir + "/Materials/M_Gemstone_Trail.mat", "PickleVerse/VFX/Skin Trail");
            trail.SetTexture("_BaseMap", ribbon);
            trail.SetTextureScale("_BaseMap", new Vector2(3, 1));
            trail.SetFloat("_Intensity", 3.2f);
            trail.SetFloat("_Scroll", 1.6f);
            trail.SetFloat("_Twinkle", .65f);
            trail.SetFloat("_TwinkleDensity", 46);
            var glint = Material(dir + "/Materials/M_Gemstone_Glint.mat", "PickleVerse/VFX/Additive Flipbook");
            glint.SetTexture("_BaseMap", star);
            glint.SetFloat("_Intensity", 3f);
            var glitter = Material(dir + "/Materials/M_Gemstone_BallGlitter.mat", "PickleVerse/VFX/Ball Glitter");
            glitter.SetColor("_ColorA", new Color(1f, .9f, .7f) * 1.6f);
            glitter.SetColor("_ColorB", new Color(.3f, 1f, .7f) * 1.6f);
            glitter.SetFloat("_Cells", 9);
            glitter.SetFloat("_Intensity", 2.4f);

            // Ball effect: glints shed along the path, golden gem dust, glints orbiting the ball, a glittering shell.
            var ballRoot = new GameObject("Gemstone_BallFx");
            var glints = Particles(ballRoot.transform, "Trail Glints", glint, 90, ParticleSystemSimulationSpace.World);
            Main(glints, new Vector2(.4f, .7f), new Vector2(0f, .15f), new Vector2(.07f, .15f));
            SetColors(glints, Diamond, Emerald, Gold);
            var e = glints.emission; e.rateOverTime = 0; e.rateOverDistance = 24;
            Sphere(glints, .05f);
            Flipbook(glints, true);
            Noise(glints, .08f);
            var dust = Particles(ballRoot.transform, "Gem Dust", glint, 140, ParticleSystemSimulationSpace.World);
            Main(dust, new Vector2(.4f, .8f), new Vector2(0f, .1f), new Vector2(.014f, .03f));
            SetColors(dust, Gold, new Color(1f, .95f, .8f), Gold);
            var dm = dust.main; dm.gravityModifier = .25f;
            e = dust.emission; e.rateOverTime = 0; e.rateOverDistance = 34;
            Sphere(dust, .04f);
            Flipbook(dust, false, 7);
            var orbit = Particles(ballRoot.transform, "Orbit Glints", glint, 10, ParticleSystemSimulationSpace.Local);
            Main(orbit, new Vector2(.22f, .36f), new Vector2(0f, 0f), new Vector2(.04f, .07f));
            SetColors(orbit, Diamond, Emerald, Diamond);
            e = orbit.emission; e.rateOverTime = 8;
            var os = orbit.shape; os.shapeType = ParticleSystemShapeType.Sphere; os.radius = .07f; os.radiusThickness = 0;
            Flipbook(orbit, true);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "Glitter Shell";
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(ballRoot.transform, false);
            shell.transform.localScale = Vector3.one * .085f;
            var sr = shell.GetComponent<MeshRenderer>();
            sr.sharedMaterial = glitter;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.lightProbeUsage = LightProbeUsage.Off;
            sr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var ballFx = PrefabUtility.SaveAsPrefabAsset(ballRoot, dir + "/Prefabs/Gemstone_BallFx.prefab");
            Object.DestroyImmediate(ballRoot);

            // Contact: a ring of glints bursting off the paddle face plus one bright star flash.
            var impactRoot = new GameObject("Gemstone_Impact");
            var burst = Particles(impactRoot.transform, "Burst Glints", glint, 20, ParticleSystemSimulationSpace.World);
            Main(burst, new Vector2(.28f, .5f), new Vector2(1f, 2.6f), new Vector2(.06f, .13f));
            var bm = burst.main; bm.loop = false; bm.playOnAwake = false; bm.duration = .5f;
            SetColors(burst, Diamond, Emerald, Gold);
            e = burst.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 14) });
            Sphere(burst, .06f);
            var limit = burst.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .25f; limit.limit = .4f;
            Flipbook(burst, true);
            var flash = Particles(impactRoot.transform, "Star Flash", glint, 2, ParticleSystemSimulationSpace.World);
            Main(flash, new Vector2(.18f, .18f), new Vector2(0, 0), new Vector2(.42f, .5f));
            var fm = flash.main; fm.loop = false; fm.playOnAwake = false; fm.duration = .3f;
            SetColors(flash, Color.white, Gold, Color.white);
            e = flash.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var fs = flash.shape; fs.enabled = false;
            Flipbook(flash, true);
            var impact = PrefabUtility.SaveAsPrefabAsset(impactRoot, dir + "/Prefabs/Gemstone_Impact.prefab");
            Object.DestroyImmediate(impactRoot);

            string profilePath = dir + "/Gemstone.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PaddleSkinVfxProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<PaddleSkinVfxProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.skinId = "paddle.gemstone";
            profile.displayName = "Royal Court Sparkle";
            profile.trailMaterial = trail;
            profile.trailTime = .45f;
            profile.trailWidth = .13f;
            profile.trailWidthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.25f, .7f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Gold, .14f), new GradientColorKey(Emerald, .6f), new GradientColorKey(Emerald, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.95f, .3f), new GradientAlphaKey(.55f, .65f), new GradientAlphaKey(0, 1) });
            profile.trailColor = gradient;
            profile.ballEffect = ballFx;
            profile.impactEffect = impact;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Dragon Ruby: a red dragon leaves the paddle with the shot and chases the ball along a sine wave.</summary>
        static PaddleSkinVfxProfile BuildDragonRuby()
        {
            string dir = Root + "/PaddleSkins/DragonRuby";
            Directory.CreateDirectory(dir + "/Materials");
            Directory.CreateDirectory(dir + "/Prefabs");
            var fire = Texture(dir + "/Textures/T_DragonRuby_Fire.png", TextureWrapMode.Repeat);
            var flame = Texture(dir + "/Textures/T_DragonRuby_Flame.png", TextureWrapMode.Clamp);
            var ring = Texture(dir + "/Textures/T_DragonRuby_Ring.png", TextureWrapMode.Clamp);
            var emblem = Texture(dir + "/Textures/T_DragonRuby_Emblem.png", TextureWrapMode.Clamp);
            var halo = Texture(dir + "/Textures/T_DragonRuby_Glow.png", TextureWrapMode.Clamp);
            Color hot = new Color(1f, .86f, .55f), orange = new Color(1f, .42f, .10f), ruby = new Color(1f, .08f, .04f), goldLeaf = new Color(1f, .74f, .30f);

            var spirit = Material(dir + "/Materials/M_DragonRuby_Spirit.mat", "PickleVerse/VFX/Dragon Spirit");
            spirit.SetFloat("_Light", .85f);
            spirit.SetFloat("_Glow", 1.7f);
            spirit.SetColor("_Rim", new Color(2.3f, .30f, .04f));
            spirit.SetColor("_Burn", new Color(5f, 1.2f, .14f));
            var ribbon = Material(dir + "/Materials/M_DragonRuby_Ribbon.mat", "PickleVerse/VFX/Dragon Ribbon");
            ribbon.SetTexture("_BaseMap", fire);
            ribbon.SetTextureScale("_BaseMap", new Vector2(.75f, 1));
            ribbon.SetFloat("_Width", .20f);
            ribbon.SetFloat("_Sign", 1);
            ribbon.SetFloat("_Start", .30f);
            var counter = Material(dir + "/Materials/M_DragonRuby_CounterRibbon.mat", "PickleVerse/VFX/Dragon Ribbon");
            counter.SetTexture("_BaseMap", fire);
            counter.SetTextureScale("_BaseMap", new Vector2(1.1f, 1));
            counter.SetFloat("_Width", .06f);
            counter.SetFloat("_Sign", -1);
            counter.SetFloat("_Start", .10f);
            counter.SetColor("_Hot", new Color(3.2f, 2.3f, .9f));
            counter.SetColor("_Mid", new Color(2.0f, 1.05f, .22f));
            counter.SetColor("_Tail", new Color(.7f, .22f, .03f));
            var trail = Fire(dir + "/Materials/M_DragonRuby_Trail.mat", fire, 2.0f, .7f, .25f);
            trail.SetTextureScale("_BaseMap", new Vector2(2, 1));
            trail.SetFloat("_Scroll", 2.2f);
            var ember = Fire(dir + "/Materials/M_DragonRuby_Flame.mat", flame, 2.4f, .6f, .55f);
            var shock = Fire(dir + "/Materials/M_DragonRuby_Ring.mat", ring, 2.2f, .8f, .5f);
            var head = Fire(dir + "/Materials/M_DragonRuby_Emblem.mat", emblem, 1.7f, .85f, .15f);
            var glow = Fire(dir + "/Materials/M_DragonRuby_Glow.mat", halo, 2.6f, .25f, .75f);

            // Ball: the dragon, its burning sine curve and golden counter-wave, embers, and the pearl it chases.
            var ballRoot = new GameObject("DragonRuby_BallFx");
            var motion = ballRoot.AddComponent<DragonBallMotion>();
            var body = Visual(ballRoot.transform, "Dragon", DragonMesh(dir), spirit);
            var strip = StripMesh(dir + "/Models/DragonRibbon.asset", 96);
            var main = Visual(ballRoot.transform, "Sine Ribbon", strip, ribbon);
            var mirror = Visual(ballRoot.transform, "Counter Ribbon", strip, counter);
            motion.renderers = new Renderer[] { body, main, mirror };
            var aura = Particles(ballRoot.transform, "Pearl Glow", glow, 4, ParticleSystemSimulationSpace.Local);
            Main(aura, new Vector2(.5f, .5f), Vector2.zero, new Vector2(.36f, .44f));
            var am = aura.main; am.startColor = new Color(1f, .50f, .14f, .85f); am.startRotation = 0;
            var ae = aura.emission; ae.rateOverTime = 6;
            var ash = aura.shape; ash.enabled = false;
            Pulse(aura);
            var embers = Particles(ballRoot.transform, "Ball Embers", ember, 60, ParticleSystemSimulationSpace.World);
            Main(embers, new Vector2(.30f, .55f), new Vector2(0f, .25f), new Vector2(.05f, .12f));
            SetColors(embers, hot, orange, ruby);
            var e = embers.emission; e.rateOverTime = 0; e.rateOverDistance = 12;
            Sphere(embers, .04f);
            Flipbook(embers, true);
            Noise(embers, .25f);
            var riders = new System.Collections.Generic.List<Transform>();
            foreach (var distance in new[] { .75f, 1.65f })
            {
                var rider = new GameObject("Rider " + distance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).transform;
                rider.SetParent(ballRoot.transform, false);
                riders.Add(rider);
                var sparks = Particles(rider, "Body Embers", ember, 60, ParticleSystemSimulationSpace.World);
                Main(sparks, new Vector2(.4f, .8f), new Vector2(.05f, .4f), new Vector2(.03f, .09f));
                SetColors(sparks, goldLeaf, orange, ruby);
                var sm = sparks.main; sm.gravityModifier = -.06f;
                e = sparks.emission; e.rateOverTime = 0; e.rateOverDistance = 9;
                Sphere(sparks, .07f);
                Flipbook(sparks, true);
                Noise(sparks, .35f);
            }
            motion.riders = riders.ToArray();
            motion.riderDistances = new[] { .75f, 1.65f };
            var ballFx = PrefabUtility.SaveAsPrefabAsset(ballRoot, dir + "/Prefabs/DragonRuby_BallFx.prefab");
            Object.DestroyImmediate(ballRoot);

            // Contact: the dragon's head roars out of the paddle inside a ring of fire, with golden sparks.
            var impactRoot = new GameObject("DragonRuby_Impact");
            var sparksOut = Particles(impactRoot.transform, "Gold Sparks", ember, 26, ParticleSystemSimulationSpace.World);
            Main(sparksOut, new Vector2(.28f, .55f), new Vector2(1.4f, 3.4f), new Vector2(.05f, .12f));
            SetColors(sparksOut, hot, goldLeaf, orange);
            Once(sparksOut, 18, .6f);
            Sphere(sparksOut, .05f);
            var limit = sparksOut.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .22f; limit.limit = .4f;
            Flipbook(sparksOut, true);
            var shockwave = Particles(impactRoot.transform, "Fire Ring", shock, 2, ParticleSystemSimulationSpace.World);
            Main(shockwave, new Vector2(.34f, .34f), Vector2.zero, new Vector2(.95f, 1.05f));
            SetColors(shockwave, orange, orange, ruby);
            Once(shockwave, 1, .4f);
            Grow(shockwave, .15f, 1f);
            var roar = Particles(impactRoot.transform, "Dragon Roar", head, 2, ParticleSystemSimulationSpace.World);
            Main(roar, new Vector2(.42f, .42f), Vector2.zero, new Vector2(.62f, .62f));
            var rm = roar.main; rm.startRotation = 0; rm.startColor = Color.white;
            Once(roar, 1, .5f);
            Grow(roar, .45f, 1.15f);
            var impact = PrefabUtility.SaveAsPrefabAsset(impactRoot, dir + "/Prefabs/DragonRuby_Impact.prefab");
            Object.DestroyImmediate(impactRoot);

            string profilePath = dir + "/DragonRuby.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PaddleSkinVfxProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<PaddleSkinVfxProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.skinId = "paddle.dragonruby";
            profile.displayName = "Xích Long Truy Châu";
            profile.trailMaterial = trail;
            profile.trailTime = .10f;
            profile.trailWidth = .06f;
            profile.trailWidthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .6f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(hot, 0), new GradientColorKey(orange, .3f), new GradientColorKey(ruby, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) });
            profile.trailColor = gradient;
            profile.ballEffect = ballFx;
            profile.impactEffect = impact;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Blue Phoenix: a glowing blue phoenix bursts out of the paddle with wings spread and flies after the ball, its long
        /// tail trailing a blue fire ribbon and a ribbon of black smoke; black ash with blue embers falls from its wings.
        /// </summary>
        static PaddleSkinVfxProfile BuildBluePhoenix()
        {
            string dir = Root + "/PaddleSkins/BluePhoenix";
            Directory.CreateDirectory(dir + "/Materials");
            Directory.CreateDirectory(dir + "/Prefabs");
            var feathers = Texture(dir + "/Textures/T_BluePhoenix_Feathers.png", TextureWrapMode.Clamp);
            var featherImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(feathers));
            featherImporter.sRGBTexture = false;                      // brightness and coverage, not a colour
            featherImporter.mipMapsPreserveCoverage = true;           // feathers keep their shape far from the camera
            featherImporter.alphaTestReferenceValue = .42f;
            featherImporter.SaveAndReimport();
            var fire = Texture(dir + "/Textures/T_BluePhoenix_Fire.png", TextureWrapMode.Repeat);
            var flame = Texture(dir + "/Textures/T_BluePhoenix_Flame.png", TextureWrapMode.Clamp);
            var ashSheet = Texture(dir + "/Textures/T_BluePhoenix_Ash.png", TextureWrapMode.Clamp);
            var ring = Texture(dir + "/Textures/T_BluePhoenix_Ring.png", TextureWrapMode.Clamp);
            var emblem = Texture(dir + "/Textures/T_BluePhoenix_Emblem.png", TextureWrapMode.Clamp);
            var halo = Texture(dir + "/Textures/T_BluePhoenix_Glow.png", TextureWrapMode.Clamp);
            Color white = new Color(.86f, .96f, 1f), cyan = new Color(.35f, .82f, 1f), electric = new Color(.10f, .45f, 1f), deep = new Color(.04f, .16f, .85f);
            var whiteCore = new Vector4(.80f, .95f, 1f, 1);

            var spirit = Material(dir + "/Materials/M_BluePhoenix_Spirit.mat", "PickleVerse/VFX/Phoenix Spirit");
            spirit.SetTexture("_BaseMap", feathers);
            spirit.SetFloat("_Glow", 2.2f);
            spirit.SetColor("_Deep", new Color(.004f, .03f, .38f));
            spirit.SetColor("_Rim", new Color(.25f, 1.1f, 4.2f));
            spirit.SetFloat("_Flap", 34);
            spirit.SetFloat("_FlapSpeed", 12);
            spirit.SetFloat("_Dihedral", 10);
            spirit.SetFloat("_Roll", 0);
            spirit.SetFloat("_Scale", 1.3f);                    // wings about 2 m across, tail 2.5 m
            spirit.SetFloat("_AshReach", .38f);
            var fireRibbon = Material(dir + "/Materials/M_BluePhoenix_FireRibbon.mat", "PickleVerse/VFX/Dragon Ribbon");
            fireRibbon.SetTexture("_BaseMap", fire);
            fireRibbon.SetTextureScale("_BaseMap", new Vector2(.8f, 1));
            fireRibbon.SetFloat("_Width", .26f);
            fireRibbon.SetFloat("_Cover", .55f);
            fireRibbon.SetFloat("_Sign", 1);
            fireRibbon.SetFloat("_Start", .55f);
            fireRibbon.SetFloat("_Scroll", 1.8f);
            fireRibbon.SetColor("_Hot", new Color(1.8f, 3.8f, 8f));
            fireRibbon.SetColor("_Mid", new Color(.15f, 1.2f, 6.5f));
            fireRibbon.SetColor("_Tail", new Color(.02f, .18f, 2.4f));
            var smokeRibbon = Material(dir + "/Materials/M_BluePhoenix_SmokeRibbon.mat", "PickleVerse/VFX/Dragon Ribbon");
            smokeRibbon.SetTexture("_BaseMap", fire);
            smokeRibbon.SetTextureScale("_BaseMap", new Vector2(1.2f, 1));
            smokeRibbon.SetFloat("_Width", .15f);
            smokeRibbon.SetFloat("_Sign", -1);
            smokeRibbon.SetFloat("_Start", .40f);
            smokeRibbon.SetFloat("_Scroll", 1.1f);
            smokeRibbon.SetFloat("_Cover", .92f);
            smokeRibbon.SetColor("_Hot", new Color(.03f, .10f, .30f));
            smokeRibbon.SetColor("_Mid", new Color(.012f, .016f, .03f));
            smokeRibbon.SetColor("_Tail", new Color(.005f, .006f, .01f));
            var trail = Fire(dir + "/Materials/M_BluePhoenix_Trail.mat", fire, 2.2f, .55f, .45f);
            trail.SetTextureScale("_BaseMap", new Vector2(2, 1));
            trail.SetFloat("_Scroll", 2.4f);
            trail.SetVector("_HotColor", whiteCore);
            var sparkMat = Fire(dir + "/Materials/M_BluePhoenix_Flame.mat", flame, 2.6f, .45f, .55f);
            sparkMat.SetVector("_HotColor", whiteCore);
            var shock = Fire(dir + "/Materials/M_BluePhoenix_Ring.mat", ring, 2.4f, .55f, .5f);
            shock.SetVector("_HotColor", whiteCore);
            var cry = Fire(dir + "/Materials/M_BluePhoenix_Emblem.mat", emblem, 1.9f, .7f, .15f);
            cry.SetVector("_HotColor", whiteCore);
            var glow = Fire(dir + "/Materials/M_BluePhoenix_Glow.mat", halo, 2.6f, .2f, .75f);
            glow.SetVector("_HotColor", whiteCore);
            var ash = Material(dir + "/Materials/M_BluePhoenix_Ash.mat", "PickleVerse/VFX/Black Ash");
            ash.SetTexture("_BaseMap", ashSheet);
            ash.SetFloat("_Intensity", 4f);
            ash.SetFloat("_Cover", .9f);
            ash.SetFloat("_EmberRed", 1);
            ash.SetFloat("_EmberEdge", 0);
            var blackFlame = Material(dir + "/Materials/M_BluePhoenix_BlackFlame.mat", "PickleVerse/VFX/Black Ash");
            blackFlame.SetTexture("_BaseMap", flame);
            blackFlame.SetFloat("_Intensity", 3.2f);
            blackFlame.SetFloat("_Cover", .8f);
            blackFlame.SetFloat("_EmberRed", 0);
            blackFlame.SetFloat("_EmberEdge", 1);

            // Ball: the phoenix, its blue fire ribbon and black smoke ribbon, ash and embers, and the glowing ball.
            var ballRoot = new GameObject("BluePhoenix_BallFx");
            var motion = ballRoot.AddComponent<PhoenixBallMotion>();
            motion.amplitude = .07f;
            motion.wavelength = 1.7f;
            motion.ramp = .55f;
            motion.gap = .10f;
            motion.reach = 2.9f;
            motion.floor = .12f;
            motion.appear = .10f;
            motion.dissolve = .35f;
            var body = Visual(ballRoot.transform, "Phoenix", ImportedMesh(dir + "/Models/BluePhoenixSpirit.fbx", false), spirit);
            var strip = StripMesh(dir + "/Models/PhoenixRibbon.asset", 96);
            var main = Visual(ballRoot.transform, "Fire Ribbon", strip, fireRibbon);
            var smoke = Visual(ballRoot.transform, "Smoke Ribbon", strip, smokeRibbon);
            motion.renderers = new Renderer[] { body, main, smoke };
            var aura = Particles(ballRoot.transform, "Ball Glow", glow, 4, ParticleSystemSimulationSpace.Local);
            Main(aura, new Vector2(.5f, .5f), Vector2.zero, new Vector2(.32f, .40f));
            var am = aura.main; am.startColor = new Color(.35f, .7f, 1f, .85f); am.startRotation = 0;
            var ae = aura.emission; ae.rateOverTime = 6;
            var ash_ = aura.shape; ash_.enabled = false;
            Pulse(aura);
            var sparks = Particles(ballRoot.transform, "Ball Sparks", sparkMat, 50, ParticleSystemSimulationSpace.World);
            Main(sparks, new Vector2(.25f, .5f), new Vector2(0f, .2f), new Vector2(.04f, .10f));
            SetColors(sparks, white, cyan, electric);
            var e = sparks.emission; e.rateOverTime = 0; e.rateOverDistance = 10;
            Sphere(sparks, .04f);
            Flipbook(sparks, true);
            Noise(sparks, .2f);
            var riders = new System.Collections.Generic.List<Transform>();
            var distances = new[] { .34f, 1.2f, 2.05f };
            foreach (var distance in distances)
            {
                var rider = new GameObject("Rider " + distance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).transform;
                rider.SetParent(ballRoot.transform, false);
                riders.Add(rider);
                bool wings = distance < .5f;
                // black ash flakes with blue embers, tumbling and falling away from the wings and tail
                var flakes = Particles(rider, wings ? "Wing Ash" : "Tail Ash", ash, wings ? 60 : 40, ParticleSystemSimulationSpace.World);
                Main(flakes, new Vector2(.6f, 1.2f), new Vector2(.05f, .35f), new Vector2(.022f, .06f));
                SetColors(flakes, cyan, electric, white);
                var fm = flakes.main; fm.gravityModifier = .18f;
                e = flakes.emission; e.rateOverTime = 0; e.rateOverDistance = wings ? 16 : 8;
                Sphere(flakes, wings ? .55f : .25f);
                Ash(flakes);
                Noise(flakes, .35f);
                if (wings)
                {
                    // the blue light the phoenix sheds around itself, so it glows even where the bloom is weak
                    var light = Particles(rider, "Phoenix Light", glow, 12, ParticleSystemSimulationSpace.World);
                    Main(light, new Vector2(.22f, .32f), Vector2.zero, new Vector2(1.1f, 1.6f));
                    var lm = light.main; lm.startColor = new Color(.18f, .5f, 1f, .32f); lm.startRotation = 0;
                    e = light.emission; e.rateOverTime = 30; e.rateOverDistance = 0;
                    var ls = light.shape; ls.enabled = false;
                    Pulse(light);
                    light.GetComponent<ParticleSystemRenderer>().maxParticleSize = 1.5f;
                    var embers = Particles(rider, "Wing Embers", sparkMat, 50, ParticleSystemSimulationSpace.World);
                    Main(embers, new Vector2(.3f, .6f), new Vector2(.05f, .3f), new Vector2(.025f, .06f));
                    SetColors(embers, white, cyan, electric);
                    var em = embers.main; em.gravityModifier = -.05f;
                    e = embers.emission; e.rateOverTime = 0; e.rateOverDistance = 12;
                    Sphere(embers, .58f);
                    Flipbook(embers, true);
                    Noise(embers, .3f);
                }
                else
                {
                    // black fire licking off the tail
                    var dark = Particles(rider, "Black Flames", blackFlame, 36, ParticleSystemSimulationSpace.World);
                    Main(dark, new Vector2(.35f, .6f), new Vector2(.02f, .18f), new Vector2(.09f, .18f));
                    SetColors(dark, electric, cyan, deep);
                    var dm = dark.main; dm.gravityModifier = -.08f;
                    e = dark.emission; e.rateOverTime = 0; e.rateOverDistance = 7;
                    Sphere(dark, .14f);
                    Flipbook(dark, true);
                    Noise(dark, .2f);
                }
            }
            motion.riders = riders.ToArray();
            motion.riderDistances = distances;
            var ballFx = PrefabUtility.SaveAsPrefabAsset(ballRoot, dir + "/Prefabs/BluePhoenix_BallFx.prefab");
            Object.DestroyImmediate(ballRoot);

            // Contact: the phoenix cries out of the paddle inside a ring of blue fire, with blue sparks and a burst of ash.
            var impactRoot = new GameObject("BluePhoenix_Impact");
            var burst = Particles(impactRoot.transform, "Blue Sparks", sparkMat, 26, ParticleSystemSimulationSpace.World);
            Main(burst, new Vector2(.28f, .55f), new Vector2(1.4f, 3.4f), new Vector2(.05f, .12f));
            SetColors(burst, white, cyan, electric);
            Once(burst, 18, .6f);
            Sphere(burst, .05f);
            var limit = burst.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = .22f; limit.limit = .4f;
            Flipbook(burst, true);
            var shockwave = Particles(impactRoot.transform, "Fire Ring", shock, 2, ParticleSystemSimulationSpace.World);
            Main(shockwave, new Vector2(.36f, .36f), Vector2.zero, new Vector2(.95f, 1.05f));
            SetColors(shockwave, cyan, cyan, electric);
            Once(shockwave, 1, .4f);
            Grow(shockwave, .15f, 1f);
            var call = Particles(impactRoot.transform, "Phoenix Cry", cry, 2, ParticleSystemSimulationSpace.World);
            Main(call, new Vector2(.5f, .5f), Vector2.zero, new Vector2(.80f, .80f));
            var cm = call.main; cm.startRotation = 0; cm.startColor = Color.white;
            Once(call, 1, .55f);
            Grow(call, .4f, 1.2f);
            var ashBurst = Particles(impactRoot.transform, "Ash Burst", ash, 24, ParticleSystemSimulationSpace.World);
            Main(ashBurst, new Vector2(.7f, 1.3f), new Vector2(.5f, 1.6f), new Vector2(.03f, .07f));
            SetColors(ashBurst, cyan, electric, white);
            var abm = ashBurst.main; abm.gravityModifier = .25f;
            Once(ashBurst, 16, .9f);
            Sphere(ashBurst, .08f);
            var al = ashBurst.limitVelocityOverLifetime; al.enabled = true; al.dampen = .25f; al.limit = .35f;
            Ash(ashBurst);
            var impact = PrefabUtility.SaveAsPrefabAsset(impactRoot, dir + "/Prefabs/BluePhoenix_Impact.prefab");
            Object.DestroyImmediate(impactRoot);

            string profilePath = dir + "/BluePhoenix.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PaddleSkinVfxProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<PaddleSkinVfxProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            profile.skinId = "paddle.bluephoenix";
            profile.displayName = "Lam Phượng Tung Cánh";
            profile.trailMaterial = trail;
            profile.trailTime = .12f;
            profile.trailWidth = .07f;
            profile.trailWidthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.3f, .6f), new Keyframe(1, 0));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(white, 0), new GradientColorKey(cyan, .3f), new GradientColorKey(electric, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) });
            profile.trailColor = gradient;
            profile.ballEffect = ballFx;
            profile.impactEffect = impact;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>4x4 sheet of ash flakes: each particle keeps one random flake and tumbles; its embers fade with it.</summary>
        static void Ash(ParticleSystem ps)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 15);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(.5f, .5f, .5f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.85f, .6f), new GradientAlphaKey(0, 1) });
            color.color = g;
        }

        static Material Fire(string path, Texture2D sprite, float intensity, float cover, float hot)
        {
            var m = Material(path, "PickleVerse/VFX/Dragon Fire");
            m.SetTexture("_BaseMap", sprite);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_Cover", cover);
            m.SetFloat("_Hot", hot);
            return m;
        }

        static void Pulse(ParticleSystem ps)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.55f, .5f), new GradientAlphaKey(0, 1) });
            color.color = g;
        }

        static MeshRenderer Visual(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            Plain(renderer, material);
            renderer.enabled = false;
            return renderer;
        }

        static void Plain(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static void Once(ParticleSystem ps, short burst, float duration)
        {
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = duration;
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, burst) });
            var shape = ps.shape; shape.enabled = burst > 1;
        }

        static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, from, 0, 3), new Keyframe(.55f, to * .92f), new Keyframe(1, to)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            color.color = g;
            ps.GetComponent<ParticleSystemRenderer>().maxParticleSize = .6f;
        }

        /// <summary>The straight dragon from ArtSource/VFX/DragonRuby (x = distance behind the ball, y = back, z = side).</summary>
        static Mesh DragonMesh(string dir) => ImportedMesh(dir + "/Models/DragonRubySpirit.fbx", true);

        /// <summary>
        /// A creature mesh bent by its shader: no materials, animation or tangents. The phoenix keeps every vertex as authored
        /// (its second UV channel and the shared vertices of its feather cards must not be welded or reordered).
        /// </summary>
        static Mesh ImportedMesh(string path, bool optimise)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = importer.importLights = false;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            if (!optimise)
            {
                importer.weldVertices = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().First();
        }

        /// <summary>Flat strip for the ribbons: x = 0..1 along the path, y = -1..1 across.</summary>
        static Mesh StripMesh(string path, int segments)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                vertices[i * 2] = new Vector3(i / (float)segments, -1, 0);
                vertices[i * 2 + 1] = new Vector3(i / (float)segments, 1, 0);
                if (i == segments)
                    break;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Texture2D Texture(string path, TextureWrapMode wrap)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = wrap;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string path, string shader)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find(shader);
            EditorUtility.SetDirty(m);
            return m;
        }

        static ParticleSystem Particles(Transform parent, string name, Material material, int max, ParticleSystemSimulationSpace space)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.playOnAwake = true;
            main.loop = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = .2f;
            return ps;
        }

        static void Main(ParticleSystem ps, Vector2 life, Vector2 speed, Vector2 size)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        }

        static void SetColors(ParticleSystem ps, Color a, Color b, Color c)
        {
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(new[] { new GradientColorKey(a, .45f), new GradientColorKey(b, .8f), new GradientColorKey(c, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        }

        static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        static void Noise(ParticleSystem ps, float strength)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = .8f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        /// <summary>4x4 star-glint sheet: play the twinkle over the lifetime, or hold one frame.</summary>
        static void Flipbook(ParticleSystem ps, bool animate, int frame = 0)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.cycleCount = 1;
            sheet.frameOverTime = animate ? new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1)) : new ParticleSystem.MinMaxCurve(frame / 16f);
        }
    }
}
