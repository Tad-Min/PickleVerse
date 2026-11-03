using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace PickleVerse.LocalMatch.Editor
{
    // Explicit, offline asset/animation verification. No accounts or match results are created.
    public static class AudienceVerification
    {
        public static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/audience-20261031"));

        public static object Verify()
        {
            var checks = new List<string>();
            void Check(bool value, string name)
            {
                if (!value) throw new InvalidOperationException("Audience check failed: " + name);
                checks.Add(name);
            }
            var rows = new List<object>();
            foreach (string key in new[] { "Professional", "LowPoly" })
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/Resources/Audience/" + key + ".prefab");
                var court = PrefabUtility.LoadPrefabContents("Assets/Resources/Courts/" + key + ".prefab");
                try
                {
                    var crowd = root.GetComponent<StadiumAudience>();
                    var fans = root.GetComponentsInChildren<AudienceSpectator>(true);
                    var seats = AudienceInstaller.SeatSurfaces(key == "Professional" ? court : root);
                    Check(fans.Length == (key == "Professional" ? 48 : 32), key + " intended audience count");
                    Check(fans.Select(f => f.visual.name + ":" + AssetDatabase.GetAssetPath(f.cheering)).Distinct().Count() == 4, key + " all four supplied characters");
                    Check(fans.Select(f => f.supportedTeam).Distinct().Count() == 2, key + " both teams have supporters");
                    Check(fans.All(f => seats.Any(p => Vector3.Distance(p, f.transform.position) < .06f)), key + " every fan on a real chair");
                    Check(fans.All(f => Vector3.Dot(f.transform.forward, -new Vector3(f.transform.position.x, 0, f.transform.position.z).normalized) > .35f), key + " chairs face play");
                    Check(root.GetComponentsInChildren<Collider>(true).Length == 0, key + " crowd adds no gameplay colliders");
                    Check(root.GetComponentsInChildren<Renderer>(true).All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.isSupported)), key + " valid materials");
                    Check(fans.All(f => new[] { f.cheering, f.pointing, f.disappointed, f.angry }.All(clip => clip &&
                        AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).All(b => string.IsNullOrEmpty(b.path) || f.animator.transform.Find(b.path)))), key + " animation paths bind to supplied skeletons");
                    crowd.Initialize();
                    var initial = fans.SelectMany(f => f.visual.GetComponentsInChildren<Transform>(true)).Select(t => t.localRotation).ToArray();
                    crowd.Advance(.6f, true);
                    var animated = fans.SelectMany(f => f.visual.GetComponentsInChildren<Transform>(true)).Select(t => t.localRotation).ToArray();
                    Check(initial.Zip(animated, Quaternion.Angle).Sum() > 100, key + " cheering actually animates bones");
                    Check(fans.All(f => f.IsAnimating && f.Emotion == AudienceEmotion.Cheering), key + " default cheering");
                    crowd.React(0, false, false); crowd.Advance(.7f, true);
                    Check(fans.All(f => f.Emotion == AudienceEmotion.Cheering), key + " side-out without a point keeps cheering");
                    crowd.React(0, true, false); crowd.Advance(.7f, true);
                    Check(fans.Where(f => f.supportedTeam == 0).All(f => f.Emotion == AudienceEmotion.Pointing), key + " scoring supporters point");
                    Check(fans.Where(f => f.supportedTeam == 1).Any(f => f.Emotion == AudienceEmotion.Angry) &&
                          fans.Where(f => f.supportedTeam == 1).Any(f => f.Emotion == AudienceEmotion.Disappointed), key + " losing supporters show both reactions");
                    float maxDrift = 0;
                    for (int i = 0; i < 200; i++)
                    {
                        crowd.Advance(.05f, true);
                        maxDrift = Mathf.Max(maxDrift, fans.Max(f => Vector3.Distance(f.pelvis.position, f.transform.TransformPoint(new Vector3(0, .095f, 0)))));
                    }
                    Check(maxDrift < .005f, key + " gestures keep pelvis anchored to seats");
                    Check(fans.All(f => f.Emotion == AudienceEmotion.Cheering), key + " temporary reactions return to cheering");
                    crowd.React(1, true, true); crowd.Advance(.7f, true); crowd.Advance(12, true);
                    Check(fans.Where(f => f.supportedTeam == 0).All(f => f.Emotion == AudienceEmotion.Angry || f.Emotion == AudienceEmotion.Disappointed), key + " defeat reaction persists after match");
                    crowd.Initialize();
                    Check(fans.All(f => f.Emotion == AudienceEmotion.Cheering), key + " replaying match resets reactions");
                    rows.Add(new { venue = key, spectators = fans.Length, seats = seats.Count, mobileSpectators = fans.Count(f => f.showOnMobile), maxDrift });
                }
                finally { PrefabUtility.UnloadPrefabContents(root); PrefabUtility.UnloadPrefabContents(court); }
            }
            foreach (string path in new[] { "Assets/Scenes/BotMatch.unity", "Assets/Scenes/MultiplayerCourt.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var selection = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CourtVenueSelection>(true)).Single();
                    Check(selection.includeAudience, path + " enables audience across venue changes");
                    Check(selection.current.GetComponentsInChildren<StadiumAudience>(true).Length == 1, path + " exactly one saved audience");
                    Check(selection.current.GetComponentInChildren<StadiumAudience>(true).gameObject.scene == scene, path + " audience belongs to gameplay scene");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            var result = new { passed = checks.Count, checks, venues = rows };
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "asset-animation-checks.json"), JsonUtilitySafe(result));
            return result;
        }

        static string JsonUtilitySafe(object value) => Newtonsoft.Json.JsonConvert.SerializeObject(value, Newtonsoft.Json.Formatting.Indented);

        const string RuntimeScene = "Assets/Temp/AudienceVerification20261031.unity";
        const string SetupKey = "PickleVerse.AudienceVerification.SceneSetup";

        public static void PreparePlayCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Already playing.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes before running this explicit probe.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RuntimeScene)) throw new InvalidOperationException("A runtime fixture already exists; restore its scene setup first.");
            SessionState.SetString(SetupKey, JsonUtilitySafe(EditorSceneManager.GetSceneManagerSetup()));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Audience/Professional.prefab"));
            Directory.CreateDirectory("Assets/Temp");
            EditorSceneManager.SaveScene(scene, RuntimeScene);
            EditorApplication.isPlaying = true;
        }

        public static object RunPlayChecks()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start the isolated audience runtime probe first.");
            var checks = new List<string>();
            void Check(bool value, string name) { if (!value) throw new InvalidOperationException("Runtime audience check failed: " + name); checks.Add(name); }
            // AccountScreen deliberately redirects unauthenticated startup scenes to
            // Menu. Create the offline probe additively after that guard has completed.
            var scene = SceneManager.CreateScene("Audience Runtime Probe");
            var crowd = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Audience/Professional")).GetComponent<StadiumAudience>();
            SceneManager.MoveGameObjectToScene(crowd.gameObject, scene);
            var fans = crowd.GetComponentsInChildren<AudienceSpectator>();
            Check(fans.Length == 48 && fans.All(f => f.IsAnimating), "OnEnable creates all playable graphs in Play mode");
            StadiumAudience.Restart(scene);
            StadiumAudience.Rally(scene, 0, true, false); crowd.Advance(.7f, true);
            Check(fans.Where(f => f.supportedTeam == 0).All(f => f.Emotion == AudienceEmotion.Pointing), "scene-routed scoring event reaches spectators");
            for (int i = 0; i < 220; i++) crowd.Advance(.05f, true);
            Check(fans.All(f => f.Emotion == AudienceEmotion.Cheering), "live crowd returns to cheering");
            var other = SceneManager.CreateScene("Other audience scene");
            var venue = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Courts/LowPoly")).GetComponent<StadiumEnvironment>();
            SceneManager.MoveGameObjectToScene(venue.gameObject, other);
            try
            {
                StadiumAudience.Ensure(venue, CourtVenue.LowPoly); StadiumAudience.Ensure(venue, CourtVenue.LowPoly);
                var second = venue.GetComponentInChildren<StadiumAudience>();
                Check(second && second.SpectatorCount == 32 && second.gameObject.scene == other, "venue-created crowd stays in the correct additive scene");
                Check(venue.GetComponentsInChildren<StadiumAudience>().Length == 1, "rebinding venue does not duplicate crowd");
                StadiumAudience.Rally(other, 1, true, true); second.Advance(.7f, true);
                Check(fans.All(f => f.Emotion == AudienceEmotion.Cheering), "another court cannot change the current audience");
                Check(second.GetComponentsInChildren<AudienceSpectator>().Where(f => f.supportedTeam == 0).All(f => f.Emotion == AudienceEmotion.Angry || f.Emotion == AudienceEmotion.Disappointed), "second court receives its own defeat event");
            }
            finally { venue.gameObject.SetActive(false); UnityEngine.Object.Destroy(venue.gameObject); SceneManager.UnloadSceneAsync(other); }
            StadiumAudience.Rally(scene, 1, true, true); crowd.Advance(.7f, true);
            for (int i = 0; i < 220; i++) crowd.Advance(.05f, true);
            Check(fans.Where(f => f.supportedTeam == 0).All(f => f.Emotion == AudienceEmotion.Angry || f.Emotion == AudienceEmotion.Disappointed), "match defeat persists while result is displayed");
            crowd.gameObject.SetActive(false);
            Check(fans.All(f => !f.IsAnimating), "disabled court releases playable graphs");
            crowd.gameObject.SetActive(true);
            Check(fans.All(f => f.IsAnimating && f.Emotion == AudienceEmotion.Cheering), "reactivated court rebuilds cheering animation");
            var timings = new double[240];
            for (int i = 0; i < timings.Length; i++)
            {
                var timer = System.Diagnostics.Stopwatch.StartNew(); crowd.Advance(1f / 30, true); timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(timings);
            var result = new { passed = checks.Count, checks, animationCpu = new { spectators = fans.Length, samples = timings.Length, p50Ms = timings[timings.Length / 2], p95Ms = timings[(int)(timings.Length * .95)], maxMs = timings.Last(), scope = "Editor CPU, all 48 poses forced visible; excludes rendering, networking and device FPS" } };
            File.WriteAllText(Path.Combine(Output, "playmode-checks.json"), JsonUtilitySafe(result));
            return result;
        }

        public static void RestoreAfterPlayCheck()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
            string setup = SessionState.GetString(SetupKey, "");
            if (string.IsNullOrEmpty(setup)) throw new InvalidOperationException("No saved scene setup.");
            EditorSceneManager.RestoreSceneManagerSetup(Newtonsoft.Json.JsonConvert.DeserializeObject<SceneSetup[]>(setup));
            AssetDatabase.DeleteAsset(RuntimeScene);
            SessionState.EraseString(SetupKey);
        }

        public static object Render(string venue, string mood = "cheering", bool close = false, bool night = false, int forceLod = -1, bool originalSample = false)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Render in Edit mode.");
            var previous = SceneManager.GetActiveScene();
            var previousLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
            foreach (var light in previousLights) light.enabled = false;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var stadium = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/" + venue + ".prefab"));
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Audience/" + venue + ".prefab"), stadium.transform);
                var crowd = root.GetComponent<StadiumAudience>(); crowd.Initialize();
                if (originalSample)
                {
                    // One source character only, so comparing a million-triangle source
                    // never accidentally populates the entire stadium with that mesh.
                    var fans = root.GetComponentsInChildren<AudienceSpectator>();
                    var sample = fans.OrderBy(f => Vector3.Distance(f.transform.position, new Vector3(-6.72f, .76f, -3))).First();
                    foreach (var fan in fans) if (fan != sample) fan.gameObject.SetActive(false);
                    string model = sample.cheering.name.StartsWith("dân") ? "PlayBoy" : sample.cheering.name.StartsWith("nam") ? "Wibu" : sample.cheering.name.StartsWith("nữ-T1") ? "T1-girl" : "YellowHairGirl";
                    sample.DisposeGraph(); UnityEngine.Object.DestroyImmediate(sample.GetComponent<LODGroup>());
                    UnityEngine.Object.DestroyImmediate(sample.visual.gameObject);
                    sample.visual = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/" + model + ".glb"), sample.transform).transform;
                    sample.pelvis = sample.visual.GetComponentsInChildren<Transform>().Single(t => t.name == "pelvis");
                    sample.animator = sample.pelvis.parent.GetComponent<Animator>();
                    if (!sample.animator) sample.animator = sample.pelvis.parent.gameObject.AddComponent<Animator>();
                    sample.Initialize();
                    foreach (var skin in sample.GetComponentsInChildren<SkinnedMeshRenderer>()) { skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true; }
                }
                if (forceLod >= 0) foreach (var lod in root.GetComponentsInChildren<LODGroup>()) lod.ForceLOD(forceLod);
                if (mood != "cheering") { crowd.React(mood == "defeat" ? 1 : 0, true, mood == "defeat"); crowd.Advance(1.1f, true); }
                var sun = new GameObject("Audience preview sun", typeof(Light)).GetComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = night ? 0 : 1.1f;
                sun.transform.rotation = Quaternion.Euler(45, -25, 0);
                RenderSettings.sun = sun;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = night ? MatchEnvironmentLighting.NightAmbientSky : new Color(.52f, .63f, .76f);
                RenderSettings.ambientEquatorColor = night ? MatchEnvironmentLighting.NightAmbientEquator : new Color(.34f, .40f, .48f);
                RenderSettings.ambientGroundColor = night ? MatchEnvironmentLighting.NightAmbientGround : new Color(.2f, .23f, .25f);
                RenderSettings.ambientIntensity = 1;
                stadium.GetComponent<StadiumEnvironment>().ApplyNight(night ? 1 : 0, true, .2f);
                var camera = new GameObject("Audience preview camera", typeof(Camera)).GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                // A render must exclude the user's still-open BotMatch scene, otherwise
                // its saved audience overlaps the preview and produces false z-fighting.
                foreach (var sceneRoot in scene.GetRootGameObjects())
                    foreach (var transform in sceneRoot.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                camera.cullingMask = 1 << 31; sun.cullingMask = 1 << 31;
                camera.fieldOfView = close ? 46 : 54; camera.nearClipPlane = .1f; camera.farClipPlane = 150;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .14f, .19f);
                camera.transform.position = close ? new Vector3(-2.4f, 3.1f, -3.1f) : new Vector3(18, 13, -23);
                camera.transform.LookAt(close ? new Vector3(-7.4f, 1.65f, 0) : new Vector3(0, .4f, 0));
                var texture = new RenderTexture(1440, 960, 24);
                var image = new Texture2D(1440, 960, TextureFormat.RGB24, false);
                var priorTarget = RenderTexture.active;
                try
                {
                    camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0, 0, 1440, 960), 0, 0); image.Apply();
                    Directory.CreateDirectory(Output);
                    string path = Path.Combine(Output, venue + "-" + mood + (close ? "-close" : "-overview") + (night ? "-night" : "") + (forceLod >= 0 ? "-lod" + forceLod : "") + (originalSample ? "-original" : "") + ".png");
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    return new { path, spectators = crowd.SpectatorCount };
                }
                finally { camera.targetTexture = null; RenderTexture.active = priorTarget; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image); }
            }
            finally
            {
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true);
                foreach (var light in previousLights) if (light) light.enabled = true;
            }
        }
    }
}
