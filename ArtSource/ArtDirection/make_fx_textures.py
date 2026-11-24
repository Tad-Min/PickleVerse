"""Atmosphere FX textures (Art Bible §04): soft mote, two-leaf atlas, light-shaft gradient.

usage: python make_fx_textures.py <Assets/Textures/ArtDirection/FX>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def mote(n=64):
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n * 2 - 1
    r = np.sqrt(x * x + y * y)
    a = np.clip(1 - r, 0, 1) ** 2.2
    rgb = np.ones((n, n, 3))
    return Image.fromarray((np.dstack([rgb, a[..., None]]) * 255).astype(np.uint8), "RGBA")


def leaf_shape(n, tilt, curl):
    y, x = (np.mgrid[0:n, 0:n] + 0.5) / n * 2 - 1
    c, s = np.cos(tilt), np.sin(tilt)
    u, v = c * x - s * y, s * x + c * y
    v = v + curl * u * u
    inside = (u / 0.92) ** 2 + (v / (0.42 * (1 - 0.55 * u * u))) ** 2
    a = np.clip((1 - inside) * 6, 0, 1)
    vein = np.exp(-(v / 0.04) ** 2) * (np.abs(u) < 0.85)
    shade = 0.78 + 0.22 * (1 - np.abs(v) / 0.42) - vein * 0.18
    return a, np.clip(shade, 0, 1)


def leaves(n=128):
    """2x1 atlas: a sun-yellow flame-tree leaf and a mid-green broadleaf (tinted further by particle colour)."""
    out = np.zeros((n, n * 2, 4))
    for i, (col, tilt, curl) in enumerate((((0.95, 0.72, 0.30), 0.5, 0.25), ((0.55, 0.72, 0.30), -0.4, -0.2))):
        a, shade = leaf_shape(n, tilt, curl)
        for k in range(3):
            out[:, i * n:(i + 1) * n, k] = col[k] * shade
        out[:, i * n:(i + 1) * n, 3] = a
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")


def shaft(w=64, h=256):
    """Vertical light-shaft card: soft across, fading at both ends (additive)."""
    y, x = (np.mgrid[0:h, 0:w] + 0.5)
    u = x / w * 2 - 1
    v = y / h
    across = np.clip(1 - np.abs(u), 0, 1) ** 1.6
    along = np.clip(v / 0.15, 0, 1) * np.clip((1 - v) / 0.55, 0, 1)
    a = across * along
    return Image.fromarray((np.dstack([np.ones((h, w, 3)), a[..., None]]) * 255).astype(np.uint8), "RGBA")


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    mote().save(out / "T_FX_Mote.png", optimize=True)
    leaves().save(out / "T_FX_Leaves.png", optimize=True)
    shaft().save(out / "T_FX_Shaft.png", optimize=True)
    print(sorted(p.name for p in out.glob("*.png")))


if __name__ == "__main__":
    main()
