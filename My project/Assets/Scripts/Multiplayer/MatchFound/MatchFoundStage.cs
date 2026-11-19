using System.Collections.Generic;
using UnityEngine;
using C = PickleVerse.Multiplayer.MatchFoundChoreography;

namespace PickleVerse.Multiplayer
{
    /// <summary>
    /// The 3D half of the match-found screen: the rally, the crossed paddles and the VS ball, rendered by a private
    /// camera into a texture shown under the ready-check UI. It lives far below the world, uses self-lit shaders
    /// (scene lights and fog never touch it) and is destroyed as soon as the ready check closes.
    /// </summary>
    public sealed class MatchFoundStage : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(0, -5000, 0);
        const string Root = "MatchFound/";

        public RenderTexture Texture { get; private set; }
        Camera view;
        readonly Transform[] paddles = new Transform[2];
        readonly Renderer[] auras = new Renderer[2];
        Transform ball, plume, backdrop;
        Renderer plumeRenderer;
        TrailRenderer trail;
        Material energy;
        MaterialPropertyBlock block;
        C.Shot[] shots;
        float readyWaveAt = -1;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity"), ModeId = Shader.PropertyToID("_Mode"), SeedId = Shader.PropertyToID("_Seed"),
            ColorId = Shader.PropertyToID("_Color"), TimeId = Shader.PropertyToID("_MFTime"), FlashId = Shader.PropertyToID("_MFFlash");

        sealed class Burst { public Renderer renderer; public float frame, life, r0, r1, strength; public Vector3 at, normal; public Color color; }
        sealed class Spark { public Renderer renderer; public float actionStart, life; public Vector3 origin, velocity; }
        readonly List<Burst> bursts = new List<Burst>();
        readonly List<Spark> sparks = new List<Spark>();

        public static MatchFoundStage Create(int width, int height, bool mobile)
        {
            var go = new GameObject("Match Found Stage");
            DontDestroyOnLoad(go);
            var stage = go.AddComponent<MatchFoundStage>();
            if (!stage.Build(width, height, mobile))
            {
                Destroy(go);
                return null;
            }
            return stage;
        }

        bool Build(int width, int height, bool mobile)
        {
            var paddlePrefab = Resources.Load<GameObject>(Root + "PV_Paddle_Elongated");
            var ballPrefab = Resources.Load<GameObject>(Root + "PV_VS_Ball");
            energy = Resources.Load<Material>(Root + "Materials/Energy");
            var trailMat = Resources.Load<Material>(Root + "Materials/Trail");
            Material Mat(string n) => Resources.Load<Material>(Root + "Materials/" + n);
            if (!paddlePrefab || !ballPrefab || !energy || !trailMat)
            {
                Debug.LogWarning("[MatchFound] Stage assets are missing; the ready check falls back to the plain card.");
                return false;
            }
            block = new MaterialPropertyBlock();
            transform.position = Origin;
            shots = C.Shots();

            var paddleMaterials = new[] { Mat("Paddle Atlas"), Mat("Paddle Guard"), Mat("Paddle Cap") };
            for (int i = 0; i < 2; i++)
            {
                var p = Instantiate(paddlePrefab, transform);
                p.name = i == 0 ? "Paddle Left" : "Paddle Right";
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>())
                    r.sharedMaterials = paddleMaterials;
                paddles[i] = p.transform;
                auras[i] = Quad("Paddle Aura " + i, 0, i * 3.7f);
            }
            var b = Instantiate(ballPrefab, transform);
            b.name = "VS Ball";
            foreach (var r in b.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = new[] { Mat("Ball"), Mat("VS Rim"), Mat("VS Ink") };
            ball = b.transform;
            trail = b.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMat;
            trail.time = .2f;
            trail.minVertexDistance = .05f;
            trail.widthMultiplier = 1;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .45f), new Keyframe(1, 0));
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;

            plumeRenderer = Quad("Hero Plume", 0, 9.1f);
            plume = plumeRenderer.transform;

            // Court lines under the rally give the camera moves a sense of space.
            foreach (var (x0, y0, x1, y1, w) in CourtLines())
            {
                var line = Quad("Court Line", 4, 0);
                block.Clear();
                block.SetFloat(ModeId, 4);
                block.SetColor(ColorId, new Color(.30f, .80f, .72f, .55f));
                block.SetFloat(IntensityId, 1);
                line.SetPropertyBlock(block);
                var a = C.ToUnity(new Vector3(x0, y0, -2.69f));
                var c = C.ToUnity(new Vector3(x1, y1, -2.69f));
                line.transform.position = Origin + (a + c) * .5f;
                line.transform.rotation = Quaternion.LookRotation(Vector3.up, c - a);
                line.transform.localScale = new Vector3(w, Vector3.Distance(a, c) + w, 1);
            }

            var camGo = new GameObject("Match Found Camera");
            camGo.transform.SetParent(transform, false);
            view = camGo.AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.004f, .008f, .02f, 1);
            view.nearClipPlane = .1f;
            view.farClipPlane = 140;
            view.allowHDR = false;
            // A script-created URP camera defaults to no post-processing and no AA; the stage shaders cast no shadows.
            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Match Found Stage", antiAliasing = mobile ? 2 : 4 };
            Texture.Create();
            view.targetTexture = Texture;
            view.aspect = width / (float)height;

            // Screen-locked backdrop far behind everything (gradient + out-of-focus stadium glows).
            var back = Quad("Backdrop", 2, 0);
            backdrop = back.transform;
            backdrop.SetParent(camGo.transform, false);

            foreach (var (frame, at, normal, r0, r1, life, strength) in HitRings())
                bursts.Add(new Burst { renderer = Quad("Ring", 1, 0), frame = frame, at = at, normal = normal, r0 = r0, r1 = r1, life = life, strength = strength, color = new Color(.62f, .82f, 1f) });
            SparkBurst(C.H1, C.Contact1, new Vector3(1, 0, .4f), 10, .55f, 5.5f, 9, 11);
            SparkBurst(C.H2, C.Contact2, new Vector3(-1, 0, .3f), 10, .55f, 5.5f, 9, 23);
            SparkBurst(C.H3, C.Contact3, new Vector3(1, 0, 1), 10, .55f, 5.5f, 9, 37);
            SparkBurst(C.Lock, C.BallRest + new Vector3(0, -.2f, 0), new Vector3(0, -.3f, .2f), 26, 1.25f, 7f, 16, 53);

            Shader.SetGlobalColor("_MFKeyColor", new Color(1f, .86f, .70f) * .95f);
            Shader.SetGlobalColor("_MFFillColor", new Color(.55f, .65f, .85f) * .22f);
            Shader.SetGlobalColor("_MFRimColor", new Color(.30f, .55f, 1f) * .9f);
            Shader.SetGlobalColor("_MFAmbient", new Color(.035f, .045f, .07f));
            Shader.SetGlobalVector("_MFKeyDir", C.ToUnity(new Vector3(-4.5f, -5.5f, 4.6f).normalized));
            Shader.SetGlobalVector("_MFFillDir", C.ToUnity(new Vector3(0, -7f, -1.9f).normalized));
            Shader.SetGlobalVector("_MFHeroDir", C.ToUnity(new Vector3(-3.4f, -4.8f, 3.35f).normalized));
            Tick(0);
            trail.Clear();   // the first pose moved the ball from the stage origin
            return true;
        }

        static IEnumerable<(float, float, float, float, float)> CourtLines()
        {
            foreach (float y in new[] { -3f, 3f }) yield return (-6.7f, y, 6.7f, y, .07f);
            foreach (float x in new[] { -6.7f, -2.1f, 2.1f, 6.7f }) yield return (x, -3f, x, 3f, .07f);
            yield return (-6.7f, 0, -2.1f, 0, .07f);
            yield return (2.1f, 0, 6.7f, 0, .07f);
            yield return (0, -3.2f, 0, 3.2f, .09f);
        }

        static IEnumerable<(float, Vector3, Vector3, float, float, float, float)> HitRings()
        {
            yield return (C.HitFrame1, C.Contact1, Vector3.right, .25f, 1.15f, 8, 1);
            yield return (C.HitFrame2, C.Contact2, Vector3.right, .25f, 1.15f, 22, 1);
            yield return (C.HitFrame3, C.Contact3, Vector3.right, .25f, 1.15f, 8, 1);
            yield return (C.LockFrame, C.BallRest + new Vector3(0, -.05f, 0), new Vector3(0, -1, 0), .45f, 3.4f, 14, 1);
            yield return (C.LockFrame + 1, C.BallRest + new Vector3(0, -.04f, 0), new Vector3(0, -1, 0), .35f, 1.9f, 9, .8f);
        }

        void SparkBurst(float actionStart, Vector3 origin, Vector3 direction, int count, float spread, float speed, float life, int seed)
        {
            for (int i = 0; i < count; i++)
            {
                int h = seed * 131 + i * 7;
                var d = (direction + new Vector3(C.Hash(h) * 2 - 1, C.Hash(h + 1) * 2 - 1, C.Hash(h + 2) * 1.8f - .6f) * spread).normalized;
                sparks.Add(new Spark
                {
                    renderer = Quad("Spark", 3, 0),
                    actionStart = actionStart,
                    life = life * (.7f + .5f * C.Hash(h + 3)),
                    origin = origin,
                    velocity = d * speed * (.6f + .7f * C.Hash(h + 4)),
                });
            }
        }

        Renderer Quad(string name, float mode, float seed)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = energy;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            block.Clear();
            block.SetFloat(ModeId, mode);
            block.SetFloat(SeedId, seed);
            r.SetPropertyBlock(block);
            return r;
        }

        public void TriggerReadyWave(float seconds)
        {
            if (readyWaveAt < 0)
                readyWaveAt = seconds;
        }

        /// <summary>Pose everything for the given time since the ready check appeared.</summary>
        public void Tick(float seconds)
        {
            float frame = Mathf.Min(C.FrameAt(seconds), C.EndFrame);
            float a = C.ActionAt(frame);
            Shader.SetGlobalFloat(TimeId, seconds);
            Shader.SetGlobalFloat(FlashId, C.Flash(frame) * .6f + (readyWaveAt >= 0 ? .35f * Mathf.Exp(-(seconds - readyWaveAt) * 5) : 0));
            Shader.SetGlobalColor("_MFHeroColor", new Color(1f, .9f, .76f) * (.55f * C.HeroKey(frame)));

            // camera
            C.CameraAt(shots, frame, out var camB, out var targetB, out var lens);
            var camPos = Origin + C.ToUnity(camB);
            view.transform.position = camPos;
            view.transform.rotation = Quaternion.LookRotation(Origin + C.ToUnity(targetB) - camPos, Vector3.up);
            view.fieldOfView = C.VerticalFov(lens, view.aspect);
            float far = 120;
            float halfH = Mathf.Tan(view.fieldOfView * .5f * Mathf.Deg2Rad) * far;
            backdrop.localPosition = new Vector3(0, 0, far);
            backdrop.localRotation = Quaternion.identity;
            backdrop.localScale = new Vector3(halfH * 2 * view.aspect * 1.02f, halfH * 2 * 1.02f, 1);

            // paddles + their auras
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                C.PaddlePose(side, a, out var posB, out var rotB);
                var rot = C.ToUnity(rotB);
                paddles[i].SetPositionAndRotation(Origin + C.ToUnity(posB), rot);
                var anchor = paddles[i].position + rot * C.ToUnity(new Vector3(0, 0, C.HeadCentre * .78f));
                Face(auras[i].transform, anchor, .55f, 3.1f, 4.1f);
                SetEnergy(auras[i], 0, i * 3.7f, C.PaddleAura(frame), Color.white);
            }

            // ball + trail
            var bp = C.BallPosition(a, out float radius);
            ball.SetPositionAndRotation(Origin + C.ToUnity(bp), C.ToUnity(C.BallRotation(a)));
            ball.localScale = Vector3.one * radius;
            bool rally = frame < C.LockFrame;
            if (trail.emitting != rally)
            {
                trail.emitting = rally;
                if (!rally) trail.Clear();
            }
            trail.widthMultiplier = radius * 2;

            // plume behind the crossing
            float plumeScale = C.PlumeScale(frame);
            Face(plume, Origin + C.ToUnity(C.Crossing + new Vector3(0, .2f, .5f)), 1.0f, 8.0f * plumeScale, 6.2f * plumeScale);
            SetEnergy(plumeRenderer, 0, 9.1f, C.PlumeIntensity(frame), Color.white);

            foreach (var burst in bursts)
                Animate(burst, frame);
            if (readyWaveAt >= 0)
                AnimateReadyWave(seconds);
            foreach (var spark in sparks)
                Animate(spark, a);
        }

        void Face(Transform quad, Vector3 anchor, float back, float width, float height)
        {
            var toCam = (view.transform.position - anchor).normalized;
            quad.position = anchor - toCam * back;
            quad.rotation = Quaternion.LookRotation(-toCam, view.transform.up);
            quad.localScale = new Vector3(width, height, 1);
        }

        void SetEnergy(Renderer r, float mode, float seed, float intensity, Color color)
        {
            bool on = intensity > .002f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            block.Clear();
            block.SetFloat(ModeId, mode);
            block.SetFloat(SeedId, seed);
            block.SetFloat(IntensityId, intensity);
            block.SetColor(ColorId, color);
            r.SetPropertyBlock(block);
        }

        void Animate(Burst b, float frame)
        {
            float t = (frame - b.frame) / b.life;
            if (t < 0 || t > 1)
            {
                if (b.renderer.enabled) b.renderer.enabled = false;
                return;
            }
            float r = Mathf.Lerp(b.r0, b.r1, C.Smooth(t));
            b.renderer.transform.position = Origin + C.ToUnity(b.at);
            b.renderer.transform.rotation = Quaternion.LookRotation(C.ToUnity(b.normal).normalized);
            b.renderer.transform.localScale = Vector3.one * (2 * r / .82f);
            SetEnergy(b.renderer, 1, 0, b.strength * (1 - t), b.color);
        }

        Renderer readyWave;
        void AnimateReadyWave(float seconds)
        {
            if (!readyWave) readyWave = Quad("Ready Wave", 1, 0);
            float t = (seconds - readyWaveAt) / (18f / C.Fps);
            if (t > 1)
            {
                if (readyWave.enabled) readyWave.enabled = false;
                return;
            }
            float r = Mathf.Lerp(.5f, 3f, C.Smooth(t));
            readyWave.transform.position = Origin + C.ToUnity(C.BallRest + new Vector3(0, -.06f, 0));
            readyWave.transform.rotation = Quaternion.LookRotation(C.ToUnity(new Vector3(0, -1, 0)));
            readyWave.transform.localScale = Vector3.one * (2 * r / .82f);
            SetEnergy(readyWave, 1, 0, 1 - t, new Color(.62f, .82f, 1f));
        }

        void Animate(Spark s, float a)
        {
            float age = a - s.actionStart;
            if (age < 0 || age > s.life)
            {
                if (s.renderer.enabled) s.renderer.enabled = false;
                return;
            }
            float t = age / C.Fps;
            var vel = s.velocity + new Vector3(0, 0, -9f * t);
            var pos = s.origin + s.velocity * t + new Vector3(0, 0, -4.5f * t * t);
            float k = 1 - age / s.life;
            var p = Origin + C.ToUnity(pos);
            var v = C.ToUnity(vel);
            var toCam = (view.transform.position - p).normalized;
            var tr = s.renderer.transform;
            tr.position = p;
            tr.rotation = Quaternion.LookRotation(-toCam, v.sqrMagnitude > 1e-6f ? v : Vector3.up);
            tr.localScale = new Vector3(.05f * k, (.16f + .02f * v.magnitude / 6) * k, 1);
            SetEnergy(s.renderer, 3, 0, k, new Color(.75f, .9f, 1f));
        }

        void OnDestroy()
        {
            if (view) view.targetTexture = null;
            if (Texture)
            {
                Texture.Release();
                Destroy(Texture);
            }
            Shader.SetGlobalFloat(FlashId, 0);
        }
    }
}
