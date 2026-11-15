"""Material/detail pass on the two match venues + sculpted light masts.

    blender --background --python upgrade_courts.py

Reads the original sources ArtSource/Stadiums/{ProfessionalStadium,PickleballCourt_LowPoly}.blend (never
saved over) and writes:
  SunsetDistrict/<name>_Sunset.blend            edited copies
  My project/Assets/Models/Stadium_Enhanced/<name>.glb   runtime models (same paths, same object names)
  My project/Assets/Models/Stadium_Enhanced/District/VenueFixtures.glb   mast + luminaire meshes

Geometry of the playing surface, lines and net is left vertex-for-vertex unchanged: only materials,
vertex colours and UVs are rewritten. The old blob lawn and old park plants are dropped from the export
(the district replaces them).
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bmesh
import bpy
from mathutils import Vector

from sd_common import *   # noqa: F401,F403

SOURCES = os.path.abspath(os.path.join(HERE, ".."))

# old material -> (new material, sRGB colour, uv mode, uv scale in metres per tile)
P = {
    # playing surfaces
    "Court_Blue": ("SD_Acrylic", (0.15, 0.39, 0.60), "box", 3.0),
    "Court_Green": ("SD_Acrylic", (0.21, 0.42, 0.33), "box", 3.0),
    "Apron_Navy": ("SD_Acrylic", (0.16, 0.22, 0.34), "box", 3.0),
    "Line_White": ("SD_Acrylic", (0.96, 0.96, 0.94), "box", 3.0),
    "White": ("SD_Acrylic", (0.96, 0.96, 0.94), "box", 3.0),
    "Slab_Edge": ("SD_Concrete", (0.80, 0.78, 0.75), "box", 2.0),
    "Barrier_Navy": ("SD_Paint", (0.14, 0.18, 0.28), "box", 2.0),
    # net
    "Net_Weave": ("SD_Net", (1.0, 1.0, 1.0), "vertical", 0.30),
    "Net_Frame": ("SD_Paint", (0.06, 0.06, 0.07), "box", 1.0),
    "Net_Binding": ("SD_Paint", (0.95, 0.95, 0.93), "box", 1.0),
    "Net_Tape_White": ("SD_Paint", (0.95, 0.95, 0.93), "box", 1.0),
    # fence / metal
    "ChainLink": ("SD_ChainLink", (0.30, 0.36, 0.34), "vertical", 0.48),
    "Fence_Steel": ("SD_Paint", (0.14, 0.18, 0.17), "box", 1.0),
    "Steel_Highlight": ("SD_Metal", (0.74, 0.75, 0.76), "box", 1.0),
    "Rail_Black": ("SD_Paint", (0.07, 0.07, 0.08), "box", 1.0),
    "Silver": ("SD_Metal", (0.80, 0.81, 0.82), "box", 1.0),
    "Venue_Metal": ("SD_Metal", (0.56, 0.58, 0.59), "box", 1.0),
    # hardscape
    "Venue_Paving": ("SD_Paving", (0.93, 0.89, 0.83), "box", 2.4),
    "Concrete": ("SD_Concrete", (0.82, 0.80, 0.77), "box", 2.0),
    "Planter_Charcoal": ("SD_Concrete", (0.30, 0.30, 0.31), "box", 1.0),
    # furniture / props
    "Venue_Timber": ("SD_Wood", (0.95, 0.88, 0.80), "plank", 1.2),
    "Venue_Polymer": ("SD_Paint", (0.18, 0.54, 0.60), "box", 1.0),
    "Venue_OffWhite": ("SD_Paint", (0.93, 0.92, 0.89), "box", 1.0),
    "Bench_Plastic_White": ("SD_Paint", (0.93, 0.93, 0.92), "box", 1.0),
    "Seat_Blue": ("SD_Paint", (0.15, 0.37, 0.62), "box", 1.0),
    "Camera_Charcoal": ("SD_Paint", (0.11, 0.11, 0.12), "box", 1.0),
    "Lens_Blue": ("SD_Metal", (0.10, 0.17, 0.30), "box", 1.0),
    "Leaf_Light": ("SD_Paint", (0.45, 0.62, 0.26), "box", 1.0),
    "Leaf_Green": ("SD_Paint", (0.21, 0.40, 0.16), "box", 1.0),
    "Soil": ("SD_Paint", (0.24, 0.17, 0.11), "box", 1.0),
    "Petal_White": ("SD_Paint", (0.96, 0.95, 0.91), "box", 1.0),
}
KEEP_MATERIALS = {"Venue_Signage"}         # bound in Unity to the existing Refined_Venue_Signage material
DROP_OBJECT_PREFIXES = ("Refined_Garden_Grass", "Preview_", "Broadleaf_", "Shrub_", "GrassPatch", "Grass_", "Park_")

REPORT = {}


def principled_color(mat):
    """Fallback colour from the authored node tree (Mix RGBA B input, then Base Color)."""
    try:
        bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        sock = bsdf.inputs['Base Color']
        if sock.is_linked and sock.links[0].from_node.type == 'MIX':
            c = sock.links[0].from_node.inputs[7].default_value
        else:
            c = sock.default_value
        lin = lambda v: v ** (1 / 2.2)
        return (lin(c[0]), lin(c[1]), lin(c[2]))
    except Exception:
        return (0.8, 0.8, 0.8)


def project_uv(co, n, mode, scale, axis_long):
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    if mode == "vertical":
        t = Vector((-n.y, n.x, 0.0))
        if t.length < 1e-4:
            t = Vector((1, 0, 0))
        t.normalize()
        return (co.dot(t) / scale, co.z / scale)
    if mode == "plank":        # planks run along the object's long horizontal axis
        if az >= ax and az >= ay:
            return ((co.y if axis_long == 1 else co.x) / scale, (co.x if axis_long == 1 else co.y) / scale)
        if ax >= ay:
            return (co.y / scale, co.z / scale)
        return (co.x / scale, co.z / scale)
    if az >= ax and az >= ay:
        return (co.x / scale, co.y / scale)
    if ax >= ay:
        return (co.y / scale, co.z / scale)
    return (co.x / scale, co.z / scale)


def rematerial(obj):
    me = obj.data
    if me.users > 1:
        obj.data = me = me.copy()
    old = [s.material.name if s.material else None for s in obj.material_slots]
    targets, colours, modes = [], [], []
    for name in old:
        key = name.split(".")[0] if name else None
        if key in KEEP_MATERIALS:
            targets.append(key)
            colours.append((1.0, 1.0, 1.0))
            modes.append(("keep", 1.0))
        elif key in P:
            t, c, m, sc = P[key]
            targets.append(t)
            colours.append(c)
            modes.append((m, sc))
        else:
            targets.append("SD_Paint")
            colours.append(principled_color(bpy.data.materials[name]) if name else (0.8, 0.8, 0.8))
            modes.append(("box", 1.0))
            REPORT.setdefault("unmapped", []).append(name)
    unique = []
    for t in targets:
        if t not in unique:
            unique.append(t)
    mw = obj.matrix_world
    nmat = mw.to_3x3().inverted().transposed()
    bb = [mw @ Vector(c) for c in obj.bound_box]
    axis_long = 1 if (max(v.y for v in bb) - min(v.y for v in bb)) > (max(v.x for v in bb) - min(v.x for v in bb)) else 0
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for layer in list(bm.loops.layers.float_color):
        if layer.name != "Col":
            bm.loops.layers.float_color.remove(layer)
    col = bm.loops.layers.float_color.get("Col") or bm.loops.layers.float_color.new("Col")
    for layer in list(bm.loops.layers.color):
        bm.loops.layers.color.remove(layer)
    new_index = []
    for f in bm.faces:
        idx = min(f.material_index, len(targets) - 1)
        c = srgb(*colours[idx])
        mode, scale = modes[idx]
        n = (nmat @ f.normal).normalized()
        # subtle ambient-occlusion style darkening towards the ground on vertical faces
        for loop in f.loops:
            w = mw @ loop.vert.co
            k = 1.0
            if abs(n.z) < 0.6 and targets[idx] in ("SD_Concrete", "SD_Paint", "SD_Metal"):
                k = 0.78 + 0.22 * min(1.0, max(0.0, (w.z + 0.1) / 0.6))
            loop[col] = (c[0] * math.sqrt(k), c[1] * math.sqrt(k), c[2] * math.sqrt(k), 1.0)
            if mode != "keep":
                loop[uv].uv = project_uv(w, n, mode, scale, axis_long)
        new_index.append(unique.index(targets[idx]))
    bm.to_mesh(me)
    bm.free()
    # materials.clear() resets every polygon to slot 0, so assign the new slots afterwards.
    me.materials.clear()
    for t in unique:
        me.materials.append(bpy.data.materials.get(t) if t in KEEP_MATERIALS else material(t))
    me.polygons.foreach_set("material_index", new_index)
    attr = me.color_attributes.get("Col")
    me.color_attributes.active_color = attr
    me.color_attributes.render_color_index = list(me.color_attributes).index(attr)
    me.update()
    return old, unique


def upgrade(name):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SOURCES, name + ".blend"))
    scene = bpy.context.scene
    dropped = []
    for o in list(scene.objects):
        if o.name.startswith(DROP_OBJECT_PREFIXES) or (o.type == 'MESH' and o.data.materials and
                                                       all(m and m.name.split(".")[0] in ("Bark", "Leaf", "Grass") for m in o.data.materials)):
            dropped.append(o.name)
            bpy.data.objects.remove(o, do_unlink=True)
    rows = {}
    keep = []
    for o in scene.objects:
        if o.type != 'MESH':
            keep.append(o)
            continue
        verts_before = [tuple(round(c, 6) for c in (o.matrix_world @ v.co)) for v in o.data.vertices] if o.name.startswith(("Court_", "PortableNet")) else None
        old, new = rematerial(o)
        if verts_before is not None:
            after = [tuple(round(c, 6) for c in (o.matrix_world @ v.co)) for v in o.data.vertices]
            assert after == verts_before, "playing-surface geometry changed: " + o.name
        rows[o.name] = {"before": old, "after": new}
        keep.append(o)
    out_blend = os.path.join(HERE, name + "_Sunset.blend")
    bpy.ops.wm.save_as_mainfile(filepath=out_blend, relative_remap=True, copy=True)
    glb = export_glb(os.path.join(VENUE_MODELS, name + ".glb"), [o for o in keep if o.type == 'MESH'])
    REPORT[name] = {"dropped": len(dropped), "objects": rows, "glb": glb}


# ----------------------------------------------------------------------------- sculpted masts
def mast(height, scale_xz=0.1):
    """Lyre-topped light mast in world metres (plan frame), base at z = -0.2, luminaire centre at `height`."""
    m = Mesh("Mast_%d" % round(height * 10))
    dark, bronze = srgb(0.17, 0.18, 0.19), srgb(0.55, 0.42, 0.26)
    # fluted plinth
    m.tube([(0, 0, -0.20), (0, 0, 0.10), (0, 0, 0.22), (0, 0, 0.40), (0, 0, 0.48)], [0.17, 0.17, 0.15, 0.105, 0.09], 12, "SD_Paint", dark, cap_end=False)
    # tapering shaft with entasis
    shaft = [(0, 0, 0.48 + (height - 0.95) * t) for t in (0.0, 0.25, 0.5, 0.75, 1.0)]
    m.tube(shaft, [0.085, 0.080, 0.072, 0.064, 0.056], 10, "SD_Paint", dark, cap_end=False)
    for z, r in ((0.52, 0.10), (2.4, 0.083), (height - 1.2, 0.068)):      # bronze collars
        m.tube([(0, 0, z - 0.035), (0, 0, z), (0, 0, z + 0.035)], [r * 0.95, r * 1.18, r * 0.95], 10, "SD_Metal", bronze, cap_end=False)
    top = height - 0.47
    m.tube([(0, 0, top - 0.02), (0, 0, top + 0.05), (0, 0, top + 0.09)], [0.056, 0.075, 0.02], 10, "SD_Metal", bronze, cap_end=False)
    # lyre: two arms leave the shaft, flare out and curl back up to hold the luminaire ends
    for s in (-1, 1):
        arm = catmull([(0.0, 0.0, top - 0.25), (s * 0.10, 0.0, top + 0.02), (s * 0.30, 0.0, top + 0.14), (s * 0.40, 0.0, top + 0.32), (s * 0.37, 0.0, height - 0.06)], 4)
        m.tube(arm, [0.026] * len(arm), 6, "SD_Paint", dark, cap_end=True, cap_start=True)
        curl = catmull([(s * 0.30, 0.0, top + 0.14), (s * 0.20, 0.0, top + 0.26), (s * 0.24, 0.0, top + 0.36), (s * 0.31, 0.0, top + 0.31)], 3)
        m.tube(curl, [0.014] * len(curl), 5, "SD_Metal", bronze, cap_end=True, cap_start=True)
    return m


def luminaire():
    """Slim rounded LED head: lens (submesh 0, switched day/night in Unity) + finned body (submesh 1)."""
    m = Mesh("Luminaire")
    dark = srgb(0.16, 0.17, 0.18)
    lens_c = srgb(0.96, 0.95, 0.92)
    outline = rounded_rect(0, 0, 0.42, 0.20, 0.08, 4)
    inner = rounded_rect(0, 0, 0.38, 0.165, 0.05, 4)
    # lens first so it becomes submesh 0
    m.fan_xy(inner[::-1], -0.055, "SD_Glow", lens_c, centre=(0, 0))
    m.strip_xy(inner[::-1], outline[::-1], -0.065, "SD_Paint", dark, closed=True)
    m.wall(inner, -0.065, -0.055, "SD_Paint", dark, closed=True, flip=True)
    m.wall(outline, -0.065, 0.045, "SD_Paint", dark, closed=True, smooth=True)
    top = rounded_rect(0, 0, 0.40, 0.18, 0.07, 4)
    m.strip_xy(top, outline, 0.045, "SD_Paint", dark, closed=True)
    m.fan_xy(top, 0.06, "SD_Paint", dark, centre=(0, 0))
    m.wall(top, 0.045, 0.06, "SD_Paint", dark, closed=True)
    for k in range(7):     # cooling fins
        x = -0.30 + 0.10 * k
        m.box(x - 0.008, -0.15, 0.06, x + 0.008, 0.15, 0.08, "SD_Paint", dark, top=True)
    return m


def fixtures():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    coll = collection("VenueFixtures")
    objs = []
    # Prefab transforms these meshes are authored for (centre y, scale) -> mesh is divided by the scale.
    specs = [("Mast_LowPoly", mast(6.80), 3.4, (0.1, 6.8, 0.1)), ("Mast_Professional", mast(7.80), 3.9, (0.1, 7.8, 0.1))]
    for name, mesh_, centre_y, sc in specs:
        mesh_.name = name
        mesh_.verts = [(v[0] / sc[0], v[1] / sc[2], (v[2] - centre_y) / sc[1]) for v in mesh_.verts]
        # normals are recomputed by Blender from the scaled geometry; Unity re-applies the scale, so
        # pre-transform them with the inverse-transpose to keep shading round.
        o = mesh_.object(coll)
        objs.append(o)
    lum = luminaire()
    lum.name = "Luminaire"
    lum.verts = [(v[0] / 0.85, v[1] / 0.42, v[2] / 0.16) for v in lum.verts]
    objs.append(lum.object(coll))
    finish_objects(objs)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "VenueFixtures.blend"))
    REPORT["fixtures"] = export_glb(os.path.join(MODELS, "VenueFixtures.glb"), objs)


if __name__ == "__main__":
    for venue in ("PickleballCourt_LowPoly", "ProfessionalStadium"):
        upgrade(venue)
    fixtures()
    with open(os.path.join(HERE, "upgrade_report.json"), "w") as f:
        json.dump(REPORT, f, indent=1)
    print("UPGRADE_REPORT", json.dumps({k: (v.get("glb") if isinstance(v, dict) and "glb" in v else v) for k, v in REPORT.items() if k != "unmapped"}))
    print("UNMAPPED", REPORT.get("unmapped"))
