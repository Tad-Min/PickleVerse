"""Shared helpers for the PickleVerse golden-hour district builders (Blender 5.1, background mode).

Frames
------
Builders work in a right-handed "plan frame": x = Unity x (right), y = Unity z (forward, the
court's long axis), z = height. `finish()` rotates every object 180 degrees about the vertical
axis so the existing glTF -> Unity convention of this project still holds
(Unity x = -Blender X, Unity z = -Blender Y).

Vertex colours
--------------
* Surface meshes: RGB = paint/tint stored with gamma 2 (the shader squares it), A = extra data.
* Foliage meshes: R = wind bend weight, G = phase, B = occlusion, A = tonal variation (raw).
"""
import json
import math
import os
import random
import struct

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ASSETS = os.path.join(ROOT, "My project", "Assets")
TEXTURES = os.path.join(ASSETS, "EnvironmentContent", "SunsetDistrict", "Textures")
MODELS = os.path.join(ASSETS, "Models", "Stadium_Enhanced", "District")
VENUE_MODELS = os.path.join(ASSETS, "Models", "Stadium_Enhanced")
os.makedirs(MODELS, exist_ok=True)

# Golden hour: the sun sits low behind-left of the near-end camera, 21 degrees above the horizon, so the
# opponent's face is lit and shadows rake diagonally across the court away from the camera.
# Keep in sync with MatchEnvironmentLighting.GoldenHourSun (Euler 21, 62, 0 in Unity).
SUN_ELEVATION = math.radians(21.0)
SUN_YAW_UNITY = math.radians(62.0)
_fx, _fz = math.sin(SUN_YAW_UNITY), math.cos(SUN_YAW_UNITY)            # light travel direction in Unity x/z
TO_SUN = Vector((-_fx * math.cos(SUN_ELEVATION), -_fz * math.cos(SUN_ELEVATION), math.sin(SUN_ELEVATION))).normalized()
MASK_SPAN = 224.0            # metres covered by the baked sun/occlusion mask, centred on the main court

WHITE = (1.0, 1.0, 1.0, 1.0)


def srgb(r, g, b, a=1.0):
    """Author colours as sRGB 0..1; returns the gamma-2 encoded linear tint the surface shader expects."""
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (math.sqrt(lin(r)), math.sqrt(lin(g)), math.sqrt(lin(b)), a)


def shade(c, k):
    """Darken/lighten an encoded tint by a linear factor k."""
    s = math.sqrt(max(k, 0.0))
    return (c[0] * s, c[1] * s, c[2] * s, c[3])


def lerp(a, b, t):
    return a + (b - a) * t


def vlerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(len(a)))


def smooth01(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ----------------------------------------------------------------------------- scene / materials
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    return scene


def collection(name, parent=None):
    c = bpy.data.collections.get(name)
    if not c:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c


def image(name):
    path = os.path.join(TEXTURES, name)
    img = bpy.data.images.get(name)
    if not img and os.path.exists(path):
        img = bpy.data.images.load(path)
        img.name = name
    return img


# name -> (base map, normal map, roughness, metallic, alpha mode, uses vertex tint)
MATERIAL_LIBRARY = {
    "SD_Acrylic":   ("SD_Acrylic.png", "SD_Acrylic_N.png", 0.62, 0.0, None, True),
    "SD_Asphalt":   ("SD_Asphalt.png", "SD_Asphalt_N.png", 0.82, 0.0, None, True),
    "SD_Paving":    ("SD_Paving.png", "SD_Paving_N.png", 0.74, 0.0, None, True),
    "SD_Concrete":  ("SD_Concrete.png", None, 0.86, 0.0, None, True),
    "SD_Grass":     ("SD_Grass.png", None, 0.95, 0.0, None, True),
    "SD_Wood":      ("SD_Wood.png", None, 0.58, 0.0, None, True),
    "SD_Paint":     ("SD_White.png", None, 0.38, 0.0, None, True),       # satin powder-coat / road paint; colour from vertices
    "SD_Metal":     ("SD_White.png", None, 0.30, 0.85, None, True),      # galvanised / brushed steel
    "SD_Glow":      ("SD_White.png", None, 0.40, 0.0, None, True),       # lamp lenses: emissive at night
    "SD_Facade":    ("SD_Facade.png", None, 0.55, 0.0, None, True),
    "SD_Water":     ("SD_White.png", "SD_Ripple_N.png", 0.06, 0.0, None, True),
    "SD_ChainLink": ("SD_ChainLink.png", None, 0.42, 0.8, "CLIP", True),
    "SD_Net":       ("SD_Net.png", None, 0.80, 0.0, "CLIP", True),
    "SD_Skyline":   ("SD_Skyline.png", None, 1.0, 0.0, "CLIP", True),
    "SD_Signage":   (None, None, 0.5, 0.0, None, False),                  # keeps the venue's existing sign atlas in Unity
    "SD_Bark":      ("SD_Bark.png", None, 0.90, 0.0, None, False),
    "SD_Foliage":   ("SD_Foliage.png", None, 0.70, 0.0, "CLIP", False),
    "SD_Impostor":  ("SD_TreeImpostors.png", None, 0.90, 0.0, "CLIP", False),
}


def material(name):
    """Principled material that reads clearly in Blender and maps by name to a Unity material on import."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    base, normal, rough, metal, alpha, tinted = MATERIAL_LIBRARY[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    colour_socket = None
    img = image(base) if base else None
    if img and not alpha:
        img.alpha_mode = 'CHANNEL_PACKED'     # alpha carries data (e.g. facade glass mask), not coverage
    if img:
        tex = nodes.new('ShaderNodeTexImage')
        tex.image = img
        tex.location = (-760, 300)
        colour_socket = tex.outputs['Color']
        if alpha:
            links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    if tinted:
        attr = nodes.new('ShaderNodeVertexColor')
        attr.layer_name = "Col"
        attr.location = (-760, 0)
        square = nodes.new('ShaderNodeMix')
        square.data_type = 'RGBA'
        square.blend_type = 'MULTIPLY'
        square.inputs[0].default_value = 1.0
        square.location = (-540, 0)
        links.new(attr.outputs['Color'], square.inputs[6])
        links.new(attr.outputs['Color'], square.inputs[7])
        if colour_socket:
            mul = nodes.new('ShaderNodeMix')
            mul.data_type = 'RGBA'
            mul.blend_type = 'MULTIPLY'
            mul.inputs[0].default_value = 1.0
            mul.location = (-320, 200)
            links.new(colour_socket, mul.inputs[6])
            links.new(square.outputs[2], mul.inputs[7])
            colour_socket = mul.outputs[2]
        else:
            colour_socket = square.outputs[2]
    if colour_socket:
        links.new(colour_socket, bsdf.inputs['Base Color'])
    if name == "SD_Glow":
        bsdf.inputs['Emission Strength'].default_value = 0.0
    nimg = image(normal) if normal else None
    if nimg:
        nimg.colorspace_settings.name = 'Non-Color'
        ntex = nodes.new('ShaderNodeTexImage')
        ntex.image = nimg
        ntex.location = (-760, -320)
        nmap = nodes.new('ShaderNodeNormalMap')
        nmap.location = (-320, -320)
        links.new(ntex.outputs['Color'], nmap.inputs['Color'])
        links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    if alpha:
        try:
            m.blend_method = 'HASHED'
        except Exception:
            pass
        try:
            m.surface_render_method = 'DITHERED'
        except Exception:
            pass
    m.use_backface_culling = not alpha
    return m


# ----------------------------------------------------------------------------- mesh builder
class Mesh:
    """Accumulates polygons with per-corner UVs/colours. One instance becomes one Blender object."""

    def __init__(self, name, foliage=False):
        self.name = name
        self.verts, self.faces = [], []
        self.uvs, self.cols, self.normals = [], [], []
        self.face_mat, self.face_smooth = [], []
        self.slots = []
        self.foliage = foliage
        self.custom_normals = False

    def slot(self, name):
        if name not in self.slots:
            self.slots.append(name)
        return self.slots.index(name)

    def poly(self, pts, uvs=None, col=WHITE, mat="SD_Paint", smooth=True, cols=None, normals=None):
        """pts: counter-clockwise seen from the visible side."""
        n = len(pts)
        base = len(self.verts)
        self.verts.extend(tuple(p) for p in pts)
        self.faces.append(tuple(range(base, base + n)))
        self.uvs.extend(uvs if uvs else [(0.0, 0.0)] * n)
        self.cols.extend(cols if cols else [col] * n)
        if normals:
            self.custom_normals = True
            self.normals.extend(tuple(v) for v in normals)
        else:
            self.normals.extend([None] * n)
        self.face_mat.append(self.slot(mat))
        self.face_smooth.append(smooth)

    # -- flat helpers ------------------------------------------------------------------------
    def quad_xy(self, x0, y0, x1, y1, z, mat, col=WHITE, uv_scale=1.0, uv_rot=False):
        """Horizontal rectangle facing up, UVs in metres / uv_scale."""
        pts = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
        self.poly(pts, [self.planar(p, uv_scale, uv_rot) for p in pts], col, mat, smooth=False)

    @staticmethod
    def planar(p, scale, rot=False):
        return (p[1] / scale, p[0] / scale) if rot else (p[0] / scale, p[1] / scale)

    def fan_xy(self, outline, z, mat, col=WHITE, uv_scale=1.0, centre=None, cols=None):
        """Convex/star-shaped horizontal polygon as a triangle fan around its centroid (CCW outline)."""
        n = len(outline)
        cx = centre[0] if centre else sum(p[0] for p in outline) / n
        cy = centre[1] if centre else sum(p[1] for p in outline) / n
        c = (cx, cy, z)
        for i in range(n):
            a = (outline[i][0], outline[i][1], z)
            b = (outline[(i + 1) % n][0], outline[(i + 1) % n][1], z)
            self.poly([c, a, b], [self.planar(c, uv_scale), self.planar(a, uv_scale), self.planar(b, uv_scale)],
                      col, mat, smooth=False, cols=[cols[0], cols[1], cols[1]] if cols else None)

    def strip_xy(self, left, right, z, mat, col=WHITE, uv_scale=1.0, uv_along=None, closed=False, zs=None, cols=None, v_scale=None):
        """Ribbon between two polylines (left/right as seen walking along it). Faces up.
        uv_along: use (across 0..1, distance / uv_along) instead of planar mapping."""
        n = len(left)
        dist = 0.0
        rng = range(n if closed else n - 1)
        for i in rng:
            j = (i + 1) % n
            za = zs[i] if zs else z
            zb = zs[j] if zs else z
            l0, r0, l1, r1 = left[i], right[i], left[j], right[j]
            seg = math.hypot((l1[0] + r1[0] - l0[0] - r0[0]) * 0.5, (l1[1] + r1[1] - l0[1] - r0[1]) * 0.5)
            pts = [(r0[0], r0[1], za), (r1[0], r1[1], zb), (l1[0], l1[1], zb), (l0[0], l0[1], za)]
            if uv_along:
                width = math.hypot(l0[0] - r0[0], l0[1] - r0[1]) / (v_scale or uv_along)
                uvs = [(dist / uv_along, 0.0), ((dist + seg) / uv_along, 0.0), ((dist + seg) / uv_along, width), (dist / uv_along, width)]
            else:
                uvs = [self.planar(p, uv_scale) for p in pts]
            c = cols[i] if cols else col
            self.poly(pts, uvs, c, mat, smooth=True)
            dist += seg

    def wall(self, line, z0, z1, mat, col=WHITE, uv_scale=1.0, closed=False, col_top=None, flip=False, smooth=True):
        """Vertical skin along a polyline. Visible from the right-hand side when walking along the line."""
        n = len(line)
        dist = 0.0
        for i in (range(n) if closed else range(n - 1)):
            a, b = line[i], line[(i + 1) % n]
            seg = math.hypot(b[0] - a[0], b[1] - a[1])
            pts = [(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z1), (a[0], a[1], z1)]
            uvs = [(dist / uv_scale, z0 / uv_scale), ((dist + seg) / uv_scale, z0 / uv_scale), ((dist + seg) / uv_scale, z1 / uv_scale), (dist / uv_scale, z1 / uv_scale)]
            cs = [col, col, col_top or col, col_top or col]
            if flip:
                pts, uvs, cs = pts[::-1], uvs[::-1], cs[::-1]
            self.poly(pts, uvs, mat=mat, smooth=smooth, cols=cs)
            dist += seg

    def box(self, x0, y0, z0, x1, y1, z1, mat="SD_Paint", col=WHITE, uv_scale=1.0, bottom=False, top=True, col_bottom=None, yaw=0.0, origin=None):
        """Axis-aligned box (optionally yawed about `origin`). col_bottom darkens the base for contact shading."""
        cb = col_bottom or col
        ca, sa = math.cos(yaw), math.sin(yaw)
        ox, oy = origin if origin else ((x0 + x1) * 0.5, (y0 + y1) * 0.5)

        def T(x, y, z):
            dx, dy = x - ox, y - oy
            return (ox + dx * ca - dy * sa, oy + dx * sa + dy * ca, z)

        def side(a, b, w):
            pts = [T(a[0], a[1], z0), T(b[0], b[1], z0), T(b[0], b[1], z1), T(a[0], a[1], z1)]
            uvs = [(0, z0 / uv_scale), (w / uv_scale, z0 / uv_scale), (w / uv_scale, z1 / uv_scale), (0, z1 / uv_scale)]
            self.poly(pts, uvs, mat=mat, smooth=False, cols=[cb, cb, col, col])

        side((x0, y0), (x1, y0), x1 - x0)
        side((x1, y0), (x1, y1), y1 - y0)
        side((x1, y1), (x0, y1), x1 - x0)
        side((x0, y1), (x0, y0), y1 - y0)
        if top:
            pts = [T(x0, y0, z1), T(x1, y0, z1), T(x1, y1, z1), T(x0, y1, z1)]
            self.poly(pts, [((x0) / uv_scale, y0 / uv_scale), (x1 / uv_scale, y0 / uv_scale), (x1 / uv_scale, y1 / uv_scale), (x0 / uv_scale, y1 / uv_scale)], col, mat, smooth=False)
        if bottom:
            pts = [T(x0, y1, z0), T(x1, y1, z0), T(x1, y0, z0), T(x0, y0, z0)]
            self.poly(pts, None, cb, mat, smooth=False)

    # -- round helpers -----------------------------------------------------------------------
    def tube(self, path, radii, sides, mat="SD_Paint", col=WHITE, cols=None, cap_start=False, cap_end=True, uv_around=1.0, uv_along=1.0, up_hint=(0.0, 0.0, 1.0), v0=0.0, radial_normals=False):
        """Generalised cylinder along a polyline with per-point radius. Smooth shaded."""
        pts = [Vector(p) for p in path]
        n = len(pts)
        rings = []
        prev_x = None
        dist = v0
        dists = []
        for i in range(n):
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)])
            if t.length < 1e-6:
                t = Vector((0, 0, 1))
            t.normalize()
            x = (prev_x - t * prev_x.dot(t)) if prev_x is not None else Vector(up_hint).cross(t)
            if x.length < 1e-4:
                x = Vector((1, 0, 0)).cross(t)
                if x.length < 1e-4:
                    x = Vector((0, 1, 0)).cross(t)
            x.normalize()
            y = t.cross(x)
            prev_x = x
            r = radii[i] if isinstance(radii, (list, tuple)) else radii
            rings.append([pts[i] + (x * math.cos(a) + y * math.sin(a)) * r for a in (2 * math.pi * k / sides for k in range(sides))])
            if i > 0:
                dist += (pts[i] - pts[i - 1]).length
            dists.append(dist)
        for i in range(n - 1):
            c0 = cols[i] if cols else col
            c1 = cols[i + 1] if cols else col
            for k in range(sides):
                k1 = (k + 1) % sides
                a, b, c, d = rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]
                u0, u1 = k / sides * uv_around, (k + 1) / sides * uv_around
                normals = None
                if radial_normals:
                    normals = [(a - pts[i]).normalized(), (b - pts[i]).normalized(), (c - pts[i + 1]).normalized(), (d - pts[i + 1]).normalized()]
                self.poly([a, b, c, d], [(u0, dists[i] / uv_along), (u1, dists[i] / uv_along), (u1, dists[i + 1] / uv_along), (u0, dists[i + 1] / uv_along)],
                          mat=mat, smooth=True, cols=[c0, c0, c1, c1], normals=normals)
        if cap_end and (radii[-1] if isinstance(radii, (list, tuple)) else radii) > 1e-4:
            self.poly(list(rings[-1]), None, cols[-1] if cols else col, mat, smooth=False)
        if cap_start:
            self.poly(list(rings[0])[::-1], None, cols[0] if cols else col, mat, smooth=False)

    def cylinder(self, x, y, z0, z1, r, sides=8, mat="SD_Paint", col=WHITE, r_top=None, col_bottom=None, cap=True):
        self.tube([(x, y, z0), (x, y, z1)], [r, r if r_top is None else r_top], sides, mat, col, cols=[col_bottom or col, col], cap_end=cap)

    def disc(self, x, y, z, r, sides, mat, col=WHITE, uv_scale=1.0, r_inner=0.0, col_inner=None, y_scale=1.0):
        ring = [(x + math.cos(2 * math.pi * k / sides) * r, y + math.sin(2 * math.pi * k / sides) * r * y_scale) for k in range(sides)]
        if r_inner <= 0:
            self.fan_xy(ring, z, mat, col, uv_scale, centre=(x, y), cols=[col_inner or col, col])
        else:
            inner = [(x + math.cos(2 * math.pi * k / sides) * r_inner, y + math.sin(2 * math.pi * k / sides) * r_inner * y_scale) for k in range(sides)]
            self.strip_xy(inner, ring, z, mat, col, uv_scale, closed=True)

    # -- output ------------------------------------------------------------------------------
    def triangles(self):
        return sum(len(f) - 2 for f in self.faces)

    def object(self, coll=None, merge=0.0005, sharp_angle=40.0):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.verts, [], self.faces)
        for name in self.slots:
            mesh.materials.append(material(name))
        uv = mesh.uv_layers.new(name="UVMap")
        col = mesh.color_attributes.new(name="Col", type='FLOAT_COLOR', domain='CORNER')
        flat_uv = [c for u in self.uvs for c in u]
        flat_col = [c for u in self.cols for c in u]
        uv.data.foreach_set("uv", flat_uv)
        col.data.foreach_set("color", flat_col)
        mesh.polygons.foreach_set("material_index", self.face_mat)
        mesh.polygons.foreach_set("use_smooth", self.face_smooth)
        mesh.color_attributes.active_color = col
        mesh.color_attributes.render_color_index = 0
        mesh.update()
        obj = bpy.data.objects.new(self.name, mesh)
        (coll or bpy.context.scene.collection).objects.link(obj)
        if self.custom_normals:
            fallback = Vector((0, 0, 1))
            mesh.normals_split_custom_set([tuple(n) if n is not None else tuple(fallback) for n in self.normals])
        else:
            # Weld coincident corners so cylinders shade round, then split hard edges by angle.
            import bmesh
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=merge)
            bm.to_mesh(mesh)
            bm.free()
            mesh.set_sharp_from_angle(angle=math.radians(sharp_angle))
        mesh.validate()
        mesh.update()
        return obj


# ----------------------------------------------------------------------------- curves
def arc(cx, cy, r, a0, a1, steps):
    return [(cx + math.cos(lerp(a0, a1, i / steps)) * r, cy + math.sin(lerp(a0, a1, i / steps)) * r) for i in range(steps + 1)]


def catmull(points, per_segment=8, closed=False):
    """Centripetal-ish Catmull-Rom through 2D/3D points; returns a dense polyline."""
    pts = [Vector(p) for p in points]
    n = len(pts)
    out = []
    count = n if closed else n - 1
    for i in range(count):
        p0 = pts[(i - 1) % n] if closed or i > 0 else pts[0] * 2 - pts[1]
        p1, p2 = pts[i % n], pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if closed or i + 2 < n else pts[-1] * 2 - pts[-2]
        for s in range(per_segment):
            t = s / per_segment
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    if not closed:
        out.append(pts[-1])
    return [tuple(p) for p in out]


def offset_polyline(line, d, closed=False):
    """Offset a 2D polyline to its left (d > 0) or right (d < 0)."""
    n = len(line)
    out = []
    for i in range(n):
        a = line[(i - 1) % n] if (closed or i > 0) else line[i]
        b = line[(i + 1) % n] if (closed or i < n - 1) else line[i]
        tx, ty = b[0] - a[0], b[1] - a[1]
        ln = math.hypot(tx, ty) or 1.0
        out.append((line[i][0] - ty / ln * d, line[i][1] + tx / ln * d))
    return out


def rounded_rect(cx, cy, hx, hy, r, steps=6):
    """CCW outline of a rounded rectangle."""
    out = []
    for (sx, sy, a0) in ((1, -1, -math.pi / 2), (1, 1, 0.0), (-1, 1, math.pi / 2), (-1, -1, math.pi)):
        out.extend(arc(cx + sx * (hx - r), cy + sy * (hy - r), r, a0, a0 + math.pi / 2, steps))
    return out


def polyline_length(line, closed=False):
    return sum(math.hypot(line[(i + 1) % len(line)][0] - line[i][0], line[(i + 1) % len(line)][1] - line[i][1]) for i in (range(len(line)) if closed else range(len(line) - 1)))


def resample(line, spacing, closed=False):
    """Points every `spacing` metres along a 2D polyline with the local heading: [(x, y, angle)]."""
    out = []
    n = len(line)
    carry = 0.0
    for i in (range(n) if closed else range(n - 1)):
        a, b = line[i], line[(i + 1) % n]
        seg = math.hypot(b[0] - a[0], b[1] - a[1])
        if seg < 1e-6:
            continue
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        d = carry
        while d < seg:
            t = d / seg
            out.append((lerp(a[0], b[0], t), lerp(a[1], b[1], t), ang))
            d += spacing
        carry = d - seg
    return out


# ----------------------------------------------------------------------------- export
def finish_objects(objects):
    """Rotate plan-frame objects into this project's Blender convention and freeze the transform."""
    for o in objects:
        if o.type != 'MESH':
            continue
        me = o.data
        if me.users > 1 and me.get("_sd_rotated"):
            continue
        for v in me.vertices:
            v.co.x, v.co.y = -v.co.x, -v.co.y
        if me.has_custom_normals:
            normals = [(-n.vector.x, -n.vector.y, n.vector.z) for n in me.corner_normals]
            me.normals_split_custom_set(normals)
        me["_sd_rotated"] = 1
        me.update()


def export_glb(path, objects, strip_images=True):
    """Selection-only GLB without cameras/lights/animation; vertex colours packed to 8 bit and textures stripped
    (Unity binds the separately compressed shared maps by material name)."""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.hide_set(False)
        o.select_set(True)
        parent = o.parent
        while parent:
            parent.select_set(True)
            parent = parent.parent
    options = dict(filepath=str(path), export_format='GLB', use_selection=True, export_animations=False, export_cameras=False,
                   export_lights=False, export_tangents=False, export_yup=True, export_apply=True)
    props = bpy.ops.export_scene.gltf.get_rna_type().properties
    if 'export_vertex_color' in props:
        options['export_vertex_color'] = 'ACTIVE'
    if 'export_all_vertex_colors' in props:
        options['export_all_vertex_colors'] = False
    if 'export_active_vertex_color_when_no_material' in props:
        options['export_active_vertex_color_when_no_material'] = True
    if 'export_extras' in props:
        options['export_extras'] = False
    bpy.ops.export_scene.gltf(**options)
    raw = open(path, 'rb').read()
    n = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20 + n])
    binary = raw[28 + n:]
    replaced = {}
    colour_accessors = {p['attributes']['COLOR_0'] for m in doc['meshes'] for p in m['primitives'] if 'COLOR_0' in p['attributes']}
    for index in colour_accessors:
        a = doc['accessors'][index]
        view = doc['bufferViews'][a['bufferView']]
        offset = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        comps = 4 if a['type'] == 'VEC4' else 3
        if a['componentType'] == 5123:
            values = struct.unpack_from('<' + 'H' * (a['count'] * comps), binary, offset)
            data = bytes((v + 128) // 257 for v in values)
        elif a['componentType'] == 5126:
            values = struct.unpack_from('<' + 'f' * (a['count'] * comps), binary, offset)
            data = bytes(max(0, min(255, int(v * 255 + 0.5))) for v in values)
        else:
            continue
        if comps == 3:      # glTFast expects four byte-aligned channels
            data = b''.join(data[i:i + 3] + b'\xff' for i in range(0, len(data), 3))
            a['type'] = 'VEC4'
        replaced[a['bufferView']] = data
        a['componentType'] = 5121
        a['normalized'] = True
        a['byteOffset'] = 0
        view.pop('byteStride', None)
    if strip_images:
        for key in ('images', 'textures', 'samplers'):
            doc.pop(key, None)
        for m in doc.get('materials', []):
            pbr = m.get('pbrMetallicRoughness', {})
            pbr.pop('baseColorTexture', None)
            pbr.pop('metallicRoughnessTexture', None)
            m.pop('normalTexture', None)
            m.pop('occlusionTexture', None)
            m.pop('emissiveTexture', None)
            m.pop('extensions', None)
        doc.pop('extensionsUsed', None)
        doc.pop('extensionsRequired', None)
    used = sorted({a['bufferView'] for a in doc['accessors']})
    views, mapping, packed = [], {}, bytearray()
    for old in used:
        view = dict(doc['bufferViews'][old])
        offset = view.get('byteOffset', 0)
        data = replaced.get(old, binary[offset:offset + view['byteLength']])
        packed.extend(b'\0' * ((-len(packed)) % 4))
        view['byteOffset'] = len(packed)
        view['byteLength'] = len(data)
        mapping[old] = len(views)
        views.append(view)
        packed.extend(data)
    for a in doc['accessors']:
        a['bufferView'] = mapping[a['bufferView']]
    doc['bufferViews'] = views
    doc['buffers'][0]['byteLength'] = len(packed)
    encoded = json.dumps(doc, separators=(',', ':')).encode()
    encoded += b' ' * ((-len(encoded)) % 4)
    packed.extend(b'\0' * ((-len(packed)) % 4))
    body = struct.pack('<II', len(encoded), 0x4E4F534A) + encoded + struct.pack('<II', len(packed), 0x004E4942) + packed
    with open(path, 'wb') as f:
        f.write(struct.pack('<III', 0x46546C67, 2, 12 + len(body)) + body)
    tris = 0
    for m in doc['meshes']:
        for p in m['primitives']:
            tris += doc['accessors'][p['indices']]['count'] // 3
    return {"file": os.path.basename(path), "bytes": os.path.getsize(path), "meshes": len(doc['meshes']), "triangles": tris,
            "materials": [m.get('name') for m in doc.get('materials', [])]}


def rng(seed):
    return random.Random(seed)
