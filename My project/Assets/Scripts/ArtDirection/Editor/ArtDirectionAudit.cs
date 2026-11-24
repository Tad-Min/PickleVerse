using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Art Bible compliance check: lists every renderer material in the build scenes and prefabs whose shader is not one
    /// of the approved PickleVerse shaders (Stylized Lit, foliage, sky/FX, match-found, shop hero shaders) or a UI/FX shader.
    /// Menu: PickleVerse/Art Direction/Audit materials.
    /// </summary>
    public static class ArtDirectionAudit
    {
        public static bool Approved(Shader s)
        {
            if (!s) return false;
            string n = s.name;
            return n.StartsWith("PickleVerse/") || n.StartsWith("TextMeshPro/") || n.StartsWith("UI/") || n.StartsWith("Sprites/")
                || n.Contains("Particles") || n.StartsWith("Skybox/") || n.StartsWith("Hidden/");
        }

        [MenuItem("PickleVerse/Art Direction/Audit materials")]
        public static string Run()
        {
            var offenders = new Dictionary<string, HashSet<string>>();
            void Check(IEnumerable<Renderer> renderers, string where)
            {
                foreach (var r in renderers)
                    foreach (var m in r.sharedMaterials)
                        if (m && !Approved(m.shader))
                        {
                            string key = m.name + " [" + m.shader.name + "] " + AssetDatabase.GetAssetPath(m);
                            if (!offenders.TryGetValue(key, out var set)) offenders[key] = set = new HashSet<string>();
                            set.Add(where);
                        }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Photon/") || path.Contains("TextMesh Pro") || path.Contains("PickleVerse.Testing.Generated")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go) Check(go.GetComponentsInChildren<Renderer>(true), path);
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            foreach (var s in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                Check(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)), s.path);
            }
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            var sb = new StringBuilder();
            foreach (var pair in offenders.OrderBy(p => p.Key))
                sb.AppendLine(pair.Key + "  <=  " + string.Join(", ", pair.Value.Take(4)) + (pair.Value.Count > 4 ? " …" : ""));
            sb.AppendLine("non-standard materials: " + offenders.Count);
            return sb.ToString();
        }
    }
}
