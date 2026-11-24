using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Builds the Art Bible Looks (Doc/ArtBible/03-Lighting-Color.md) as Volume profiles plus the scene → Look table,
    /// and installs SSAO on the PC renderer. Values live here so the look is reproducible and reviewable in code.
    /// Menu: PickleVerse/Art Direction/Rebuild looks.
    /// </summary>
    public static class ArtDirectionLookBuilder
    {
        public const string Folder = "Assets/Settings/ArtDirection";
        const string LooksPath = "Assets/Resources/ArtDirection/ArtDirectionLooks.asset";
        const string PcRenderer = "Assets/Settings/PC_Renderer.asset";

        struct Grade
        {
            public float exposure, contrast, saturation, temperature, tint;
            public Vector4 lift, gamma, gain;
            public float bloomThreshold, bloomIntensity, bloomScatter;
            public Color bloomTint;
            public float vignette, vignetteSmooth;
        }

        [MenuItem("PickleVerse/Art Direction/Rebuild looks")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Path.GetDirectoryName(LooksPath));

            // Golden hour: warm key, cool fill; gentle S-curve, warm whites, cool lifted shadows.
            var golden = Profile("Look_GoldenHour", new Grade
            {
                exposure = .22f, contrast = 12, saturation = 6, temperature = 7, tint = 2,
                lift = new Vector4(.97f, .99f, 1.04f, 0f), gamma = new Vector4(1f, 1f, 1f, 0f), gain = new Vector4(1.04f, 1.0f, .95f, 0f),
                bloomThreshold = 1.05f, bloomIntensity = .28f, bloomScatter = .62f, bloomTint = new Color(1f, .9f, .78f),
                vignette = .20f, vignetteSmooth = .45f,
            });
            // Night: floodlit court, moonlit blue ambient, lamps bloom softly.
            var night = Profile("Look_Night", new Grade
            {
                exposure = .20f, contrast = 14, saturation = -4, temperature = -5, tint = 0,
                lift = new Vector4(.95f, .98f, 1.07f, 0f), gamma = new Vector4(1f, 1f, 1.02f, 0f), gain = new Vector4(1.02f, 1.0f, .97f, 0f),
                bloomThreshold = .95f, bloomIntensity = .45f, bloomScatter = .68f, bloomTint = new Color(1f, .88f, .72f),
                vignette = .26f, vignetteSmooth = .5f,
            });
            // Academy arena: warm skylight on wood and court, calmer greens, stands recede in cool shade.
            var arena = Profile("Look_Arena", new Grade
            {
                exposure = .14f, contrast = 14, saturation = -4, temperature = 8, tint = 1,
                lift = new Vector4(.96f, .985f, 1.05f, 0f), gamma = new Vector4(1f, 1f, 1f, 0f), gain = new Vector4(1.05f, 1.0f, .94f, 0f),
                bloomThreshold = 1.05f, bloomIntensity = .25f, bloomScatter = .6f, bloomTint = new Color(1f, .9f, .78f),
                vignette = .22f, vignetteSmooth = .45f,
            });
            // Showroom: studio key/fill/rim, jewel-like products, brand navy background.
            var showroom = Profile("Look_Showroom", new Grade
            {
                exposure = 0f, contrast = 10, saturation = 4, temperature = 3, tint = 0,
                lift = new Vector4(.97f, .99f, 1.04f, 0f), gamma = new Vector4(1f, 1f, 1f, 0f), gain = new Vector4(1.03f, 1.0f, .97f, 0f),
                bloomThreshold = .95f, bloomIntensity = .35f, bloomScatter = .55f, bloomTint = new Color(1f, .93f, .82f),
                vignette = .28f, vignetteSmooth = .5f,
            });
            var cinematic = CinematicProfile();
            var low = LowQualityProfile();

            var looks = AssetDatabase.LoadAssetAtPath<ArtDirectionLooks>(LooksPath);
            if (!looks)
            {
                looks = ScriptableObject.CreateInstance<ArtDirectionLooks>();
                AssetDatabase.CreateAsset(looks, LooksPath);
            }
            looks.looks = new[]
            {
                new ArtDirectionLooks.Look
                {
                    name = "Sunset District", scenes = new[] { "Menu", "BotMatch", "MultiplayerCourt" },
                    profile = golden, nightProfile = night, postProcess = true,
                    shadowTint = new Color(.66f, .76f, 1f, .38f), rimTint = new Color(1f, .88f, .74f, 1f),
                    ambientBoost = new Color(1f, 1f, 1f), wind = new Vector4(.91f, .41f, 1f, 1f), wetness = 0,
                },
                new ArtDirectionLooks.Look
                {
                    name = "Academy Arena", scenes = new[] { "Training", "Tutorial", "Replay" },
                    profile = arena, postProcess = true,
                    shadowTint = new Color(.68f, .78f, 1f, .32f), rimTint = new Color(1f, .9f, .78f, .9f),
                    ambientBoost = new Color(1f, 1f, 1f), wind = new Vector4(.7f, .7f, .35f, .6f), wetness = 0,
                },
                new ArtDirectionLooks.Look
                {
                    name = "Showroom", scenes = new[] { "Shop", "Collection" },
                    profile = showroom, postProcess = true,
                    shadowTint = new Color(.7f, .8f, 1f, .25f), rimTint = new Color(1f, .92f, .82f, 1.1f),
                    ambientBoost = new Color(1f, 1f, 1f), wind = Vector4.zero, wetness = 0,
                },
            };
            looks.cinematicProfile = cinematic;
            looks.lowQualityProfile = low;
            var pc = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(PcRenderer);
            looks.ssaoRenderers = pc ? new[] { pc } : new ScriptableRendererData[0];
            EditorUtility.SetDirty(looks);
            InstallSsao(pc);
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtDirection] Looks rebuilt: " + string.Join(", ", looks.looks.Select(l => l.name)));
        }

        static VolumeProfile LoadOrCreate(string name)
        {
            string path = Folder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var component in profile.components.ToArray())
            {
                profile.components.Remove(component);
                Object.DestroyImmediate(component, true);
            }
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static VolumeProfile Profile(string name, Grade g)
        {
            var p = LoadOrCreate(name);
            var tone = Add<Tonemapping>(p);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = Add<ColorAdjustments>(p);
            color.postExposure.Override(g.exposure);
            color.contrast.Override(g.contrast);
            color.saturation.Override(g.saturation);
            var wb = Add<WhiteBalance>(p);
            wb.temperature.Override(g.temperature);
            wb.tint.Override(g.tint);
            var lgg = Add<LiftGammaGain>(p);
            lgg.lift.Override(g.lift);
            lgg.gamma.Override(g.gamma);
            lgg.gain.Override(g.gain);
            var bloom = Add<Bloom>(p);
            bloom.threshold.Override(g.bloomThreshold);
            bloom.intensity.Override(g.bloomIntensity);
            bloom.scatter.Override(g.bloomScatter);
            bloom.tint.Override(g.bloomTint);
            bloom.highQualityFiltering.Override(false);
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(6);
            var vignette = Add<Vignette>(p);
            vignette.color.Override(new Color(.03f, .05f, .09f));
            vignette.intensity.Override(g.vignette);
            vignette.smoothness.Override(g.vignetteSmooth);
            vignette.rounded.Override(false);
            EditorUtility.SetDirty(p);
            return p;
        }

        static VolumeProfile CinematicProfile()
        {
            var p = LoadOrCreate("Look_Cinematic");
            var dof = Add<DepthOfField>(p);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(8f);
            dof.gaussianEnd.Override(30f);
            dof.gaussianMaxRadius.Override(1.1f);
            dof.highQualitySampling.Override(false);
            var vignette = Add<Vignette>(p);
            vignette.color.Override(new Color(.03f, .05f, .09f));
            vignette.intensity.Override(.3f);
            vignette.smoothness.Override(.5f);
            EditorUtility.SetDirty(p);
            return p;
        }

        static VolumeProfile LowQualityProfile()
        {
            var p = LoadOrCreate("Look_QualityLow");
            var bloom = Add<Bloom>(p);
            bloom.intensity.Override(0f);
            var vignette = Add<Vignette>(p);
            vignette.intensity.Override(0f);
            var dof = Add<DepthOfField>(p);
            dof.mode.Override(DepthOfFieldMode.Off);
            EditorUtility.SetDirty(p);
            return p;
        }

        /// <summary>
        /// Soft contact shadows: SSAO after opaques from the depth texture (no DepthNormals prepass), half resolution,
        /// small radius and moderate intensity so it grounds props and players without a dirty halo.
        /// </summary>
        static void InstallSsao(ScriptableRendererData data)
        {
            if (!data)
                return;
            var feature = data.rendererFeatures.FirstOrDefault(f => f && f.GetType() == typeof(ScreenSpaceAmbientOcclusion));
            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "Art Direction SSAO";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var fs = new SerializedObject(feature);
            fs.FindProperty("m_Settings.AfterOpaque").boolValue = true;
            fs.FindProperty("m_Settings.Source").enumValueIndex = 0;          // Depth (no DepthNormals prepass)
            fs.FindProperty("m_Settings.NormalSamples").enumValueIndex = 1;   // Medium reconstruction
            fs.FindProperty("m_Settings.Downsample").boolValue = true;
            fs.FindProperty("m_Settings.Intensity").floatValue = .55f;
            fs.FindProperty("m_Settings.DirectLightingStrength").floatValue = .2f;
            fs.FindProperty("m_Settings.Radius").floatValue = .3f;
            fs.FindProperty("m_Settings.Falloff").floatValue = 45f;
            fs.FindProperty("m_Settings.Samples").enumValueIndex = 1;         // Medium (8)
            fs.FindProperty("m_Settings.BlurQuality").enumValueIndex = 1;     // Gaussian
            fs.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;        // Interleaved gradient (stable)
            fs.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
    }
}
