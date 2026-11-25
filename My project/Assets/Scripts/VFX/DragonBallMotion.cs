using System;
using UnityEngine;

namespace PickleVerse.VFX
{
    /// <summary>
    /// The red dragon of the Dragon Ruby paddle: it leaves the paddle with the shot and chases the ball along the ball's own
    /// path, weaving on a sine wave (y = A sin(2 pi x / wavelength), x = distance flown). Presentation only - it reads the
    /// ball's rendered position and never touches physics, rules or the network. The body is bent on the GPU: this component
    /// uploads a short list of path points and the wave; the shaders place every vertex.
    /// </summary>
    public sealed class DragonBallMotion : BallSkinMotion
    {
        /// <summary>Path points handed to the shaders (must match _Spine in DragonSpine.hlsl).</summary>
        public const int SpinePoints = 32;
        const int History = 192;

        [Header("Sine flight (metres)")]
        [Min(0)] public float amplitude = .2f;
        [Min(.1f)] public float wavelength = 1.1f;
        [Tooltip("The wave opens over this distance behind the ball and after the paddle, so both stay on the ball's path.")]
        [Min(.01f)] public float ramp = .75f;
        [Tooltip("Distance kept between the ball and the dragon's nose.")]
        [Min(0)] public float gap = .13f;
        [Tooltip("Length of path covered behind the ball (dragon and ribbons).")]
        [Min(.5f)] public float reach = 3.6f;
        [Tooltip("The wave never dips below this height, so the dragon stays above the court.")]
        public float floor = .07f;

        [Header("Parts")]
        public Renderer[] renderers = Array.Empty<Renderer>();
        [Tooltip("Emitters carried along the body.")]
        public Transform[] riders = Array.Empty<Transform>();
        [Tooltip("Distance behind the ball of each rider.")]
        public float[] riderDistances = Array.Empty<float>();

        [Header("Timing (seconds)")]
        [Min(.01f)] public float appear = .12f;
        [Min(.01f)] public float dissolve = .3f;

        static readonly int SpineId = Shader.PropertyToID("_Spine"), SpineInfoId = Shader.PropertyToID("_SpineInfo"), WaveId = Shader.PropertyToID("_Wave"),
            WaveAxisId = Shader.PropertyToID("_WaveAxis"), SideAxisId = Shader.PropertyToID("_SideAxis"), FadeId = Shader.PropertyToID("_Fade");

        readonly Vector3[] path = new Vector3[History];
        readonly float[] arcs = new float[History];
        readonly Vector4[] spine = new Vector4[SpinePoints];
        MaterialPropertyBlock block;
        int count, newest;
        float fade;
        bool shown, hasAxis;
        Vector3 waveAxis = Vector3.up, sideAxis = Vector3.right;

        /// <summary>0 = hidden, 1 = fully visible.</summary>
        public float Fade => fade;
        /// <summary>Distance the ball has flown since the contact.</summary>
        public float Flown => count > 0 ? arcs[newest] : 0;

        /// <summary>Sideways offset of the wave at a distance behind the ball (the shaders use the same formula).</summary>
        public static float WaveOffset(float behind, float flown, float amplitude, float wavelength, float ramp)
        {
            float travelled = flown - behind;
            if (travelled <= 0)
                return 0;
            float open = Mathf.SmoothStep(0, 1, behind / ramp) * Mathf.SmoothStep(0, 1, travelled / ramp);
            return amplitude * open * Mathf.Sin(2 * Mathf.PI * travelled / wavelength);
        }

        public override void Restart(Vector3 contact)
        {
            count = 0;
            hasAxis = false;
            Push(contact);
            if ((transform.position - contact).sqrMagnitude < 4)
                Push(transform.position);
        }

        public override void SetShown(bool value) => shown = value;

        void OnEnable() => Show(false);

        void OnDisable()
        {
            fade = 0;
            count = 0;
            Show(false);
        }

        void Push(Vector3 p)
        {
            if (count == 0)
            {
                newest = 0;
                path[0] = p;
                arcs[0] = 0;
                count = 1;
                return;
            }
            float step = Vector3.Distance(path[newest], p);
            if (step < .02f)
                return;
            int next = (newest + 1) % History;
            path[next] = p;
            arcs[next] = arcs[newest] + step;
            newest = next;
            count = Mathf.Min(count + 1, History);
        }

        /// <summary>Point of the ball's path at a distance behind the ball (clamped to the contact).</summary>
        public Vector3 PathPoint(float behind)
        {
            if (count == 0)
                return transform.position;
            float target = arcs[newest] - behind;
            int i = newest;
            for (int n = 1; n < count; n++)
            {
                int older = (i + History - 1) % History;
                if (arcs[older] <= target)
                    return Vector3.Lerp(path[older], path[i], Mathf.InverseLerp(arcs[older], arcs[i], target));
                i = older;
            }
            return path[i];
        }

        /// <summary>Point on the sine wave at a distance behind the ball.</summary>
        public Vector3 WavePoint(float behind)
        {
            var p = PathPoint(behind) + waveAxis * WaveOffset(behind, Flown, amplitude, wavelength, ramp);
            p.y = Mathf.Max(p.y, Mathf.Min(floor, PathPoint(behind).y));
            return p;
        }

        void LateUpdate() => Advance(transform.position, Time.deltaTime, Camera.main);

        internal void Advance(Vector3 ball, float dt, Camera camera)
        {
            fade = Mathf.MoveTowards(fade, shown ? 1 : 0, dt / (shown ? appear : dissolve));
            if (count == 0 || fade <= 0)
            {
                Show(false);
                return;
            }
            // While it dissolves the dragon stays on the path it already flew.
            if (shown)
            {
                if ((ball - path[newest]).sqrMagnitude > 9)   // the ball was placed somewhere else
                    Restart(ball);
                else
                    Push(ball);
            }
            Axes(camera, dt);
            float step = reach / (SpinePoints - 1);
            var bounds = new Bounds(path[newest], Vector3.zero);
            for (int i = 0; i < SpinePoints; i++)
            {
                var p = PathPoint(i * step);
                spine[i] = new Vector4(p.x, p.y, p.z, Mathf.Min(floor, p.y));
                bounds.Encapsulate(p);
            }
            bounds.Expand(2 * amplitude + .6f);
            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (!r)
                    continue;
                r.GetPropertyBlock(block);
                block.SetVectorArray(SpineId, spine);
                block.SetVector(SpineInfoId, new Vector4(step, Flown, gap, reach));
                block.SetVector(WaveId, new Vector4(amplitude, 2 * Mathf.PI / wavelength, ramp, 0));
                block.SetVector(WaveAxisId, waveAxis);
                block.SetVector(SideAxisId, sideAxis);
                block.SetFloat(FadeId, fade);
                r.SetPropertyBlock(block);
                r.bounds = bounds;
            }
            for (int i = 0; i < riders.Length && i < riderDistances.Length; i++)
                if (riders[i])
                    riders[i].position = WavePoint(Mathf.Min(riderDistances[i], Flown));
            Show(true);
        }

        /// <summary>The wave is drawn in the plane that faces the camera, so the sine curve always reads from the side.</summary>
        void Axes(Camera camera, float dt)
        {
            var head = path[newest];
            var travel = head - PathPoint(Mathf.Min(Flown, 1.6f));
            if (travel.sqrMagnitude < 1e-4f)
                return;
            travel.Normalize();
            var view = camera ? (camera.transform.position - head).normalized : Vector3.Cross(travel, Vector3.up);
            var side = Vector3.ProjectOnPlane(view, travel);
            if (side.sqrMagnitude < .02f)                     // the ball flies straight at the camera
                side = hasAxis ? Vector3.ProjectOnPlane(sideAxis, travel) : Vector3.ProjectOnPlane(Vector3.up, travel);
            if (side.sqrMagnitude < 1e-4f)
                return;
            side.Normalize();
            var wave = Vector3.Cross(travel, side);
            if (hasAxis ? Vector3.Dot(wave, waveAxis) < 0 : wave.y < 0)
                wave = -wave;
            float blend = hasAxis ? 1 - Mathf.Exp(-9 * dt) : 1;
            waveAxis = Vector3.Slerp(waveAxis, wave, blend).normalized;
            sideAxis = Vector3.Slerp(hasAxis ? sideAxis : side, side, blend).normalized;
            hasAxis = true;
        }

        void Show(bool on)
        {
            foreach (var r in renderers)
                if (r && r.enabled != on)
                    r.enabled = on;
        }
    }
}
