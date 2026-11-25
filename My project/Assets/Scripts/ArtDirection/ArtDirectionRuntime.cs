using System.Collections.Generic;
using PickleVerse.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PickleVerse.ArtDirection
{
    /// <summary>
    /// Applies the Art Bible Look of the active scene (presentation only; no gameplay, network or timing effect):
    /// global Volumes (base, venue night, cinematic, low-quality overrides), post-processing and anti-aliasing on
    /// the scene's 3D cameras that render to the screen, SSAO per graphics preset, and the shader globals of the
    /// stylized lighting model (cool shadow tint, warm rim, ambient scale, wind, wetness).
    /// UI-only scenes have no Look and are left untouched, so canvases are never tonemapped.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class ArtDirectionRuntime : MonoBehaviour
    {
        static readonly int ShadowTintId = Shader.PropertyToID("_PV_ShadowTint"), RimTintId = Shader.PropertyToID("_PV_RimTint"),
            WindId = Shader.PropertyToID("_PV_WindParams"), AmbientId = Shader.PropertyToID("_PV_AmbientBoost"),
            WetnessId = Shader.PropertyToID("_PV_Wetness"), LookOnId = Shader.PropertyToID("_PV_LookOn"),
            NightGlowId = Shader.PropertyToID("_PV_NightGlow");

        public static ArtDirectionRuntime Instance { get; private set; }
        /// <summary>Look applied to the active scene, or null for UI-only scenes.</summary>
        public ArtDirectionLooks.Look Current { get; private set; }
        public int Tier { get; private set; } = -1;

        ArtDirectionLooks looks;
        Volume baseVolume, nightVolume, cinematicVolume, focusVolume, qualityVolume;
        DepthOfField focusDof;
        float focusStart, focusEnd, focus;
        bool closeUp;
        readonly HashSet<Camera> configured = new HashSet<Camera>();
        readonly Dictionary<ScriptableRendererFeature, bool> ssaoOriginal = new Dictionary<ScriptableRendererFeature, bool>();
        float cinematicTarget;
        string appliedScene, appliedSceneForCinematic;
        static string heldScene;
        GameObject atmosphere;
        int appliedMsaa = -1;

        /// <summary>0 = Low (grading only), 1 = Medium (+ bloom, vignette), 2 = High (+ SSAO, cinematic depth of field).</summary>
        public static int QualityTier(int graphics, bool mobile) => mobile ? 0 : Mathf.Clamp(graphics, 0, 2);

        /// <summary>Anti-aliasing for a camera whose MSAA is off: SMAA on High, FXAA on Medium, none on Low.</summary>
        public static AntialiasingMode PostAntialiasing(int tier, int msaa) => msaa > 1 ? AntialiasingMode.None :
            tier >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : tier == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;

        /// <summary>
        /// Keeps a scene's look while its venue outlives the scene (the online result keeps the court after Fusion unloads
        /// it). Pass null to return to the active scene's look.
        /// </summary>
        public static void HoldLook(string sceneName)
        {
            heldScene = string.IsNullOrEmpty(sceneName) ? null : sceneName;
            if (Instance) Instance.Refresh(true);
        }

        /// <summary>Look scene for an active scene while <paramref name="held"/> may override it (pure, unit-tested).</summary>
        public static string LookScene(string active, string held) => string.IsNullOrEmpty(held) ? active : held;

        /// <summary>Seconds the depth of field takes to come in on a close-up (and to leave when the shot widens).</summary>
        public const float FocusSeconds = .5f;

        /// <summary>
        /// Blends in the cinematic look for intros, victories and replays' hero shots. The shallow depth of field belongs to
        /// close-ups only: wide shots and gameplay always show the whole court sharp, so ending a cinematic drops it at once.
        /// </summary>
        public static void SetCinematic(bool on, bool closeUp = false)
        {
            if (!Instance) return;
            Instance.cinematicTarget = on ? 1 : 0;
            Instance.closeUp = on && closeUp;
            if (!on) Instance.ApplyFocus(0);
        }

        /// <summary>
        /// Blur start and end distances at a focus amount (0 = everything sharp, 1 = authored). Distances recede
        /// geometrically, so the blur slides in and out instead of popping when the effect switches on.
        /// </summary>
        public static Vector2 FocusDistances(float start, float end, float focus)
        {
            float scale = Mathf.Pow(40, 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(focus)));
            return new Vector2(start * scale, end * scale);
        }

        void ApplyFocus(float amount)
        {
            focus = amount;
            if (!focusDof) return;
            focusVolume.weight = amount > 0 ? 1 : 0;
            var distances = FocusDistances(focusStart, focusEnd, amount);
            focusDof.gaussianStart.Override(distances.x);
            focusDof.gaussianEnd.Override(distances.y);
        }

        // Depth of field runs in its own volume: its mode cannot blend, so it is faded through its distances. Both volumes use
        // runtime copies; the project's Look_Cinematic asset is never written.
        void SetUpFocus()
        {
            if (!cinematicVolume.sharedProfile || !cinematicVolume.profile.TryGet<DepthOfField>(out var authored)) return;
            focusStart = authored.gaussianStart.value;
            focusEnd = authored.gaussianEnd.value;
            focusDof = focusVolume.profile.Add<DepthOfField>();
            focusDof.mode.Override(authored.mode.value);
            focusDof.gaussianMaxRadius.Override(authored.gaussianMaxRadius.value);
            focusDof.highQualitySampling.Override(authored.highQualitySampling.value);
            authored.active = false;
            ApplyFocus(0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
#if UNITY_SERVER
            return;
#else
            if (Application.isBatchMode || Instance) return;
            if (!Resources.Load<ArtDirectionLooks>(ArtDirectionLooks.ResourcePath)) return;
            var go = new GameObject("Art Direction");
            DontDestroyOnLoad(go);
            go.AddComponent<ArtDirectionRuntime>();
#endif
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            looks = Resources.Load<ArtDirectionLooks>(ArtDirectionLooks.ResourcePath);
            baseVolume = CreateVolume("Look", 0);
            nightVolume = CreateVolume("Look Night", 1);
            cinematicVolume = CreateVolume("Look Cinematic", 2);
            focusVolume = CreateVolume("Look Focus", 3);
            qualityVolume = CreateVolume("Look Quality", 10);
            if (looks)
            {
                cinematicVolume.sharedProfile = looks.cinematicProfile;
                SetUpFocus();
                qualityVolume.sharedProfile = looks.lowQualityProfile;
                foreach (var data in looks.ssaoRenderers)
                    if (data)
                        foreach (var feature in data.rendererFeatures)
                            if (feature && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion") && !ssaoOriginal.ContainsKey(feature))
                                ssaoOriginal.Add(feature, feature.isActive);
            }
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Refresh(true);
        }

        void OnActiveSceneChanged(Scene previous, Scene next) => Refresh(true);

        Volume CreateVolume(string name, float priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = 0;
            return volume;
        }

        void Refresh(bool force)
        {
            var scene = LookScene(SceneManager.GetActiveScene().name, heldScene);
            int tier = QualityTier(GameSettings.Current.graphics, MobileRenderPolicy.Enabled);
            int msaa = GameSettings.Current.msaa;
            if (!force && scene == appliedScene && tier == Tier && msaa == appliedMsaa)
                return;
            appliedScene = scene;
            Tier = tier;
            appliedMsaa = msaa;
            configured.Clear();
            if (scene != appliedSceneForCinematic)
            {
                // A cinematic never leaks into the next scene (victory screens end with a scene change).
                appliedSceneForCinematic = scene;
                cinematicTarget = 0;
                cinematicVolume.weight = 0;
                closeUp = false;
                ApplyFocus(0);
            }
            Current = looks ? looks.Find(scene) : null;
            bool post = Current != null && Current.postProcess;
            baseVolume.sharedProfile = Current?.profile;
            baseVolume.weight = Current?.profile ? 1 : 0;
            nightVolume.sharedProfile = Current?.nightProfile;
            qualityVolume.weight = post && tier == 0 && qualityVolume.sharedProfile ? 1 : 0;
            SetSsao(post && tier >= 2);
            SpawnAtmosphere(Current != null && tier >= 1 ? Current.atmosphere : null);
            if (Current != null)
            {
                Shader.SetGlobalVector(ShadowTintId, Current.shadowTint);
                Shader.SetGlobalVector(RimTintId, Current.rimTint);
                Shader.SetGlobalVector(AmbientId, Current.ambientBoost);
                Shader.SetGlobalVector(WindId, Current.wind);
                Shader.SetGlobalFloat(WetnessId, Current.wetness);
                Shader.SetGlobalFloat(LookOnId, 1);
            }
            else
                Shader.SetGlobalFloat(LookOnId, 0);
        }

        void SpawnAtmosphere(GameObject prefab)
        {
            if (atmosphere && atmosphere.scene == SceneManager.GetActiveScene() && atmosphere.name == (prefab ? prefab.name : null))
                return;
            if (atmosphere)
                Destroy(atmosphere);
            atmosphere = null;
            if (!prefab)
                return;
            atmosphere = Instantiate(prefab);
            atmosphere.name = prefab.name;
            SceneManager.MoveGameObjectToScene(atmosphere, SceneManager.GetActiveScene());
        }

        void SetSsao(bool on)
        {
            foreach (var pair in ssaoOriginal)
                if (pair.Key && pair.Key.isActive != (on && pair.Value))
                    pair.Key.SetActive(on && pair.Value);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (Current == null || camera.cameraType != CameraType.Game || camera.targetTexture)
                return;
            var id = camera;
            if (configured.Contains(id))
                return;
            configured.Add(id);
            var data = camera.GetUniversalAdditionalCameraData();
            if (!data || data.renderType != CameraRenderType.Base)
                return;
            data.renderPostProcessing = Current.postProcess;
            data.antialiasing = Current.postProcess ? PostAntialiasing(Tier, appliedMsaa) : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.Medium;
        }

        void LateUpdate()
        {
            Refresh(false);
            float dt = Time.unscaledDeltaTime;
            if (nightVolume.sharedProfile)
                nightVolume.weight = Mathf.Clamp01(Shader.GetGlobalFloat(NightGlowId));
            else
                nightVolume.weight = 0;
            float target = Current != null && Current.postProcess && Tier >= 1 ? cinematicTarget : 0;
            cinematicVolume.weight = Mathf.MoveTowards(cinematicVolume.weight, target, dt / .35f);
            float focusTarget = target > 0 && closeUp ? 1 : 0;
            if (focus != focusTarget)
                ApplyFocus(Mathf.MoveTowards(focus, focusTarget, dt / FocusSeconds));
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            // Renderer data are project assets: give SSAO back its authored state (matters in Editor Play Mode).
            foreach (var pair in ssaoOriginal)
                if (pair.Key && pair.Key.isActive != pair.Value)
                    pair.Key.SetActive(pair.Value);
            Shader.SetGlobalFloat(LookOnId, 0);
            Instance = null;
        }
    }
}
