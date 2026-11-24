using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Applies the Art Bible material standards (Doc/ArtBible/02-Material-Language.md): every opaque 3D surface uses
    /// PickleVerse/Stylized Lit with one of the five standard sets (Court, Metal, Wood, Stone, Fabric) or the Character
    /// preset; hue comes from the palette (vertex colour or _BaseColor), value/detail from the set textures.
    /// Menu: PickleVerse/Art Direction/Apply material standards. Idempotent; logs a report.
    /// </summary>
    public static class ArtDirectionMaterialStandards
    {
        public const string ShaderName = "PickleVerse/Stylized Lit";
        const string Tex = "Assets/Textures/ArtDirection";
        public const string MaterialRoot = "Assets/Materials/ArtDirection";
        const string Sunset = "Assets/EnvironmentContent/SunsetDistrict/Materials/";
        const string Stadiums = "Assets/LocalMatchContent/Stadiums/";
        const string Arena = "Assets/LocalMatchContent/StadiumEnhanced/Materials/";
        const string Audience = "Assets/LocalMatchContent/Audience/Materials/";

        public enum Set { None, Court, Metal, Wood, Stone, Fabric }

        // Palette (Art Bible): sRGB hex.
        public static readonly Color CourtBlue = Hex("2F6CAE"), Kitchen = Hex("285E9C"), Surround = Hex("2B5F6B"),
            LineWhite = Hex("EFEBE1"), RimWarm = Hex("FFD39C");

        public sealed class Spec
        {
            public string path;
            public Set set;
            public bool setBase = true, setNormal = true, setMask = true;   // replace textures with the set's
            public float smoothness = .4f, metallic, normalScale = .6f;
            public float wrap = .25f, rim = .12f, rimPower = 4.5f, edge = .25f, shadowTint = 1f;
            public float noiseAlbedo, noiseSmooth, detail;
            public bool vertexTint, court, wind, emission, wet;
            public Color? baseColor, emissionColor;
            public Color surround = new Color(0, 0, 0, 0);
            public float gradient;
        }

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        public static IEnumerable<Spec> Specs()
        {
            // ---- Sunset District venue (Menu, BotMatch, MultiplayerCourt): vertex-tinted, palette from the GLB colours.
            yield return new Spec { path = Sunset + "SD_Acrylic.mat", set = Set.Court, vertexTint = true, court = true, smoothness = .55f, normalScale = .5f, wrap = .18f, rim = .06f, edge = .05f, noiseAlbedo = .035f, noiseSmooth = .05f, surround = new Color(Surround.r, Surround.g, Surround.b, 1f), wet = true };
            yield return new Spec { path = Sunset + "SD_Paint.mat", set = Set.Metal, vertexTint = true, smoothness = .62f, metallic = 0f, normalScale = .35f, edge = .35f, rim = .14f };
            yield return new Spec { path = Sunset + "SD_Metal.mat", set = Set.Metal, vertexTint = true, smoothness = .74f, metallic = .85f, normalScale = .4f, edge = .4f, rim = .1f };
            yield return new Spec { path = Sunset + "SD_Wood.mat", set = Set.Wood, vertexTint = true, smoothness = .55f, normalScale = .7f, edge = .35f, rim = .14f, wrap = .28f };
            yield return new Spec { path = Sunset + "SD_Concrete.mat", set = Set.Stone, vertexTint = true, smoothness = .5f, normalScale = .6f, edge = .2f, detail = .35f, noiseAlbedo = .05f, wet = true };
            yield return new Spec { path = Sunset + "SD_Paving.mat", set = Set.Stone, vertexTint = true, setBase = false, setNormal = false, smoothness = .6f, edge = .15f, detail = .3f, noiseAlbedo = .05f, noiseSmooth = .06f, wet = true };
            yield return new Spec { path = Sunset + "SD_Asphalt.mat", set = Set.Stone, vertexTint = true, setBase = false, setNormal = false, smoothness = .55f, edge = .05f, detail = .3f, noiseAlbedo = .06f, noiseSmooth = .08f, wet = true };
            yield return new Spec { path = Sunset + "SD_Net.mat", set = Set.Fabric, vertexTint = true, setBase = false, setNormal = false, setMask = false, smoothness = .3f, wrap = .35f, rim = .1f, edge = 0f };
            yield return new Spec { path = Sunset + "SD_ChainLink.mat", set = Set.Metal, vertexTint = true, setBase = false, setNormal = false, setMask = false, smoothness = .5f, wrap = .3f, rim = .08f, edge = 0f };
            yield return new Spec { path = Sunset + "SD_Facade.mat", set = Set.Stone, vertexTint = true, setBase = false, setNormal = false, setMask = false, smoothness = .35f, wrap = .22f, rim = .05f, edge = .12f, noiseAlbedo = .06f, detail = .2f };
            yield return new Spec { path = Sunset + "SD_Grass.mat", set = Set.None, vertexTint = true, setBase = false, setNormal = false, setMask = false, smoothness = .12f, wrap = .4f, rim = .05f, edge = 0f, noiseAlbedo = .1f, detail = .35f };
            yield return new Spec { path = Sunset + "SD_Skyline.mat", set = Set.None, vertexTint = true, setBase = false, setNormal = false, setMask = false };
            yield return new Spec { path = Sunset + "SD_Glow.mat", set = Set.None, vertexTint = true, setBase = false, setNormal = false, setMask = false, edge = 0f, rim = 0f };
            yield return new Spec { path = Sunset + "SD_Water.mat", set = Set.None, vertexTint = true, setBase = false, setNormal = false, setMask = false, edge = 0f, rim = .1f };
            // ---- Shared venue dressing (both outdoor venues).
            yield return new Spec { path = Stadiums + "Surroundings.mat", set = Set.Court, baseColor = Surround, smoothness = .5f, normalScale = .4f, edge = .05f, rim = .06f, noiseAlbedo = .04f };
            yield return new Spec { path = Stadiums + "Refined_Venue_Signage.mat", set = Set.Fabric, setBase = false, smoothness = .35f, normalScale = .25f, edge = .1f, rim = .1f };
            yield return new Spec { path = Stadiums + "LED_White.mat", set = Set.Metal, setBase = false, smoothness = .7f, normalScale = .2f, edge = .2f, rim = .05f };
            // ---- Academy arena (Training, Tutorial, Replay): palette colours on the standard sets.
            yield return new Spec { path = Arena + "EN_CourtCompetition.mat", set = Set.Court, baseColor = CourtBlue, court = true, smoothness = .55f, normalScale = .5f, wrap = .18f, rim = .06f, edge = .05f, noiseAlbedo = .035f, noiseSmooth = .05f };
            yield return new Spec { path = Arena + "EN_CourtNVZ.mat", set = Set.Court, baseColor = Kitchen, court = true, smoothness = .58f, normalScale = .5f, wrap = .18f, rim = .06f, edge = .05f, noiseAlbedo = .035f, noiseSmooth = .05f };
            yield return new Spec { path = Arena + "EN_Runoff.mat", set = Set.Court, baseColor = Surround, smoothness = .5f, normalScale = .5f, wrap = .18f, rim = .06f, edge = .05f, noiseAlbedo = .045f, noiseSmooth = .05f };
            yield return new Spec { path = Arena + "EN_CourtLines.mat", set = Set.Court, baseColor = LineWhite, smoothness = .5f, normalScale = .3f, edge = .05f, rim = .05f };
            yield return new Spec { path = Arena + "EN_PlazaStone.mat", set = Set.Stone, setBase = false, smoothness = .5f, normalScale = .5f, edge = .2f, detail = .3f, noiseAlbedo = .05f };
            yield return new Spec { path = Arena + "EN_Concrete.mat", set = Set.Stone, setBase = false, smoothness = .45f, normalScale = .6f, edge = .25f, detail = .3f, noiseAlbedo = .05f };
            yield return new Spec { path = Arena + "EN_Terracotta.mat", set = Set.Stone, setBase = false, smoothness = .35f, normalScale = .5f, edge = .3f, rim = .12f };
            yield return new Spec { path = Arena + "EN_IvoryMembrane.mat", set = Set.Fabric, setBase = false, smoothness = .35f, normalScale = .3f, wrap = .4f, edge = .05f, rim = .05f };
            yield return new Spec { path = Arena + "EN_SportsBanner.mat", set = Set.Fabric, setBase = false, smoothness = .35f, normalScale = .3f, edge = .1f, rim = .1f };
            yield return new Spec { path = Arena + "EN_VietnamSatin.mat", set = Set.Fabric, setBase = false, smoothness = .62f, normalScale = .3f, wrap = .35f, edge = .05f, rim = .12f, wind = true };
            yield return new Spec { path = Arena + "EN_PlantLeaves.mat", set = Set.None, setBase = false, setNormal = false, setMask = false, smoothness = .25f, wrap = .45f, edge = 0f, rim = .18f, wind = true };
            yield return new Spec { path = Arena + "EN_BlackStructure.mat", set = Set.Metal, setBase = false, smoothness = .58f, normalScale = .35f, edge = .35f, rim = .12f };
            yield return new Spec { path = Arena + "EN_Stainless.mat", set = Set.Metal, setBase = false, smoothness = .74f, metallic = .9f, normalScale = .4f, edge = .4f, rim = .1f };
            yield return new Spec { path = Arena + "EN_AccentGold.mat", set = Set.Metal, setBase = false, smoothness = .72f, metallic = .9f, normalScale = .3f, edge = .4f, rim = .12f };
            yield return new Spec { path = Arena + "EN_BlueSeats.mat", set = Set.Fabric, setBase = false, smoothness = .6f, normalScale = .15f, edge = .3f, rim = .12f, wrap = .3f };
            yield return new Spec { path = Stadiums + "Professional_Concrete.mat", set = Set.Stone, setBase = false, smoothness = .45f, normalScale = .5f, edge = .2f, noiseAlbedo = .05f };
            yield return new Spec { path = Stadiums + "Professional_Rail_Black.mat", set = Set.Metal, setBase = false, smoothness = .6f, normalScale = .3f, edge = .35f, rim = .12f };
            yield return new Spec { path = Stadiums + "Professional_Seat_Blue.mat", set = Set.Fabric, setBase = false, smoothness = .6f, normalScale = .15f, edge = .3f, rim = .12f, wrap = .3f };
            yield return new Spec { path = Stadiums + "Professional_Silver.mat", set = Set.Metal, setBase = false, smoothness = .74f, metallic = .9f, normalScale = .4f, edge = .4f, rim = .1f };
            yield return new Spec { path = Arena + "LED Off.mat", set = Set.Metal, setBase = false, smoothness = .6f, metallic = .2f, normalScale = .2f, edge = .2f, rim = .05f };
            yield return new Spec { path = Arena + "LED On.mat", set = Set.Metal, setBase = false, smoothness = .6f, metallic = .2f, normalScale = .2f, edge = .2f, rim = .05f, emission = true };
            // ---- Seated audience (Character preset): soft wrap, warm rim, no edge highlight on skin.
            foreach (var name in new[] { "PlayBoy-Material_0", "Wibu-Material_0", "T1-girl-BakedMaterial", "YellowHairGirl-Material_0.001" })
                yield return Character(Audience + name + ".mat");
        }

        public static Spec Character(string path) => new Spec
        {
            path = path, set = Set.None, setBase = false, setNormal = false, setMask = false,
            smoothness = .32f, wrap = .38f, rim = .32f, rimPower = 3.5f, edge = .04f, shadowTint = .9f,
        };

        [MenuItem("PickleVerse/Art Direction/Apply material standards")]
        public static string Apply()
        {
            ConfigureTextures();
            var report = new StringBuilder();
            CreateReferenceMaterials(report);
            var shader = Shader.Find(ShaderName);
            foreach (var spec in Specs())
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(spec.path);
                if (!m) { report.AppendLine("missing " + spec.path); continue; }
                Convert(m, shader, spec);
                report.AppendLine("converted " + m.name + " -> " + spec.set);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ArtDirection] Material standards\n" + report);
            return report.ToString();
        }

        public static Texture2D T(Set set, string map) => set == Set.None ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{Tex}/{set}/T_{set}_{map}.png");

        public static void Convert(Material m, Shader shader, Spec s)
        {
            // Read the URP / Sunset values that carry over before the shader switch.
            // glTFast shader-graph materials name their inputs after glTF (baseColorFactor, baseColorTexture, ...).
            bool gltf = m.HasProperty("baseColorFactor");
            var baseTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : gltf && m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
            var baseSt = m.HasProperty("_BaseMap") ? m.GetVector("_BaseMap_ST") : new Vector4(1, 1, 0, 0);
            var baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : gltf ? m.GetColor("baseColorFactor") : Color.white;
            var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : gltf && m.HasProperty("normalTexture") ? m.GetTexture("normalTexture") : null;
            var emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            var emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            bool clip = m.IsKeywordEnabled("_ALPHATEST_ON") || m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > .5f;
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : .5f;
            float cull = m.HasProperty("_Cull") ? m.GetFloat("_Cull") : 2;
            bool wasStylized = m.shader == shader;
            if (!wasStylized)
                m.shader = shader;

            m.SetFloat("_VertexTint", s.vertexTint ? 1 : 0);
            m.SetColor("_BaseColor", s.baseColor ?? (s.vertexTint ? Color.white : baseColor));
            var setBase = s.setBase ? T(s.set, "BaseColor") : null;
            m.SetTexture("_BaseMap", setBase ? setBase : baseTex);
            m.SetTextureScale("_BaseMap", new Vector2(baseSt.x, baseSt.y));
            m.SetTextureOffset("_BaseMap", new Vector2(baseSt.z, baseSt.w));
            var normal = s.setNormal ? T(s.set, "Normal") : bump;
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", s.setNormal ? s.normalScale : (m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1));
            Toggle(m, "_UseNormalMap", "_NORMALMAP", normal);
            var mask = s.setMask ? T(s.set, "Mask") : null;
            m.SetTexture("_MaskMap", mask);
            Toggle(m, "_UseMaskMap", "_MASKMAP", mask);
            // With a mask the smoothness channel (A ~0.7) scales the material value: keep the authored look.
            m.SetFloat("_Smoothness", mask ? Mathf.Clamp01(s.smoothness / .7f) : s.smoothness);
            m.SetFloat("_Metallic", s.metallic);
            m.SetVector("_SmoothnessRange", new Vector4(.04f, .88f, 0, 0));
            m.SetFloat("_OcclusionStrength", 1);

            bool emissive = s.emission || emission.maxColorComponent > .01f;
            m.SetColor("_EmissionColor", s.emissionColor ?? (emissive ? emission : Color.black));
            m.SetTexture("_EmissionMap", emissionMap ? emissionMap : Texture2D.whiteTexture);
            Toggle(m, "_UseEmission", "_EMISSION", emissive);
            if (emissive) m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            m.SetFloat("_Wrap", s.wrap);
            m.SetFloat("_ShadowTintStrength", s.shadowTint);
            m.SetColor("_RimColor", RimWarm);
            m.SetFloat("_RimStrength", s.rim);
            m.SetFloat("_RimPower", s.rimPower);
            m.SetFloat("_EdgeHighlight", s.edge);
            m.SetFloat("_SpecularScale", 1);
            m.SetFloat("_GradientStrength", s.gradient);
            m.SetFloat("_NoiseAlbedo", s.noiseAlbedo);
            m.SetFloat("_NoiseSmoothness", s.noiseSmooth);
            m.SetFloat("_NoiseScale", .15f);
            var detail = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/T_WorldDetail.png");
            m.SetTexture("_DetailMap", detail);
            m.SetFloat("_DetailScale", 7f);
            m.SetFloat("_DetailStrength", s.detail);
            Toggle(m, "_UseWorldDetail", "_WORLD_DETAIL", s.detail > 0 && detail);
            Toggle(m, "_UseWetness", "_WETNESS", s.wet);
            m.SetFloat("_WetResponse", 1);
            Toggle(m, "_UseWind", "_WIND", s.wind);
            m.SetFloat("_WindWeight", s.wind ? .06f : 0);
            Toggle(m, "_UseCourt", "_COURT", s.court);
            m.SetVector("_CourtParams", new Vector4(0, 0, 3.05f, 6.705f));
            m.SetFloat("_KitchenDepth", 2.134f);
            m.SetColor("_KitchenTint", new Color(.93f, .95f, 1f));
            m.SetFloat("_CourtWear", .55f);
            m.SetFloat("_CourtGrain", .35f);
            m.SetColor("_SurroundTint", s.surround);
            m.SetVector("_SurroundExtent", new Vector4(9.25f, 13.2f, 0, 0));
            if (!wasStylized)
            {
                m.SetFloat("_Cull", cull);
                m.SetFloat("_Cutoff", cutoff);
                Toggle(m, "_AlphaTest", "_ALPHATEST_ON", clip);
            }
            EditorUtility.SetDirty(m);
        }

        static void Toggle(Material m, string property, string keyword, Object on) => Toggle(m, property, keyword, on != null);

        static void Toggle(Material m, string property, string keyword, bool on)
        {
            m.SetFloat(property, on ? 1 : 0);
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }

        /// <summary>The five standard sets as standalone reference materials (Art Bible swatches, new assets start here).</summary>
        static void CreateReferenceMaterials(StringBuilder report)
        {
            Directory.CreateDirectory(MaterialRoot);
            var shader = Shader.Find(ShaderName);
            var refs = new[]
            {
                new Spec { path = MaterialRoot + "/M_Court_Acrylic.mat", set = Set.Court, baseColor = CourtBlue, court = true, smoothness = .55f, normalScale = .5f, wrap = .18f, rim = .06f, edge = .05f, noiseAlbedo = .035f, noiseSmooth = .05f },
                new Spec { path = MaterialRoot + "/M_Metal_Coated.mat", set = Set.Metal, baseColor = Hex("2D343C"), smoothness = .62f, normalScale = .35f, edge = .35f, rim = .14f },
                new Spec { path = MaterialRoot + "/M_Metal_Bare.mat", set = Set.Metal, baseColor = Hex("C9CDD2"), smoothness = .74f, metallic = .9f, normalScale = .4f, edge = .4f, rim = .1f },
                new Spec { path = MaterialRoot + "/M_Wood_Oiled.mat", set = Set.Wood, baseColor = Hex("8A5B36"), smoothness = .55f, normalScale = .7f, edge = .35f, rim = .14f, wrap = .28f },
                new Spec { path = MaterialRoot + "/M_Stone_Concrete.mat", set = Set.Stone, baseColor = Hex("B79D82"), smoothness = .5f, normalScale = .6f, edge = .2f, detail = .35f, noiseAlbedo = .05f, wet = true },
                new Spec { path = MaterialRoot + "/M_Fabric_Banner.mat", set = Set.Fabric, baseColor = Hex("1B2B46"), smoothness = .35f, normalScale = .3f, edge = .1f, rim = .1f },
                new Spec { path = MaterialRoot + "/M_Fabric_Polymer.mat", set = Set.Fabric, baseColor = Hex("2F6CAE"), smoothness = .62f, normalScale = .2f, edge = .25f, rim = .12f },
                new Spec { path = MaterialRoot + "/M_Emissive_LED.mat", set = Set.Metal, baseColor = Hex("E8E8E8"), smoothness = .6f, metallic = .2f, normalScale = .2f, edge = .2f, rim = .05f, emission = true, emissionColor = new Color(2.2f, 2.1f, 1.9f) },
            };
            foreach (var spec in refs)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(spec.path);
                if (!m)
                {
                    m = new Material(shader) { name = Path.GetFileNameWithoutExtension(spec.path) };
                    AssetDatabase.CreateAsset(m, spec.path);
                }
                Convert(m, shader, spec);
                if (spec.emission)
                    m.SetTexture("_EmissionMap", T(Set.Metal, "Emission"));
                report.AppendLine("reference " + m.name);
            }
        }

        /// <summary>Import settings of the standard textures: sRGB base, linear masks, tangent normals, aniso, streaming.</summary>
        public static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Tex }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/FX/")) continue;   // atmosphere sprites: clamped, set by the atmosphere builder
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_Normal.png"), mask = path.EndsWith("_Mask.png") || path.EndsWith("T_WorldDetail.png");
                bool changed = false;
                void Set<T>(T current, T wanted, System.Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); changed = true; } }
                Set(importer.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                Set(importer.sRGBTexture, !(normal || mask), v => importer.sRGBTexture = v);
                Set(importer.anisoLevel, path.Contains("/Court/") || path.Contains("/Stone/") ? 8 : 4, v => importer.anisoLevel = v);
                Set(importer.streamingMipmaps, true, v => importer.streamingMipmaps = v);
                Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
                Set(importer.wrapMode, TextureWrapMode.Repeat, v => importer.wrapMode = v);
                var android = importer.GetPlatformTextureSettings("Android");
                if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
                {
                    android.overridden = true;
                    android.format = TextureImporterFormat.ASTC_6x6;
                    android.maxTextureSize = 512;
                    importer.SetPlatformTextureSettings(android);
                    changed = true;
                }
                if (changed)
                    importer.SaveAndReimport();
            }
        }
    }
}
