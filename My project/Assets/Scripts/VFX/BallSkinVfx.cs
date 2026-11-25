using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PickleVerse.LocalMatch;
using PickleVerse.Shop;
using UnityEngine;

[assembly: InternalsVisibleTo("PickleVerse.VFX.Tests")]

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
        /// <summary>One skin's effect instance on this ball, with the parts that are switched on and off.</summary>
        sealed class BallEffect
        {
            public Transform root;
            public Renderer shell;
            public ParticleSystem[] particles;
            public BallSkinMotion[] motions;
            public float fade;
            public bool emitting = true;
        }

        const float FadeIn = .12f, FadeOut = .08f, CarriedDistance = .5f;
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
        readonly Dictionary<PaddleSkinVfxProfile, BallEffect> ballEffects = new Dictionary<PaddleSkinVfxProfile, BallEffect>();
        readonly Dictionary<PaddleSkinVfxProfile, ParticleSystem[]> impacts = new Dictionary<PaddleSkinVfxProfile, ParticleSystem[]>();
        MaterialPropertyBlock block;
        Vector3 restPoint;

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
            Apply(null);
            foreach (var pair in ballEffects)
                Show(pair.Value, 0, false);
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
            if (profile && ballEffects.TryGetValue(profile, out var effect) && effect.root)
                foreach (var motion in effect.motions)
                    if (motion)
                        motion.Restart(point);
            if (profile && profile.impactEffect)
                PlayImpact(profile, point);
        }

        /// <summary>Switches the ball to a skin effect (null = standard trail).</summary>
        public void Apply(PaddleSkinVfxProfile profile)
        {
            restPoint = transform.position;
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
            EnsureBallEffect(profile);
        }

        void EnsureBallEffect(PaddleSkinVfxProfile profile)
        {
            if (!profile || !profile.ballEffect || ballEffects.TryGetValue(profile, out var known) && known.root)
                return;
            var root = Instantiate(profile.ballEffect, transform, false).transform;
            root.name = profile.ballEffect.name;
            // Author effects in metres: cancel the ball's own scale.
            var s = transform.lossyScale;
            root.localScale = new Vector3(1 / Mathf.Max(s.x, 1e-4f), 1 / Mathf.Max(s.y, 1e-4f), 1 / Mathf.Max(s.z, 1e-4f));
            var shell = root.Find("Glitter Shell");
            var effect = new BallEffect { root = root, shell = shell ? shell.GetComponent<Renderer>() : null, particles = root.GetComponentsInChildren<ParticleSystem>(true), motions = root.GetComponentsInChildren<BallSkinMotion>(true) };
            FitShell(shell);
            ballEffects[profile] = effect;
            Show(effect, 0, false);
        }

        void FitShell(Transform shell)
        {
            var ballRenderer = GetComponent<MeshRenderer>() ? GetComponent<MeshRenderer>() : GetComponentInChildren<MeshRenderer>();
            if (!shell || !ballRenderer || ballRenderer.transform.IsChildOf(shell.parent))
                return;
            float diameter = Mathf.Max(ballRenderer.bounds.size.x, .04f);
            shell.localScale = Vector3.one * diameter * 1.12f;
        }

        /// <summary>Shows an effect at a strength; particles stop being emitted but live out their own lifetime.</summary>
        void Show(BallEffect effect, float fade, bool emitting)
        {
            if (!effect.root)
                return;
            if (effect.shell && (effect.fade != fade || effect.shell.enabled != fade > .001f))
            {
                block ??= new MaterialPropertyBlock();
                effect.shell.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                effect.shell.SetPropertyBlock(block);
                effect.shell.enabled = fade > .001f;
            }
            effect.fade = fade;
            foreach (var motion in effect.motions)
                if (motion)
                    motion.SetShown(emitting);
            if (effect.emitting == emitting)
                return;
            effect.emitting = emitting;
            foreach (var ps in effect.particles)
            {
                if (!ps)
                    continue;
                var emission = ps.emission;
                emission.enabled = emitting;
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

        void LateUpdate() => Advance(Time.deltaTime, !trail || trail.emitting);

        /// <summary>
        /// Fades the active skin in and every other one out. A ball that is picked up for the next point (it moves while it is
        /// not flying) goes back to the standard trail; a pause leaves it where it is and keeps the effect.
        /// </summary>
        internal void Advance(float dt, bool flying)
        {
            if (flying)
                restPoint = transform.position;
            else if (active && (transform.position - restPoint).sqrMagnitude > CarriedDistance * CarriedDistance)
                Apply(null);
            foreach (var pair in ballEffects)
            {
                bool on = pair.Key == active && flying;
                Show(pair.Value, Mathf.MoveTowards(pair.Value.fade, on ? 1 : 0, dt / (on ? FadeIn : FadeOut)), on);
            }
        }
    }
}
