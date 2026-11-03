"""Run with Blender --background --python scripts/BuildAudienceModels.py.

Create skinned crowd LODs from the supplied GLBs. Source models are never changed.
The original skeleton, vertex groups and material colours are retained.
"""
import json
from pathlib import Path
import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "My project/Assets/Models/Characters"
OUTPUT = ROOT / "artifacts/audience-20261031/Exports"
OUTPUT.mkdir(parents=True, exist_ok=True)
reports = []


def smooth(mesh):
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    # Imported split normals describe the original dense triangles, not the
    # simplified surface. Recompute them instead of exporting fractured shading.
    mesh.normals_split_custom_set_from_vertices([(0.0, 0.0, 0.0)] * len(mesh.vertices))
    mesh.update()


for name in ["PlayBoy", "T1-girl", "Wibu", "YellowHairGirl"]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE / (name + ".glb")), merge_vertices=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    counts = [sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes]
    before = sum(counts)
    levels = [0, 0]
    for obj, triangles in zip(meshes, counts):
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        # Some supplied dense meshes have coincident, disconnected vertices.
        # Weld in bind space while retaining loop UVs and deform-weight layers.
        editable = bmesh.new()
        editable.from_mesh(obj.data)
        bmesh.ops.remove_doubles(editable, verts=list(editable.verts), dist=0.000001)
        bmesh.ops.recalc_face_normals(editable, faces=list(editable.faces))
        editable.to_mesh(obj.data)
        editable.free()
        smooth(obj.data)
        target = max(1200, round(24000 * triangles / before))
        if triangles > target:
            modifier = obj.modifiers.new("Crowd silhouette budget", 'DECIMATE')
            modifier.ratio = target / triangles
            # Simplify the bind-pose mesh, before skin deformation. Applying a
            # modifier after Armature can bake a posed body and deform it twice.
            bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        smooth(obj.data)
        obj.name += "_LOD0"
        levels[0] += sum(len(p.vertices) - 2 for p in obj.data.polygons)
        low = obj.copy()
        low.data = obj.data.copy()
        low.name = obj.name.replace("_LOD0", "_LOD1")
        bpy.context.collection.objects.link(low)
        bpy.context.view_layer.objects.active = low
        modifier = low.modifiers.new("Distant crowd budget", 'DECIMATE')
        modifier.ratio = .28
        bpy.ops.object.modifier_move_to_index(modifier=modifier.name, index=0)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        smooth(low.data)
        levels[1] += sum(len(p.vertices) - 2 for p in low.data.polygons)
    for image in bpy.data.images:
        if image.size[0] and max(image.size) > 1024:
            ratio = 1024 / max(image.size)
            image.scale(max(1, round(image.size[0] * ratio)), max(1, round(image.size[1] * ratio)))
            image.pack()
    path = OUTPUT / (name + "_Crowd.glb")
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', export_animations=False,
                              export_cameras=False, export_lights=False, export_yup=True)
    reports.append(dict(model=name, source_triangles=before, lod_triangles=levels, bytes=path.stat().st_size))
    print("AUDIENCE_MODEL " + json.dumps(reports[-1]), flush=True)
report = ROOT / 'artifacts/audience-20261031/model-budgets.json'
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps(reports, indent=2), encoding='utf-8')
