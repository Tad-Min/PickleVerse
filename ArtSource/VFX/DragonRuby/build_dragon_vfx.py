"""Dragon Ruby VFX - game mesh, emblem texture and a design preview of the sine flight (Blender 5.1, script only).

    blender --background --factory-startup --python build_dragon_vfx.py -- <work_dir> mesh    <unity_model_dir>
    blender --background --factory-startup --python build_dragon_vfx.py -- <work_dir> emblem  <unity_texture_dir>
    blender --background --factory-startup --python build_dragon_vfx.py -- <work_dir> preview [frames]

<work_dir> holds dragon.npz (sculpt_dragon.py) and, for the emblem, pommel.npz of the paddle (ArtSource/Paddles/DragonRuby).
The mesh is exported straight: Unity x = distance behind the ball, y = dorsal, z = lateral.
"""
import math, os, sys
import bpy
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:]
WORK, MODE = ARGS[0], ARGS[1]
TRIS = 5200
SS = np.array([.14, .24, .34, .55, .85, 1.20, 1.55, 1.85, 2.08, 2.25])
RR = np.array([.040, .040, .046, .058, .060, .053, .041, .028, .015, .004])


def trunk(rings=84, sides=12):
    """ring mesh of the body: ruby back, golden ribbed belly"""
    s = np.linspace(.15, 2.25, rings)
    r = np.interp(s, SS, RR)
    verts, cols = [], []
    for i in range(rings):
        rib = 1 + .045 * np.sin(2 * np.pi * s[i] / .05)
        for k in range(sides):
            a = 2 * np.pi * k / sides                       # 0 = belly
            belly = np.cos(a) > .55
            q = r[i] * (rib if belly else 1)
            verts.append((-s[i], -q * np.sin(a) * .94, -q * np.cos(a)))
            cols.append((.72, .26, .06, .10) if belly else (.50, .010, .012, .10))
    faces = []
    for i in range(rings - 1):
        for k in range(sides):
            a, b = i * sides + k, i * sides + (k + 1) % sides
            faces.append((a, a + sides, b + sides, b))
    faces.append(tuple(range((rings - 1) * sides, rings * sides)))
    me = bpy.data.meshes.new('trunk')
    me.from_pydata(verts, [], faces)
    at = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    at.data.foreach_set('color', np.asarray(cols, np.float32).reshape(-1))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    o = bpy.data.objects.new('trunk', me)
    bpy.context.collection.objects.link(o)
    return o


def load():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    d = np.load(os.path.join(WORK, 'dragon.npz'))
    v = d['verts']
    # Unity (x, y, z) = Blender (-x, z, -y)
    co = np.stack([-v[:, 0], -v[:, 2], v[:, 1]], 1)
    me = bpy.data.meshes.new('DragonRubySpirit')
    me.from_pydata(co.tolist(), [], d['faces'].tolist())
    a = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    a.data.foreach_set('color', d['col'].reshape(-1))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    o = bpy.data.objects.new('DragonRubySpirit', me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    m = o.modifiers.new('d', 'DECIMATE')
    m.ratio = TRIS / len(me.polygons)
    bpy.ops.object.modifier_apply(modifier='d')
    t = trunk()
    t.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.join()
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    print('DRAGON tris', len(o.data.polygons), 'verts', len(o.data.vertices))
    return o


def mesh(out):
    o = load()
    os.makedirs(out, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, 'DragonRubySpirit.fbx'), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', colors_type='LINEAR', bake_anim=False, path_mode='STRIP')
    print('EXPORTED', out)


def material():
    m = bpy.data.materials.new('spirit')
    m.use_nodes = True
    t = m.node_tree
    p = t.nodes['Principled BSDF']
    c = t.nodes.new('ShaderNodeVertexColor')
    c.layer_name = 'Col'
    t.links.new(c.outputs[0], p.inputs['Base Color'])
    t.links.new(c.outputs[0], p.inputs['Emission Color'])
    mul = t.nodes.new('ShaderNodeMath')
    mul.operation = 'MULTIPLY_ADD'
    t.links.new(c.outputs[1], mul.inputs[0])
    mul.inputs[1].default_value, mul.inputs[2].default_value = 2.2, .10
    t.links.new(mul.outputs[0], p.inputs['Emission Strength'])
    p.inputs['Roughness'].default_value = .32
    p.inputs['Metallic'].default_value = .15
    p.inputs['Coat Weight'].default_value = .5
    return m


def stage(cam_loc, target, res, lens=50):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Medium High Contrast'
    w = bpy.data.worlds.new('w')
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (.020, .026, .040, 1)
    for loc, e, c in (((-1, -2, 2), 90, (1, .9, .8)), ((2, 1.5, 1.2), 60, (.7, .85, 1)), ((-2.5, 1, .6), 50, (1, .55, .35))):
        l = bpy.data.lights.new('l', 'AREA')
        l.energy, l.size, l.color = e, 1.2, c
        ob = bpy.data.objects.new('l', l)
        ob.location = loc
        ob.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        bpy.context.collection.objects.link(ob)
    cam = bpy.data.cameras.new('c')
    cam.lens = lens
    co = bpy.data.objects.new('c', cam)
    co.location = cam_loc
    co.rotation_euler = (Vector(target) - Vector(cam_loc)).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(co)
    sc.camera = co
    return sc


def preview(frames):
    o = load()
    o.data.materials.append(material())
    rest = np.empty(len(o.data.vertices) * 3, np.float32)
    o.data.vertices.foreach_get('co', rest)
    rest = rest.reshape(-1, 3)
    s, lat, up = -rest[:, 0], -rest[:, 1], rest[:, 2]
    sc = stage((-.4, -5.2, 1.25), (-.35, 0, .15), (1280, 560), 50)
    ball = bpy.data.meshes.new('ball')
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=.037)
    bm.to_mesh(ball)
    bm.free()
    bo = bpy.data.objects.new('ball', ball)
    bpy.context.collection.objects.link(bo)
    bmat = bpy.data.materials.new('pearl')
    bmat.use_nodes = True
    bp = bmat.node_tree.nodes['Principled BSDF']
    bp.inputs['Base Color'].default_value = (1, .72, .25, 1)
    bp.inputs['Emission Color'].default_value = (1, .55, .12, 1)
    bp.inputs['Emission Strength'].default_value = 3
    ball.materials.append(bmat)
    A, LAM, GAP, RAMP = .20, 1.10, .13, .75
    for f in range(frames):
        head = -2.2 + 3.9 * f / max(frames - 1, 1)                    # ball position along the flight
        d = s + GAP
        x = head - d
        k = 2 * math.pi / LAM

        def wave(dd, xx):
            e = np.clip(dd / RAMP, 0, 1)
            e = e * e * (3 - 2 * e)
            return A * e * np.sin(k * xx)
        h = 1e-3
        y0 = wave(d, x)
        slope = (wave(d - h, x + h) - wave(d + h, x - h)) / (2 * h)
        tx, ty = 1 / np.sqrt(1 + slope ** 2), slope / np.sqrt(1 + slope ** 2)
        co = np.stack([x - ty * up, -lat, y0 + tx * up + .15], 1)
        o.data.vertices.foreach_set('co', co.astype(np.float32).reshape(-1))
        o.data.update()
        bo.location = (head, 0, .15)
        sc.render.filepath = os.path.join(WORK, 'pv_%03d.png' % f)
        bpy.ops.render.render(write_still=True)
    print('PREVIEW', frames)


def emblem(out):
    """dragon-head emblem for the contact flash: front view of the pommel head, white-hot core to red edge, alpha from the shape"""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    d = np.load(os.path.join(WORK, 'pommel.npz'))
    me = bpy.data.meshes.new('p')
    me.from_pydata((d['verts'] / 1000).tolist(), [], d['faces'].tolist())
    cols = [(.85, .05, .03), (1, .72, .22), (.10, .0, .0), (1, .95, .8), (1, 1, .7), (1, .22, .04)]
    for i, c in enumerate(cols):
        m = bpy.data.materials.new('m%d' % i)
        m.use_nodes = True
        b = m.node_tree.nodes['Principled BSDF']
        b.inputs['Base Color'].default_value = (*c, 1)
        b.inputs['Roughness'].default_value = .35
        b.inputs['Emission Color'].default_value = (*c, 1)
        b.inputs['Emission Strength'].default_value = (.25, .4, 0, .4, 3, .5)[i]
        me.materials.append(m)
    fm = d['mats'][d['faces']].astype(int)
    votes = np.stack([(fm == k).sum(1) for k in range(6)], 1)
    me.polygons.foreach_set('material_index', votes.argmax(1).astype(np.int32))
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.update()
    o = bpy.data.objects.new('p', me)
    bpy.context.collection.objects.link(o)
    sc = stage((.125, -.205, .034), (.002, 0, .032), (512, 512), 85)
    sc.render.film_transparent = True
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    for l in [x for x in bpy.data.objects if x.type == 'LIGHT']:
        l.location *= .12
        l.data.energy *= .022
        l.data.size *= .12
        l.rotation_euler = (Vector((0, 0, .03)) - l.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.filepath = os.path.join(out, 'T_DragonRuby_Emblem.png')
    os.makedirs(out, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    print('EMBLEM', sc.render.filepath)


{'mesh': lambda: mesh(ARGS[2]), 'emblem': lambda: emblem(ARGS[2]), 'preview': lambda: preview(int(ARGS[2]) if len(ARGS) > 2 else 4)}[MODE]()
