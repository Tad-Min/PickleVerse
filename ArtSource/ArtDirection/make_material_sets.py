"""PickleVerse Art Direction — the five standard Stylized PBR material sets (Art Bible §02).

Generates tileable textures for Court, Metal, Wood, Stone and Fabric, plus a world-space detail map:
  T_<Set>_BaseColor.png  value/detail only (hue comes from the palette tint: vertex colour or _BaseColor)
  T_<Set>_Normal.png     tangent-space normal (OpenGL, +Y up), derived from the set's height field
  T_<Set>_Mask.png       R metallic, G ambient occlusion, B edge/detail mask, A smoothness
  T_<Set>_Emission.png   only for sets with emissive parts (Metal: LED panel, Fabric: backlit signage strip)
  T_WorldDetail.png      grey macro variation (0.5 = neutral) for the shader's world-space detail

Rules baked in (Art Bible): low noise (value variation <= +/-6 %), controlled smoothness ranges per set, edges and
pores visible in the mask so the shader can lift edges / darken cavities, every texture tiles seamlessly.

usage: python make_material_sets.py <output Assets/Textures/ArtDirection folder>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

RNG = np.random.default_rng(20261124)


def band_noise(n, lo, hi, seed):
    """Periodic band-limited noise in [-1, 1]: white noise filtered in the frequency domain (tiles seamlessly)."""
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((n, n))
    f = np.fft.fftfreq(n) * n
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt(fx * fx + fy * fy)
    mask = np.exp(-((np.log(np.maximum(r, 1e-3)) - np.log(np.sqrt(lo * hi))) ** 2) / (2 * (np.log(hi / lo) / 2.2) ** 2))
    mask[0, 0] = 0
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * mask))
    return out / (np.abs(out).max() + 1e-9)


def aniso_noise(n, lo, hi, seed, stretch):
    """Band noise stretched along X (grain, brushing)."""
    rng = np.random.default_rng(seed)
    white = rng.standard_normal((n, n))
    f = np.fft.fftfreq(n) * n
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt((fx / stretch) ** 2 + (fy * 1.0) ** 2)
    mask = np.exp(-((np.log(np.maximum(r, 1e-3)) - np.log(np.sqrt(lo * hi))) ** 2) / (2 * (np.log(hi / lo) / 2.2) ** 2))
    mask[0, 0] = 0
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * mask))
    return out / (np.abs(out).max() + 1e-9)


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx * inv, ny * inv, nz * inv], axis=-1)
    return ((n * 0.5 + 0.5) * 255).round().astype(np.uint8)


def cavity(h, radius_noise):
    """AO-like cavity term: how far a texel sits below its neighbourhood (periodic blur)."""
    blur = h.copy()
    for k in (1, 2, 4, 8):
        blur = (blur + np.roll(blur, k, 0) + np.roll(blur, -k, 0) + np.roll(blur, k, 1) + np.roll(blur, -k, 1)) / 5
    return np.clip((blur - h) * radius_noise, 0, 1)


def edges(h, k):
    """Convexity: texels above their neighbourhood (ridges, rims, chips)."""
    blur = h.copy()
    for s in (1, 2, 4):
        blur = (blur + np.roll(blur, s, 0) + np.roll(blur, -s, 0) + np.roll(blur, s, 1) + np.roll(blur, -s, 1)) / 5
    return np.clip((h - blur) * k, 0, 1)


def to8(a):
    return (np.clip(a, 0, 1) * 255).round().astype(np.uint8)


def save_rgb(path, value, warm=0.0):
    rgb = np.stack([value * (1 + warm), value, value * (1 - warm)], axis=-1)
    Image.fromarray(to8(rgb), "RGB").save(path, optimize=True)


def save_mask(path, metal, ao, edge, smooth):
    Image.fromarray(np.stack([to8(metal), to8(ao), to8(edge), to8(smooth)], axis=-1), "RGBA").save(path, optimize=True)


def court(out, n=1024):
    # Textured sport acrylic: silica sand grain + faint roller mottling. Value 0.84 +/- 0.03.
    sand = band_noise(n, 140, 420, 11)
    fine = band_noise(n, 300, 512, 12)
    mottle = band_noise(n, 2, 7, 13)
    h = sand * 0.7 + fine * 0.3
    value = 0.84 + mottle * 0.018 + sand * 0.022
    save_rgb(out / "T_Court_BaseColor.png", value, warm=0.004)
    Image.fromarray(normal_from_height(h, 3.0), "RGB").save(out / "T_Court_Normal.png", optimize=True)
    ao = 1 - cavity(h, 1.6) * 0.35
    speck = np.clip(fine * 1.8 - 0.9, 0, 1)
    smooth = 0.70 + mottle * 0.06 - np.clip(sand, 0, 1) * 0.10
    save_mask(out / "T_Court_Mask.png", np.zeros_like(h), ao, speck, smooth)


def metal(out, n=512):
    # Powder-coated / painted metal: orange-peel, light brushing on bare areas, edge chips in B.
    peel = band_noise(n, 18, 60, 21)
    brush = aniso_noise(n, 30, 200, 22, 10.0)
    chips = band_noise(n, 6, 24, 23)
    h = peel * 0.6 + brush * 0.25
    value = 0.86 + peel * 0.015 + band_noise(n, 2, 6, 24) * 0.02
    save_rgb(out / "T_Metal_BaseColor.png", value)
    Image.fromarray(normal_from_height(h, 1.4), "RGB").save(out / "T_Metal_Normal.png", optimize=True)
    edge = np.clip(chips * 2.2 - 1.2, 0, 1) * 0.8 + edges(h, 6) * 0.2
    ao = 1 - cavity(h, 1.2) * 0.2
    smooth = 0.78 + brush * 0.08 + peel * 0.03
    save_mask(out / "T_Metal_Mask.png", np.ones_like(h), ao, edge, smooth)
    # LED panel emission: 8 x 4 grid of soft rounded diodes (lamp heads, scoreboards).
    y, x = np.mgrid[0:n, 0:n] / n
    gx, gy = (x * 8) % 1 - 0.5, (y * 8) % 1 - 0.5
    led = np.clip(1 - np.sqrt(gx * gx + gy * gy) / 0.33, 0, 1) ** 1.5
    Image.fromarray(to8(np.stack([led, led * 0.96, led * 0.88], -1)), "RGB").save(out / "T_Metal_Emission.png", optimize=True)


def wood(out, n=512):
    # Oiled hardwood: long grain along U, gentle rings, pores in the mask.
    grain = aniso_noise(n, 8, 120, 31, 14.0)
    # Straight, slightly wandering growth lines (flat-sawn boards), not swirls.
    wander = aniso_noise(n, 1, 6, 32, 8.0) * 0.22
    rings = np.sin((np.arange(n)[:, None] / n * 26 + wander) * 2 * np.pi) * 0.5 + 0.5
    rings = rings ** 3
    pores = np.clip(aniso_noise(n, 120, 400, 33, 6.0) * 2 - 1.1, 0, 1)
    h = grain * 0.6 + rings * 0.25 - pores * 0.4
    value = 0.80 + grain * 0.05 + (rings - 0.5) * 0.05 - pores * 0.05
    save_rgb(out / "T_Wood_BaseColor.png", value, warm=0.03)
    Image.fromarray(normal_from_height(h, 1.8), "RGB").save(out / "T_Wood_Normal.png", optimize=True)
    ao = 1 - pores * 0.25 - cavity(h, 1.0) * 0.15
    smooth = 0.62 + grain * 0.06 - pores * 0.25
    save_mask(out / "T_Wood_Mask.png", np.zeros_like(h), ao, edges(h, 5), smooth)


def stone(out, n=1024):
    # Concrete / paving: aggregate speckle, shallow pits, soft mottling; mostly rough.
    mottle = band_noise(n, 2, 8, 41)
    agg = band_noise(n, 60, 200, 42)
    pits = np.clip(band_noise(n, 80, 260, 43) * 2.4 - 1.6, 0, 1)
    h = agg * 0.5 + mottle * 0.3 - pits * 0.6
    value = 0.80 + mottle * 0.04 + agg * 0.025 - pits * 0.08
    save_rgb(out / "T_Stone_BaseColor.png", value, warm=0.01)
    Image.fromarray(normal_from_height(h, 2.2), "RGB").save(out / "T_Stone_Normal.png", optimize=True)
    ao = 1 - pits * 0.45 - cavity(h, 1.4) * 0.2
    smooth = 0.32 + mottle * 0.05 - pits * 0.2
    save_mask(out / "T_Stone_Mask.png", np.zeros_like(h), ao, edges(h, 4), smooth)


def fabric(out, n=512):
    # Woven fabric / polymer: plain weave (banners, seat mesh, net tape), satin sheen in smoothness.
    y, x = np.mgrid[0:n, 0:n] / n
    k = 64
    warp = np.sin(x * k * 2 * np.pi) * 0.5 + 0.5
    weft = np.sin(y * k * 2 * np.pi) * 0.5 + 0.5
    checker = np.sign(np.sin(x * k * np.pi) * np.sin(y * k * np.pi))
    weave = np.where(checker > 0, warp, weft)
    fuzz = band_noise(n, 80, 256, 51)
    h = weave * 0.8 + fuzz * 0.2
    value = 0.86 + (weave - 0.5) * 0.06 + fuzz * 0.02
    save_rgb(out / "T_Fabric_BaseColor.png", value)
    Image.fromarray(normal_from_height(h, 1.2), "RGB").save(out / "T_Fabric_Normal.png", optimize=True)
    ao = 1 - (1 - weave) * 0.18
    smooth = 0.45 + weave * 0.15 + fuzz * 0.03
    save_mask(out / "T_Fabric_Mask.png", np.zeros_like(h), ao, edges(h, 3) * 0.5, smooth)
    # Backlit signage strip: soft horizontal light band (stadium boards at night).
    band = np.exp(-((y - 0.5) / 0.18) ** 2)
    Image.fromarray(to8(np.stack([band, band * 0.94, band * 0.84], -1)), "RGB").save(out / "T_Fabric_Emission.png", optimize=True)


def world_detail(out, n=512):
    d = band_noise(n, 2, 10, 61) * 0.6 + band_noise(n, 12, 40, 62) * 0.4
    Image.fromarray(to8(0.5 + d * 0.22), "L").save(out / "T_WorldDetail.png", optimize=True)


def main():
    root = Path(sys.argv[1])
    for name, fn in (("Court", court), ("Metal", metal), ("Wood", wood), ("Stone", stone), ("Fabric", fabric)):
        folder = root / name
        folder.mkdir(parents=True, exist_ok=True)
        fn(folder)
        print(name, sorted(p.name for p in folder.glob("*.png")))
    world_detail(root)
    print("T_WorldDetail.png")


if __name__ == "__main__":
    main()
