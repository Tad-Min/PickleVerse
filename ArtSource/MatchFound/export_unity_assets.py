"""Export the match-found stage assets for Unity (FBX meshes; textures are copied separately).

blender --background --factory-startup --python export_unity_assets.py -- <out_dir>
Writes PV_Paddle_Elongated.fbx (3 material slots: atlas, guard, cap) and PV_VS_Ball.fbx
(3 material slots: ball, VS ink, VS rim; radius 1, "VS" printed on the -Y side in Blender).
"""
import bpy, math, os, sys
from mathutils import Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
OUT = sys.argv[sys.argv.index("--") + 1]
os.makedirs(OUT, exist_ok=True)
ASSETS = os.path.normpath(os.path.join(HERE, "..", "..", "My project", "Assets"))

# ---------------------------------------------------------------- paddle (shared builder)
sys.argv = [sys.argv[0]]          # build_paddle reads its own optional args
import build_paddle               # noqa: E402  (builds the scene and writes the GLB on import)

paddle = bpy.data.objects["PV Paddle Elongated"]


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH"},
                             use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False,
                             path_mode="STRIP", embed_textures=False)
    print("FBX", path, os.path.getsize(path) // 1024, "KB")


export_fbx(paddle, os.path.join(OUT, "PV_Paddle_Elongated.fbx"))

# ---------------------------------------------------------------- VS ball
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.join(ASSETS, "Models", "ball", "pickleball_ball_cartoon.glb"))
ball = bpy.context.selected_objects[0]
ball.name = "PV VS Ball"
for p in ball.data.polygons:
    p.use_smooth = True


def material(name, rgb, rough):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    p = m.node_tree.nodes.get("Principled BSDF")
    if p:
        p.inputs["Base Color"].default_value = (*rgb, 1)
        p.inputs["Roughness"].default_value = rough
    return m


ball.data.materials.clear()
ball.data.materials.append(material("PV Ball", (1.0, 0.92, 0.1), 0.4))
ink = material("PV VS Ink", (0.03, 0.045, 0.075), 0.35)
rim = material("PV VS Rim", (1.0, 0.93, 0.8), 0.3)
font = bpy.data.fonts.load(os.path.join(ASSETS, "Fonts", "ArialBoldItalic.ttf"))


def vs_layer(name, mat, offset, depth):
    cu = bpy.data.curves.new(name, "FONT")
    cu.body, cu.font, cu.size, cu.align_x, cu.align_y, cu.extrude, cu.offset = "VS", font, 0.74, "CENTER", "CENTER", 0.0, offset
    cu.resolution_u = 3          # light decal: a flat printed label, no extrusion
    t = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(t)
    t.rotation_euler = (math.radians(90), 0, 0)
    t.location = (0.02, -1.25, 0.0)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(t.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.matrix_world = t.matrix_world.copy()
    bpy.data.objects.remove(t)
    me.materials.clear()
    me.materials.append(mat)
    sw = o.modifiers.new("Wrap", "SHRINKWRAP")
    sw.target, sw.wrap_method, sw.wrap_mode, sw.offset = ball, "PROJECT", "OUTSIDE_SURFACE", depth
    sw.use_project_z = True
    sw.use_negative_direction = sw.use_positive_direction = True
    bpy.context.view_layer.objects.active = o
    for mod in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o


layers = [vs_layer("VS Rim", rim, 0.045, 0.006), vs_layer("VS Ink", ink, 0.0, 0.02)]
bpy.ops.object.select_all(action="DESELECT")
for o in layers:
    o.select_set(True)
ball.select_set(True)
bpy.context.view_layer.objects.active = ball
bpy.ops.object.join()
tris = sum(len(p.vertices) - 2 for p in ball.data.polygons)
print("BALL tris", tris, "materials", [m.name for m in ball.data.materials])
export_fbx(ball, os.path.join(OUT, "PV_VS_Ball.fbx"))
