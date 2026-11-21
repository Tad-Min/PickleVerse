"""Golden-hour Eevee previews of the assembled district (authoring check, not the game renderer).

    blender --background --python preview_render.py -- <out_dir> [LowPoly|Professional|None] [label]
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector

from assemble import assemble
from sd_common import TO_SUN

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(__file__), "_preview")
VENUE = None if len(argv) < 2 or argv[1] == "None" else argv[1]
LABEL = argv[2] if len(argv) > 2 else "preview"
os.makedirs(OUT, exist_ok=True)

assemble(VENUE, lod=0)
scene = bpy.context.scene
engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
try:
    scene.view_settings.look = 'AgX - Punchy'
except Exception:
    pass
try:
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_shadows = True
    scene.eevee.shadow_ray_count = 2
except Exception:
    pass

# Sun: Blender direction = plan direction rotated 180 degrees about Z.
sun_dir = Vector((-TO_SUN.x, -TO_SUN.y, TO_SUN.z))
sun = bpy.data.objects.get("Preview_GoldenSun")
sun.data.energy = 4.2
sun.data.color = (1.0, 0.70, 0.42)
sun.data.angle = math.radians(1.2)
sun.rotation_euler = Vector((0, 0, 1)).rotation_difference(sun_dir).to_euler()

world = bpy.data.worlds.new("GoldenHour")
scene.world = world
world.use_nodes = True
nt = world.node_tree
bg = nt.nodes['Background']
grad = nt.nodes.new('ShaderNodeTexGradient')
coord = nt.nodes.new('ShaderNodeTexCoord')
sep = nt.nodes.new('ShaderNodeSeparateXYZ')
ramp = nt.nodes.new('ShaderNodeValToRGB')
nt.links.new(coord.outputs['Generated'], sep.inputs[0])
nt.links.new(sep.outputs['Z'], ramp.inputs['Fac'])
ramp.color_ramp.elements[0].position = 0.48
ramp.color_ramp.elements[0].color = (1.0, 0.62, 0.36, 1)
ramp.color_ramp.elements[1].position = 0.75
ramp.color_ramp.elements[1].color = (0.30, 0.45, 0.72, 1)
nt.links.new(ramp.outputs['Color'], bg.inputs['Color'])
bg.inputs['Strength'].default_value = 0.55

cam_data = bpy.data.cameras.new("PreviewCam")
cam = bpy.data.objects.new("PreviewCam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam_data.clip_end = 600


def shot(name, pos_unity, target_unity, fov=56):
    # Unity (x, y, z) -> Blender (-x, -z, y)
    p = Vector((-pos_unity[0], -pos_unity[2], pos_unity[1]))
    t = Vector((-target_unity[0], -target_unity[2], target_unity[1]))
    cam.location = p
    cam.rotation_euler = (t - p).to_track_quat('-Z', 'Y').to_euler()
    cam_data.sensor_fit = 'VERTICAL'
    cam_data.angle_y = math.radians(fov)
    scene.render.filepath = os.path.join(OUT, f"{LABEL}-{name}.png")
    bpy.ops.render.render(write_still=True)


shot("Gameplay", (1.45, 5.3, -11.5), (1.45, 0.6, -0.5), 56)
shot("Establishing", (-34, 17, -38), (0, 0, 4), 50)
shot("PlayerEye", (0, 1.75, -3.2), (0.4, 1.9, 30), 60)
shot("Aerial", (24, 8, -24), (5, 1, -3), 57)
shot("Trees", (-9.5, 1.7, -13.5), (-14.5, 3.2, -4), 52)
shot("Overview", (0, 150, -120), (0, 0, 0), 40)
print("PREVIEW_DONE", OUT)
