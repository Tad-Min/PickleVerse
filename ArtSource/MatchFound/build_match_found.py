"""PickleVerse "match found" intro: two paddles rally, cross, and hold the VS ball between the two players.

Run headless:
  blender --background --factory-startup --python build_match_found.py -- <out_dir> <mode> [scale]
mode: build (save .blend only) | stills (key frames as PNG) | anim (every frame as PNG)

All motion is object-level keyframes (no simulations), so the paddles, ball and camera can later be
exported to glTF for real-time playback in Unity. The avatars, title and button are a preview of the
uGUI layer and are parented to the camera so they stay fixed on screen.
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Quaternion, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = ARGS[0] if ARGS else os.path.join(os.path.dirname(__file__), "render")
MODE = ARGS[1] if len(ARGS) > 1 else "build"
SCALE = float(ARGS[2]) if len(ARGS) > 2 else 1.0
HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", "..", "My project", "Assets"))
FONT_BOLD = os.path.join(ASSETS, "Fonts", "ArialBold.ttf")
FONT_ITALIC = os.path.join(ASSETS, "Fonts", "ArialBoldItalic.ttf")
END = 240
FPS = 30
random.seed(7)

GOLD = (1.0, 0.80, 0.52)
INK = (0.03, 0.045, 0.075)
MINT = (0.45, 0.92, 0.82)
MUTED = (0.50, 0.58, 0.70)
WHITE = (0.95, 0.97, 1.0)


# ----------------------------------------------------------------------------- helpers
def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def smoother(t):
    t = max(0.0, min(1.0, t))
    return t * t * t * (t * (t * 6 - 15) + 10)


def lerp(a, b, t):
    return a + (b - a) * t


def vlerp(a, b, t):
    return Vector(a).lerp(Vector(b), t)


def link(obj, parent=None):
    bpy.context.scene.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def empty(name, loc=(0, 0, 0), parent=None):
    o = bpy.data.objects.new(name, None)
    o.location = loc
    return link(o, parent)


def key(target, path, frame, value=None, index=-1):
    if value is not None:
        setattr(target, path, value)
    target.keyframe_insert(path, frame=frame, index=index)


def linear(obj_or_data):
    ad = obj_or_data.animation_data
    if not ad or not ad.action:
        return
    for layer in getattr(ad.action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for k in fc.keyframe_points:
                        k.interpolation = "LINEAR"
    for fc in getattr(ad.action, "fcurves", []) or []:
        for k in fc.keyframe_points:
            k.interpolation = "LINEAR"


def nodes_on(idblock):
    try:
        idblock.use_nodes = True   # deprecated (always on) in Blender 5
    except Exception:
        pass


def principled(name, base, rough=0.4, metal=0.0, emit=None, strength=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    nodes_on(m)
    p = m.node_tree.nodes.get("Principled BSDF")
    if p is None:
        p = m.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
        out = m.node_tree.nodes.get("Material Output") or m.node_tree.nodes.new("ShaderNodeOutputMaterial")
        m.node_tree.links.new(p.outputs[0], out.inputs["Surface"])
    p.inputs["Base Color"].default_value = (*base, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if coat:
        p.inputs["Coat Weight"].default_value = coat
    if emit:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = strength
    return m


def glow(name, color, strength=1.0, alpha=1.0, radial=False):
    """Unlit emission with an animatable fade (Mix factor) - used for the UI layer and effects."""
    m = bpy.data.materials.new(name)
    nodes_on(m)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    em.name, mix.name = "Emit", "Fade"
    em.inputs["Color"].default_value = (*color, 1)
    em.inputs["Strength"].default_value = strength
    mix.inputs["Fac"].default_value = alpha
    if radial:
        # Soft round falloff for halos: alpha follows a spherical gradient on generated coordinates.
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Location"].default_value = (-1.0, -1.0, 0)
        mp.inputs["Scale"].default_value = (2.0, 2.0, 0.0)
        gr = nt.nodes.new("ShaderNodeTexGradient")
        gr.gradient_type = "SPHERICAL"
        mul = nt.nodes.new("ShaderNodeMath")
        mul.operation = "MULTIPLY"
        mul.name = "Mask"
        pw = nt.nodes.new("ShaderNodeMath")
        pw.operation = "POWER"
        pw.inputs[1].default_value = 2.2
        nt.links.new(tc.outputs["Generated"], mp.inputs["Vector"])
        nt.links.new(mp.outputs["Vector"], gr.inputs["Vector"])
        nt.links.new(gr.outputs["Fac"], pw.inputs[0])
        nt.links.new(pw.outputs[0], mul.inputs[0])
        mul.inputs[1].default_value = alpha
        nt.links.new(mul.outputs[0], mix.inputs["Fac"])
        # Keep the animatable value on the Math node; mirror name for fade().
        mix.name = "FadeMix"
        mul.name = "Fade"
    nt.links.new(tr.outputs[0], mix.inputs[1])
    nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass
    return m


def fade_socket(m):
    n = m.node_tree.nodes["Fade"]
    return n.inputs["Fac"] if n.type == "MIX_SHADER" else n.inputs[1]


def set_fade(m, frame, value):
    s = fade_socket(m)
    s.default_value = value
    s.keyframe_insert("default_value", frame=frame)


def set_emit(m, frame, strength=None, color=None):
    em = m.node_tree.nodes["Emit"]
    if strength is not None:
        em.inputs["Strength"].default_value = strength
        em.inputs["Strength"].keyframe_insert("default_value", frame=frame)
    if color is not None:
        em.inputs["Color"].default_value = (*color, 1)
        em.inputs["Color"].keyframe_insert("default_value", frame=frame)


def text(name, body, size, mat, font, parent=None, loc=(0, 0, 0), extrude=0.0, align="CENTER"):
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = body
    cu.font = bpy.data.fonts.load(font, check_existing=True)
    cu.size = size
    cu.align_x = align
    cu.align_y = "CENTER"
    cu.extrude = extrude
    cu.materials.append(mat)
    o = bpy.data.objects.new(name, cu)
    o.location = loc
    return link(o, parent)


def mesh_object(name, build, mat=None, parent=None, loc=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    build(bm)
    bm.to_mesh(me)
    bm.free()
    if mat:
        me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    o.location = loc
    return link(o, parent)


def torus(name, major, minor, mat, parent=None, loc=(0, 0, 0), segs=64):
    def build(bm):
        bmesh.ops.create_circle(bm, cap_ends=False, radius=1, segments=segs)
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings, sides = segs, 12
    verts = []
    for i in range(rings):
        a = 2 * math.pi * i / rings
        row = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            r = major + minor * math.cos(b)
            row.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        verts.append(row)
    for i in range(rings):
        for j in range(sides):
            bm.faces.new((verts[i][j], verts[(i + 1) % rings][j], verts[(i + 1) % rings][(j + 1) % sides], verts[i][(j + 1) % sides]))
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    o.location = loc
    return link(o, parent)


def disc(name, radius, mat, parent=None, loc=(0, 0, 0), segs=96):
    def build(bm):
        bmesh.ops.create_circle(bm, cap_ends=True, radius=radius, segments=segs)
    return mesh_object(name, build, mat, parent, loc)


def rounded_rect(name, w, h, r, mat, parent=None, loc=(0, 0, 0)):
    def build(bm):
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
        for v in bm.verts:
            v.co.x *= w
            v.co.y *= h
        bmesh.ops.bevel(bm, geom=list(bm.verts), offset=r, segments=8, affect="VERTICES")
    return mesh_object(name, build, mat, parent, loc)


def face_camera(o):
    """UI meshes are built in XY; camera-local space looks down -Z, so XY already faces the lens."""
    o.rotation_euler = (0, 0, 0)


# ----------------------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 1, END
scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
scene.render.resolution_percentage = int(100 * SCALE)
scene.render.engine = "BLENDER_EEVEE"
ee = scene.eevee
ee.taa_render_samples = int(os.environ.get("PV_SAMPLES", 32 if MODE == "anim" else 24))
ee.use_raytracing = True
ee.use_shadows = True
scene.render.use_motion_blur = True
scene.render.motion_blur_shutter = 0.45
for vt, look in (("AgX", "AgX - Medium High Contrast"), ("AgX", "None")):
    try:
        scene.view_settings.view_transform = vt
        scene.view_settings.look = look
        break
    except TypeError:
        pass
scene.view_settings.exposure = 0.15

world = bpy.data.worlds.new("Arena Night")
nodes_on(world)
bg = world.node_tree.nodes.get("Background") or world.node_tree.nodes.new("ShaderNodeBackground")
if not bg.outputs[0].is_linked:
    wo = world.node_tree.nodes.get("World Output") or world.node_tree.nodes.new("ShaderNodeOutputWorld")
    world.node_tree.links.new(bg.outputs[0], wo.inputs["Surface"])
bg.inputs["Color"].default_value = (0.004, 0.008, 0.02, 1)
# Screen-space vignette: a touch of teal behind the action, black at the edges.
wt = world.node_tree
wc = wt.nodes.new("ShaderNodeTexCoord")
wm = wt.nodes.new("ShaderNodeMapping")
wm.inputs["Location"].default_value = (-1.0, -1.1, 0)
wm.inputs["Scale"].default_value = (2.0, 2.4, 0)
wg = wt.nodes.new("ShaderNodeTexGradient")
wg.gradient_type = "SPHERICAL"
wr = wt.nodes.new("ShaderNodeValToRGB")
wr.color_ramp.elements[0].color = (0.002, 0.004, 0.01, 1)
wr.color_ramp.elements[1].color = (0.016, 0.04, 0.06, 1)
wt.links.new(wc.outputs["Window"], wm.inputs["Vector"])
wt.links.new(wm.outputs["Vector"], wg.inputs["Vector"])
wt.links.new(wg.outputs["Fac"], wr.inputs["Fac"])
wt.links.new(wr.outputs["Color"], bg.inputs["Color"])
bg.inputs["Strength"].default_value = 1.0
scene.world = world

# ----------------------------------------------------------------------------- time: slow motion on the second hit
H1, H2, H3, LOCK = 8.0, 26.0, 44.0, 64.0          # action frames
SLOW_LO, SLOW_HI, SLOW, RAMP = 22.0, 30.0, 0.32, 3.0


def speed(x):
    if x < SLOW_LO - RAMP or x > SLOW_HI + RAMP:
        return 1.0
    if x < SLOW_LO:
        return 1 + (SLOW - 1) * smooth((x - (SLOW_LO - RAMP)) / RAMP)
    if x > SLOW_HI:
        return SLOW + (1 - SLOW) * smooth((x - SLOW_HI) / RAMP)
    return SLOW


ACT = [0.0] * (END + 3)
cur = 0.0
for f in range(1, END + 3):
    ACT[f] = cur
    cur += speed(cur)


def real(a):
    for f in range(1, END + 2):
        if ACT[f + 1] >= a:
            return f + (a - ACT[f]) / max(1e-6, ACT[f + 1] - ACT[f])
    return END


FH1, FH2, FH3, FL = (int(round(real(x))) for x in (H1, H2, H3, LOCK))
F_IN = FL + 6                 # avatars slide in
F_ACCEPT_L = FL + 52          # local player presses accept
F_ACCEPT_R = FL + 94          # opponent accepts
F_ALL = F_ACCEPT_R + 14       # everyone ready
print("EVENTS", dict(FH1=FH1, FH2=FH2, FH3=FH3, FL=FL, F_IN=F_IN, F_ACCEPT_L=F_ACCEPT_L, F_ACCEPT_R=F_ACCEPT_R, F_ALL=F_ALL))

# ----------------------------------------------------------------------------- stage: glossy floor, court lines, bokeh, lights
floor_mat = principled("Floor Gloss", (0.010, 0.016, 0.028), rough=0.16, coat=0.4)
floor = mesh_object("Floor", lambda bm: bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40), floor_mat, loc=(0, 6, -2.7))
line_mat = glow("Court Lines", (0.30, 0.80, 0.72), 0.9)
COURT = []
def court_line(x0, y0, x1, y1, w=0.07):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    lx, ly = abs(x1 - x0) + w, abs(y1 - y0) + w
    def build(bm):
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
        for v in bm.verts:
            v.co.x *= lx
            v.co.y *= ly
    COURT.append(mesh_object("Court Line", build, line_mat, loc=(cx, cy, -2.69)))
for y in (-3.0, 3.0):
    court_line(-6.7, y, 6.7, y)
for x in (-6.7, -2.1, 2.1, 6.7):
    court_line(x, -3.0, x, 3.0)
court_line(-6.7, 0, -2.1, 0)
court_line(2.1, 0, 6.7, 0)
court_line(0, -3.2, 0, 3.2, w=0.09)
COURT[-1].data.materials[0] = glow("Net Tape", (0.85, 0.9, 1.0), 1.4)

for i, (x, z, size, c, e) in enumerate([(-19, 5.5, 7, GOLD, 0.6), (-13, 0.0, 5, (1.0, 0.92, 0.78), 0.4), (13, 0.5, 6, GOLD, 0.5),
                                           (17, 5.0, 8, (0.55, 0.85, 0.95), 0.5), (-25, 1.0, 9, (0.45, 0.75, 0.9), 0.4),
                                           (24, 0.5, 9, GOLD, 0.45), (0, 2.0, 16, (0.25, 0.55, 0.7), 0.3)]):
    m = glow("Stadium Glow %d" % i, c, e, 0.8, radial=True)
    o = mesh_object("Stadium Glow", lambda bm: bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=size / 2), m, loc=(x, 30, z))
    o.rotation_euler = (math.radians(90), 0, 0)


def area(name, loc, energy, color, size, aim=(0, 0, 0.4)):
    d = bpy.data.lights.new(name, "AREA")
    d.energy, d.color, d.size = energy, color, size
    o = link(bpy.data.objects.new(name, d))
    o.location = loc
    o.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


area("Key Warm", (-4.5, -5.5, 5.0), 700, (1.0, 0.85, 0.68), 4)
area("Rim Blue", (4.5, 4.0, 3.0), 1100, (0.40, 0.62, 1.0), 3)
area("Rim Cool", (-5.0, 5.0, 2.0), 700, (0.86, 0.92, 1.0), 3)
area("Fill", (0, -7, -1.5), 160, (0.92, 0.95, 1.0), 6)
hero_key = area("Hero Key", (-3.4, -4.8, 3.8), 0, (1.0, 0.9, 0.76), 2.5, aim=(0, 0, 0.45))
spot_d = bpy.data.lights.new("Top Pool", "SPOT")
spot_d.energy, spot_d.color, spot_d.spot_size, spot_d.spot_blend = 2600, (1.0, 0.86, 0.66), math.radians(55), 0.6
spot = link(bpy.data.objects.new("Top Pool", spot_d))
spot.location = (0, 0.5, 8)
flash_d = bpy.data.lights.new("Impact Flash", "POINT")
flash_d.energy, flash_d.color, flash_d.shadow_soft_size = 0, (1.0, 0.85, 0.6), 0.6
flash = link(bpy.data.objects.new("Impact Flash", flash_d))
flash.location = (0, -2.8, 1.3)

# ----------------------------------------------------------------------------- paddles and ball
sys.path.insert(0, HERE)
import paddle_shape as ps
bpy.ops.import_scene.gltf(filepath=os.path.join(HERE, "PV_Paddle_Elongated.glb"))
paddle_mesh = bpy.context.selected_objects[0]
bpy.ops.import_scene.gltf(filepath=os.path.join(ASSETS, "Models", "ball", "pickleball_ball_cartoon.glb"))
ball = bpy.context.selected_objects[0]
ball.name = "VS Ball"
for p in ball.data.polygons:
    p.use_smooth = True
smooth_mod = ball.modifiers.new("Smooth", "SUBSURF")
smooth_mod.levels, smooth_mod.render_levels = 1, 1

HEAD = ps.HEAD_CENTRE   # grip -> centre of the paddle face
rigs = {}
for side, name in ((-1, "Left"), (1, "Right")):
    rig = empty("Paddle %s Rig" % name)
    rig.rotation_mode = "QUATERNION"
    m = paddle_mesh if side < 0 else paddle_mesh.copy()
    if side > 0:
        link(m)
    m.name = "Paddle %s" % name
    m.parent = rig
    m.location = (0, 0, 0)   # the model origin is the grip hold point
    rigs[side] = rig

# "VS" printed on the ball (ball local radius is 1; the front of the ball faces -Y)
ink = principled("VS Ink", INK, rough=0.35, emit=(0.0, 0.0, 0.0), strength=0)
rim = principled("VS Rim", (1.0, 0.93, 0.80), rough=0.3)
def vs_layer(name, mat, offset, depth):
    t = text(name, "VS", 0.74, mat, FONT_ITALIC, extrude=0.04)
    t.data.offset = offset
    t.rotation_euler = (math.radians(90), 0, 0)
    t.location = (0.02, -1.25, 0.0)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(t.evaluated_get(dg))
    o = bpy.data.objects.new(name, me)
    o.matrix_world = t.matrix_world.copy()
    link(o, ball)
    o.matrix_parent_inverse = Matrix.Identity(4)
    o.location = t.location
    o.rotation_euler = t.rotation_euler
    bpy.data.objects.remove(t)
    sw = o.modifiers.new("Wrap", "SHRINKWRAP")
    sw.target = ball
    sw.wrap_method = "PROJECT"
    sw.use_project_z = True
    sw.use_negative_direction = True
    sw.use_positive_direction = True
    sw.wrap_mode = "OUTSIDE_SURFACE"
    sw.offset = depth
    sub = o.modifiers.new("Bulk", "SOLIDIFY")
    sub.thickness = 0.014
    return o
vs_layer("VS Rim", rim, 0.045, 0.006)
vs_layer("VS Ink", ink, 0.0, 0.02)

# ----------------------------------------------------------------------------- choreography (action time)
CONTACT = {H1: Vector((-2.91, 0.0, 0.44)), H2: Vector((2.91, -0.35, 0.62)), H3: Vector((-2.91, 0.30, 0.36))}
SERVE = Vector((-2.35, 0.0, 2.9))
C = Vector((0, 0, 0.15))                       # paddle crossing point (at the throat, heads spread above)
C_BALL = Vector((0, -0.62, 0.15))
R_RALLY, R_HERO = 0.28, 0.40


def arc(p0, p1, apex, t):
    p = p0.lerp(p1, t)
    p.z += apex * 4 * t * (1 - t)
    return p


def ball_at(a):
    if a <= H1:
        t = max(0.0, a) / H1
        p = SERVE.lerp(CONTACT[H1], t)
        p.z = lerp(SERVE.z, CONTACT[H1].z, t * t)      # dropping into the first hit
        return p, R_RALLY
    if a <= H2:
        return arc(CONTACT[H1], CONTACT[H2], 1.15, (a - H1) / (H2 - H1)), R_RALLY
    if a <= H3:
        return arc(CONTACT[H2], CONTACT[H3], 0.75, (a - H2) / (H3 - H2)), R_RALLY
    if a <= LOCK:
        t = (a - H3) / (LOCK - H3)
        p = CONTACT[H3].lerp(C_BALL, t)
        p.z = lerp(CONTACT[H3].z, C_BALL.z, t) + 3.0 * 4 * t * (1 - t)  # the lob
        return p, lerp(R_RALLY, R_HERO, smoother((a - (H3 + 8)) / (LOCK - H3 - 8)))
    hold = a - LOCK
    return C_BALL + Vector((0, 0, 0.035 * math.sin(hold * 0.11) * smooth(hold / 12))), R_HERO


SPIN_AXIS = Vector((0.35, 0.25, 1.0)).normalized()


def ball_rot(a):
    if a <= LOCK:
        spin = Quaternion(SPIN_AXIS, a * 0.42)
        settle = smoother((a - (LOCK - 9)) / 9)
        return spin.slerp(Quaternion(), settle)
    hold = a - LOCK
    wob = math.radians(4) * smooth(hold / 20)
    return Quaternion((0, 0, 1), wob * math.sin(hold * 0.07)) @ Quaternion((1, 0, 0), 0.5 * wob * math.sin(hold * 0.05))


def basis(n, u):
    n, u = Vector(n).normalized(), Vector(u).normalized()
    u = (u - n * u.dot(n)).normalized()
    x = n.cross(u)
    return Matrix((x, n, u)).transposed().to_quaternion()


HITS = {-1: [(H1, False), (H3, True)], 1: [(H2, False)]}
KEYS = [(-12, (0, 0, 0, 0)), (-4, (40, -6, -0.42, -0.16)), (0, (0, 8, 0, 0)), (6, (-38, 22, 0.48, 0.30)), (18, (0, 0, 0, 0))]


def swing(a, hit, lob):
    dt = a - hit
    if dt <= KEYS[0][0] or dt >= KEYS[-1][0]:
        return (0, 0, 0, 0)
    for (t0, k0), (t1, k1) in zip(KEYS, KEYS[1:]):
        if t0 <= dt <= t1:
            t = smooth((dt - t0) / (t1 - t0))
            k0, k1 = list(k0), list(k1)
            if lob:
                if t0 == 0:
                    k0[1] = 35
                if t1 == 0:
                    k1[1] = 35
                if t0 == 6 or t1 == 6:
                    pass
            return tuple(lerp(x, y, t) for x, y in zip(k0, k1))
    return (0, 0, 0, 0)


THETA = math.radians(42)
CROSS_AT = 0.62   # grip -> crossing point (in the throat)
CROSS = {}
for side in (-1, 1):
    u = Vector((-side * math.sin(THETA), 0, math.cos(THETA)))
    CROSS[side] = (C - u * CROSS_AT + Vector((0, -0.07 if side < 0 else 0.09, 0)), basis((0, -1, 0), u))


def paddle_at(side, a):
    hits = HITS[side]
    # Track the height/depth of this paddle's next (or last) contact.
    upcoming = [h for h, _ in hits if h >= a - 10]
    target = CONTACT[upcoming[0]] if upcoming else CONTACT[hits[-1][0]]
    prev = [h for h, _ in hits if h < a - 10]
    if prev and upcoming:
        p0 = CONTACT[prev[-1]]
        t = smooth((a - (prev[-1] + 10)) / max(1, upcoming[0] - prev[-1] - 18))
        target = p0.lerp(CONTACT[upcoming[0]], t)
    yaw = pitch = fwd = up = 0.0
    for h, lob in hits:
        s = swing(a, h, lob)
        yaw += s[0]; pitch += s[1]; fwd += s[2]; up += s[3]
    face = Vector((-side, 0, 0))
    q_yaw = Quaternion((0, 0, 1), math.radians(yaw * -side))
    n = q_yaw @ face
    tilt_axis = n.cross(Vector((0, 0, 1))).normalized()
    q_pitch = Quaternion(tilt_axis, math.radians(pitch))   # positive pitch opens the face upwards
    n = q_pitch @ n
    u = q_pitch @ Vector((0.0, 0.0, 1.0))
    bob = 0.045 * math.sin(a * 0.23 + (0 if side < 0 else 1.7))
    pos = Vector((side * 3.25 - side * fwd, target.y, target.z - HEAD + up + bob))
    pos -= (u - Vector((0, 0, 1))) * HEAD    # tilt about the face centre so the face meets the ball on the lob
    rot = basis(n, u)
    # Sweep into the cross after the lob.
    t = smoother((a - 47) / 15)
    if t > 0:
        cpos, crot = CROSS[side]
        swoop = Vector((0, 0, 0.55 * math.sin(math.pi * t)))
        pos = pos.lerp(cpos, t) + swoop
        rot = rot.slerp(crot, t)
    if a > 62:
        k = a - 62
        recoil = 0.09 * math.exp(-k / 4.5) * math.sin(k * 0.95)
        breathe = 0.025 * math.sin((a - 62) * 0.09 + (0 if side < 0 else 1.3)) * smooth(k / 20)
        pos = pos + Vector((side * recoil, 0, breathe))
        rot = Quaternion((0, 1, 0), side * 0.012 * math.sin(a * 0.06)) @ rot
    return pos, rot


# ----------------------------------------------------------------------------- bake the action
TRAIL_N, TRAIL_STEP = 12, 0.5
trail_cu = bpy.data.curves.new("Ball Trail", "CURVE")
trail_cu.dimensions = "3D"
trail_cu.bevel_depth = 1.0
trail_cu.bevel_resolution = 3
trail_spline = trail_cu.splines.new("POLY")
trail_spline.points.add(TRAIL_N - 1)
trail_m = glow("Ball Trail", (1.0, 0.9, 0.55), 2.2, 1.0)
tn = trail_m.node_tree
uvn = tn.nodes.new("ShaderNodeTexCoord")
sep = tn.nodes.new("ShaderNodeSeparateXYZ")
inv = tn.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
pw_ = tn.nodes.new("ShaderNodeMath"); pw_.operation = "POWER"; pw_.inputs[1].default_value = 1.8
mul_ = tn.nodes.new("ShaderNodeMath"); mul_.operation = "MULTIPLY"; mul_.inputs[1].default_value = 0.55
tn.links.new(uvn.outputs["UV"], sep.inputs[0])
tn.links.new(sep.outputs["X"], inv.inputs[1])
tn.links.new(inv.outputs[0], pw_.inputs[0])
tn.links.new(pw_.outputs[0], mul_.inputs[0])
tn.links.new(mul_.outputs[0], tn.nodes["Fade"].inputs["Fac"])
trail_cu.materials.append(trail_m)
trail = link(bpy.data.objects.new("Ball Trail", trail_cu))


def act_at(fr):
    if fr <= 1:
        return ACT[1] - (1 - fr)
    i = int(math.floor(fr))
    return lerp(ACT[i], ACT[i + 1], fr - i)


ball.rotation_mode = "QUATERNION"
prev_q = {}
for f in range(1, END + 1):
    a = ACT[f]
    p, r = ball_at(a)
    ball.location = p
    ball.scale = (r, r, r)
    ball.rotation_quaternion = ball_rot(a)
    for path in ("location", "scale", "rotation_quaternion"):
        ball.keyframe_insert(path, frame=f)
    for side, rig in rigs.items():
        pos, rot = paddle_at(side, a)
        if side in prev_q and prev_q[side].dot(rot) < 0:
            rot.negate()
        prev_q[side] = rot
        rig.location = pos
        rig.rotation_quaternion = rot
        rig.keyframe_insert("location", frame=f)
        rig.keyframe_insert("rotation_quaternion", frame=f)
    for i, pt in enumerate(trail_spline.points):
        fr = f - i * TRAIL_STEP
        tp, tr_ = ball_at(act_at(fr))
        pt.co = (tp.x, tp.y, tp.z, 1.0)
        live = f <= FL and fr >= 1
        pt.radius = tr_ * 0.55 * (1 - i / TRAIL_N) ** 1.4 if live else 0.0
        pt.keyframe_insert("co", frame=f)
        pt.keyframe_insert("radius", frame=f)
for o in [ball, *rigs.values(), trail_cu]:
    linear(o)

# ----------------------------------------------------------------------------- effects: hit rings, sparks, impact
ENERGY = (0.62, 0.82, 1.0)


def ring_burst(name, frame, where, normal, r0, r1, life, color=ENERGY, strength=8):
    m = glow(name, color, strength, 0)
    o = torus(name, 1.0, 0.035, m, loc=where)
    o.rotation_euler = normal.to_track_quat("Z", "Y").to_euler()
    for f, s, a in ((frame - 1, r0, 0.0), (frame, r0, 1.0), (frame + life, r1, 0.0)):
        o.scale = (s, s, s * 0.6)
        o.keyframe_insert("scale", frame=f)
        set_fade(m, f, a)
    return o


SPARK_MAT = glow("Spark", (0.75, 0.9, 1.0), 8)


def sparks(frame_a, where, direction, count, spread, speed_, life):
    for i in range(count):
        d = (direction + Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-0.6, 1.2))) * spread).normalized()
        v = d * speed_ * random.uniform(0.6, 1.3)
        lf = life * random.uniform(0.7, 1.2)
        o = mesh_object("Spark", lambda bm: bmesh.ops.create_cube(bm, size=1), SPARK_MAT)
        o.rotation_mode = "QUATERNION"
        start = real(frame_a)
        f0 = int(math.floor(start))
        o.scale = (0, 0, 0)
        o.keyframe_insert("scale", frame=max(1, f0 - 1))
        for f in range(f0, min(END, int(real(frame_a + lf))) + 2):
            t = (ACT[f] - frame_a) / 30.0
            if t < 0:
                continue
            vel = v + Vector((0, 0, -9.0 * t))
            o.location = where + v * t + Vector((0, 0, -4.5 * t * t))
            o.rotation_quaternion = vel.to_track_quat("Z", "Y")
            k = max(0.0, 1 - (ACT[f] - frame_a) / lf)
            o.scale = (0.022 * k, 0.022 * k, 0.11 * k + 0.02 * k * vel.length / 6)
            for path in ("location", "rotation_quaternion", "scale"):
                o.keyframe_insert(path, frame=f)
        linear(o)


for h, d in ((H1, Vector((1, 0, 0.4))), (H2, Vector((-1, 0, 0.3))), (H3, Vector((1, 0, 1.0)))):
    fr = int(round(real(h)))
    ring_burst("Hit Ring", fr, CONTACT[h], Vector((1, 0, 0)), 0.25, 1.15, 8 if h != H2 else 22, strength=5)
    sparks(h, CONTACT[h], d, 10, 0.55, 5.5, 9)

ring_burst("Impact Ring", FL, C_BALL + Vector((0, -0.05, 0)), Vector((0, -1, 0)), 0.45, 3.4, 14, strength=5)
ring_burst("Impact Ring Inner", FL + 1, C_BALL + Vector((0, -0.04, 0)), Vector((0, -1, 0)), 0.35, 1.9, 9, color=(1, 1, 1), strength=3.5)
sparks(LOCK, C_BALL + Vector((0, -0.2, 0)), Vector((0, -0.3, 0.2)), 26, 1.25, 7.0, 16)
for f, e in ((FL - 1, 0), (FL, 520), (FL + 2, 360), (FL + 10, 0)):
    key(flash.data, "energy", f, e)
for f, e in ((FL - 16, 0), (FL - 2, 170)):
    key(hero_key.data, "energy", f, e)

# ----------------------------------------------------------------------------- camera (real time: keeps moving through the slow motion)
cam_d = bpy.data.cameras.new("Cinematic")
cam_d.clip_end = 200
cam_d.sensor_width = 36
cam = link(bpy.data.objects.new("Cinematic Camera", cam_d))
scene.camera = cam
target = empty("Camera Target")
tt = cam.constraints.new("TRACK_TO")
tt.target, tt.track_axis, tt.up_axis = target, "TRACK_NEGATIVE_Z", "UP_Y"
cam_d.dof.use_dof = True
cam_d.dof.focus_object = target

SHOTS = [
    # 1. Low and close on the left paddle as the ball drops in.
    (1, (-5.6, -2.6, 0.0), (-2.9, 0.0, 1.35), 32, 1.6),
    (FH1, (-5.2, -3.3, 0.2), (-2.8, 0.0, 0.55), 32, 1.6),
    # 2. Track the ball across the net.
    (FH1 + 9, (-2.6, -6.4, 0.9), (0.0, -0.15, 1.45), 30, 1.8),
    # 3. Bullet time: orbit the right paddle from its side towards the face while the ball compresses.
    (FH2 - 9, (0.6, -3.6, 0.55), (2.6, -0.35, 0.75), 32, 1.4),
    (FH2, (0.75, -2.55, 0.5), (2.85, -0.35, 0.62), 34, 1.2),
    (FH2 + 16, (0.3, -1.25, 0.95), (2.75, -0.32, 0.62), 34, 1.2),
    # 4. Swing out in front of the right paddle and look back across the court for the lob.
    (FH3 - 6, (4.6, -3.4, 1.4), (-2.4, 0.25, 0.5), 34, 2.0),
    (FH3 + 6, (2.0, -6.8, -0.3), (-0.8, 0.0, 1.6), 30, 2.2),
    # 5. Settle on the hero shot as the paddles cross and the ball lands.
    (FL - 6, (0.0, -8.1, 0.38), (0.0, -0.35, 0.5), 40, 2.8),
    (FL + 14, (0.0, -7.9, 0.4), (0.0, -0.3, 0.42), 40, 2.8),
    (END, (0.0, -7.05, 0.36), (0.0, -0.3, 0.4), 40, 2.8),
]


def hermite(keys, f, idx):
    ts = [k[0] for k in keys]
    if f <= ts[0]:
        return Vector(keys[0][idx]) if isinstance(keys[0][idx], tuple) else keys[0][idx]
    if f >= ts[-1]:
        return Vector(keys[-1][idx]) if isinstance(keys[-1][idx], tuple) else keys[-1][idx]
    i = max(j for j in range(len(ts) - 1) if ts[j] <= f)
    def val(j):
        v = keys[max(0, min(len(keys) - 1, j))][idx]
        return Vector(v) if isinstance(v, tuple) else Vector((v, 0, 0))
    def tan(j):
        j0, j1 = max(0, j - 1), min(len(keys) - 1, j + 1)
        return (val(j1) - val(j0)) / max(1e-6, ts[j1] - ts[j0])
    t0, t1 = ts[i], ts[i + 1]
    h = t1 - t0
    s = (f - t0) / h
    p = (2 * s ** 3 - 3 * s ** 2 + 1) * val(i) + (s ** 3 - 2 * s ** 2 + s) * h * tan(i) + (-2 * s ** 3 + 3 * s ** 2) * val(i + 1) + (s ** 3 - s ** 2) * h * tan(i + 1)
    return p if isinstance(keys[0][idx], tuple) else p.x


for f in range(1, END + 1):
    cam.location = hermite(SHOTS, f, 1)
    target.location = hermite(SHOTS, f, 2)
    if FL <= f <= FL + 10:
        k = 1 - (f - FL) / 10
        cam.location += Vector((random.uniform(-1, 1), 0, random.uniform(-1, 1))) * 0.07 * k
    cam_d.lens = hermite(SHOTS, f, 3)
    cam_d.dof.aperture_fstop = hermite(SHOTS, f, 4)
    cam.keyframe_insert("location", frame=f)
    target.keyframe_insert("location", frame=f)
    cam_d.keyframe_insert("lens", frame=f)
    cam_d.dof.keyframe_insert("aperture_fstop", frame=f)
for o in (cam, target, cam_d):
    linear(o)

# ----------------------------------------------------------------------------- aura: procedural smoke and energy waves
# Animated 4D noise on camera-facing cards: cheap, no simulation, portable to a Unity shader with a scrolling noise.
def _time_socket(nt):
    v = nt.nodes.new("ShaderNodeValue")
    v.name = "Time"
    out = v.outputs[0]
    out.default_value = 0.0
    out.keyframe_insert("default_value", frame=1)
    out.default_value = END / FPS
    out.keyframe_insert("default_value", frame=END + 1)
    return out


class G:
    def __init__(self, nt):
        self.nt = nt

    def n(self, kind, **props):
        node = self.nt.nodes.new(kind)
        for k, v in props.items():
            setattr(node, k, v)
        return node

    def link(self, src, dst):
        if isinstance(src, (int, float)):
            dst.default_value = src
        elif isinstance(src, tuple):
            dst.default_value = src
        else:
            self.nt.links.new(src, dst)

    def math(self, op, a, b=None, clamp=False):
        node = self.n("ShaderNodeMath", operation=op, use_clamp=clamp)
        self.link(a, node.inputs[0])
        if b is not None:
            self.link(b, node.inputs[1])
        return node.outputs[0]

    def vec(self, op, a, b=None, scale=None):
        node = self.n("ShaderNodeVectorMath", operation=op)
        self.link(a, node.inputs[0])
        if b is not None:
            self.link(b, node.inputs[1])
        if scale is not None:
            self.link(scale, node.inputs[3])
        return node.outputs[1] if op in ("LENGTH", "DOT_PRODUCT", "DISTANCE") else node.outputs[0]

    def combine(self, x, y, z=0.0):
        node = self.n("ShaderNodeCombineXYZ")
        self.link(x, node.inputs[0]); self.link(y, node.inputs[1]); self.link(z, node.inputs[2])
        return node.outputs[0]

    def noise(self, vector, w, scale, detail, rough, distortion=0.0):
        # 3D noise drifting through its third axis: half the cost of 4D noise, same look on a flat card.
        node = self.n("ShaderNodeTexNoise", noise_dimensions="3D")
        self.link(self.vec("ADD", vector, self.combine(0.0, 0.0, w)), node.inputs["Vector"])
        node.inputs["Scale"].default_value = scale
        node.inputs["Detail"].default_value = detail
        node.inputs["Roughness"].default_value = rough
        node.inputs["Distortion"].default_value = distortion
        return node.outputs["Fac"]

    def remap(self, value, a, b, smooth=True):
        node = self.n("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP" if smooth else "LINEAR", clamp=True)
        self.link(value, node.inputs["Value"])
        node.inputs["From Min"].default_value = a
        node.inputs["From Max"].default_value = b
        return node.outputs["Result"]

    def ramp(self, fac, stops):
        node = self.n("ShaderNodeValToRGB")
        cr = node.color_ramp
        cr.elements[0].position, cr.elements[0].color = stops[0][0], (*stops[0][1], 1)
        cr.elements[1].position, cr.elements[1].color = stops[-1][0], (*stops[-1][1], 1)
        for pos, col in stops[1:-1]:
            cr.elements.new(pos).color = (*col, 1)
        self.link(fac, node.inputs["Fac"])
        return node.outputs["Color"]


def energy_material(name, half_w, half_h, kind, accept_time=0.0):
    m = bpy.data.materials.new(name)
    nodes_on(m)
    nt = m.node_tree
    nt.nodes.clear()
    g = G(nt)
    T = _time_socket(nt)
    inten = g.n("ShaderNodeValue")
    inten.name = "Intensity"
    inten.outputs[0].default_value = 0.0
    I = inten.outputs[0]
    obj = g.n("ShaderNodeTexCoord").outputs["Object"]
    p = g.vec("MULTIPLY", obj, (1.0 / half_w, 1.0 / half_h, 0.0))
    r = g.vec("LENGTH", p)
    d = g.vec("NORMALIZE", p)
    if kind == "smoke":
        # Smoke pushed outward from the paddle and rising, blue at the core fading through navy into black.
        q = g.vec("SUBTRACT", g.vec("SCALE", p, scale=1.7), g.vec("SCALE", d, scale=g.math("MULTIPLY", T, 0.26)))
        q = g.vec("ADD", q, g.combine(0.0, g.math("MULTIPLY", T, -0.34), 0.0))
        n1 = g.noise(q, g.math("MULTIPLY", T, 0.14), 1.35, 2.5, 0.5, 0.7)
        n2 = g.noise(g.vec("SCALE", q, scale=2.1), g.math("MULTIPLY", T, 0.2), 1.6, 1.0, 0.45)
        mix = g.math("ADD", g.math("MULTIPLY", n1, 0.62), g.math("MULTIPLY", n2, 0.38))
        smoke = g.remap(mix, 0.36, 0.80)
        ell = g.vec("LENGTH", g.vec("MULTIPLY", p, (1.0, 0.92, 0.0)))
        mask = g.remap(ell, 1.0, 0.32)
        core = g.remap(r, 0.6, 0.0)
        density = g.math("MULTIPLY", g.math("ADD", smoke, g.math("MULTIPLY", core, 0.32)), mask, clamp=True)
        tone = g.math("MULTIPLY", density, g.math("SUBTRACT", 1.08, g.math("MULTIPLY", r, 0.55)))
        colour = g.ramp(tone, [(0.0, (0, 0, 0)), (0.30, (0.0, 0.005, 0.02)), (0.52, (0.01, 0.07, 0.36)),
                                (0.78, (0.10, 0.38, 1.0)), (1.0, (0.70, 0.88, 1.0))])
        gain = 2.6
        alpha_gain = 1.5
    else:
        # Energy waves rippling out of the avatar ring, with flowing streams between them.
        sep = g.n("ShaderNodeSeparateXYZ")
        g.link(p, sep.inputs[0])
        wob = g.noise(g.vec("SCALE", d, scale=1.8), g.math("MULTIPLY", T, 0.55), 2.2, 1.0, 0.5)
        rr = g.math("ADD", r, g.math("MULTIPLY", g.math("SUBTRACT", wob, 0.5), 0.14))
        phase = g.math("SUBTRACT", g.math("MULTIPLY", rr, 3.0), g.math("MULTIPLY", g.math("SUBTRACT", T, accept_time), 1.25))
        tri = g.math("SUBTRACT", 1.0, g.math("MULTIPLY", g.math("ABSOLUTE", g.math("SUBTRACT", g.math("FRACT", phase), 0.5)), 2.0))
        waves = g.math("POWER", tri, 5.0)
        stream_v = g.vec("SUBTRACT", g.vec("SCALE", p, scale=2.4), g.vec("SCALE", d, scale=g.math("MULTIPLY", T, 0.55)))
        streams = g.remap(g.noise(stream_v, g.math("MULTIPLY", T, 0.3), 2.0, 2.0, 0.55), 0.5, 0.8)
        inner = g.remap(r, 0.36, 0.5)
        outer = g.remap(r, 1.0, 0.5)
        energy = g.math("ADD", g.math("MULTIPLY", waves, 0.8), g.math("MULTIPLY", streams, 0.6))
        density = g.math("MULTIPLY", g.math("MULTIPLY", energy, inner), outer, clamp=True)
        tone = density
        colour = g.ramp(tone, [(0.0, (0, 0, 0)), (0.3, (0.01, 0.10, 0.45)), (0.68, (0.18, 0.52, 1.0)), (1.0, (0.82, 0.94, 1.0))])
        gain = 3.4
        alpha_gain = 1.0
    em = g.n("ShaderNodeEmission")
    g.link(colour, em.inputs["Color"])
    g.link(g.math("MULTIPLY", g.math("MULTIPLY", density, I), gain), em.inputs["Strength"])
    tr = g.n("ShaderNodeBsdfTransparent")
    mixs = g.n("ShaderNodeMixShader")
    g.link(g.math("MINIMUM", g.math("MULTIPLY", g.math("MULTIPLY", density, I), alpha_gain), 1.0), mixs.inputs["Fac"])
    nt.links.new(tr.outputs[0], mixs.inputs[1])
    nt.links.new(em.outputs[0], mixs.inputs[2])
    out = g.n("ShaderNodeOutputMaterial")
    nt.links.new(mixs.outputs[0], out.inputs["Surface"])
    try:
        m.surface_render_method = "BLENDED"
    except Exception:
        pass
    return m


def set_intensity(m, frame, value):
    s_ = m.node_tree.nodes["Intensity"].outputs[0]
    s_.default_value = value
    s_.keyframe_insert("default_value", frame=frame)


def card(name, half_w, half_h, mat, parent, loc):
    def build(bm):
        bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
        for v in bm.verts:
            v.co.x *= 2 * half_w
            v.co.y *= 2 * half_h
    return mesh_object(name, build, mat, parent, loc)


def facing_anchor(name, parent, loc):
    a = empty(name, loc, parent)
    c = a.constraints.new("TRACK_TO")
    c.target, c.track_axis, c.up_axis = cam, "TRACK_Z", "UP_Y"
    return a


# Smoke behind each paddle (follows the rally), and a large plume behind the crossed paddles.
AURA_MATS = []
for side, rig in rigs.items():
    am = energy_material("Paddle Aura %d" % side, 1.55, 2.05, "smoke")
    anchor = facing_anchor("Paddle Aura Anchor %d" % side, rig, (0, 0, HEAD * 0.78))
    card("Paddle Aura", 1.55, 2.05, am, anchor, (0, 0, -0.55))
    for f, v in ((1, 0.35), (FL - 12, 0.45), (FL, 1.0), (FL + 24, 0.7), (END, 0.75)):
        set_intensity(am, f, v)
    AURA_MATS.append(am)
plume = energy_material("Hero Plume", 4.4, 3.4, "smoke")
plume_anchor = facing_anchor("Hero Plume Anchor", None, C + Vector((0, 0.2, 0.5)))
plume_card = card("Hero Plume", 4.4, 3.4, plume, plume_anchor, (0, 0, -1.0))
for f, v in ((FL - 2, 0.0), (FL + 2, 1.6), (FL + 22, 1.0), (END, 1.05)):
    set_intensity(plume, f, v)
for f, sc in ((FL - 1, 0.45), (FL + 10, 1.06), (FL + 24, 1.0)):
    plume_card.scale = (sc, sc, sc)
    plume_card.keyframe_insert("scale", frame=f)
AURA_MATS.append(plume)


# ----------------------------------------------------------------------------- UI preview layer (camera space, 8 units ahead)
UI_Z = -8.0
hud = empty("HUD", parent=cam)
hud.location = (0, 0, UI_Z)


def ui_in(o_mats, start, dur=8, frm=0.0):
    for m in o_mats:
        set_fade(m, start - 1, frm)
        set_fade(m, start + dur, 1.0)


title_m = glow("Title", GOLD, 1.25, 0)
title = text("Title", "ĐÃ TÌM THẤY TRẬN", 0.34, title_m, FONT_BOLD, hud, (0, 1.70, 0))
mode_m = glow("Mode", WHITE, 0.9, 0)
mode = text("Mode", "ĐẤU ĐƠN  ·  1v1", 0.15, mode_m, FONT_BOLD, hud, (0, 1.38, 0))
ready_m = glow("All Ready", MINT, 1.3, 0)
ready_t = text("All Ready", "TẤT CẢ ĐÃ SẴN SÀNG", 0.34, ready_m, FONT_BOLD, hud, (0, 1.70, 0))
ui_in([title_m, mode_m], FL + 4)
set_fade(title_m, F_ALL - 1, 1.0); set_fade(title_m, F_ALL + 4, 0.0)
set_fade(ready_m, F_ALL + 4, 0.0); set_fade(ready_m, F_ALL + 10, 1.0)
for f, y in ((FL + 3, 1.95), (FL + 12, 1.70)):
    title.location.y = y
    title.keyframe_insert("location", index=1, frame=f)

# Accept button (champagne) -> confirmed (glass)
btn = empty("Accept Button", parent=hud)
btn.location = (0, -1.62, 0)
btn_gold_m = glow("Button Gold", (0.93, 0.80, 0.58), 1.0, 0)
btn_glass_m = glow("Button Glass", (0.05, 0.075, 0.12), 1.0, 0)
rounded_rect("Button Face", 2.5, 0.46, 0.08, btn_gold_m, btn)
rounded_rect("Button Glass", 2.5, 0.46, 0.08, btn_glass_m, btn, (0, 0, -0.002))
btn_edge_m = glow("Button Edge", GOLD, 1.2, 0)
rounded_rect("Button Edge", 2.54, 0.50, 0.09, btn_edge_m, btn, (0, 0, -0.004))
btn_t1_m = glow("Button Label", INK, 1.0, 0)
text("Button Label", "CHẤP NHẬN", 0.21, btn_t1_m, FONT_BOLD, btn, (0, 0, 0.003))
btn_t2_m = glow("Button Done", GOLD, 1.2, 0)
text("Button Done", "ĐÃ XÁC NHẬN", 0.21, btn_t2_m, FONT_BOLD, btn, (0, 0, 0.003))
ui_in([btn_gold_m, btn_t1_m], FL + 10)
set_fade(btn_edge_m, FL + 9, 0.0); set_fade(btn_edge_m, FL + 18, 0.0)
for f, s in ((FL + 9, 0.9), (FL + 18, 1.0), (F_ACCEPT_L - 3, 1.0), (F_ACCEPT_L, 0.93), (F_ACCEPT_L + 5, 1.0)):
    btn.scale = (s, s, s)
    btn.keyframe_insert("scale", frame=f)
for m in (btn_gold_m, btn_t1_m):
    set_fade(m, F_ACCEPT_L, 1.0); set_fade(m, F_ACCEPT_L + 4, 0.0)
for m in (btn_glass_m, btn_edge_m):
    set_fade(m, F_ACCEPT_L + 1, 0.0); set_fade(m, F_ACCEPT_L + 6, 1.0)
set_fade(btn_t2_m, F_ACCEPT_L + 4, 0.0); set_fade(btn_t2_m, F_ACCEPT_L + 9, 1.0)


def avatar(side, initial, name, accept_at):
    root = empty("Avatar %s" % name, parent=hud)
    x = side * 2.42
    halo_m = glow("Halo %s" % name, (0.28, 0.58, 1.0), 1.6, 0, radial=True)
    halo = mesh_object("Halo", lambda bm: bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.25), halo_m, root, (0, 0, -0.02))
    disc_m = glow("Disc %s" % name, (0.025, 0.04, 0.07), 1.0, 0)
    disc("Disc", 0.60, disc_m, root, (0, 0, -0.01))
    ring_m = glow("Ring %s" % name, GOLD, 0.55, 0)
    torus("Ring", 0.64, 0.032, ring_m, root)
    ini_m = glow("Initial %s" % name, MUTED, 1.0, 0)
    text("Initial", initial, 0.62, ini_m, FONT_BOLD, root, (0, -0.02, 0.01))
    name_m = glow("Name %s" % name, WHITE, 1.0, 0)
    text("Name", name, 0.23, name_m, FONT_BOLD, root, (0, -0.97, 0))
    wait_m = glow("Waiting %s" % name, MUTED, 1.0, 0)
    text("Waiting", "ĐANG CHỜ…", 0.16, wait_m, FONT_BOLD, root, (0, -1.27, 0))
    ok_m = glow("Ready %s" % name, MINT, 1.3, 0)
    text("Ready", "ĐÃ SẴN SÀNG", 0.16, ok_m, FONT_BOLD, root, (0, -1.27, 0))
    pulse_m = glow("Pulse %s" % name, (0.7, 0.88, 1.0), 4, 0)
    wave_m = energy_material("Avatar Waves %s" % name, 1.55, 1.55, "waves", accept_time=(accept_at - 1) / FPS)
    card("Avatar Waves", 1.55, 1.55, wave_m, root, (0, 0, -0.035))
    AURA_MATS.append(wave_m)
    pulse = torus("Pulse", 0.64, 0.02, pulse_m, root)
    check_root = empty("Check", parent=root)
    check_root.location = (0.50, -0.50, 0.05)
    badge_m = glow("Check Badge %s" % name, MINT, 1.4, 1)
    disc("Check Badge", 0.17, badge_m, check_root, segs=48)
    mark_m = glow("Check Mark %s" % name, INK, 1.0, 1)
    def tick(bm):
        pts = [(-0.075, 0.0), (-0.02, -0.055), (0.08, 0.06)]
        w = 0.03
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            d = Vector((x1 - x0, y1 - y0, 0)).normalized()
            nrm = Vector((-d.y, d.x, 0)) * w
            v = [bm.verts.new(c) for c in (Vector((x0, y0, 0.002)) - nrm - d * w * 0.6, Vector((x1, y1, 0.002)) - nrm + d * w * 0.6,
                                           Vector((x1, y1, 0.002)) + nrm + d * w * 0.6, Vector((x0, y0, 0.002)) + nrm - d * w * 0.6)]
            bm.faces.new(v)
    mesh_object("Check Mark", tick, mark_m, check_root)
    # Slide in from the screen edge.
    for f, px, s in ((F_IN - 1, side * 4.4, 0.78), (F_IN + 14, x, 0.9)):
        root.location = (px, -0.05, 0)
        root.scale = (s, s, s)
        root.keyframe_insert("location", frame=f)
        root.keyframe_insert("scale", frame=f)
    ui_in([disc_m, ring_m, ini_m, name_m, wait_m], F_IN, 10)
    # Waiting: the ring breathes softly.
    for f in range(F_IN + 10, accept_at, 3):
        set_emit(ring_m, f, strength=0.45 + 0.25 * (0.5 + 0.5 * math.sin((f - F_IN) * 0.16)))
    # Accept: the avatar lights up.
    A = accept_at
    set_emit(ring_m, A, strength=0.5, color=GOLD)
    set_emit(ring_m, A + 5, strength=7.0, color=(0.85, 0.94, 1.0))
    set_emit(ring_m, A + 18, strength=3.4, color=(0.55, 0.78, 1.0))
    set_emit(ini_m, A, color=MUTED, strength=1.0)
    set_emit(ini_m, A + 5, color=(0.95, 0.98, 1.0), strength=2.0)
    set_emit(disc_m, A, color=(0.025, 0.04, 0.07))
    set_emit(disc_m, A + 6, color=(0.02, 0.07, 0.2))
    for f, v in ((A, 0.0), (A + 4, 1.5), (A + 22, 0.85), (END, 0.9)):
        set_intensity(wave_m, f, v)
    set_fade(halo_m, A, 0.0); set_fade(halo_m, A + 6, 1.0)
    for f in range(A + 8, END + 1, 4):
        set_emit(halo_m, f, strength=1.4 + 0.4 * math.sin((f - A) * 0.12))
    set_fade(pulse_m, A - 1, 0.0); set_fade(pulse_m, A, 1.0); set_fade(pulse_m, A + 16, 0.0)
    for f, s in ((A, 1.0), (A + 16, 1.9)):
        pulse.scale = (s, s, s)
        pulse.keyframe_insert("scale", frame=f)
    for f, s in ((A + 2, 0.0), (A + 6, 1.25), (A + 10, 1.0)):
        check_root.scale = (s, s, s)
        check_root.keyframe_insert("scale", frame=f)
    set_fade(wait_m, A, 1.0); set_fade(wait_m, A + 4, 0.0)
    set_fade(ok_m, A + 4, 0.0); set_fade(ok_m, A + 9, 1.0)
    for f, s in ((A, 0.9), (A + 4, 0.98), (A + 12, 0.9)):
        root.scale = (s, s, s)
        root.keyframe_insert("scale", frame=f)
    return root


avatar(-1, "B", "Bạn", F_ACCEPT_L)
avatar(1, "S", "SmashKing", F_ACCEPT_R)

# Everyone ready: a gold wave from the VS ball.
ring_burst("Ready Wave", F_ALL, C_BALL + Vector((0, -0.06, 0)), Vector((0, -1, 0)), 0.5, 3.0, 18, strength=4)
for f, e in ((F_ALL - 1, 0), (F_ALL + 1, 450), (F_ALL + 12, 0)):
    key(flash.data, "energy", f, e)

for m_ in AURA_MATS:
    linear(m_.node_tree)

# Fade from black at the start.
fade_m = glow("Fade", (0, 0, 0), 1.0, 1.0)
fade_m.node_tree.nodes["Emit"].inputs["Strength"].default_value = 0.0
blk = mesh_object("Fade Card", lambda bm: bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6), fade_m, cam, (0, 0, -1.0))
set_fade(fade_m, 1, 1.0); set_fade(fade_m, 7, 0.0)
# The black card must use the Emit colour (black) when opaque: Fac=1 -> emission (black), 0 -> transparent.

# ----------------------------------------------------------------------------- compositor: soft bloom
try:
    tree = bpy.data.node_groups.new("Match Found Comp", "CompositorNodeTree")
    tree.interface.new_socket(name="Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    rl = tree.nodes.new("CompositorNodeRLayers")
    gl = tree.nodes.new("CompositorNodeGlare")
    gl.inputs["Type"].default_value = "Bloom"
    gl.inputs["Quality"].default_value = "Medium"
    gl.inputs["Threshold"].default_value = 1.0
    gl.inputs["Strength"].default_value = 0.4
    gl.inputs["Size"].default_value = 0.75
    out = tree.nodes.new("NodeGroupOutput")
    tree.links.new(rl.outputs["Image"], gl.inputs["Image"])
    tree.links.new(gl.outputs["Image"], out.inputs[0])
    scene.compositing_node_group = tree
    print("COMPOSITOR ok")
except Exception as error:
    print("COMPOSITOR skipped", error)

blend_path = os.path.join(HERE, "match_found_vs.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print("SAVED", blend_path)

os.makedirs(OUT, exist_ok=True)
scene.render.image_settings.file_format = "PNG"
if MODE == "stills":
    frames = sorted(set([1, 6, FH1, FH1 + 8, FH2 - 6, FH2, FH2 + 10, FH3, FH3 + 8, FL - 6, FL, FL + 3, F_IN + 16,
                         F_ACCEPT_L + 10, F_ACCEPT_R + 10, F_ALL + 6, END]))
    for f in frames:
        scene.frame_set(f)
        scene.render.filepath = os.path.join(OUT, "still_%03d.png" % f)
        bpy.ops.render.render(write_still=True)
    print("STILLS", frames)
elif MODE.startswith("frame"):
    for f in [int(x) for x in MODE[5:].split(",")]:
        scene.frame_set(f)
        scene.render.filepath = os.path.join(OUT, "frame_%03d.png" % f)
        bpy.ops.render.render(write_still=True)
elif MODE.startswith("range"):
    # re-render chosen spans into the animation frame names, e.g. range50-70,137-150
    for span in MODE[5:].split(","):
        a_, b_ = (int(x) for x in span.split("-"))
        for f in range(a_, b_ + 1):
            scene.frame_set(f)
            scene.render.filepath = os.path.join(OUT, "f_%04d.png" % f)
            bpy.ops.render.render(write_still=True)
elif MODE == "anim":
    scene.render.filepath = os.path.join(OUT, "f_")
    bpy.ops.render.render(animation=True)
    print("ANIM done")
