using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using PickleVerse.LocalMatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Explicit Editor commands only (no menu hooks, no runtime code, no network). Installs the golden-hour
// district: texture import rules, materials, tree/district prefabs and the two upgraded venue prefabs.
// Sources: ArtSource/Stadiums/SunsetDistrict (Blender scripts + .blend). Evidence: BlenderWork/SunsetDistrict20261115.
public static class SunsetDistrictInstaller
{
    public const string Folder = "Assets/EnvironmentContent/SunsetDistrict";
    const string Tex = Folder + "/Textures", Mats = Folder + "/Materials", Prefabs = Folder + "/Prefabs";
    const string Models = "Assets/Models/Stadium_Enhanced";
    const string SkyPath = "Assets/Resources/Lighting/GoldenHourSky.mat";
    static readonly string Art = Path.GetFullPath("../ArtSource/Stadiums/SunsetDistrict");
    static readonly string Work = Path.GetFullPath("../BlenderWork/SunsetDistrict20261115");
    static readonly string[] Keys = { "Professional", "LowPoly" };
    static string ModelFile(string key) => key == "Professional" ? "ProfessionalStadium" : "PickleballCourt_LowPoly";
    // District ground height relative to each venue's court surface (matches assemble.py / bake).
    public static readonly Dictionary<string, float> Ground = new Dictionary<string, float> { { "Professional", -0.47f }, { "LowPoly", -0.16f } };
    static readonly string[] Cutouts = { "SD_Foliage", "SD_ChainLink", "SD_Net", "SD_Skyline", "SD_TreeImpostors" };
    static readonly string[] GroundMaps = { "SD_Asphalt", "SD_Paving", "SD_Acrylic", "SD_Grass", "SD_Concrete", "SD_Asphalt_N", "SD_Paving_N", "SD_Acrylic_N" };

    public static object Ping() => new { ready = !EditorApplication.isCompiling, playing = EditorApplication.isPlaying, unity = Application.unityVersion };

    // ------------------------------------------------------------------ geometry guard
    static object Geometry(StadiumEnvironment e)
    {
        return new
        {
            surfaces = new[] { e.competitionSurface, e.kitchenSurface, e.courtLines, e.netStructure }.Select(f => new
            {
                f.name,
                vertices = f.sharedMesh.vertices.Select(f.transform.TransformPoint).Select(v => new[] { Mathf.Round(v.x * 10000), Mathf.Round(v.y * 10000), Mathf.Round(v.z * 10000) })
                    .GroupBy(v => string.Join(",", v)).Select(g => g.First()).OrderBy(v => v[0]).ThenBy(v => v[1]).ThenBy(v => v[2]).ToArray()
            }).ToArray(),
            anchors = new[] { e.nearEntrance, e.farEntrance, e.nearResult, e.farResult }.Select(t => new { t.name, position = t.position.ToString("F4") }).ToArray(),
            limits = e.courtsideCameraLimits.ToString("F4"),
            lights = e.GetComponentsInChildren<Light>(true).OrderBy(l => l.name).Select(l => new { l.name, position = l.transform.position.ToString("F4"), rotation = l.transform.rotation.eulerAngles.ToString("F2"), l.range, l.spotAngle, l.intensity, color = l.color.ToString("F3"), l.shadows }).ToArray(),
            colliders = e.GetComponentsInChildren<Collider>(true).Length,
            behaviours = e.GetComponentsInChildren<MonoBehaviour>(true).Select(b => b.GetType().Name).OrderBy(n => n).ToArray()
        };
    }

    public static object CaptureGeometry(string label)
    {
        Directory.CreateDirectory(Work);
        var rows = Keys.ToDictionary(k => k, k =>
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/Resources/Courts/" + k + ".prefab");
            try { return Geometry(root.GetComponent<StadiumEnvironment>()); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        });
        File.WriteAllText(Path.Combine(Work, "geometry-" + label + ".json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
        return new { captured = rows.Count };
    }

    public static object ValidateGeometry()
    {
        var before = JObject.Parse(File.ReadAllText(Path.Combine(Work, "geometry-before.json")));
        CaptureGeometry("after");
        var after = JObject.Parse(File.ReadAllText(Path.Combine(Work, "geometry-after.json")));
        var checks = new List<object>();
        foreach (string key in Keys)
            foreach (string field in new[] { "surfaces", "anchors", "limits", "lights", "colliders", "behaviours" })
                checks.Add(new { name = key + " unchanged " + field, passed = JToken.DeepEquals(before[key][field], after[key][field]) });
        var result = new { passed = checks.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), checks };
        File.WriteAllText(Path.Combine(Work, "geometry-validation.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }

    // ------------------------------------------------------------------ textures
    public static object ConfigureTextures()
    {
        var rows = new List<object>();
        foreach (string path in Directory.GetFiles(Tex, "*.png").Select(p => p.Replace('\\', '/')))
        {
            string n = Path.GetFileNameWithoutExtension(path);
            var t = (TextureImporter)AssetImporter.GetAtPath(path);
            bool normal = n.EndsWith("_N"), mask = n.StartsWith("SD_SunMask"), cutout = Cutouts.Contains(n), white = n == "SD_White";
            // Venue mask: R = golden-hour sun visibility, G = sqrt(night lamp pools). Linear data, two channels.
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !(normal || mask || n == "SD_Cloud");
            t.alphaSource = cutout || n == "SD_Facade" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            t.alphaIsTransparency = cutout;
            t.mipmapEnabled = !white;
            // Fine weaves keep the true average alpha in their mips (the shader dithers it at distance);
            // leaves/billboards/skyline preserve coverage so crowns do not thin out.
            t.mipMapsPreserveCoverage = cutout && n != "SD_ChainLink" && n != "SD_Net";
            t.alphaTestReferenceValue = n == "SD_Foliage" || n == "SD_TreeImpostors" ? 0.45f : 0.5f;
            t.isReadable = false;
            t.wrapModeU = mask || n == "SD_TreeImpostors" || n == "SD_Foliage" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            t.wrapModeV = mask || n == "SD_TreeImpostors" || n == "SD_Foliage" || n == "SD_Skyline" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            t.filterMode = white ? FilterMode.Point : FilterMode.Trilinear;
            t.anisoLevel = GroundMaps.Contains(n) ? 8 : n == "SD_ChainLink" || n == "SD_Net" ? 4 : mask ? 1 : 2;
            t.GetSourceTextureWidthAndHeight(out int w, out int h);
            // Distant-only maps (tree billboards, skyline card, sky clouds) never need more than 512 px.
            int size = n == "SD_TreeImpostors" || n == "SD_Skyline" || n == "SD_Cloud" ? Mathf.Min(512, Mathf.Max(w, h)) : Mathf.Max(w, h);
            t.maxTextureSize = size;
            t.textureCompression = white ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            int android = n == "SD_TreeImpostors" || n == "SD_Skyline" || n == "SD_Cloud" ? Mathf.Min(512, size) : size;
            var a = t.GetPlatformTextureSettings("Android");
            a.name = "Android"; a.overridden = !white; a.maxTextureSize = android;
            a.format = cutout ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            a.compressionQuality = 75;
            t.SetPlatformTextureSettings(a);
            var pc = t.GetPlatformTextureSettings("Standalone");
            pc.name = "Standalone"; pc.overridden = mask; pc.maxTextureSize = size;
            if (mask) pc.format = TextureImporterFormat.BC5;
            t.SetPlatformTextureSettings(pc);
            t.SaveAndReimport();
            rows.Add(new { n, size, android, cutout, normal, mask });
        }
        return rows;
    }

    // ------------------------------------------------------------------ materials
    static Texture2D T(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/" + name + ".png");

    static Material Mat(string name, string shader, Action<Material> setup, string folder = Mats)
    {
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var s = Shader.Find(shader);
        if (!s) throw new Exception("Shader missing: " + shader);
        if (!m) { m = new Material(s) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.shader = s;
        m.enableInstancing = true;
        setup(m);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssetIfDirty(m);
        return m;
    }

    static Action<Material> Surface(string map, string normal, float normalStrength, float smooth, float metal = 0, bool sunMask = false, bool cutout = false,
        float glass = 0, float glow = 0, bool water = false, bool unlit = false, bool a2c = false, bool dither = false)
    {
        return m =>
        {
            m.SetTexture("_BaseMap", T(map));
            m.SetTexture("_BumpMap", normal != null ? T(normal) : null);
            m.SetFloat("_BumpScale", normalStrength);
            if (normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_UseNormalMap", normal != null ? 1 : 0);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); m.SetFloat("_GlassSmoothness", glass);
            m.SetFloat("_SunMask", sunMask ? 1 : 0); m.SetFloat("_Glow", glow); m.SetFloat("_Water", water ? 1 : 0); m.SetFloat("_Unlit", unlit ? 1 : 0);
            m.SetFloat("_Dither", dither ? 1 : 0);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_AlphaTest", cutout ? 1 : 0);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetFloat("_AlphaToMask", a2c ? 1 : 0);
            m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
        };
    }

    static Action<Material> Foliage(string map, bool cutout, float wind, float translucency, float cutoff = 0.45f, bool billboard = false)
    {
        return m =>
        {
            m.SetTexture("_BaseMap", T(map));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_WindStrength", wind); m.SetFloat("_WindSpeed", 0.9f);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_Billboard", billboard ? 1 : 0);
            m.SetFloat("_AlphaTest", cutout ? 1 : 0);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetFloat("_AlphaToMask", cutout ? 1 : 0);
            m.renderQueue = cutout ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
        };
    }

    static Dictionary<string, Material> materials;
    public static object CreateMaterials()
    {
        const string S = "PickleVerse/Environment/Sunset Surface", F = "PickleVerse/Environment/Sunset Foliage";
        materials = new Dictionary<string, Material>
        {
            ["SD_Acrylic"] = Mat("SD_Acrylic", S, Surface("SD_Acrylic", "SD_Acrylic_N", 0.55f, 0.42f, sunMask: true)),
            ["SD_Asphalt"] = Mat("SD_Asphalt", S, Surface("SD_Asphalt", "SD_Asphalt_N", 0.8f, 0.18f, sunMask: true)),
            ["SD_Paving"] = Mat("SD_Paving", S, Surface("SD_Paving", "SD_Paving_N", 0.9f, 0.24f, sunMask: true)),
            ["SD_Concrete"] = Mat("SD_Concrete", S, Surface("SD_Concrete", null, 0, 0.14f)),
            ["SD_Grass"] = Mat("SD_Grass", S, Surface("SD_Grass", null, 0, 0.06f, sunMask: true)),
            ["SD_Wood"] = Mat("SD_Wood", S, Surface("SD_Wood", null, 0, 0.38f)),
            ["SD_Paint"] = Mat("SD_Paint", S, Surface("SD_White", null, 0, 0.52f)),
            ["SD_Metal"] = Mat("SD_Metal", S, Surface("SD_White", null, 0, 0.62f, metal: 0.85f)),
            ["SD_Glow"] = Mat("SD_Glow", S, Surface("SD_White", null, 0, 0.60f, glow: 1)),
            ["SD_Facade"] = Mat("SD_Facade", S, Surface("SD_Facade", null, 0, 0.22f, glass: 0.88f, glow: 0.6f)),
            ["SD_Water"] = Mat("SD_Water", S, Surface("SD_White", "SD_Ripple_N", 0.6f, 0.92f, sunMask: true, water: true)),
            ["SD_ChainLink"] = Mat("SD_ChainLink", S, Surface("SD_ChainLink", null, 0, 0.45f, metal: 0.5f, cutout: true, a2c: true, dither: true)),
            ["SD_Net"] = Mat("SD_Net", S, Surface("SD_Net", null, 0, 0.20f, cutout: true, a2c: true, dither: true)),
            ["SD_Skyline"] = Mat("SD_Skyline", S, Surface("SD_Skyline", null, 0, 0, cutout: true, unlit: true)),
            ["SD_Foliage"] = Mat("SD_Foliage", F, Foliage("SD_Foliage", true, 0.12f, 0.55f)),
            ["SD_Bark"] = Mat("SD_Bark", F, Foliage("SD_Bark", false, 0.03f, 0f)),
            ["SD_Impostor"] = Mat("SD_Impostor", F, Foliage("SD_TreeImpostors", true, 0.0f, 0.35f, 0.5f, billboard: true)),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(SkyPath));
        var sky = Mat("GoldenHourSky", "PickleVerse/Environment/Golden Hour Sky", m => m.SetTexture("_CloudTex", T("SD_Cloud")), Path.GetDirectoryName(SkyPath).Replace('\\', '/'));
        sky.enableInstancing = false;
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();
        return materials.Keys.Concat(new[] { "GoldenHourSky" }).ToArray();
    }

    // ------------------------------------------------------------------ night glare sprites
    static Material HaloMaterial() => Mat("SD_Halo", "PickleVerse/Environment/Lamp Halo", m =>
    {
        m.SetColor("_Color", new Color(1.0f, .70f, .40f));
        m.SetFloat("_Intensity", 1.1f);
        m.SetFloat("_MinAngular", .012f);
        m.enableInstancing = false;
        m.renderQueue = (int)RenderQueue.Transparent + 20;
    });

    /// <summary>One static mesh, one draw call: every sprite's four vertices sit at the lamp centre; the shader
    /// expands them towards the camera. UV0 = corner, UV1 = (radius m, strength).</summary>
    static Mesh HaloMesh(string name, IList<Vector3> centres, IList<Vector2> lamps)
    {
        Directory.CreateDirectory(Folder + "/Meshes");
        string path = Folder + "/Meshes/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        var vertices = new List<Vector3>(); var corners = new List<Vector2>(); var data = new List<Vector2>(); var triangles = new List<int>();
        for (int i = 0; i < centres.Count; i++)
        {
            int b = vertices.Count;
            foreach (var c in new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) })
            { vertices.Add(centres[i]); corners.Add(c); data.Add(lamps[i]); }
            triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        mesh.SetVertices(vertices); mesh.SetUVs(0, corners); mesh.SetUVs(1, data); mesh.SetTriangles(triangles, 0);
        var bounds = new Bounds(centres[0], Vector3.zero);
        foreach (var c in centres) bounds.Encapsulate(c);
        bounds.Expand(6f);      // sprites grow around their centres in the vertex shader
        mesh.bounds = bounds;
        mesh.UploadMeshData(false);
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    static GameObject HaloObject(string name, Transform parent, Mesh mesh)
    {
        var old = parent.Find(name);
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = HaloMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    static Material Remap(Material source)
    {
        string n = source ? source.name.Replace(" (Instance)", "") : "";
        if (materials.TryGetValue(n, out var m)) return m;
        if (n.StartsWith("Venue_Signage")) return AssetDatabase.LoadAssetAtPath<Material>("Assets/LocalMatchContent/Stadiums/Refined_Venue_Signage.mat");
        throw new Exception("No material mapping for " + n);
    }

    static void Flags(Renderer r, bool castShadows)
    {
        r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows = true;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        r.allowOcclusionWhenDynamic = true;
    }

    // ------------------------------------------------------------------ prefabs
    static GameObject Model(string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!model) throw new Exception("Model not imported: " + path);
        return model;
    }

    static string TreePrefab(string species) => Prefabs + "/Tree_" + species + ".prefab";

    public static object BuildTreePrefabs()
    {
        Directory.CreateDirectory(Prefabs);
        var source = Model(Models + "/District/SunsetTrees.glb");
        var rows = new List<object>();
        foreach (string species in new[] { "Canopy", "Laurel", "Flame", "Palm" })
        {
            var root = new GameObject("Tree_" + species);
            try
            {
                var lods = new List<LOD>();
                // Screen-relative heights; PC LOD bias 2 doubles the distances, mobile skips LOD0.
                float[] heights = { 0.30f, 0.13f, 0.045f, 0.006f };
                for (int l = 0; l < 4; l++)
                {
                    var template = source.GetComponentsInChildren<Transform>(true).Single(t => t.name == species + "_LOD" + l);
                    var go = UnityEngine.Object.Instantiate(template.gameObject, root.transform, false);
                    go.name = species + "_LOD" + l;
                    go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                    var r = go.GetComponent<MeshRenderer>();
                    r.sharedMaterials = r.sharedMaterials.Select(Remap).ToArray();
                    Flags(r, false);
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                    lods.Add(new LOD(heights[l], new Renderer[] { r }));
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(lods.ToArray());
                group.RecalculateBounds();
                group.fadeMode = LODFadeMode.None;
                PrefabUtility.SaveAsPrefabAsset(root, TreePrefab(species));
                rows.Add(new { species, triangles = lods.Select(l => l.renderers[0].GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3).ToArray(), size = group.size });
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        return rows;
    }

    public static object BuildDistrictPrefab()
    {
        var layout = JObject.Parse(File.ReadAllText(Path.Combine(Art, "district_layout.json")));
        var root = new GameObject("Sunset District");
        try
        {
            var model = UnityEngine.Object.Instantiate(Model(Models + "/District/SunsetDistrict.glb"), root.transform, false);
            model.name = "District Model";
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(Remap).ToArray();
                Flags(r, false);     // the sun's static shadows are baked into the ground mask
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
            }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            var trees = new GameObject("Sunset Trees");
            trees.transform.SetParent(root.transform, false);
            int i = 0;
            foreach (var row in layout["trees"])
            {
                string species = (string)row["species"];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefab(species));
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees.transform);
                tree.name = species + " " + i++;
                tree.transform.localPosition = new Vector3((float)row["x"], 0, (float)row["y"]);
                tree.transform.localRotation = Quaternion.Euler(0, (float)row["yaw"], 0);
                tree.transform.localScale = Vector3.one * (float)row["scale"];
            }
            // Street lamps (7 m heads) get a larger glare than the 3.6 m park lanterns.
            var lamps = layout["lamps"].Select(l => new { p = new Vector3((float)l["x"], (float)l["z"] - 0.12f, (float)l["y"]), street = (float)l["z"] > 5 }).ToArray();
            HaloObject("District Halos", root.transform, HaloMesh("DistrictHalos", lamps.Select(l => l.p).ToList(),
                lamps.Select(l => l.street ? new Vector2(1.5f, 1.0f) : new Vector2(0.95f, 0.85f)).ToList()));
            PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/SunsetDistrict.prefab");
            return new
            {
                trees = i,
                renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length,
                staticTriangles = model.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3),
                materials = model.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToArray()
            };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static object UpgradeVenue(string key)
    {
        string path = "Assets/Resources/Courts/" + key + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var e = root.GetComponent<StadiumEnvironment>();
            var old = root.transform.Find("Venue Visual");
            if (!old) throw new Exception("Venue Visual missing in " + key);
            var visual = UnityEngine.Object.Instantiate(Model(Models + "/" + ModelFile(key) + ".glb"), root.transform);
            visual.name = "Venue Visual";
            visual.transform.localPosition = old.localPosition; visual.transform.localRotation = old.localRotation; visual.transform.localScale = old.localScale;
            foreach (var r in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterials = r.sharedMaterials.Select(Remap).ToArray();
                bool flat = r.name.StartsWith("Court_") || r.name.Contains("Paving") || r.name.Contains("Signage") || r.name.Contains("Cutout");
                Flags(r, !flat);
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            }
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            var filters = visual.GetComponentsInChildren<MeshFilter>(true);
            e.competitionSurface = filters.Single(f => f.name.StartsWith("Court_"));
            e.kitchenSurface = e.courtLines = e.competitionSurface;
            e.netStructure = filters.Single(f => f.name == "PortableNet_White_Headband");
            float plane = e.competitionSurface.sharedMesh.vertices.Select(e.competitionSurface.transform.TransformPoint).Max(v => v.y);
            visual.transform.position -= Vector3.up * plane;
            var gate = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gate_Hinge_OPEN_ROTATE_LOCAL_Z");
            if (gate) gate.Rotate(Vector3.up, 105, Space.World);
            UnityEngine.Object.DestroyImmediate(old.gameObject);

            var park = root.transform.Find("Natural Park");
            if (park) UnityEngine.Object.DestroyImmediate(park.gameObject);
            var previous = root.transform.Find("Sunset District");
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var district = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/SunsetDistrict.prefab"), root.transform);
            district.name = "Sunset District";
            district.transform.localPosition = new Vector3(0, Ground[key], 0);

            // Sculpted masts and luminaires replace the primitive cubes in place (same objects, same lights).
            var fixtures = Model(Models + "/District/VenueFixtures.glb");
            Transform Node(string n) => fixtures.GetComponentsInChildren<Transform>(true).Single(t => t.name == n);
            var mast = Node(key == "Professional" ? "Mast_Professional" : "Mast_LowPoly");
            var lum = Node("Luminaire");
            foreach (Transform child in root.transform)
            {
                if (child.name.StartsWith("Floodlight Pole"))
                {
                    child.GetComponent<MeshFilter>().sharedMesh = mast.GetComponent<MeshFilter>().sharedMesh;
                    var r = child.GetComponent<MeshRenderer>();
                    r.sharedMaterials = mast.GetComponent<MeshRenderer>().sharedMaterials.Select(Remap).ToArray();
                    Flags(r, true);
                }
                else if (child.name.StartsWith("White LED Fixture"))
                {
                    child.GetComponent<MeshFilter>().sharedMesh = lum.GetComponent<MeshFilter>().sharedMesh;
                    var r = child.GetComponent<MeshRenderer>();
                    var lens = r.sharedMaterial;
                    var mats = lum.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name.StartsWith("SD_Glow") ? lens : Remap(m)).ToArray();
                    r.sharedMaterials = mats;
                    Flags(r, true);
                }
            }
            // Floodlight glare at the four luminaires (visual only; the four realtime spotlights are unchanged).
            var heads = root.transform.Cast<Transform>().Where(t => t.name.StartsWith("White LED Fixture")).Select(t => t.localPosition + Vector3.down * 0.12f).ToList();
            HaloObject("Floodlight Halos", root.transform, HaloMesh("FloodlightHalos_" + key, heads, heads.Select(_ => new Vector2(1.9f, 0.8f)).ToList()));
            e.sunShadowMask = T("SD_SunMask_" + key);
            e.sunMaskGroundY = Ground[key];
            e.sunMaskSpan = 224;
            e.ApplyNight(0, true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return new { key, venueRenderers = visual.GetComponentsInChildren<MeshRenderer>(true).Length, venueMaterials = visual.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().Count() };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Full install. Run CaptureGeometry("before") once beforehand; ValidateGeometry() afterwards.</summary>
    public static object Install()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Clean Edit mode required");
        if (!File.Exists(Path.Combine(Work, "geometry-before.json"))) throw new Exception("Capture geometry-before first");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var textures = ConfigureTextures();
        var mats = CreateMaterials();
        var trees = BuildTreePrefabs();
        var district = BuildDistrictPrefab();
        var venues = Keys.Select(UpgradeVenue).ToArray();
        AssetDatabase.SaveAssets();
        var result = new { textures, mats, trees, district, venues, geometry = ValidateGeometry() };
        File.WriteAllText(Path.Combine(Work, "installation.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
}
