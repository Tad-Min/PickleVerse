"""Blue Phoenix paddle - model, materials, renders and export (Blender 5.2, script only).

    blender --background --factory-startup --python build_blue_phoenix.py -- <work_dir> build              build + save the blend
    blender --background --factory-startup --python build_blue_phoenix.py -- <work_dir> render [shots]     studio renders r_<shot>.png
    blender --background --factory-startup --python build_blue_phoenix.py -- <work_dir> thumbnail <png>    1024 px shop thumbnail
    blender --background --factory-startup --python build_blue_phoenix.py -- <work_dir> export <unity_dir>  FBX + textures

<work_dir> must hold the output of make_phoenix_maps.py (face_*, grip_*, outline.json).
Paddle space: metres, x right, z up, painted front faces -y, pommel bottom at z = 0, height 0.4118.

Head: a silver frame swept along the painted outline, the painting as a real relief on both faces (displaced from the height
map, the back is the front turned round), and a side guard with a blue light line. Grip set: the shield with the phoenix
emblem, ribbed silver rings, the leather wrap with its spiral blue-fire band and two raised silver cords, and the barrel
pommel cap with its emblem.
"""
import json, math, os, sys
import bpy, bmesh
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:]
WORK, MODE = ARGS[0], ARGS[1]
BLEND = os.path.join(HERE, 'BluePhoenixPaddle.blend')
O = json.load(open(os.path.join(WORK, 'outline.json')))
MM = O['mm_per_px'] * 1e-3                 # metres per reference pixel
BOT, TOP = O['bottom_px'], O['top_px']
HD = O['head']
ROWS, HALF = np.array(O['rows'], float), np.array(O['half'], float)
RELIEF = .0024                             # height of the relief over the face plane
FACE_Y = .0063                             # face plane, from the centre
OVAL = .84                                 # grip section: depth / width


def hw(y):
    return float(np.interp(y, ROWS, HALF))


def z_of(y):
    return (BOT - y) * MM


def y_of(z):
    return BOT - z / MM


def face_uv(x, z):
    """planar front projection into the head sheet"""
    y = y_of(z)
    return (x / MM + HD['half_px']) * HD['scale'] / HD['size'], 1 - (y - HD['top_px']) * HD['scale'] / HD['size']


C = O['column']


def col_uv(x, z):
    """planar front projection into the painted column of the grip sheet"""
    y = y_of(z)
    return (C['x0'] + (x / MM + C['half_px']) * C['k']) / C['size'], 1 - (C['y0'] + (y - C['top_px']) * C['k']) / C['size']


U = O['unrolled']
WRAP_Z = (z_of(O['wrap'][1]), z_of(O['wrap'][0]))


def wrap_uv(theta, z):
    """unrolled leather wrap: a = 0.5 at the front, increasing towards +x"""
    a = theta / (2 * math.pi) + .5
    t = (z - WRAP_Z[0]) / (WRAP_Z[1] - WRAP_Z[0])
    k = U['size'] / C['size']
    return a * k, 1 - (1 - t) * k


GS, SW = O['grip_swatch'], O['swatch']


# ---- generic helpers -------------------------------------------------------------------------------------------------------
def mesh_object(name, verts, faces, mats=None, uv=None, smooth=True):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    if uv is not None:
        layer = me.uv_layers.new(name='UVMap')
        layer.data.foreach_set('uv', np.asarray(uv, np.float32).reshape(-1))
    for m in mats or []:
        me.materials.append(m)
    me.polygons.foreach_set('use_smooth', [smooth] * len(me.polygons))
    me.validate()
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def grid_faces(nu, nv):
    return [(j * nu + i, j * nu + i + 1, (j + 1) * nu + i + 1, (j + 1) * nu + i) for j in range(nv - 1) for i in range(nu - 1)]


def activate(o, others=()):
    bpy.ops.object.select_all(action='DESELECT')
    for x in (*others, o):
        x.select_set(True)
    bpy.context.view_layer.objects.active = o


def join(objs, name):
    activate(objs[0], objs[1:])
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def weld(o, dist=1e-6):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bm.to_mesh(o.data)
    bm.free()


def facing_out(o, centre=None):
    """turn the faces of a part outwards (the game culls back faces)"""
    me = o.data
    score = 0.0
    for p in me.polygons:
        c = p.center if centre is None else p.center - Vector(centre)
        score += p.normal.dot(Vector((c.x, c.y, 0)) if centre is None else c)
    if score < 0:
        me.flip_normals()


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def image(name, path, colorspace='sRGB'):
    img = bpy.data.images.load(path, check_existing=True)
    img.name = name
    img.colorspace_settings.name = colorspace
    return img


# ---- head ---------------------------------------------------------------------------------------------------------------------
HEAD_END = 979.0


def head_outline(n):
    """open polyline (x, z) from the left neck over the crown to the right neck, evenly spaced, symmetric"""
    ys = np.arange(TOP + 2, HEAD_END + .1, 1.0)
    xr = np.array([hw(y) for y in ys])
    xt = np.linspace(0, xr[0], 24)[:-1]
    yt = TOP - .6 + (ys[0] - TOP + .6) * (xt / xr[0]) ** 2.2
    right = np.concatenate([np.stack([xt, yt], 1), np.stack([xr, ys], 1)])
    path = np.vstack([right[::-1] * [-1, 1], right[1:]])
    path = np.stack([path[:, 0] * MM, z_of(path[:, 1])], 1)
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    q = np.linspace(0, s[-1], n)
    p = np.stack([np.interp(q, s, path[:, 0]), np.interp(q, s, path[:, 1])], 1)
    for _ in range(4):
        p[1:-1] = .25 * p[:-2] + .5 * p[1:-1] + .25 * p[2:]
    p[:, 0] = (p[:, 0] - p[::-1, 0]) / 2
    p[:, 1] = (p[:, 1] + p[::-1, 1]) / 2
    return p


def inward(p):
    t = np.gradient(p, axis=0)
    t /= np.linalg.norm(t, axis=1, keepdims=True)
    return np.stack([t[:, 1], -t[:, 0]], 1)


# front half of the frame: inset from the outer edge (mm), depth towards the front (mm)
RIM = [(0.0, 0.0), (0.04, 3.2), (0.25, 5.6), (.85, 7.4), (1.9, 8.35), (3.3, 8.55), (4.6, 8.2), (5.4, 7.4), (5.85, 6.6), (6.1, 6.3)]


def build_head(mat, seg, face_res):
    p = head_outline(seg)
    n = inward(p)
    if np.mean(n[len(n) // 2:, 0]) > 0:            # right flank must point inwards (-x)
        n = -n
    prof = [(d, -y) for d, y in RIM[::-1]] + [(d, y) for d, y in RIM[1:]]
    nu = len(prof)
    verts = []
    for i in range(seg):
        for d, y in prof:
            q = p[i] + n[i] * d * 1e-3
            verts.append((q[0], y * 1e-3, q[1]))
    faces = [(j * nu + i, j * nu + i + 1, (j + 1) * nu + i + 1, (j + 1) * nu + i) for j in range(seg - 1) for i in range(nu - 1)]
    g0, g1, gv0, gv1 = SW['guard']
    uv = []
    for f in faces:
        depth = np.mean([abs(verts[v][1]) for v in f])
        for v in f:
            x, y, z = verts[v]
            if depth > .0060:
                uv.append(face_uv(x if y < 0 else -x, z))
            else:
                k = v % nu
                d = prof[k][1]
                uv.append((g0 + (g1 - g0) * np.clip((d + 7.4) / 14.8, 0, 1), (gv0 + gv1) / 2 + .02 * math.sin(v * .37)))
    rim = mesh_object('Frame', verts, faces, [mat], uv)
    facing_out(rim)

    # painted relief on the front; the back is the same surface turned round
    hm = np.load(os.path.join(WORK, 'face_height.npy')).astype(np.float32)
    h, w = hm.shape
    inner = p + n * 5.95e-3
    zs = np.linspace(inner[:, 1].min() + 1e-4, inner[:, 1].max() - 1e-5, face_res[1])
    r = inner[len(inner) // 2:]
    order = np.argsort(r[:, 1])
    half = np.interp(zs, r[order, 1], r[order, 0])
    nx = face_res[0]
    verts = []
    for j, z in enumerate(zs):
        xs = half[j] * np.linspace(-1, 1, nx)
        for x in xs:
            u, v = face_uv(x, z)
            fx, fy = np.clip(u * w - .5, 0, w - 1.001), np.clip((1 - v) * h - .5, 0, h - 1.001)
            ix, iy = int(fx), int(fy)
            a, b = fx - ix, fy - iy
            hh = (hm[iy, ix] * (1 - a) + hm[iy, ix + 1] * a) * (1 - b) + (hm[iy + 1, ix] * (1 - a) + hm[iy + 1, ix + 1] * a) * b
            edge = min(1, (half[j] - abs(x)) / 3e-3, (zs[-1] - z) / 3e-3, (z - zs[0]) / 3e-3)
            verts.append((x, -(FACE_Y + RELIEF * float(hh) * max(edge, 0) ** .5), z))
    front = mesh_object('Face_Front', verts, grid_faces(nx, len(zs)), [mat])
    if np.mean([poly.normal.y for poly in front.data.polygons]) > 0:
        front.data.flip_normals()
    return rim, front


def finish_face(front, ratio):
    activate(front)
    m = front.modifiers.new('d', 'DECIMATE')
    m.ratio = ratio
    bpy.ops.object.modifier_apply(modifier='d')
    me = front.data
    layer = me.uv_layers.new(name='UVMap') if not me.uv_layers else me.uv_layers[0]
    for loop in me.loops:
        x, y, z = me.vertices[loop.vertex_index].co
        layer.data[loop.index].uv = face_uv(x, z)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    back = front.copy()
    back.data = me.copy()
    back.name = 'Face_Back'
    bpy.context.collection.objects.link(back)
    for v in back.data.vertices:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return back


# ---- grip set -------------------------------------------------------------------------------------------------------------------
def radius(y):
    return hw(y) * MM


def revolve(name, profile, seg, mat, uv_mode='column'):
    """profile: list of (half width in metres, z); elliptic section (depth = OVAL x width); seam column duplicated then welded"""
    verts, uvs = [], []
    for rx, z in profile:
        for i in range(seg + 1):
            th = -math.pi + 2 * math.pi * i / seg
            verts.append((rx * math.sin(th), -rx * OVAL * math.cos(th), z))
    nu = seg + 1
    faces = grid_faces(nu, len(profile))
    uv = []
    for f in faces:
        cy = np.mean([verts[v][1] for v in f])
        for v in f:
            x, y, z = verts[v]
            if uv_mode == 'dark':
                uv.append(GS['dark'])
            else:
                uv.append(col_uv(x if cy < 0 else -x, z))
    o = mesh_object(name, verts, faces, [mat], uv)
    weld(o)
    facing_out(o, (0, 0, float(np.mean([z for _, z in profile]))))
    return o


def ribs(y0, y1, count, swell, steps=6):
    """stacked half-round silver ribs between two reference rows"""
    out = []
    for k in range(count):
        a, b = y0 + (y1 - y0) * k / count, y0 + (y1 - y0) * (k + 1) / count
        for i in range(steps + 1):
            t = i / steps
            y = a + (b - a) * t
            out.append((radius(y) * .985 + swell * math.sin(math.pi * t) ** .7, z_of(y)))
    return out


def disc(name, rx, z, down, mat, seg=48):
    verts = [(0, 0, z)] + [(rx * math.sin(-math.pi + 2 * math.pi * i / seg), -rx * OVAL * math.cos(-math.pi + 2 * math.pi * i / seg), z) for i in range(seg)]
    faces = [(0, 1 + i, 1 + (i + 1) % seg) if not down else (0, 1 + (i + 1) % seg, 1 + i) for i in range(seg)]
    return mesh_object(name, verts, faces, [mat], [GS['dark']] * (3 * seg), smooth=False)


def build_shield(mat):
    """the shield under the head: a bevelled trapezoid with a crown arch, the painted emblem on both faces"""
    S = O['shield']
    top, bottom, crown = S['top'], S['bottom'], S['crown']
    pts = []
    for i in range(17):                               # crown arch, right to left
        a = math.pi * i / 16
        pts.append((S['crown_hw'] * math.cos(a), top - (top - crown) * math.sin(a) ** .8))
    pts += [(-S['top_hw'], top + 2), (-S['bottom_hw'], bottom), (S['bottom_hw'], bottom), (S['top_hw'], top + 2)]
    ring = np.array(pts, float)
    # resample evenly
    closed = np.vstack([ring, ring[:1]])
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(closed, axis=0), axis=1))])
    q = np.linspace(0, s[-1], 64)[:-1]
    ring = np.stack([np.interp(q, s, closed[:, 0]), np.interp(q, s, closed[:, 1])], 1)
    ring = np.stack([ring[:, 0] * MM, z_of(ring[:, 1])], 1)
    c = ring.mean(0)
    prof = [(0, 0), (0, .0086), (.0007, .0101), (.0016, .0106)]      # inset, depth
    layers = [(d, -y) for d, y in prof[::-1]] + [(d, y) for d, y in prof[1:]]
    n = len(ring)
    verts = []
    for d, y in layers:
        for x, z in ring:
            dirn = np.array([x, z]) - c
            dirn /= np.linalg.norm(dirn)
            verts.append((x - dirn[0] * d, y, z - dirn[1] * d))
    faces = []
    for j in range(len(layers) - 1):
        for i in range(n):
            faces.append((j * n + i, j * n + (i + 1) % n, (j + 1) * n + (i + 1) % n, (j + 1) * n + i))
    base = len(verts)
    verts += [(c[0], -.0106, c[1]), (c[0], .0106, c[1])]
    last = (len(layers) - 1) * n
    for i in range(n):
        faces.append((base, (i + 1) % n, i))                         # front cap (layer 0, y = -0.0106)
        faces.append((base + 1, last + i, last + (i + 1) % n))       # back cap
    uv = []
    for f in faces:
        cy = np.mean([verts[v][1] for v in f])
        for v in f:
            x, y, z = verts[v]
            uv.append(col_uv(x if cy < 0 else -x, z) if abs(cy) > .0080 else GS['dark'])
    o = mesh_object('Shield', verts, faces, [mat], uv)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()
    facing_out(o, (0, 0, float(c[1])))
    return o


def surf(theta, z, lift=0.0):
    rx = radius(y_of(z)) * .97
    ry = rx * OVAL
    s, co = math.sin(theta), math.cos(theta)
    nrm = Vector((s / rx, -co / ry, 0)).normalized()
    return Vector((rx * s, -ry * co, z)) + nrm * lift, nrm


def build_wrap(mat, seg, rows):
    verts, uv_of = [], []
    for j in range(rows):
        z = WRAP_Z[0] + (WRAP_Z[1] - WRAP_Z[0]) * j / (rows - 1)
        for i in range(seg + 1):
            th = -math.pi + 2 * math.pi * i / seg
            verts.append(surf(th, z)[0])
            uv_of.append(wrap_uv(th, z))
    faces = grid_faces(seg + 1, rows)
    o = mesh_object('Wrap', verts, faces, [mat], [uv_of[v] for f in faces for v in f])
    weld(o)
    facing_out(o, (0, 0, (WRAP_Z[0] + WRAP_Z[1]) / 2))
    return o


def build_cords(mat, segs, sides, r=.00085):
    """the two silver cords edging the blue band, wound with the same spiral as the texture"""
    objs = []
    length = U['length_mm']
    band, pitch, phase = U['band'], U['pitch_mm'], U['phase']
    for edge in (-1, 1):
        w0 = .5 - phase + .5 + edge * (band / 2 + .025)              # helix coordinate of the edge (see make_phoenix_maps)
        pts = []
        for i in range(segs + 1):
            Y = length * i / segs
            z = WRAP_Z[0] + (WRAP_Z[1] - WRAP_Z[0]) * Y / length
            a = (w0 - Y / pitch)
            th = 2 * math.pi * (a - .5)
            p, nn = surf(th, z, r * .55)
            pts.append((p, nn))
        verts, uv = [], []
        for i, (p, nn) in enumerate(pts):
            t = (pts[min(i + 1, segs)][0] - pts[max(i - 1, 0)][0]).normalized()
            b = t.cross(nn).normalized()
            taper = min(1, .35 + 6 * min(i, segs - i) / segs)
            for k in range(sides + 1):
                ang = 2 * math.pi * k / sides
                verts.append(p + (nn * math.cos(ang) + b * math.sin(ang)) * r * taper)
        faces = grid_faces(sides + 1, segs + 1)
        cu0, cu1 = GS['cord_u']
        for f in faces:
            for v in f:
                k = v % (sides + 1)
                ang = 2 * math.pi * k / sides
                uv.append((cu0 + (cu1 - cu0) * (.5 + .5 * math.cos(ang) * .9), GS['cord_v']))
        o = mesh_object('Cord', verts, faces, [mat], uv)
        weld(o)
        mid = o.data.polygons[len(o.data.polygons) // 2]
        if mid.normal.dot(mid.center - Vector((0, 0, mid.center.z))) < 0:
            o.data.flip_normals()
        objs.append(o)
    return objs


def build_cap(mat, seg):
    y0, y1 = O['cap']
    prof = []
    for y in np.linspace(y0, y1 - 9, 16):
        prof.append((radius(y), z_of(y)))
    rb = radius(y1 - 9)
    for i in range(1, 7):                                      # rounded lower edge
        a = math.pi / 2 * i / 6
        prof.append((rb - .0026 * (1 - math.cos(a)) - .0010 * math.sin(a) ** 4, z_of(y1 - 9) - .0024 * math.sin(a)))
    cap = revolve('Cap', prof, seg, mat)
    bottom = disc('CapBottom', prof[-1][0], prof[-1][1], True, mat, seg)
    return [cap, bottom]


# ---- materials (Blender previews; the game uses its own shader) ---------------------------------------------------------------
def final_materials(face_mat, grip_mat):
    for m, prefix in ((face_mat, 'face'), (grip_mat, 'grip')):
        m.use_nodes = True
        t = m.node_tree
        t.nodes.clear()
        out = t.nodes.new('ShaderNodeOutputMaterial')
        p = t.nodes.new('ShaderNodeBsdfPrincipled')
        t.links.new(p.outputs[0], out.inputs[0])
        b = t.nodes.new('ShaderNodeTexImage')
        b.image = image(prefix + '_base', os.path.join(WORK, prefix + '_base.png'))
        n = t.nodes.new('ShaderNodeTexImage')
        n.image = image(prefix + '_normal', os.path.join(WORK, prefix + '_normal.png'), 'Non-Color')
        k = t.nodes.new('ShaderNodeTexImage')
        k.image = image(prefix + '_mask', os.path.join(WORK, prefix + '_mask.png'), 'Non-Color')
        k.image.alpha_mode = 'CHANNEL_PACKED'
        sep = t.nodes.new('ShaderNodeSeparateColor')
        t.links.new(k.outputs[0], sep.inputs[0])
        mul = t.nodes.new('ShaderNodeMix')
        mul.data_type = 'RGBA'
        mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = .35
        t.links.new(b.outputs[0], mul.inputs[6])
        t.links.new(sep.outputs[1], mul.inputs[7])
        t.links.new(mul.outputs[2], p.inputs['Base Color'])
        t.links.new(sep.outputs[0], p.inputs['Metallic'])
        inv = t.nodes.new('ShaderNodeMath')
        inv.operation = 'SUBTRACT'
        inv.inputs[0].default_value = 1
        t.links.new(k.outputs[1], inv.inputs[1])
        t.links.new(inv.outputs[0], p.inputs['Roughness'])
        nm = t.nodes.new('ShaderNodeNormalMap')
        t.links.new(n.outputs[0], nm.inputs['Color'])
        t.links.new(nm.outputs[0], p.inputs['Normal'])
        t.links.new(b.outputs[0], p.inputs['Emission Color'])
        es = t.nodes.new('ShaderNodeMath')
        es.operation = 'MULTIPLY'
        es.inputs[1].default_value = 1.6
        t.links.new(sep.outputs[2], es.inputs[0])
        t.links.new(es.outputs[0], p.inputs['Emission Strength'])
        # clear coat only on the glossy enamel and silver, never on the leather or the stone ground
        rng = t.nodes.new('ShaderNodeMapRange')
        rng.interpolation_type = 'SMOOTHSTEP'
        rng.inputs[1].default_value, rng.inputs[2].default_value, rng.inputs[4].default_value = .70, .86, .45
        t.links.new(k.outputs[1], rng.inputs[0])
        t.links.new(rng.outputs[0], p.inputs['Coat Weight'])
        p.inputs['Coat Roughness'].default_value = .08


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    face_mat = bpy.data.materials.new('BluePhoenix_Face')
    grip_mat = bpy.data.materials.new('BluePhoenix_Grip')

    rim, front = build_head(face_mat, 112, (300, 400))
    back = finish_face(front, 4600 / (2 * 299 * 399))
    head = join([rim, front, back], 'BluePhoenix_Head')

    parts = [build_shield(grip_mat)]
    r1 = O['ring1']
    parts.append(revolve('Ring1', ribs(r1[0], r1[1], 2, .0011), 48, grip_mat))
    parts.append(disc('Ring1Top', radius(r1[0]) * .99, z_of(r1[0]), False, grip_mat))
    parts.append(build_wrap(grip_mat, 48, 46))
    parts += build_cords(grip_mat, 104, 6)
    r2 = O['ring2']
    parts.append(revolve('Ring2', ribs(r2[0], r2[1], 3, .0012), 48, grip_mat))
    parts += build_cap(grip_mat, 48)
    for o in parts:
        print('PART', o.name, tris(o))
    grip = join(parts, 'BluePhoenix_Grip')
    print('TRIS head', tris(head), 'grip', tris(grip), 'total', tris(head) + tris(grip))

    final_materials(face_mat, grip_mat)
    root = bpy.data.objects.new('BluePhoenixPaddle', None)
    bpy.context.collection.objects.link(root)
    for o in (head, grip):
        o.parent = root
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
    print('SAVED', BLEND)


# ---- studio --------------------------------------------------------------------------------------------------------------------
def gpu():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('CUDA', 'OPTIX'):
        try:
            prefs.compute_device_type = kind
            prefs.get_devices()
            for d in prefs.devices:
                d.use = d.type == kind
            sc.cycles.device = 'GPU'
            return kind
        except Exception:
            continue
    return 'CPU'


def studio():
    sc = bpy.context.scene
    w = bpy.data.worlds.new('Studio')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (.010, .013, .022, 1)

    def area(name, loc, energy, size, color, target=(0, 0, .22)):
        l = bpy.data.lights.new(name, 'AREA')
        l.energy, l.size, l.color = energy, size, color
        o = bpy.data.objects.new(name, l)
        o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        bpy.context.collection.objects.link(o)

    area('Key', (-.45, -.75, .75), 22, .40, (.92, .95, 1))
    area('Fill', (.75, -.55, .25), 8, .5, (.70, .82, 1))
    area('Rim', (.25, .75, .55), 16, .35, (.45, .65, 1))
    area('Low', (-.1, -.5, -.25), 3.0, .4, (.55, .7, 1))
    cam = bpy.data.cameras.new('Camera')
    o = bpy.data.objects.new('Camera', cam)
    bpy.context.collection.objects.link(o)
    sc.camera = o
    return o


def grade(sc, exposure):
    """Standard view: electric blue stays saturated instead of washing out to white like AgX does"""
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.view_settings.exposure = exposure


def render():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    sc = bpy.context.scene
    cam = studio()
    print('GPU', gpu())
    sc.cycles.samples = int(os.environ.get('BP_SAMPLES', '64'))
    sc.cycles.use_denoising = True
    grade(sc, 0.0)
    sc.render.image_settings.file_format = 'PNG'
    shots = {'front': ((0, -1.25, .206), (0, 0, .206), 85, (760, 1400)), 'threequarter': ((.62, -1.05, .34), (0, 0, .206), 85, (900, 1400)),
             'back': ((-.35, 1.2, .28), (0, 0, .206), 85, (900, 1400)), 'grip': ((.07, -.50, .12), (0, 0, .082), 85, (900, 1300)),
             'grip_side': ((.46, -.22, .12), (0, 0, .082), 85, (900, 1300)), 'face': ((.22, -.48, .36), (.0, 0, .29), 85, (1200, 1100)),
             'edge': ((.55, -.05, .30), (0, 0, .25), 85, (900, 1100))}
    only = ARGS[2].split(',') if len(ARGS) > 2 else list(shots)
    for name in only:
        loc, target, lens, res = shots[name]
        cam.location = loc
        cam.data.lens = lens
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.resolution_x, sc.render.resolution_y = res
        sc.render.filepath = os.path.join(WORK, 'r_%s.png' % name)
        bpy.ops.render.render(write_still=True)
        print('RENDERED', name, flush=True)


def thumbnail(path):
    """1024 px transparent shop thumbnail framed like the other paddles"""
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    sc = bpy.context.scene
    cam = studio()
    print('GPU', gpu())
    sc.cycles.samples = 160
    sc.cycles.use_denoising = True
    sc.render.film_transparent = True
    grade(sc, .15)
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.resolution_x = sc.render.resolution_y = 1024
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = .4118 * 1024 / 792
    loc, target = Vector((.32, -1.3, .30)), Vector((0, 0, .2059))
    cam.location = loc
    cam.rotation_euler = (target - loc).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print('THUMBNAIL', path)


def export(unity_dir):
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    os.makedirs(unity_dir, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.data.objects:
        if o.type in ('MESH', 'EMPTY'):
            o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(unity_dir, 'BluePhoenixPaddle.fbx'), use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    import shutil
    for s, d in (('face_base', 'T_BluePhoenix_Face_BaseColor'), ('face_normal', 'T_BluePhoenix_Face_Normal'), ('face_mask', 'T_BluePhoenix_Face_Mask'),
                 ('grip_base', 'T_BluePhoenix_Grip_BaseColor'), ('grip_normal', 'T_BluePhoenix_Grip_Normal'), ('grip_mask', 'T_BluePhoenix_Grip_Mask')):
        shutil.copyfile(os.path.join(WORK, s + '.png'), os.path.join(unity_dir, d + '.png'))
        img = bpy.data.images.get(s)
        if img:                                   # the blend points at the game textures instead of carrying its own copies
            img.filepath = bpy.path.relpath(os.path.join(unity_dir, d + '.png'), start=os.path.dirname(BLEND))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
    print('EXPORTED', os.listdir(unity_dir))


{'build': build, 'render': render, 'export': lambda: export(ARGS[2]), 'thumbnail': lambda: thumbnail(ARGS[2])}[MODE]()
