"""Reports the loose parts (one per prop) of the courtside furniture meshes in a venue source .blend.

blender --background <venue>_Sunset.blend --python analyze_courtside.py -- <out.json>
Coordinates are Blender world space (metres, Z up) of the evaluated mesh.
"""
import json
import sys

import bmesh
import bpy

NAMES = ("Refined_Venue_Timber", "Refined_Venue_Polymer", "Refined_Venue_OffWhite", "Refined_Venue_Metal")
out = sys.argv[sys.argv.index("--") + 1]
report = {}
for name in NAMES:
    o = bpy.data.objects.get(name)
    if not o:
        report[name] = None
        continue
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bm.transform(o.matrix_world)
    bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            comp.append(x.co.copy())
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        mn = [min(c[i] for c in comp) for i in range(3)]
        mx = [max(c[i] for c in comp) for i in range(3)]
        parts.append({"min": [round(a, 3) for a in mn], "max": [round(a, 3) for a in mx], "verts": len(comp)})
    report[name] = {"parts": parts, "materials": [m.name for m in o.data.materials if m],
                    "colors": list(o.data.color_attributes.keys()), "polys": len(o.data.polygons)}
    bm.free()
json.dump(report, open(out, "w"), indent=1)
print("ANALYZE_DONE", out)
