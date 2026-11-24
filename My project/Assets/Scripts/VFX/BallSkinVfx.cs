using System.Collections.Generic;
using PickleVerse.LocalMatch;
using PickleVerse.Shop;
using UnityEngine;

namespace PickleVerse.VFX
{
    /// <summary>
    /// Gives the ball the signature effect of the paddle that last hit it (presentation only). Every client decides from
    /// the hitter's rendered paddle skin, which online play already replicates, so nothing new is sent over the network
    /// and rules, physics and replays are untouched. A contact by a paddle without a skin effect restores the standard trail.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BallSkinVfx : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static PaddleSkinVfxLibrary library;
        static bool libraryLoaded;

        TrailRenderer trail;
        Material defaultMaterial;
        float defaultTime, defaultWidth;
        AnimationCurve defaultWidthCurve;
        Gradient defaultColor;
        LineTextureMode defaultTextureMode;
        PaddleSkinVfxProfile active;
        readonly Dictionary<PaddleSkinVfxProfile, Transform> ballEffects = new Dictionary<PaddleSkinVfxProfile, Transform>();
        readonly Dictionary<PaddleSkinVfxProfile, ParticleSystem[]> impacts = new Dictionary<PaddleSkinVfxProfile, ParticleSystem[]>();
        MaterialPropertyBlock block;
        float fade;

        /// <summary>Skin whose effect the ball currently shows, or null for the standard trail.</summary>
        public string ActiveSkin => active ? active.skinId : null;

        public static PaddleSkinVfxLibrary Library
        {
            get
            {
                if (!libraryLoaded)
                {
                    library = Resources.Load<PaddleSkinVfxLibrary>(PaddleSkinVfxLibrary.ResourcePath);
                    libraryLoaded = true;
                }
                return library;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BallPresentationRegistry.BallRegistered -= Attach;
            libraryLoaded = false;
            library = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.isBatchMode)
                return;
            BallPresentationRegistry.BallRegistered -= Attach;
            BallPresentationRegistry.BallRegistered += Attach;
        }

        static void Attach(Transform ball, TrailRenderer trail) => Attach(ball.gameObject, trail);

        /// <summary>Adds (or reuses) the effect on a ball and remembers its standard trail settings.</summary>
        public static BallSkinVfx Attach(GameObject ball, TrailRenderer trail)
        {
            var fx = ball.GetComponent<BallSkinVfx>();
            if (!fx)
                fx = ball.AddComponent<BallSkinVfx>();
            fx.Bind(trail);
            return fx;
        }

        /// <summary>Skin id of the paddle an actor is holding (the rendered skin, local or replicated).</summary>
        public static string SkinOf(MatchActor actor)
        {
            var appearance = actor && actor.paddle ? actor.paddle.GetComponent<PaddleAppearance>() : null;
            return appearance ? appearance.CurrentId : ShopCatalog.DefaultPaddleId;
        }

        void Bind(TrailRenderer target)
        {
            if (trail == target && trail)
                return;
            trail = target;
            if (!trail)
                return;
            defaultMaterial = trail.sharedMaterial;
            defaultTime = trail.time;
            defaultWidth = trail.widthMultiplier;
            defaultWidthCurve = trail.widthCurve;
            defaultColor = trail.colorGradient;
            defaultTextureMode = trail.textureMode;
        }

        void OnEnable()
        {
            BallPresentationRegistry.SuccessfulContact -= OnContact;
            BallPresentationRegistry.SuccessfulContact += OnContact;
        }

        void OnDisable()
        {
            BallPresentationRegistry.SuccessfulContact -= OnContact;
            SetBallEffect(null);
        }

        void OnDestroy()
        {
            foreach (var pair in impacts)
                if (pair.Value != null && pair.Value.Length > 0 && pair.Value[0])
                    Destroy(pair.Value[0].transform.root.gameObject);
        }

        void OnContact(MatchActor actor, Vector3 point)
        {
            var profile = Library ? Library.Find(SkinOf(actor)) : null;
            Apply(profile);
            if (profile && profile.impactEffect)
                PlayImpact(profile, point);
        }

        /// <summary>Switches the ball to a skin effect (null = standard trail).</summary>
        public void Apply(PaddleSkinVfxProfile profile)
        {
            if (profile == active)
                return;
            active = profile;
            if (trail)
            {
                if (profile && profile.trailMaterial)
                {
                    trail.sharedMaterial = profile.trailMaterial;
                    trail.time = profile.trailTime;
                    trail.widthMultiplier = profile.trailWidth;
                    trail.widthCurve = profile.trailWidthCurve;
                    trail.colorGradient = profile.trailColor;
                    trail.textureMode = LineTextureMode.Stretch;
                }
                else
                {
                    trail.sharedMaterial = defaultMaterial;
                    trail.time = defaultTime;
                    trail.widthMultiplier = defaultWidth;
                    trail.widthCurve = defaultWidthCurve;
                    trail.colorGradient = defaultColor;
                    trail.textureMode = defaultTextureMode;
                }
            }
            SetBallEffect(profile);
        }

        void SetBallEffect(PaddleSkinVfxProfile profile)
        {
            foreach (var pair in ballEffects)
                if (pair.Value && pair.Key != profile)
                    SetEmitting(pair.Value, false);
            if (!profile || !profile.ballEffect)
                return;
            if (!ballEffects.TryGetValue(profile, out var root) || !root)
            {
                root = Instantiate(profile.ballEffect, transform, false).transform;
                root.name = profile.ballEffect.name;
                // Author effects in metres: cancel the ball's own scale.
                var s = transform.lossyScale;
                root.localScale = new Vector3(1 / Mathf.Max(s.x, 1e-4f), 1 / Mathf.Max(s.y, 1e-4f), 1 / Mathf.Max(s.z, 1e-4f));
                FitShell(root);
                ballEffects[profile] = root;
            }
            SetEmitting(root, true);
        }

        void FitShell(Transform root)
        {
            var shell = root.Find("Glitter Shell");
            var ballRenderer = GetComponentInChildren<MeshRenderer>();
            if (!shell || !ballRenderer)
                return;
            float diameter = Mathf.Max(ballRenderer.bounds.size.x, .04f);
            shell.localScale = Vector3.one * diameter * 1.12f;
        }

        static void SetEmitting(Transform root, bool on)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = ps.emission;
                emission.enabled = on;
            }
        }

        void PlayImpact(PaddleSkinVfxProfile profile, Vector3 point)
        {
            if (!impacts.TryGetValue(profile, out var systems) || systems == null || systems.Length == 0 || !systems[0])
            {
                var go = Instantiate(profile.impactEffect);
                go.name = profile.impactEffect.name + " (pooled)";
                if (gameObject.scene.IsValid())
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                systems = go.GetComponentsInChildren<ParticleSystem>(true);
                impacts[profile] = systems;
            }
            var t = systems[0].transform.root;
            t.position = point;
            foreach (var ps in systems)
            {
                ps.Clear(false);
                ps.Play(false);
            }
        }

        void LateUpdate()
        {
            if (!active || !ballEffects.TryGetValue(active, out var root) || !root)
                return;
            bool flying = !trail || trail.emitting;
            fade = Mathf.MoveTowards(fade, flying ? 1 : 0, Time.deltaTime / .25f);
            var shell = root.Find("Glitter Shell");
            if (shell && shell.TryGetComponent<Renderer>(out var r))
            {
                block ??= new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                r.SetPropertyBlock(block);
                r.enabled = fade > .001f;
            }
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
            {
                var emission = ps.emission;
                emission.enabled = flying;
            }
        }
    }
}
