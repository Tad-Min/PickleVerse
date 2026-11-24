"""PickleVerse VFX — Gemstone paddle "Royal Court Sparkle" (Blender 5.1, script-only).

    blender --background --factory-startup --python build_gemstone_vfx.py -- flipbook <Assets/VFX/PaddleSkins/Gemstone/Textures>
    blender --background --factory-startup --python build_gemstone_vfx.py -- preview  <frames_dir> [frames]

flipbook: renders the diamond star-glint animation (16 frames, 128 px, transparent, bloom) and packs it into a 4x4 atlas
          T_Gemstone_StarGlint.png for the Unity particle sheet; also writes the faceted ribbon T_Gemstone_Ribbon.png.
preview:  renders the review animation (1280x720 PNG frames f_0001…): the Gemstone paddle strikes the ball, a champagne-gold
          to emerald faceted ribbon follows it, diamond and emerald star glints twinkle along the path and the ball glitters.
Palette (Royal Court paddle): champagne gold (0.95, 0.80, 0.55), emerald (0.10, 0.85, 0.52), diamond white (0.92, 0.96, 1.0).
"""
import math
import os
import random
import sys

import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
MODE, OUT = ARGS[0], os.path.abspath(ARGS[1])
HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.abspath(os.path.join(HERE, "..", "..", "..", "My project", "Assets"))
GOLD, EMERALD, DIAMOND = (0.95, 0.80, 0.55), (0.10, 0.85, 0.52), (0.92, 0.96, 1.0)
os.makedirs(OUT, exist_ok=True)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 16
    scene.view_settings.view_transform = "Standard"
    return scene


def bloom(scene, strength=0.55, threshold=0.6, size=0.7):
    try:
        tree = bpy.data.node_groups.new("Sparkle Comp", "CompositorNodeTree")
        tree.interface.new_socket(name="Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        rl = tree.nodes.new("CompositorNodeRLayers")
        gl = tree.nodes.new("CompositorNodeGlare")
        gl.inputs["Type"].default_value = "Bloom"
        gl.inputs["Quality"].default_value = "High"
        gl.inputs["Threshold"].default_value = threshold
        gl.inputs["Strength"].default_value = strength
        gl.inputs["Size"].default_value = size
        out = tree.nodes.new("NodeGroupOutput")
        tree.links.new(rl.outputs["Image"], gl.inputs["Image"])
        tree.links.new(gl.outputs["Image"], out.inputs[0])
        scene.compositing_node_group = tree
    except Exception as error:   # older/newer compositor API: render without bloom rather than fail
        print("BLOOM skipped", error)


def emission(name, color, strength, additive=False):
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*color, 1)
    em.inputs["Strength"].default_value = strength
    if additive:
        # Transparent + emission = additive glow (the halo never hides the gold core behind it).
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        add = nt.nodes.new("ShaderNodeAddShader")
        nt.links.new(tr.outputs[0], add.inputs[0])
        nt.links.new(em.outputs[0], add.inputs[1])
        nt.links.new(add.outputs[0], out.inputs["Surface"])
    else:
        nt.links.new(em.outputs[0], out.inputs["Surface"])
    m.surface_render_method = "BLENDED" if hasattr(m, "surface_render_method") else None
    return m


def star_mesh(name, rays=4, long=1.0, short=0.42, width=0.075):
    """Diamond glint: four long tapered rays + four short diagonal rays + an octagonal core (flat, faces +Z)."""
    verts, faces = [(0, 0, 0)], []
    def ray(angle, length, w):
        a = len(verts)
        c, s = math.cos(angle), math.sin(angle)
        px, py = -s * w, c * w
        verts.extend([(px, py, 0), (c * length, s * length, 0), (-px, -py, 0)])
        faces.append((0, a, a + 1))
        faces.append((0, a + 1, a + 2))
    for i in range(rays):
        ray(i * math.pi * 2 / rays, long, width)
        ray(i * math.pi * 2 / rays + math.pi / rays, short, width * 0.8)
    core = len(verts)
    for i in range(8):
        a = i * math.pi / 4
        verts.append((math.cos(a) * 0.16, math.sin(a) * 0.16, 0))
    for i in range(8):
        faces.append((0, core + i, core + (i + 1) % 8))
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    return me


# ----------------------------------------------------------------------------- flipbook
def flipbook():
    scene = reset()
    scene.render.resolution_x = scene.render.resolution_y = 128
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("Black")
    world.color = (0, 0, 0)
    scene.world = world
    bloom(scene, strength=0.9, threshold=0.4, size=0.6)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.4
    cam.location = (0, 0, 5)
    scene.collection.objects.link(cam)
    scene.camera = cam
    star = bpy.data.objects.new("Star", star_mesh("StarMesh"))
    star.data.materials.append(emission("StarWhite", (1, 1, 1), 6.0))
    scene.collection.objects.link(star)
    frames = []
    n = 16
    for f in range(n):
        t = f / (n - 1)
        pop = math.sin(math.pi * t) ** 0.65                   # grow, peak, fade
        flare = 1 + 0.55 * math.exp(-((t - 0.45) / 0.12) ** 2)  # long rays flare at the peak
        star.scale = (pop * 0.55 * flare, pop * 0.55 * flare, 1)
        star.rotation_euler = (0, 0, math.radians(-8 + 36 * t))
        path = os.path.join(OUT, "_glint_%02d.png" % f)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        frames.append(path)
    atlas = np.zeros((512, 512, 4), dtype=np.float32)
    for i, path in enumerate(frames):
        img = bpy.data.images.load(path)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(128, 128, 4)
        col, row = i % 4, i // 4
        # Blender images are bottom-up; place frame 0 at the top-left of the atlas (Unity sheet order).
        y0 = 512 - (row + 1) * 128
        atlas[y0:y0 + 128, col * 128:(col + 1) * 128] = px
        bpy.data.images.remove(img)
        os.remove(path)
    # Premultiplied-looking additive sprite: brightness in RGB, coverage in A.
    atlas[..., 3] = np.clip(np.max(atlas[..., :3], axis=2) * 1.2, 0, 1)
    save(atlas, "T_Gemstone_StarGlint.png")
    ribbon()


def ribbon(w=256, h=64):
    """Faceted ribbon (U along the trail, V across): bright core, cut-gem lattice, soft edges."""
    u, v = np.meshgrid(np.linspace(0, 1, w, endpoint=False), np.linspace(-1, 1, h))
    core = np.exp(-(v / 0.18) ** 2)
    edge = np.clip(1 - np.abs(v), 0, 1) ** 1.5
    a = (u * 8 + v * 1.0) % 1.0
    b = (u * 8 - v * 1.0) % 1.0
    facets = (np.minimum(np.abs(a - 0.5), np.abs(b - 0.5)) < 0.06).astype(np.float32) * 0.55
    value = np.clip(core * 1.0 + facets * edge + edge * 0.25, 0, 1)
    img = np.dstack([value, value, value, np.clip(edge * 0.9 + core * 0.4, 0, 1)])[::-1]
    save(img.astype(np.float32), "T_Gemstone_Ribbon.png")


def save(rgba, name):
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(rgba.reshape(-1))
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = "PNG"
    img.save()
    print("SAVED", img.filepath_raw)


# ----------------------------------------------------------------------------- review animation
def preview(frames=60):
    scene = reset()
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 1, frames
    scene.eevee.taa_render_samples = 12
    scene.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("Dusk")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.012, 0.022, 0.05, 1)
    bg.inputs["Strength"].default_value = 1.0
    scene.world = world
    bloom(scene, strength=0.6, threshold=0.75, size=0.75)
    rnd = random.Random(7)

    # Court: navy-blue acrylic with white lines, teal run-off (art bible palette), under a warm key.
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    ground = bpy.context.object
    gm = bpy.data.materials.new("Court")
    bsdf = gm.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.05, 0.16, 0.34, 1)
    bsdf.inputs["Roughness"].default_value = 0.55
    ground.data.materials.append(gm)
    for x in (-1.2, 2.8):
        bpy.ops.mesh.primitive_plane_add(size=1, location=(x, 0, 0.002))
        line = bpy.context.object
        line.scale = (0.025, 20, 1)
        lm = bpy.data.materials.new("Line")
        lm.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (0.85, 0.85, 0.82, 1)
        line.data.materials.append(lm)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.data.color = (1.0, 0.82, 0.62)
    sun.rotation_euler = (math.radians(55), 0, math.radians(35))
    scene.collection.objects.link(sun)
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "SUN"))
    fill.data.energy = 0.6
    fill.data.color = (0.55, 0.65, 1.0)
    fill.rotation_euler = (math.radians(-40), 0, math.radians(200))
    scene.collection.objects.link(fill)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 36
    cam.location = (0.35, -4.5, 1.45)
    scene.collection.objects.link(cam)
    scene.camera = cam
    target = Vector((0.35, 0.0, 1.05))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()

    # Ball flight: launched off the paddle at the left, apex 1.6 m, lands right.
    start, end, apex = Vector((-1.1, 0.0, 1.05)), Vector((3.1, 0.5, 0.55)), 1.62
    def ball_at(t):
        p = start.lerp(end, t)
        p.z = start.z + (end.z - start.z) * t + 4 * (apex - (start.z + end.z) / 2) * t * (1 - t)
        return p
    flight_start, flight_frames = 12, 48

    # Paddle (the Gemstone skin): swings through the contact frame and follows through; ball model.
    paddle_path = os.path.join(ASSETS, "Models", "Paddle", "paddle-skin-gemstone-model.glb")
    if os.path.exists(paddle_path):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=paddle_path)
        parts = [o for o in bpy.data.objects if o not in before]
        root = next((o for o in parts if o.parent is None), parts[0])
        dims = max(max(o.dimensions) for o in parts if o.type == "MESH")
        root.scale = [0.98 / max(dims, 1e-3)] * 3
        root.location = (-1.42, -0.12, 1.08)
        root.rotation_euler = (math.radians(80), math.radians(-8), math.radians(-12))
        hinge = bpy.data.objects.new("SwingHinge", None)
        hinge.location = (-1.5, -0.12, 0.55)
        scene.collection.objects.link(hinge)
        bpy.context.view_layer.update()
        root.parent = hinge
        root.matrix_parent_inverse = hinge.matrix_world.inverted()
        for f, angle in ((1, -58), (6, -64), (flight_start, 4), (flight_start + 6, 34), (flight_start + 16, 26), (frames, 22)):
            hinge.rotation_euler = (0, math.radians(angle), 0)
            hinge.keyframe_insert("rotation_euler", frame=f)
        # Jeweller's lighting: a warm key on the gems, an emerald rim from behind.
        for name, loc, energy, colour, size in (("PaddleKey", (-1.7, -1.5, 1.75), 26, (1.0, 0.9, 0.74), 0.5), ("PaddleRim", (-2.1, 0.9, 1.6), 22, (0.35, 1.0, 0.7), 0.6)):
            light = bpy.data.objects.new(name, bpy.data.lights.new(name, "AREA"))
            light.data.energy, light.data.color, light.data.size = energy, colour, size
            light.location = loc
            light.rotation_euler = (Vector((-1.4, -0.1, 1.1)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
            scene.collection.objects.link(light)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.075, segments=32, ring_count=16)
    ball = bpy.context.object
    bpy.ops.object.shade_smooth()
    bm = bpy.data.materials.new("BallGlitter")
    nt = bm.node_tree
    p = nt.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (0.92, 0.88, 0.22, 1)
    p.inputs["Roughness"].default_value = 0.35
    # Glitter: thresholded animated noise drives a white-gold emission (twinkling facets on the ball).
    tex = nt.nodes.new("ShaderNodeTexNoise")
    tex.inputs["Scale"].default_value = 60
    tex.inputs["Detail"].default_value = 1
    mapping = nt.nodes.new("ShaderNodeMapping")
    coord = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(coord.outputs["Object"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.68
    ramp.color_ramp.elements[1].position = 0.72
    nt.links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
    p.inputs["Emission Color"].default_value = (1.0, 0.95, 0.8, 1)
    nt.links.new(ramp.outputs["Color"], p.inputs["Emission Strength"])
    ball.data.materials.append(bm)
    for f in range(1, frames + 1):
        mapping.inputs["Location"].default_value = (f * 0.11, f * 0.07, f * 0.05)
        mapping.inputs["Location"].keyframe_insert("default_value", frame=f)
        t = min(max((f - flight_start) / flight_frames, 0), 1)
        ball.location = ball_at(t)
        ball.keyframe_insert("location", frame=f)
        ball.rotation_euler = (f * 0.35, f * 0.2, 0)
        ball.keyframe_insert("rotation_euler", frame=f)

    # Ribbon: a curve along the whole path, revealed with animated bevel start/end (tapered, gold -> emerald).
    curve = bpy.data.curves.new("Trail", "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.022
    curve.bevel_resolution = 2
    curve.use_map_taper = True
    sp = curve.splines.new("POLY")
    pts = [ball_at(i / 80) for i in range(81)]
    sp.points.add(len(pts) - 1)
    for pt, q in zip(sp.points, pts):
        pt.co = (*q, 1)
    taper = bpy.data.curves.new("Taper", "CURVE")
    tsp = taper.splines.new("POLY")
    tsp.points.add(2)
    for pt, q in zip(tsp.points, [(0, 0.05), (0.75, 0.75), (1, 1.0)]):
        pt.co = (q[0], q[1], 0, 1)
    tob = bpy.data.objects.new("TaperObj", taper)
    scene.collection.objects.link(tob)
    curve.taper_object = tob
    trail = bpy.data.objects.new("Trail", curve)
    scene.collection.objects.link(trail)
    tm = bpy.data.materials.new("Ribbon")
    tn = tm.node_tree
    tn.nodes.clear()
    out = tn.nodes.new("ShaderNodeOutputMaterial")
    em = tn.nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = 1.7
    em.inputs["Color"].default_value = (1.0, 0.66, 0.22, 1)
    tn.links.new(em.outputs[0], out.inputs["Surface"])
    trail.data.materials.append(tm)
    # Emerald halo: a wider, dimmer copy of the ribbon (reads as the green fire of the paddle's emeralds).
    halo_curve = curve.copy()
    halo_curve.bevel_depth = 0.05
    halo = bpy.data.objects.new("TrailHalo", halo_curve)
    scene.collection.objects.link(halo)
    halo_curve.materials.clear()
    halo_curve.materials.append(emission("EmeraldHalo", (0.04, 0.78, 0.30), 0.34, additive=True))
    for f in range(1, frames + 1):
        t = min(max((f - flight_start) / flight_frames, 0), 1)
        for c, lag in ((curve, 0.32), (halo_curve, 0.26)):
            c.bevel_factor_end = t
            c.bevel_factor_start = max(0.0, t - lag)
            c.keyframe_insert("bevel_factor_end", frame=f)
            c.keyframe_insert("bevel_factor_start", frame=f)

    # Star glints: born along the path behind the ball, each twinkles for ~0.4 s (diamond, emerald, gold mix).
    smesh = star_mesh("Glint")
    mats = [emission("Diamond", DIAMOND, 9), emission("Emerald", EMERALD, 7), emission("Gold", GOLD, 7)]
    def glint(at_frame, position, size, mat, life=12):
        o = bpy.data.objects.new("Glint", smesh.copy())
        o.data.materials.append(mat)
        scene.collection.objects.link(o)
        o.location = position
        # face the camera
        o.rotation_euler = (cam.location - position).to_track_quat("Z", "Y").to_euler()
        spin = rnd.uniform(-0.6, 0.6)
        for k in range(life + 2):
            f = at_frame + k
            s = size * (math.sin(math.pi * min(k / life, 1)) ** 0.7 if k <= life else 0)
            o.scale = (s, s, s)
            o.keyframe_insert("scale", frame=f)
            o.rotation_euler.z += spin * 0.05
            o.keyframe_insert("rotation_euler", frame=f)
        o.scale = (0, 0, 0)
        o.keyframe_insert("scale", frame=max(1, at_frame - 1))
    for i in range(46):
        tb = rnd.uniform(0.0, 0.98)
        born = int(flight_start + tb * flight_frames) + rnd.randint(0, 2)
        jitter = Vector((rnd.uniform(-.08, .08), rnd.uniform(-.08, .08), rnd.uniform(-.06, .08)))
        glint(born, ball_at(tb) + jitter, rnd.uniform(0.05, 0.12), rnd.choices(mats, weights=(5, 3, 2))[0], life=rnd.randint(9, 15))
    # Impact burst on the paddle face.
    for i in range(12):
        a = i * math.pi * 2 / 12
        glint(flight_start - 1 + (i % 3), start + Vector((0.0, math.cos(a) * 0.18, math.sin(a) * 0.18)), 0.11, mats[i % 3], life=8)
    # Orbiting glints on the ball while it flies.
    for k in range(3):
        o = bpy.data.objects.new("Orbit%d" % k, smesh.copy())
        o.data.materials.append(mats[k % 2])
        scene.collection.objects.link(o)
        for f in range(1, frames + 1):
            t = min(max((f - flight_start) / flight_frames, 0), 1)
            a = f * 0.55 + k * 2.1
            o.location = ball_at(t) + Vector((math.cos(a) * 0.12, math.sin(a) * 0.05, math.sin(a) * 0.12))
            s = 0.07 * (0.5 + 0.5 * math.sin(f * 0.9 + k)) if 0 < t < 1 else 0
            o.scale = (s, s, s)
            o.rotation_euler = (cam.location - o.location).to_track_quat("Z", "Y").to_euler()
            o.keyframe_insert("location", frame=f)
            o.keyframe_insert("scale", frame=f)
            o.keyframe_insert("rotation_euler", frame=f)

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "gemstone_vfx_preview.blend"))
    for f in range(1, frames + 1):
        scene.frame_set(f)
        scene.render.filepath = os.path.join(OUT, "f_%04d.png" % f)
        bpy.ops.render.render(write_still=True)
    print("PREVIEW_DONE", OUT)


if __name__ == "__main__":
    if MODE == "flipbook":
        flipbook()
    else:
        preview(int(ARGS[2]) if len(ARGS) > 2 else 60)
