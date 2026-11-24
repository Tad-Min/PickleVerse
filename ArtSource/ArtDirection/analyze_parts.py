"""Histogram of loose-part sizes of one object in a .blend (finds repeated props such as stadium seats).

blender --background <file.blend> --python analyze_parts.py -- <object name> <out.json>
"""
import collections
import json
import sys

import bmesh
import bpy

name, out = sys.argv[sys.argv.index("--") + 1:][:2]
o = bpy.data.objects[name]
bm = bmesh.new()
bm.from_mesh(o.data)
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()
mw = o.matrix_world
seen, parts = set(), []
for v in bm.verts:
    if v.index in seen:
        continue
    stack, comp = [v], []
    seen.add(v.index)
    while stack:
        x = stack.pop()
        comp.append(x)
        for e in x.link_edges:
            y = e.other_vert(x)
            if y.index not in seen:
                seen.add(y.index)
                stack.append(y)
    pts = [mw @ x.co for x in comp]
    mn = [min(p[k] for p in pts) for k in range(3)]
    mx = [max(p[k] for p in pts) for k in range(3)]
    faces = {f.index for x in comp for f in x.link_faces}
    mats = collections.Counter(bm.faces[i].material_index for i in faces)
    parts.append({"min": mn, "max": mx, "verts": len(comp), "faces": len(faces), "mat": mats.most_common(1)[0][0] if mats else -1})
hist = collections.Counter((round(p["max"][0] - p["min"][0], 2), round(p["max"][1] - p["min"][1], 2), round(p["max"][2] - p["min"][2], 2), p["verts"], p["mat"]) for p in parts)
json.dump({"materials": [m.name for m in o.data.materials], "parts": parts,
           "hist": [[list(k), c] for k, c in hist.most_common()]}, open(out, "w"))
print("PARTS", len(parts), "MATERIALS", [m.name for m in o.data.materials])
for k, c in hist.most_common(25):
    print("  size", k[:3], "verts", k[3], "mat", k[4], "x", c)
