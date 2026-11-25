"""Dragon Ruby paddle - face maps from the reference painting (system Python: numpy, OpenCV, SciPy).

    python make_face_maps.py <out_dir> [size]

Rectifies the painting (its centre line leans), separates ruby / gold / obsidian, and writes for the head of the paddle:
    face_base.png   painted colour with the painted lighting reduced, clean ruby rim, gold filigree and gold neck rails
    face_height.png 16-bit relief (dragon bodies inflated, scales from the paint), used for displacement and normals
    face_normal.png tangent normal (OpenGL, +Y up) of the relief
    face_mask.png   R metallic, G ambient occlusion, B emission, A smoothness
    outline.json    smoothed half-width per height of the paddle head, in millimetres
Layout: the head fills the left part of a square sheet (planar front projection); the right column holds flat swatches.
"""
import json, os, sys
import cv2
import numpy as np
from scipy import ndimage as ndi

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1]
SIZE = int(sys.argv[2]) if len(sys.argv) > 2 else 2048
os.makedirs(OUT, exist_ok=True)

TOP, BOTTOM, HALF = 2, 792, 300            # reference pixels: rows of the head, half canvas width
MM = 0.3364                                # millimetres per reference pixel
RIM = 22.5                                 # rim width in reference pixels
CANVAS_H = BOTTOM - TOP
SCALE = SIZE / CANVAS_H


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


src = cv2.imread(os.path.join(HERE, 'reference.png'), cv2.IMREAD_UNCHANGED)
alpha = (src[..., 3] > 128).astype(np.uint8)
alpha = cv2.morphologyEx(alpha, cv2.MORPH_OPEN, np.ones((15, 15), np.uint8))
_, lab, stats, _ = cv2.connectedComponentsWithStats(alpha)
alpha = (lab == 1 + np.argmax(stats[1:, 4])).astype(np.uint8)

# ---- centre line and half width ------------------------------------------------------------------------------------
rows = np.arange(16, 740)
left = np.array([np.where(alpha[y])[0].min() for y in rows], float)
right = np.array([np.where(alpha[y])[0].max() for y in rows], float)
centre = np.polyfit(rows, (left + right) / 2, 2)
half = ndi.gaussian_filter1d((right - left) / 2, 6, mode='nearest')


def half_width(y):
    """Half width of the head (reference px) at row y; the top arc closes as a super-ellipse, the neck narrows to the collar."""
    y = np.asarray(y, float)
    w = np.interp(y, rows, half)
    t = np.clip((y - 4) / (40 - 4), 0, 1)
    top = np.interp(40, rows, half) * np.power(np.clip(1 - (1 - t) ** 2.6, 0, 1), 1 / 2.6) * 0.45 + w * 0.55 * np.sqrt(t)
    w = np.where(y < 40, np.minimum(np.maximum(top, 0), np.interp(40, rows, half)), w)
    neck = np.interp(728, rows, half)
    n = np.clip((y - 700) / 60, 0, 1)
    return np.where(y > 700, np.interp(700, rows, half) * (1 - smooth(0, 1, n)) + 66 * smooth(0, 1, n), w)


ys = np.arange(4, 792, 2)
json.dump({'y_px': ys.tolist(), 'half_px': [float(v) for v in half_width(ys)], 'mm_per_px': MM, 'top_px': TOP,
           'bottom_px': BOTTOM, 'half_canvas_px': HALF, 'rim_px': RIM, 'tex_scale': SCALE / SIZE},
          open(os.path.join(OUT, 'outline.json'), 'w'))

# ---- rectified, upscaled painting ------------------------------------------------------------------------------------
W = int(round(2 * HALF * SCALE))
gx, gy = np.meshgrid((np.arange(W) + .5) / SCALE - HALF, (np.arange(SIZE) + .5) / SCALE + TOP)
cx = np.polyval(centre, np.clip(gy, 16, 740))
rgb = cv2.remap(src[..., :3], (gx + cx).astype(np.float32), gy.astype(np.float32), cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_REPLICATE)
rgb = cv2.bilateralFilter(rgb, 7, 22, 4)
rgb = np.clip(rgb.astype(np.float32) * 1.55 - cv2.GaussianBlur(rgb.astype(np.float32), (0, 0), 2.4) * .55, 0, 255)[..., ::-1] / 255
hw = half_width(gy)
# distance to the outline in reference pixels (signed, positive inside)
inside = (np.abs(gx) < hw) & (gy > 4)
dist = ndi.distance_transform_edt(inside) / SCALE
face = smooth(RIM - 1.2, RIM + 1.2, dist)          # 1 on the painted face, 0 on the rim
px = SCALE                                          # texture px per reference px

hsv = cv2.cvtColor(rgb.astype(np.float32), cv2.COLOR_RGB2HSV)
H, S, V = hsv[..., 0], hsv[..., 1], hsv[..., 2]
L = rgb @ np.array([.30, .59, .11], np.float32)
gold = smooth(17, 25, H) * (1 - smooth(52, 64, H)) * smooth(.22, .38, S) * smooth(.28, .45, V)
gold = np.maximum(gold, smooth(.62, .8, L) * smooth(12, 20, H) * (1 - smooth(52, 64, H)))
red = (1 - smooth(15, 23, H) * (1 - smooth(330, 345, H))) * smooth(.45, .62, S) * smooth(.12, .26, V)
red = np.clip(red - gold, 0, 1)
moon = 1 - smooth(31, 34, np.hypot(gx - (822 - 603.5), gy - 408))
gold = np.maximum(gold, moon * smooth(.30, .5, L))
gold, red = cv2.GaussianBlur(gold.astype(np.float32), (0, 0), .8 * px), cv2.GaussianBlur(np.clip(red - moon, 0, 1).astype(np.float32), (0, 0), .8 * px)

# ---- relief -----------------------------------------------------------------------------------------------------------
fg = np.clip(red * 1.1 + gold, 0, 1) * face
body = cv2.morphologyEx((fg > .42).astype(np.uint8), cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(5 * px) | 1,) * 2))
body = cv2.morphologyEx(body, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(3 * px) | 1,) * 2))
d = ndi.distance_transform_edt(body) / px
inflate = np.sqrt(np.clip(d / 26, 0, 1))
inflate = cv2.GaussianBlur(inflate.astype(np.float32), (0, 0), 1.6 * px)
low = cv2.GaussianBlur(L, (0, 0), 5 * px)
detail = L - cv2.GaussianBlur(L, (0, 0), 4.5 * px)
fine = L - cv2.GaussianBlur(L, (0, 0), 1.5 * px)
height = (.58 * inflate + .30 * low + .55 * detail + .25 * fine) * face
height += (1 - face) * 0
height = cv2.GaussianBlur(height.astype(np.float32), (0, 0), .55 * px)
lo, hi = np.percentile(height[face > .9], [.5, 99.5])
height = np.clip((height - lo) / (hi - lo), 0, 1) * face + (1 - face) * .55

# ---- colour -----------------------------------------------------------------------------------------------------------
flat = rgb * (0.62 / (0.34 + cv2.GaussianBlur(L, (0, 0), 14 * px)))[..., None] ** .42
flat = np.clip(flat, 0, 1)
spec = smooth(.62, .95, flat.min(-1))                         # painted highlights: the shader supplies them now
ruby = np.array([.80, .035, .035], np.float32)
golden = np.array([1.0, .70, .26], np.float32)
lum = flat @ np.array([.30, .59, .11], np.float32)
red_col = np.clip(ruby * (0.30 + 2.1 * lum)[..., None], 0, 1)
gold_col = np.clip(golden * (0.22 + 1.02 * lum)[..., None], 0, 1)
col = flat * (1 - spec[..., None] * .35)
col = col * (1 - red[..., None] * .55) + red_col * red[..., None] * .55
col = col * (1 - gold[..., None] * .6) + gold_col * gold[..., None] * .6
black = np.clip(1 - red - gold, 0, 1) * face
col = col * (1 - black[..., None] * .62) + np.array([.012, .010, .013]) * black[..., None] * .62

# deepen: ruby and lacquer lose their haze, gold keeps its light
col = np.clip(col, 0, 1) ** (1 + (.42 * red + .75 * black + .10 * gold) * face)[..., None]

# rim: clean ruby enamel, the painting's gold filigree where it has any, gold rails at the neck
rim = (1 - face) * inside
zones = np.maximum.reduce([smooth(96, 70, np.abs(gx)) * (gy < 40), smooth(180, 200, gy) * (1 - smooth(470, 490, gy)),
                           smooth(600, 625, gy) * (1 - smooth(690, 700, gy))]).astype(np.float32)
# scroll vine drawn in (arc length, inset) coordinates of the outline
edge_px = np.stack([half_width(np.arange(4, 762, .5)), np.arange(4, 762, .5)], 1)
arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(edge_px, axis=0), axis=1))])
from scipy.spatial import cKDTree
rr, cc = np.nonzero(rim > .02)
_, near = cKDTree(edge_px).query(np.stack([np.abs(gx[rr, cc]), gy[rr, cc]], 1))
s_arc, d_in = arc[near], dist[rr, cc]
P = 30.0                                                   # pattern period in reference px
ph = (s_arc / P) % 1
mid, amp = RIM * .46, 4.0
vine = np.abs(d_in - (mid + amp * np.sin(2 * np.pi * ph))) / np.sqrt(1 + (amp * 2 * np.pi / P * np.cos(2 * np.pi * ph)) ** 2)
curls = np.full(len(s_arc), 99.0)
for c0, side in ((.25, -1), (.75, 1)):
    dx, dy = ((ph - c0 + .5) % 1 - .5) * P, d_in - (mid + side * 2.3)
    rad, ang = np.hypot(dx, dy), np.arctan2(dy, dx * side)
    curls = np.minimum(curls, np.abs(rad - (4.3 - 1.9 * ((ang + np.pi) / (2 * np.pi)))))
    curls = np.minimum(curls, np.abs(rad) - .6)
line = np.minimum(vine - .95, curls - .80)
draw = np.zeros_like(dist, np.float32)
draw[rr, cc] = np.clip(.5 - line / .9, 0, 1)
filigree = draw * zones * smooth(2.2, 3.6, dist) * (1 - smooth(RIM - 6.5, RIM - 5.0, dist))
rails = smooth(668, 706, gy) * rim
gold_rim = np.maximum(filigree, rails)
ruby_rim = np.array([.62, .016, .022], np.float32)
col = col * (1 - rim[..., None]) + (ruby_rim * (1 - gold_rim[..., None]) + golden * .92 * gold_rim[..., None]) * rim[..., None]
height = np.where(rim > 0, .55 + .35 * filigree, height)
gold_all = np.clip(gold * face + gold_rim * rim, 0, 1)
red_all = np.clip(red * face + (1 - gold_rim) * rim, 0, 1)

# ---- masks ------------------------------------------------------------------------------------------------------------
ao = np.ones_like(height)
for r, k in ((2.2, 1.5), (6, 1.1), (16, .8)):
    ao *= np.clip(1 - k * np.clip(cv2.GaussianBlur(height, (0, 0), r * px) - height, 0, 1), 0, 1)
ao = np.clip(ao, .12, 1) * face + (1 - face)
metal = np.clip(gold_all * 1.0 + red_all * .12, 0, 1)
smoothness = .30 + .46 * red_all + .44 * gold_all
smoothness -= .18 * np.clip(1 - ao, 0, 1) + .05 * ndi.gaussian_filter(np.random.default_rng(7).random(height.shape), 2 * px)
smoothness = np.clip(smoothness, .12, .9)
pearl = np.exp(-(((gx - (822 - 604)) ** 2 + (gy - 408) ** 2) / (2 * 30 ** 2))) * smooth(.4, .7, L)
eye = np.exp(-(((gx - (762 - 602)) ** 2 + (gy - 229) ** 2) / (2 * 9 ** 2))) * smooth(.35, .6, L)
ember = smooth(.05, .25, H / 360 * 1.0 * 0 + (rgb[..., 0] - rgb[..., 2])) * smooth(14, 20, H) * (1 - smooth(34, 42, H)) * smooth(.55, .8, V) * smooth(.65, .85, S) * face
emission = np.clip(pearl * .9 + eye * 1.0 + ember * .32, 0, 1)

# ---- normal -----------------------------------------------------------------------------------------------------------
strength = 7.5 * (SIZE / 2048)
g = cv2.GaussianBlur(height.astype(np.float32), (0, 0), .35 * px)
nx = -cv2.Sobel(g, cv2.CV_32F, 1, 0, ksize=3) / 8 * strength * px * 1.6
ny = cv2.Sobel(g, cv2.CV_32F, 0, 1, ksize=3) / 8 * strength * px * 1.6
nz = np.ones_like(nx)
n = np.stack([nx, ny, nz], -1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)


# ---- sheet: the head on the left, flat swatches on the right ----------------------------------------------------------
def sheet(img, swatches, fill):
    img = img.astype(np.float32)
    shape = (SIZE, SIZE) + img.shape[2:]
    out = np.empty(shape, np.float32)
    out[...] = fill
    out[:, :W] = img
    for i, s in enumerate(swatches):
        out[int(SIZE * i / len(swatches)):int(SIZE * (i + 1) / len(swatches)), W + 8:] = s
    return out


grow = ndi.distance_transform_edt(~inside, return_distances=False, return_indices=True)
col = np.where(inside[..., None], col, col[tuple(grow)])
base = sheet(col, [ruby_rim, golden * .92, (.02, .018, .022)], ruby_rim)
hgt = sheet(height, [.55] * 3, .55)
nrm = sheet(n, [(0, 0, 1)] * 3, (0, 0, 1))
msk = sheet(np.stack([metal, ao, emission, smoothness], -1), [(.12, 1, 0, .80), (1, 1, 0, .76), (0, 1, 0, .4)], (.12, 1, 0, .80))


def save(name, a, bits=8):
    a = np.clip(a, 0, 1)
    arr = (a * (65535 if bits == 16 else 255) + .5).astype(np.uint16 if bits == 16 else np.uint8)
    if arr.ndim == 3:
        arr = arr[..., [2, 1, 0] + ([3] if arr.shape[2] == 4 else [])]
    cv2.imwrite(os.path.join(OUT, name), arr)


save('face_base.png', base)
save('face_height.png', hgt, 16)
save('face_normal.png', nrm * .5 + .5)
save('face_mask.png', msk)
print('face maps', SIZE, 'head width', W, 'u range', W / SIZE)
