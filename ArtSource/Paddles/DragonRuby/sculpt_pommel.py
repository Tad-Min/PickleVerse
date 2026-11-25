"""Dragon Ruby paddle - dragon-head pommel sculpted with signed distance fields (system Python: numpy, scikit-image).

    python sculpt_pommel.py <out.npz> [voxel_mm]

Coordinates are millimetres in paddle space: x right, y back (the painted front faces -y), z up, pommel bottom at z = 0.
The head looks to the front right and slightly down, like the painting. Each vertex carries a material id:
0 ruby skin, 1 gold, 2 obsidian, 3 ivory, 4 eye, 5 ruby mane.
"""
import sys
import numpy as np
from skimage import measure

OUT = sys.argv[1]
VOX = float(sys.argv[2]) if len(sys.argv) > 2 else .32
LO, HI = np.array([-34., -39., -3.]), np.array([38., 29., 66.])
N = np.ceil((HI - LO) / VOX).astype(int) + 1
axes = [LO[i] + np.arange(N[i]) * VOX for i in range(3)]
D = np.full(tuple(N), 40., np.float32)
M = np.zeros(tuple(N), np.int8)
SKIN, GOLD, BLACK, IVORY, EYE, MANE = range(6)

yaw, pitch = np.radians(31), np.radians(9)
F = np.array([np.sin(yaw) * np.cos(pitch), -np.cos(yaw) * np.cos(pitch), -np.sin(pitch)])
R = np.array([np.cos(yaw), np.sin(yaw), 0.])
U = np.cross(R, F) * -1
U = U / np.linalg.norm(U)
if U[2] < 0:
    U = -U
O = np.array([1.5, -1.0, 30.5])


def W(r, u, f):
    """head frame (right, up, forward) -> paddle millimetres"""
    return O + R * r + U * u + F * f


def box(lo, hi):
    a = np.clip(np.floor((lo - LO) / VOX).astype(int), 0, N - 1)
    b = np.clip(np.ceil((hi - LO) / VOX).astype(int) + 1, 1, N)
    s = tuple(slice(a[i], b[i]) for i in range(3))
    g = np.meshgrid(*[axes[i][s[i]] for i in range(3)], indexing='ij', copy=False)
    return s, g


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
    """smooth subtraction of d; the carved wall takes the material"""
    cur = D[s]
    h = np.clip(.5 - .5 * (cur + d) / max(k, 1e-3), 0, 1)
    new = cur * (1 - h) - d * h + k * h * (1 - h)
    m = M[s]
    m[(-d > cur - .6) & (d < .9)] = mat
    D[s] = new
    M[s] = m


def ellipsoid_d(g, c, radii, basis):
    p = [g[i] - c[i] for i in range(3)]
    q = [(p[0] * b[0] + p[1] * b[1] + p[2] * b[2]) / r for b, r in zip(basis, radii)]
    k0 = np.sqrt(q[0] ** 2 + q[1] ** 2 + q[2] ** 2)
    k1 = np.sqrt((q[0] / radii[0]) ** 2 + (q[1] / radii[1]) ** 2 + (q[2] / radii[2]) ** 2)
    return (k0 * (k0 - 1) / np.maximum(k1, 1e-5)).astype(np.float32)


def ellipsoid(c, radii, mat, k=2.5, basis=None, sub=False, margin=5):
    c = np.asarray(c, float)
    rm = max(radii) + margin
    s, g = box(c - rm, c + rm)
    d = ellipsoid_d(g, c, radii, basis if basis is not None else (R, U, F))
    (carve if sub else blend)(s, d, mat, k)


def catmull(pts, n):
    pts = np.asarray(pts, float)
    if len(pts) == 2:
        t = np.linspace(0, 1, n)[:, None]
        return pts[0] * (1 - t) + pts[1] * t
    p = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = []
    seg = len(pts) - 1
    for t in np.linspace(0, seg - 1e-6, n):
        i = int(t)
        u = t - i
        a, b, c, d = p[i], p[i + 1], p[i + 2], p[i + 3]
        out.append(.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u ** 3))
    return np.array(out)


def strand(pts, radii, mat, k=.8, n=22, flat=None, squash=1.0, sub=False):
    """tapered tube along a spline; flat = axis along which the section is squashed"""
    P = catmull(pts, n)
    r = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(radii)), radii)
    rm = max(radii) + 4
    s, g = box(P.min(0) - rm, P.max(0) + rm)
    d = np.full(g[0].shape, 60., np.float32)
    for i in range(n - 1):
        a, b = P[i], P[i + 1]
        ab = b - a
        l2 = max(ab @ ab, 1e-6)
        q = [g[j] - a[j] for j in range(3)]
        t = np.clip((q[0] * ab[0] + q[1] * ab[1] + q[2] * ab[2]) / l2, 0, 1)
        e = [q[j] - t * ab[j] for j in range(3)]
        if flat is not None:
            f = e[0] * flat[0] + e[1] * flat[1] + e[2] * flat[2]
            e = [e[j] + flat[j] * f * (squash - 1) for j in range(3)]
        dist = np.sqrt(e[0] ** 2 + e[1] ** 2 + e[2] ** 2)
        d = np.minimum(d, dist - (r[i] * (1 - t) + r[i + 1] * t))
    (carve if sub else blend)(s, d.astype(np.float32), mat, k)


def lock(pts, radii, mat, k=.5, n=26, parts=4, twist=1.6, spread=.62, phase=0.0, edge=None):
    """a hair lock or flame: a few tapered strands twisting about one spline"""
    P = catmull(pts, n)
    r = np.interp(np.linspace(0, 1, n), np.linspace(0, 1, len(radii)), radii)
    T = np.gradient(P, axis=0)
    T /= np.linalg.norm(T, axis=1, keepdims=True)
    up = np.array([.31, .22, .93])
    A = np.cross(T, up)
    A /= np.linalg.norm(A, axis=1, keepdims=True)
    B = np.cross(T, A)
    t = np.linspace(0, 1, n)
    for j in range(parts):
        ang = phase + 2 * np.pi * j / parts + twist * t
        off = (A * np.cos(ang)[:, None] + B * np.sin(ang)[:, None]) * (r * spread)[:, None]
        tip = 1 - .16 * ((j * 7) % parts) / parts
        m = max(int(n * tip), 6)
        rr = r[:m] * .62 * np.clip((1 - np.linspace(0, 1, m) ** 3), .06, 1)
        strand(P[:m] + off[:m], rr.tolist(), edge if edge is not None and j == 0 else mat, k, n=m)


# ---- knob --------------------------------------------------------------------------------------------------------------
world = (np.array([1., 0, 0]), np.array([0, 1., 0]), np.array([0, 0, 1.]))
ellipsoid((0.5, 1.5, 25), (25.0, 22.5, 22.5), BLACK, 0, world)
ellipsoid((0, 1, 44), (22.5, 19.5, 9), BLACK, 4, world)

# ---- face masses -------------------------------------------------------------------------------------------------------
ellipsoid(W(0, 5, 7), (19, 14, 15), SKIN, 3)                       # skull
ellipsoid(W(0, 9.5, 13), (15, 8, 9), SKIN, 4)                      # forehead
ellipsoid(W(0, -3.5, 19), (11.8, 7.2, 13), SKIN, 4)                # muzzle
ellipsoid(W(0, -1.0, 28), (9.2, 6.2, 6.4), SKIN, 3)                # nose block
ellipsoid(W(0, 4.2, 24), (4.6, 3.2, 8.5), SKIN, 3)                 # bridge
for sgn in (-1, 1):
    ellipsoid(W(13.5 * sgn, -2, 11), (7.2, 7.0, 9), SKIN, 4)       # cheek
    ellipsoid(W(4.6 * sgn, 1.0, 30.5), (3.9, 3.6, 3.6), SKIN, 1.6) # nostril wing
    ellipsoid(W(9.0 * sgn, -6.5, 24), (4.8, 4.0, 7.0), SKIN, 2.5)  # upper lip pad
ellipsoid(W(0, -18.2, 17.5), (10.2, 4.4, 13.5), SKIN, 2.5)         # lower jaw
ellipsoid(W(0, -18.8, 27), (6.6, 3.4, 5.2), SKIN, 2)               # chin

# ---- mouth, nostrils, eye sockets ----------------------------------------------------------------------------------------
ellipsoid(W(0, -12.2, 25), (10.2, 3.9, 17), BLACK, .8, sub=True)
for sgn in (-1, 1):
    ellipsoid(W(11.5 * sgn, -10.2, 14), (5.0, 3.0, 8.0), BLACK, .8, sub=True)
    ellipsoid(W(4.9 * sgn, .7, 34.4), (2.2, 1.9, 2.6), BLACK, .5, sub=True)
    ellipsoid(W(10.6 * sgn, 4.0, 21.2), (4.6, 3.2, 4.6), BLACK, .8, sub=True)
ellipsoid(W(0, -15.2, 20), (5.4, 1.7, 10), MANE, .6)               # tongue

for sgn in (-1, 1):
    X = lambda r, u, f: W(r * sgn, u, f)
    # eyes: glowing ball, slanted lids
    ellipsoid(X(10.0, 3.8, 18.9), (3.3, 2.7, 2.9), EYE, 0)
    ellipsoid(X(9.4, 3.6, 21.2), (.55, 1.9, .9), BLACK, 0)
    strand([X(5.2, 2.4, 22.8), X(8.2, 5.0, 23.0), X(12.4, 6.8, 20.8), X(16.4, 6.4, 16.0)], [1.0, 1.7, 1.6, .8], SKIN, .7)
    strand([X(6.0, 3.0, 22.4), X(9.0, 1.0, 22.6), X(12.8, 1.4, 20.4), X(15.8, 4.6, 16.6)], [.8, 1.15, 1.1, .6], SKIN, .7)
    strand([X(8.6, 1.9, 22.9), X(10.6, .9, 22.5), X(12.8, 1.4, 21.0)], [.35, .5, .35], GOLD, .2)
    # flaming gold brows
    lock([X(2.4, 5.2, 25.2), X(6.2, 7.8, 25.2), X(11.4, 9.4, 22.4), X(16.6, 11.6, 16.8), X(21.5, 15.8, 8)], [1.3, 2.7, 2.8, 2.0, .4], GOLD, .5, parts=4, twist=.9)
    lock([X(5.4, 8.6, 24.2), X(9.2, 12.2, 22.6), X(14, 15.6, 17.0), X(17.5, 20.5, 10)], [1.4, 2.0, 1.5, .3], GOLD, .5, parts=3, twist=.8)
    lock([X(3.6, 8.2, 24.6), X(5.6, 13.0, 22.6), X(8.6, 17.6, 18.0)], [1.1, 1.6, .3], GOLD, .5, parts=3, twist=.6)
    strand([X(12, 8.6, 21.4), X(17.4, 9.4, 16.5), X(22.4, 10.8, 8.5)], [1.2, 1.5, .35], GOLD, .6)
    # nose and muzzle ridges
    strand([X(2.2, 5.8, 26.5), X(4.4, 3.8, 29.2), X(7.2, .6, 30.4), X(7.6, -2.2, 29.2)], [.9, 1.3, 1.2, .8], SKIN, .8)
    strand([X(6.6, 3.0, 25.6), X(9.4, -.4, 25.2), X(11.8, -3.6, 22.0), X(15.0, -4.0, 16.0)], [.8, 1.2, 1.2, .8], SKIN, .8)
    strand([X(8.8, 1.2, 22.4), X(12.2, -1.2, 20.6), X(16.2, -1.0, 15.2), X(19.2, 1.2, 9.2)], [.7, 1.2, 1.4, .9], SKIN, .8)
    for q, (u, f) in enumerate(((6.6, 27.0), (5.4, 28.6), (3.6, 30.6))):
        strand([X(.2, u, f), X(2.6, u - .6, f - .2), X(4.8, u - 2.2, f - 1.4)], [.75, .8, .35], SKIN, .5)
    for q, (r, u, f) in enumerate(((3.4, 13.2, 21.8), (6.4, 15.8, 18.6), (2.6, 17.6, 18.0), (11.6, 13.6, 18.4))):
        ellipsoid(X(r, u, f), (2.0, 2.0, 1.2), SKIN, .5)
    for i, t in enumerate(np.linspace(0, 1, 7)):                   # beaded cheek ridge
        c = catmull([X(12.0, -.4, 21.4), X(16.6, .6, 16.4), X(19.8, 2.6, 10.6), X(21.4, 4.4, 6)], 20)[int(t * 19)]
        ellipsoid(c, (1.55 - .08 * i,) * 3, SKIN, .5)
    # lips: upper S-curve and lower lip, gold edge at the corner of the mouth
    strand([X(0, -6.6, 33.2), X(5.2, -7.6, 32.0), X(10.0, -9.0, 27.2), X(12.6, -7.2, 21.6), X(15.6, -6.2, 16), X(18.2, -9.2, 10.6)], [1.3, 1.5, 1.5, 1.3, 1.3, .9], SKIN, .9)
    strand([X(0, -15.4, 32.0), X(5.0, -15.4, 30.6), X(9.0, -15.8, 25.4), X(12.4, -15.4, 18.6), X(17.4, -11.2, 11)], [1.2, 1.4, 1.35, 1.25, .9], SKIN, .8)
    strand([X(12.8, -8.0, 22.2), X(16.6, -7.4, 16.4), X(18.8, -9.8, 11.8), X(16.8, -12.2, 13.2), X(13.4, -14.0, 18)], [.5, .62, .62, .6, .4], GOLD, .25)
    # teeth: four fangs and small rows
    strand([X(6.4, -8.6, 30.4), X(6.8, -11.4, 30.4), X(6.4, -14.2, 29.4)], [1.5, 1.05, .2], IVORY, .25)
    strand([X(7.8, -15.4, 27.2), X(8.0, -12.6, 27.4), X(7.6, -10.2, 26.8)], [1.3, .9, .2], IVORY, .25)
    for f, r, h in ((32.2, 2.0, 2.2), (31.6, 4.2, 2.2), (27.4, 9.4, 2.4), (24.4, 11.0, 2.4), (21.4, 12.4, 2.3), (18.4, 13.8, 2.1)):
        strand([X(r, -8.0, f), X(r, -8.4 - h, f - .2)], [.95, .22], IVORY, .2, n=6)
    for f, r, h in ((30.2, 2.2, 2.0), (29.0, 4.8, 2.0), (23.6, 9.2, 2.2), (20.4, 10.8, 2.0)):
        strand([X(r, -15.2, f), X(r, -14.8 + h, f - .2)], [.9, .2], IVORY, .2, n=6)
    # ears: leaf with a golden rim
    flat = (R * sgn * .35 + U * -.25 + F * .9)
    flat = flat / np.linalg.norm(flat)
    strand([X(16.5, 8.5, 9.0), X(22.0, 13.0, 7.2), X(26.5, 18.8, 3.5), X(29.5, 24.5, -1.5)], [3.0, 4.2, 2.8, .4], SKIN, .8, flat=flat, squash=1.9)
    strand([X(18.2, 9.4, 11.0), X(22.2, 13.2, 9.4), X(26.0, 18.6, 5.8)], [1.5, 2.3, .5], GOLD, .3, flat=flat, squash=1.7)
    # horns: gold, with ring and tine, rising in front of the grip
    strand([X(7.6, 13.5, 15.5), X(9.4, 19.5, 17.2), X(11.8, 26, 17.5), X(15.5, 32, 15.5), X(17.0, 38.5, 13.5)], [3.2, 2.7, 2.2, 1.6, .4], GOLD, .6, n=28)
    strand([X(11.4, 25.2, 17.6), X(9.0, 30.0, 18.4), X(8.6, 35.0, 18.0)], [1.7, 1.2, .3], GOLD, .6)
    strand([X(7.9, 14.6, 15.8), X(8.4, 16.4, 16.4)], [3.9, 3.7], GOLD, .3, n=6)
    ellipsoid(X(7.6, 12.6, 15.0), (5.0, 3.6, 4.6), SKIN, 1.5)
    # cheek mane: layered flames sweeping back and up around the knob
    rows = ((9.5, 6.0, 15.0, 4.5, 2.3), (5.6, 2.2, 9.5, 5.0, 2.6), (1.6, -2.6, 4.5, 5.2, 2.7), (-2.6, -7.2, -.8, 6.0, 2.7),
            (-7.0, -12.0, -6.0, 7.5, 2.6), (-11.2, -16.6, -11.4, 9.0, 2.4), (-14.6, -20.2, -17.0, 11.0, 2.0))
    for q, (u0, u1, u2, f0, r0) in enumerate(rows):
        lift = 2.2 * np.sin(q * 2.1)
        lock([X(17.0, u0, f0 + 5), X(22.8, u1 + 2.5, f0 - 3), X(25.4, u1 + 1.0 + lift, f0 - 11), X(23.2, u2 + lift, f0 - 19), X(18.6, u2 + 5.0 + lift, f0 - 26)],
             [r0 * .7, r0, r0 * .9, r0 * .6, .3], MANE, .45, n=28, parts=4, twist=2.2, phase=q)
        lock([X(18.6, u0 - 2.0, f0 + 4.5), X(24.2, u1 + .4, f0 - 3), X(27.6, u1 + 1.6 + lift, f0 - 9.5), X(28.4, u1 + 5.2 + lift, f0 - 14)],
             [r0 * .45, r0 * .62, r0 * .42, .2], SKIN, .4, n=20, parts=3, twist=1.6, phase=q * 1.7)
        strand([X(19.2, u0 - 1.0, f0 + 5), X(24.8, u1 + 1.4, f0 - 3.2), X(27.0, u1 + 2.2 + lift, f0 - 10.5)], [.35, .62, .18], GOLD, .25)
    for q, (u, f, w) in enumerate(((12.5, 7.5, 1.7), (3.5, 10.0, 1.7), (-5.0, 10.5, 1.7))):
        lock([X(15.2, u, f + 4), X(20.0, u - 2.6, f - .5), X(24.0, u - 1.8, f - 7), X(25.4, u + 2.0, f - 12)], [w * .6, w, w * .7, .2], MANE, .4, n=22, parts=3, twist=1.4, phase=q * 2.0)
    # gold beard and jaw flames
    lock([X(2.2, -20.6, 28.6), X(3.0, -25.0, 27.4), X(1.4, -29.6, 22.8)], [1.9, 1.6, .3], GOLD, .45, parts=3)
    lock([X(5.2, -19.8, 26.4), X(7.4, -24.2, 24.0), X(6.4, -28.6, 18.0)], [1.6, 1.5, .3], GOLD, .45, parts=3)
    lock([X(9.2, -18.4, 20.6), X(12.8, -22.2, 16.4), X(12.6, -26.2, 8)], [1.5, 1.5, .3], GOLD, .45, parts=3)
    strand([X(12.6, -16.2, 15.4), X(17.6, -18.6, 9.4), X(18.4, -21.6, 1.0), X(13, -22, -7)], [1.2, 1.4, 1.0, .3], GOLD, .6)
    # whiskers curling off the muzzle
    strand([X(8.6, -3.2, 30.0), X(13.6, -1.8, 28.4), X(18.4, -5.0, 22.6), X(21.6, -2.2, 15.5), X(24.8, 2.2, 10)], [.95, .85, .7, .55, .2], GOLD, .25, n=30)

ellipsoid(W(0, 8.6, 26.0), (2.3, 2.3, 1.8), SKIN, .5)               # brow jewel
strand([W(0, 10.5, 24.6), W(0, 14.5, 21.2), W(0, 18.0, 16.5)], [1.5, 1.9, .5], SKIN, .8)
lock([W(0, -21, 28.0), W(0, -25.8, 26.8), W(0, -30.5, 21.4)], [1.8, 1.5, .3], GOLD, .45, parts=3)

# ---- mane crown around the grip and flame scrolls on the back of the knob --------------------------------------------------
rng = np.random.default_rng(11)
back = np.array([np.sin(yaw), -np.cos(yaw)])
for i in range(19):
    a = 2 * np.pi * i / 19
    dirv = np.array([np.sin(a), -np.cos(a)])
    if dirv @ back > .80:
        continue
    tang = np.array([np.cos(a), np.sin(a)]) * (.45 if i % 2 else -.30)
    h = 9 + 7 * rng.random()
    b = np.array([dirv[0] * 23.4, dirv[1] * 20.4 + 1])
    lock([np.array([b[0], b[1], 37]), np.array([b[0] * 1.05, b[1] * 1.05, 43]),
          np.array([b[0] * 1.02 + tang[0] * 7, b[1] * 1.02 + tang[1] * 7, 43 + h * .55]), np.array([b[0] * .94 + tang[0] * 15, b[1] * .94 + tang[1] * 15, 43 + h])],
         [2.2, 2.7, 1.7, .25], GOLD if i % 5 == 2 else MANE, .45, n=22, parts=3, twist=1.2, phase=i)
for i in range(15):                                                  # back of the knob: ruby locks, gold flames between them
    q = np.radians(-118 + 236 * i / 14) + np.pi + np.arctan2(back[0], -back[1])
    dirv = np.array([np.sin(q), -np.cos(q)])
    tang = np.array([np.cos(q), np.sin(q)])
    P = []
    for t, z in ((0, 42), (.28, 35), (.6, 24), (.85, 14), (1, 7)):
        rad = np.sqrt(max(1 - ((z - 25) / 24.5) ** 2, .06))
        sway = np.sin(t * 6.0 + i * 1.3) * 3.0
        P.append(np.array([dirv[0] * 25.4 * rad + tang[0] * sway + .5, dirv[1] * 23.0 * rad + tang[1] * sway + 1.5, z]))
    lock(P, [1.3, 2.7, 2.2, 1.3, .3], MANE if i % 2 == 0 else SKIN, .45, n=30, parts=4, twist=2.4, phase=i)
    if i % 2:
        strand([p + np.array([dirv[0], dirv[1], 0]) * 1.5 for p in P], [.3, .75, .7, .45, .15], GOLD, .25, n=26)
for i in range(15):                                                  # gold flames licking up from the base
    a = 2 * np.pi * i / 15 + .2
    dirv = np.array([np.sin(a), -np.cos(a)])
    if dirv @ back > .74:
        continue
    tang = np.array([np.cos(a), np.sin(a)])
    P = []
    for t, z in ((0, 4), (.35, 8.5), (.7, 14), (1, 20)):
        rad = np.sqrt(max(1 - ((z - 25) / 23.4) ** 2, .05))
        P.append(np.array([dirv[0] * 26.0 * rad + tang[0] * np.sin(t * 3) * 5 + .5, dirv[1] * 23.6 * rad + tang[1] * np.sin(t * 3) * 5 + 1.5, z]))
    lock(P, [1.0, 1.8, 1.2, .2], GOLD, .4, n=20, parts=3, twist=1.0, phase=i)

# flat bottom so the paddle can stand, clipped to the pommel
zg = axes[2][None, None, :]
D[:] = np.maximum(D, (-zg - 0.0).astype(np.float32))

verts, faces, normals, _ = measure.marching_cubes(D, 0.0, spacing=(VOX,) * 3)
idx = np.clip(np.round(verts / VOX).astype(int), 0, N - 1)
mats = M[idx[:, 0], idx[:, 1], idx[:, 2]]
verts = verts + LO
np.savez_compressed(OUT, verts=verts.astype(np.float32), faces=faces[:, ::-1].astype(np.int32), mats=mats.astype(np.int8))
print('pommel', len(verts), 'verts', len(faces), 'tris', 'bounds', verts.min(0).round(1), verts.max(0).round(1),
      'materials', np.bincount(mats.astype(int), minlength=6).tolist())
