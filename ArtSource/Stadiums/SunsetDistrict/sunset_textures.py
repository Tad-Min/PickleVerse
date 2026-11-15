"""Procedural textures for the golden-hour district (PickleVerse).

Run with the system Python (needs numpy + Pillow):
    python sunset_textures.py

Everything is generated here: no downloaded texture packs. Tiling maps are
periodic by construction (spectral noise / wrapped drawing). Output goes to
My project/Assets/EnvironmentContent/SunsetDistrict/Textures and a small
atlas-layout JSON next to this script for the Blender builders.
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "My project", "Assets", "EnvironmentContent", "SunsetDistrict", "Textures")
os.makedirs(OUT, exist_ok=True)


# ----------------------------------------------------------------------------- helpers
def fbm(h, w=None, beta=2.0, seed=0, lo=None, hi=None, stretch=(1.0, 1.0)):
    """Periodic fractal noise in 0..1. stretch=(sy, sx) > 1 elongates features along that axis."""
    w = w or h
    r = np.random.default_rng(seed)
    f = np.fft.fft2(r.standard_normal((h, w)))
    fy = np.fft.fftfreq(h)[:, None] * h * stretch[0]
    fx = np.fft.fftfreq(w)[None, :] * w * stretch[1]
    k = np.sqrt(fx * fx + fy * fy)
    k[0, 0] = 1
    amp = 1.0 / np.power(k, beta / 2.0)
    if lo is not None:
        amp = amp * (k >= lo)
    if hi is not None:
        amp = amp * (k <= hi)
    amp[0, 0] = 0
    n = np.real(np.fft.ifft2(f * amp))
    n -= n.min()
    n /= max(n.max(), 1e-9)
    return n


def normal_map(height, strength):
    """Tangent-space normal (OpenGL / Unity convention, +Y up in UV space) from a periodic height field."""
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5 * strength
    dy = (np.roll(height, 1, 0) - np.roll(height, -1, 0)) * 0.5 * strength
    n = np.dstack([-dx, -dy, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return np.clip((n * 0.5 + 0.5) * 255 + 0.5, 0, 255).astype(np.uint8)


def to_img(rgb, alpha=None):
    a = np.clip(rgb * 255 + 0.5, 0, 255).astype(np.uint8)
    if alpha is None:
        return Image.fromarray(a, "RGB")
    al = np.clip(alpha * 255 + 0.5, 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([a, al]), "RGBA")


def save(img, name):
    path = os.path.join(OUT, name)
    img.save(path, optimize=True)
    print(f"  {name:28s} {img.size[0]}x{img.size[1]} {img.mode:4s} {os.path.getsize(path) / 1024:7.1f} KB")


def mix(a, b, t):
    t = t[..., None] if t.ndim == 2 else t
    return a * (1 - t) + b * t


def col(r, g, b):
    return np.array([r, g, b], dtype=np.float64)


def solidify(img):
    """Bleed opaque colours into transparent texels so mip-maps and bilinear taps never pick up black fringes."""
    arr = np.asarray(img).astype(np.float64) / 255.0
    rgb, a = arr[..., :3], arr[..., 3]
    out = rgb.copy()
    filled = a > 0.02
    mean = (rgb * a[..., None]).sum((0, 1)) / max(a.sum(), 1e-6)
    for radius in (2, 6, 18, 54):
        pa = Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))
        wa = np.asarray(pa).astype(np.float64) / 255.0
        acc = np.zeros_like(rgb)
        for c in range(3):
            pc = Image.fromarray((rgb[..., c] * a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))
            acc[..., c] = np.asarray(pc).astype(np.float64) / 255.0
        ok = (wa > 0.02) & ~filled
        out[ok] = acc[ok] / wa[ok][:, None]
        filled |= ok
    out[~filled] = mean
    return Image.fromarray(np.dstack([np.clip(out * 255 + 0.5, 0, 255), a * 255]).astype(np.uint8), "RGBA")


# ----------------------------------------------------------------------------- ground surfaces
def asphalt():
    s = 512
    fine = fbm(s, beta=0.6, seed=11, lo=40)
    mid = fbm(s, beta=2.2, seed=12, lo=2, hi=24)
    grit = fbm(s, beta=0.2, seed=13, lo=90)
    base = 0.215 + (mid - 0.5) * 0.07 + (fine - 0.5) * 0.10
    rgb = np.dstack([base * 1.02, base * 1.0, base * 0.98])
    stones = grit > 0.80                      # pale aggregate
    pits = grit < 0.16                        # dark binder pits
    rgb[stones] = rgb[stones] * 0.55 + np.array([0.46, 0.44, 0.41]) * 0.45
    rgb[pits] *= 0.62
    save(to_img(rgb), "SD_Asphalt.png")
    height = fine * 1.4 + grit * 1.1 + mid * 0.6
    save(Image.fromarray(normal_map(height, 2.6), "RGB"), "SD_Asphalt_N.png")


def paving():
    """Large-format warm stone pavers in running bond. One tile = 2.4 m square (8 courses x 4 slabs)."""
    s = 512
    rows, cols = 8, 4
    ph, pw = s // rows, s // cols
    r = np.random.default_rng(21)
    yy, xx = np.mgrid[0:s, 0:s]
    row = yy // ph
    shift = (row % 2) * (pw // 2)
    cx = ((xx + shift) % s) // pw
    tone = r.uniform(-0.055, 0.055, (rows, cols))[row, cx]
    warm = r.uniform(-0.02, 0.02, (rows, cols))[row, cx]
    # distance to the nearest joint, in pixels
    dy = np.minimum(yy % ph, ph - 1 - (yy % ph))
    dx = np.minimum((xx + shift) % pw, pw - 1 - ((xx + shift) % pw))
    d = np.minimum(dx, dy).astype(np.float64)
    joint = np.clip(1.0 - d / 1.6, 0, 1)
    bevel = np.clip(d / 5.0, 0, 1)
    grain = fbm(s, beta=1.2, seed=22, lo=12)
    blot = fbm(s, beta=2.4, seed=23, lo=1, hi=10)
    v = 0.60 + tone + (grain - 0.5) * 0.07 + (blot - 0.5) * 0.06
    rgb = np.dstack([v * (1.03 + warm), v * 1.0, v * (0.93 - warm)])
    rgb = rgb * (0.80 + 0.20 * bevel)[..., None]
    rgb = mix(rgb, np.array([0.21, 0.19, 0.17]), joint)
    save(to_img(rgb), "SD_Paving.png")
    height = bevel * 2.2 + grain * 0.7
    save(Image.fromarray(normal_map(height, 1.9), "RGB"), "SD_Paving_N.png")


def concrete():
    s = 256
    blot = fbm(s, beta=2.3, seed=31, lo=1, hi=14)
    fine = fbm(s, beta=0.9, seed=32, lo=20)
    pin = fbm(s, beta=0.1, seed=33, lo=60) < 0.07
    v = 0.56 + (blot - 0.5) * 0.10 + (fine - 0.5) * 0.07
    rgb = np.dstack([v * 1.01, v, v * 0.97])
    rgb[pin] *= 0.72
    save(to_img(rgb), "SD_Concrete.png")


def acrylic():
    """Cushioned acrylic court coating: near-white so vertex colours tint it; sand-grain normal map."""
    s = 512
    grain = fbm(s, beta=0.15, seed=41, lo=70)
    roller = fbm(s, beta=2.0, seed=42, lo=1, hi=6, stretch=(6.0, 1.0))   # long roller passes
    cloud = fbm(s, beta=2.6, seed=43, lo=1, hi=5)
    v = 0.925 + (grain - 0.5) * 0.085 + (roller - 0.5) * 0.035 + (cloud - 0.5) * 0.03
    save(to_img(np.dstack([v, v, v])), "SD_Acrylic.png")
    save(Image.fromarray(normal_map(grain * 1.6 + roller * 0.35, 2.2), "RGB"), "SD_Acrylic_N.png")


def _wrapped_lines(draw, s, lines, width_fn):
    for (x0, y0, x1, y1, colour, wdt) in lines:
        for ox in (-s, 0, s):
            for oy in (-s, 0, s):
                if -8 <= min(x0, x1) + ox <= s + 8 or -8 <= max(x0, x1) + ox <= s + 8:
                    if -8 <= min(y0, y1) + oy <= s + 8 or -8 <= max(y0, y1) + oy <= s + 8:
                        draw.line((x0 + ox, y0 + oy, x1 + ox, y1 + oy), fill=colour, width=wdt)


def grass():
    """Mown lawn seen from standing height: mottled sward with individual blade strokes. 512 px = 3 m."""
    s, ss = 512, 2
    S = s * ss
    mott = fbm(s, beta=2.3, seed=51, lo=1, hi=9)
    fine = fbm(s, beta=0.8, seed=52, lo=16)
    dry = fbm(s, beta=2.0, seed=53, lo=2, hi=12)
    g = 0.30 + (mott - 0.5) * 0.10 + (fine - 0.5) * 0.08
    rgb = np.dstack([g * 0.62 + dry * 0.05, g * 1.0, g * 0.30])
    base = to_img(rgb).resize((S, S), Image.BICUBIC)
    draw = ImageDraw.Draw(base)
    r = np.random.default_rng(54)
    lines = []
    for _ in range(9000):
        x, y = r.uniform(0, S), r.uniform(0, S)
        ang = r.normal(-math.pi / 2, 0.55)
        ln = r.uniform(7, 20) * ss / 2
        b = r.uniform(0.20, 0.50)
        yellow = r.uniform(0.50, 0.80)
        colour = (int(255 * b * yellow), int(255 * b), int(255 * b * r.uniform(0.18, 0.34)))
        lines.append((x, y, x + math.cos(ang) * ln, y + math.sin(ang) * ln, colour, 1 if r.random() < 0.7 else 2))
    _wrapped_lines(draw, S, lines, None)
    save(base.resize((s, s), Image.LANCZOS), "SD_Grass.png")


def bark():
    """Deeply furrowed trunk bark, 256x512, tiles both ways. Furrow shading is baked into the albedo."""
    h, w = 512, 256
    ridge = fbm(h, w, beta=1.9, seed=61, lo=3, stretch=(0.16, 1.0))      # tall, narrow plates
    ridge2 = fbm(h, w, beta=1.4, seed=62, lo=8, stretch=(0.30, 1.0))
    crack = np.abs(ridge * 0.7 + ridge2 * 0.3 - 0.5) * 2.0                 # 0 in the furrow
    crack = np.clip(crack * 2.2, 0, 1) ** 0.7
    fine = fbm(h, w, beta=0.8, seed=63, lo=30)
    tone = fbm(h, w, beta=2.4, seed=64, lo=1, hi=5)
    dark, mid, light = col(0.10, 0.075, 0.055), col(0.36, 0.28, 0.21), col(0.50, 0.42, 0.33)
    rgb = mix(np.broadcast_to(dark, (h, w, 3)), np.broadcast_to(mid, (h, w, 3)), crack)
    rgb = mix(rgb, np.broadcast_to(light, (h, w, 3)), np.clip((crack - 0.75) * 3.0, 0, 1) * 0.6)
    rgb *= (0.86 + fine * 0.28)[..., None]
    rgb *= (0.90 + tone * 0.22)[..., None]
    save(to_img(np.clip(rgb, 0, 1)), "SD_Bark.png")


def wood():
    """Oiled hardwood slats (benches, pergola). Planks run along U."""
    s = 256
    grain = fbm(s, beta=1.6, seed=71, lo=4, stretch=(1.0, 0.10))
    tone = fbm(s, beta=2.2, seed=72, lo=1, hi=5)
    yy = np.mgrid[0:s, 0:s][0]
    plank = yy // (s // 4)
    r = np.random.default_rng(73)
    pv = r.uniform(-0.06, 0.06, 4)[plank]
    gap = np.clip(1 - np.minimum(yy % (s // 4), (s // 4) - 1 - yy % (s // 4)) / 1.5, 0, 1)
    v = 0.44 + pv + (grain - 0.5) * 0.22 + (tone - 0.5) * 0.06
    rgb = np.dstack([v * 1.18, v * 0.80, v * 0.50])
    rgb = mix(rgb, np.array([0.10, 0.07, 0.05]), gap * 0.9)
    save(to_img(np.clip(rgb, 0, 1)), "SD_Wood.png")


# ----------------------------------------------------------------------------- alpha-tested weaves
def chain_link():
    """Galvanised chain-link, 4x4 diamonds per tile. Stylised wire gauge so it survives mip-mapping."""
    s, ss = 256, 4
    S = s * ss
    img = Image.new("RGBA", (S, S), (150, 155, 160, 0))
    d = ImageDraw.Draw(img)
    n = 4
    cell = S / n
    wire = int(cell * 0.085)
    for i in range(-n, 2 * n + 1):
        d.line((i * cell, 0, i * cell + S, S), fill=(176, 181, 186, 255), width=wire)
        d.line((i * cell, 0, i * cell - S, S), fill=(140, 146, 152, 255), width=wire)
    # brighter twisted knuckle where wires cross
    for i in range(n + 1):
        for j in range(n + 1):
            for (cx, cy) in ((i * cell, j * cell), ((i + .5) * cell, (j + .5) * cell)):
                d.ellipse((cx - wire * .8, cy - wire * .8, cx + wire * .8, cy + wire * .8), fill=(205, 209, 213, 255))
    out = img.resize((s, s), Image.LANCZOS)
    save(solidify(out), "SD_ChainLink.png")


def net():
    """Knotted black court net, 6x6 squares per tile, with a faint sheen on the cord."""
    s, ss = 256, 4
    S = s * ss
    img = Image.new("RGBA", (S, S), (22, 22, 24, 0))
    d = ImageDraw.Draw(img)
    n = 6
    cell = S / n
    cord = int(cell * 0.13)
    for i in range(n + 1):
        d.line((i * cell, 0, i * cell, S), fill=(26, 26, 29, 255), width=cord)
        d.line((0, i * cell, S, i * cell), fill=(34, 34, 38, 255), width=cord)
    for i in range(n + 1):
        for j in range(n + 1):
            cx, cy = i * cell, j * cell
            d.ellipse((cx - cord * .85, cy - cord * .85, cx + cord * .85, cy + cord * .85), fill=(48, 48, 54, 255))
    out = img.resize((s, s), Image.LANCZOS)
    save(solidify(out), "SD_Net.png")


# ----------------------------------------------------------------------------- foliage atlas
LEAF_DARK, LEAF_MID, LEAF_LIGHT = col(0.085, 0.175, 0.055), col(0.185, 0.335, 0.095), col(0.37, 0.50, 0.15)


def leaf_colour(r, depth, warm=0.0):
    """depth 0 = buried in the cluster (dark), 1 = sunlit outer leaf."""
    t = np.clip(depth + r.normal(0, 0.13), 0, 1)
    c = LEAF_DARK * (1 - t) + LEAF_MID * t if t < 0.6 else LEAF_MID * (1 - (t - 0.6) / 0.4) + LEAF_LIGHT * ((t - 0.6) / 0.4)
    c = c * r.uniform(0.86, 1.14)
    c = c + np.array([0.10, 0.045, -0.02]) * warm * r.uniform(0.0, 1.0)
    return np.clip(c, 0, 1)


def leaf_shape(length, width, n=9, tip=0.68):
    """Half-outline of a pointed ovate leaf along +X, base at the origin."""
    pts = []
    for i in range(n + 1):
        t = i / n
        w = width * (math.sin(math.pi * t ** tip) ** 0.9)
        pts.append((t * length, w))
    return pts


def draw_leaf(d, x, y, ang, length, width, colour, r, veins=True):
    ca, sa = math.cos(ang), math.sin(ang)
    half = leaf_shape(length, width)

    def tr(px, py):
        return (x + px * ca - py * sa, y + px * sa + py * ca)

    upper = [tr(px, py) for px, py in half]
    lower = [tr(px, -py) for px, py in half]
    spine = [tr(px, 0) for px, _ in half]
    c1 = tuple(int(255 * v) for v in np.clip(colour * 1.10, 0, 1)) + (255,)
    c2 = tuple(int(255 * v) for v in np.clip(colour * 0.84, 0, 1)) + (255,)
    d.polygon(upper + spine[::-1], fill=c1)
    d.polygon(lower + spine[::-1], fill=c2)
    if veins:
        rib = tuple(int(255 * v) for v in np.clip(colour * 1.45 + 0.03, 0, 1)) + (255,)
        d.line([spine[0], spine[-1]], fill=rib, width=max(1, int(width * 0.13)))


def draw_twig(d, pts, width, colour=(70, 52, 36, 255)):
    for i in range(len(pts) - 1):
        d.line([pts[i], pts[i + 1]], fill=colour, width=max(1, int(width * (1 - i / len(pts) * 0.6))))


def broadleaf_cluster(size, seed, leaf_len, leaf_w, count, warm=0.0):
    """Fan of ovate leaves radiating from the bottom-centre pivot. Returns an RGBA tile."""
    r = np.random.default_rng(seed)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ox, oy = size * 0.5, size * 0.97
    twigs = []
    for k in range(9):
        a = -math.pi / 2 + (k - 4) * 0.27 + r.normal(0, 0.06)
        ln = size * r.uniform(0.74, 0.90) * (1 - abs(k - 4) * 0.075)
        pts = [(ox, oy)]
        cur = a
        for sgm in range(1, 9):
            cur += r.normal(0, 0.07) + (0.02 if a > -math.pi / 2 else -0.02)
            pts.append((pts[-1][0] + math.cos(cur) * ln / 8, pts[-1][1] + math.sin(cur) * ln / 8))
        twigs.append(pts)
    for pts in twigs:
        draw_twig(d, pts, size * 0.016)
    for layer in range(3):
        depth = (0.10, 0.52, 0.90)[layer]
        n = int(count * (0.42, 0.34, 0.24)[layer])
        for _ in range(n):
            pts = twigs[r.integers(len(twigs))]
            i = int(len(pts) * r.uniform(0.12, 1.0) ** 0.75)
            i = min(max(i, 1), len(pts) - 1)
            bx, by = pts[i]
            px, py = pts[i - 1]
            base = math.atan2(by - py, bx - px)
            side = 1 if r.random() < 0.5 else -1
            ang = base + side * r.uniform(0.35, 1.25)
            ln = leaf_len * r.uniform(0.72, 1.18) * (0.85 + 0.15 * layer)
            draw_leaf(d, bx + r.normal(0, size * 0.012), by + r.normal(0, size * 0.012), ang, ln, leaf_w * r.uniform(0.8, 1.15),
                      leaf_colour(r, depth, warm), r)
    return img


def shrub_cluster(size, seed):
    """Dense small-leaved hedge card with an irregular outline."""
    r = np.random.default_rng(seed)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for layer in range(3):
        depth = (0.08, 0.5, 0.92)[layer]
        for _ in range((520, 420, 300)[layer]):
            a = r.uniform(0, 2 * math.pi)
            rad = size * 0.46 * math.sqrt(r.uniform(0, 1)) * (0.86 + 0.14 * math.sin(a * 5 + seed))
            x, y = size * 0.5 + math.cos(a) * rad, size * 0.52 + math.sin(a) * rad * 0.94
            draw_leaf(d, x, y, r.uniform(0, 2 * math.pi), size * r.uniform(0.045, 0.075), size * r.uniform(0.016, 0.026),
                      leaf_colour(r, depth), r, veins=False)
    return img


def pinnate_cluster(w, h, seed):
    """Feathery bipinnate fronds (flamboyant / phuong vi): rachises fanning from the left edge pivot."""
    r = np.random.default_rng(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    ox, oy = w * 0.02, h * 0.5
    for layer in range(2):
        depth = (0.25, 0.85)[layer]
        for k in range(6):
            a = (k - 2.5) * 0.17 + r.normal(0, 0.03)
            ln = w * r.uniform(0.80, 0.96)
            steps = 30
            cur = a
            x, y = ox, oy
            for sgm in range(steps):
                cur += (0.0 - a) * 0.01 + r.normal(0, 0.008)
                nx, ny = x + math.cos(cur) * ln / steps, y + math.sin(cur) * ln / steps
                d.line((x, y, nx, ny), fill=(74, 66, 34, 255), width=max(1, int(h * 0.012)))
                if sgm > 1:
                    t = sgm / steps
                    leaflet = h * 0.115 * math.sin(math.pi * min(1, t * 1.15)) ** 0.6 + h * 0.02
                    for side in (-1, 1):
                        la = cur + side * (1.15 - 0.25 * t) + r.normal(0, 0.06)
                        draw_leaf(d, nx, ny, la, leaflet * r.uniform(0.85, 1.1), leaflet * 0.20,
                                  leaf_colour(r, depth, 0.25) * np.array([1.05, 1.06, 0.95]), r, veins=False)
                x, y = nx, ny
    return img


def flower_cluster(size, seed):
    """Flame-tree blossom truss: scarlet/orange five-petal flowers over a few leaflets."""
    r = np.random.default_rng(seed)
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(46):
        a = r.uniform(0, 2 * math.pi)
        rad = size * 0.42 * math.sqrt(r.uniform(0, 1))
        x, y = size * 0.5 + math.cos(a) * rad, size * 0.5 + math.sin(a) * rad
        draw_leaf(d, x, y, r.uniform(0, 2 * math.pi), size * 0.11, size * 0.022, leaf_colour(r, 0.35, 0.2), r, veins=False)
    for layer in range(2):
        for _ in range((30, 26)[layer]):
            a = r.uniform(0, 2 * math.pi)
            rad = size * 0.40 * math.sqrt(r.uniform(0, 1))
            x, y = size * 0.5 + math.cos(a) * rad, size * 0.5 + math.sin(a) * rad
            pr = size * r.uniform(0.042, 0.062)
            shade = (0.62, 1.0)[layer] * r.uniform(0.88, 1.08)
            hot = r.uniform(0, 1)
            base = np.array([0.86, 0.13 + 0.20 * hot, 0.035]) * shade
            rot = r.uniform(0, 2 * math.pi)
            for p in range(5):
                pa = rot + p * 2 * math.pi / 5
                cx, cy = x + math.cos(pa) * pr * 0.75, y + math.sin(pa) * pr * 0.75
                fill = tuple(int(255 * v) for v in np.clip(base * r.uniform(0.9, 1.1), 0, 1)) + (255,)
                d.ellipse((cx - pr * 0.62, cy - pr * 0.62, cx + pr * 0.62, cy + pr * 0.62), fill=fill)
            d.ellipse((x - pr * 0.26, y - pr * 0.26, x + pr * 0.26, y + pr * 0.26), fill=(246, 196, 70, 255))
    return img


def palm_frond(w, h, seed):
    """Pinnate palm frond along +X, base at the left edge. Leaflets taper and yellow slightly at the tips."""
    r = np.random.default_rng(seed)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    y0 = h * 0.5
    steps = 54
    for layer in range(2):
        for i in range(2, steps):
            t = i / steps
            x = w * (0.02 + 0.96 * t)
            env = math.sin(math.pi * min(1.0, t * 1.08)) ** 0.55
            ln = h * 0.50 * env * r.uniform(0.86, 1.06) + h * 0.03
            for side in (-1, 1):
                if layer == 0 and r.random() < 0.5:
                    continue
                ang = side * (1.18 - 0.62 * t) + r.normal(0, 0.05)
                depth = (0.30, 0.82)[layer] * (0.85 + 0.3 * (1 if side < 0 else 0))
                c = leaf_colour(r, depth, 0.35) * np.array([1.0, 1.04, 0.80])
                draw_leaf(d, x, y0, ang, ln, h * 0.030 * (1.15 - 0.4 * t), c, r, veins=False)
    d.line((w * 0.0, y0, w * 0.97, y0), fill=(120, 118, 54, 255), width=max(2, int(h * 0.030)))
    return img


def foliage_atlas():
    """1024 atlas; drawn at 2x and filtered down for clean alpha edges."""
    s, ss = 1024, 2
    S = s * ss
    atlas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    # name: (x, y, w, h) in final pixels, y measured from the TOP of the image
    rects = {
        "broadleaf_a": (0, 0, 512, 512),
        "broadleaf_b": (512, 0, 512, 512),
        "pinnate": (0, 512, 512, 256),
        "flowers": (512, 512, 256, 256),
        "shrub": (768, 512, 256, 256),
        "palm": (0, 768, 1024, 256),
    }
    tiles = {
        "broadleaf_a": broadleaf_cluster(512 * ss, 101, 512 * ss * 0.165, 512 * ss * 0.058, 210),
        "broadleaf_b": broadleaf_cluster(512 * ss, 102, 512 * ss * 0.128, 512 * ss * 0.048, 300, warm=0.5),
        "pinnate": pinnate_cluster(512 * ss, 256 * ss, 103),
        "flowers": flower_cluster(256 * ss, 104),
        "shrub": shrub_cluster(256 * ss, 105),
        "palm": palm_frond(1024 * ss, 256 * ss, 106),
    }
    pad = 6 * ss            # transparent gutter so neighbours never bleed across mips
    for name, (x, y, w, h) in rects.items():
        tile = tiles[name].resize((w * ss - 2 * pad, h * ss - 2 * pad), Image.LANCZOS)
        atlas.paste(tile, (x * ss + pad, y * ss + pad))
    out = solidify(atlas.resize((s, s), Image.LANCZOS))
    save(out, "SD_Foliage.png")
    a = np.asarray(out)[..., 3] / 255.0
    layout = {}
    for name, (x, y, w, h) in rects.items():
        cov = float((a[y:y + h, x:x + w] > 0.5).mean())
        # UV rect with v up (glTF importer / Blender convention handled by the builder)
        layout[name] = {"u0": x / s, "v0": 1 - (y + h) / s, "u1": (x + w) / s, "v1": 1 - y / s, "coverage": round(cov, 3)}
        print(f"      {name:12s} coverage {cov:.2f}")
    with open(os.path.join(HERE, "foliage_atlas.json"), "w") as f:
        json.dump(layout, f, indent=1)


# ----------------------------------------------------------------------------- city
def facade():
    """Tiling apartment/office facade: 8 x 8 bays per tile (24 m x 24 m). Alpha = glass mask."""
    s, ss = 512, 2
    S = s * ss
    r = np.random.default_rng(81)
    wall_noise = fbm(s, beta=1.6, seed=82, lo=4)
    wall = np.dstack([0.71 + (wall_noise - 0.5) * 0.08, 0.675 + (wall_noise - 0.5) * 0.08, 0.63 + (wall_noise - 0.5) * 0.08])
    img = to_img(wall).resize((S, S), Image.BICUBIC).convert("RGBA")
    mask = Image.new("L", (S, S), 0)
    d, dm = ImageDraw.Draw(img), ImageDraw.Draw(mask)
    n = 8
    cell = S / n
    for j in range(n):
        # floor band / slab edge
        d.rectangle((0, j * cell, S, j * cell + cell * 0.07), fill=(196, 188, 176, 255))
        for i in range(n):
            x0, y0 = i * cell + cell * 0.16, j * cell + cell * 0.20
            x1, y1 = (i + 1) * cell - cell * 0.16, (j + 1) * cell - cell * 0.12
            kind = r.random()
            for row in range(int(y0), int(y1)):
                t = (row - y0) / (y1 - y0)
                if kind < 0.24:      # catches the low sun: molten glass
                    c = (int(255 * (0.98 - 0.25 * t)), int(255 * (0.66 - 0.28 * t)), int(255 * (0.30 - 0.16 * t)))
                elif kind < 0.36:    # interior light already on
                    c = (int(255 * (0.92 - 0.10 * t)), int(255 * (0.80 - 0.14 * t)), int(255 * (0.52 - 0.14 * t)))
                else:                # reflects the cool upper sky
                    c = (int(255 * (0.30 - 0.13 * t)), int(255 * (0.39 - 0.15 * t)), int(255 * (0.52 - 0.17 * t)))
                d.line((x0, row, x1, row), fill=c + (255,))
            dm.rectangle((x0, y0, x1, y1), fill=255)
            d.rectangle((x0, y0, x1, y1), outline=(232, 228, 220, 255), width=max(2, int(cell * 0.035)))
            d.line(((x0 + x1) / 2, y0, (x0 + x1) / 2, y1), fill=(226, 222, 214, 255), width=max(2, int(cell * 0.028)))
            d.rectangle((x0 - cell * 0.03, y1, x1 + cell * 0.03, y1 + cell * 0.035), fill=(150, 144, 136, 255))
    out = img.resize((s, s), Image.LANCZOS)
    out.putalpha(mask.resize((s, s), Image.LANCZOS))
    save(out, "SD_Facade.png")


def skyline():
    """Hazy distant city silhouette strip, tiles along U. Alpha-tested card far beyond the park."""
    w, h, ss = 1024, 256, 2
    W, H = w * ss, h * ss
    r = np.random.default_rng(91)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for layer in range(2):
        x = -40
        tint = ((142, 122, 150), (104, 92, 128))[layer]
        while x < W + 40:
            bw = int(r.uniform(34, 110) * ss / 2 * (1.0, 1.25)[layer])
            bh = int(H * r.uniform(0.16, 0.50) * (1.0, 0.72)[layer] ** 0 + (H * 0.30 if r.random() < 0.12 and layer == 0 else 0))
            bh = min(bh, int(H * 0.92))
            for ox in (0, W, -W):
                d.rectangle((x + ox, H - bh, x + bw + ox, H), fill=tint + (255,))
                if r.random() < 0.35:
                    d.rectangle((x + bw * 0.25 + ox, H - bh - H * 0.05, x + bw * 0.75 + ox, H - bh), fill=tint + (255,))
                if r.random() < 0.18:
                    d.line((x + bw * 0.5 + ox, H - bh - H * 0.14, x + bw * 0.5 + ox, H - bh), fill=tint + (255,), width=ss)
            # sparse windows catching the last light
            for _ in range(int(bw * bh / 900)):
                wx, wy = x + r.uniform(3, bw - 3), H - r.uniform(4, bh - 4)
                cw = (255, 196, 120) if r.random() < 0.55 else (176, 168, 196)
                for ox in (0, W, -W):
                    d.rectangle((wx + ox, wy, wx + 2 * ss + ox, wy + 2 * ss), fill=cw + (255,))
            x += int(bw * r.uniform(0.82, 1.25))
    out = img.resize((w, h), Image.LANCZOS)
    arr = np.asarray(out).astype(np.float64) / 255.0
    grad = np.linspace(0.0, 1.0, h)[:, None]                         # 0 top, 1 bottom: warm haze near the ground
    haze = np.array([0.93, 0.66, 0.50])
    arr[..., :3] = arr[..., :3] * (1 - 0.45 * grad[..., None]) + haze * 0.45 * grad[..., None]
    save(solidify(Image.fromarray((arr * 255).astype(np.uint8), "RGBA")), "SD_Skyline.png")


# ----------------------------------------------------------------------------- sky / water helpers
def cloud():
    """Tiling cloud density for the procedural sky. R = wispy cirrus, G = soft cumulus bank."""
    s = 512
    wisps = fbm(s, beta=2.5, seed=111, lo=1, hi=64, stretch=(3.2, 1.0))
    warp = fbm(s, beta=2.2, seed=112, lo=1, hi=12)
    wisps = np.clip((wisps * 0.75 + warp * 0.25 - 0.47) * 3.4, 0, 1) ** 1.2
    banks = fbm(s, beta=3.0, seed=113, lo=1, hi=40)
    banks = np.clip((banks - 0.50) * 3.6, 0, 1) ** 1.1
    detail = fbm(s, beta=1.8, seed=114, lo=8)
    save(to_img(np.dstack([wisps, banks * (0.75 + 0.25 * detail), detail])), "SD_Cloud.png")


def ripple():
    s = 256
    h = fbm(s, beta=2.4, seed=121, lo=2, hi=40, stretch=(1.0, 2.2))
    save(Image.fromarray(normal_map(h * 9.0, 1.0), "RGB"), "SD_Ripple_N.png")


def white():
    Image.new("RGB", (4, 4), (255, 255, 255)).save(os.path.join(OUT, "SD_White.png"))


if __name__ == "__main__":
    only = set(sys.argv[1:])
    jobs = [asphalt, paving, concrete, acrylic, grass, bark, wood, chain_link, net, foliage_atlas, facade, skyline, cloud, ripple, white]
    print("Writing", OUT)
    for job in jobs:
        if not only or job.__name__ in only:
            job()


def tree_impostors():
    """2x2 atlas of the orthographic LOD0 renders written by build_trees.py (tile 0 = top-left)."""
    work = os.path.join(HERE, "_impostor")
    layout = json.load(open(os.path.join(work, "layout.json")))
    tile = 512
    atlas = Image.new("RGBA", (tile * 2, tile * 2), (0, 0, 0, 0))
    for name, info in layout.items():
        index = info["tile"]
        src = Image.open(os.path.join(work, f"{index}_{name}.png")).convert("RGBA").resize((tile, tile), Image.LANCZOS)
        arr = np.asarray(src).astype(np.float64)
        arr[..., 3] = np.where(arr[..., 3] > 110, 255, 0)          # crisp silhouette for alpha testing
        atlas.paste(Image.fromarray(arr.astype(np.uint8), "RGBA"), ((index % 2) * tile, (index // 2) * tile))
    save(solidify(atlas), "SD_TreeImpostors.png")


if __name__ == "__main__" and "tree_impostors" in sys.argv[1:]:
    tree_impostors()
