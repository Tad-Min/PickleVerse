using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PickleVerse.LocalMatch.Editor
{
    public static class MatchEnvironmentSetup
    {
        const string SkyRoot = "Assets/Materials/Environment/Sky";
        const string Images = SkyRoot + "/AllSkyFree/AllSkyFree_Skyboxes/";
        public const string NightImage = Images + "AllSky_Night_MoonBurst Equirect.png";
        public const string DayImage = Images + "Epic_BlueSunset_EquiRect_flat.png";
        const string Materials = "Assets/LocalMatchContent/Lighting";

        [MenuItem("PickleVerse/Configure BotMatch day and night lighting")]
        public static void ConfigureMenu()
        {
            Configure();
        }

        public static string Configure()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/BotMatch.unity")
                throw new InvalidOperationException("Open BotMatch first.");
            Directory.CreateDirectory("../UnityWork/MatchLighting");
            if (!File.Exists("../UnityWork/MatchLighting/Before.unity"))
                EditorSceneManager.SaveScene(scene, "../UnityWork/MatchLighting/Before.unity", true);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var match = UnityEngine.Object.FindAnyObjectByType<BotMatch>();
            var lighting = match.GetComponent<MatchEnvironmentLighting>() ?? match.gameObject.AddComponent<MatchEnvironmentLighting>();
            lighting.daySky = SkyMaterial(DayImage, "BlueSunset", 1.0f, 0);
            lighting.nightSky = SkyMaterial(NightImage, "MoonBurst", .70f, 35);
            lighting.matchCamera = UnityEngine.Object.FindAnyObjectByType<MatchCamera>().GetComponent<Camera>();
            lighting.matchCamera.clearFlags = CameraClearFlags.Skybox;
            var sun = UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            if (!sun)
                sun = new GameObject("Match Sun").AddComponent<Light>();
            sun.name = "Match Sun";
            sun.type = LightType.Directional;
            sun.color = new Color(1, .93f, .82f);
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(28, -35, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .8f;
            sun.shadowBias = .035f;
            sun.shadowNormalBias = .12f;
            lighting.sun = sun;
            var lamps = UnityEngine.Object.FindObjectsByType<Transform>().Where(t => t.name == "LampHousing").OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToArray();
            if (lamps.Length != 4)
                throw new InvalidOperationException("Expected four corner lamp housings.");
            var arena = GameObject.Find("Arena").transform;
            lighting.floodlights = new Light[4];
            lighting.lampFaces = lamps.Select(t => t.GetComponent<Renderer>()).ToArray();
            lighting.lampsOff = LampMaterial("LampsOff", false);
            lighting.lampsOn = LampMaterial("LampsOn", true);
            for (int i = 0; i < 4; i++)
            {
                string name = "Corner Floodlight " + (i + 1);
                var existing = arena.Find(name);
                var light = existing ? existing.GetComponent<Light>() : new GameObject(name).AddComponent<Light>();
                light.transform.SetParent(arena, false);
                light.transform.position = lamps[i].position + Vector3.down * .11f;
                var target = new Vector3(Mathf.Sign(light.transform.position.x) * .8f, .25f, Mathf.Sign(light.transform.position.z) * 1.8f);
                light.transform.rotation = Quaternion.LookRotation(target - light.transform.position);
                light.type = LightType.Spot;
                light.range = 24;
                light.spotAngle = 95;
                light.innerSpotAngle = 62;
                light.color = new Color(.91f, .95f, 1);
                light.intensity = 40;
                light.renderMode = LightRenderMode.ForcePixel;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = .8f;
                light.shadowBias = .025f;
                light.shadowNormalBias = .06f;
                light.shadowNearPlane = .12f;
                light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
                lighting.floodlights[i] = light;
                EditorUtility.SetDirty(light);
            }
            // URP must actually render the four additional lights and their shadows.
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!asset)
                    continue;
                var data = new SerializedObject(asset);
                data.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
                data.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = Mathf.Max(4, data.FindProperty("m_AdditionalLightsPerObjectLimit").intValue);
                data.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            lighting.Apply(lighting.selectedNight);
            EditorUtility.SetDirty(lighting);
            EditorUtility.SetDirty(sun);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "BotMatch saved with two sky materials, a morning sun and four shadow-casting corner spotlights.";
        }
        static Material SkyMaterial(string image, string name, float exposure, float rotation)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(image);
            if (!importer)
                throw new FileNotFoundException(image);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat;
            importer.wrapModeV = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Panoramic");
            if (!shader)
                throw new InvalidOperationException("Panoramic sky shader missing.");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(image));
            material.SetFloat("_Mapping", 1);
            material.SetFloat("_ImageType", 0);
            material.SetFloat("_Exposure", exposure);
            material.SetFloat("_Rotation", rotation);
            material.SetColor("_Tint", new Color(.5f, .5f, .5f, .5f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
        static Material LampMaterial(string name, bool glowing)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", glowing ? new Color(.9f, .96f, 1) : new Color(.64f, .69f, .72f));
            material.SetFloat("_Metallic", .12f);
            material.SetFloat("_Smoothness", .3f);
            if (glowing)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(.55f, .70f, 1) * 2.5f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }
        public static string DeleteUnusedSkyPackAssets()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play first.");
            var keep = new HashSet<string> { DayImage, NightImage, SkyRoot + "/LICENSE", SkyRoot + "/README.md" };
            var files = Directory.GetFiles(SkyRoot, "*", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).Where(p => !p.EndsWith(".meta") && !keep.Contains(p)).ToArray();
            long bytes = 0;
            var deleted = new List<string>();
            foreach (var path in files)
            {
                if (!path.StartsWith(SkyRoot + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException(path);
                long size = new FileInfo(path).Length;
                if (!AssetDatabase.DeleteAsset(path))
                    throw new IOException("Cannot delete " + path);
                bytes += size;
                deleted.Add(path);
            }
            string report = Newtonsoft.Json.JsonConvert.SerializeObject(new { removedFiles = deleted.Count, removedBytes = bytes, keptImages = new[] { NightImage, DayImage }, deleted = deleted }, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("../UnityWork/MatchLighting/sky-cleanup.json", report);
            return report;
        }
    }
}
