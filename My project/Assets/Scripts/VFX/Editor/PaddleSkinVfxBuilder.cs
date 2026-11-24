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
            var gemstone = BuildGemstone();
            var library = AssetDatabase.LoadAssetAtPath<PaddleSkinVfxLibrary>(LibraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<PaddleSkinVfxLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.profiles = library.profiles.Where(p => p && p.skinId != gemstone.skinId).Append(gemstone).ToArray();
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
