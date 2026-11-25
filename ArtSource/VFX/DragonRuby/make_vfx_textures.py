"""Dragon Ruby VFX - procedural sprite textures (system Python: numpy, SciPy, OpenCV).

    python make_vfx_textures.py <unity_texture_dir>

T_DragonRuby_Fire.png   tileable fire ribbon (R value, A coverage)
T_DragonRuby_Flame.png  4x4 flipbook: a lick of flame that rises, splits and burns out
T_DragonRuby_Ring.png   shock ring with flame teeth for the contact
T_DragonRuby_Glow.png   soft halo of the pearl
"""
import os, sys
import cv2
import numpy as np
from scipy import ndimage as ndi

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(5)


def noise(h, w, cells_y, cells_x, seed, octaves=4):
    """tileable value noise in 0..1"""
    r = np.random.default_rng(seed)
    out, amp, tot = np.zeros((h, w)), 1.0, 0.0
    for _ in range(octaves):
        g = r.random((cells_y, cells_x))
        g = np.pad(g, ((0, 3), (0, 3)), mode='wrap')
        y = np.arange(h) * cells_y / h
        x = np.arange(w) * cells_x / w
        out += amp * ndi.map_coordinates(g, np.meshgrid(y + 1, x + 1, indexing='ij'), order=3, mode='grid-wrap')
        tot += amp
        amp *= .5
        cells_y, cells_x = cells_y * 2, cells_x * 2
    return np.clip(out / tot, 0, 1)


def save(name, rgb, a):
    rgba = np.dstack([rgb, rgb, rgb, a]) if rgb.ndim == 2 else np.dstack([rgb, a])
    cv2.imwrite(os.path.join(OUT, name), (np.clip(rgba, 0, 1)[..., [2, 1, 0, 3]] * 255 + .5).astype(np.uint8))


def st(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# fire ribbon
h, w = 128, 512
n1, n2 = noise(h, w, 4, 6, 1), noise(h, w, 8, 14, 2)
v = np.abs((np.arange(h) + .5) / h * 2 - 1)[:, None]
tongue = st(1.0, .12, v + (n1 - .45) * 1.15)
value = np.clip(tongue * (.45 + .75 * n2) + st(.55, 0, v) * .25, 0, 1)
save('T_DragonRuby_Fire.png', value, np.clip(tongue * (.55 + .6 * n2), 0, 1))

# flame flipbook
cell = 128
sheet = np.zeros((cell * 4, cell * 4))
alpha = np.zeros_like(sheet)
yy, xx = np.mgrid[0:cell, 0:cell] / (cell - 1)
for f in range(16):
    t = f / 15
    n = noise(cell, cell, 4, 4, 40, 4)
    n = np.roll(n, int(-t * cell * .9), axis=0)
    y = 1 - yy                                             # 0 bottom, 1 top
    x = (xx - .5) + (n - .5) * .30 * y * (1 + t)
    height = .55 + .35 * st(0, .4, t)
    half = (.23 * (1 - .45 * t)) * np.sqrt(np.clip(1 - (y / height) ** 1.6, 0, 1)) * st(-.02, .18, y)
    body = st(0, .10, half - np.abs(x)) * st(0, .03, y)
    burn = st(t * 1.15 - .25, t * 1.15 + .05, 1 - n * .9 - y * .22 * t)
    a = body * burn * (1 - st(.82, 1, t))
    core = st(0, .16, half * .62 - np.abs(x)) * (1 - y * .7)
    cy, cx = divmod(f, 4)
    sheet[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = np.clip(.45 + .75 * core, 0, 1) * a
    alpha[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = a
save('T_DragonRuby_Flame.png', sheet, alpha)

# shock ring
s = 512
yy, xx = (np.mgrid[0:s, 0:s] + .5) / s * 2 - 1
r, ang = np.hypot(xx, yy), np.arctan2(yy, xx)
u = ((ang + np.pi) / (2 * np.pi) * s).astype(int) % s
teeth = np.clip((noise(8, s, 1, 44, 9, 3)[1][u] - .30) / .55, 0, 1)
ring = st(.16, 0, np.abs(r - .66)) * .22 + st(.030, 0, np.abs(r - .66))
reach = .66 + .30 * teeth ** 2.2
flames = st(.0, .10, reach - r) * st(.64, .68, r) * (.25 + .75 * np.clip((reach - r) / .12, 0, 1))
val = np.clip(ring + flames * .85, 0, 1)
save('T_DragonRuby_Ring.png', val, np.clip(val, 0, 1) * st(1.0, .93, r))
# pearl halo
s = 128
yy, xx = (np.mgrid[0:s, 0:s] + .5) / s * 2 - 1
r = np.hypot(xx, yy)
halo = np.clip(1 - r, 0, 1) ** 2.2
save('T_DragonRuby_Glow.png', np.clip(halo * 1.2, 0, 1), halo)
print('textures', os.listdir(OUT))
