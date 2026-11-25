using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PickleVerse.ArtDirection.Editor
{
    /// <summary>
    /// Keeps the depth-of-field shaders that the project's Volume profiles use in every player build. URP decides from
    /// DepthOfField.IsActive(), which asks the build machine's graphics device; a command-line build runs with -nographics,
    /// so URP sees no device, drops the shader and the cinematics silently lose their depth of field.
    /// </summary>
    public sealed class DepthOfFieldVariantKeeper : IShaderVariantStripper
    {
        public const string GaussianShader = "Hidden/Universal Render Pipeline/GaussianDepthOfField";
        public const string BokehShader = "Hidden/Universal Render Pipeline/BokehDepthOfField";

        HashSet<string> kept;

        public bool active => true;

        // A variant is removed only when every stripper agrees, so this one answers "yes" for every other shader.
        public bool CanRemoveVariant(Shader shader, ShaderSnippetData shaderVariant, ShaderCompilerData shaderCompilerData)
        {
            kept ??= UsedShaders(ProjectProfiles());
            return !kept.Contains(shader.name);
        }

        /// <summary>Depth-of-field shaders the profiles switch on, whatever the current graphics device supports.</summary>
        public static HashSet<string> UsedShaders(IEnumerable<VolumeProfile> profiles)
        {
            var used = new HashSet<string>();
            foreach (var profile in profiles)
            {
                if (!profile || !profile.TryGet<DepthOfField>(out var dof) || !dof.active || !dof.mode.overrideState)
                    continue;
                if (dof.mode.value == DepthOfFieldMode.Gaussian)
                    used.Add(GaussianShader);
                else if (dof.mode.value == DepthOfFieldMode.Bokeh)
                    used.Add(BokehShader);
            }
            return used;
        }

        static IEnumerable<VolumeProfile> ProjectProfiles()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" }))
                yield return AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
