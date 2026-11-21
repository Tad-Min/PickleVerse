"""Assemble the district, the tree instances and (optionally) a venue for previews and the sun-mask bake.

Used by preview_render.py and bake_sunmask.py:
    from assemble import assemble
    info = assemble(venue="LowPoly"|"Professional"|None, lod=1)

Blender frame of the assembled scene = this project's export convention (Unity x = -X, Unity z = -Y, up = Z).
Venue models are imported from the runtime GLBs and offset exactly as the Unity prefabs do, so every
coordinate here matches the match scene (court surface at Z = 0).
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector

from sd_common import HERE, MODELS, VENUE_MODELS, collection

# Ground level of the district relative to the court surface, per venue (matches the Unity installer).
VENUE_GROUND = {"LowPoly": -0.16, "Professional": -0.47, None: -0.16}
VENUE_FILE = {"LowPoly": "PickleballCourt_LowPoly.glb", "Professional": "ProfessionalStadium.glb"}
# Height of the court surface inside each venue GLB (the prefab moves the model down by this much).
VENUE_COURT_Z = {"LowPoly": 0.0, "Professional": 0.45}


def _append(blend, names):
    with bpy.data.libraries.load(blend, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n in names]
    out = {}
    for o in dst.objects:
        if o is not None:
            out[o.name] = o
    return out


def import_glb(path, coll):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        for c in list(o.users_collection):
            c.objects.unlink(o)
        coll.objects.link(o)
    return new


def assemble(venue=None, lod=1, skip_trees=False):
    blend = os.path.join(HERE, "SunsetDistrict.blend")
    bpy.ops.wm.open_mainfile(filepath=blend)
    scene = bpy.context.scene
    ground = VENUE_GROUND[venue]
    district = [o for o in scene.objects if o.name.startswith("District_")]
    for o in district:
        o.location.z = ground
    info = {"ground": ground, "district": [o.name for o in district]}

    if not skip_trees:
        layout = json.load(open(os.path.join(HERE, "district_layout.json")))
        species = sorted({t["species"] for t in layout["trees"]} | {s["kind"] for s in layout.get("shrubs", [])})
        district = [o for o in scene.objects if o.name.startswith("District_")]
        protos = _append(os.path.join(HERE, "SunsetTrees.blend"), [f"{s}_LOD{lod}" for s in species])
        tc = collection("Trees")
        for p in protos.values():
            p.location = (0, 0, -1000)
            tc.objects.link(p)
            p.hide_render = True
        count = 0
        merged_hedges = any(o.name.startswith("District_Hedges") for o in district)
        extra = [] if merged_hedges else [dict(s, species=s["kind"]) for s in layout.get("shrubs", [])]
        for t in layout["trees"] + extra:
            proto = protos.get(f"{t['species']}_LOD{lod}")
            if not proto:
                continue
            o = bpy.data.objects.new(f"{t['species']}_{count}", proto.data)
            o.location = (-t["x"], -t["y"], ground + t.get("z", 0.0))
            o.rotation_euler = (0, 0, -math.radians(t["yaw"]))
            o.scale = (t["scale"],) * 3
            tc.objects.link(o)
            count += 1
        info["trees"] = count

    if venue:
        vc = collection("Venue")
        objs = import_glb(os.path.join(VENUE_MODELS, VENUE_FILE[venue]), vc)
        roots = [o for o in objs if o.parent is None]
        for r in roots:
            r.location.z -= VENUE_COURT_Z[venue]
        # the old blob lawn is replaced by the district
        for o in objs:
            if o.name.startswith("Refined_Garden_Grass"):
                o.hide_render = True
                o.hide_set(True)
        info["venue"] = [o.name for o in objs if o.type == 'MESH']
    return info
