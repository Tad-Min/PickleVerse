"""Paint the PickleVerse Elongated paddle atlas (albedo + normal), 1024x2048.

python make_paddle_textures.py <out_dir>
Top 3/4: face graphics in face space (see paddle_shape.face_px). Bottom 1/4: perforated overgrip strip.
"""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paddle_shape as ps

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "textures")
os.makedirs(OUT, exist_ok=True)
SS = 2                                    # supersampling for the face graphics
FW, FH = ps.ATLAS_W * SS, int(ps.ATLAS_H * (1 - ps.FACE_V0)) * SS
BLUE = (34, 110, 255)
SKY = (120, 185, 255)
WHITE = (244, 246, 250)
FONT = "C:/Windows/Fonts/bahnschrift.ttf"


def px(x, z):
    u, v = ps.face_uv(x, z)
    return u * FW, (1 - (v - ps.FACE_V0) / (1 - ps.FACE_V0)) * FH


def U(units):
    return units * ps.PX_PER_UNIT * SS


face = Image.new("RGBA", (FW, FH), (11, 11, 13, 255))


# --------------------------------------------------------------------------------------- PICKLEVERSE wordmarks
def wordmark(length_units, cap_units):
    font = ImageFont.truetype(FONT, 400)
    font.set_variation_by_name("Bold SemiCondensed")
    word, left, mid = "PICKLEVERSE", "PICK", "PICKLE"
    l0, t0, r0, b0 = font.getbbox(word)
    img = Image.new("RGBA", (r0 + 80, b0 + 80), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.text((20, 20), word, font=font, fill=WHITE)
    x_le0 = 20 + font.getlength(left)
    x_le1 = 20 + font.getlength(mid)
    # "LE": white melting into blue, with a speed slash through the middle
    arr = np.array(img).astype(np.float32)
    ys = np.arange(arr.shape[0])[:, None]
    xs = np.arange(arr.shape[1])[None, :]
    in_le = (xs >= x_le0) & (xs < x_le1)
    top, bottom = 20 + t0, 20 + b0
    g = np.clip((ys - top) / max(1, bottom - top), 0, 1)
    for c in range(3):
        blend = WHITE[c] * (1 - g) + SKY[c] * g * 0.6 + BLUE[c] * g * 0.4
        arr[..., c] = np.where(in_le & (arr[..., 3] > 0), blend, arr[..., c])
    img = Image.fromarray(arr.astype(np.uint8))
    d = ImageDraw.Draw(img)
    mid_y = (top + bottom) / 2 + 10
    d.polygon([(x_le0 - 40, mid_y - 9), (x_le1 + 70, mid_y - 14), (x_le1 + 60, mid_y + 4), (x_le0 - 50, mid_y + 9)], fill=BLUE + (255,))
    img = img.crop(img.getbbox())
    # italic shear
    shear = 0.18
    w, h = img.size
    img = img.transform((int(w + h * shear), h), Image.AFFINE, (1, shear, -h * shear, 0, 1, 0), Image.BICUBIC)
    img = img.crop(img.getbbox())
    # fit to the requested size on the face
    cap = U(cap_units)
    scale = cap / (b0 - t0)
    target_len = U(length_units)
    return img.resize((int(target_len), int(img.size[1] * scale)), Image.LANCZOS)


def place(img, cx, cz):
    x, y = px(cx, cz)
    face.alpha_composite(img, (int(x - img.size[0] / 2), int(y - img.size[1] / 2)))


wm = wordmark(0.76, 0.082)
place(wm.rotate(-90, expand=True, resample=Image.BICUBIC), -0.325, 1.46)   # reads downwards, baseline on the outer edge
place(wm.rotate(90, expand=True, resample=Image.BICUBIC), 0.335, 1.39)     # reads upwards, baseline on the outer edge


# --------------------------------------------------------------------------------------- edge accents
path = ps.open_path()


def ribbon(points, inner, widths, colour):
    """Fill a strip that starts 'inner' units inside the edge and grows inward by widths[i]."""
    ns = ps.normals(points)
    outer = [px(x - nx * inner, z - nz * inner) for (x, z), (nx, nz) in zip(points, ns)]
    inner_pts = [px(x - nx * (inner + w), z - nz * (inner + w)) for (x, z), (nx, nz), w in zip(points, ns, widths)]
    ImageDraw.Draw(face).polygon(outer + inner_pts[::-1], fill=colour)


def dense(points, step=0.004):
    out = []
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        n = max(1, int(math.hypot(x1 - x0, z1 - z0) / step))
        out += [(x0 + (x1 - x0) * i / n, z0 + (z1 - z0) * i / n) for i in range(n)]
    return out + [points[-1]]


edge = dense(path)
# top-left: blue crescent hugging the corner, a fine white arc inside it
tl = [p for p in edge if p[0] < -0.01 and p[1] > 1.74]
n = len(tl)
ribbon(tl, ps.GUARD_HALF + 0.002, [0.048 * max(0.0, math.sin(math.pi * i / (n - 1))) ** 0.7 for i in range(n)], BLUE + (255,))
tl2 = [p for p in edge if p[0] < 0.06 and p[1] > 1.80]
n = len(tl2)
ribbon(tl2, 0.104, [0.009 * max(0.0, math.sin(math.pi * i / (n - 1))) ** 0.5 for i in range(n)], WHITE + (255,))
tl3 = [p for p in edge if -0.36 < p[0] < 0.0 and p[1] > 2.02]
n = len(tl3)
ribbon(tl3, 0.128, [0.0055 * max(0.0, math.sin(math.pi * i / (n - 1))) for i in range(n)], SKY + (230,))
# right side: two blue fins along the edge (sharp leading tip, long tail)
for lo, hi, peak, width in ((1.72, 2.06, 0.68, 0.058), (0.80, 1.14, 0.40, 0.066)):
    seg = [p for p in edge if p[0] > 0.1 and lo < p[1] < hi]
    n = len(seg)
    def prof(t):
        return (t / peak) ** 1.6 if t < peak else ((1 - t) / (1 - peak)) ** 0.7
    ribbon(seg, ps.GUARD_HALF + 0.002, [width * prof(i / (n - 1)) for i in range(n)], BLUE + (255,))
    ribbon(seg, ps.GUARD_HALF + 0.002, [width * 0.3 * prof(i / (n - 1)) for i in range(n)], SKY + (255,))

# --------------------------------------------------------------------------------------- orbit emblem in the throat
emb = Image.new("RGBA", (int(U(0.30)), int(U(0.30))), (0, 0, 0, 0))
d = ImageDraw.Draw(emb)
cx, cy = emb.size[0] / 2, emb.size[1] / 2
rx, ry = U(0.092), U(0.036)
d.arc([cx - rx, cy - ry, cx + rx, cy + ry], start=205, end=505, fill=WHITE + (255,), width=int(U(0.0095)))
emb = emb.rotate(16, resample=Image.BICUBIC)
place(emb, 0.015, 0.80)
d = ImageDraw.Draw(face)
x0, y0 = px(-0.005, 0.735)
x1, y1 = px(-0.005, 0.92)
d.line([(x0, y0), (x1, y1)], fill=WHITE + (255,), width=int(U(0.0065)))
d.polygon([(x1, y1 - U(0.028)), (x1 - U(0.014), y1 + U(0.004)), (x1 + U(0.014), y1 + U(0.004))], fill=WHITE + (255,))
tag_font = ImageFont.truetype(FONT, int(U(0.026)))
tag_font.set_variation_by_name("Bold SemiCondensed")
tx, ty = px(0.0, 0.66)
d.text((tx, ty), "PRO", font=tag_font, fill=BLUE + (255,), anchor="mm")

face = face.resize((ps.ATLAS_W, int(ps.ATLAS_H * (1 - ps.FACE_V0))), Image.LANCZOS)

# --------------------------------------------------------------------------------------- overgrip strip
GH = int(ps.ATLAS_H * ps.FACE_V0)
GW = ps.ATLAS_W
GRIP_LEN = ps.GRIP_TOP - ps.GRIP_BOTTOM
CIRC = 0.60
PITCH = 0.094
u = (np.arange(GW)[None, :] + 0.5) / GW
v = 1 - (np.arange(GH)[:, None] + 0.5) / GH
phase = (v * GRIP_LEN) / PITCH - u
t = phase - np.floor(phase)
height = np.clip(t / 0.07, 0, 1) ** 0.6 * (0.62 + 0.38 * t)
albedo = 0.86 + 0.06 * t
albedo = np.where(t < 0.05, albedo * (0.80 + 0.2 * t / 0.05), albedo)
DOTS = 22
for row_t, stagger in ((0.33, 0.0), (0.70, 0.5)):
    k = u * DOTS + stagger + np.floor(phase) * 0.37
    du = (k - np.floor(k) - 0.5) / DOTS * CIRC
    dt = (t - row_t) * PITCH
    dist = np.hypot(du, dt)
    hole = np.clip(1 - (dist - 0.0034) / 0.0013, 0, 1)
    albedo = albedo * (1 - 0.42 * hole)
    height = height - 0.35 * hole
grip_rgb = np.stack([albedo * 0.95, albedo * 0.96, albedo * 0.985], axis=-1)
grip = Image.fromarray((np.clip(grip_rgb, 0, 1) * 255).astype(np.uint8), "RGB")

atlas = Image.new("RGB", (ps.ATLAS_W, ps.ATLAS_H))
atlas.paste(face.convert("RGB"), (0, 0))
atlas.paste(grip, (0, ps.ATLAS_H - GH))
atlas.save(os.path.join(OUT, "pv_paddle_albedo.png"), optimize=True)

# normal map: flat face, grip relief from the height field (physical units)
dx = np.gradient(height, axis=1) / (CIRC / GW)
dy = -np.gradient(height, axis=0) / (GRIP_LEN / GH)
strength = 0.0035
nx, ny = -dx * strength, -dy * strength
nz = np.ones_like(nx)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
nrm = np.stack([nx / ln, ny / ln, nz / ln], axis=-1) * 0.5 + 0.5
normal = Image.new("RGB", (ps.ATLAS_W, ps.ATLAS_H), (128, 128, 255))
normal.paste(Image.fromarray((nrm * 255).astype(np.uint8), "RGB"), (0, ps.ATLAS_H - GH))
normal.save(os.path.join(OUT, "pv_paddle_normal.png"), optimize=True)
for name in ("pv_paddle_albedo.png", "pv_paddle_normal.png"):
    print(name, os.path.getsize(os.path.join(OUT, name)) // 1024, "KB")
