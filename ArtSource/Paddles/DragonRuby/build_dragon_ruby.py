"""Dragon Ruby paddle - model, bake and export (Blender 5.1, script only).

    blender --background --factory-startup --python build_dragon_ruby.py -- <work_dir> build      build + bake + save the blend
    blender --background --factory-startup --python build_dragon_ruby.py -- <work_dir> export <unity_dir>
    blender --background --factory-startup --python build_dragon_ruby.py -- <work_dir> render
    blender --background --factory-startup --python build_dragon_ruby.py -- <work_dir> thumbnail <png>

<work_dir> must hold the output of make_face_maps.py (face_*.png, outline.json) and sculpt_pommel.py (pommel.npz).
Paddle space: metres, x right, z up, painted front faces -y, pommel bottom at z = 0, height 0.412.

The head is displaced from the painted relief; the grip set (collar, leather, two ruby dragon bodies coiling down the grip with
gold trims and fins, and the sculpted dragon-head pommel) is baked from a high-resolution build into one sheet.
"""
import json, math, os, sys
import bpy, bmesh
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:]
WORK, MODE = ARGS[0], ARGS[1]
BLEND = os.path.join(HERE, 'DragonRubyPaddle.blend')
TEX = int(os.environ.get('DR_TEX', '2048'))

TOP_Z = .4118                      # reference pixel row 8
OUT = json.load(open(os.path.join(WORK, 'outline.json')))
MM = OUT['mm_per_px'] * 1e-3
RX, RY = .0200, .0172              # leather grip radii
COLLAR = (.1405, .1640)


def z_of(y):
    return TOP_Z - (y - 8) * MM


def face_uv(x, z):
    """planar front projection into the face sheet"""
    px = x / MM
    y = (TOP_Z - z) / MM + 8
    u = (px + OUT['half_canvas_px']) * OUT['tex_scale']
    v = 1 - (y - OUT['top_px']) * OUT['tex_scale']
    return u, v


SWATCH_RUBY, SWATCH_GOLD, SWATCH_BLACK = (.90, .86), (.90, .5), (.90, .15)


# ---- generic helpers -------------------------------------------------------------------------------------------------
def mesh_object(name, verts, faces, mats=None, uv=None, smooth=True, coll=None):
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
    (coll or bpy.context.collection).objects.link(o)
    return o


def grid_faces(nu, nv, close_u=False):
    f = []
    for j in range(nv - 1):
        for i in range(nu if close_u else nu - 1):
            a, b = j * nu + i, j * nu + (i + 1) % nu
            f.append((a, b, b + nu, a + nu))
    return f


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


def recalc(o):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()


def outward(o):
    """closed meshes: faces wound so the signed volume is positive (the game culls back faces)"""
    me = o.data
    co = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, np.int32)
    me.loop_triangles.foreach_get('vertices', tri)
    a, b, c = (co[tri.reshape(-1, 3)[:, i]] for i in range(3))
    if np.einsum('ij,ij->i', a, np.cross(b, c)).sum() < 0:
        me.flip_normals()
        print('FLIPPED', o.name)


def check_outward(o):
    """share of faces that look away from the paddle's centre line: a guard against culled parts"""
    good = sum(1 for p in o.data.polygons if p.normal.dot(Vector((p.center.x, p.center.y, 0))) > 0)
    print('OUTWARD', o.name, round(good / max(len(o.data.polygons), 1), 3))


def image(name, path, colorspace='sRGB'):
    img = bpy.data.images.load(path, check_existing=True)
    img.name = name
    img.colorspace_settings.name = colorspace
    return img


# ---- outline -----------------------------------------------------------------------------------------------------------
def head_outline(n):
    """open polyline from the left neck, over the top, to the right neck (x, z), evenly spaced"""
    y = np.array(OUT['y_px'], float)
    h = np.array(OUT['half_px'], float)
    keep = y <= 762
    x, z = h[keep] * MM, z_of(y[keep])
    right = np.stack([x, z], 1)
    right[0, 0] = 0
    # round the crown: replace the first millimetres with a smooth arc
    k = 11
    xc = right[k, 0]
    zc = right[k, 1]
    t = np.linspace(0, 1, 24)[:-1]
    crown = np.stack([xc * t, right[0, 1] + .0004 - (right[0, 1] + .0004 - zc) * t ** 3.0], 1)
    right = np.vstack([crown, right[k:]])
    path = np.vstack([right[::-1] * [-1, 1], right[1:]])
    s = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    q = np.linspace(0, s[-1], n)
    p = np.stack([np.interp(q, s, path[:, 0]), np.interp(q, s, path[:, 1])], 1)
    for _ in range(6):
        p[1:-1] = .25 * p[:-2] + .5 * p[1:-1] + .25 * p[2:]
    p[:, 0] = (p[:, 0] - p[::-1, 0]) / 2
    p[:, 1] = (p[:, 1] + p[::-1, 1]) / 2
    return p


def inward(p):
    t = np.gradient(p, axis=0)
    t /= np.linalg.norm(t, axis=1, keepdims=True)
    n = np.stack([t[:, 1], -t[:, 0]], 1)
    return n


# front half of the rim: inset from the outer edge (mm), depth towards the front (mm), material zone
RIM = [(0.00, 0.0, 'r'), (0.05, 4.6, 'r'), (.75, 7.0, 'r'), (2.3, 8.35, 'r'), (4.5, 8.5, 'r'), (6.0, 7.7, 'r'),
       (6.35, 8.25, 'g'), (6.95, 8.50, 'g'), (7.45, 7.9, 'g'), (7.75, 5.9, 'g')]


def build_head(mat, seg, face_res, relief):
    objs = []
    p = head_outline(seg)
    n = inward(p)
    prof = [(d, -y, zone) for d, y, zone in RIM[::-1]] + [(d, y, zone) for d, y, zone in RIM[1:]]
    nu = len(prof)
    verts, zone = [], [z for _, _, z in prof]
    for i in range(seg):
        for d, y, _ in prof:
            q = p[i] + n[i] * d * 1e-3
            verts.append((q[0], y * 1e-3, q[1]))
    faces = grid_faces(nu, seg)
    uv = []
    for f in faces:
        ks = [v % nu for v in f]
        gold = all(zone[k] == 'g' for k in ks) or (sum(zone[k] == 'g' for k in ks) >= 2 and any(abs(prof[k][0] - 6.0) < .1 for k in ks))
        mean_y = np.mean([verts[v][1] for v in f])
        for v in f:
            x, y, z = verts[v]
            k = v % nu
            if gold:
                uv.append((SWATCH_GOLD[0] + .02 * math.sin(v), SWATCH_GOLD[1] + .05 * (z - .28)))
            elif abs(mean_y) < .0062:
                sw = SWATCH_GOLD if np.mean([verts[q][2] for q in f]) < .1835 else SWATCH_RUBY
                uv.append((sw[0] + .02 * math.sin(v), sw[1] + .05 * (z - .28)))
            else:
                uv.append(face_uv(x if y < 0 else -x, z))
    rim = mesh_object('Rim', verts, faces, [mat], uv)
    recalc(rim)
    objs.append(rim)

    # painted relief, front; the back is the same sheet turned around so the dragon reads correctly on both sides
    img = bpy.data.images.load(os.path.join(WORK, 'face_height.png'))
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size
    hm = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(hm)
    hm = hm.reshape(h, w, 4)[..., 0]
    bpy.data.images.remove(img)
    inner = p + n * 7.3e-3
    zs = np.linspace(inner[:, 1].min() + 1e-4, inner[:, 1].max() - 1e-5, face_res[1])
    half = []
    r = inner[len(inner) // 2:]
    order = np.argsort(r[:, 1])
    half = np.interp(zs, r[order, 1], r[order, 0])
    nx = face_res[0]
    verts = []
    for j, z in enumerate(zs):
        for i in range(nx):
            x = half[j] * (2 * i / (nx - 1) - 1)
            u, v = face_uv(x, z)
            fx, fy = np.clip(u * w - .5, 0, w - 1.001), np.clip(v * h - .5, 0, h - 1.001)
            ix, iy = int(fx), int(fy)
            a, b = fx - ix, fy - iy
            height = (hm[iy, ix] * (1 - a) + hm[iy, ix + 1] * a) * (1 - b) + (hm[iy + 1, ix] * (1 - a) + hm[iy + 1, ix + 1] * a) * b
            edge = min(1, (half[j] - abs(x)) / 3e-3, (zs[-1] - z) / 3e-3)
            verts.append((x, -(6.3e-3 + relief * float(height) * max(edge, 0) ** .5), z))
    faces = grid_faces(nx, len(zs))
    front = mesh_object('Face_Front', verts, faces, [mat])
    recalc(front)
    if np.mean([poly.normal.y for poly in front.data.polygons]) > 0:
        front.data.flip_normals()
    return objs, front


def finish_face(front, mat, ratio):
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


# ---- grip ----------------------------------------------------------------------------------------------------------------
def surf(theta, z, lift=0.0, rx=RX, ry=RY):
    """point and outward normal on the grip: theta 0 = front, +pi/2 = right"""
    s, c = math.sin(theta), math.cos(theta)
    nrm = Vector((s / rx, -c / ry, 0)).normalized()
    return Vector((rx * s, -ry * c, z)) + nrm * lift, nrm


def coil_path(n):
    """the dragon body zigzags over the front of the grip exactly as painted"""
    z = np.linspace(.1475, .0525, 1200)
    phase = np.pi * (.1312 - z) / .0288
    k = .90
    theta = 1.60 * np.arcsin(k * np.sin(phase)) / math.asin(k)
    pts = [surf(t, zz)[0] for t, zz in zip(theta, z)]
    s = np.concatenate([[0], np.cumsum([(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)])])
    q = np.linspace(0, s[-1], n)
    return np.interp(q, s, theta), np.interp(q, s, z), s[-1]


def build_coil(n, across, width, rise, turn, name, mats, gold_sides, uv_scale=1.0):
    th, zz, length = coil_path(n)
    objs = []
    centre, nrms = [], []
    for t, z in zip(th, zz):
        p, nn = surf(t, z)
        centre.append(p)
        nrms.append(nn)
    tang = []
    for i in range(n):
        a, b = centre[max(i - 1, 0)], centre[min(i + 1, n - 1)]
        tang.append((b - a).normalized())
    # lateral direction in the unrolled surface so the strip hugs the grip through the tight turns
    verts, uv_of = [], []
    edges = [[], []]
    for i in range(n):
        u = i / (n - 1)
        w = width * (1 - .25 * max(0, (u - .9) / .1)) * (.75 + .25 * min(1, u / .06))
        lat = tang[i].cross(nrms[i]).normalized()
        dth = lat.x * math.cos(th[i]) / RX * 1.0 + lat.y * math.sin(th[i]) / RY
        dz = lat.z
        for j in range(across):
            a = j / (across - 1) * 2 - 1
            prof = math.sqrt(max(0, 1 - abs(a) ** 2.4))
            p, _ = surf(th[i] + dth * a * w / 2, zz[i] + dz * a * w / 2, rise * prof + .0002)
            verts.append(p)
            uv_of.append(((a + 1) / 2, u * length / width * uv_scale))
        for e, a in enumerate((-1, 1)):
            p, nn = surf(th[i] + dth * a * w * .49, zz[i] + dz * a * w * .49, .0011)
            edges[e].append((p + nn * .0004, nn, lat * a))
    faces = grid_faces(across, n)
    uv = [uv_of[v] for f in faces for v in f]
    body = mesh_object(name, verts, faces, [mats['scales']], uv)
    recalc(body)
    mid = body.data.polygons[(n // 2) * (across - 1) + across // 2]
    c, _ = mid.center, 0
    if Vector((c.x, c.y, 0)).dot(mid.normal) < 0:
        body.data.flip_normals()
    objs.append(body)
    # gold trims and dorsal fins
    for e in range(2):
        pts = [p for p, _, _ in edges[e]]
        verts, faces = [], []
        for i in range(n):
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
            nn = edges[e][i][1]
            b = t.cross(nn).normalized()
            r = .00165 * (1 + .12 * math.sin(i * 60.0 / n * 1.9))
            for k in range(gold_sides):
                ang = 2 * math.pi * k / gold_sides
                verts.append(pts[i] + (nn * math.cos(ang) + b * math.sin(ang)) * r)
        faces = grid_faces(gold_sides, n, close_u=True)
        trim = mesh_object(name + '_trim%d' % e, verts, faces, [mats['gold']], [(0, 0)] * (4 * len(faces)))
        recalc(trim)
        objs.append(trim)
    e = 0 if sum(edges[0][i][2].z for i in range(n)) > sum(edges[1][i][2].z for i in range(n)) else 1
    fv, ff = [], []
    step = n / (26 if e is not None else 26)
    for q in range(25):
        i = int((q + .8) * step)
        if i + 2 >= n:
            break
        p, nn, out = edges[e][i]
        t = (edges[e][min(i + 1, n - 1)][0] - edges[e][i - 1][0]).normalized()
        big = 1.35 if q % 3 == 1 else .85
        root = p + nn * .0002
        tip = p + out * .0036 * big - t * .0052 * big + nn * .0017 * big
        half = .0020 * big
        a, b = root + t * half, root - t * half
        ridge = p + nn * .0016 + out * .0008
        low = p - nn * .0004 + out * .0004
        mid = (root + tip) / 2 + t * .0010 * big + nn * .0007
        base = len(fv)
        fv += [a, b, ridge, low, mid, tip]
        ff += [(base, base + 2, base + 4), (base + 2, base + 1, base + 4), (base, base + 4, base + 3), (base + 3, base + 4, base + 1),
               (base, base + 4, base + 5), (base + 4, base + 1, base + 5)]
    fins = mesh_object(name + '_fins', fv, ff, [mats['gold']], [(0, 0)] * (3 * len(ff)))
    recalc(fins)
    objs.append(fins)
    if turn:
        for o in objs:
            for v in o.data.vertices:
                v.co.x, v.co.y = -v.co.x, -v.co.y
    return objs


def revolve(name, profile, seg, mat, rx0=0, ry0=0):
    """profile: list of (extra radius in metres over the leather, z)"""
    verts = []
    for dr, z in profile:
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append(((RX + dr) * math.sin(a), -(RY + dr) * math.cos(a), z))
    faces = grid_faces(seg, len(profile), close_u=True)
    o = mesh_object(name, verts, faces, [mat], [(0, 0)] * (4 * len(faces)))
    recalc(o)
    up = sum(p.normal.z + p.normal.dot(Vector((p.center.x, p.center.y, 0)).normalized()) for p in o.data.polygons)
    if up < 0:
        o.data.flip_normals()
    return o


def ring_profile(z0, z1, r0, r1, k):
    """half-round moulding between two heights"""
    out = []
    for i in range(k + 1):
        a = math.pi * i / k
        out.append((r0 + (r1 - r0) * math.sin(a), z0 + (z1 - z0) * (1 - math.cos(a)) / 2))
    return out


def build_collar(mats, seg, k):
    z0, z1 = COLLAR
    objs = [revolve('Collar_LowerRing', ring_profile(z0, z0 + .0042, .0008, .0042, k), seg, mats['gold']),
            revolve('Collar_Band', ring_profile(z0 + .0034, z1 - .0030, .0020, .0038, max(k, 4)), seg, mats['enamel']),
            revolve('Collar_UpperRing', ring_profile(z1 - .0040, z1, .0006, .0046, k), seg, mats['gold']),
            revolve('Collar_Cap', [(.0020, z1 - .0012), (-.006, z1 + .0012), (-.0150, z1 + .0016)], seg, mats['gold'])]
    return objs


def build_leather(mat, seg, rows, wraps):
    verts = []
    for j in range(rows):
        z = .0400 + (.1500 - .0400) * j / (rows - 1)
        for i in range(seg):
            a = 2 * math.pi * i / seg
            dr = 0
            if wraps:
                q = (z + .0020 * math.sin(a * 1.0 + 1.0) + .0009 * math.sin(a * 2 + z * 220)) / .0128
                f = q - math.floor(q)
                dr = .00055 * math.sin(math.pi * f) ** .5 - .0011 * math.exp(-((min(f, 1 - f)) / .045) ** 2)
                dr += .00018 * math.sin(a * 3 + q * 2.2) * math.sin(q * 3.1)
            verts.append(((RX + dr) * math.sin(a), -(RY + dr) * math.cos(a), z))
    faces = grid_faces(seg, rows, close_u=True)
    o = mesh_object('Leather', verts, faces, [mat], [(0, 0)] * (4 * len(faces)))
    recalc(o)
    return o


def tube(points, radius, sides, mat, name):
    verts = []
    n = len(points)
    for i in range(n):
        p, nn = points[i]
        t = (points[min(i + 1, n - 1)][0] - points[max(i - 1, 0)][0]).normalized()
        b = t.cross(nn).normalized()
        r = radius * min(1, .35 + 4 * min(i, n - 1 - i) / n)
        for k in range(sides):
            a = 2 * math.pi * k / sides
            verts.append(p + (nn * math.cos(a) * .8 + b * math.sin(a)) * r)
    faces = grid_faces(sides, n, close_u=True)
    o = mesh_object(name, verts, faces, [mat], [(0, 0)] * (4 * len(faces)))
    recalc(o)
    return o


def spiral(theta, z, r, turns, direction, start, lift, rx, ry, n=46):
    pts = []
    for i in range(n):
        t = i / (n - 1)
        a = start + direction * turns * 2 * math.pi * t
        rad = r * (1 - .86 * t)
        pts.append(surf(theta + rad * math.cos(a) / RX, z + rad * math.sin(a), lift, rx, ry))
    return pts


def dome(theta, z, r, rise, mat):
    verts, rings, seg = [], 6, 26
    for k in range(rings + 1):
        t = k / rings
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append(surf(theta + r * t * math.cos(a) / RX, z + r * t * math.sin(a), .0002 + rise * math.sqrt(max(0, 1 - t ** 3)))[0])
    o = mesh_object('dome', verts, grid_faces(seg, rings + 1, close_u=True), [mat], [(0, 0)] * (4 * seg * rings))
    recalc(o)
    return o


def build_scrolls(mats):
    """high-resolution only: gold filigree on the collar band and gold clouds on the leather"""
    objs = []
    zc = (COLLAR[0] + COLLAR[1]) / 2
    rx, ry = RX + .0036, RY + .0036
    for q in range(10):
        th = 2 * math.pi * q / 10
        d = 1 if q % 2 else -1
        objs.append(tube(spiral(th, zc + .0007 * d, .0040, 1.25, d, math.pi * .5 * d, .0002, rx, ry), .00062, 7, mats['gold'], 'scroll'))
        objs.append(tube(spiral(th + .16, zc - .0016 * d, .0026, 1.1, -d, -math.pi * .5 * d, .0002, rx, ry), .00048, 7, mats['gold'], 'scroll'))
        wave = [surf(th + .63 * t, zc + .0022 * math.sin(t * 2 * math.pi) * d, .0002, rx, ry) for t in np.linspace(0, 1, 30)]
        objs.append(tube(wave, .00042, 6, mats['gold'], 'scroll'))
    cloud = [(0, 0, 4.2), (-5.2, -1.2, 3.3), (5.4, -1.0, 3.4), (-9.4, -2.6, 2.3), (9.8, -2.4, 2.4), (-2.6, 3.0, 2.6), (2.8, 3.4, 2.6)]
    for th0, z0, s in ((-.05, .0648, 1.0), (.02, .1330, .62), (math.pi - .05, .0648, 1.0), (math.pi + .02, .1330, .62),
                       (1.5, .1005, .7), (-1.5, .0800, .7), (math.pi * .52, .0700, .6), (-math.pi * .52, .1180, .6)):
        for x, z, r in cloud:
            r *= s * 1e-3
            objs.append(tube(spiral(th0 + x * s * 1e-3 / RX, z0 + z * s * 1e-3, r, 1.5, 1 if x >= 0 else -1, math.pi * (.75 if x >= 0 else .25), .0011, RX, RY),
                             .00062 * (.6 + .4 * s), 7, mats['gold'], 'cloud'))
            objs.append(dome(th0 + x * s * 1e-3 / RX, z0 + z * s * 1e-3, r * 1.02, .0009, mats['gold']))
    return objs


def pommel_mesh(name, mats_list):
    d = np.load(os.path.join(WORK, 'pommel.npz'))
    v = d['verts'] / 1000
    me = bpy.data.meshes.new(name)
    me.from_pydata(v.tolist(), [], d['faces'].tolist())
    for m in mats_list:
        me.materials.append(m)
    fm = d['mats'][d['faces']].astype(int)
    votes = np.stack([(fm == k).sum(1) for k in range(len(mats_list))], 1)
    me.polygons.foreach_set('material_index', votes.argmax(1).astype(np.int32))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.uv_layers.new(name='UVMap')
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    outward(o)
    return o


# ---- bake materials --------------------------------------------------------------------------------------------------------
class Mat:
    """procedural material whose channels can be rerouted to an emission shader for baking"""

    def __init__(self, name):
        self.m = bpy.data.materials.new(name)
        self.m.use_nodes = True
        self.t = self.m.node_tree
        self.t.nodes.clear()
        self.out = self.t.nodes.new('ShaderNodeOutputMaterial')
        self.p = self.t.nodes.new('ShaderNodeBsdfPrincipled')
        self.e = self.t.nodes.new('ShaderNodeEmission')
        self.ch = {}
        self.t.links.new(self.p.outputs[0], self.out.inputs[0])

    def n(self, kind, **kw):
        node = self.t.nodes.new(kind)
        for k, v in kw.items():
            setattr(node, k, v)
        return node

    def link(self, a, b):
        self.t.links.new(a, b)

    def val(self, v):
        if hasattr(v, 'is_linked'):
            return v
        if isinstance(v, (int, float)):
            node = self.n('ShaderNodeValue')
            node.outputs[0].default_value = v
            return node.outputs[0]
        node = self.n('ShaderNodeRGB')
        node.outputs[0].default_value = (*v, 1)
        return node.outputs[0]

    def math(self, op, a, b=0.0, c=None, clamp=False):
        node = self.n('ShaderNodeMath', operation=op, use_clamp=clamp)
        for i, x in enumerate((a, b, c)):
            if x is None:
                continue
            if hasattr(x, 'is_linked'):
                self.link(x, node.inputs[i])
            else:
                node.inputs[i].default_value = x
        return node.outputs[0]

    def mix(self, f, a, b):
        node = self.n('ShaderNodeMix', data_type='RGBA')
        self.link(self.val(f), node.inputs[0])
        self.link(self.val(a), node.inputs[6])
        self.link(self.val(b), node.inputs[7])
        return node.outputs[2]

    def ramp(self, f, lo, hi):
        node = self.n('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
        self.link(f, node.inputs[0])
        node.inputs[1].default_value, node.inputs[2].default_value = lo, hi
        return node.outputs[0]

    def noise(self, scale, detail=4, rough=.55, dist=0.0):
        tc = self.n('ShaderNodeTexCoord')
        node = self.n('ShaderNodeTexNoise')
        self.link(tc.outputs['Object'], node.inputs['Vector'])
        node.inputs['Scale'].default_value = scale
        node.inputs['Detail'].default_value = detail
        node.inputs['Roughness'].default_value = rough
        node.inputs['Distortion'].default_value = dist
        return node.outputs[0]

    def voronoi(self, scale, feature='DISTANCE_TO_EDGE'):
        tc = self.n('ShaderNodeTexCoord')
        node = self.n('ShaderNodeTexVoronoi', feature=feature)
        self.link(tc.outputs['Object'], node.inputs['Vector'])
        node.inputs['Scale'].default_value = scale
        return node.outputs['Distance']

    def ao(self, dist):
        node = self.n('ShaderNodeAmbientOcclusion', samples=8)
        node.inputs['Distance'].default_value = dist
        return node.outputs['AO']

    def edge(self, lo=.5, hi=.6):
        return self.ramp(self.n('ShaderNodeNewGeometry').outputs['Pointiness'], lo, hi)

    def bump(self, h, strength, distance, base=None):
        node = self.n('ShaderNodeBump')
        node.inputs['Strength'].default_value = strength
        node.inputs['Distance'].default_value = distance
        self.link(h, node.inputs['Height'])
        if base is not None:
            self.link(base, node.inputs['Normal'])
        return node.outputs[0]

    def done(self, color, metal, rough, emit=0.0, normal=None, coat=0.0):
        self.ch = {'base': self.val(color), 'metal': self.val(metal), 'rough': self.val(rough), 'emit': self.val(emit)}
        self.link(self.ch['base'], self.p.inputs['Base Color'])
        self.link(self.ch['metal'], self.p.inputs['Metallic'])
        self.link(self.ch['rough'], self.p.inputs['Roughness'])
        self.p.inputs['Coat Weight'].default_value = coat
        if normal is not None:
            self.link(normal, self.p.inputs['Normal'])
        return self

    def route(self, channel):
        if channel is None:
            self.link(self.p.outputs[0], self.out.inputs[0])
        else:
            self.link(self.ch[channel], self.e.inputs[0])
            self.link(self.e.outputs[0], self.out.inputs[0])


def scale_tile(path):
    """overlapping dragon scales, tiled across and along the body: R height, G per-scale variation, B edge"""
    n = 1024
    yy, xx = np.mgrid[0:n, 0:n] / n
    cols, rows = 5, 8
    best = np.full((n, n), -1.0)
    var = np.zeros((n, n))
    edge = np.zeros((n, n))
    rng = np.random.default_rng(3)
    table = rng.random((64, 64))
    for dj in range(-1, rows + 2):
        for di in range(-1, cols + 2):
            cx = (di + .5 * (dj % 2)) / cols
            cy = dj / rows
            dx = (xx - cx) * cols
            dy = (yy - cy) * rows
            # diamond scale, its tip covering the next row
            d = np.abs(dx) * 1.0 + np.abs(dy) * .62
            inside = d < .62
            h = (1 - (d / .62) ** 2.2) * .72 + .28 * np.clip(.5 + dy * .45, 0, 1)
            h = np.where(inside, h, -1)
            m = h > best
            best = np.where(m, h, best)
            var = np.where(m, table[dj % rows, di % cols], var)
            edge = np.where(m, np.clip((d / .62 - .72) / .28, 0, 1), edge)
    best = np.clip(best, 0, 1)
    px = np.stack([best, var, edge, np.ones_like(best)], -1).astype(np.float32)
    img = bpy.data.images.new('scale_tile', n, n, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(px.reshape(-1))
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    return img


def materials():
    M = {}
    # ruby skin of the pommel
    for key, deep, body, hot, fine in (('skin', (.022, .0006, .0012), (.46, .006, .009), (.95, .15, .05), .40),
                                       ('mane', (.018, .0005, .0010), (.52, .010, .009), (1.0, .34, .07), .06)):
        m = Mat('bake_' + key)
        ao = m.math('POWER', m.ao(.0040), 1.7)
        col = m.mix(ao, deep, body)
        col = m.mix(m.math('MULTIPLY', m.edge(.505, .60), .75), col, hot)
        col = m.mix(m.math('MULTIPLY', m.noise(260, 3), .25), col, deep)
        nrm = m.bump(m.voronoi(430), fine, .00035) if key == 'skin' else m.bump(m.noise(900, 2), fine, .0002)
        rough = m.math('ADD', .24, m.math('MULTIPLY', m.math('SUBTRACT', 1.0, ao), .34))
        M[key] = m.done(col, .14, rough, 0.0, nrm, .5)
    m = Mat('bake_gold')
    ao = m.math('POWER', m.ao(.0022), 1.2)
    col = m.mix(ao, (.24, .085, .014), (1.0, .64, .20))
    col = m.mix(m.math('MULTIPLY', m.edge(.505, .58), .8), col, (1.0, .86, .50))
    col = m.mix(m.math('MULTIPLY', m.ramp(m.noise(420, 4), .48, .75), .35), col, (.66, .28, .06))
    nrm = m.bump(m.noise(700, 3), .12, .0002)
    M['gold'] = m.done(col, 1.0, m.math('ADD', .20, m.math('MULTIPLY', m.math('SUBTRACT', 1.0, ao), .35)), 0.0, nrm)
    m = Mat('bake_black')
    ao = m.ao(.004)
    col = m.mix(ao, (.002, .002, .002), (.016, .013, .016))
    col = m.mix(m.math('MULTIPLY', m.edge(.51, .62), .5), col, (.10, .02, .02))
    M['black'] = m.done(col, .1, .40, 0.0, m.bump(m.voronoi(380), .25, .0003))
    m = Mat('bake_ivory')
    M['ivory'] = m.done(m.mix(m.ao(.002), (.32, .13, .03), (.95, .80, .50)), .05, .28)
    m = Mat('bake_eye')
    M['eye'] = m.done((1.0, .62, .10), .2, .15, 1.0)
    # black embossed leather: grain, engraved swirls from the contour lines of a warped noise
    m = Mat('bake_leather')
    ao = m.ao(.005)
    lines = m.math('PINGPONG', m.math('MULTIPLY', m.noise(78, 0, .4, 1.6), 5.0), .5)
    line = m.ramp(lines, .015, .075)
    grain = m.voronoi(820)
    col = m.mix(ao, (.0016, .0015, .0018), (.017, .016, .018))
    col = m.mix(m.math('MULTIPLY', m.math('SUBTRACT', 1.0, line), .55), col, (.003, .003, .0035))
    col = m.mix(m.math('MULTIPLY', m.edge(.505, .56), .5), col, (.05, .046, .05))
    nrm = m.bump(grain, .22, .00022, m.bump(line, .55, .00028))
    M['leather'] = m.done(col, .0, m.math('ADD', .42, m.math('MULTIPLY', m.noise(180, 3), .22)), 0.0, nrm)
    # coiling dragon body
    img = scale_tile(os.path.join(WORK, 'scale_tile.png'))
    m = Mat('bake_scales')
    tex = m.n('ShaderNodeTexImage', image=img)
    uv = m.n('ShaderNodeUVMap', uv_map='UVMap')
    mp = m.n('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (1, .64, 1)
    m.link(uv.outputs[0], mp.inputs[0])
    m.link(mp.outputs[0], tex.inputs[0])
    sep = m.n('ShaderNodeSeparateColor')
    m.link(tex.outputs[0], sep.inputs[0])
    h, var, edge = sep.outputs[0], sep.outputs[1], sep.outputs[2]
    ao = m.ao(.005)
    body = m.mix(var, (.17, .0016, .0032), (.46, .007, .008))
    col = m.mix(m.ramp(h, .22, .60), (.006, .0002, .0004), body)
    col = m.mix(m.math('MULTIPLY', m.ramp(h, .80, 1.0), .28), col, (.95, .13, .04))
    col = m.mix(m.math('MULTIPLY', m.ramp(var, .86, .94), .30), col, (1.0, .42, .12))
    col = m.mix(ao, (.010, .0004, .0008), col)
    rough = m.math('ADD', .20, m.math('MULTIPLY', m.math('SUBTRACT', 1.0, m.ramp(h, .05, .6)), .45))
    M['scales'] = m.done(col, .16, rough, 0.0, m.bump(h, 1.0, .0013), .6)
    # collar enamel
    m = Mat('bake_enamel')
    ao = m.ao(.004)
    v = m.ramp(m.voronoi(300), .0, .10)
    col = m.mix(ao, (.05, .001, .002), m.mix(v, (.09, .0015, .0025), (.40, .006, .009)))
    M['enamel'] = m.done(col, .12, .18, 0.0, m.bump(v, .4, .0003), .8)
    return M


# ---- build ---------------------------------------------------------------------------------------------------------------
def gpu():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for kind in ('OPTIX', 'CUDA'):
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


def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    M = materials()
    mats = {k: v.m for k, v in M.items()}
    pommel_mats = [mats['skin'], mats['gold'], mats['black'], mats['ivory'], mats['eye'], mats['mane']]

    # ---- game mesh
    face_mat = bpy.data.materials.new('DragonRuby_Face')
    grip_mat = bpy.data.materials.new('DragonRuby_Grip')
    rim, front = build_head(face_mat, 100, (170, 222), .0024)
    back = finish_face(front, face_mat, .040)
    head = join(rim + [front, back], 'DragonRuby_Head')

    g = {'gold': grip_mat, 'scales': grip_mat, 'enamel': grip_mat}
    low = []
    low += build_coil(84, 7, .0135, .0046, False, 'CoilA', g, 4)
    low += build_coil(84, 7, .0135, .0046, True, 'CoilB', g, 4)
    low += build_collar(g, 36, 4)
    low.append(build_leather(grip_mat, 24, 14, True))
    pm = pommel_mesh('PommelLow', [grip_mat])
    pm.data.materials.clear()
    pm.data.materials.append(grip_mat)
    pm.data.polygons.foreach_set('material_index', [0] * len(pm.data.polygons))
    activate(pm)
    mod = pm.modifiers.new('r', 'DECIMATE')
    mod.ratio = 9200 / len(pm.data.polygons)
    bpy.ops.object.modifier_apply(modifier='r')
    low.append(pm)
    for o in low:
        check_outward(o)
    grip = join(low, 'DragonRuby_Grip')
    grip.data.materials.clear()
    grip.data.materials.append(grip_mat)
    for poly in grip.data.polygons:
        poly.material_index = 0
    activate(grip)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(62), island_margin=.0035, area_weight=.3, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # ---- bake source
    high = []
    high += build_coil(420, 33, .0135, .0046, False, 'HiCoilA', mats, 10)
    high += build_coil(420, 33, .0135, .0046, True, 'HiCoilB', mats, 10)
    high += build_collar(mats, 160, 14)
    high.append(build_leather(mats['leather'], 220, 300, True))
    high += build_scrolls(mats)
    high.append(pommel_mesh('HiPommel', pommel_mats))
    hi = join(high, 'HighGrip')

    tris = lambda o: sum(len(p.vertices) - 2 for p in o.data.polygons)
    print('TRIS head', tris(head), 'grip', tris(grip), 'total', tris(head) + tris(grip), 'high', tris(hi))

    print('GPU', gpu())
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = .0009
    sc.render.bake.max_ray_distance = .0030
    sc.render.bake.margin = 12
    grip_mat.use_nodes = True
    node = grip_mat.node_tree.nodes.new('ShaderNodeTexImage')
    grip_mat.node_tree.nodes.active = node
    head.hide_render = False
    baked = {}
    for channel, kind, samples in (('base', 'EMIT', 24), ('metal', 'EMIT', 8), ('rough', 'EMIT', 16), ('emit', 'EMIT', 8), ('normal', 'NORMAL', 24), ('ao', 'AO', 96)):
        img = bpy.data.images.new('grip_' + channel, TEX, TEX, alpha=False, float_buffer=False)
        img.colorspace_settings.name = 'sRGB' if channel == 'base' else 'Non-Color'
        node.image = img
        for m in M.values():
            m.route(channel if kind == 'EMIT' else None)
        sc.cycles.samples = samples
        sc.render.bake.normal_space = 'TANGENT'
        activate(grip, [hi])
        bpy.ops.object.bake(type=kind, use_clear=True)
        img.filepath_raw = os.path.join(WORK, 'grip_%s.png' % channel)
        img.file_format = 'PNG'
        img.save()
        baked[channel] = img
        print('BAKED', channel, flush=True)
    for m in M.values():
        m.route(None)
    px = {k: np.empty(TEX * TEX * 4, np.float32) for k in ('metal', 'ao', 'emit', 'rough')}
    for k in px:
        baked[k].pixels.foreach_get(px[k])
    mask = np.stack([px['metal'].reshape(-1, 4)[:, 0], np.clip(px['ao'].reshape(-1, 4)[:, 0], .08, 1), px['emit'].reshape(-1, 4)[:, 0], 1 - px['rough'].reshape(-1, 4)[:, 0]], -1)
    img = bpy.data.images.new('grip_mask', TEX, TEX, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(mask.reshape(-1).astype(np.float32))
    img.filepath_raw = os.path.join(WORK, 'grip_mask.png')
    img.file_format = 'PNG'
    img.save()

    bpy.data.objects.remove(hi)
    final_materials(face_mat, grip_mat)
    root = bpy.data.objects.new('DragonRubyPaddle', None)
    bpy.context.collection.objects.link(root)
    for o in (head, grip):
        o.parent = root
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
    print('SAVED', BLEND)


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
        mul.inputs[0].default_value = .30
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
        es.inputs[1].default_value = 1.4
        t.links.new(sep.outputs[2], es.inputs[0])
        t.links.new(es.outputs[0], p.inputs['Emission Strength'])
        rng = t.nodes.new('ShaderNodeMapRange')
        rng.interpolation_type = 'SMOOTHSTEP'
        rng.inputs[1].default_value, rng.inputs[2].default_value, rng.inputs[4].default_value = .60, .74, .7
        t.links.new(k.outputs[1], rng.inputs[0])
        t.links.new(rng.outputs[0], p.inputs['Coat Weight'])
        p.inputs['Coat Roughness'].default_value = .08


# ---- studio render --------------------------------------------------------------------------------------------------------
def studio():
    sc = bpy.context.scene
    w = bpy.data.worlds.new('Studio')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (.030, .034, .045, 1)

    def area(name, loc, energy, size, color, target=(0, 0, .22)):
        l = bpy.data.lights.new(name, 'AREA')
        l.energy, l.size, l.color = energy, size, color
        o = bpy.data.objects.new(name, l)
        o.location = loc
        o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        bpy.context.collection.objects.link(o)

    area('Key', (-.45, -.75, .75), 24, .40, (1, .93, .84))
    area('Fill', (.75, -.55, .25), 8, .5, (.78, .88, 1))
    area('Rim', (.25, .75, .55), 14, .35, (1, .82, .6))
    area('Low', (-.1, -.5, -.25), 3.5, .4, (1, .6, .45))
    cam = bpy.data.cameras.new('Camera')
    o = bpy.data.objects.new('Camera', cam)
    bpy.context.collection.objects.link(o)
    sc.camera = o
    return o


def render():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    sc = bpy.context.scene
    cam = studio()
    print('GPU', gpu())
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    sc.render.image_settings.file_format = 'PNG'
    shots = {'front': ((0, -1.25, .206), (0, 0, .206), 85, (760, 1400)), 'threequarter': ((.62, -1.05, .34), (0, 0, .206), 85, (900, 1400)),
             'back': ((-.35, 1.2, .28), (0, 0, .206), 85, (900, 1400)), 'grip': ((.07, -.50, .12), (0, 0, .082), 85, (900, 1300)),
             'grip_side': ((.46, -.22, .12), (0, 0, .082), 85, (900, 1300)), 'face': ((.22, -.48, .36), (.0, 0, .29), 85, (1200, 1100))}
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
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - High Contrast'
    sc.view_settings.exposure = .35
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
    bpy.ops.export_scene.fbx(filepath=os.path.join(unity_dir, 'DragonRubyPaddle.fbx'), use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    import shutil
    for s, d in (('face_base', 'T_DragonRuby_Face_BaseColor'), ('face_normal', 'T_DragonRuby_Face_Normal'), ('face_mask', 'T_DragonRuby_Face_Mask'),
                 ('grip_base', 'T_DragonRuby_Grip_BaseColor'), ('grip_normal', 'T_DragonRuby_Grip_Normal'), ('grip_mask', 'T_DragonRuby_Grip_Mask')):
        shutil.copyfile(os.path.join(WORK, s + '.png'), os.path.join(unity_dir, d + '.png'))
        img = bpy.data.images.get(s)
        if img:                                   # the blend points at the game textures instead of carrying its own copies
            img.filepath = bpy.path.relpath(os.path.join(unity_dir, d + '.png'), start=os.path.dirname(BLEND))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
    print('EXPORTED', os.listdir(unity_dir))


{'build': build, 'render': render, 'export': lambda: export(ARGS[2]), 'thumbnail': lambda: thumbnail(ARGS[2])}[MODE]()
