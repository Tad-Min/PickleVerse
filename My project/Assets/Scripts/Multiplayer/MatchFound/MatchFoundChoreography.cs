using System;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>
    /// The "match found" intro, ported 1:1 from ArtSource/MatchFound/build_match_found.py (the approved Blender
    /// preview): three hits with bullet time on the second, a lob, the paddles crossing and the VS ball locking at
    /// the crossing point. Everything is evaluated in Blender space (X right, Y away from the camera, Z up) and
    /// converted with <see cref="ToUnity(Vector3)"/>. Pure math so it is deterministic and testable.
    /// </summary>
    public static class MatchFoundChoreography
    {
        public const int Fps = 30, EndFrame = 240;
        public const float H1 = 8, H2 = 26, H3 = 44, Lock = 64;     // action frames of the three hits and the lock
        const float SlowLo = 22, SlowHi = 30, Slow = .32f, Ramp = 3;
        public const float HeadCentre = 1.55f;                        // grip -> centre of the hitting area
        public const float RallyRadius = .28f, HeroRadius = .40f;
        static readonly float[] act;
        public static readonly int HitFrame1, HitFrame2, HitFrame3, LockFrame;

        public static readonly Vector3 Serve = new Vector3(-2.35f, 0, 2.9f);
        public static readonly Vector3 Contact1 = new Vector3(-2.91f, 0, .44f), Contact2 = new Vector3(2.91f, -.35f, .62f), Contact3 = new Vector3(-2.91f, .30f, .36f);
        public static readonly Vector3 Crossing = new Vector3(0, 0, .15f), BallRest = new Vector3(0, -.62f, .15f);
        static readonly Vector3 SpinAxis = new Vector3(.35f, .25f, 1f).normalized;
        const float Theta = 42 * Mathf.Deg2Rad, CrossAt = .62f;

        static MatchFoundChoreography()
        {
            act = new float[EndFrame + 3];
            float cur = 0;
            for (int f = 1; f < act.Length; f++)
            {
                act[f] = cur;
                cur += Speed(cur);
            }
            HitFrame1 = Mathf.RoundToInt(RealFrame(H1));
            HitFrame2 = Mathf.RoundToInt(RealFrame(H2));
            HitFrame3 = Mathf.RoundToInt(RealFrame(H3));
            LockFrame = Mathf.RoundToInt(RealFrame(Lock));
        }

        public static float LockSeconds => (LockFrame - 1) / (float)Fps;
        public static float FrameAt(float seconds) => 1 + seconds * Fps;

        // ------------------------------------------------------------------ time: bullet time on the second hit
        static float Speed(float x)
        {
            if (x < SlowLo - Ramp || x > SlowHi + Ramp) return 1;
            if (x < SlowLo) return 1 + (Slow - 1) * Smooth((x - (SlowLo - Ramp)) / Ramp);
            if (x > SlowHi) return Slow + (1 - Slow) * Smooth((x - SlowHi) / Ramp);
            return Slow;
        }

        /// <summary>Action time at a (fractional) real frame.</summary>
        public static float ActionAt(float frame)
        {
            if (frame <= 1) return act[1] - (1 - frame);
            int last = act.Length - 1;
            if (frame >= last) return act[last] + (frame - last);
            int i = (int)frame;
            return Mathf.Lerp(act[i], act[i + 1], frame - i);
        }

        public static float RealFrame(float action)
        {
            for (int f = 1; f < act.Length - 1; f++)
                if (act[f + 1] >= action)
                    return f + (action - act[f]) / Mathf.Max(1e-6f, act[f + 1] - act[f]);
            return EndFrame;
        }

        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        public static float Smoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

        // ------------------------------------------------------------------ ball
        static Vector3 Arc(Vector3 a, Vector3 b, float apex, float t)
        {
            var p = Vector3.Lerp(a, b, t);
            p.z += apex * 4 * t * (1 - t);
            return p;
        }

        public static Vector3 BallPosition(float a, out float radius)
        {
            radius = RallyRadius;
            if (a <= H1)
            {
                float t = Mathf.Max(0, a) / H1;
                var p = Vector3.Lerp(Serve, Contact1, t);
                p.z = Mathf.Lerp(Serve.z, Contact1.z, t * t);
                return p;
            }
            if (a <= H2) return Arc(Contact1, Contact2, 1.15f, (a - H1) / (H2 - H1));
            if (a <= H3) return Arc(Contact2, Contact3, .75f, (a - H2) / (H3 - H2));
            if (a <= Lock)
            {
                float t = (a - H3) / (Lock - H3);
                var p = Vector3.Lerp(Contact3, BallRest, t);
                p.z = Mathf.Lerp(Contact3.z, BallRest.z, t) + 3f * 4 * t * (1 - t);
                radius = Mathf.Lerp(RallyRadius, HeroRadius, Smoother((a - (H3 + 8)) / (Lock - H3 - 8)));
                return p;
            }
            radius = HeroRadius;
            float hold = a - Lock;
            return BallRest + new Vector3(0, 0, .035f * Mathf.Sin(hold * .11f) * Smooth(hold / 12));
        }

        public static Quaternion BallRotation(float a)
        {
            if (a <= Lock)
            {
                var spin = AxisAngle(SpinAxis, a * .42f);
                return Quaternion.Slerp(spin, Quaternion.identity, Smoother((a - (Lock - 9)) / 9));
            }
            float hold = a - Lock;
            float wob = 4 * Mathf.Deg2Rad * Smooth(hold / 20);
            return AxisAngle(Vector3.forward, wob * Mathf.Sin(hold * .07f)) * AxisAngle(Vector3.right, .5f * wob * Mathf.Sin(hold * .05f));
        }

        // ------------------------------------------------------------------ paddles (side -1 = left, +1 = right)
        struct SwingKey { public float t, yaw, pitch, fwd, up; public SwingKey(float t, float yaw, float pitch, float fwd, float up) { this.t = t; this.yaw = yaw; this.pitch = pitch; this.fwd = fwd; this.up = up; } }
        static readonly SwingKey[] Keys = { new SwingKey(-12, 0, 0, 0, 0), new SwingKey(-4, 40, -6, -.42f, -.16f), new SwingKey(0, 0, 8, 0, 0), new SwingKey(6, -38, 22, .48f, .30f), new SwingKey(18, 0, 0, 0, 0) };

        static void Swing(float a, float hit, bool lob, ref float yaw, ref float pitch, ref float fwd, ref float up)
        {
            float dt = a - hit;
            if (dt <= Keys[0].t || dt >= Keys[Keys.Length - 1].t) return;
            for (int i = 0; i < Keys.Length - 1; i++)
            {
                var k0 = Keys[i];
                var k1 = Keys[i + 1];
                if (dt < k0.t || dt > k1.t) continue;
                if (lob && k0.t == 0) k0.pitch = 35;
                if (lob && k1.t == 0) k1.pitch = 35;
                float t = Smooth((dt - k0.t) / (k1.t - k0.t));
                yaw += Mathf.Lerp(k0.yaw, k1.yaw, t);
                pitch += Mathf.Lerp(k0.pitch, k1.pitch, t);
                fwd += Mathf.Lerp(k0.fwd, k1.fwd, t);
                up += Mathf.Lerp(k0.up, k1.up, t);
                return;
            }
        }

        static Vector3 ContactOf(float hit) => hit == H1 ? Contact1 : hit == H2 ? Contact2 : Contact3;

        /// <summary>Paddle pose in Blender space: position of the grip and the rotation of the paddle model.</summary>
        public static void PaddlePose(int side, float a, out Vector3 position, out Quaternion rotation)
        {
            // Track the height/depth of this paddle's next (or last) contact.
            float first = side < 0 ? H1 : H2, second = side < 0 ? H3 : H2;
            Vector3 target;
            if (side > 0) target = Contact2;
            else if (H1 >= a - 10) target = Contact1;
            else if (H3 >= a - 10) target = Vector3.Lerp(Contact1, Contact3, Smooth((a - (H1 + 10)) / Mathf.Max(1, H3 - H1 - 18)));
            else target = Contact3;
            float yaw = 0, pitch = 0, fwd = 0, up = 0;
            Swing(a, first, false, ref yaw, ref pitch, ref fwd, ref up);
            if (side < 0) Swing(a, second, true, ref yaw, ref pitch, ref fwd, ref up);
            var face = new Vector3(-side, 0, 0);
            var n = AxisAngle(Vector3.forward, yaw * -side * Mathf.Deg2Rad) * face;
            var tilt = Vector3.Cross(n, Vector3.forward).normalized;
            var qPitch = AxisAngle(tilt, pitch * Mathf.Deg2Rad);
            n = qPitch * n;
            var u = qPitch * Vector3.forward;
            float bob = .045f * Mathf.Sin(a * .23f + (side < 0 ? 0 : 1.7f));
            position = new Vector3(side * 3.25f - side * fwd, target.y, target.z - HeadCentre + up + bob);
            // Tilt about the centre of the face (not the grip) so the face still meets the ball on the lob.
            position -= (u - Vector3.forward) * HeadCentre;
            rotation = Basis(n, u);
            float cross = Smoother((a - 47) / 15);
            if (cross > 0)
            {
                CrossPose(side, out var cpos, out var crot);
                position = Vector3.Lerp(position, cpos, cross) + new Vector3(0, 0, .55f * Mathf.Sin(Mathf.PI * cross));
                rotation = Quaternion.Slerp(rotation, crot, cross);
            }
            if (a > 62)
            {
                float k = a - 62;
                float recoil = .09f * Mathf.Exp(-k / 4.5f) * Mathf.Sin(k * .95f);
                float breathe = .025f * Mathf.Sin(k * .09f + (side < 0 ? 0 : 1.3f)) * Smooth(k / 20);
                position += new Vector3(side * recoil, 0, breathe);
                rotation = AxisAngle(Vector3.up, side * .012f * Mathf.Sin(a * .06f)) * rotation;
            }
        }

        public static void CrossPose(int side, out Vector3 position, out Quaternion rotation)
        {
            var u = new Vector3(-side * Mathf.Sin(Theta), 0, Mathf.Cos(Theta));
            position = Crossing - u * CrossAt + new Vector3(0, side < 0 ? -.07f : .09f, 0);
            rotation = Basis(new Vector3(0, -1, 0), u);
        }

        // ------------------------------------------------------------------ camera (real frames: keeps moving through bullet time)
        public struct Shot { public float frame; public Vector3 cam, target; public float lens; }

        public static Shot[] Shots(float heroDistance = 7.6f)
        {
            Shot S(float f, float cx, float cy, float cz, float tx, float ty, float tz, float lens) =>
                new Shot { frame = f, cam = new Vector3(cx, cy, cz), target = new Vector3(tx, ty, tz), lens = lens };
            return new[]
            {
                S(1, -5.6f, -2.6f, 0f, -2.9f, 0f, 1.35f, 32),
                S(HitFrame1, -5.2f, -3.3f, .2f, -2.8f, 0f, .55f, 32),
                S(HitFrame1 + 9, -2.6f, -6.4f, .9f, 0f, -.15f, 1.45f, 30),
                S(HitFrame2 - 9, .6f, -3.6f, .55f, 2.6f, -.35f, .75f, 32),
                S(HitFrame2, .75f, -2.55f, .5f, 2.85f, -.35f, .62f, 34),
                S(HitFrame2 + 16, .3f, -1.25f, .95f, 2.75f, -.32f, .62f, 34),
                S(HitFrame3 - 6, 4.6f, -3.4f, 1.4f, -2.4f, .25f, .5f, 34),
                S(HitFrame3 + 6, 2.0f, -6.8f, -.3f, -.8f, 0f, 1.6f, 30),
                S(LockFrame - 6, 0f, -(heroDistance + 1.0f), .38f, 0f, -.35f, .46f, 40),
                S(LockFrame + 14, 0f, -(heroDistance + .8f), .38f, 0f, -.3f, .40f, 40),
                S(EndFrame, 0f, -heroDistance, .34f, 0f, -.3f, .36f, 40),
            };
        }

        public static void CameraAt(Shot[] shots, float frame, out Vector3 cam, out Vector3 target, out float lens)
        {
            cam = Hermite(shots, frame, s => s.cam);
            target = Hermite(shots, frame, s => s.target);
            lens = Hermite(shots, frame, s => new Vector3(s.lens, 0, 0)).x;
            if (frame >= LockFrame && frame <= LockFrame + 10)
            {
                float k = 1 - (frame - LockFrame) / 10f;
                int f = Mathf.FloorToInt(frame);
                cam += new Vector3(Hash(f * 2) * 2 - 1, 0, Hash(f * 2 + 1) * 2 - 1) * .07f * k;
            }
        }

        static Vector3 Hermite(Shot[] keys, float f, Func<Shot, Vector3> get)
        {
            if (f <= keys[0].frame) return get(keys[0]);
            if (f >= keys[keys.Length - 1].frame) return get(keys[keys.Length - 1]);
            int i = 0;
            while (i < keys.Length - 2 && keys[i + 1].frame <= f) i++;
            Vector3 Tan(int j)
            {
                int j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(keys.Length - 1, j + 1);
                return (get(keys[j1]) - get(keys[j0])) / Mathf.Max(1e-6f, keys[j1].frame - keys[j0].frame);
            }
            float t0 = keys[i].frame, h = keys[i + 1].frame - t0, s = (f - t0) / h;
            float s2 = s * s, s3 = s2 * s;
            return (2 * s3 - 3 * s2 + 1) * get(keys[i]) + (s3 - 2 * s2 + s) * h * Tan(i) + (-2 * s3 + 3 * s2) * get(keys[i + 1]) + (s3 - s2) * h * Tan(i + 1);
        }

        /// <summary>Horizontal 36 mm sensor (Blender's default fit) to Unity's vertical field of view.</summary>
        public static float VerticalFov(float lensMm, float aspect)
        {
            float half = Mathf.Atan(18f / lensMm);
            return 2 * Mathf.Atan(Mathf.Tan(half) / Mathf.Max(.1f, aspect)) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ keyed effect curves (real frames)
        public static float Curve(float frame, params float[] keys)
        {
            if (frame <= keys[0]) return keys[1];
            for (int i = 0; i + 3 < keys.Length; i += 2)
                if (frame <= keys[i + 2])
                    return Mathf.Lerp(keys[i + 1], keys[i + 3], Smooth((frame - keys[i]) / Mathf.Max(1e-4f, keys[i + 2] - keys[i])));
            return keys[keys.Length - 1];
        }

        public static float PaddleAura(float f) => Curve(f, 1, .35f, LockFrame - 12, .45f, LockFrame, 1f, LockFrame + 24, .7f, EndFrame, .75f);
        public static float PlumeIntensity(float f) => Curve(f, LockFrame - 2, 0, LockFrame + 2, 1.6f, LockFrame + 22, 1f, EndFrame, 1.05f);
        public static float PlumeScale(float f) => Curve(f, LockFrame - 1, .45f, LockFrame + 10, 1.06f, LockFrame + 24, 1f);
        public static float HeroKey(float f) => Curve(f, LockFrame - 16, 0, LockFrame - 2, 1);
        public static float Flash(float f) => Curve(f, LockFrame - 1, 0, LockFrame, .55f, LockFrame + 2, .38f, LockFrame + 10, 0);

        // ------------------------------------------------------------------ space conversion and helpers
        /// <summary>
        /// Blender (right handed, Z up) to Unity (left handed, Y up) exactly as the FBX import maps the stage meshes:
        /// (x, y, z) -> (-x, z, -y). Verified on import: the paddle's long axis lands on +Y and the printed "VS" on +Z.
        /// </summary>
        public static Vector3 ToUnity(Vector3 b) => new Vector3(-b.x, b.z, -b.y);

        public static Quaternion ToUnity(Quaternion b)
        {
            // R_u = M R_b M^T with the reflection M above; equal to a rotation about (-M) * axis.
            return new Quaternion(b.x, -b.z, b.y, b.w);
        }

        public static Quaternion AxisAngle(Vector3 axis, float radians)
        {
            axis = axis.normalized;
            float s = Mathf.Sin(radians * .5f);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(radians * .5f));
        }

        /// <summary>Rotation whose local X, Y, Z map to n x u, n (face normal) and u (long axis), like the Blender rig.</summary>
        public static Quaternion Basis(Vector3 n, Vector3 u)
        {
            n = n.normalized;
            u = (u - n * Vector3.Dot(u, n)).normalized;
            var x = Vector3.Cross(n, u);
            return FromColumns(x, n, u);
        }

        static Quaternion FromColumns(Vector3 c0, Vector3 c1, Vector3 c2)
        {
            float m00 = c0.x, m10 = c0.y, m20 = c0.z, m01 = c1.x, m11 = c1.y, m21 = c1.z, m02 = c2.x, m12 = c2.y, m22 = c2.z;
            float trace = m00 + m11 + m22;
            Quaternion q;
            if (trace > 0)
            {
                float s = Mathf.Sqrt(trace + 1) * 2;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, .25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1 + m00 - m11 - m22) * 2;
                q = new Quaternion(.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1 + m11 - m00 - m22) * 2;
                q = new Quaternion((m01 + m10) / s, .25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = Mathf.Sqrt(1 + m22 - m00 - m11) * 2;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, .25f * s, (m10 - m01) / s);
            }
            return q.normalized;
        }

        /// <summary>Deterministic 0..1 noise for camera shake and sparks.</summary>
        public static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
