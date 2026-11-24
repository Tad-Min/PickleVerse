using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// glTF-imported materials (players, paddle, ball, arena structure) are sub-assets the importer regenerates, so they
    /// cannot be converted in place. This tool builds Art Direction materials from their textures and factors and remaps
    /// every renderer that uses them in the build scenes and in prefabs. Menu: PickleVerse/Art Direction/Remap imported materials.
    /// </summary>
    public static class ArtDirectionMaterialRemap
    {
        const string Root = ArtDirectionMaterialStandards.MaterialRoot + "/Imported";

        sealed class Rule
        {
            public string source, materialName, output;
            public ArtDirectionMaterialStandards.Spec spec;
            public bool keepBase = true, keepNormal = true;
            public float smoothnessFromRoughness = -1;
        }

        static IEnumerable<Rule> Rules()
        {
            var character = ArtDirectionMaterialStandards.Character(null);
            yield return new Rule { source = "Assets/LocalMatchContent/Models/Male.glb", materialName = "BakedMaterial", output = "M_Player_Male", spec = character };
            yield return new Rule { source = "Assets/LocalMatchContent/Models/Female.glb", materialName = "BakedMaterial.002", output = "M_Player_Female", spec = character };
            yield return new Rule
            {
                source = "Assets/LocalMatchContent/Models/Paddle.glb", materialName = "Pickleball_StealthBlack_Refined.001", output = "M_Paddle_Standard",
                spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.None, setBase = false, setNormal = false, setMask = false, smoothness = .7f, wrap = .2f, rim = .22f, edge = .3f },
            };
            yield return new Rule
            {
                source = "Assets/Models/Paddle/Paddle.glb", materialName = "Pickleball_StealthBlack_Refined.001", output = "M_Paddle_Standard",
                spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.None, setBase = false, setNormal = false, setMask = false, smoothness = .7f, wrap = .2f, rim = .22f, edge = .3f },
            };
            yield return new Rule
            {
                source = "Assets/Models/Paddle/paddle-skin-thot-model.glb", materialName = "BakedMaterial", output = "M_Paddle_Thot",
                spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.None, setBase = false, setNormal = false, setMask = false, smoothness = .62f, wrap = .2f, rim = .22f, edge = .3f },
            };
            yield return new Rule
            {
                source = "Assets/LocalMatchContent/Models/Ball.glb", materialName = "Mat_PickleballYellow.001", output = "M_Ball_Optic",
                spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.None, setBase = false, setNormal = false, setMask = false, baseColor = ArtDirectionMaterialStandards.Hex("E6E03C"), smoothness = .5f, wrap = .3f, rim = .25f, edge = .1f },
            };
            const string arena = "Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb";
            yield return new Rule { source = arena, materialName = "EN_BlackStructure", output = "M_Arena_BlackStructure", keepBase = false, keepNormal = false, spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.Metal, smoothness = .58f, normalScale = .35f, edge = .35f, rim = .12f } };
            yield return new Rule { source = arena, materialName = "EN_Stainless", output = "M_Arena_Stainless", keepBase = false, keepNormal = false, spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.Metal, smoothness = .74f, metallic = .9f, normalScale = .4f, edge = .4f, rim = .1f } };
            yield return new Rule { source = arena, materialName = "EN_AccentGold", output = "M_Arena_AccentGold", keepBase = false, keepNormal = false, spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.Metal, smoothness = .72f, metallic = .9f, normalScale = .3f, edge = .4f, rim = .12f } };
            yield return new Rule { source = arena, materialName = "EN_BlueSeats", output = "M_Arena_BlueSeats", keepBase = false, keepNormal = false, spec = new ArtDirectionMaterialStandards.Spec { set = ArtDirectionMaterialStandards.Set.Fabric, smoothness = .6f, normalScale = .15f, edge = .3f, rim = .12f, wrap = .3f } };
        }

        [MenuItem("PickleVerse/Art Direction/Remap imported materials")]
        public static string Run()
        {
            var report = new StringBuilder();
            var map = BuildMaterials(report);
            // FemaleFringe is a standalone asset on the glTF shader: convert in place (hair preset).
            var fringe = AssetDatabase.LoadAssetAtPath<Material>("Assets/CharacterMotion/FemaleFringeMaterial.mat");
            if (fringe)
            {
                var color = fringe.HasProperty("baseColorFactor") ? fringe.GetColor("baseColorFactor") : new Color(.17f, .15f, .16f);
                var spec = ArtDirectionMaterialStandards.Character(null);
                spec.baseColor = color;
                spec.smoothness = .45f;
                ArtDirectionMaterialStandards.Convert(fringe, Shader.Find(ArtDirectionMaterialStandards.ShaderName), spec);
                report.AppendLine("converted FemaleFringeMaterial (hair)");
            }
            int prefabs = 0, scenes = 0, swaps = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Photon/") || path.Contains("TextMesh Pro") || path.Contains("PickleVerse.Testing.Generated")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset || !asset.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(m => m && (map.ContainsKey(m) ||
                    Translucent.Contains(m.name) && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)))) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                int n = Swap(root.GetComponentsInChildren<Renderer>(true), map);
                if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabs++; swaps += n; report.AppendLine($"prefab {path}: {n}"); }
                PrefabUtility.UnloadPrefabContents(root);
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                int n = Swap(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)), map);
                if (n > 0) { EditorSceneManager.SaveScene(scene); scenes++; swaps += n; report.AppendLine($"scene {s.path}: {n}"); }
            }
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            AssetDatabase.SaveAssets();
            report.AppendLine($"remapped {swaps} material slots in {prefabs} prefabs and {scenes} scenes");
            Debug.Log("[ArtDirection] " + report);
            return report.ToString();
        }

        /// <summary>Translucent fabrics (the arena's membrane roof) let diffuse skylight through: no hard shadow.</summary>
        static readonly string[] Translucent = { "EN_IvoryMembrane" };

        static int Swap(IEnumerable<Renderer> renderers, Dictionary<Material, Material> map)
        {
            int n = 0;
            foreach (var r in renderers)
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off && r.sharedMaterials.Any(m => m && Translucent.Contains(m.name)))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    EditorUtility.SetDirty(r);
                    n++;
                }
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] && map.TryGetValue(mats[i], out var to)) { mats[i] = to; changed = true; n++; }
                if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
            }
            return n;
        }

        static Dictionary<Material, Material> BuildMaterials(StringBuilder report)
        {
            Directory.CreateDirectory(Root);
            var shader = Shader.Find(ArtDirectionMaterialStandards.ShaderName);
            var map = new Dictionary<Material, Material>();
            foreach (var rule in Rules())
            {
                var source = AssetDatabase.LoadAllAssetsAtPath(rule.source).OfType<Material>().FirstOrDefault(m => m.name == rule.materialName);
                if (!source) { report.AppendLine("missing " + rule.source + " / " + rule.materialName); continue; }
                string path = Root + "/" + rule.output + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(shader) { name = rule.output }; AssetDatabase.CreateAsset(m, path); }
                var spec = rule.spec;
                spec.path = path;
                var factor = source.HasProperty("baseColorFactor") ? source.GetColor("baseColorFactor") : Color.white;
                var baseTex = rule.keepBase && source.HasProperty("baseColorTexture") ? source.GetTexture("baseColorTexture") : null;
                var normal = rule.keepNormal && source.HasProperty("normalTexture") ? source.GetTexture("normalTexture") : null;
                if (spec.baseColor == null) spec.baseColor = factor;
                ArtDirectionMaterialStandards.Convert(m, shader, spec);
                if (baseTex) m.SetTexture("_BaseMap", baseTex);
                if (normal)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 1);
                    m.SetFloat("_UseNormalMap", 1);
                    m.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(m);
                map[source] = m;
                report.AppendLine($"material {rule.output} <- {rule.materialName}");
            }
            return map;
        }
    }
}
