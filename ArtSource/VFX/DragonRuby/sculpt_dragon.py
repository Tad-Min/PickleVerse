"""Dragon Ruby VFX - the red dragon that chases the ball, sculpted straight with signed distance fields (system Python).

    python sculpt_dragon.py <out.npz> [voxel_m]

Axes (metres): s = distance behind the ball (nose at s = 0, tail tip near s = 2.3), y = dorsal, z = lateral.
The in-game shader bends this straight dragon along the sine path, so every part keeps its s.
Vertex colour: rgb albedo (linear), a = glow.
"""
import sys
import numpy as np
from skimage import measure

OUT = sys.argv[1]
VOX = float(sys.argv[2]) if len(sys.argv) > 2 else .0036
LO, HI = np.array([-.03, -.215, -.185]), np.array([2.40, .235, .185])
N = np.ceil((HI - LO) / VOX).astype(int) + 1
axes = [LO[i] + np.arange(N[i]) * VOX for i in range(3)]
D = np.full(tuple(N), .5, np.float32)
M = np.zeros(tuple(N), np.int8)
RED, GOLD, BLACK, IVORY, EYE, MANE, BELLY = range(7)
COL = np.array([[.50, .010, .012, .10], [.92, .44, .09, .20], [.02, .004, .004, .0], [.95, .74, .42, .12], [1.0, .70, .18, 1.0],
                [.80, .045, .012, .45], [.72, .26, .06, .10]], np.float32)


def box(lo, hi):
    a = np.clip(np.floor((lo - LO) / VOX).astype(int), 0, N - 1)
    b = np.clip(np.ceil((hi - LO) / VOX).astype(int) + 1, 1, N)
    s = tuple(slice(a[i], b[i]) for i in range(3))
    return s, np.meshgrid(*[axes[i][s[i]] for i in range(3)], indexing='ij', copy=False)


def blend(s, d, mat, k):
    cur = D[s]
    closer = d < cur
    if k > 0:
        h = np.clip(.5 + .5 * (cur - d) / k, 0, 1)
        D[s] = cur * (1 - h) + d * h - k * h * (1 - h)
    else:
        D[s] = np.minimum(cur, d)
    m = M[s]
    m[closer] = mat
    M[s] = m


def carve(s, d, mat, k):
    cur = D[s]
    h = np.clip(.5 - .5 * (cur + d) / max(k, 1e-4), 0, 1)
    m = M[s]
    m[(-d > cur - .004) & (d < .006)] = mat
    D[s] = cur * (1 - h) - d * h + k * h * (1 - h)
    M[s] = m


def ellipsoid(c, r, mat, k=.012, sub=False):
    c = np.asarray(c, float)
    rm = max(r) + .03
    s, g = box(c - rm, c + rm)
    q = [(g[i] - c[i]) / r[i] for i in range(3)]
    k0 = np.sqrt(sum(x * x for x in q))
    k1 = np.sqrt(sum((q[i] / r[i]) ** 2 for i in range(3)))
    d = (k0 * (k0 - 1) / np.maximum(k1, 1e-3)).astype(np.float32)
    (carve if sub else blend)(s, d, mat, k)


def catmull(pts, n):
    pts = np.asarray(pts, float)
    if len(pts) == 2:
        t = np.linspace(0, 1, n)[:, None]
        return pts[0] * (1 - t) + pts[1] * t
    p = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = []
    for t in np.linspace(0, len(pts) - 1 - 1e-6, n):
        i, u = int(t), t - int(t)
        a, b, c, d = p[i:i + 4]
        out.append(.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u ** 3))
    return np.array(out)


def strand(pts, radii, mat, k=.005, n=18, flat=None, squash=1.0):
    P = catmull(pts, n)
    r = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(radii)), radii)
    rm = max(radii) + .025
    s, g = box(P.min(0) - rm, P.max(0) + rm)
    d = np.full(g[0].shape, .6, np.float32)
    for i in range(n - 1):
        a, b = P[i], P[i + 1]
        ab = b - a
        q = [g[j] - a[j] for j in range(3)]
        t = np.clip(sum(q[j] * ab[j] for j in range(3)) / max(ab @ ab, 1e-9), 0, 1)
        e = [q[j] - t * ab[j] for j in range(3)]
        if flat is not None:
            e[flat] = e[flat] * squash
        d = np.minimum(d, np.sqrt(sum(x * x for x in e)) - (r[i] * (1 - t) + r[i + 1] * t))
    blend(s, d.astype(np.float32), mat, k)


def lock(pts, radii, mat, parts=3, twist=1.5, phase=0., k=.004, n=20):
    P = catmull(pts, n)
    r = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(radii)), radii)
    T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True) + 1e-9
    A = np.cross(T, [.2, .3, .93])
    A /= np.linalg.norm(A, axis=1, keepdims=True) + 1e-9
    B = np.cross(T, A)
    t = np.linspace(0, 1, n)
    for j in range(parts):
        a = phase + 2 * np.pi * j / parts + twist * t
        o = (A * np.cos(a)[:, None] + B * np.sin(a)[:, None]) * (r * .6)[:, None]
        m = max(int(n * (1 - .14 * j / parts)), 6)
        strand(P[:m] + o[:m], (r[:m] * .62 * np.clip(1 - np.linspace(0, 1, m) ** 3, .08, 1)).tolist(), mat, k, n=m)


# ---- body ----------------------------------------------------------------------------------------------------------------
ss = np.array([.14, .24, .34, .55, .85, 1.20, 1.55, 1.85, 2.08, 2.25])
rr = np.array([.040, .040, .046, .058, .060, .053, .041, .028, .015, .004])


def radius(s):
    return np.interp(s, ss, rr)


# The trunk is a ring mesh (build_dragon_vfx.py adds it) so it bends smoothly; only a short neck joins the sculpted head.
strand([[.13, 0, 0], [.20, 0, 0]], [.040, .038], RED, 0, n=6)
i, s = 0, .30
while s < 2.06:                                                           # dorsal flames
    r = radius(s)
    h = (.052 if i % 3 == 1 else .036) * (.45 + .55 * r / .06)
    strand([[s, r * .80, 0], [s + h * .35, r + h * .55, 0], [s + h * 1.5, r + h * 1.15, 0]], [.016 * r / .06 + .004, .010 * r / .06 + .003, .0016], GOLD if i % 3 else MANE, .004, n=9, flat=2, squash=1.5)
    i += 1
    s += .046 + .012 * (r / .06)

# ---- head ----------------------------------------------------------------------------------------------------------------
ellipsoid((.160, .014, 0), (.078, .056, .056), RED, .02)
ellipsoid((.118, .030, 0), (.050, .032, .046), RED, .018)
ellipsoid((.080, .004, 0), (.074, .029, .038), RED, .018)
ellipsoid((.022, .012, 0), (.024, .019, .026), RED, .012)
ellipsoid((.066, .026, 0), (.048, .012, .014), RED, .010)
strand([[.175, -.036, 0], [.11, -.066, 0], [.045, -.086, 0]], [.034, .027, .016], RED, .014, flat=1, squash=1.5)
ellipsoid((.072, -.036, 0), (.082, .017, .032), BLACK, .004, sub=True)
strand([[.150, -.044, 0], [.10, -.058, 0], [.062, -.058, 0]], [.015, .012, .004], MANE, .003, flat=1, squash=2)
for q in (-1, 1):
    Z = lambda s, y, z: [s, y, z * q]
    ellipsoid(Z(.012, .020, .015), (.014, .012, .011), RED, .006)
    ellipsoid(Z(.002, .022, .016), (.007, .005, .005), BLACK, .002, sub=True)
    ellipsoid(Z(.132, -.004, .046), (.044, .030, .020), RED, .012)
    ellipsoid(Z(.112, .032, .040), (.015, .012, .012), EYE, 0)
    ellipsoid(Z(.103, .033, .050), (.003, .009, .004), BLACK, 0)
    strand([Z(.088, .030, .036), Z(.108, .046, .046), Z(.140, .046, .050)], [.006, .010, .005], RED, .004)
    lock([Z(.082, .036, .024), Z(.112, .056, .044), Z(.150, .066, .056), Z(.200, .092, .066)], [.007, .014, .011, .002], GOLD, 3, 1.0)
    lock([Z(.100, .046, .020), Z(.136, .076, .030), Z(.178, .106, .038)], [.006, .010, .002], GOLD, 3, .8)
    strand([Z(.010, -.006, .020), Z(.05, -.016, .036), Z(.10, -.022, .044), Z(.145, -.012, .052)], [.006, .007, .007, .005], RED, .004)
    strand([Z(.045, -.076, .014), Z(.09, -.062, .032), Z(.145, -.036, .046)], [.006, .007, .006], RED, .004)
    for s, z, h in ((.016, .016, .020), (.044, .030, .026), (.070, .035, .018), (.094, .038, .016)):
        strand([Z(s, -.016, z), Z(s + .004, -.018 - h, z)], [.0062, .0012], IVORY, .001, n=6)
    for s, z, h in ((.052, .016, .022), (.080, .026, .016), (.106, .032, .014)):
        strand([Z(s, -.066, z), Z(s - .002, -.066 + h, z)], [.0058, .0012], IVORY, .001, n=6)
    # antlers, ears, whiskers, beard
    strand([Z(.152, .058, .022), Z(.190, .104, .034), Z(.250, .132, .044), Z(.330, .186, .050), Z(.390, .200, .046)], [.015, .012, .010, .007, .0015], GOLD, .004, n=24)
    strand([Z(.240, .130, .043), Z(.270, .176, .062), Z(.300, .206, .066)], [.008, .006, .0015], GOLD, .003)
    strand([Z(.150, .054, .022), Z(.162, .072, .026)], [.019, .017], GOLD, .003, n=5)
    strand([Z(.176, .036, .054), Z(.215, .056, .088), Z(.275, .074, .102)], [.014, .017, .002], RED, .005, flat=1, squash=1.8)
    strand([Z(.024, .004, .028), Z(-.004, .020, .062), Z(.050, .056, .104), Z(.170, .040, .134), Z(.300, .082, .138), Z(.430, .048, .120)], [.0062, .0058, .0054, .0046, .0036, .0010], GOLD, .002, n=36)
    strand([Z(.030, -.012, .030), Z(.030, -.052, .060), Z(.110, -.086, .076), Z(.230, -.074, .084)], [.0054, .005, .004, .001], GOLD, .002, n=22)
    lock([Z(.062, -.088, .012), Z(.092, -.122, .016), Z(.150, -.150, .014)], [.008, .009, .002], GOLD, 3)
    lock([Z(.112, -.074, .028), Z(.160, -.108, .036), Z(.230, -.124, .034)], [.008, .010, .002], MANE, 3)
    # flaming mane
    for j, (s0, y0, z0, s1, y1, z1, w) in enumerate(((.17, .060, .014, .40, .120, .030, .016), (.18, .040, .040, .42, .080, .092, .017), (.18, .010, .050, .44, .024, .116, .018),
                                                    (.18, -.018, .048, .42, -.044, .104, .017), (.17, -.044, .036, .39, -.098, .072, .015),
                                                    (.24, .044, .026, .54, .096, .060, .015), (.25, .000, .040, .56, .016, .096, .016), (.24, -.034, .030, .52, -.080, .062, .014))):
        wob = .022 * np.sin(j * 2.3)
        lock([Z(s0, y0, z0), Z(s0 * .66 + s1 * .34, y0 * .5 + y1 * .62 + wob, z0 * .4 + z1 * .75), Z(s0 * .30 + s1 * .70, y1 * 1.02 - wob, z1), Z(s1, y1 * .86 + wob, z1 * .9)],
             [w * .8, w, w * .7, .0018], MANE if j % 3 else GOLD, 3, 1.8, j)
    # legs with golden claws
    for s0, sc in ((.66, 1.0), (1.28, .85)):
        r = radius(s0)
        hip, knee, foot = Z(s0, -r * .35, r * .75), Z(s0 + .075 * sc, -r * .9 - .035 * sc, r + .052 * sc), Z(s0 + .005 * sc, -r - .085 * sc, r + .050 * sc)
        strand([hip, knee, foot], [.030 * sc, .022 * sc, .015 * sc], RED, .010)
        lock([knee, [knee[0] + .05, knee[1] + .012, knee[2]], [knee[0] + .11, knee[1] + .03, knee[2]]], [.010 * sc, .010 * sc, .0015], MANE, 3)
        for a in (-1, 0, 1):
            tip = [foot[0] - .046 * sc, foot[1] - .012 * sc, foot[2] + .020 * a * sc]
            strand([foot, [foot[0] - .024 * sc, foot[1] + .004, foot[2] + .016 * a * sc], tip], [.011 * sc, .009 * sc, .0016], GOLD, .003, n=8)
for j, (y, z) in enumerate(((.0, .0), (.042, .010), (-.036, -.012), (.016, .044), (-.012, -.044), (.066, -.02), (-.056, .024))):
    lock([[2.06, 0, 0], [2.15, y * .6, z * .6], [2.25, y * 1.05, z * 1.1], [2.37, y * .8, z * .8]], [.008, .014, .010, .0016], MANE if j % 2 else GOLD, 3, 2.0, j)
strand([[.06, -.084, 0], [.10, -.128, 0], [.16, -.166, 0]], [.008, .009, .002], GOLD, .004)

verts, faces, _, _ = measure.marching_cubes(D, 0., spacing=(VOX,) * 3)
idx = np.clip(np.round(verts / VOX).astype(int), 0, N - 1)
mats = M[idx[:, 0], idx[:, 1], idx[:, 2]]
verts = verts + LO
col = COL[mats]
# painterly shading: darker underside and crevices, hot tips on the mane
shade = np.clip(.72 + .28 * (verts[:, 1] / .08), .55, 1.0)
col[:, :3] *= np.where(np.isin(mats, (RED, BELLY))[:, None], shade[:, None], 1)
np.savez_compressed(OUT, verts=verts.astype(np.float32), faces=faces.astype(np.int32), col=col.astype(np.float32))
print('dragon', len(verts), 'verts', len(faces), 'tris', verts.min(0).round(3), verts.max(0).round(3))
