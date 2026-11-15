using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using PickleVerse.LocalMatch;
using PickleVerse.Settings;
using Newtonsoft.Json;
using PickleVerse;

// Editor-only before/after evidence for the golden-hour district. Explicit commands only:
// no runtime bootstrap, no backend calls, no player data. Output goes to the ignored BlenderWork folder.
public static class SunsetDistrictProbe
{
    static readonly string Work = Path.GetFullPath("../BlenderWork/SunsetDistrict20261115");
    static readonly string[] Keys = { "Professional", "LowPoly" };
    const string DaySky = "Assets/LocalMatchContent/Lighting/BlueSunset.mat", NightSky = "Assets/LocalMatchContent/Lighting/MoonBurst.mat";

    static string Out(string name)
    {
        Directory.CreateDirectory(Work);
        return Path.Combine(Work, name);
    }

    // ---------------------------------------------------------------- inventory (Edit mode)
    static long FileSize(string assetPath) => File.Exists(assetPath) ? new FileInfo(assetPath).Length : 0;

    public static object Inventory(string label)
    {
        var rows = new List<object>();
        foreach (string key in Keys)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/" + key + ".prefab");
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.activeInHierarchy || ActiveInPrefab(r.transform, root.transform)).ToArray();
            var groups = root.GetComponentsInChildren<LODGroup>(true);
            var lodOf = new Dictionary<Renderer, int>();
            foreach (var g in groups)
            {
                var lods = g.GetLODs();
                for (int i = 0; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers)
                        if (r) lodOf[r] = i;
            }
            long Tris(MeshRenderer r) { var f = r.GetComponent<MeshFilter>(); return f && f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0; }
            var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
            var meshes = renderers.Select(r => r.GetComponent<MeshFilter>()).Where(f => f && f.sharedMesh).Select(f => f.sharedMesh).Distinct().ToArray();
            var textures = new HashSet<Texture>();
            foreach (var m in materials)
                foreach (string property in m.GetTexturePropertyNames())
                {
                    var t = m.GetTexture(property);
                    if (t) textures.Add(t);
                }
            var sources = AssetDatabase.GetDependencies("Assets/Resources/Courts/" + key + ".prefab", true)
                .Where(p => p.EndsWith(".glb") || p.EndsWith(".fbx") || p.EndsWith(".png") || p.EndsWith(".jpg") || p.EndsWith(".asset") || p.EndsWith(".mat") || p.EndsWith(".shader")).ToArray();
            rows.Add(new
            {
                venue = key,
                gameObjects = root.GetComponentsInChildren<Transform>(true).Length,
                renderers = renderers.Length,
                lodGroups = groups.Length,
                colliders = root.GetComponentsInChildren<Collider>(true).Length,
                behaviours = root.GetComponentsInChildren<MonoBehaviour>(true).Length,
                lights = root.GetComponentsInChildren<Light>(true).Length,
                trianglesAllLods = renderers.Sum(Tris),
                trianglesNoLodOrLod0 = renderers.Where(r => !lodOf.TryGetValue(r, out int l) || l == 0).Sum(Tris),
                trianglesNoLodOrLod1 = renderers.Where(r => !lodOf.TryGetValue(r, out int l) || l == 1).Sum(Tris),
                trianglesNoLod = renderers.Where(r => !lodOf.ContainsKey(r)).Sum(Tris),
                submeshDraws = renderers.Sum(r => r.sharedMaterials.Length),
                uniqueMeshes = meshes.Length,
                uniqueMeshTriangles = meshes.Sum(m => (long)m.triangles.Length / 3),
                uniqueMaterials = materials.Length,
                shaders = materials.Select(m => m.shader ? m.shader.name : "<none>").GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count()),
                shadowCasters = renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off),
                textures = textures.Select(t => new { t.name, t.width, t.height, format = (t as Texture2D) ? ((Texture2D)t).format.ToString() : t.GetType().Name, runtimeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t), path = AssetDatabase.GetAssetPath(t) }).OrderBy(t => t.name).ToArray(),
                textureRuntimeBytes = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)),
                sourceFiles = sources.Select(p => new { path = p, bytes = FileSize(p) }).OrderByDescending(p => p.bytes).ToArray(),
                sourceBytes = sources.Sum(FileSize),
                modelBytes = sources.Where(p => p.EndsWith(".glb") || p.EndsWith(".fbx")).Sum(FileSize),
                textureBytes = sources.Where(p => p.EndsWith(".png") || p.EndsWith(".jpg")).Sum(FileSize)
            });
        }
        var result = new { label, unity = Application.unityVersion, rows };
        File.WriteAllText(Out(label + "-inventory.json"), JsonConvert.SerializeObject(result, Formatting.Indented));
        return result;
    }
    static bool ActiveInPrefab(Transform t, Transform root)
    {
        for (; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == root) break;
        }
        return true;
    }

    // ---------------------------------------------------------------- pictures (Edit mode)
    static void Shot(Camera c, string file, int width, int height)
    {
        var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear };
        var target = RenderTexture.GetTemporary(descriptor);
        var old = c.targetTexture;
        var active = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            c.targetTexture = target;
            c.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Out(file), texture.EncodeToPNG());
        }
        finally
        {
            c.targetTexture = old;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    public static object Pictures(string label, int width = 1600, int height = 900, bool night = false)
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit mode required");
        string previous = SceneManager.GetActiveScene().path;
        var files = new List<string>();
        try
        {
            foreach (string key in Keys)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/BotMatch.unity");
                var lighting = UnityEngine.Object.FindAnyObjectByType<MatchEnvironmentLighting>();
                if (lighting.stadium) lighting.stadium.gameObject.SetActive(false);
                lighting.stadium = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Courts/" + key + ".prefab")).GetComponent<StadiumEnvironment>();
                var c = lighting.matchCamera;
                float originalFar = c.farClipPlane, originalNear = c.nearClipPlane;
                foreach (bool dark in night ? new[] { false, true } : new[] { false })
                {
                    lighting.SelectForMatch(dark);
                    string suffix = dark ? "-Night" : "";
                    // Production gameplay framing from both ends.
                    CourtFollowView.Apply(c, new Vector3(1.45f, 0, -7.25f), -1, false, 0, true);
                    Shot(c, label + "-" + key + "-Gameplay" + suffix + ".png", width, height); files.Add(label + "-" + key + "-Gameplay" + suffix);
                    CourtFollowView.Apply(c, new Vector3(-1.45f, 0, 7.25f), 1, false, 0, true);
                    Shot(c, label + "-" + key + "-GameplayFarEnd" + suffix + ".png", width, height);
                    c.transform.position = new Vector3(4.1f, 1.55f, -8.8f); c.transform.LookAt(new Vector3(-1.2f, 1.0f, 2.5f)); c.fieldOfView = 55;
                    Shot(c, label + "-" + key + "-Courtside" + suffix + ".png", width, height);
                    c.transform.position = new Vector3(24, 8, -24); c.transform.LookAt(new Vector3(5, 1, -3)); c.fieldOfView = 57;
                    Shot(c, label + "-" + key + "-Aerial" + suffix + ".png", width, height);
                    c.transform.position = new Vector3(-34, 17, -38); c.transform.LookAt(new Vector3(0, 0, 4)); c.fieldOfView = 50;
                    Shot(c, label + "-" + key + "-Establishing" + suffix + ".png", width, height);
                    c.transform.position = new Vector3(-9.5f, 1.7f, -13.5f); c.transform.LookAt(new Vector3(-14.5f, 3.2f, -4f)); c.fieldOfView = 52;
                    Shot(c, label + "-" + key + "-Trees" + suffix + ".png", width, height);
                    c.transform.position = new Vector3(0, 1.75f, -3.2f); c.transform.LookAt(new Vector3(0.4f, 1.9f, 30f)); c.fieldOfView = 60;
                    Shot(c, label + "-" + key + "-PlayerEye" + suffix + ".png", width, height);
                }
                c.farClipPlane = originalFar; c.nearClipPlane = originalNear;
            }
            return new { folder = Work, label };
        }
        finally
        {
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }

    // ---------------------------------------------------------------- live presentation regression (Play mode)
    // Same scenario as VenueRefinementPresentationChecks (actual Menu + BotMatch scenes, runtime venue switch,
    // entrance cinematic, local rules/ball simulation, outcome cameras, day/night), with end-of-frame screenshots.
    // Signed-out fixture: the caller removes the sign-in redirect from the temporary Play session only.
    static readonly List<object> checks = new List<object>(), presentationPerf = new List<object>();
    static readonly List<float> liveFrames = new List<float>();
    static bool liveRecord; static int liveFrame; static BotMatch manual; static Action visual;
    static void SaveLive(string status) => File.WriteAllText(Out("presentation-validation.json"), JsonConvert.SerializeObject(new { status, checks, performance = presentationPerf, errors }, Formatting.Indented));
    static void Check(string name, bool passed) { checks.Add(new { name, passed }); if (!passed) throw new Exception(name); }
    static void LiveTick()
    {
        if (!Application.isPlaying || Time.frameCount == liveFrame) return;
        liveFrame = Time.frameCount;
        if (manual) { manual.StepSimulation(Mathf.Min(Time.deltaTime, .04f), Vector2.zero, false); visual?.Invoke(); }
        if (liveRecord) liveFrames.Add(Time.unscaledDeltaTime * 1000);
    }
    static void BotLoaded(Scene s, LoadSceneMode mode)
    {
        if (s.name == "BotMatch") foreach (var root in s.GetRootGameObjects()) foreach (var m in root.GetComponentsInChildren<BotMatch>(true)) { m.enabled = false; if (m.opening) m.opening.enabled = false; }
    }
    static async Task LiveSample(string scene, string variant)
    {
        await Task.Delay(1500); liveFrames.Clear(); liveRecord = true; await Task.Delay(5000); liveRecord = false;
        var v = liveFrames.OrderBy(x => x).ToArray(); Check(scene + " " + variant + " produces rendered frames", v.Length >= 60);
        presentationPerf.Add(new { scene, variant, frames = v.Length, meanFps = 1000 / v.Average(), p95Ms = v[(int)((v.Length - 1) * .95)], maxMs = v.Last(), width = Screen.width, height = Screen.height });
        SaveLive("running");
    }
    static async Task LivePicture(string name) { ScreenCapture.CaptureScreenshot(Out("Live-" + name + ".png")); await Task.Delay(400); }
    static void Win(PickleVerse.Rules.SinglesRules rules, int winner, int end)
    {
        for (int i = 0; i < 60 && rules.Winner < 0; i++) { if (rules.GameFinished) rules.NextGame(); rules.PrepareServe(end); rules.Fail(1 - winner, PickleVerse.Rules.Fault.Out); rules.CommitRally(); }
        if (rules.Winner != winner) throw new Exception("Local score fixture failed");
    }
    public static object Presentation()
    {
        if (!Application.isPlaying) throw new Exception("Play mode required");
        if (task != null && !task.IsCompleted) throw new Exception("Probe already running");
        checks.Clear(); presentationPerf.Clear(); errors.Clear(); task = RunPresentation();
        return new { started = true, path = Out("presentation-validation.json") };
    }
    static async Task RunPresentation()
    {
        SaveLive("running"); string saved = JsonUtility.ToJson(GameSettings.Current);
        var hidden = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Application.logMessageReceived += Log; EditorApplication.update += LiveTick; SceneManager.sceneLoaded += BotLoaded;
        try
        {
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component.GetType().FullName == "PickleVerse.FrontEnd.AccountScreen") UnityEngine.Object.Destroy(component.gameObject);
            await Task.Delay(100); Time.timeScale = 1;
            var data = GameSettings.Current; data.fps = 240; data.vsync = false; data.renderScale = 1; data.msaa = 2; SettingsRuntime.Apply(data);
            var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")); view.Show(); view.Focus();
            SceneManager.LoadScene("Menu"); await Task.Delay(1600); Check("Menu loads actual lobby scene", SceneManager.GetActiveScene().name == "Menu");
            Check("Menu retains open sky", Camera.main && Camera.main.clearFlags == CameraClearFlags.Skybox); await LiveSample("Menu", "actual lobby"); await LivePicture("Menu");
            foreach (CourtVenue venue in new[] { CourtVenue.Professional, CourtVenue.LowPoly })
            {
                SceneManager.LoadScene("BotMatch"); await Task.Delay(600); var m = UnityEngine.Object.FindAnyObjectByType<BotMatch>(); Check(venue + " actual BotMatch created", m && m.Rules != null);
                var choice = m.GetComponent<CourtVenueSelection>(); Check(venue + " runtime venue switch", choice.Select(venue)); await Task.Delay(150);
                Check(venue + " district installed under the venue", choice.current.transform.Find("Sunset District") && choice.current.sunShadowMask);
                Check(venue + " no colliders added by decoration", choice.current.transform.Find("Sunset District").GetComponentsInChildren<Collider>(true).Length == 0);
                var intro = m.opening; Check(venue + " actual entrance cinematic starts", intro.Play());
                foreach (float time in new[] { 1.1f, 2.8f, 4.4f, 5.4f, 7.2f, 8.5f })
                {
                    intro.Seek(time); if (!intro.Stage.Contains("entrance")) continue; var actor = intro.Stage == "Opponent entrance" ? m.bot : m.human;
                    var p = Camera.main.WorldToViewportPoint(actor.transform.position + Vector3.up * .9f);
                    Check(venue + " entrance actor framed @" + time, p.z > 0 && p.x > .02f && p.x < .98f && p.y > .02f && p.y < .98f);
                    Check(venue + " entrance actor grounded @" + time, Mathf.Abs(actor.transform.position.y) < .001f);
                }
                await LivePicture(venue + "-Entrance"); intro.Skip(); m.autoPlayer = true;
                manual = m; visual = (Action)Delegate.CreateDelegate(typeof(Action), m, typeof(BotMatch).GetMethod("UpdateVisuals", hidden));
                var lighting = m.GetComponent<MatchEnvironmentLighting>();
                foreach (bool night in new[] { false, true })
                {
                    lighting.SelectForMatch(night); Check(venue + " white court lights " + night, lighting.stadium.poleIllumination.All(l => l.enabled && l.color == Color.white && l.intensity > 0));
                    Check(venue + " golden-hour sky " + night, night ? RenderSettings.skybox == lighting.nightSky : RenderSettings.skybox && RenderSettings.skybox.shader.name == "PickleVerse/Environment/Golden Hour Sky");
                    await LiveSample("BotMatch", venue + " " + (night ? "night" : "day")); await LivePicture(venue + (night ? "-Night" : "-Day"));
                }
                checks.Add(new { name = venue + " local simulation strikes ball", passed = m.TotalHits > 0, snapshot = m.Snapshot(), paused = m.Paused }); manual = null; visual = null;
                await Task.Delay(3500);
                foreach (int winner in new[] { 0, 1 })
                {
                    var rules = new PickleVerse.Rules.SinglesRules(winner); Win(rules, winner, m.human.end); typeof(BotMatch).GetProperty("Rules").SetValue(m, rules);
                    typeof(BotMatch).GetProperty("Phase").SetValue(m, MatchPhase.MatchOver);
                    Check(venue + " outcome starts " + winner, m.victory.Begin()); m.victory.Advance(4); await Task.Delay(250);
                    var p = Camera.main.WorldToViewportPoint(m.human.transform.position + Vector3.up * .9f);
                    Check(venue + " local outcome actor framed " + winner, m.victory.Ready && p.z > 0 && p.x > .05f && p.x < .95f && p.y > .05f && p.y < .95f);
                    Check(venue + " Vietnamese outcome " + winner, m.victory.winnerTitle.text == (winner == 0 ? "Thắng" : "Thua")); await LivePicture(venue + (winner == 0 ? "-Win" : "-Loss")); m.victory.Cancel();
                }
            }
            Check("No runtime errors in local presentation fixture", errors.Count == 0);
            SaveLive("passed");
        }
        catch (Exception error) { errors.Add(error.ToString()); SaveLive("failed"); }
        finally
        {
            manual = null; visual = null; liveRecord = false; EditorApplication.update -= LiveTick; Application.logMessageReceived -= Log; SceneManager.sceneLoaded -= BotLoaded;
            JsonUtility.FromJsonOverwrite(saved, GameSettings.Current); SettingsRuntime.Apply(GameSettings.Current);
        }
    }

    // ---------------------------------------------------------------- measured frames (Play mode)
    static Task task; static int lastFrame; static bool recording, moving; static float caseTime;
    static readonly List<float> values = new List<float>();
    static readonly List<double> gpu = new List<double>();
    static readonly List<int[]> stats = new List<int[]>();
    static readonly List<object> rows = new List<object>();
    static readonly List<string> errors = new List<string>();
    static readonly FrameTiming[] timings = new FrameTiming[1];
    static MatchActor[] actors; static string stage;

    static void Log(string condition, string trace, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(condition); }
    static void Tick()
    {
        if (!Application.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (actors != null && moving)
        {
            caseTime += Time.deltaTime;
            for (int i = 0; i < actors.Length; i++)
                if (actors[i]) actors[i].Step(new Vector2(Mathf.Cos(caseTime * 2 + i) * .2f, Mathf.Sin(caseTime * 2 + i) * .2f), false, Time.deltaTime);
        }
        if (!recording) return;
        values.Add(Time.unscaledDeltaTime * 1000);
        stats.Add(new[] { UnityStats.triangles, UnityStats.vertices, Batches(), UnityStats.drawCalls, UnityStats.setPassCalls, UnityStats.shadowCasters });
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0) gpu.Add(timings[0].gpuFrameTime);
    }
    // The batch counters were renamed between Editor versions; read whichever this Editor exposes.
    static int Batches()
    {
        int total = 0;
        foreach (string name in new[] { "batches", "dynamicBatches", "staticBatches", "instancedBatches" })
        {
            var property = typeof(UnityStats).GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (property == null) continue;
            total += Convert.ToInt32(property.GetValue(null));
            if (name == "batches") break;
        }
        return total;
    }
    static void Save(string status, object extra = null)
    {
        File.WriteAllText(Out(stage + "-performance.json"), JsonConvert.SerializeObject(new { status, rows, errors, extra }, Formatting.Indented));
    }
    static object Summary(float[] data)
    {
        Array.Sort(data);
        if (data.Length < 30) throw new Exception("Insufficient rendered frames");
        return new { frames = data.Length, meanFps = 1000 / data.Average(), medianMs = data[data.Length / 2], p95Ms = data[(int)((data.Length - 1) * .95)], p99Ms = data[(int)((data.Length - 1) * .99)], maxMs = data.Last(), over16ms = data.Count(x => x > 1000f / 60) };
    }
    static int Median(int column) { var sorted = stats.Select(s => s[column]).OrderBy(v => v).ToArray(); return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2]; }

    static async Task Empty()
    {
        // The sign-in screen otherwise redirects an empty Play session to Menu.
        // Remove UI only from this temporary fixture; nothing is written to disk.
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component.GetType().FullName == "PickleVerse.FrontEnd.AccountScreen") UnityEngine.Object.Destroy(component.gameObject);
        Time.timeScale = 1;
        await Task.Delay(700);
        var old = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var empty = SceneManager.CreateScene("SunsetDistrict isolated QA " + Guid.NewGuid().ToString("N").Substring(0, 6));
        SceneManager.SetActiveScene(empty);
        foreach (var scene in old) { var op = SceneManager.UnloadSceneAsync(scene); while (op != null && !op.isDone) await Task.Yield(); }
        await Task.Delay(250);
    }

    /// <summary>Play mode. label: "before" or "after". Writes label-performance.json when finished.</summary>
    public static object Measure(string label, bool includeNight = true)
    {
        if (!Application.isPlaying) throw new Exception("Play mode required");
        if (task != null && !task.IsCompleted) throw new Exception("Probe already running");
        stage = label; rows.Clear(); errors.Clear();
        task = Run(includeNight);
        return new { started = true, path = Out(label + "-performance.json") };
    }
    public static object State() => new { running = task != null && !task.IsCompleted, stage, rows = rows.Count, errors };

    static async Task Run(bool includeNight)
    {
        Save("running");
        var saved = JsonUtility.ToJson(GameSettings.Current);
        Camera cam = null; GameObject venue = null, host = null; Light sun = null; MatchRenderBudget budget = null;
        var originalMobile = MobileRenderPolicy.Preview;
        var oldSky = RenderSettings.skybox;
        Application.logMessageReceived += Log; EditorApplication.update += Tick;
        try
        {
            await Empty();
            var data = GameSettings.Current; data.fps = 240; data.vsync = false; data.renderScale = 1; data.msaa = 2;
            SettingsRuntime.Apply(data); Application.runInBackground = true;
            var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")); view.Show(); view.Focus();
            cam = new GameObject("District probe camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            cam.tag = "MainCamera"; cam.nearClipPlane = .1f; cam.farClipPlane = 100; cam.fieldOfView = 56; cam.clearFlags = CameraClearFlags.Skybox;
            // Same light the gameplay scenes author as "Match Sun"; the production lighting component may restyle it.
            sun = new GameObject("District probe sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1, .98f, .95f); sun.shadows = LightShadows.Soft; sun.shadowStrength = .8f; sun.shadowBias = .035f; sun.shadowNormalBias = .12f;
            sun.transform.rotation = new Quaternion(.44037747f, -.26672933f, .13885048f, .8459573f);
            host = new GameObject("District probe lighting"); host.SetActive(false);
            var lighting = host.AddComponent<MatchEnvironmentLighting>();
            lighting.daySky = AssetDatabase.LoadAssetAtPath<Material>(DaySky); lighting.nightSky = AssetDatabase.LoadAssetAtPath<Material>(NightSky);
            lighting.matchCamera = cam; lighting.sun = sun; lighting.useLocalLaunchSettings = false;
            budget = cam.gameObject.AddComponent<MatchRenderBudget>();
            actors = new MatchActor[4];
            for (int i = 0; i < 4; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Multiplayer/BotMatch/" + (i % 2 == 0 ? "Female" : "Male") + "Actor.prefab");
                if (!prefab) throw new Exception("Actor prefab absent");
                actors[i] = UnityEngine.Object.Instantiate(prefab).GetComponent<MatchActor>(); actors[i].Initialize(); actors[i].ResetAt(new Vector3(i % 2 == 0 ? -1.3f : 1.3f, 0, i < 2 ? -5 : 5), i < 2 ? -1 : 1);
            }
            moving = true;
            foreach (bool mobile in new[] { false, true })
            {
                MobileRenderPolicy.Preview = mobile; budget.enabled = false; SettingsRuntime.Apply(data); budget.enabled = true;
                foreach (string id in Keys)
                {
                    var prefab = Resources.Load<GameObject>("Courts/" + id);
                    if (!prefab) throw new Exception("Venue missing: " + id);
                    venue = UnityEngine.Object.Instantiate(prefab); var e = venue.GetComponent<StadiumEnvironment>();
                    lighting.stadium = e; host.SetActive(true);
                    foreach (bool night in includeNight ? new[] { false, true } : new[] { false })
                    {
                        lighting.SelectForMatch(night);
                        CourtFollowView.Apply(cam, new Vector3(1.45f, 0, -7.25f), -1, false, 0, true);
                        budget.enabled = false; budget.enabled = true; caseTime = 0;
                        for (int i = 0; i < 4; i++) actors[i].ResetAt(new Vector3(i % 2 == 0 ? -1.3f : 1.3f, 0, i < 2 ? -5 : 5), i < 2 ? -1 : 1);
                        await Task.Delay(3000);
                        caseTime = 0;
                        for (int i = 0; i < 4; i++) actors[i].ResetAt(new Vector3(i % 2 == 0 ? -1.3f : 1.3f, 0, i < 2 ? -5 : 5), i < 2 ? -1 : 1);
                        values.Clear(); gpu.Clear(); stats.Clear(); recording = true; await Task.Delay(5000); recording = false;
                        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                        var renderers = e.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                        rows.Add(new
                        {
                            venue = id, mobile, night, frames = Summary(values.ToArray()), gpuMs = gpu.Count > 0 ? (double?)gpu.Average() : null,
                            viewTriangles = Median(0), viewVertices = Median(1), batches = Median(2), drawCalls = Median(3), setPassCalls = Median(4), shadowCasters = Median(5),
                            venueRenderers = renderers.Length, venueSubmeshes = renderers.Sum(r => r.sharedMaterials.Length),
                            width = Screen.width, height = Screen.height, renderScale = pipeline.renderScale, budgetScale = budget.Scale, msaa = pipeline.msaaSampleCount,
                            shadowDistance = pipeline.shadowDistance, cascades = pipeline.shadowCascadeCount, hdr = pipeline.supportsHDR, maximumLod = QualitySettings.maximumLODLevel, lodBias = QualitySettings.lodBias, farClip = cam.farClipPlane,
                            frameBudget60 = values.Average() <= 1000f / 60 && values.OrderBy(v => v).ElementAt((int)((values.Count - 1) * .95)) <= 1000f / 60
                        });
                        Save("running");
                    }
                    host.SetActive(false);
                    UnityEngine.Object.Destroy(venue); venue = null; await Task.Delay(150);
                }
            }
            Save(errors.Count == 0 ? "passed" : "failed", new
            {
                hardware = SystemInfo.graphicsDeviceName, api = SystemInfo.graphicsDeviceType.ToString(), cpu = SystemInfo.processorType,
                scope = "Live Editor Game view, real venue prefabs, four moving actor prefabs, production MatchEnvironmentLighting and MatchRenderBudget, gameplay camera framing. No backend or online session. The mobile rows are the mobile render policy previewed on the desktop GPU, not a phone measurement."
            });
        }
        catch (Exception ex) { errors.Add(ex.ToString()); Save("failed"); }
        finally
        {
            recording = moving = false; EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            MobileRenderPolicy.Preview = originalMobile; JsonUtility.FromJsonOverwrite(saved, GameSettings.Current); SettingsRuntime.Apply(GameSettings.Current);
            if (venue) UnityEngine.Object.Destroy(venue); if (cam) UnityEngine.Object.Destroy(cam.gameObject); if (sun) UnityEngine.Object.Destroy(sun.gameObject); if (host) UnityEngine.Object.Destroy(host);
            if (actors != null) foreach (var a in actors) if (a) UnityEngine.Object.Destroy(a.gameObject);
            actors = null; RenderSettings.skybox = oldSky;
        }
    }
}
