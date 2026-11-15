"""Lush, card-based trees for the PickleVerse golden-hour district.

    blender --background --python build_trees.py

Writes SunsetTrees.blend (authoring source, next to this script), SunsetTrees.glb (Unity prototypes with
three mesh LODs + a crossed-billboard LOD per tree) and a raw impostor render that sunset_textures.py turns
into SD_TreeImpostors.png.

Leaves are alpha-tested cluster cards from SD_Foliage.png with canopy-shaped ("spherical") normals, so a few
hundred triangles read as a dense crown. Vertex colour: R wind weight, G phase, B occlusion, A tone.
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector

from sd_common import *   # noqa: F401,F403

ATLAS = json.load(open(os.path.join(HERE, "foliage_atlas.json")))
REPORT = {"prototypes": [], "impostors": {}}


def fib_dir(i, n, jitter, r):
    """Even directions on a sphere (Fibonacci lattice) with a little jitter."""
    z = 1 - 2 * (i + 0.5) / n
    rad = math.sqrt(max(0.0, 1 - z * z))
    a = i * 2.399963 + r.uniform(-jitter, jitter)
    return Vector((math.cos(a) * rad, math.sin(a) * rad, z))


class Tree(Mesh):
    def __init__(self, name, crown_centre, crown_radius, height):
        super().__init__(name, foliage=True)
        self.crown_centre = Vector(crown_centre)
        self.crown_radius = Vector(crown_radius)
        self.height = height

    # -- wood ------------------------------------------------------------------------------
    def wood(self, path, radii, sides, phase=0.0, ao0=0.55, ao1=0.9, tone=0.5, v0=0.0):
        pts = [Vector(p) for p in path]
        cols = []
        for i, p in enumerate(pts):
            h = max(0.0, min(1.0, p.z / self.height))
            reach = min(1.0, math.hypot(p.x, p.y) / max(self.crown_radius.x, 0.01))
            wind = 0.55 * h * h + 0.25 * reach * h
            cols.append((wind, phase, lerp(ao0, ao1, i / max(1, len(pts) - 1)), tone))
        self.tube(pts, radii, sides, mat="SD_Bark", cols=cols, cap_end=False, uv_around=max(1.0, round(radii[0] * 2 * math.pi / 0.55)), uv_along=1.1, v0=v0, radial_normals=True)

    # -- leaves ----------------------------------------------------------------------------
    def crown_normal(self, p, card_normal, roundness=0.82):
        d = Vector(p) - self.crown_centre
        d = Vector((d.x / self.crown_radius.x, d.y / self.crown_radius.y, d.z / self.crown_radius.z + 0.28))
        if d.length < 1e-4:
            d = Vector((0, 0, 1))
        d.normalize()
        n = d * roundness + card_normal * (1 - roundness)
        return n.normalized()

    def card(self, base, grow, side, length, width, tile, pivot, phase, ao, tone, segments=1, bend=0.0, wind_scale=1.0):
        """Textured leaf card. pivot 'bottom' (tile v = growth), 'left' (tile u = growth) or 'centre'."""
        rect = ATLAS[tile]
        u0, v0, u1, v1 = rect["u0"], rect["v0"], rect["u1"], rect["v1"]
        inset = 0.004
        u0, v0, u1, v1 = u0 + inset, v0 + inset, u1 - inset, v1 - inset
        grow, side = Vector(grow).normalized(), Vector(side).normalized()
        normal = side.cross(grow).normalized()
        if normal.z < 0:
            normal = -normal
        base = Vector(base)
        if pivot == "centre":
            base = base - grow * length * 0.5
        rows = []
        for s in range(segments + 1):
            t = s / segments
            offset = normal * (bend * length * math.sin(t * math.pi * 0.5) ** 1.4) - grow * (bend * length * 0.35 * t * t)
            c = base + grow * (length * t) + offset
            rows.append((c - side * width * 0.5, c + side * width * 0.5, t))
        for s in range(segments):
            (a0, b0, t0), (a1, b1, t1) = rows[s], rows[s + 1]
            pts = [a0, b0, b1, a1]
            if pivot == "left":
                uvs = [(lerp(u0, u1, t0), v0), (lerp(u0, u1, t0), v1), (lerp(u0, u1, t1), v1), (lerp(u0, u1, t1), v0)]
            else:
                uvs = [(u0, lerp(v0, v1, t0)), (u1, lerp(v0, v1, t0)), (u1, lerp(v0, v1, t1)), (u0, lerp(v0, v1, t1))]
            cols, normals = [], []
            for p, t in zip(pts, (t0, t0, t1, t1)):
                h = max(0.0, min(1.0, p.z / self.height))
                reach = min(1.0, math.hypot(p.x, p.y) / max(self.crown_radius.x, 0.01))
                wind = min(1.0, (0.50 * h * h + 0.30 * reach * h + 0.32 * t) * wind_scale)
                cols.append((wind, phase, min(1.0, ao + 0.10 * t), tone))
                normals.append(self.crown_normal(p, normal))
            self.poly(pts, uvs, mat="SD_Foliage", smooth=True, cols=cols, normals=normals)

    def puff(self, centre, radius, count, tile, size, r, lod, pivot="bottom", aspect=1.0, flat=0.0, droop=0.0, inner=0.42, tiles=None):
        """Ellipsoidal leaf mass: cards grow from inside the puff out to (and past) its surface."""
        centre, radius = Vector(centre), Vector(radius)
        for i in range(count):
            d = fib_dir(i, count, 0.5, r)
            if d.z < -0.55:                       # the underside of a crown is mostly shade and branches
                d.z = -0.55 - (d.z + 0.55) * 0.25
                d.normalize()
            d = Vector((d.x, d.y, d.z * (1 - flat))).normalized()
            surf = Vector((d.x * radius.x, d.y * radius.y, d.z * radius.z))
            s = size * r.uniform(0.82, 1.22)
            start = centre + surf * r.uniform(inner, inner + 0.26)
            grow = (d + Vector((r.uniform(-0.35, 0.35), r.uniform(-0.35, 0.35), r.uniform(-0.25, 0.30) - droop))).normalized()
            ref = Vector((0, 0, 1)) if abs(grow.z) < 0.92 else Vector((1, 0, 0))
            side = grow.cross(ref).normalized()
            twist = r.uniform(-1.2, 1.2)
            side = (side * math.cos(twist) + grow.cross(side) * math.sin(twist)).normalized()
            depth = (start - self.crown_centre)
            depth = min(1.0, math.sqrt((depth.x / self.crown_radius.x) ** 2 + (depth.y / self.crown_radius.y) ** 2 + (depth.z / self.crown_radius.z) ** 2))
            ao = lerp(0.42, 0.98, depth ** 1.3) * lerp(0.80, 1.0, (d.z + 1) * 0.5)
            name = tiles[i % len(tiles)] if tiles else tile
            self.card(start, grow, side, s if pivot != "left" else s * aspect, s if pivot == "left" else s * aspect,
                      name, pivot, r.random(), ao, r.random(), segments=2 if lod == 0 else 1, bend=r.uniform(0.06, 0.16) if lod == 0 else 0.0)


def limb_path(start, end, sag, r, n=4):
    start, end = Vector(start), Vector(end)
    pts = []
    for i in range(n + 1):
        t = i / n
        p = start.lerp(end, t)
        p.z += math.sin(t * math.pi) * sag
        if 0 < i < n:
            p += Vector((r.uniform(-0.08, 0.08), r.uniform(-0.08, 0.08), 0))
        pts.append(p)
    return pts


def broadleaf(name, lod, seed, tile, height=9.2, spread=3.9, tall=1.0, tile2=None):
    """Round-headed street/park shade tree (xa cu / sau silhouette)."""
    r = rng(seed)
    fork = height * 0.36
    crown_c = (0.0, 0.0, fork + (height - fork) * 0.55)
    crown_r = (spread, spread, (height - fork) * 0.62 * tall)
    t = Tree(f"{name}_LOD{lod}", crown_c, crown_r, height)
    sides = (9, 6, 4)[lod]
    trunk = [(0, 0, 0), (0.05, -0.03, 0.35), (0.10, 0.02, fork * 0.5), (0.04, 0.08, fork * 0.82), (0.0, 0.0, fork)]
    t.wood(trunk, [0.46, 0.30, 0.25, 0.22, 0.21], sides, ao0=0.45)
    limbs = 6
    ends = []
    for i in range(limbs):
        a = i * 2 * math.pi / limbs + r.uniform(-0.25, 0.25)
        reach = spread * r.uniform(0.50, 0.66)
        rise = (height - fork) * r.uniform(0.34, 0.62)
        end = Vector((math.cos(a) * reach, math.sin(a) * reach, fork + rise))
        ends.append(end)
        if lod < 2 or i % 2 == 0:
            t.wood(limb_path((0, 0, fork - 0.15), end, 0.35, r, n=(4, 3, 2)[lod]), [0.15, 0.115, 0.085, 0.06, 0.035][: (5, 4, 3)[lod]], (6, 5, 3)[lod], phase=i / limbs, ao0=0.5)
        if lod == 0:
            for j in range(2):
                b = Vector((0, 0, fork)).lerp(end, 0.55 + j * 0.2)
                tip = b + Vector((math.cos(a + (j - 0.5) * 1.6) * 1.0, math.sin(a + (j - 0.5) * 1.6) * 1.0, r.uniform(0.5, 1.0)))
                t.wood([b, b.lerp(tip, 0.5) + Vector((0, 0, 0.12)), tip], [0.05, 0.032, 0.012], 4, phase=r.random(), ao0=0.5)
    top = Vector((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), height - crown_r[2] * 0.42))
    if lod < 2:
        t.wood(limb_path((0, 0, fork), top, 0.0, r, n=(4, 3, 2)[lod]), [0.17, 0.12, 0.085, 0.055, 0.03][: (5, 4, 3)[lod]], (6, 5, 3)[lod], phase=0.5, ao0=0.5)
    count = (96, 36, 13)[lod]
    size = (1.55, 2.15, 3.10)[lod]
    tiles = [tile, tile, tile2 or tile]
    for end in ends:
        t.puff(end + Vector((0, 0, 0.35)), (spread * 0.47, spread * 0.47, crown_r[2] * 0.52), count, tile, size, r, lod, tiles=tiles)
    t.puff(top, (spread * 0.60, spread * 0.60, crown_r[2] * 0.58), int(count * 1.3), tile, size, r, lod, tiles=tiles)
    return t


def flamboyant(name, lod, seed, height=7.4, spread=5.6):
    """Flame tree (phuong vi): low fork, wide umbrella of feathery fronds topped with scarlet blossom."""
    r = rng(seed)
    fork = height * 0.30
    crown_c = (0.0, 0.0, height * 0.70)
    crown_r = (spread, spread, height * 0.30)
    t = Tree(f"{name}_LOD{lod}", crown_c, crown_r, height)
    sides = (9, 6, 4)[lod]
    trunk = [(0, 0, 0), (-0.04, 0.05, 0.3), (0.08, 0.10, fork * 0.6), (0.0, 0.0, fork)]
    t.wood(trunk, [0.50, 0.32, 0.27, 0.25], sides, ao0=0.45)
    limbs = 7
    for i in range(limbs):
        a = i * 2 * math.pi / limbs + r.uniform(-0.2, 0.2)
        reach = spread * r.uniform(0.62, 0.80)
        end = Vector((math.cos(a) * reach, math.sin(a) * reach, height * r.uniform(0.66, 0.80)))
        if lod < 2 or i % 2 == 0:
            t.wood(limb_path((0, 0, fork - 0.1), end, 0.9, r, n=(5, 3, 2)[lod]), [0.17, 0.13, 0.10, 0.075, 0.05, 0.03][: (6, 4, 3)[lod]], (6, 5, 3)[lod], phase=i / limbs, ao0=0.5)
        n = (46, 18, 7)[lod]
        sz = (2.5, 3.4, 4.8)[lod]
        t.puff(end.lerp(Vector((0, 0, end.z)), 0.22), (spread * 0.44, spread * 0.44, height * 0.11), n, "pinnate", sz, r, lod, pivot="left", aspect=0.5, flat=0.55, droop=0.10, inner=0.10)
        # blossom trusses sit on the sunlit outside of the canopy
        nf = (20, 9, 4)[lod]
        for k in range(nf):
            d = fib_dir(k, nf, 0.6, r)
            d.z = abs(d.z) * 0.6 + 0.25
            d.normalize()
            p = end.lerp(Vector((0, 0, end.z)), 0.25) + Vector((d.x * spread * 0.42, d.y * spread * 0.42, d.z * height * 0.11 + 0.20))
            ref = Vector((0, 0, 1)).cross(d).normalized() if abs(d.z) < 0.95 else Vector((1, 0, 0))
            grow = d.cross(ref).normalized()
            fs = (1.25, 1.75, 2.5)[lod] * r.uniform(0.8, 1.2)
            t.card(p, grow, ref, fs, fs, "flowers", "centre", r.random(), r.uniform(0.85, 1.0), r.random())
    centre = Vector((0, 0, height * 0.82))
    t.puff(centre, (spread * 0.55, spread * 0.55, height * 0.10), (60, 24, 9)[lod], "pinnate", (2.5, 3.4, 4.8)[lod], r, lod, pivot="left", aspect=0.5, flat=0.6, droop=0.08, inner=0.05)
    return t


def palm(name, lod, seed, height=9.0):
    """Royal/coconut palm: ringed tapering trunk and a crown of arching pinnate fronds."""
    r = rng(seed)
    lean = Vector((r.uniform(0.25, 0.55), r.uniform(-0.3, 0.3), 0))
    crown = Vector((lean.x, lean.y, height))
    t = Tree(f"{name}_LOD{lod}", (crown.x, crown.y, height + 0.4), (3.6, 3.6, 2.6), height + 2.4)
    n = (7, 4, 2)[lod]
    path, radii = [], []
    for i in range(n + 1):
        s = i / n
        path.append(Vector((lean.x * s * s, lean.y * s * s, height * s)))
        radii.append(lerp(0.24, 0.135, s) + (0.10 * (1 - s) ** 6))
    t.wood(path, radii, (8, 6, 4)[lod], ao0=0.5, ao1=0.75)
    if lod == 0:   # crownshaft bulge under the fronds
        t.wood([crown - Vector((0, 0, 0.9)), crown - Vector((0, 0, 0.45)), crown + Vector((0, 0, 0.2))], [0.15, 0.19, 0.10], 8, ao0=0.7, ao1=0.9, tone=0.9)
    fronds = (16, 10, 6)[lod]
    segs = (5, 3, 2)[lod]
    for i in range(fronds):
        a = i * 2.399963 + r.uniform(-0.2, 0.2)
        tier = i / max(1, fronds - 1)
        elev0 = lerp(math.radians(72), math.radians(-8), tier) + r.uniform(-0.12, 0.12)
        droop = lerp(math.radians(60), math.radians(105), tier)
        length = r.uniform(3.9, 4.6) * (1.12 if lod == 2 else 1.0)
        width = 1.18 * (1.0, 1.15, 1.45)[lod]
        hd = Vector((math.cos(a), math.sin(a), 0))
        pts = [Vector(crown)]
        for s in range(1, segs + 1):
            e = elev0 - droop * ((s - 0.5) / segs) ** 1.15
            pts.append(pts[-1] + (hd * math.cos(e) + Vector((0, 0, math.sin(e)))) * (length / segs))
        rect = ATLAS["palm"]
        u0, v0, u1, v1 = rect["u0"] + 0.004, rect["v0"] + 0.004, rect["u1"] - 0.004, rect["v1"] - 0.004
        side = Vector((-hd.y, hd.x, 0))
        phase = r.random()
        tone = r.random()
        for s in range(segs):
            p0, p1 = pts[s], pts[s + 1]
            t0, t1 = s / segs, (s + 1) / segs
            tangent = (p1 - p0).normalized()
            up = side.cross(tangent).normalized()
            if up.z < 0:
                up = -up
            fold = 0.26 if lod == 0 else 0.0
            halves = ((-1, 0.0, 0.5), (1, 0.5, 1.0)) if lod == 0 else ((0, 0.0, 1.0),)
            for sgn, va, vb in halves:
                if sgn == 0:
                    edge0a, edge0b = p0 - side * width * 0.5, p0 + side * width * 0.5
                    edge1a, edge1b = p1 - side * width * 0.5, p1 + side * width * 0.5
                    quad = [edge0a, edge0b, edge1b, edge1a]
                    uvs = [(lerp(u0, u1, t0), v0), (lerp(u0, u1, t0), v1), (lerp(u0, u1, t1), v1), (lerp(u0, u1, t1), v0)]
                    weights = [t0, t0, t1, t1]
                else:
                    out0 = p0 + side * sgn * width * 0.5 - up * fold * width * 0.5
                    out1 = p1 + side * sgn * width * 0.5 - up * fold * width * 0.5
                    vm, vo = lerp(v0, v1, 0.5), (v0 if sgn < 0 else v1)
                    quad = [p0, out0, out1, p1] if sgn > 0 else [out0, p0, p1, out1]
                    uvs = ([(lerp(u0, u1, t0), vm), (lerp(u0, u1, t0), vo), (lerp(u0, u1, t1), vo), (lerp(u0, u1, t1), vm)] if sgn > 0 else
                           [(lerp(u0, u1, t0), vo), (lerp(u0, u1, t0), vm), (lerp(u0, u1, t1), vm), (lerp(u0, u1, t1), vo)])
                    weights = [t0, t0, t1, t1]
                cols = [(min(1.0, 0.25 + 0.75 * w), phase, lerp(0.55, 1.0, w), tone) for w in weights]
                normals = [(up * 0.75 + Vector((0, 0, 0.45)) + hd * 0.2).normalized()] * 4
                t.poly(quad, uvs, mat="SD_Foliage", smooth=True, cols=cols, normals=normals)
    return t


def shrub(name, lod, seed, flowers=False, radius=0.85, height=1.05):
    r = rng(seed)
    t = Tree(f"{name}_LOD{lod}", (0, 0, height * 0.55), (radius, radius, height * 0.6), height)
    n = (34, 17, 9)[lod]
    sz = (0.78, 1.05, 1.40)[lod]
    for i in range(n):
        d = fib_dir(i, n, 0.5, r)
        d.z = abs(d.z) * 0.9 - 0.05
        d.normalize()
        p = Vector((d.x * radius * 0.72, d.y * radius * 0.72, height * 0.46 + d.z * height * 0.46))
        ref = Vector((0, 0, 1)).cross(d)
        ref = ref.normalized() if ref.length > 1e-3 else Vector((1, 0, 0))
        grow = (d.cross(ref) + d * 0.35).normalized()
        tile = "flowers" if (flowers and i % 3 == 0) else "shrub"
        s = sz * r.uniform(0.85, 1.2) * (0.72 if tile == "flowers" else 1.0)
        t.card(p, grow, ref, s, s, tile, "centre", r.random(), lerp(0.55, 1.0, d.z), r.random(), wind_scale=0.5)
    return t


def billboard(name, width, height, tile_index, x_shift=0.0):
    """Two crossed quads textured from the impostor atlas (2x2 tiles)."""
    t = Tree(f"{name}_LOD3", (0, 0, height * 0.6), (width * 0.5, width * 0.5, height * 0.5), height)
    tx, ty = tile_index % 2, tile_index // 2
    u0, u1 = tx * 0.5 + 0.004, tx * 0.5 + 0.496
    v0, v1 = 1 - (ty + 1) * 0.5 + 0.004, 1 - ty * 0.5 - 0.004
    for ang in (0.0, math.pi / 2):
        d = Vector((math.cos(ang), math.sin(ang), 0))
        pts = [Vector((x_shift, 0, 0)) - d * width * 0.5, Vector((x_shift, 0, 0)) + d * width * 0.5,
               Vector((x_shift, 0, height)) + d * width * 0.5, Vector((x_shift, 0, height)) - d * width * 0.5]
        uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
        cols = [(0.0, 0.3, 0.9, 0.5), (0.0, 0.3, 0.9, 0.5), (0.35, 0.3, 1.0, 0.5), (0.35, 0.3, 1.0, 0.5)]
        normals = [(Vector((p.x - x_shift, p.y, 0)) * 0.22 + Vector((0, 0, 1))).normalized() for p in pts]
        t.poly(pts, uvs, mat="SD_Impostor", smooth=True, cols=cols, normals=normals)
    return t


SPECIES = [
    ("Canopy", lambda lod: broadleaf("Canopy", lod, 4101, "broadleaf_a", height=9.4, spread=3.9, tile2="broadleaf_b")),
    ("Laurel", lambda lod: broadleaf("Laurel", lod, 4202, "broadleaf_b", height=7.6, spread=2.7, tall=1.22, tile2="broadleaf_a")),
    ("Flame", lambda lod: flamboyant("Flame", lod, 4303)),
    ("Palm", lambda lod: palm("Palm", lod, 4404)),
]
BUSHES = [
    ("Shrub", lambda lod: shrub("Shrub", lod, 4505)),
    ("Blossom", lambda lod: shrub("Blossom", lod, 4606, flowers=True, radius=0.95, height=1.15)),
]


def bounds(objs):
    xs, zs = [], []
    for o in objs:
        for v in o.data.vertices:
            xs.append(v.co.x)
            zs.append(v.co.z)
    return min(xs), max(xs), min(zs), max(zs)


def impostor_material():
    """Unlit albedo x baked occlusion, for the neutral impostor render only."""
    m = bpy.data.materials.new("SD_ImpostorBake_Leaf")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = image("SD_Foliage.png")
    col = nt.nodes.new('ShaderNodeVertexColor')
    col.layer_name = "Col"
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    mul = nt.nodes.new('ShaderNodeMix')
    mul.data_type = 'RGBA'
    mul.blend_type = 'MULTIPLY'
    mul.inputs[0].default_value = 1.0
    emit = nt.nodes.new('ShaderNodeEmission')
    transp = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    gt = nt.nodes.new('ShaderNodeMath')
    gt.operation = 'GREATER_THAN'
    gt.inputs[1].default_value = 0.42
    nt.links.new(col.outputs['Color'], sep.inputs[0])
    nt.links.new(tex.outputs['Color'], mul.inputs[6])
    nt.links.new(sep.outputs[2], mul.inputs[7])
    nt.links.new(mul.outputs[2], emit.inputs['Color'])
    nt.links.new(tex.outputs['Alpha'], gt.inputs[0])
    nt.links.new(gt.outputs[0], mix.inputs[0])
    nt.links.new(transp.outputs[0], mix.inputs[1])
    nt.links.new(emit.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs['Surface'])
    try:
        m.surface_render_method = 'DITHERED'
    except Exception:
        pass
    try:
        m.blend_method = 'HASHED'
    except Exception:
        pass
    m.use_backface_culling = False
    bark = bpy.data.materials.new("SD_ImpostorBake_Bark")
    bark.use_nodes = True
    nt = bark.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = image("SD_Bark.png")
    emit = nt.nodes.new('ShaderNodeEmission')
    emit.inputs['Strength'].default_value = 0.75
    nt.links.new(tex.outputs['Color'], emit.inputs['Color'])
    nt.links.new(emit.outputs[0], out.inputs['Surface'])
    return m, bark


def render_impostors(scene, lod0):
    """Orthographic side view of each LOD0 tree into a 2x2 atlas (512 px tiles, down-filtered later)."""
    tile = 512
    leaf, bark = impostor_material()
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    scene.render.film_transparent = True
    scene.render.resolution_x = scene.render.resolution_y = tile
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    try:
        scene.eevee.taa_render_samples = 48
    except Exception:
        pass
    cam_data = bpy.data.cameras.new("ImpostorCam")
    cam_data.type = 'ORTHO'
    cam = bpy.data.objects.new("ImpostorCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    all_objs = [o for o in scene.objects if o.type == 'MESH']
    work = os.path.join(HERE, "_impostor")
    os.makedirs(work, exist_ok=True)
    for index, (name, obj) in enumerate(lod0):
        originals = [s.material for s in obj.material_slots]
        for s in obj.material_slots:
            s.material = leaf if s.material.name == "SD_Foliage" else bark
        for o in all_objs:
            o.hide_render = o is not obj
        x0, x1, z0, z1 = bounds([obj])
        span = max(x1 - x0, z1 - z0) * 1.04
        cx = (x0 + x1) * 0.5
        cam_data.ortho_scale = span
        cam.location = (obj.location.x + cx, obj.location.y - 60, span * 0.5 + z0 - (span - (z1 - z0)) * 0.0)
        cam.rotation_euler = (math.radians(90), 0, 0)
        cam_data.clip_end = 200
        scene.render.filepath = os.path.join(work, f"{index}_{name}.png")
        bpy.ops.render.render(write_still=True)
        REPORT["impostors"][name] = {"tile": index, "width": span, "height": span, "centre_x": cx, "base_z": z0}
        for s, mat in zip(obj.material_slots, originals):
            s.material = mat
    for o in all_objs:
        o.hide_render = False
    bpy.data.objects.remove(cam)
    with open(os.path.join(work, "layout.json"), "w") as f:
        json.dump(REPORT["impostors"], f, indent=1)


def main():
    scene = reset_scene()
    coll = collection("SunsetTrees")
    objects, lod0 = [], []
    column = 0
    for name, make in SPECIES + BUSHES:
        for lod in range(3):
            tree = make(lod)
            obj = tree.object(coll)
            obj.location = (column * 14.0, lod * 16.0, 0)
            objects.append(obj)
            REPORT["prototypes"].append({"name": obj.name, "triangles": tree.triangles(), "vertices": len(obj.data.vertices)})
            if lod == 0 and (name, make) in SPECIES:
                lod0.append((name, obj))
        column += 1
    render_impostors(scene, lod0)
    column = 0
    for name, _ in SPECIES:
        info = REPORT["impostors"][name]
        b = billboard(name, info["width"], info["height"], info["tile"], x_shift=info["centre_x"])
        obj = b.object(coll)
        obj.location = (column * 14.0, 3 * 16.0, 0)
        objects.append(obj)
        REPORT["prototypes"].append({"name": obj.name, "triangles": b.triangles(), "vertices": len(obj.data.vertices)})
        column += 1

    # Preview light so the .blend opens looking like the game's golden hour.
    sun_data = bpy.data.lights.new("Preview_GoldenSun", 'SUN')
    sun_data.energy = 4.0
    sun_data.color = (1.0, 0.72, 0.45)
    sun = bpy.data.objects.new("Preview_GoldenSun", sun_data)
    scene.collection.objects.link(sun)
    sun.rotation_euler = Vector((0, 0, -1)).rotation_difference(Vector((TO_SUN.x, TO_SUN.y, -TO_SUN.z))).to_euler()

    finish_objects(objects)
    blend = os.path.join(HERE, "SunsetTrees.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend, relative_remap=True)

    # Export prototypes at the origin; the Unity installer instances them.
    for o in objects:
        o.location = (0, 0, 0)
    REPORT["glb"] = export_glb(os.path.join(MODELS, "SunsetTrees.glb"), objects)
    with open(os.path.join(HERE, "trees_report.json"), "w") as f:
        json.dump(REPORT, f, indent=1)
    print("TREES_REPORT", json.dumps(REPORT["glb"]))
    for p in REPORT["prototypes"]:
        print("  ", p)


if __name__ == "__main__":
    main()
