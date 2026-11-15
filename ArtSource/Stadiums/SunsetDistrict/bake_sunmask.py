"""Bake the golden-hour sun visibility of all ground surfaces into a top-down mask (Cycles, direct sun only).

    blender --background --python bake_sunmask.py -- [LowPoly|Professional] [resolution]

The static district, its trees and the venue are assembled exactly as in Unity; ground surfaces are the
only camera-visible objects and everything else only casts shadows (alpha-tested leaves included).
Output: Assets/EnvironmentContent/SunsetDistrict/Textures/SD_SunMask_<venue>.png
UV in Unity: (0.5 + x / SPAN, 0.5 + z / SPAN), SPAN = sd_common.MASK_SPAN, centred on the court.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Vector

from assemble import assemble
from sd_common import MASK_SPAN, TEXTURES, TO_SUN

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
VENUE = argv[0] if argv else "LowPoly"
RES = int(argv[1]) if len(argv) > 1 else 1024
RECEIVER_PREFIXES = ("District_Grass", "District_Paving", "District_Asphalt", "District_Acrylic", "District_Water",
                     "Court_", "Refined_Venue_Paving")

assemble(VENUE, lod=1)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
cy = scene.cycles
cy.samples = 48
cy.use_denoising = False
cy.max_bounces = 16
cy.diffuse_bounces = 0
cy.glossy_bounces = 0
cy.transmission_bounces = 0
cy.transparent_max_bounces = 24
cy.caustics_reflective = cy.caustics_refractive = False
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for dev_type in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = dev_type
            prefs.get_devices()
            if any(d.type == dev_type for d in prefs.devices):
                for d in prefs.devices:
                    d.use = d.type == dev_type
                cy.device = 'GPU'
                break
        except Exception:
            continue
except Exception:
    pass
scene.render.resolution_x = scene.render.resolution_y = RES
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0.0
scene.render.image_settings.file_format = 'OPEN_EXR'
scene.render.image_settings.color_depth = '32'

world = bpy.data.worlds.new("Black")
world.color = (0, 0, 0)
scene.world = world

white = bpy.data.materials.new("BakeWhite")
white.use_nodes = True
bsdf = white.node_tree.nodes.get("Principled BSDF")
bsdf.inputs['Base Color'].default_value = (1, 1, 1, 1)
bsdf.inputs['Roughness'].default_value = 1.0
bsdf.inputs['Specular IOR Level'].default_value = 0.0

receivers = 0
for o in scene.objects:
    if o.type != 'MESH':
        continue
    if o.name.startswith(RECEIVER_PREFIXES):
        o.visible_camera = True
        o.data = o.data.copy()
        o.data.materials.clear()
        o.data.materials.append(white)
        receivers += 1
    else:
        o.visible_camera = False
        if o.name.startswith("District_Skyline"):
            o.visible_shadow = False      # distant backdrop card, never a shadow caster
        # Keep the authored alpha for leaves; make everything else an opaque blocker.
        if o.data.materials and all(m and m.name not in ("SD_Foliage", "SD_ChainLink", "SD_Net", "SD_Impostor") for m in o.data.materials):
            pass

for o in [o for o in scene.objects if o.type == 'LIGHT']:
    bpy.data.objects.remove(o)
sun_data = bpy.data.lights.new("BakeSun", 'SUN')
sun_data.energy = 1.0
sun_data.angle = math.radians(1.0)
sun = bpy.data.objects.new("BakeSun", sun_data)
scene.collection.objects.link(sun)
sun_dir = Vector((-TO_SUN.x, -TO_SUN.y, TO_SUN.z))       # plan -> Blender frame
sun.rotation_euler = Vector((0, 0, 1)).rotation_difference(sun_dir).to_euler()

cam_data = bpy.data.cameras.new("MaskCam")
cam_data.type = 'ORTHO'
cam_data.ortho_scale = MASK_SPAN
cam_data.clip_start = 1
cam_data.clip_end = 400
cam = bpy.data.objects.new("MaskCam", cam_data)
scene.collection.objects.link(cam)
cam.location = (0, 0, 200)
cam.rotation_euler = (0, 0, 0)
scene.camera = cam

tmp = os.path.join(os.path.dirname(__file__), "_impostor", f"mask_{VENUE}.exr")
scene.render.filepath = tmp
bpy.ops.render.render(write_still=True)

img = bpy.data.images.load(tmp)
raw = np.array(img.pixels[:], dtype=np.float32).reshape(RES, RES, 4)
alpha = raw[..., 3]
px = raw[..., 0] / np.maximum(alpha, 1e-6)          # un-premultiply partial-coverage edge pixels
seen = alpha > 0.5
lit = np.percentile(px[seen & (px > 1e-4)], 99.0) if np.any(seen & (px > 1e-4)) else 1.0
mask = np.clip(px / lit, 0.0, 1.0)
# Pixels that saw no receiver (outside the ground) stay lit.
mask[~seen] = 1.0
# Blender image rows run bottom-up with +X right = Unity -x and +Y up = Unity -z: rotate 180 degrees so
# that texture u grows with Unity x and v with Unity z.
mask = mask[::-1, ::-1]
out = bpy.data.images.new(f"SD_SunMask_{VENUE}", RES, RES, alpha=False, float_buffer=False)
out.colorspace_settings.name = 'Non-Color'
rgba = np.ones((RES, RES, 4), dtype=np.float32)
rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = mask
out.pixels.foreach_set(rgba.ravel())
path = os.path.join(TEXTURES, f"SD_SunMask_{VENUE}.png")
out.filepath_raw = path
out.file_format = 'PNG'
out.save()
print("SUNMASK", VENUE, path, "receivers", receivers, "lit_ref", float(lit), "shadowed_fraction", float((mask < 0.5).mean()))
