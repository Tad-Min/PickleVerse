"""Blue Phoenix VFX - sprite textures, the spirit phoenix mesh, its emblem and design previews (Blender 5.2, script only).

    blender --background --factory-startup --python build_phoenix_vfx.py -- <work_dir> textures <unity_texture_dir>
    blender --background --factory-startup --python build_phoenix_vfx.py -- <work_dir> mesh     <unity_model_dir>
    blender --background --factory-startup --python build_phoenix_vfx.py -- <work_dir> emblem   <unity_texture_dir>
    blender --background --factory-startup --python build_phoenix_vfx.py -- <work_dir> preview

Spine space of the mesh (metres, what the shader reads): x = distance behind the ball (beak at 0, tail tips near 1.65),
y = across the spread wings, z = the phoenix's back. The game bends it along the ball's path (PhoenixBallMotion +
PhoenixSpirit.shader): the body and wings ride one rigid frame, the tail follows the path.
UV0: feather atlas. UV1: x = how much the vertex flaps with the wing (0..1), y = how far along its feather (ash at the tips).
Vertex colour: rgb glow colour (linear), a = glow strength.
"""
import math, os, sys
import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:]
WORK, MODE = ARGS[0], ARGS[1]
os.makedirs(WORK, exist_ok=True)
ATLAS_W, ATLAS_H = 1024, 512


def st(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise(h, w, cy, cx, seed, octaves=4):
    """tileable value noise 0..1 (cy, cx cells of the first octave)"""
    r = np.random.default_rng(seed)
    out, amp, tot = np.zeros((h, w), np.float32), 1.0, 0.0
    yy = (np.arange(h) + .5) / h
    xx = (np.arange(w) + .5) / w
    for _ in range(octaves):
        g = r.random((cy, cx)).astype(np.float32)
        y, x = yy * cy, xx * cx
        iy, ix = np.floor(y).astype(int), np.floor(x).astype(int)
        fy, fx = y - iy, x - ix
        fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
        a = g[iy[:, None] % cy, ix[None, :] % cx]
        b = g[iy[:, None] % cy, (ix[None, :] + 1) % cx]
        c = g[(iy[:, None] + 1) % cy, ix[None, :] % cx]
        d = g[(iy[:, None] + 1) % cy, (ix[None, :] + 1) % cx]
        out += amp * ((a + (b - a) * fx[None]) * (1 - fy[:, None]) + (c + (d - c) * fx[None]) * fy[:, None])
        tot += amp
        amp *= .5
        cy, cx = cy * 2, cx * 2
    return out / tot


def save(path, rgb, a):
    rgba = np.dstack([rgb, rgb, rgb, a]) if rgb.ndim == 2 else np.dstack([rgb, a])
    rgba = np.clip(rgba, 0, 1).astype(np.float32)
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(rgba[::-1].reshape(-1))
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


# =========================================================================================================================
# textures
# =========================================================================================================================
def feather_atlas():
    """1024 x 512: wing feather (left top), body down (right top), long tail plume with its flame eye (bottom row).
    RGB = brightness (shaft, barbs, glowing rim), A = coverage."""
    val = np.zeros((ATLAS_H, ATLAS_W), np.float32)
    cov = np.zeros_like(val)
    # wing feather: u along (root 0 -> tip 1), v across (.5 shaft)
    h, w = 256, 512
    v, u = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = (u + .5) / w, (v + .5) / h
    n1 = noise(h, w, 4, 24, 3)
    width = .47 * st(0, .08, u) * (1 - st(.50, 1.0, u)) ** .45 + .02
    side = np.abs(v - .5)
    d = side / width
    ragged = 1 - .26 * (n1 - .3) * st(.25, .9, u)
    notch = st(.05, 0, np.abs(((u * 7 + (v > .5) * .5) % 1) - .5) - .45) * st(.45, .7, d)
    shape = st(1.0, .86, d / ragged) * (1 - notch * .6)
    barbs = st(.18, 0, np.abs(((u * 34 - side * 22) % 1) - .5) - .28)
    shaft = np.exp(-((v - .5) / .014) ** 2) * (1 - st(.75, 1, u))
    rim = st(.55, .95, d / ragged)
    value = np.clip(.30 + .30 * barbs + .55 * shaft + .55 * rim + .25 * u, 0, 1)
    val[:h, :w], cov[:h, :w] = value * shape, shape
    # body down: opaque, soft overlapping scales along u
    v, u = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = (u + .5) / w, (v + .5) / h
    cell = np.abs(((u * 10 + (np.floor(v * 6) % 2) * .5) % 1) - .5) + np.abs(((v * 6) % 1) - .5) * .8
    val[:h, w:] = .55 + .35 * st(.55, .2, cell) + .1 * noise(h, w, 8, 16, 5)
    cov[:h, w:] = 1
    # tail plume: a slender streamer opening into a flame-shaped eye near the tip
    h, w = 256, 1024
    v, u = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = (u + .5) / w, (v + .5) / h
    n2 = noise(h, w, 4, 40, 7)
    eye_c = .80
    open_ = np.exp(-((u - eye_c) / .11) ** 2)
    width = (.10 + .36 * open_) * st(0, .05, u) * (1 - st(.9, 1.0, u)) + .015
    side = np.abs(v - .5)
    d = side / width
    tongue = 1 - .35 * (n2 - .35) * st(.1, .9, u)
    shape = st(1.0, .8, d / tongue)
    ring = np.hypot((u - eye_c) / .07, (v - .5) / .22)
    eye = np.exp(-((ring - .62) / .12) ** 2) + st(.35, .0, ring) * .9
    barbs = st(.2, 0, np.abs(((u * 70 - side * 30) % 1) - .5) - .3)
    shaft = np.exp(-((v - .5) / .012) ** 2) * (1 - open_ * .6)
    rim = st(.5, .95, d / tongue)
    value = np.clip(.28 + .25 * barbs + .5 * shaft + .45 * rim + .7 * eye * open_, 0, 1)
    val[256:, :], cov[256:, :] = value * shape, shape
    return val, cov


def textures(out):
    os.makedirs(out, exist_ok=True)
    v, a = feather_atlas()
    save(os.path.join(out, 'T_BluePhoenix_Feathers.png'), v, a)
    # fire ribbon (tileable along u)
    h, w = 128, 512
    n1, n2 = noise(h, w, 4, 6, 1), noise(h, w, 8, 14, 2)
    vv = np.abs((np.arange(h) + .5) / h * 2 - 1)[:, None]
    tongue = st(1.0, .12, vv + (n1 - .45) * 1.15)
    value = np.clip(tongue * (.45 + .75 * n2) + st(.55, 0, vv) * .25, 0, 1)
    save(os.path.join(out, 'T_BluePhoenix_Fire.png'), value, np.clip(tongue * (.55 + .6 * n2), 0, 1))
    # flame flipbook 4x4: a lick of flame that rises, splits and burns out
    cell = 128
    sheet, alpha = np.zeros((cell * 4, cell * 4)), np.zeros((cell * 4, cell * 4))
    yy, xx = np.mgrid[0:cell, 0:cell] / (cell - 1)
    for f in range(16):
        t = f / 15
        n = np.roll(noise(cell, cell, 4, 4, 40, 4), int(-t * cell * .9), axis=0)
        y = 1 - yy
        x = (xx - .5) + (n - .5) * .30 * y * (1 + t)
        height = .55 + .35 * st(0, .4, t)
        half = (.23 * (1 - .45 * t)) * np.sqrt(np.clip(1 - (y / height) ** 1.6, 0, 1)) * st(-.02, .18, y)
        body = st(0, .10, half - np.abs(x)) * st(0, .03, y)
        burn = st(t * 1.15 - .25, t * 1.15 + .05, 1 - n * .9 - y * .22 * t)
        aa = body * burn * (1 - st(.82, 1, t))
        core = st(0, .16, half * .62 - np.abs(x)) * (1 - y * .7)
        cy, cx = divmod(f, 4)
        sheet[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = np.clip(.45 + .75 * core, 0, 1) * aa
        alpha[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = aa
    save(os.path.join(out, 'T_BluePhoenix_Flame.png'), sheet, alpha)
    # ash flakes 4x4: charred shards, R = embers glowing along the broken edges and in specks, A = coverage
    sheet, alpha = np.zeros((cell * 4, cell * 4)), np.zeros((cell * 4, cell * 4))
    rng = np.random.default_rng(9)
    yy, xx = (np.mgrid[0:cell, 0:cell] + .5) / cell * 2 - 1
    ang = np.arctan2(yy, xx)
    rad = np.hypot(xx, yy)
    for f in range(16):
        k = rng.integers(5, 9)
        a0 = np.sort(rng.random(k)) * 2 * np.pi
        rr = .35 + .45 * rng.random(k)
        # polygon radius as a function of the angle (straight edges between random corners)
        prof = np.interp(ang % (2 * np.pi), np.concatenate([a0, [a0[0] + 2 * np.pi]]), np.concatenate([rr, [rr[0]]]), period=2 * np.pi)
        n = noise(cell, cell, 6, 6, 100 + f, 3)
        edge = prof * (.85 + .3 * n) - rad
        cover = st(0, .05, edge)
        holes = st(.62, .70, noise(cell, cell, 5, 5, 200 + f, 3))
        cover *= 1 - holes * .85
        ember = st(.10, .0, edge) * cover + st(.80, .9, n) * cover * .8
        cy, cx = divmod(f, 4)
        sheet[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = np.clip(ember, 0, 1)
        alpha[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = cover
    save(os.path.join(out, 'T_BluePhoenix_Ash.png'), sheet, alpha)
    # shock ring with feather-like teeth
    s = 512
    yy, xx = (np.mgrid[0:s, 0:s] + .5) / s * 2 - 1
    r, ang = np.hypot(xx, yy), np.arctan2(yy, xx)
    u = ((ang + np.pi) / (2 * np.pi) * s).astype(int) % s
    teeth = np.clip((noise(8, s, 1, 36, 9, 3)[1][u] - .30) / .55, 0, 1)
    ringv = st(.16, 0, np.abs(r - .62)) * .22 + st(.030, 0, np.abs(r - .62))
    reach = .62 + .34 * teeth ** 2.0
    flames = st(.0, .10, reach - r) * st(.60, .64, r) * (.25 + .75 * np.clip((reach - r) / .12, 0, 1))
    val = np.clip(ringv + flames * .85, 0, 1)
    save(os.path.join(out, 'T_BluePhoenix_Ring.png'), val, val * st(1.0, .93, r))
    # halo
    s = 128
    yy, xx = (np.mgrid[0:s, 0:s] + .5) / s * 2 - 1
    halo = np.clip(1 - np.hypot(xx, yy), 0, 1) ** 2.2
    save(os.path.join(out, 'T_BluePhoenix_Glow.png'), np.clip(halo * 1.2, 0, 1), halo)
    print('TEXTURES', sorted(os.listdir(out)))


# =========================================================================================================================
# phoenix mesh (built in spine space with numpy, converted to Blender axes at the end)
# =========================================================================================================================
class Builder:
    def __init__(self):
        self.v, self.f, self.uv, self.attr, self.col = [], [], [], [], []

    def add(self, verts, faces, uv, attr, col):
        base = len(self.v)
        self.v += [tuple(p) for p in verts]
        self.uv += [tuple(p) for p in uv]
        self.attr += [tuple(p) for p in attr]
        self.col += [tuple(p) for p in col]
        self.f += [tuple(base + i for i in face) for face in faces]


def region_uv(region, u, v):
    """atlas regions: 'wing' (0..512, rows 0..256), 'body' (512..1024, rows 0..256), 'tail' (0..1024, rows 256..512)"""
    x0, x1, y0, y1 = {'wing': (0, 512, 0, 256), 'body': (512, 1024, 0, 256), 'tail': (0, 1024, 256, 512)}[region]
    x = x0 + (x1 - x0) * np.clip(u, .004, .996)
    y = y0 + (y1 - y0) * np.clip(v, .01, .99)
    return x / ATLAS_W, 1 - y / ATLAS_H


def blade(B, centre, across, widths, normal, region, flap, tip_scale, colours, glow, crease=.12):
    """a feather card along a centre line: three vertices across (edge, raised shaft, edge)"""
    n = len(centre)
    verts, uv, attr, col = [], [], [], []
    for i in range(n):
        t = i / (n - 1)
        c, a, w = np.asarray(centre[i]), np.asarray(across[i]), widths[i]
        cc = np.interp(t, np.linspace(0, 1, len(colours)), np.arange(len(colours)))
        k = int(min(cc, len(colours) - 2))
        colr = np.asarray(colours[k]) * (1 - (cc - k)) + np.asarray(colours[k + 1]) * (cc - k)
        for j, s in enumerate((-1, 0, 1)):
            p = c + a * s * w + (np.asarray(normal[i]) * w * crease if s == 0 else 0)
            verts.append(p)
            uv.append(region_uv(region, t, .5 + .5 * s))
            fl = flap(p) if callable(flap) else flap
            attr.append((fl, t * tip_scale))
            col.append((*colr, glow))
    faces = [(i * 3 + j, i * 3 + j + 1, (i + 1) * 3 + j + 1, (i + 1) * 3 + j) for i in range(n - 1) for j in range(2)]
    B.add(verts, faces, uv, attr, col)


def curve(points, n):
    """Catmull-Rom through the control points, n samples"""
    pts = np.asarray(points, float)
    p = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = []
    for t in np.linspace(0, len(pts) - 1 - 1e-6, n):
        i, u = int(t), t - int(t)
        a, b, c, d = p[i:i + 4]
        out.append(.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u ** 3))
    return np.array(out)


def frame_across(cen, plane_normal):
    t = np.gradient(cen, axis=0)
    t /= np.linalg.norm(t, axis=1, keepdims=True) + 1e-9
    a = np.cross(np.asarray(plane_normal, float)[None].repeat(len(cen), 0), t)
    a /= np.linalg.norm(a, axis=1, keepdims=True) + 1e-9
    nrm = np.cross(t, a)
    return a, nrm


def ellipsoid(B, c, r, seg, rings, colour, glow, flap=0.0, region='body', bright=None):
    verts, uv, attr, col = [], [], [], []
    for j in range(rings + 1):
        ph = math.pi * j / rings
        for i in range(seg + 1):
            th = 2 * math.pi * i / seg
            d = np.array([math.cos(ph), math.sin(ph) * math.cos(th), math.sin(ph) * math.sin(th)])
            p = np.asarray(c) + d * np.asarray(r)
            verts.append(p)
            uv.append(region_uv(region, j / rings, i / seg))
            attr.append((flap, 0))
            cl = colour if bright is None else bright(p, colour)
            col.append((*cl[:3], cl[3] if len(cl) > 3 else glow))
    faces = [(j * (seg + 1) + i, j * (seg + 1) + i + 1, (j + 1) * (seg + 1) + i + 1, (j + 1) * (seg + 1) + i) for j in range(rings) for i in range(seg)]
    B.add(verts, faces, uv, attr, col)


SHOULDER = .045
SPAN = .78
CYAN, BLUE, DEEP, WHITE = (.14, .52, 1.0), (.04, .28, 1.0), (.02, .09, .80), (.45, .78, 1.0)


def phoenix():
    B = Builder()
    wing_flap = lambda p: float(np.clip((abs(p[1]) - SHOULDER) / (SPAN - SHOULDER), 0, 1)) ** .8 * .999 + .001

    # body, neck, head and beak
    ellipsoid(B, (.205, 0, .004), (.105, .052, .046), 14, 10, (*CYAN, .85), .85)
    for k in range(6):                                        # neck: a short chain of overlapping rings
        s = .155 - k * .02
        ellipsoid(B, (s, 0, .012 + k * .004), (.03, .03 - k * .0015, .029 - k * .0012), 10, 6, (*CYAN, .9), .9)
    eye = lambda p, c: (1, 1, 1, 1.0) if abs(abs(p[1]) - .024) < .012 and p[2] > .010 and abs(p[0] - .040) < .014 else c
    ellipsoid(B, (.045, 0, .030), (.038, .031, .030), 14, 10, (*WHITE, .95), .95, bright=eye)
    beak = []
    for i in range(7):                                        # hooked beak, pointing at the ball
        t = i / 6
        beak.append((.018 - .058 * t, 0, .022 - .014 * t ** 2))
    cen = np.array(beak)
    blade(B, cen, [(0, 1, 0)] * 7, [.013 * (1 - t) + .002 for t in np.linspace(0, 1, 7)], [(0, 0, 1)] * 7, 'body', 0.0, 0.0,
          [WHITE, CYAN], 1.0, crease=.6)
    blade(B, cen, [(0, 0, 1)] * 7, [.010 * (1 - t) + .002 for t in np.linspace(0, 1, 7)], [(0, 1, 0)] * 7, 'body', 0.0, 0.0,
          [WHITE, CYAN], 1.0, crease=.6)
    # crest: flame plumes sweeping back from the head
    for k, (dy, rise, length) in enumerate(((0, .14, .26), (.03, .11, .22), (-.03, .11, .22), (.055, .08, .18), (-.055, .08, .18))):
        cen = curve([(.050, dy * .2, .05), (.10, dy * .6, .05 + rise * .6), (.17, dy, .05 + rise), (.05 + length, dy * 1.3, .04 + rise * 1.05)], 10)
        a, nrm = frame_across(cen, (0, 1, 0))
        blade(B, cen, a, [.018 * math.sin(math.pi * min(t * 1.2, 1)) ** .6 + .004 for t in np.linspace(0, 1, 10)], nrm, 'wing', 0.0, 1.0,
              [WHITE, CYAN, BLUE], .95)

    # wings (right wing built, then mirrored): arm from the shoulder to the wrist, hand to the tip
    for side in (1, -1):
        def M(p):
            return np.array([p[0], p[1] * side, p[2]])
        # leading edge: shoulder -> wrist (a little forward) -> hand; feathers grow from it and fan backwards and out
        wrist, hand = np.array([.135, .27, .05]), np.array([.19, .40, .07])

        def feather(root, ang_back, L, wid, segs, tip_scale, colours, glow, lift):
            """ang_back: 0 = straight out along the span, 90 = straight back along the body"""
            a_ = math.radians(ang_back)
            d = np.array([math.sin(a_), math.cos(a_), .05])
            d /= np.linalg.norm(d)
            cen = curve([root, root + d * L * .5 + [0, 0, lift], root + d * L + [0, 0, lift * .4]], segs)
            a, nrm = frame_across(cen, (0, 0, 1))
            prof = [wid * (.75 + .25 * math.sin(math.pi * min(u * 1.6, 1))) * (1 - .35 * u) for u in np.linspace(0, 1, segs)]
            blade(B, [M(p) for p in cen], [M(p) for p in a], prof, [M(p) for p in nrm], 'wing', wing_flap, tip_scale, colours, glow)

        # secondaries along the arm: broad, swept back
        for k in range(10):
            t = k / 9
            root = np.array([.17 - .03 * t, SHOULDER + .015 + (wrist[1] - SHOULDER - .015) * t, .022 + .025 * t])
            feather(root, 84 - 22 * t, .24 + .08 * t, .046, 9, .85, [CYAN, BLUE, DEEP], .85, .01)
        # primaries fanning from the hand: the longest at the tip of the wing
        for k in range(9):
            t = k / 8
            root = wrist + (hand - wrist) * t
            feather(root, 62 - 40 * t, .26 + .12 * t, .044 - .006 * t, 11, 1.0, [CYAN, BLUE, DEEP], .9, .016)
        # coverts: two rows of shorter, brighter feathers over the leading edge
        for row, (z0, L0, n, ang0) in enumerate(((.034, .14, 11, 70), (.046, .095, 13, 62))):
            for k in range(n):
                t = k / (n - 1)
                root = np.array([.16 - .02 * t + .02 * row, SHOULDER + .01 + (hand[1] - .02 - SHOULDER) * t, z0 + .03 * t])
                feather(root, ang0 - 28 * t, L0 * (1 + .5 * t), .036 - .006 * row, 6, .5, [WHITE, CYAN, BLUE], .95, .006)

    # tail: long plumes with flame eyes, and slender streamers between them; they follow the ball's path in the game
    for k, (dy, length, wid) in enumerate(((0, 1.62, .072), (.16, 1.45, .066), (-.16, 1.45, .066), (.32, 1.22, .06), (-.32, 1.22, .06))):
        cen = curve([(.28, dy * .05, .0), (.55, dy * .35, .012), (.95, dy * .7, -.01), (.28 + length, dy, .015)], 26)
        a, nrm = frame_across(cen, (0, 0, 1))
        blade(B, cen, a, [wid * (.45 + .55 * t) for t in np.linspace(0, 1, 26)], nrm, 'tail', 0.0, 1.0, [CYAN, BLUE, BLUE, DEEP], .9)
    for k, (dy, length) in enumerate(((.08, 1.05), (-.08, 1.05), (.24, .92), (-.24, .92))):
        cen = curve([(.28, dy * .1, .006), (.6, dy * .5, .02), (.28 + length, dy, .0)], 18)
        a, nrm = frame_across(cen, (0, 0, 1))
        blade(B, cen, a, [.026 * (1 - .4 * t) for t in np.linspace(0, 1, 18)], nrm, 'wing', 0.0, 1.0, [WHITE, CYAN, BLUE], .9)
    return B


def mesh(out):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    B = phoenix()
    v = np.asarray(B.v, np.float32)
    # spine (x, y, z) -> Blender (-x, -z, y); the FBX export turns it back into Unity (x, y, z)
    co = np.stack([-v[:, 0], -v[:, 2], v[:, 1]], 1)
    me = bpy.data.meshes.new('BluePhoenixSpirit')
    me.from_pydata(co.tolist(), [], B.f)
    loops = np.array([i for f in B.f for i in f])
    uv0 = me.uv_layers.new(name='UVMap')
    uv0.data.foreach_set('uv', np.asarray(B.uv, np.float32)[loops].reshape(-1))
    uv1 = me.uv_layers.new(name='Attr')
    uv1.data.foreach_set('uv', np.asarray(B.attr, np.float32)[loops].reshape(-1))
    c = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    c.data.foreach_set('color', np.asarray(B.col, np.float32).reshape(-1))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.validate()
    me.update()
    o = bpy.data.objects.new('BluePhoenixSpirit', me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    print('PHOENIX tris', sum(len(p.vertices) - 2 for p in me.polygons), 'verts', len(me.vertices),
          'x', v[:, 0].min().round(3), v[:, 0].max().round(3), 'y', v[:, 1].min().round(3), v[:, 1].max().round(3), 'z', v[:, 2].min().round(3), v[:, 2].max().round(3))
    os.makedirs(out, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'BluePhoenixSpirit.fbx'), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', colors_type='LINEAR', bake_anim=False, path_mode='STRIP')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'BluePhoenixSpirit.blend'), compress=True)
    print('EXPORTED', out)


# =========================================================================================================================
# emblem and previews
# =========================================================================================================================
def spirit_material(atlas, strength=3.0):
    m = bpy.data.materials.new('spirit')
    m.use_nodes = True
    m.surface_render_method = 'DITHERED'
    t = m.node_tree
    t.nodes.clear()
    out = t.nodes.new('ShaderNodeOutputMaterial')
    em = t.nodes.new('ShaderNodeEmission')
    tr = t.nodes.new('ShaderNodeBsdfTransparent')
    mix = t.nodes.new('ShaderNodeMixShader')
    tex = t.nodes.new('ShaderNodeTexImage')
    tex.image = bpy.data.images.load(atlas)
    tex.image.colorspace_settings.name = 'Non-Color'
    tex.interpolation = 'Linear'
    vc = t.nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'Col'
    deep = t.nodes.new('ShaderNodeMix')
    deep.data_type = 'RGBA'
    t.links.new(tex.outputs['Color'], deep.inputs[0])
    deep.inputs[6].default_value = (.01, .04, .30, 1)
    t.links.new(vc.outputs['Color'], deep.inputs[7])
    t.links.new(deep.outputs[2], em.inputs['Color'])
    em.inputs['Strength'].default_value = strength
    gate = t.nodes.new('ShaderNodeMath')
    gate.operation = 'GREATER_THAN'
    gate.inputs[1].default_value = .5
    t.links.new(tex.outputs['Alpha'], gate.inputs[0])
    t.links.new(gate.outputs[0], mix.inputs[0])
    t.links.new(tr.outputs[0], mix.inputs[1])
    t.links.new(em.outputs[0], mix.inputs[2])
    t.links.new(mix.outputs[0], out.inputs[0])
    return m


def load_phoenix(atlas, strength=3.0):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    B = phoenix()
    v = np.asarray(B.v, np.float32)
    me = bpy.data.meshes.new('p')
    # preview axes: x = -s (beak towards +x... flying left to right), y = span, z = back
    me.from_pydata(np.stack([-v[:, 0], v[:, 1], v[:, 2]], 1).tolist(), [], B.f)
    loops = np.array([i for f in B.f for i in f])
    uv0 = me.uv_layers.new(name='UVMap')
    uv0.data.foreach_set('uv', np.asarray(B.uv, np.float32)[loops].reshape(-1))
    c = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    c.data.foreach_set('color', np.asarray(B.col, np.float32).reshape(-1))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    me.materials.append(spirit_material(atlas, strength))
    o = bpy.data.objects.new('p', me)
    bpy.context.collection.objects.link(o)
    return o


def stage(cam_loc, target, res, lens=50, ortho=None):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (.015, .02, .035, 1)
    cam = bpy.data.cameras.new('c')
    cam.lens = lens
    if ortho:
        cam.type = 'ORTHO'
        cam.ortho_scale = ortho
    co = bpy.data.objects.new('c', cam)
    co.location = cam_loc
    co.rotation_euler = (Vector(target) - Vector(cam_loc)).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(co)
    sc.camera = co
    return sc


def emblem(out):
    """the phoenix seen from above, wings spread, for the contact flash: white-hot body, blue feathers, alpha from the shape"""
    atlas = os.path.join(out, 'T_BluePhoenix_Feathers.png')
    o = load_phoenix(atlas, 1.15)
    # tuck the long tail in so the emblem stays compact
    me = o.data
    for vtx in me.vertices:
        s = -vtx.co.x
        if s > .45:
            k = .45 + (s - .45) * .30
            vtx.co.x = -k
            vtx.co.y *= .8
    sc = stage((-.40, 0, 3), (-.40, 0, 0), (512, 512), ortho=1.62)
    co = sc.camera
    co.rotation_euler = (0, 0, math.radians(-90))              # beak up
    sc.render.film_transparent = True
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.filepath = os.path.join(out, 'T_BluePhoenix_Emblem.png')
    bpy.ops.render.render(write_still=True)
    print('EMBLEM', sc.render.filepath)


def preview():
    atlas = os.path.join(WORK, 'T_BluePhoenix_Feathers.png')
    o = load_phoenix(atlas)
    for name, loc, target, res in (('top', (-.6, 0, 3.2), (-.6, 0, 0), (1400, 900)), ('three', (.9, -1.6, 1.3), (-.55, 0, 0), (1400, 900)),
                                   ('behind', (1.9, .0, .7), (-.2, 0, 0), (1200, 800))):
        sc = stage(loc, target, res, 40)
        sc.render.filepath = os.path.join(WORK, 'pv_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        for x in [x for x in bpy.data.objects if x.type == 'CAMERA']:
            bpy.data.objects.remove(x)
    print('PREVIEW')


{'textures': lambda: textures(ARGS[2]), 'mesh': lambda: mesh(ARGS[2]), 'emblem': lambda: emblem(ARGS[2]), 'preview': preview}[MODE]()
