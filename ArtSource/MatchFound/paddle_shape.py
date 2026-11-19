"""Shared outline of the PickleVerse "Elongated" paddle (pure Python, used by the texture and mesh builders).

Units: Blender units. Z runs along the paddle (grip hold point at z = 0), X across the face, the face normal is Y.
"""
import math

HALF_WIDTH = 0.50        # head half width
TOP = 2.14               # top of the head
SIDE_TOP = 1.935         # where the top corner meets the straight side
SIDE_BOTTOM = 0.98       # where the side starts tapering into the throat
THROAT_HALF = 0.072      # half width of the throat at the collar
THROAT_Z = 0.44          # throat meets the collar
GUARD_HALF = 0.042       # edge guard half width (in the face plane)
GUARD_DEPTH = 0.060      # edge guard half depth (along the face normal)
CORE_DEPTH = 0.038       # face half thickness
GRIP_BOTTOM, GRIP_TOP = -0.40, 0.37
HEAD_CENTRE = 1.55       # grip -> centre of the hitting area

# Face graphics are mapped isotropically: x in [-0.6, 0.6] -> u in [0, 1], z in [0.36, 2.16] -> top 3/4 of the atlas.
FACE_X0, FACE_X1, FACE_Z0, FACE_Z1 = -0.6, 0.6, 0.36, 2.16
ATLAS_W, ATLAS_H = 1024, 2048
FACE_V0 = 0.25           # face occupies v in [0.25, 1]; the grip strip is v in [0, 0.25]


def _bez(p0, p1, p2, p3, t):
    a = (1 - t) ** 3
    b = 3 * (1 - t) ** 2 * t
    c = 3 * (1 - t) * t ** 2
    d = t ** 3
    return (a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1])


def _segments():
    w, k = HALF_WIDTH, 0.5523 * (HALF_WIDTH - 0.30)
    return [
        ((0.0, TOP), (0.14, TOP), (0.23, TOP - 0.002), (0.30, TOP - 0.006), 6),            # gently crowned top edge
        ((0.30, TOP - 0.006), (0.30 + k, TOP - 0.006), (w, SIDE_TOP + k), (w, SIDE_TOP), 16),  # rounded corner
        ((w, SIDE_TOP), (w, SIDE_TOP - 0.28), (w, SIDE_BOTTOM + 0.28), (w, SIDE_BOTTOM), 7),   # straight side
        ((w, SIDE_BOTTOM), (w, 0.78), (THROAT_HALF + 0.012, 0.71), (THROAT_HALF, THROAT_Z), 22),  # shoulder into the throat
    ]


def right_half():
    """Points from the top centre clockwise down to the throat (right side, x >= 0)."""
    pts = []
    for p0, p1, p2, p3, n in _segments():
        for i in range(n):
            pts.append(_bez(p0, p1, p2, p3, i / n))
    pts.append((THROAT_HALF, THROAT_Z))
    return pts


def open_path():
    """Edge path for the guard: left throat -> up the left side -> over the top -> down to the right throat."""
    right = right_half()
    left = [(-x, z) for x, z in reversed(right[1:])]
    return left + right


def closed_outline():
    return open_path()   # the short bottom edge across the throat closes it (hidden inside the collar)


def normals(path, closed=False):
    out = []
    n = len(path)
    for i in range(n):
        a = path[i - 1] if (i > 0 or closed) else path[i]
        b = path[(i + 1) % n] if (i < n - 1 or closed) else path[i]
        tx, tz = b[0] - a[0], b[1] - a[1]
        length = math.hypot(tx, tz) or 1.0
        # the path runs clockwise seen from the front (x right, z up), so the outward normal is (-tz, tx)
        out.append((-tz / length, tx / length))
    return out


def inset(path, amount):
    ns = normals(path)
    return [(x - nx * amount, z - nz * amount) for (x, z), (nx, nz) in zip(path, ns)]


def face_uv(x, z):
    u = (x - FACE_X0) / (FACE_X1 - FACE_X0)
    v = FACE_V0 + (1 - FACE_V0) * (z - FACE_Z0) / (FACE_Z1 - FACE_Z0)
    return u, v


def face_px(x, z):
    """Atlas pixel (origin top-left) of a face point."""
    u, v = face_uv(x, z)
    return u * ATLAS_W, (1 - v) * ATLAS_H


PX_PER_UNIT = ATLAS_W / (FACE_X1 - FACE_X0)
