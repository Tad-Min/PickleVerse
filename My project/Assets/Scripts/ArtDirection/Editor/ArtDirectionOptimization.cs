using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Measured optimisations (Art Bible §06 "Visual Quality per Millisecond"). Each rule is backed by a frame
    /// counter measurement recorded in Doc/ArtBible/06-Rendering-Performance.md.
    ///  - Seated audience: the 24k-triangle LOD0 only for close-ups (LOD0 within ~7.5 m of the camera); the gameplay camera sees
    ///    the 6.7k LOD1 skinned with two bones. In BotMatch this removes ~0.8M skinned triangles per frame.
    /// Menu: PickleVerse/Art Direction/Apply measured optimisations.
    /// </summary>
    public static class ArtDirectionOptimization
    {
        public const float AudienceLod0 = .8f, AudienceCull = .006f;   // LODGroup size ~3.4 m, QualitySettings.lodBias 2
        static readonly string[] AudiencePrefabs = { "Assets/Resources/Audience/Professional.prefab", "Assets/Resources/Audience/LowPoly.prefab" };

        [MenuItem("PickleVerse/Art Direction/Apply measured optimisations")]
        public static string Apply()
        {
            var report = new StringBuilder();
            foreach (var path in AudiencePrefabs)
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                int groups = 0;
                foreach (var group in root.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods = group.GetLODs();
                    if (lods.Length != 2 || lods[0].renderers.Length == 0 || !(lods[0].renderers[0] is SkinnedMeshRenderer))
                        continue;
                    lods[0].screenRelativeTransitionHeight = AudienceLod0;
                    lods[1].screenRelativeTransitionHeight = AudienceCull;
                    group.SetLODs(lods);
                    foreach (var r in lods[1].renderers.OfType<SkinnedMeshRenderer>())
                    {
                        r.quality = SkinQuality.Bone2;
                        r.updateWhenOffscreen = false;
                    }
                    groups++;
                }
                if (groups > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                report.AppendLine($"{path}: {groups} audience LOD groups");
            }
            return report.ToString();
        }
    }
}
