"""Build the PickleVerse Elongated paddle (low poly, one texture atlas) and export it as GLB.

blender --background --factory-startup --python build_paddle.py -- [beauty_png]
Writes pv_paddle.blend and PV_Paddle_Elongated.glb next to this script.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import paddle_shape as ps

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BEAUTY = ARGS[0] if ARGS else None
TEX = os.path.join(HERE, "textures")


def nodes_on(idblock):
    try:
        idblock.use_nodes = True
    except Exception:
        pass


def bsdf_material(name):
    m = bpy.data.materials.new(name)
    nodes_on(m)
    nt = m.node_tree
    p = nt.nodes.get("Principled BSDF") or nt.nodes.new("ShaderNodeBsdfPrincipled")
    out = nt.nodes.get("Material Output") or nt.nodes.new("ShaderNodeOutputMaterial")
    if not p.outputs[0].is_linked:
        nt.links.new(p.outputs[0], out.inputs["Surface"])
    return m, p


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    albedo = bpy.data.images.load(os.path.join(TEX, "pv_paddle_albedo.png"))
    normal = bpy.data.images.load(os.path.join(TEX, "pv_paddle_normal.png"))
    normal.colorspace_settings.name = "Non-Color"

    # Face + grip share the atlas (one texture set); guard/collar are lacquered blue, the end cap black.
    atlas, p = bsdf_material("PV Paddle Atlas")
    nt = atlas.node_tree
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = albedo
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = normal
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = 1.0
    nt.links.new(ta.outputs["Color"], p.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], p.inputs["Normal"])
    p.inputs["Roughness"].default_value = 0.6
    p.inputs["Specular IOR Level"].default_value = 0.45

    guard, g = bsdf_material("PV Edge Guard")
    g.inputs["Base Color"].default_value = (0.010, 0.13, 0.85, 1)
    g.inputs["Metallic"].default_value = 0.55
    g.inputs["Roughness"].default_value = 0.2
    g.inputs["Coat Weight"].default_value = 1.0
    g.inputs["Coat Roughness"].default_value = 0.06

    cap, c = bsdf_material("PV End Cap")
    c.inputs["Base Color"].default_value = (0.012, 0.012, 0.014, 1)
    c.inputs["Roughness"].default_value = 0.35

    me = bpy.data.meshes.new("PV Paddle Elongated")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    mat_index = {"atlas": 0, "guard": 1, "cap": 2}

    def face(verts, uvs, mat):
        f = bm.faces.new(verts)
        f.material_index = mat_index[mat]
        f.smooth = True
        if uvs:
            for loop, co in zip(f.loops, uvs):
                loop[uv].uv = co
        return f

    # ---- core: front and back faces of the outline, 0.02 inside the edge (the guard lip covers the seam)
    path = ps.open_path()
    core = ps.inset(path, 0.02)
    front = [bm.verts.new((x, -ps.CORE_DEPTH, z)) for x, z in core]
    back = [bm.verts.new((x, ps.CORE_DEPTH, z)) for x, z in core]
    # outline runs clockwise seen from the front; reverse so the front face points to -Y
    f = face(front[::-1], [ps.face_uv(x, z) for x, z in core[::-1]], "atlas")
    f.smooth = False
    f = face(back, [(1 - ps.face_uv(x, z)[0], ps.face_uv(x, z)[1]) for x, z in core], "atlas")
    f.smooth = False

    # ---- edge guard: rounded profile swept along the open edge (ends hide inside the collar)
    prof = []
    a, b, r, seg = ps.GUARD_HALF, ps.GUARD_DEPTH, 0.03, 4
    centres = [(a - r, b - r, 0), (-(a - r), b - r, 90), (-(a - r), -(b - r), 180), (a - r, -(b - r), 270)]
    for cx, cy, start in centres:
        for i in range(seg + 1):
            t = math.radians(start + 90 * i / seg)
            prof.append((cx + r * math.cos(t), cy + r * math.sin(t)))
    ns = ps.normals(path)
    rings = []
    for (x, z), (nx, nz) in zip(path, ns):
        ring = [bm.verts.new((x + nx * pa, pb, z + nz * pa)) for pa, pb in prof]
        rings.append(ring)
    m = len(prof)
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(m):
            j = (i + 1) % m
            face([r0[i], r0[j], r1[j], r1[i]], None, "guard")

    # ---- handle: superellipse section, flared butt; UV on the grip strip
    def section(a_, b_, n=32, e=2.6):
        pts = []
        for i in range(n):
            t = 2 * math.pi * i / n
            c, s = math.cos(t), math.sin(t)
            pts.append((a_ * math.copysign(abs(c) ** (2 / e), c), b_ * math.copysign(abs(s) ** (2 / e), s)))
        return pts

    N = 32
    zs = [ps.GRIP_BOTTOM + (ps.GRIP_TOP - ps.GRIP_BOTTOM) * i / 18 for i in range(19)]
    grip_rings = []
    for z in zs:
        k = (z - ps.GRIP_BOTTOM) / 0.11
        flare = 1 + 0.12 * max(0.0, 1 - k) ** 2
        grip_rings.append([bm.verts.new((x, y, z)) for x, y in section(0.106 * flare, 0.084 * flare, N)])
    for ri in range(len(zs) - 1):
        v0 = (zs[ri] - ps.GRIP_BOTTOM) / (ps.GRIP_TOP - ps.GRIP_BOTTOM) * ps.FACE_V0
        v1 = (zs[ri + 1] - ps.GRIP_BOTTOM) / (ps.GRIP_TOP - ps.GRIP_BOTTOM) * ps.FACE_V0
        for i in range(N):
            j = i + 1
            u0, u1 = i / N, j / N
            face([grip_rings[ri][i], grip_rings[ri][j % N], grip_rings[ri + 1][j % N], grip_rings[ri + 1][i]],
                 [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], "atlas")

    # ---- end cap
    cap_rings = []
    for z, s in ((ps.GRIP_BOTTOM + 0.004, 1.13), (ps.GRIP_BOTTOM - 0.012, 1.14), (ps.GRIP_BOTTOM - 0.028, 1.06), (ps.GRIP_BOTTOM - 0.036, 0.8)):
        cap_rings.append([bm.verts.new((x, y, z)) for x, y in section(0.106 * s, 0.084 * s, N)])
    for r0, r1 in zip(cap_rings, cap_rings[1:]):
        for i in range(N):
            face([r0[i], r0[(i + 1) % N], r1[(i + 1) % N], r1[i]], None, "cap")
    face(cap_rings[-1][::-1], None, "cap")

    # ---- collar: lacquered blue band where the throat meets the handle
    col_rings = []
    for z, s in ((0.378, 0.92), (0.385, 1.0), (0.444, 1.0), (0.452, 0.92), (0.457, 0.82)):
        col_rings.append([bm.verts.new((x, y, z)) for x, y in section(0.118 * s, 0.09 * s, N, e=2.8)])
    for r0, r1 in zip(col_rings, col_rings[1:]):
        for i in range(N):
            face([r0[i], r0[(i + 1) % N], r1[(i + 1) % N], r1[i]], None, "guard")
    face(col_rings[0][::-1], None, "guard")

    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for mat in (atlas, guard, cap):
        me.materials.append(mat)
    ob = bpy.data.objects.new("PV Paddle Elongated", me)
    bpy.context.scene.collection.objects.link(ob)
    # Crisp flat face, smooth rounded guard/handle.
    wn = ob.modifiers.new("Weighted Normals", "WEIGHTED_NORMAL")
    wn.keep_sharp = True
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier=wn.name)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print("PADDLE tris", tris, "verts", len(me.vertices))
    return ob


ob = build()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "pv_paddle.blend"))
glb = os.path.join(HERE, "PV_Paddle_Elongated.glb")
bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True, export_apply=True,
                          export_image_format="AUTO", export_texcoords=True, export_normals=True,
                          export_materials="EXPORT", export_yup=True)
print("GLB", glb, os.path.getsize(glb) // 1024, "KB")

if BEAUTY:
    # Beauty shot like the reference: dark backdrop, blue under-glow, white key.
    s = bpy.context.scene
    s.render.engine = "BLENDER_EEVEE"
    s.eevee.taa_render_samples = 48
    s.eevee.use_raytracing = True
    s.render.resolution_x, s.render.resolution_y = 1000, 1560
    try:
        s.view_settings.view_transform = "AgX"
        s.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    w = bpy.data.worlds.new("W"); nodes_on(w); s.world = w
    bg = w.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1)
    ob.rotation_euler = (0, math.radians(-4), math.radians(-14))
    back_d = bpy.data.meshes.new("Backdrop")
    bmb = bmesh.new(); bmesh.ops.create_grid(bmb, x_segments=1, y_segments=1, size=6); bmb.to_mesh(back_d); bmb.free()
    bd = bpy.data.objects.new("Backdrop", back_d); s.collection.objects.link(bd)
    bd.location = (0, 3, 0.6); bd.rotation_euler = (math.radians(90), 0, 0)
    bm_m, bp = bsdf_material("Backdrop Glow")
    nt = bm_m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); e = nt.nodes.new("ShaderNodeEmission")
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    # glow centred low behind the grip, black towards the top (like the reference photo)
    mp.inputs["Location"].default_value = (-2.0, -1.32, 0); mp.inputs["Scale"].default_value = (4.0, 3.3, 0)
    gr = nt.nodes.new("ShaderNodeTexGradient"); gr.gradient_type = "SPHERICAL"
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].color = (0.25, 0.55, 1.0, 1)
    ramp.color_ramp.elements.new(0.62).color = (0.03, 0.16, 0.85, 1)
    ramp.color_ramp.elements.new(0.35).color = (0, 0, 0, 1)
    nt.links.new(tc.outputs["Generated"], mp.inputs["Vector"]); nt.links.new(mp.outputs["Vector"], gr.inputs["Vector"])
    nt.links.new(gr.outputs["Fac"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], e.inputs["Color"])
    e.inputs["Strength"].default_value = 1.4
    nt.links.new(e.outputs[0], o.inputs["Surface"])
    back_d.materials.append(bm_m)
    for name, loc, energy, col, size in (("Key", (-1.5, -4, 3.5), 350, (1, 1, 1), 2), ("Rim L", (-2.5, 1.5, 1.0), 300, (0.3, 0.55, 1.0), 1.5),
                                         ("Rim R", (2.5, 1.5, 1.5), 300, (0.3, 0.55, 1.0), 1.5), ("Fill", (0, -3, -1), 60, (0.6, 0.75, 1.0), 3)):
        ld = bpy.data.lights.new(name, "AREA"); ld.energy, ld.color, ld.size = energy, col, size
        lo = bpy.data.objects.new(name, ld); s.collection.objects.link(lo); lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 1.0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("Cam"); cd.lens = 70
    cam = bpy.data.objects.new("Cam", cd); s.collection.objects.link(cam); s.camera = cam
    cam.location = (0.0, -8.6, 0.95)
    cam.rotation_euler = (Vector((0, 0, 0.88)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    s.render.filepath = BEAUTY
    bpy.ops.render.render(write_still=True)
    print("BEAUTY", BEAUTY)
