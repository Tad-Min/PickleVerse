using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Builds the per-look atmosphere prefabs (Art Bible §04): sunlit motes and falling leaves around the Sunset District
    /// venue, skylight shafts and dust in the Academy arena. Everything stays outside the court volume
    /// (|x| > 4.5 m or |z| > 9 m, or above 6 m) so no effect ever hides the ball or a player.
    /// Menu: PickleVerse/Art Direction/Rebuild atmosphere.
    /// </summary>
    public static class ArtDirectionAtmosphereBuilder
    {
        const string Root = "Assets/Prefabs/ArtDirection";
        const string Fx = "Assets/Textures/ArtDirection/FX";
        const string Mat = "Assets/Materials/ArtDirection/FX";
        const string LooksPath = "Assets/Resources/ArtDirection/ArtDirectionLooks.asset";

        [MenuItem("PickleVerse/Art Direction/Rebuild atmosphere")]
        public static string Build()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Mat);
            ConfigureTexture(Fx + "/T_FX_Mote.png");
            ConfigureTexture(Fx + "/T_FX_Leaves.png");
            ConfigureTexture(Fx + "/T_FX_Shaft.png");
            var mote = ParticleMaterial("M_FX_Mote", Fx + "/T_FX_Mote.png", new Color(1f, .86f, .62f, .55f), additive: true, nightDarken: 0);
            var leaf = ParticleMaterial("M_FX_Leaf", Fx + "/T_FX_Leaves.png", Color.white, additive: false, nightDarken: 1);
            var shaft = ShaftMaterial();
            var quad = ShaftQuad();

            var outdoor = new GameObject("Atmosphere_SunsetDistrict");
            // Warm motes drifting in the low sun over both stands and behind the far baseline (never above the court).
            Motes(outdoor.transform, "Sunlit Motes West", new Vector3(-11, 3.2f, 0), new Vector3(6, 4.5f, 26), mote, 70);
            Motes(outdoor.transform, "Sunlit Motes East", new Vector3(11, 3.2f, 0), new Vector3(6, 4.5f, 26), mote, 70);
            Motes(outdoor.transform, "Sunlit Motes Far", new Vector3(0, 3.0f, 16), new Vector3(20, 4, 4), mote, 40);
            Leaves(outdoor.transform, leaf);
            Save(outdoor, Root + "/Atmosphere_SunsetDistrict.prefab", out var outdoorPrefab);

            var arena = new GameObject("Atmosphere_AcademyArena");
            Shafts(arena.transform, quad, shaft);
            Motes(arena.transform, "Arena Dust West", new Vector3(-11, 4, 0), new Vector3(7, 6, 40), mote, 90);
            Motes(arena.transform, "Arena Dust East", new Vector3(11, 4, 0), new Vector3(7, 6, 40), mote, 90);
            Save(arena, Root + "/Atmosphere_AcademyArena.prefab", out var arenaPrefab);

            var looks = AssetDatabase.LoadAssetAtPath<ArtDirectionLooks>(LooksPath);
            if (looks)
            {
                foreach (var look in looks.looks)
                    look.atmosphere = look.name == "Sunset District" ? outdoorPrefab : look.name == "Academy Arena" ? arenaPrefab : null;
                EditorUtility.SetDirty(looks);
            }
            AssetDatabase.SaveAssets();
            return "atmosphere rebuilt";
        }

        static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        static Material ParticleMaterial(string name, string texture, Color tint, bool additive, float nightDarken)
        {
            string path = Mat + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("PickleVerse/FX/Atmosphere Particle"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("PickleVerse/FX/Atmosphere Particle");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_NightDarken", nightDarken);
            m.SetFloat("_SrcBlend", additive ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material ShaftMaterial()
        {
            string path = Mat + "/M_FX_LightShaft.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("PickleVerse/FX/Light Shaft"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Fx + "/T_FX_Shaft.png"));
            m.SetColor("_BaseColor", new Color(1f, .86f, .66f, 1f));
            m.SetFloat("_Intensity", .16f);
            m.SetFloat("_NearFade", 5f);
            m.SetFloat("_Drift", .35f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Unit quad, pivot at the top centre, hanging down along -Y (v = 1 at the top).</summary>
        static Mesh ShaftQuad()
        {
            string path = Mat + "/Mesh_LightShaftQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh) return mesh;
            mesh = new Mesh { name = "LightShaftQuad" };
            mesh.vertices = new[] { new Vector3(-.5f, -1, 0), new Vector3(.5f, -1, 0), new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static ParticleSystem System(Transform parent, string name, Vector3 position, Material material, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = .05f;
            return ps;
        }

        static void Motes(Transform parent, string name, Vector3 centre, Vector3 size, Material material, int max)
        {
            var ps = System(parent, name, centre, material, max);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7, 12);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .08f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .9f, .7f, .55f), new Color(1, .78f, .55f, .9f));
            var emission = ps.emission;
            emission.rateOverTime = max / 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .18f;
            noise.frequency = .25f;
            noise.scrollSpeed = .05f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());
            ps.gameObject.AddComponent<AtmosphereNightResponse>().night = .12f;
        }

        static void Leaves(Transform parent, Material material)
        {
            // Leaves leave the tree belts around the venue: emitted from the edges of a box much larger than the court.
            var ps = System(parent, "Falling Leaves", new Vector3(0, 7, 0), material, 45);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9, 14);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.09f, .16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = .025f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, 1), new Color(.85f, .9f, .8f, 1));
            var emission = ps.emission;
            emission.rateOverTime = 3.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.BoxEdge;
            shape.scale = new Vector3(40, 5, 52);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.6f, 1.6f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .6f;
            noise.frequency = .35f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 1;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0, 2);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());
            ps.gameObject.AddComponent<AtmosphereNightResponse>().night = .25f;
        }

        static void Shafts(Transform parent, Mesh quad, Material material)
        {
            // Skylight direction = MatchEnvironmentLighting.ArenaSkylight (62, 35): beams lean the same way as the shadows.
            var light = Quaternion.Euler(62f, 35f, 0f) * Vector3.forward;
            var group = new GameObject("Skylight Shafts").transform;
            group.SetParent(parent, false);
            int i = 0;
            foreach (int side in new[] { -1, 1 })
                foreach (float z in new[] { -13f, -4.5f, 4.5f, 13f })
                {
                    var go = new GameObject("Shaft " + i++);
                    go.transform.SetParent(group, false);
                    var top = new Vector3(side * 10.5f, 9.6f, z);
                    var along = -light;                                   // from the floor up to the roof opening
                    var towardCourt = new Vector3(-side, 0, 0);
                    var right = Vector3.Cross(along, towardCourt).normalized;
                    var normal = Vector3.Cross(right, along).normalized;
                    go.transform.position = top;
                    go.transform.rotation = Quaternion.LookRotation(-normal, along);
                    float length = 8.4f / Mathf.Abs(light.y);
                    go.transform.localScale = new Vector3(2.6f + (i % 3) * .5f, length, 1);
                    go.AddComponent<MeshFilter>().sharedMesh = quad;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
        }

        static Gradient Fade()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
            return g;
        }

        static void Save(GameObject go, string path, out GameObject prefab)
        {
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
