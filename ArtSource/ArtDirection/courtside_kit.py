"""Art Bible §01 — organic courtside kit for the two match venues.

    blender --background --python courtside_kit.py -- [verify|build] [preview_dir]

Replaces the boxy courtside props of ProfessionalStadium / PickleballCourt_LowPoly (benches, coolers, bottles, towels:
8 to 12 vertices each, flat shaded) with beveled, weighted-normal, smooth-by-angle models, in place:

  * reads  ArtSource/Stadiums/SunsetDistrict/<venue>_Sunset.blend (never saved over)
  * writes ArtSource/ArtDirection/<venue>_ArtDirection.blend and the runtime GLB at the same path/object names
  * every other mesh, the playing surface, lines, net, colliders' sources and materials are left untouched
    (asserted: vertex positions of Court_*/PortableNet* objects are compared before/after)

Props keep the venue conventions: vertex colour 'Col' (sRGB, gamma-2 encoded, see sd_common.srgb), box/plank UVs in
metres, one material per host object (Timber -> SD_Wood, Metal -> SD_Metal, Polymer/OffWhite -> SD_Paint).
'verify' only re-exports the unmodified source to a temp GLB and compares it with the shipped one.
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SUNSET = os.path.abspath(os.path.join(HERE, "..", "Stadiums", "SunsetDistrict"))
sys.path.insert(0, SUNSET)
from sd_common import export_glb, srgb, VENUE_MODELS  # noqa: E402

VENUES = ("ProfessionalStadium", "PickleballCourt_LowPoly")
HOSTS = ("Refined_Venue_Timber", "Refined_Venue_Polymer", "Refined_Venue_OffWhite", "Refined_Venue_Metal")
COLOURS = {   # sRGB, matching upgrade_courts.P so the venue palette is unchanged
    "Refined_Venue_Timber": (0.95, 0.88, 0.80),
    "Refined_Venue_Polymer": (0.18, 0.54, 0.60),
    "Refined_Venue_OffWhite": (0.93, 0.92, 0.89),
    "Refined_Venue_Metal": (0.56, 0.58, 0.59),
}
UV_SCALE = {"Refined_Venue_Timber": 1.2}
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = ARGS[0] if ARGS else "build"
PREVIEW = ARGS[1] if len(ARGS) > 1 else None
REPORT = {}


# ----------------------------------------------------------------------------- analysis
def loose_parts(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    mw = obj.matrix_world
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x.index)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        pts = [mw @ bm.verts[i].co for i in comp]
        mn = Vector([min(p[k] for p in pts) for k in range(3)])
        mx = Vector([max(p[k] for p in pts) for k in range(3)])
        parts.append({"verts": comp, "min": mn, "max": mx, "size": mx - mn, "centre": (mn + mx) / 2})
    bm.free()
    return parts


def near(a, b, tol):
    return all(abs(a[i] - b[i]) <= tol for i in range(len(b)))


def classify(objs):
    """Courtside layout per side (+x / -x) from the existing loose parts."""
    timber, polymer, offwhite, metal = (loose_parts(objs[n]) for n in HOSTS)
    sides = []
    for sign in (-1, 1):
        slats = [p for p in timber if p["centre"].x * sign > 0 and p["size"].z < 0.12]
        backs = [p for p in timber if p["centre"].x * sign > 0 and p["size"].z >= 0.3]
        if not slats or not backs:
            continue
        cx = sum(p["centre"].x for p in slats) / len(slats)
        cy = sum(p["centre"].y for p in slats) / len(slats)
        seat_top = max(p["max"].z for p in slats)
        length = max(p["size"].y for p in slats)
        legs = [p for p in metal if p["centre"].x * sign > 0 and near(p["size"], (0.35, 0.10, 0.48), 0.03)
                and abs(p["centre"].y - cy) < length / 2 + 0.05]
        ground = min(p["min"].z for p in legs) if legs else seat_top - 0.5
        cooler = next(p for p in polymer if p["centre"].x * sign > 0 and near(p["size"], (0.45, 0.52, 0.52), 0.03))
        lid = next(p for p in offwhite if p["centre"].x * sign > 0 and near(p["size"], (0.49, 0.56, 0.07), 0.03))
        bottle = next(p for p in polymer if p["centre"].x * sign > 0 and near(p["size"], (0.11, 0.11, 0.26), 0.03))
        cap = next(p for p in offwhite if p["centre"].x * sign > 0 and near(p["size"], (0.07, 0.07, 0.03), 0.02))
        towel = next(p for p in offwhite if p["centre"].x * sign > 0 and near(p["size"], (0.32, 0.43, 0.02), 0.03))
        sides.append({
            "sign": sign, "bench": Vector((cx, cy, ground)), "seat_top": seat_top, "length": length,
            "back_out": 1 if backs[0]["centre"].x > cx else -1,
            "cooler": Vector((cooler["centre"].x, cooler["centre"].y, cooler["min"].z)),
            "bottle": Vector((bottle["centre"].x, bottle["centre"].y, bottle["min"].z)),
            "towel": Vector((towel["centre"].x, towel["centre"].y, towel["min"].z)),
            "remove": {
                "Refined_Venue_Timber": [p["verts"] for p in slats + backs],
                "Refined_Venue_Metal": [p["verts"] for p in legs],
                "Refined_Venue_Polymer": [cooler["verts"], bottle["verts"]],
                "Refined_Venue_OffWhite": [lid["verts"], cap["verts"], towel["verts"]],
            },
        })
    return sides


# ----------------------------------------------------------------------------- modelling helpers
def finish(obj, bevel=0.0, segments=3, angle=30.0, weighted=True, smooth=True):
    """Hard-surface finish: bevel (angle-limited, hardened normals), weighted normals, smooth by angle."""
    if bevel > 0:
        b = obj.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = segments
        b.limit_method = 'ANGLE'
        b.angle_limit = math.radians(angle)
        b.profile = 0.6
        b.harden_normals = True
    me = obj.data
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    if weighted:
        w = obj.modifiers.new("WeightedNormal", 'WEIGHTED_NORMAL')
        w.keep_sharp = True
        w.weight = 50
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    obj.modifiers.clear()
    old = obj.data
    obj.data = mesh
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def box(name, size, centre, bevel, segments=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = centre
    return finish(o, bevel, segments)


def tube(name, points, radius, resolution=2):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.resolution_u = 5          # 5 segments per span: round at 5-15 m, ~0.4k triangles per frame piece
    cu.bevel_depth = radius
    cu.bevel_resolution = resolution
    cu.fill_mode = 'FULL'
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(points) - 1)
    for bp, p in zip(sp.bezier_points, points):
        bp.co = p
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    o = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(o)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    bpy.data.objects.remove(o, do_unlink=True)
    m = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(m)
    for p in me.polygons:
        p.use_smooth = True
    return m


def lathe(name, profile, steps=20, origin=Vector()):
    """Revolve a (radius, z) profile around Z (bottle)."""
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        ring = [bm.verts.new((r * math.cos(2 * math.pi * i / steps), r * math.sin(2 * math.pi * i / steps), z)) for i in range(steps)]
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for i in range(steps):
            j = (i + 1) % steps
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bottom = bm.faces.new(list(reversed(rings[0])))
    top = bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = origin
    for p in me.polygons:
        p.use_smooth = True
    return finish(o, 0.0, weighted=True)


def soft_slab(name, size, centre, droop=0.0, seed=1):
    """Folded towel: subdivided rounded slab with a soft sag and cloth-like ripples (organic, no hard edges)."""
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=6, y_segments=8, size=0.5)
    for v in bm.verts:
        x, y = v.co.x * size[0], v.co.y * size[1]
        z = -droop * (abs(x) / (size[0] / 2)) ** 2 + 0.003 * math.sin(y * 31 + seed) * math.cos(x * 23 + seed)
        v.co = Vector((x, y, z))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=size[2])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.location = centre
    return finish(o, 0.0, weighted=False)


def paint(obj, host_name, ao_floor):
    """Vertex colour 'Col' (gamma-2 sRGB, ground-darkened like the venue) and metre-scale box UVs."""
    me = obj.data
    col = me.color_attributes.get("Col") or me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
    uv = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    base = srgb(*COLOURS[host_name])
    scale = UV_SCALE.get(host_name, 1.0)
    mw = obj.matrix_world
    nmat = mw.to_3x3().inverted().transposed()
    for poly in me.polygons:
        n = (nmat @ poly.normal).normalized()
        for li in poly.loop_indices:
            w = mw @ me.vertices[me.loops[li].vertex_index].co
            k = 0.80 + 0.20 * min(1.0, max(0.0, (w.z - ao_floor) / 0.5)) if abs(n.z) < 0.7 else 1.0
            s = math.sqrt(k)
            col.data[li].color = (base[0] * s, base[1] * s, base[2] * s, 1.0)
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            if az >= ax and az >= ay:
                u, v = (w.y, w.x) if host_name == "Refined_Venue_Timber" else (w.x, w.y)
            elif ax >= ay:
                u, v = w.y, w.z
            else:
                u, v = w.x, w.z
            uv.data[li].uv = (u / scale, v / scale)
    me.color_attributes.active_color = col
    me.color_attributes.render_color_index = list(me.color_attributes).index(col)


# ----------------------------------------------------------------------------- props (Blender world, Z up)
def bench(side):
    """Slatted courtside bench: crowned oiled-wood slats + one bent powder-coated tube frame per end."""
    c, top, L, out = side["bench"], side["seat_top"], side["length"], side["back_out"]
    wood, metal = [], []
    slat_t, slat_w = 0.042, 0.105
    for i, dx in enumerate((-0.13, 0.0, 0.13)):
        wood.append(box(f"Slat{i}", (slat_w, L, slat_t), Vector((c.x + dx * -out, c.y, top - slat_t / 2)), 0.012, 3))
    # two backrest boards, leaning back 12 degrees
    for i, h in enumerate((0.20, 0.36)):
        b = box(f"Back{i}", (0.032, L, 0.11), Vector((0, 0, 0)), 0.01, 3)
        b.rotation_euler = (0, math.radians(-12 * out), 0)
        b.location = Vector((c.x + out * (0.22 + 0.04 * h / 0.36), c.y, top + h))
        wood.append(b)
    for end in (-1, 1):
        y = c.y + end * (L / 2 - 0.22)
        x0, x1 = c.x - out * 0.2, c.x + out * 0.2
        g = c.z + 0.02
        pts = [Vector((x0, y, g)), Vector((x0 + out * 0.03, y, top - 0.06)), Vector((x1, y, top - 0.06)),
               Vector((x1 + out * 0.06, y, top + 0.42)), ]
        metal.append(tube(f"FrameA{end}", pts, 0.018))
        metal.append(tube(f"FrameB{end}", [Vector((x1, y, g)), Vector((x1 - out * 0.01, y, top - 0.08))], 0.018))
        metal.append(box(f"Foot{end}", (0.46, 0.06, 0.02), Vector((c.x, y, g + 0.01)), 0.008, 2))
    return wood, metal


def cooler(side):
    """Sports cooler: soft rounded body, rounded lid, two side grips (lid/grips off-white)."""
    p = side["cooler"]
    body = box("CoolerBody", (0.44, 0.50, 0.44), Vector((p.x, p.y, p.z + 0.22)), 0.045, 4)
    # slight taper: narrower at the base, like a moulded cooler
    for v in body.data.vertices:
        t = (v.co.z + 0.22) / 0.44
        v.co.x *= 0.94 + 0.06 * t
        v.co.y *= 0.95 + 0.05 * t
    lid = box("CoolerLid", (0.48, 0.55, 0.065), Vector((p.x, p.y, p.z + 0.475)), 0.028, 4)
    grips = [tube(f"Grip{s}", [Vector((p.x - 0.12, p.y + s * 0.27, p.z + 0.36)), Vector((p.x, p.y + s * 0.30, p.z + 0.40)),
                                 Vector((p.x + 0.12, p.y + s * 0.27, p.z + 0.36))], 0.012) for s in (-1, 1)]
    return [body], [lid] + grips


def bottle(side):
    p = side["bottle"]
    profile = [(0.0, 0.0), (0.038, 0.0), (0.045, 0.01), (0.046, 0.16), (0.040, 0.19), (0.024, 0.215), (0.020, 0.235), (0.0, 0.235)]
    body = lathe("Bottle", profile, 14, Vector((p.x, p.y, p.z)))
    cap = lathe("Cap", [(0.0, 0.0), (0.023, 0.0), (0.024, 0.026), (0.018, 0.032), (0.0, 0.032)], 10, Vector((p.x, p.y, p.z + 0.232)))
    return [body], [cap]


def towel(side):
    p = side["towel"]
    return [soft_slab("Towel", (0.30, 0.42, 0.016), Vector((p.x, p.y, p.z + 0.01)), droop=0.006, seed=side["sign"])]


# ----------------------------------------------------------------------------- venue pass
def snapshot(names):
    out = {}
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name.startswith(names):
            out[o.name] = [tuple(round(c, 5) for c in (o.matrix_world @ v.co)) for v in o.data.vertices]
    return out


def remove_parts(obj, groups):
    doomed = {i for g in groups for i in g}
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in doomed], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()


def join_into(host, parts):
    if not parts:
        return
    mat = host.data.materials[0]
    for p in parts:
        p.data.materials.clear()
        p.data.materials.append(mat)
    with bpy.context.temp_override(active_object=host, object=host, selected_objects=[host] + parts,
                                   selected_editable_objects=[host] + parts):
        bpy.ops.object.join()


def build(venue):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SUNSET, venue + "_Sunset.blend"))
    objs = {n: bpy.data.objects[n] for n in HOSTS}
    protected = snapshot(("Court_", "PortableNet"))
    before = {n: len(objs[n].data.polygons) for n in HOSTS}
    sides = classify(objs)
    assert len(sides) == 2, venue + ": expected two courtside sets"
    # Vertex indices were taken before any edit: remove every side's parts from a host in one pass.
    for name in HOSTS:
        remove_parts(objs[name], [g for side in sides for g in side["remove"][name]])
    new = {n: [] for n in HOSTS}
    for side in sides:
        wood, metal = bench(side)
        body, white = cooler(side)
        btl, cap = bottle(side)
        new["Refined_Venue_Timber"] += wood
        new["Refined_Venue_Metal"] += metal
        new["Refined_Venue_Polymer"] += body + btl
        new["Refined_Venue_OffWhite"] += white + cap + towel(side)
        floor = side["bench"].z
        for name, parts in new.items():
            for p in parts:
                if "Col" not in p.data.color_attributes:
                    paint(p, name, floor)
    for name in HOSTS:
        join_into(objs[name], new[name])
        me = objs[name].data
        col = me.color_attributes.get("Col")
        me.color_attributes.active_color = col
        me.color_attributes.render_color_index = list(me.color_attributes).index(col)
    assert snapshot(("Court_", "PortableNet")) == protected, "playing surface / net geometry changed"
    after = {n: len(objs[n].data.polygons) for n in HOSTS}
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, venue + "_ArtDirection.blend"), copy=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    glb = export_glb(os.path.join(VENUE_MODELS, venue + ".glb"), meshes)
    REPORT[venue] = {"polygons_before": before, "polygons_after": after, "glb": glb,
                     "sides": [{"bench": list(map(lambda v: round(v, 3), s["bench"])), "seat_top": round(s["seat_top"], 3)} for s in sides]}
    if PREVIEW:
        render_preview(venue, sides)


def verify(venue):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SUNSET, venue + "_Sunset.blend"))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    tmp = os.path.join(os.environ.get("TEMP", HERE), venue + "_verify.glb")
    REPORT[venue] = export_glb(tmp, meshes)
    REPORT[venue]["shipped_bytes"] = os.path.getsize(os.path.join(VENUE_MODELS, venue + ".glb"))


def render_preview(venue, sides):
    """Close-up Eevee render of one courtside set (Art Bible review image)."""
    s = sides[0]
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} else 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    if hasattr(scene, "eevee"):
        scene.eevee.taa_render_samples = 16
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 40
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    target = s["bench"] + Vector((0, 0.5, 0.45))
    cam.location = target + Vector((-s["back_out"] * -2.4, -2.2, 1.1))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    sun = bpy.data.lights.new("PreviewSun", 'SUN')
    sun.energy = 4.0
    sun.color = (1.0, 0.82, 0.62)
    so = bpy.data.objects.new("PreviewSun", sun)
    so.rotation_euler = (math.radians(60), 0, math.radians(-40))
    scene.collection.objects.link(so)
    world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.55, 0.75, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    for mat in bpy.data.materials:
        if mat.use_nodes and mat.node_tree:
            nodes = mat.node_tree.nodes
            bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bsdf and not any(n.type == 'VERTEX_COLOR' or n.type == 'ATTRIBUTE' for n in nodes):
                attr = nodes.new('ShaderNodeVertexColor')
                attr.layer_name = "Col"
                mat.node_tree.links.new(attr.outputs[0], bsdf.inputs['Base Color'])
    os.makedirs(PREVIEW, exist_ok=True)
    scene.render.filepath = os.path.join(PREVIEW, venue + "_courtside.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    for venue in VENUES:
        (verify if MODE == "verify" else build)(venue)
    print("COURTSIDE_REPORT", json.dumps(REPORT, default=str))
