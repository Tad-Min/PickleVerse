"""Blue Phoenix paddle - texture maps from the reference painting (inside Blender: numpy only, no extra packages).

    blender --background --factory-startup --python make_phoenix_maps.py -- <work_dir> [size] [debug]

Head (planar front projection of the painting; the back of the paddle shows the same sheet turned around):
    face_base.png    painted colour: black lacquer kept deep, electric blue kept, the painted glare on silver eased
    face_height.npy  relief 0..1 - phoenix and blue fire raised, carved silver clouds raised, stone ground and moon sunk
    face_normal.png  tangent normal (OpenGL, +Y up) of the relief
    face_mask.png    R metallic, G ambient occlusion, B glow (blue fire), A smoothness
Grip set (one sheet): A the unrolled leather wrap with its spiral blue-fire band and silver cords (procedural, so it wraps
all the way round), B the painted shield, rings and pommel cap (front projection), C flat swatches.
    grip_base.png, grip_height.npy, grip_normal.png, grip_mask.png
    outline.json     outline per reference row, mapping constants and grip layout read by build_blue_phoenix.py
Paddle space (metres): x right, z up, painted front faces -y, pommel bottom at z = 0, height 0.4118.
"""
import json, math, os, sys
import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:]
OUT = ARGS[0]
SIZE = int(ARGS[1]) if len(ARGS) > 1 else 2048
DEBUG = len(ARGS) > 2 and ARGS[2] == 'debug'
os.makedirs(OUT, exist_ok=True)

# ---- reference layout (pixels of reference.png) ----------------------------------------------------------------------
TOP, BOT = 6, 1494                        # crown of the head, bottom of the pommel cap
HEIGHT_M = .4118                          # same height as the other paddles
MMP = HEIGHT_M / (BOT - TOP)              # metres per reference pixel
HEAD_TOP, HEAD_BOT, HALF = 2, 982, 346    # rows and half width of the head canvas
SHIELD = dict(top=898, bottom=977, top_hw=68, bottom_hw=47, crown=862, crown_hw=46)
RING1, WRAP, RING2, CAP = (977, 999), (999, 1352), (1352, 1384), (1384, BOT)
RIM_PX = 21.0                             # silver frame width
COL_TOP, COL_HALF, COL_K = 830, 115, 3    # painted column of the grip set: rows, half width, upscale
PITCH_MM, BAND = 64.0, .40                # spiral of the wrap: one turn per 64 mm, blue band = 40 % of a turn


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ---- image io --------------------------------------------------------------------------------------------------------
def load(path):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size
    px = np.empty(w * h * img.channels, np.float32)
    img.pixels.foreach_get(px)
    a = px.reshape(h, w, img.channels)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def save(name, a):
    a = np.clip(np.asarray(a, np.float32), 0, 1)
    if a.ndim == 2:
        a = np.dstack([a, a, a])
    h, w = a.shape[:2]
    if a.shape[2] == 3:
        a = np.dstack([a, np.ones((h, w), np.float32)])
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(a[::-1].reshape(-1))
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


# ---- filters ----------------------------------------------------------------------------------------------------------
def gblur(a, s, wrap_x=False):
    """Gaussian blur (sigma in pixels) through the FFT; reflected borders, or periodic across x"""
    if s <= 0:
        return a.astype(np.float32)
    a = a.astype(np.float32)
    p = int(3 * s) + 2
    px_ = 0 if wrap_x else p
    ap = np.pad(a, ((p, p), (px_, px_)) + ((0, 0),) * (a.ndim - 2), mode='reflect')
    H, W = ap.shape[:2]
    g = np.exp(-2 * (np.pi * s) ** 2 * (np.fft.fftfreq(H)[:, None] ** 2 + np.fft.rfftfreq(W)[None, :] ** 2)).astype(np.float32)
    if a.ndim == 3:
        g = g[..., None]
    out = np.fft.irfft2(np.fft.rfft2(ap, axes=(0, 1)) * g, s=(H, W), axes=(0, 1))
    return out[p:p + a.shape[0], px_:px_ + a.shape[1]].astype(np.float32)


def keys(t):
    t = np.abs(t)
    return np.where(t < 1, 1.5 * t ** 3 - 2.5 * t ** 2 + 1, np.where(t < 2, -.5 * t ** 3 + 2.5 * t ** 2 - 4 * t + 2, 0)).astype(np.float32)


def sample(img, xs, ys):
    """bicubic (Keys) sample of img (h, w, c) at pixel-centre coordinates xs, ys (any shape), clamped borders"""
    h, w = img.shape[:2]
    out = np.zeros(xs.shape + img.shape[2:], np.float32)
    rows = xs.shape[0]
    for r0 in range(0, rows, 128):
        x = xs[r0:r0 + 128] - .5
        y = ys[r0:r0 + 128] - .5
        ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
        acc = 0
        for j in range(-1, 3):
            wy = keys(y - (iy + j))
            yy = np.clip(iy + j, 0, h - 1)
            for i in range(-1, 3):
                wx = keys(x - (ix + i))
                acc = acc + img[yy, np.clip(ix + i, 0, w - 1)] * (wx * wy)[..., None]
        out[r0:r0 + 128] = acc
    return out


def sobel_normal(height, strength, wrap_x=False):
    """tangent normal (OpenGL +Y up) of a height map; image rows run downwards so +v is -row"""
    hp = np.pad(height, ((1, 1), (0, 0) if wrap_x else (1, 1)), mode='edge')
    if wrap_x:
        hp = np.concatenate([hp[:, -1:], hp, hp[:, :1]], 1)
    gx = (hp[:-2, 2:] + 2 * hp[1:-1, 2:] + hp[2:, 2:] - hp[:-2, :-2] - 2 * hp[1:-1, :-2] - hp[2:, :-2]) / 8
    gy = (hp[2:, :-2] + 2 * hp[2:, 1:-1] + hp[2:, 2:] - hp[:-2, :-2] - 2 * hp[:-2, 1:-1] - hp[:-2, 2:]) / 8
    n = np.stack([-gx * strength, gy * strength, np.ones_like(gx)], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def hsv(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = mx - mn + 1e-6
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r, (g - b) / d % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60
    return hue.astype(np.float32), (d / (mx + 1e-6)).astype(np.float32), mx.astype(np.float32)


def ao_of(height, scale):
    ao = np.ones_like(height)
    for r, k in ((2.0, 1.6), (6, 1.1), (16, .7)):
        ao *= np.clip(1 - k * np.clip(gblur(height, r * scale) - height, 0, 1), 0, 1)
    return np.clip(ao, .1, 1)


# periodic value noise -------------------------------------------------------------------------------------------------
def vnoise(x, y, cx, cy, seed):
    """value noise with period cx along x and cy along y (x, y in cell units), smooth cubic interpolation"""
    g = np.random.default_rng(seed).random((cy, cx)).astype(np.float32)
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = g[iy % cy, ix % cx]
    b = g[iy % cy, (ix + 1) % cx]
    c = g[(iy + 1) % cy, ix % cx]
    d = g[(iy + 1) % cy, (ix + 1) % cx]
    return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy


def fbm(u, v, cx, cy, seed, octaves=5, gain=.5):
    """u, v in 0..1 periods; cx, cy cells of the first octave"""
    out, amp, tot = 0, 1.0, 0
    for o in range(octaves):
        out = out + amp * vnoise(u * cx, v * cy, cx, cy, seed + 17 * o)
        tot += amp
        amp *= gain
        cx, cy = cx * 2, cy * 2
    return out / tot


# ---- the painting --------------------------------------------------------------------------------------------------------
src = load(os.path.join(HERE, 'reference.png'))[..., :3]
H_REF, W_REF = src.shape[:2]
L_ref = src @ np.array([.30, .59, .11], np.float32)

# outline: first and last strong horizontal edge of every row
Ls = gblur(L_ref, .8)
gx = np.abs(Ls[:, 2:] - Ls[:, :-2])
rows = np.arange(TOP - 2, BOT + 1)
left, right = np.zeros(len(rows)), np.zeros(len(rows))
for k, y in enumerate(rows):
    idx = np.where(gx[y] > .055)[0]
    left[k], right[k] = (idx.min() + 1, idx.max() + 1) if len(idx) else (np.nan, np.nan)
ok = ~np.isnan(left)
left, right = np.interp(rows, rows[ok], left[ok]), np.interp(rows, rows[ok], right[ok])


def med(a, k):
    p = np.pad(a, k, mode='edge')
    return np.array([np.median(p[i:i + 2 * k + 1]) for i in range(len(a))])


def gauss1(a, s):
    k = np.arange(-int(3 * s) - 1, int(3 * s) + 2)
    w = np.exp(-.5 * (k / s) ** 2)
    w /= w.sum()
    return np.convolve(np.pad(a, len(k) // 2, mode='edge'), w, mode='valid')


half = gauss1(med((right - left) / 2, 4), 2.0)
centre = gauss1(med((right + left) / 2, 12), 10.0)
head_rows = rows <= HEAD_BOT
centre[head_rows] = np.clip(centre[head_rows], 511.5, 514.5)


def hw(y):
    return np.interp(y, rows, half)


def cx(y):
    return np.interp(y, rows, centre)


print('half width: top', hw(TOP + 2), 'mid head', hw(400), 'neck', hw(960), 'grip', hw(1150), 'cap', hw(1430))

# ---- head canvas: rectified, upscaled painting --------------------------------------------------------------------------
SCALE = SIZE / (HEAD_BOT - HEAD_TOP)
W = int(round(2 * HALF * SCALE))
gy = (np.arange(SIZE, dtype=np.float32) + .5) / SCALE + HEAD_TOP
gxr = (np.arange(W, dtype=np.float32) + .5) / SCALE - HALF
GX, GY = np.meshgrid(gxr, gy)
rgb = np.clip(sample(src, GX + cx(GY), GY), 0, 1)
rgb = np.clip(rgb * 1.45 - gblur(rgb, 2.2) * .45, 0, 1)          # restore the detail lost by the upscale
px = SCALE
HW = hw(GY)
inside = (np.abs(GX) < HW) & (GY >= TOP - .5)
# distance to the outline (reference px): across the flanks, and to the points of the curved crown near the top
slope = np.gradient(hw(gy), gy)[:, None]
dist = (HW - np.abs(GX)) / np.sqrt(1 + slope ** 2)
nr = int((TOP + 130 - HEAD_TOP) * SCALE)
crown_d = np.where(np.abs(GX[:nr]) < hw(TOP), GY[:nr] - TOP + .5, 1e9)
for yy in np.arange(TOP, TOP + 130, .5):
    crown_d = np.minimum(crown_d, np.hypot(np.abs(GX[:nr]) - hw(yy), GY[:nr] - yy))
dist[:nr] = np.minimum(dist[:nr], crown_d)
dist = np.where(inside, dist, -1)
face = smooth(RIM_PX - 1.2, RIM_PX + 1.2, dist)                   # 1 on the painted face, 0 on the silver frame

H_, S_, V_ = hsv(rgb)
L = rgb @ np.array([.30, .59, .11], np.float32)
blue = smooth(178, 192, H_) * (1 - smooth(258, 272, H_)) * smooth(.30, .55, S_) * smooth(.10, .30, V_)
core = smooth(.72, .95, V_) * (1 - smooth(.15, .55, S_)) * smooth(.05, .3, gblur(blue, 3 * px))      # white-hot fire
silver = (1 - smooth(.16, .34, S_)) * smooth(.16, .42, V_) * (1 - core)
black = 1 - smooth(.05, .16, V_)


def ell(cx0, cy0, rx, ry, ang=0.0):
    c, s = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    dx, dy = GX - (cx0 - 512.5), GY - cy0
    u, v = (dx * c + dy * s) / rx, (-dx * s + dy * c) / ry
    return smooth(1.0, .55, np.sqrt(u * u + v * v))


# the phoenix (reference px): head, neck, body, wings, tail, raised above the fire around it
bird = np.maximum.reduce([ell(578, 252, 52, 46), ell(548, 318, 48, 70, -25), ell(505, 410, 92, 112, -10),
                          ell(360, 330, 175, 92, -28), ell(668, 318, 160, 96, 24), ell(500, 640, 118, 210, 6),
                          ell(560, 480, 46, 40)])
moon = smooth(64, 58, np.hypot(GX - (556 - 512.5), GY - 222)) * (1 - ell(578, 252, 58, 52))
fire = np.clip(blue + core, 0, 1)

# ---- relief ---------------------------------------------------------------------------------------------------------------
fg = np.clip(fire * 1.1 + silver * .9, 0, 1) * face
fg = gblur(fg, .9 * px)
inflate = np.sqrt(np.clip(gblur(fg, 9 * px) * 1.25, 0, 1)) * smooth(.15, .6, fg)
low = gblur(L, 5 * px)
detail = L - gblur(L, 4 * px)
fine = L - gblur(L, 1.3 * px)
height = (.42 * inflate + .22 * low + .55 * detail + .22 * fine + .22 * bird * smooth(.1, .45, fire + silver)
          + .12 * smooth(.15, .5, silver) - .10 * black * (1 - bird)) * face
height -= .16 * moon * face
height += .05 * moon * fine * 4 * face                                        # craters inside the sunk moon
height = gblur(height, .5 * px)
lo, hi = np.percentile(height[face > .9], [.5, 99.6])
height = np.clip((height - lo) / (hi - lo), 0, 1) * face + (1 - face) * .55

# ---- colour ------------------------------------------------------------------------------------------------------------------
flat = rgb * (.55 / (.30 + gblur(L, 14 * px)))[..., None] ** .35
flat = np.clip(flat, 0, 1)
col = rgb * (1 - silver[..., None] * .45) + flat * silver[..., None] * .45     # metal: the shader brings its own glare
glare = smooth(.80, .98, col.min(-1)) * silver
col = col * (1 - glare[..., None] * .30)
col = col * (1 - black[..., None] * .55) + np.array([.008, .010, .016], np.float32) * black[..., None] * .55
electric = np.array([.03, .28, 1.0], np.float32)
col = col * (1 - blue[..., None] * .25) + electric * np.clip(L * 1.6, 0, 1)[..., None] * blue[..., None] * .25

# frame: the painted silver frame stays, cleaned of the background that the upscale bled into it
rim = (1 - face) * inside
height = np.where(rim > 0, .55 + .3 * smooth(.35, .8, L) * rim, height)

# ---- masks -------------------------------------------------------------------------------------------------------------------
ao = ao_of(height, px) * face + (1 - face)
metal = np.clip(silver * .92 + black * .10, 0, 1)
smoothness = .36 + .46 * fire + .38 * silver + .12 * black
smoothness -= .16 * (1 - ao) + .06 * gblur(np.random.default_rng(7).random(height.shape).astype(np.float32), 2 * px)
smoothness = np.clip(smoothness, .12, .92)
eye = np.exp(-(((GX - (585 - 512.5)) ** 2 + (GY - 246) ** 2) / (2 * 6 ** 2))) * smooth(.4, .7, L)
glow = np.clip(blue * smooth(.18, .75, V_) * .85 + core * .95 + eye, 0, 1)
glow = np.where(rim > .5, glow * .8, glow)

# ---- normal -------------------------------------------------------------------------------------------------------------------
n = sobel_normal(gblur(height, .35 * px), 7.0 * (SIZE / 2048) * px * 1.6)


# ---- sheet: the head on the left, swatches on the right ----------------------------------------------------------------------
def sheet(img, fill):
    img = img.astype(np.float32)
    out = np.empty((SIZE, SIZE) + img.shape[2:], np.float32)
    out[...] = fill
    out[:, :W] = img
    return out


# outside the outline: repeat the frame colour so mip levels never pull in the background
edge_col = np.array([.30, .32, .36], np.float32)
col = np.where(inside[..., None], col, edge_col)
base = sheet(col, edge_col)
hgt = sheet(height, .55)
nrm = sheet(n, (0, 0, 1))
msk = sheet(np.stack([metal, ao, glow, smoothness], -1), (.9, 1, 0, .78))

# swatch column: the paddle's side guard (u across the thickness: silver lip, dark band, blue light, dark band, silver lip)
SW0 = W + 8
u = (np.arange(SIZE - SW0) + .5) / (SIZE - SW0)
lip = smooth(.10, .02, np.minimum(u, 1 - u))
line = np.exp(-((u - .5) / .045) ** 2)
guard = np.clip(np.array([.05, .055, .07]) * (1 - lip[:, None]) + np.array([.80, .82, .88]) * lip[:, None] + np.array([.05, .45, 1.0]) * line[:, None], 0, 1)
r0, r1 = 0, SIZE // 4
base[r0:r1, SW0:] = guard[None]
msk[r0:r1, SW0:] = np.stack([.25 + .7 * lip, np.ones_like(u), line * .9, .55 + .3 * lip], -1)[None]
hgt[r0:r1, SW0:] = .55
nrm[r0:r1, SW0:] = (0, 0, 1)
for i, (c, m) in enumerate((((.72, .74, .80), (.95, 1, 0, .82)), ((.03, .035, .05), (.35, 1, 0, .6)), ((.04, .40, 1.0), (0, 1, 1, .85)))):
    a, b = SIZE // 4 * (i + 1), SIZE // 4 * (i + 2)
    base[a:b, SW0:] = c
    msk[a:b, SW0:] = m
SWATCH = {'guard': ((SW0 + 4) / SIZE, (SIZE - 4) / SIZE, 1 - (r1 - 4) / SIZE, 1 - 4 / SIZE),
          'silver': ((SW0 + SIZE) / 2 / SIZE, 1 - SIZE * 1.5 / 4 / SIZE), 'dark': ((SW0 + SIZE) / 2 / SIZE, 1 - SIZE * 2.5 / 4 / SIZE),
          'blue': ((SW0 + SIZE) / 2 / SIZE, 1 - SIZE * 3.5 / 4 / SIZE)}

save('face_base.png', base)
save('face_normal.png', nrm * .5 + .5)
save('face_mask.png', msk)
np.save(os.path.join(OUT, 'face_height.npy'), hgt.astype(np.float16))
if DEBUG:
    save('dbg_face_height.png', hgt)
    save('dbg_face_fire.png', np.dstack([silver, fire, bird * .5 + moon * .5]))
    save('dbg_face_glow.png', msk[..., 2])
print('face maps', SIZE, 'head width', W)

# =========================================================================================================================
# grip sheet
# =========================================================================================================================
G = SIZE
gbase = np.zeros((G, G, 3), np.float32)
ghgt = np.full((G, G), .5, np.float32)
gnrm = np.zeros((G, G, 3), np.float32)
gnrm[..., 2] = 1
gmsk = np.zeros((G, G, 4), np.float32)
gmsk[...] = (.9, 1, 0, .8)
A = G // 2                                                   # region A: the unrolled wrap, A x A pixels

# ---- region A: unrolled leather wrap -------------------------------------------------------------------------------------
wrap_len = (WRAP[1] - WRAP[0]) * MMP * 1e3                   # mm
r_mid = np.mean([hw(y) for y in range(WRAP[0], WRAP[1])]) * MMP * 1e3
circ = 2 * math.pi * r_mid * .92                             # elliptic section, RY = .84 RX
a = (np.arange(A) + .5) / A                                  # around: a = 0.5 at the front
t = 1 - (np.arange(A) + .5) / A                              # along: 1 at the top (row 0)
AA, TT = np.meshgrid(a, t)
Ymm = TT * wrap_len
turns = wrap_len / PITCH_MM
w = AA + Ymm / PITCH_MM                                      # helix coordinate: constant along the band
phi = (w - .5 + .35) % 1.0                                   # band centred on phi = .5
across = (phi - .5) / BAND * 2                               # -1..1 across the blue band
ragged = fbm(AA, w % 1.0, 6, 10, 71, 4) - .5                 # flame tongues lick out of the band edges
band = smooth(1.10, .78, np.abs(across) - .55 * ragged * smooth(.4, 1.0, np.abs(across)))
# fire: ridged noise flowing along the band, periodic around (cells integer in a and in w)
nwarp = fbm(AA, w % 1.0, 4, 6, 11, 4)
f1 = fbm((AA + .25 * nwarp) % 1.0, (w * 1.0) % 1.0, 5, 22, 21, 5)
ridged = (1 - np.abs(2 * f1 - 1)) ** 3.2
f2 = fbm((AA + .4 * nwarp + .13) % 1.0, (w * 1.0) % 1.0, 9, 40, 31, 4)
filaments = (1 - np.abs(2 * f2 - 1)) ** 6
tongues = smooth(.25, .85, fbm(AA, w % 1.0, 7, 14, 41, 4) + .45 * (1 - np.abs(across)))
heat = np.clip(band * (.12 + .70 * ridged * tongues + .45 * filaments * tongues), 0, 1) ** 1.15
ramp_v = np.array([0, .22, .5, .78, 1.0], np.float32)
ramp_c = np.array([[.004, .006, .02], [.0, .06, .42], [.01, .26, 1.0], [.10, .58, 1.0], [.62, .90, 1.0]], np.float32)
fire_col = np.stack([np.interp(heat, ramp_v, ramp_c[:, k]) for k in range(3)], -1)
# leather: black, grain and carved swirl lines
grain = fbm(AA, TT * wrap_len / circ % 1.0, 90, 90, 51, 3)
swirl = fbm(AA, (TT * wrap_len / circ) % 1.0, 9, 9, 61, 4)
lines = np.abs(((swirl * 7.0) % 1.0) - .5)
carve = smooth(.06, .015, lines)
leather = np.array([.016, .016, .019], np.float32) * (.75 + .5 * grain[..., None]) * (1 - .55 * carve[..., None])
# silver cords on both edges of the band (geometry adds the main ones), a thin counter-wound pair crossing the leather
cord_d = np.minimum(np.abs(phi - (.5 - BAND / 2 - .025)), np.abs(phi - (.5 + BAND / 2 + .025)))
cord = np.exp(-(cord_d / .012) ** 2)
counter = (AA - Ymm / (PITCH_MM * 1.35)) % .5
cross = np.exp(-(np.minimum(np.abs(counter - .12), np.abs(counter - .16)) / .0045) ** 2) * smooth(.9, 1.15, np.abs(across))
wire = np.clip(cord + cross * .85, 0, 1)
silver_col = np.array([.62, .64, .70], np.float32)
cA = leather * (1 - band[..., None]) + fire_col * band[..., None]
cA = cA * (1 - wire[..., None]) + silver_col * (.45 + .55 * wire[..., None]) * wire[..., None]
hA = .40 - .10 * carve + .05 * (grain - .5) + band * (.10 + .10 * ridged * tongues) + .45 * cord + .30 * cross * (1 - band)
# the leather strip overlaps itself once per turn: a small step at the lower edge of the band
hA += .06 * smooth(.0, .05, ((phi - .5 - BAND / 2) % 1.0)) * smooth(.45, .3, ((phi - .5 - BAND / 2) % 1.0))
gA = np.clip(band * (.25 + .75 * heat), 0, 1) * (1 - wire)
metalA = np.clip(wire * .95 + (1 - band) * .05, 0, 1)
smoothA = np.clip(.38 + .15 * grain + .40 * band + .40 * wire - .2 * carve, .1, .92)
aoA = np.ones_like(hA)
for r, k in ((2.0, 1.8), (6, 1.2)):
    aoA *= np.clip(1 - k * np.clip(gblur(hA, r, wrap_x=True) - hA, 0, 1), 0, 1)
gbase[:A, :A] = cA
ghgt[:A, :A] = hA
gnrm[:A, :A] = sobel_normal(gblur(hA, .6, wrap_x=True), 14.0, wrap_x=True)
gmsk[:A, :A] = np.stack([metalA, np.clip(aoA, .1, 1), gA, smoothA], -1)

# ---- region B: painted column (shield, rings, pommel cap) --------------------------------------------------------------------
XB0, YB0 = A + 16, 19
rows_b = (np.arange(G - YB0 - 19) + .5) / COL_K + COL_TOP
cols_b = (np.arange(2 * COL_HALF * COL_K) + .5) / COL_K - COL_HALF
BX, BY = np.meshgrid(cols_b, rows_b)
cB = np.clip(sample(src, BX + cx(BY), BY), 0, 1)
cB = np.clip(cB * 1.4 - gblur(cB, 2.0) * .4, 0, 1)
hB_, sB_, vB_ = hsv(cB)
LB = cB @ np.array([.30, .59, .11], np.float32)
blueB = smooth(178, 192, hB_) * (1 - smooth(258, 272, hB_)) * smooth(.30, .55, sB_) * smooth(.10, .30, vB_)
coreB = smooth(.72, .95, vB_) * (1 - smooth(.15, .55, sB_)) * smooth(.05, .3, gblur(blueB, 6))
silverB = (1 - smooth(.16, .34, sB_)) * smooth(.16, .42, vB_) * (1 - coreB)
blackB = 1 - smooth(.05, .16, vB_)
fgB = np.clip(blueB + coreB + silverB * .9, 0, 1)
hB = .30 + .30 * np.sqrt(np.clip(gblur(fgB, 10), 0, 1)) * smooth(.15, .6, fgB) + .45 * (LB - gblur(LB, 9)) + .18 * (LB - gblur(LB, 3))
hB = np.clip(gblur(hB, .8), 0, 1)
cB = cB * (1 - blackB[..., None] * .5) + np.array([.008, .010, .016], np.float32) * blackB[..., None] * .5
hb, wb = cB.shape[:2]
gbase[YB0:YB0 + hb, XB0:XB0 + wb] = cB
ghgt[YB0:YB0 + hb, XB0:XB0 + wb] = hB
gnrm[YB0:YB0 + hb, XB0:XB0 + wb] = sobel_normal(hB, 9.0)
aoB = ao_of(hB, COL_K * .5)
gmsk[YB0:YB0 + hb, XB0:XB0 + wb] = np.stack([np.clip(silverB * .92 + blackB * .1, 0, 1), aoB,
                                              np.clip(blueB * smooth(.18, .75, vB_) * .85 + coreB * .95, 0, 1),
                                              np.clip(.4 + .4 * (blueB + coreB) + .35 * silverB - .15 * (1 - aoB), .12, .92)], -1)
# margins of region B repeat the frame colour (mip safety)
gbase[:, A:XB0] = gbase[:, XB0:XB0 + 1]
gbase[:, XB0 + wb:] = gbase[:, XB0 + wb - 1:XB0 + wb]

# ---- region C: swatches under the wrap (rows A..G, columns 0..A) ---------------------------------------------------------------
# cord strip: u across the wire, bright crown and dark sides; then dark metal, deep blue enamel, black leather
c_rows = (G - A) // 4
u = (np.arange(A) + .5) / A
crown = np.cos((u - .5) * math.pi) ** 2
gbase[A:A + c_rows, :A] = (np.array([.20, .21, .24]) + np.array([.62, .62, .66]) * crown[:, None])[None]
gmsk[A:A + c_rows, :A] = np.stack([np.full_like(u, .95), .55 + .45 * crown, np.zeros_like(u), .72 + .15 * crown], -1)[None]
for i, (c, m) in enumerate((((.05, .055, .07), (.75, 1, 0, .62)), ((.02, .22, .85), (0, 1, .85, .88)), ((.016, .016, .02), (0, 1, 0, .42)))):
    a0 = A + c_rows * (i + 1)
    gbase[a0:a0 + c_rows, :A] = c
    gmsk[a0:a0 + c_rows, :A] = m
GSWATCH = {'cord_u': (2 / G, (A - 2) / G), 'cord_v': 1 - (A + c_rows * .5) / G, 'dark': (.25, 1 - (A + c_rows * 1.5) / G),
           'blue': (.25, 1 - (A + c_rows * 2.5) / G), 'leather': (.25, 1 - (A + c_rows * 3.5) / G)}

save('grip_base.png', gbase)
save('grip_normal.png', gnrm * .5 + .5)
save('grip_mask.png', gmsk)
np.save(os.path.join(OUT, 'grip_height.npy'), ghgt.astype(np.float16))
if DEBUG:
    save('dbg_grip_height.png', ghgt)

json.dump({'mm_per_px': MMP * 1e3, 'top_px': TOP, 'bottom_px': BOT, 'height_m': HEIGHT_M,
           'head': {'top_px': HEAD_TOP, 'bottom_px': HEAD_BOT, 'half_px': HALF, 'scale': SCALE, 'size': SIZE, 'width_px': W, 'rim_px': RIM_PX},
           'rows': rows.tolist(), 'half': [float(v) for v in half], 'centre': [float(v) for v in centre],
           'shield': SHIELD, 'ring1': RING1, 'wrap': WRAP, 'ring2': RING2, 'cap': CAP,
           'column': {'top_px': COL_TOP, 'half_px': COL_HALF, 'k': COL_K, 'x0': XB0, 'y0': YB0, 'size': G},
           'unrolled': {'size': A, 'pitch_mm': PITCH_MM, 'band': BAND, 'phase': .35, 'length_mm': wrap_len},
           'swatch': SWATCH, 'grip_swatch': GSWATCH},
          open(os.path.join(OUT, 'outline.json'), 'w'), indent=1)
print('grip maps', G, 'wrap', round(wrap_len, 1), 'mm,', round(turns, 2), 'turns, circumference', round(circ, 1), 'mm')
