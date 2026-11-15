"""Golden-hour urban park around the PickleVerse match court.

    blender --background --python build_district.py

Plan frame: x = Unity x, y = Unity z (court long axis), z = up. Ground level of the district is z = 0 and the
Unity installer offsets it per venue. Nothing is placed inside the venue plot (|x| < 14, |y| < 19.5), so the
playing area, net, lines and every gameplay object stay exactly as they are.

Outputs
  SunsetDistrict.blend                       authoring source (this folder)
  Models/Stadium_Enhanced/District/SunsetDistrict.glb   one mesh per material (few draw calls, no colliders)
  district_layout.json                       tree / lamp instances for the Unity installer and the shadow bake
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector

from sd_common import *   # noqa: F401,F403

R = rng(20261115)
LAYOUT = {"trees": [], "lamps": [], "notes": "x,y plan metres (Unity x,z); yaw degrees about Unity Y"}

# ----------------------------------------------------------------------------- palette (sRGB authored)
C_GRASS = srgb(0.86, 0.90, 0.70)
C_GRASS_DRY = srgb(0.98, 0.90, 0.62)
C_GRASS_LUSH = srgb(0.70, 0.86, 0.60)
C_ASPHALT = srgb(0.95, 0.95, 0.97)
C_PAVE_WARM = srgb(1.0, 0.93, 0.84)
C_PAVE_COOL = srgb(0.86, 0.86, 0.88)
C_PAVE_RED = srgb(0.86, 0.60, 0.50)
C_CURB = srgb(0.93, 0.92, 0.90)
C_CONCRETE = srgb(0.92, 0.90, 0.88)
C_LINE_WHITE = srgb(0.95, 0.95, 0.93)
C_LINE_YELLOW = srgb(0.95, 0.74, 0.20)
C_POST = srgb(0.16, 0.18, 0.19)
C_POWDER_GREEN = srgb(0.16, 0.30, 0.25)
C_BLACK = srgb(0.07, 0.07, 0.08)
C_WHITE = srgb(0.93, 0.93, 0.91)
C_STEEL = srgb(0.70, 0.72, 0.74)
C_BRONZE = srgb(0.40, 0.30, 0.20)
C_WOOD = srgb(0.95, 0.88, 0.80)
C_WATER = srgb(0.34, 0.47, 0.52)
C_LENS = srgb(1.0, 0.86, 0.62)
COURT_SCHEMES = [   # (court, kitchen, apron)
    (srgb(0.20, 0.42, 0.62), srgb(0.30, 0.52, 0.70), srgb(0.24, 0.43, 0.33)),
    (srgb(0.42, 0.30, 0.56), srgb(0.50, 0.38, 0.62), srgb(0.20, 0.40, 0.46)),
    (srgb(0.18, 0.45, 0.47), srgb(0.27, 0.55, 0.55), srgb(0.30, 0.34, 0.46)),
    (srgb(0.62, 0.36, 0.22), srgb(0.70, 0.45, 0.28), srgb(0.26, 0.42, 0.34)),
]

# ----------------------------------------------------------------------------- footprint
HOLE = (14.0, 19.5)                  # venue plot: never built on
PLAZA = (19.0, 25.0, 5.0)            # rounded plaza around the venue
ROAD = (56.0, 66.0, 18.0)            # ring road centreline (half x, half y, corner radius)
ROAD_W = 10.0
WALK_IN, WALK_OUT = 3.5, 4.0
PARK = (ROAD[0] - ROAD_W / 2 - WALK_IN, ROAD[1] - ROAD_W / 2 - WALK_IN)
Z_GRASS, Z_PAVE, Z_ROAD, Z_WALK = 0.0, 0.025, -0.13, 0.04

M = {}   # material name -> Mesh


def mesh(mat):
    if mat not in M:
        M[mat] = Mesh("District_" + mat.replace("SD_", ""))
    return M[mat]


def inside_rounded(x, y, hx, hy, r, pad=0.0):
    hx, hy, r = hx + pad, hy + pad, max(0.0, r + pad)
    dx, dy = max(abs(x) - (hx - r), 0.0), max(abs(y) - (hy - r), 0.0)
    return math.hypot(dx, dy) <= r and abs(x) <= hx and abs(y) <= hy


def ring_line(hx, hy, r, steps=10):
    return rounded_rect(0, 0, hx, hy, r, steps)


# ----------------------------------------------------------------------------- ground
def grass_ground():
    """Mottled lawn grid inside the ring road. Seams hide under paths/sidewalks."""
    g = mesh("SD_Grass")
    cell = 3.0
    hx, hy = PARK[0] + 1.0, PARK[1] + 1.0
    nx, ny = int(math.ceil(hx * 2 / cell)), int(math.ceil(hy * 2 / cell))
    mottle_a = [(R.uniform(-hx, hx), R.uniform(-hy, hy), R.uniform(6, 14)) for _ in range(28)]

    def tint(x, y):
        t = 0.5
        for (mx, my, mr) in mottle_a:
            d = math.hypot(x - mx, y - my) / mr
            if d < 1:
                t += (0.5 - (mx * 7 + my * 3) % 1.0) * (1 - d) ** 2 * 0.9
        t = max(0.0, min(1.0, t))
        c = vlerp(C_GRASS_LUSH, C_GRASS_DRY, t)
        # worn, sun-bleached edges along the plaza
        near = 1 - min(1.0, max(0.0, (max(abs(x) - PLAZA[0], abs(y) - PLAZA[1]) ) / 3.0))
        return vlerp(c, C_GRASS_DRY, near * 0.5)

    for i in range(nx):
        for j in range(ny):
            x0, y0 = -hx + i * cell, -hy + j * cell
            x1, y1 = x0 + cell, y0 + cell
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            if not inside_rounded(cx, cy, PARK[0], PARK[1], ROAD[2] - ROAD_W / 2 - WALK_IN, pad=1.5):
                continue
            if abs(cx) < PLAZA[0] - 1.5 and abs(cy) < PLAZA[1] - 1.5:
                continue
            pts = [(x0, y0, Z_GRASS), (x1, y0, Z_GRASS), (x1, y1, Z_GRASS), (x0, y1, Z_GRASS)]
            g.poly(pts, [(p[0] / 3.0, p[1] / 3.0) for p in pts], mat="SD_Grass", cols=[tint(p[0], p[1]) for p in pts], smooth=True)


def plaza():
    """Warm paver plaza ring around the venue with a sweeping inlaid band and a soft concrete kerb."""
    p = mesh("SD_Paving")
    # Both outlines come from rounded_rect with the same step count, so their points pair up one to one
    # (straight sides become single long quads, corners become fans) and the ribbon has no gaps.
    outs = rounded_rect(0, 0, PLAZA[0], PLAZA[1], PLAZA[2], 8)
    ins = rounded_rect(0, 0, HOLE[0], HOLE[1], 0.02, 8)
    p.strip_xy(ins, outs, Z_PAVE, "SD_Paving", C_PAVE_WARM, uv_scale=2.4, closed=True)
    # Plot under the venue (covered by the venue's own paving where it has one).
    p.quad_xy(-HOLE[0], -HOLE[1], HOLE[0], HOLE[1], -0.03, "SD_Paving", shade(C_PAVE_WARM, 0.9), uv_scale=2.4)
    # Concrete kerb with a rounded top
    k = mesh("SD_Concrete")
    kin = offset_polyline(outs, 0.0, closed=True)
    kout = offset_polyline(outs, -0.22, closed=True)
    k.strip_xy(kin, kout, Z_PAVE + 0.012, "SD_Concrete", C_CURB, uv_scale=2.0, closed=True)
    k.wall(kout, Z_GRASS - 0.05, Z_PAVE + 0.012, "SD_Concrete", shade(C_CURB, 0.8), uv_scale=2.0, closed=True, col_top=C_CURB)


def path(points, width, z=Z_PAVE + 0.004, colour=C_PAVE_COOL, mat="SD_Paving", edge=True, per=10):
    line = catmull(points, per)
    line = [(p[0], p[1]) for p in line]
    left = offset_polyline(line, width / 2)
    right = offset_polyline(line, -width / 2)
    mesh(mat).strip_xy(left, right, z, mat, colour, uv_along=2.4, v_scale=2.4)
    if edge:
        for side in (1, -1):
            a = offset_polyline(line, side * width / 2)
            b = offset_polyline(line, side * (width / 2 + 0.16))
            l, r_ = (b, a) if side > 0 else (a, b)
            mesh("SD_Concrete").strip_xy(l, r_, z + 0.01, "SD_Concrete", C_CURB, uv_along=2.0)
    return line


# ----------------------------------------------------------------------------- roads
def ring_road():
    centre = ring_line(*ROAD, steps=14)
    hx, hy, r = ROAD
    a = mesh("SD_Asphalt")
    road_out = offset_polyline(centre, -ROAD_W / 2, closed=True)    # rounded rect is CCW: left = inside
    road_in = offset_polyline(centre, ROAD_W / 2, closed=True)
    a.strip_xy(road_in, road_out, Z_ROAD, "SD_Asphalt", C_ASPHALT, uv_scale=4.0, closed=True)
    # sidewalks + kerbs on both sides
    walk_in_edge = offset_polyline(centre, ROAD_W / 2 + WALK_IN, closed=True)
    walk_out_edge = offset_polyline(centre, -ROAD_W / 2 - WALK_OUT, closed=True)
    pv = mesh("SD_Paving")
    pv.strip_xy(walk_in_edge, road_in, Z_WALK, "SD_Paving", C_PAVE_COOL, uv_scale=2.4, closed=True)
    pv.strip_xy(road_out, walk_out_edge, Z_WALK, "SD_Paving", C_PAVE_COOL, uv_scale=2.4, closed=True)
    k = mesh("SD_Concrete")
    for edge, flip in ((road_in, False), (road_out, True)):
        k.wall(edge, Z_ROAD, Z_WALK, "SD_Concrete", shade(C_CURB, 0.75), uv_scale=1.0, closed=True, col_top=C_CURB, flip=flip)
    # park-side lip so the lawn meets the walk cleanly
    k.wall(walk_in_edge, Z_GRASS - 0.05, Z_WALK, "SD_Concrete", shade(C_CURB, 0.7), uv_scale=1.0, closed=True, col_top=C_CURB, flip=True)
    # lane markings: dashed centre line, solid edge lines
    paint = mesh("SD_Paint")
    for s in resample(centre, 6.0, closed=True):
        x, y, ang = s
        ca, sa = math.cos(ang), math.sin(ang)
        hl, hw = 1.5, 0.075
        pts = [(x - ca * hl + sa * hw, y - sa * hl - ca * hw, Z_ROAD + 0.006), (x + ca * hl + sa * hw, y + sa * hl - ca * hw, Z_ROAD + 0.006),
               (x + ca * hl - sa * hw, y + sa * hl + ca * hw, Z_ROAD + 0.006), (x - ca * hl - sa * hw, y - sa * hl + ca * hw, Z_ROAD + 0.006)]
        paint.poly(pts, None, C_LINE_YELLOW, "SD_Paint", smooth=False)
    for off in (ROAD_W / 2 - 0.45, -(ROAD_W / 2 - 0.45)):
        e0 = offset_polyline(centre, off + 0.06, closed=True)
        e1 = offset_polyline(centre, off - 0.06, closed=True)
        paint.strip_xy(e0, e1, Z_ROAD + 0.006, "SD_Paint", C_LINE_WHITE, uv_scale=1.0, closed=True)
    # zebra crossings where the park paths meet the road
    for (x, y, vertical) in ((0, hy, True), (0, -hy, True), (hx, 0, False), (-hx, 0, False)):
        for i in range(-4, 5):
            if vertical:
                paint.quad_xy(x + i * 1.0 - 0.28, y - ROAD_W / 2 + 0.7, x + i * 1.0 + 0.28, y + ROAD_W / 2 - 0.7, Z_ROAD + 0.007, "SD_Paint", C_LINE_WHITE)
            else:
                paint.quad_xy(x - ROAD_W / 2 + 0.7, y + i * 1.0 - 0.28, x + ROAD_W / 2 - 0.7, y + i * 1.0 + 0.28, Z_ROAD + 0.007, "SD_Paint", C_LINE_WHITE)
    # outer city ground
    city_in = walk_out_edge
    city_out = offset_polyline(ring_line(*ROAD, steps=14), -(ROAD_W / 2 + WALK_OUT + 62), closed=True)
    mesh("SD_Concrete").strip_xy(city_in, city_out, Z_WALK - 0.01, "SD_Concrete", shade(C_CONCRETE, 0.92), uv_scale=3.0, closed=True)
    return centre, walk_in_edge, walk_out_edge


# ----------------------------------------------------------------------------- courts
def court_lines(m, cx, cy, z, colour=C_LINE_WHITE):
    """Regulation pickleball lines (5 cm) as thin raised quads."""
    hw, hl, k, w = 3.048, 6.7056, 2.1336, 0.05
    q = lambda x0, y0, x1, y1: m.quad_xy(cx + x0, cy + y0, cx + x1, cy + y1, z, "SD_Acrylic", colour)
    q(-hw, -hl, hw, -hl + w); q(-hw, hl - w, hw, hl)
    q(-hw, -hl, -hw + w, hl); q(hw - w, -hl, hw, hl)
    q(-hw, -k - w / 2, hw, -k + w / 2); q(-hw, k - w / 2, hw, k + w / 2)
    q(-w / 2, -hl, w / 2, -k); q(-w / 2, k, w / 2, hl)


def net(cx, cy):
    """Posts with rounded caps, a black knotted net and a white tape. Net plane along x."""
    span = 3.3528 + 0.15
    metal, paint = mesh("SD_Metal"), mesh("SD_Paint")
    for s in (-1, 1):
        x = cx + s * span
        metal.cylinder(x, cy, Z_PAVE, 0.92, 0.038, 10, "SD_Metal", C_POST)
        metal.tube([(x, cy, 0.92), (x, cy, 0.95)], [0.038, 0.0], 10, "SD_Metal", C_POST, cap_end=False)
        metal.box(x - 0.18, cy - 0.04, Z_PAVE, x + 0.18, cy + 0.04, Z_PAVE + 0.05, "SD_Metal", C_POST)
    nm = mesh("SD_Net")
    sag = 0.05
    cols = 12
    for i in range(cols):
        x0 = cx - span + 2 * span * i / cols
        x1 = cx - span + 2 * span * (i + 1) / cols
        t0, t1 = (x0 - cx) / span, (x1 - cx) / span
        top0 = 0.914 - sag * (1 - t0 * t0)
        top1 = 0.914 - sag * (1 - t1 * t1)
        pts = [(x0, cy, 0.06), (x1, cy, 0.06), (x1, cy, top1 - 0.03), (x0, cy, top0 - 0.03)]
        nm.poly(pts, [(p[0] / 0.30, p[2] / 0.30) for p in pts], C_WHITE, "SD_Net", smooth=False)
        tape = [(x0, cy - 0.006, top0 - 0.035), (x1, cy - 0.006, top1 - 0.035), (x1, cy - 0.006, top1 + 0.02), (x0, cy - 0.006, top0 + 0.02)]
        paint.poly(tape, None, C_WHITE, "SD_Paint", smooth=False)
        paint.poly([tape[1], tape[0], tape[3], tape[2]], None, C_WHITE, "SD_Paint", smooth=False)


def fence(points, height, closed=False, gate=None, post_spacing=3.0):
    """Powder-coated chain-link fence: round posts with domed caps, top + bottom rails, alpha-tested mesh."""
    metal, chain = mesh("SD_Metal"), mesh("SD_ChainLink")
    segs = []
    n = len(points)
    for i in range(n if closed else n - 1):
        a, b = points[i], points[(i + 1) % n]
        segs.append((a, b))
    for (a, b) in segs:
        length = math.hypot(b[0] - a[0], b[1] - a[1])
        count = max(1, int(round(length / post_spacing)))
        for k in range(count):
            t0, t1 = k / count, (k + 1) / count
            p0 = (lerp(a[0], b[0], t0), lerp(a[1], b[1], t0))
            p1 = (lerp(a[0], b[0], t1), lerp(a[1], b[1], t1))
            mid = ((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2)
            if gate and math.hypot(mid[0] - gate[0], mid[1] - gate[1]) < gate[2]:
                continue
            seg = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
            pts = [(p0[0], p0[1], Z_PAVE + 0.05), (p1[0], p1[1], Z_PAVE + 0.05), (p1[0], p1[1], height - 0.04), (p0[0], p0[1], height - 0.04)]
            uvs = [(0, 0.05 / 0.48), (seg / 0.48, 0.05 / 0.48), (seg / 0.48, (height - 0.04) / 0.48), (0, (height - 0.04) / 0.48)]
            chain.poly(pts, uvs, C_POWDER_GREEN, "SD_ChainLink", smooth=False)
            for z in (height - 0.03, 0.12):
                metal.tube([(p0[0], p0[1], z), (p1[0], p1[1], z)], [0.021, 0.021], 6, "SD_Metal", C_POWDER_GREEN, cap_end=False)
        for k in range(count + 1):
            t = k / count
            x, y = lerp(a[0], b[0], t), lerp(a[1], b[1], t)
            metal.cylinder(x, y, Z_PAVE, height, 0.036, 8, "SD_Metal", C_POWDER_GREEN, col_bottom=shade(C_POWDER_GREEN, 0.6), cap=False)
            metal.tube([(x, y, height), (x, y, height + 0.035), (x, y, height + 0.055)], [0.042, 0.034, 0.0], 8, "SD_Metal", C_POWDER_GREEN, cap_end=False)


def court_complex(side, schemes):
    """Two pickleball courts on a shared acrylic pad, fenced, with benches between them."""
    s = 1 if side > 0 else -1
    x0, x1 = 23.0 * s, 43.0 * s
    lo, hi = min(x0, x1), max(x0, x1)
    ac = mesh("SD_Acrylic")
    centres = [33.0 * s - 4.9, 33.0 * s + 4.9]
    apron = schemes[0][2]
    # concrete slab edge, then the acrylic apron
    con = mesh("SD_Concrete")
    con.box(lo - 0.3, -10.8, Z_GRASS - 0.05, hi + 0.3, 10.8, Z_PAVE, "SD_Concrete", C_CURB, uv_scale=2.0, top=False)
    ac.quad_xy(lo - 0.3, -10.8, hi + 0.3, 10.8, Z_PAVE, "SD_Acrylic", shade(C_CURB, 0.95), uv_scale=3.0)
    ac.quad_xy(lo, -10.5, hi, 10.5, Z_PAVE + 0.003, "SD_Acrylic", apron, uv_scale=3.0)
    for cx, (court, kitchen, _) in zip(centres, schemes):
        ac.quad_xy(cx - 3.048, -6.7056, cx + 3.048, 6.7056, Z_PAVE + 0.006, "SD_Acrylic", court, uv_scale=3.0)
        ac.quad_xy(cx - 3.048, -2.1336, cx + 3.048, 2.1336, Z_PAVE + 0.008, "SD_Acrylic", kitchen, uv_scale=3.0)
        court_lines(ac, cx, 0.0, Z_PAVE + 0.010)
        net(cx, 0.0)
    corners = [(lo, -10.5), (hi, -10.5), (hi, 10.5), (lo, 10.5)]
    fence(corners, 3.0, closed=True, gate=(23.0 * s, 0.0, 1.6))
    # divider fence (low) between the two courts
    fence([(33.0 * s, -10.5), (33.0 * s, 10.5)], 1.05, post_spacing=3.5)
    for y in (-8.5, 8.5):
        bench(33.0 * s + 1.0 * s, y, 0.0 if s > 0 else math.pi, length=1.6)


# ----------------------------------------------------------------------------- street furniture
def bench(x, y, yaw, length=1.9):
    """Park bench: two cast-iron S-scroll end frames, armrest curls and six oiled hardwood slats."""
    wood, metal = mesh("SD_Wood"), mesh("SD_Metal")
    ca, sa = math.cos(yaw), math.sin(yaw)

    def T(u, v, z):   # u along the bench, v towards the back
        return (x + u * ca - v * sa, y + u * sa + v * ca, z)

    for u in (-length / 2 + 0.12, length / 2 - 0.12):
        # front leg sweeping into the seat rail and up into a reclined, scrolled back support
        prof = [(0.22, 0.02), (0.20, 0.25), (0.14, 0.42), (-0.02, 0.45), (-0.20, 0.46), (-0.26, 0.62), (-0.30, 0.86), (-0.24, 0.92), (-0.20, 0.86)]
        pts = [T(u, v, z) for v, z in catmull([(v, z) for v, z in prof], 2)]
        metal.tube(pts, [0.024] * len(pts), 5, "SD_Metal", C_BLACK, cap_end=True, cap_start=True)
        rear = [(-0.24, 0.02), (-0.20, 0.22), (-0.12, 0.44)]
        pts = [T(u, v, z) for v, z in catmull(rear, 2)]
        metal.tube(pts, [0.022] * len(pts), 4, "SD_Metal", C_BLACK, cap_end=True, cap_start=True)
        arm = [(0.20, 0.45), (0.22, 0.62), (0.10, 0.66), (-0.10, 0.64), (-0.18, 0.62)]
        curl = [(0.22 + 0.05 * math.cos(a), 0.62 + 0.05 * math.sin(a)) for a in (0.0, 1.2, 2.4, 3.6)]
        pts = [T(u + (0.02 if u > 0 else -0.02), v, z) for v, z in catmull(curl + arm[1:], 2)]
        metal.tube(pts, [0.016] * len(pts), 4, "SD_Metal", C_BLACK, cap_end=True, cap_start=True)
    seat = [(0.18, 0.455), (0.10, 0.462), (0.02, 0.466), (-0.06, 0.468), (-0.14, 0.47)]
    back = [(-0.23, 0.58), (-0.26, 0.70), (-0.29, 0.82)]
    for v, z in seat + back:
        p0, p1 = T(-length / 2, v - 0.033, z), T(length / 2, v - 0.033, z)
        p2, p3 = T(length / 2, v + 0.033, z), T(-length / 2, v + 0.033, z)
        wood.poly([(p0[0], p0[1], z + 0.012), (p1[0], p1[1], z + 0.012), (p2[0], p2[1], z + 0.012), (p3[0], p3[1], z + 0.012)],
                  [(0, 0), (length / 1.2, 0), (length / 1.2, 0.06), (0, 0.06)], C_WOOD, "SD_Wood", smooth=False)
        wood.poly([(p0[0], p0[1], z - 0.012), (p1[0], p1[1], z - 0.012), (p1[0], p1[1], z + 0.012), (p0[0], p0[1], z + 0.012)],
                  [(0, 0), (length / 1.2, 0), (length / 1.2, 0.02), (0, 0.02)], shade(C_WOOD, 0.7), "SD_Wood", smooth=False)


def street_lamp(x, y, yaw, height=6.5, ornate=False):
    """Tapered octagonal pole on a fluted base; a swan-neck arm curls into a lantern with a warm lens."""
    metal, glow = mesh("SD_Metal"), mesh("SD_Glow")
    col = C_BRONZE if ornate else srgb(0.20, 0.22, 0.24)
    metal.tube([(x, y, Z_WALK), (x, y, 0.45), (x, y, 0.55), (x, y, height)], [0.16, 0.12, 0.085, 0.055], 8, "SD_Metal", col, cap_end=False)
    metal.tube([(x, y, 0.42), (x, y, 0.50), (x, y, 0.58)], [0.12, 0.15, 0.09], 8, "SD_Metal", col, cap_end=False)
    ca, sa = math.cos(yaw), math.sin(yaw)
    reach = 1.35 if not ornate else 0.0
    if ornate:
        # park lantern: post top with a scrolled crown and a lantern on top
        for k in range(4):
            a = yaw + k * math.pi / 2
            c, s = math.cos(a), math.sin(a)
            scroll = [(x + c * r, y + s * r, height - 0.75 + z) for r, z in ((0.05, 0.0), (0.18, 0.12), (0.25, 0.32), (0.18, 0.44), (0.10, 0.38), (0.14, 0.30))]
            metal.tube(scroll, [0.018] * len(scroll), 5, "SD_Metal", col, cap_end=True, cap_start=True)
        hx, hy, hz = x, y, height
    else:
        arm = catmull([(x, y, height - 0.2), (x + ca * 0.2, y + sa * 0.2, height + 0.35), (x + ca * 0.75, y + sa * 0.75, height + 0.55),
                       (x + ca * reach, y + sa * reach, height + 0.35), (x + ca * reach, y + sa * reach, height + 0.12)], 3)
        metal.tube(arm, [lerp(0.05, 0.03, i / (len(arm) - 1)) for i in range(len(arm))], 5, "SD_Metal", col, cap_end=True)
        # decorative curl under the arm
        curl = [(x + ca * (0.08 + 0.22 * t), y + sa * (0.08 + 0.22 * t), height - 0.05 + 0.25 * math.sin(t * math.pi)) for t in (0.0, 0.25, 0.5, 0.75, 1.0)]
        metal.tube(curl, [0.014] * 5, 5, "SD_Metal", col, cap_end=True)
        hx, hy, hz = x + ca * reach, y + sa * reach, height + 0.12
    # lantern: hood + glowing lens
    metal.tube([(hx, hy, hz + 0.02), (hx, hy, hz - 0.08), (hx, hy, hz - 0.12)], [0.03, 0.22, 0.24], 10, "SD_Metal", col, cap_end=False, cap_start=False)
    glow.tube([(hx, hy, hz - 0.12), (hx, hy, hz - 0.30)], [0.20, 0.13], 10, "SD_Glow", C_LENS, cap_end=True)
    LAYOUT["lamps"].append({"x": round(hx, 3), "y": round(hy, 3), "z": round(hz - 0.25, 3)})


def pergola(cx, cy, yaw, length=9.0, width=3.4):
    """Arched pergola: paired semicircular steel ribs, timber purlins and climbing-plant masses (foliage cards)."""
    metal, wood = mesh("SD_Metal"), mesh("SD_Wood")
    ca, sa = math.cos(yaw), math.sin(yaw)

    def T(u, v, z):
        return (cx + u * ca - v * sa, cy + u * sa + v * ca, z)

    ribs = 6
    for i in range(ribs):
        u = -length / 2 + length * i / (ribs - 1)
        arch = [T(u, -width / 2 * math.cos(t), 2.1 + 1.1 * math.sin(t)) for t in [k * math.pi / 12 for k in range(13)]]
        legs = [T(u, -width / 2, Z_PAVE)] + arch + [T(u, width / 2, Z_PAVE)]
        metal.tube(legs, [0.05] * len(legs), 6, "SD_Metal", C_POWDER_GREEN, cap_end=True, cap_start=True)
    for k in range(9):
        t = k * math.pi / 8
        v, z = -width / 2 * math.cos(t), 2.1 + 1.1 * math.sin(t)
        a, b = T(-length / 2 - 0.3, v, z + 0.06), T(length / 2 + 0.3, v, z + 0.06)
        wood.tube([a, b], [0.045, 0.045], 4, "SD_Wood", C_WOOD, cap_end=True, cap_start=True)
    for i in range(14):   # climbing jasmine draped over the arches (foliage cards, merged with the hedges)
        u = -length / 2 + length * (i + 0.5) / 14
        side = -1 if i % 2 else 1
        x, y, z = T(u, side * width * 0.33, 0)
        LAYOUT.setdefault("shrubs", []).append({"kind": "Blossom" if i % 3 == 0 else "Shrub", "x": round(x, 3), "y": round(y, 3), "z": 2.75,
                                                 "yaw": round(R.uniform(0, 360), 1), "scale": 1.05})
    for v in (-width / 2 - 0.3, width / 2 + 0.3):
        bench(*T(0, v * 0.55, 0)[:2], yaw + (math.pi if v > 0 else 0.0), length=2.2)


def pond(cx, cy, rx, ry):
    """Kidney-shaped pond: dark still water inset in a curved stone rim."""
    outline = []
    for k in range(48):
        a = 2 * math.pi * k / 48
        wob = 1 + 0.12 * math.sin(2 * a + 0.6) + 0.07 * math.sin(3 * a + 1.3)
        outline.append((cx + math.cos(a) * rx * wob, cy + math.sin(a) * ry * wob))
    water = mesh("SD_Water")
    water.fan_xy(outline, Z_PAVE, "SD_Water", C_WATER, uv_scale=6.0, centre=(cx, cy))
    rim_in = outline
    rim_out = offset_polyline(outline, -0.55, closed=True)
    con = mesh("SD_Concrete")
    con.strip_xy(rim_in, rim_out, Z_PAVE + 0.09, "SD_Concrete", C_PAVE_WARM, uv_scale=2.0, closed=True)
    con.wall(rim_in, Z_PAVE, Z_PAVE + 0.09, "SD_Concrete", shade(C_CURB, 0.55), uv_scale=1.0, closed=True, col_top=C_CURB, flip=True)
    con.wall(rim_out, Z_GRASS - 0.04, Z_PAVE + 0.09, "SD_Concrete", shade(C_CURB, 0.75), uv_scale=1.0, closed=True, col_top=C_CURB)


def fountain(cx, cy):
    """Round plaza with a two-tier basin."""
    pv = mesh("SD_Paving")
    pv.disc(cx, cy, Z_PAVE + 0.006, 7.5, 40, "SD_Paving", C_PAVE_RED, uv_scale=2.4)
    con, water, metal = mesh("SD_Concrete"), mesh("SD_Water"), mesh("SD_Metal")
    rim = arc(cx, cy, 3.2, 0, 2 * math.pi, 40)[:-1]
    rim2 = arc(cx, cy, 3.6, 0, 2 * math.pi, 40)[:-1]
    con.wall(rim2, Z_PAVE, 0.45, "SD_Concrete", shade(C_CURB, 0.8), uv_scale=1.0, closed=True, col_top=C_CURB)
    con.wall(rim, 0.15, 0.45, "SD_Concrete", shade(C_CURB, 0.6), uv_scale=1.0, closed=True, col_top=C_CURB, flip=True)
    con.strip_xy(rim, rim2, 0.45, "SD_Concrete", C_CURB, uv_scale=1.0, closed=True)
    water.disc(cx, cy, 0.32, 3.2, 40, "SD_Water", C_WATER, uv_scale=6.0)
    con.tube([(cx, cy, 0.3), (cx, cy, 0.8), (cx, cy, 1.05), (cx, cy, 1.15)], [0.35, 0.22, 0.5, 0.95], 16, "SD_Concrete", C_CURB, cap_end=False)
    water.disc(cx, cy, 1.10, 0.85, 16, "SD_Water", C_WATER, uv_scale=6.0)
    metal.tube([(cx, cy, 1.1), (cx, cy, 1.55), (cx, cy, 1.75)], [0.12, 0.06, 0.0], 10, "SD_Metal", C_BRONZE, cap_end=False)


def kiosk(cx, cy, yaw):
    """Small café pavilion with a barrel-vault roof, a striped awning and glazed front."""
    paint, fac, metal = mesh("SD_Paint"), mesh("SD_Facade"), mesh("SD_Metal")
    w, d, h = 6.0, 4.0, 2.8
    ca, sa = math.cos(yaw), math.sin(yaw)

    def T(u, v, z):
        return (cx + u * ca - v * sa, cy + u * sa + v * ca, z)

    corners = [T(-w / 2, -d / 2, 0), T(w / 2, -d / 2, 0), T(w / 2, d / 2, 0), T(-w / 2, d / 2, 0)]
    pad = [T(-w / 2 - 1.0, -d / 2 - 4.6, 0), T(w / 2 + 1.0, -d / 2 - 4.6, 0), T(w / 2 + 1.0, d / 2 + 1.0, 0), T(-w / 2 - 1.0, d / 2 + 1.0, 0)]
    mesh("SD_Paving").poly([(q[0], q[1], Z_PAVE + 0.006) for q in pad], [(q[0] / 2.4, q[1] / 2.4) for q in pad], C_PAVE_RED, "SD_Paving", smooth=False)
    fac.wall([c[:2] for c in corners], Z_PAVE, h, "SD_Facade", srgb(0.98, 0.94, 0.88), uv_scale=8.0, closed=True)
    steps = 10
    for k in range(steps):
        t0, t1 = math.pi * k / steps, math.pi * (k + 1) / steps
        v0, z0 = -d / 2 * 1.08 * math.cos(t0), h + 0.9 * math.sin(t0)
        v1, z1 = -d / 2 * 1.08 * math.cos(t1), h + 0.9 * math.sin(t1)
        pts = [T(-w / 2 - 0.2, v0, z0), T(-w / 2 - 0.2, v1, z1), T(w / 2 + 0.2, v1, z1), T(w / 2 + 0.2, v0, z0)]
        paint.poly(pts[::-1], None, srgb(0.42, 0.55, 0.52), "SD_Paint", smooth=True)
    for sgn in (-1, 1):
        cap = [T(sgn * (w / 2 + 0.2), -d / 2 * 1.08 * math.cos(math.pi * k / steps), h + 0.9 * math.sin(math.pi * k / steps)) for k in range(steps + 1)]
        paint.poly(cap[::-1] if sgn > 0 else cap, None, srgb(0.36, 0.48, 0.46), "SD_Paint", smooth=False)
    for k in range(8):   # striped awning
        u0, u1 = -w / 2 + w * k / 8, -w / 2 + w * (k + 1) / 8
        c = srgb(0.86, 0.36, 0.26) if k % 2 == 0 else C_WHITE
        pts = [T(u0, -d / 2, h - 0.15), T(u1, -d / 2, h - 0.15), T(u1, -d / 2 - 1.4, h - 0.65), T(u0, -d / 2 - 1.4, h - 0.65)]
        paint.poly(pts[::-1], None, c, "SD_Paint", smooth=False)
        paint.poly(pts, None, shade(c, 0.6), "SD_Paint", smooth=False)
    for u in (-w / 2, w / 2):
        metal.tube([T(u, -d / 2 - 1.4, Z_PAVE), T(u, -d / 2 - 1.4, h - 0.65)], [0.035, 0.035], 6, "SD_Metal", C_BLACK)
    for i, u in enumerate((-1.8, 0.0, 1.8)):   # café tables
        t = T(u, -d / 2 - 3.0, 0)
        metal.cylinder(t[0], t[1], Z_PAVE, 0.74, 0.03, 6, "SD_Metal", C_BLACK)
        metal.disc(t[0], t[1], 0.75, 0.42, 14, "SD_Metal", C_WHITE)
        metal.disc(t[0], t[1], 0.742, 0.42, 14, "SD_Metal", shade(C_WHITE, 0.5), y_scale=-1.0)


# ----------------------------------------------------------------------------- city edge
def buildings(walk_out_edge):
    """A ring of mid-rise apartments and offices facing the park, a few with rounded corners."""
    fac, con, paint = mesh("SD_Facade"), mesh("SD_Concrete"), mesh("SD_Paint")
    front = offset_polyline(ring_line(*ROAD, steps=14), -(ROAD_W / 2 + WALK_OUT + 4.0), closed=True)
    tints = [srgb(1.0, 0.95, 0.88), srgb(0.95, 0.90, 0.86), srgb(0.88, 0.90, 0.94), srgb(1.0, 0.88, 0.80), srgb(0.92, 0.94, 0.90)]
    samples = resample(front, 1.0, closed=True)
    total = len(samples)
    i = 0
    while i < total:
        width = R.choice([14, 18, 22, 26])
        j = min(total - 1, i + width)
        a, b = samples[i], samples[j]
        if math.hypot(b[0] - a[0], b[1] - a[1]) < width * 0.8:    # curved stretch: shorten
            i += 4
            continue
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        depth = R.uniform(14, 20)
        # Art direction: low shophouses on the sunset side keep the park in the light; the towers stand
        # opposite, where the low sun lights their facades inside the gameplay view.
        mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
        towards_sun = (mx * TO_SUN.x + my * TO_SUN.y) / max(1e-6, math.hypot(mx, my) * math.hypot(TO_SUN.x, TO_SUN.y))
        if towards_sun > 0.35:
            height = R.choice([7.5, 9, 10.5])
        elif towards_sun > -0.2:
            height = R.choice([12, 15, 18, 21])
        else:
            height = R.choice([21, 24, 30, 36, 42])
        nx, ny = math.sin(ang), -math.cos(ang)            # away from the park
        p0 = (a[0], a[1])
        p1 = (b[0], b[1])
        p2 = (b[0] + nx * depth, b[1] + ny * depth)
        p3 = (a[0] + nx * depth, a[1] + ny * depth)
        tint = R.choice(tints)
        rounded = R.random() < 0.3
        outline = [p0, p1, p2, p3]
        if rounded:     # replace the two park-side corners with quarter circles
            r = min(5.0, width * 0.25)
            ux, uy = math.cos(ang), math.sin(ang)
            c0 = (p0[0] + ux * r + nx * r, p0[1] + uy * r + ny * r)
            c1 = (p1[0] - ux * r + nx * r, p1[1] - uy * r + ny * r)
            ts = [k * math.pi / 12 for k in range(7)]
            arc0 = [(c0[0] - ux * r * math.cos(t) - nx * r * math.sin(t), c0[1] - uy * r * math.cos(t) - ny * r * math.sin(t)) for t in ts]
            arc1 = [(c1[0] - nx * r * math.cos(t) + ux * r * math.sin(t), c1[1] - ny * r * math.cos(t) + uy * r * math.sin(t)) for t in ts]
            outline = arc0 + arc1 + [p2, p3]
        cw = list(outline)
        area = sum(cw[k][0] * cw[(k + 1) % len(cw)][1] - cw[(k + 1) % len(cw)][0] * cw[k][1] for k in range(len(cw)))
        if area < 0:      # counter-clockwise from above, so wall() faces outward
            cw = cw[::-1]
        fac.wall(cw, Z_WALK, height, "SD_Facade", tint, uv_scale=24.0, closed=True, smooth=rounded)
        # ground-floor shopfront band + canopy
        paint.wall([(p[0], p[1]) for p in offset_polyline(cw, -0.03, closed=True)], Z_WALK, 3.6, "SD_Paint", shade(tint, 0.35), uv_scale=4.0, closed=True, smooth=rounded)
        # roof slab and parapet
        con.fan_xy(cw, height + 0.01, "SD_Concrete", shade(tint, 0.75), uv_scale=4.0)
        par = offset_polyline(cw, 0.25, closed=True)
        con.wall(cw, height, height + 0.9, "SD_Concrete", shade(tint, 0.85), uv_scale=4.0, closed=True, smooth=rounded)
        con.wall(par[::-1], height, height + 0.9, "SD_Concrete", shade(tint, 0.6), uv_scale=4.0, closed=True, smooth=rounded)
        con.strip_xy(par, cw, height + 0.9, "SD_Concrete", shade(tint, 0.9), uv_scale=4.0, closed=True)
        if height >= 21 and R.random() < 0.6:   # rooftop plant room
            mx, my = sum(p[0] for p in outline) / len(outline), sum(p[1] for p in outline) / len(outline)
            con.box(mx - 3, my - 2.5, height, mx + 3, my + 2.5, height + 3.2, "SD_Concrete", shade(tint, 0.7), uv_scale=3.0, yaw=ang, origin=(mx, my))
        i = j + R.choice([3, 4, 6])


def skyline():
    sky = mesh("SD_Skyline")
    radius, h = 205.0, 62.0
    n = 14
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        p0 = (math.cos(a0) * radius, math.sin(a0) * radius)
        p1 = (math.cos(a1) * radius, math.sin(a1) * radius)
        seg = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        u0 = k * 0.37
        pts = [(p0[0], p0[1], -2.0), (p1[0], p1[1], -2.0), (p1[0], p1[1], h), (p0[0], p0[1], h)]
        uvs = [(u0, 0), (u0 + seg / 230.0, 0), (u0 + seg / 230.0, 1), (u0, 1)]
        sky.poly(pts[::-1], uvs[::-1], C_WHITE, "SD_Skyline", smooth=False)


# ----------------------------------------------------------------------------- vegetation layout
def tree(species, x, y, scale=1.0, yaw=None):
    LAYOUT["trees"].append({"species": species, "x": round(x, 3), "y": round(y, 3), "yaw": round(yaw if yaw is not None else R.uniform(0, 360), 1),
                            "scale": round(scale * R.uniform(0.9, 1.1), 3)})


def hedge(points, spacing=0.9, kind="Shrub"):
    for (x, y, ang) in resample(points, spacing):
        LAYOUT.setdefault("shrubs", []).append({"kind": kind if R.random() > 0.18 else "Blossom", "x": round(x + R.uniform(-0.1, 0.1), 3), "y": round(y + R.uniform(-0.1, 0.1), 3),
                                                 "yaw": round(R.uniform(0, 360), 1), "scale": round(R.uniform(0.85, 1.2), 3)})


def vegetation(centre):
    # Street trees on both sidewalks of the ring road, avoiding crossings and corners' sight lines.
    for off, sp, phase in ((ROAD_W / 2 + 1.6, 13.0, 0.0), (-(ROAD_W / 2 + 1.9), 13.0, 6.5)):
        line = offset_polyline(centre, off, closed=True)
        for k, (x, y, ang) in enumerate(resample(line, sp, closed=True)):
            if min(abs(x), abs(y)) < 5.0 and (abs(abs(x) - ROAD[0]) < 8 or abs(abs(y) - ROAD[1]) < 8):
                continue
            tree("Canopy" if (k + int(phase)) % 3 else "Laurel", x, y, 1.0)
    # Palms announce the four plaza entrances.
    for (x, y) in ((-3.2, 26.5), (3.2, 26.5), (-3.2, -26.5), (3.2, -26.5), (20.5, -3.0), (20.5, 3.0), (-20.5, -3.0), (-20.5, 3.0)):
        tree("Palm", x, y, 1.0)
    # Flame-tree accents and shade groves in the two parks.
    groves = [
        ("Flame", -30, 38), ("Flame", 32, 34), ("Flame", -6, 48), ("Flame", 28, -44), ("Flame", -34, -36),
        ("Canopy", -40, 47), ("Canopy", 40, 48), ("Laurel", -24, 52), ("Laurel", 22, 53), ("Canopy", 12, 52),
        ("Canopy", -42, -46), ("Canopy", 40, -50), ("Laurel", -20, -52), ("Laurel", 16, -52), ("Canopy", 42, -30),
        ("Canopy", -42, 28), ("Laurel", -12, -34), ("Flame", 10, -36), ("Canopy", 42, 30), ("Laurel", -42, -24),
    ]
    for species, x, y in groves:
        tree(species, x + R.uniform(-1.5, 1.5), y + R.uniform(-1.5, 1.5), 1.0)
    # Laurel rows screen the neighbour courts from the ring road.
    for s in (-1, 1):
        for y in (-9.0, -3.0, 3.0, 9.0):
            tree("Laurel", s * 45.6, y + R.uniform(-0.4, 0.4), 0.9)
    # Clipped hedges along the plaza edge and the court fences, with flowering shrubs mixed in.
    for s in (-1, 1):
        hedge([(s * 19.8, -17.0), (s * 19.8, -6.0)])
        hedge([(s * 19.8, 6.0), (s * 19.8, 17.0)])
        hedge([(s * 23.0 + s * -0.0, -11.4 + 0.0), (s * 43.0, -11.4)], spacing=1.1)
        hedge([(s * 23.0, 11.4), (s * 43.0, 11.4)], spacing=1.1)
    for s in (-1, 1):
        hedge([(-14.0, s * 25.9), (-6.0, s * 25.9)])
        hedge([(6.0, s * 25.9), (14.0, s * 25.9)])


def hedges(coll):
    """All shrubs/climbers merged into one static mesh (one draw call) from the LOD0 prototypes.
    Runs after finish_objects: the prototypes are already in the export frame (Unity x = -X, z = -Y)."""
    shrubs = LAYOUT.get("shrubs", [])
    if not shrubs:
        return None
    names = sorted({f"{s['kind']}_LOD0" for s in shrubs})
    with bpy.data.libraries.load(os.path.join(HERE, "SunsetTrees.blend"), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in names]
    protos = {o.name: o for o in dst.objects if o}
    parts = []
    for i, s in enumerate(shrubs):
        proto = protos[f"{s['kind']}_LOD0"]
        o = bpy.data.objects.new(f"_hedge_{i}", proto.data.copy())
        o.location = (-s["x"], -s["y"], s.get("z", 0.0))
        o.rotation_euler = (0, 0, -math.radians(s["yaw"]))
        o.scale = (s["scale"],) * 3
        coll.objects.link(o)
        parts.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    hedge = bpy.context.view_layer.objects.active
    hedge.name = hedge.data.name = "District_Hedges"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    hedge.data.materials.clear()
    hedge.data.materials.append(material("SD_Foliage"))
    for p in hedge.data.polygons:
        p.material_index = 0
    for o in protos.values():
        bpy.data.objects.remove(o)
    return hedge


def main():
    reset_scene()
    centre, walk_in_edge, walk_out_edge = ring_road()
    grass_ground()
    plaza()
    # curvy park paths: plaza -> parks -> crossings, and plaza -> neighbour courts
    path([(0, 25), (2.5, 32), (-3, 40), (1.5, 48), (0, 57.6)], 3.0, colour=C_PAVE_WARM)
    path([(0, -25), (-2.5, -31), (3, -38)], 3.0, colour=C_PAVE_WARM)
    path([(4.0, -46.0), (2, -52), (0, -57.6)], 3.0, colour=C_PAVE_WARM)
    path([(-19, 18), (-26, 24), (-34, 26), (-44, 36), (-47.6, 44)], 2.4)
    path([(19, -18), (26, -24), (34, -26), (44, -36), (47.6, -44)], 2.4)
    path([(19, 0), (22.9, 0)], 2.6, edge=False)
    path([(-19, 0), (-22.9, 0)], 2.6, edge=False)
    path([(43.2, 0), (47.6, 0)], 2.6, edge=False)
    path([(-43.2, 0), (-47.6, 0)], 2.6, edge=False)
    path([(-8, 33), (-14, 36), (-18, 33.0), (-22, 30), (-28, 29)], 2.0)
    path([(8, 33), (13, 35.5), (16, 39), (16, 44), (12, 49), (4, 50)], 2.0)
    path([(-4.5, -42), (-12, -43.5), (-15.5, -42)], 2.0)
    court_complex(1, [COURT_SCHEMES[0], COURT_SCHEMES[1]])
    court_complex(-1, [COURT_SCHEMES[2], COURT_SCHEMES[3]])
    pond(-17.0, 43.0, 8.5, 5.2)
    pergola(16.0, 41.0, math.radians(90))
    fountain(3.0, -42.0)
    kiosk(-20.0, -42.0, math.radians(-90))
    for (x, y, yaw) in ((-6.5, 30.0, 0.0), (6.0, 30.0, math.pi), (-9.0, -30.5, 0.0), (9.5, -31.0, math.pi), (-3.5, -48.5, math.radians(30)),
                        (-24.0, 27.6, math.radians(-90)), (24.0, -27.6, math.radians(90)), (-9.0, 41.0, math.radians(200))):
        bench(x, y, yaw)
    # lamps: ring road (both walks, staggered), park lanterns along the paths
    for off, phase in ((ROAD_W / 2 + 1.1, 0), (-(ROAD_W / 2 + 1.1), 1)):
        line = offset_polyline(centre, off, closed=True)
        pts = resample(line, 26.0, closed=True)
        for k, (x, y, ang) in enumerate(pts):
            if (k + phase) % 2:
                continue
            inward = ang + (math.pi / 2 if off < 0 else -math.pi / 2)
            street_lamp(x, y, inward, 7.0)
    for (x, y) in ((-4.5, 34.0), (3.5, 44.0), (-4.0, -33.0), (6.0, -50.0), (-30.0, 25.0), (30.0, -25.0), (12.0, 36.0), (-24.0, 40.0), (-12.0, -45.0)):
        street_lamp(x, y, 0.0, 3.6, ornate=True)
    for s in (-1, 1):
        for y in (-19.0, 19.0):
            street_lamp(s * 18.0, y, 0.0, 3.6, ornate=True)
    buildings(walk_out_edge)
    skyline()
    vegetation(centre)

    coll = collection("SunsetDistrict")
    objects = []
    report = {}
    for name, m in sorted(M.items()):
        o = m.object(coll)
        objects.append(o)
        report[o.name] = m.triangles()
    finish_objects(objects)
    hedge_obj = hedges(coll)
    if hedge_obj:
        objects.append(hedge_obj)
        report[hedge_obj.name] = sum(len(p.vertices) - 2 for p in hedge_obj.data.polygons)
    sun_data = bpy.data.lights.new("Preview_GoldenSun", 'SUN')
    sun_data.energy = 4.0
    sun_data.color = (1.0, 0.72, 0.45)
    sun = bpy.data.objects.new("Preview_GoldenSun", sun_data)
    bpy.context.scene.collection.objects.link(sun)
    sun.rotation_euler = Vector((0, 0, -1)).rotation_difference(Vector((TO_SUN.x, TO_SUN.y, -TO_SUN.z))).to_euler()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "SunsetDistrict.blend"), relative_remap=True)
    glb = export_glb(os.path.join(MODELS, "SunsetDistrict.glb"), objects)
    with open(os.path.join(HERE, "district_layout.json"), "w") as f:
        json.dump(LAYOUT, f, indent=1)
    print("DISTRICT_REPORT", json.dumps({"glb": glb, "objects": report, "trees": len(LAYOUT["trees"]), "shrubs": len(LAYOUT.get("shrubs", [])), "lamps": len(LAYOUT["lamps"])}))


if __name__ == "__main__":
    main()
