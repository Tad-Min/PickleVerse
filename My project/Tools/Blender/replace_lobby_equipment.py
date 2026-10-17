"""Replace only the lobby paddle/ball; keep the existing UI and motion.

Blender 5.1, XY is the UI plane and +Z faces the existing bake camera.
Run: blender --background Lobby_UI_Source.blend --python replace_lobby_equipment.py
The reference is used at its native pixel density, once, packed in the blend.
"""
import bpy
import json
import math
import os
import shutil
import struct
import sys
import zlib
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent.parent
REPO = PROJECT.parent
WORK = REPO / 'UnityWork' / 'LobbyPaddle20261016'
SOURCE = HERE / 'Lobby_UI_Source.blend'
REFERENCE = HERE / 'Paddle.png'
BALL_SOURCE = PROJECT / 'Assets/Models/ball/pickleball_ball_cartoon.glb'
WORK.mkdir(parents=True, exist_ok=True)
for required in (SOURCE, REFERENCE, BALL_SOURCE):
    if not required.is_file():
        raise FileNotFoundError(required)
backup = WORK / 'Lobby_UI_Source.before.blend'
if not backup.exists():
    shutil.copy2(SOURCE, backup)
if Path(bpy.data.filepath).resolve() != SOURCE.resolve():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
pivot = bpy.data.objects.get('Play_Strike_Pivot')
ball_pivot = bpy.data.objects.get('Play_Ball_Flight')
if pivot is None or ball_pivot is None:
    raise RuntimeError('Expected lobby paddle/ball animation pivots are missing.')

# Explicit scope: replace children of these two known pivots only.
removed = []
old_materials = set()
for root in (pivot, ball_pivot):
    for obj in list(root.children_recursive):
        removed.append(obj.name)
        data = obj.data
        if hasattr(data, 'materials'):
            old_materials.update(m for m in data.materials if m is not None)
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Curve):
                bpy.data.curves.remove(data)
for m in old_materials:
    if m.users==0:
        bpy.data.materials.remove(m)

# A lossless, tight crop retains the original weave and lettering. No upscaling,
# duplicate normal maps, text geometry or modeled pores are needed for a UI asset.
import numpy as np
reference = bpy.data.images.load(str(REFERENCE), check_existing=False)
w, h = reference.size[:]
pixels = np.asarray(reference.pixels[:], dtype=np.float32).reshape(h, w, 4)
crop = (224, 64, 864, 1416)  # top-left pixel coordinates, 640 x 1352, native scale
x0, y0, x1, y1 = crop
texture = bpy.data.images.get('Lobby_PickleVerse_Paddle_Reference')
if texture is not None:
    bpy.data.images.remove(texture)
texture = bpy.data.images.new('Lobby_PickleVerse_Paddle_Reference', x1-x0, y1-y0, alpha=True)
texture.colorspace_settings.name = 'sRGB'
texture.pixels.foreach_set(np.ascontiguousarray(pixels[h-y1:h-y0, x0:x1]).reshape(-1))
texture.update()
texture.file_format = 'PNG'
texture.filepath_raw = str(WORK / 'Paddle-native-crop.png')
texture.save()
# Recompress the existing PNG stream at level 9; retain every source pixel and
# color-profile chunk. This saves ~160 KB without resizing or lossy encoding.
png=Path(texture.filepath_raw).read_bytes()
chunks=[];compressed=[];offset=8
while offset<len(png):
    length,kind=struct.unpack_from('>I4s',png,offset)
    payload=png[offset+8:offset+8+length]
    if kind==b'IDAT':
        compressed.append(payload)
    else:
        chunks.append((kind,payload))
    offset+=length+12
def png_chunk(kind,payload):
    return struct.pack('>I',len(payload))+kind+payload+struct.pack('>I',zlib.crc32(kind+payload)&0xffffffff)
encoded=zlib.compress(zlib.decompress(b''.join(compressed)),9)
packed_png=png[:8]+b''.join(png_chunk(k,p) for k,p in chunks if k!=b'IEND')+png_chunk(b'IDAT',encoded)+png_chunk(b'IEND',b'')
Path(texture.filepath_raw).write_bytes(packed_png)
texture.pack(data=packed_png,data_len=len(packed_png))
texture.filepath = '//Textures/Paddle_Reference_Crop.png'  # packed, never overwrite the original reference on unpack
bpy.data.images.remove(reference)

def material(name, color, metallic=0.0, roughness=0.4, photo=False):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    m.diffuse_color = (*color, 1)
    nodes = m.node_tree.nodes
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    if photo:
        # The supplied image already contains studio shading. Emission reproduces
        # its exact sRGB colors rather than applying that lighting a second time.
        shader = nodes.new('ShaderNodeEmission')
        shader.inputs['Strength'].default_value = 1.0
        image = nodes.new('ShaderNodeTexImage')
        image.image = texture
        image.interpolation = 'Linear'
        image.extension = 'EXTEND'
        m.node_tree.links.new(image.outputs['Color'], shader.inputs['Color'])
    else:
        shader = nodes.new('ShaderNodeBsdfPrincipled')
        shader.inputs['Base Color'].default_value = (*color, 1)
        shader.inputs['Metallic'].default_value = metallic
        shader.inputs['Roughness'].default_value = roughness
    m.node_tree.links.new(shader.outputs[0], output.inputs['Surface'])
    return m

front = material('PKV Reference - Carbon, silver edge and lettering', (.12,.13,.16), photo=True)
edge = material('PKV Brushed blue silver sides', (.32,.43,.66), .82, .25)
white = material('PKV White wrapped grip sides', (.83,.87,.93), .04, .48)
black = material('PKV Black grip collar sides', (.008,.011,.018), .24, .32)

SCALE = 1.72 / (1404-71)
CENTER_X = 542.0
TOP = .85
def xy(px, py):
    return ((px-CENTER_X)*SCALE, TOP-(py-71)*SCALE)
def uv(px, py):
    return ((px-x0)/(x1-x0), (y1-py)/(y1-y0))

def create_mesh(name, verts, faces, mats, indices, uvs, smooth=None):
    mesh = bpy.data.meshes.new(name+'Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.parent = pivot
    for m in mats:
        mesh.materials.append(m)
    layer = mesh.uv_layers.new(name='ReferenceUV')
    for polygon, index in zip(mesh.polygons, indices):
        polygon.material_index = index
        polygon.use_smooth = polygon.index in (smooth or set())
        for li in polygon.loop_indices:
            layer.data[li].uv = uvs[mesh.loops[li].vertex_index]
    import bmesh
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj['lobby_equipment'] = 'reference_paddle'
    obj['source_reference'] = '//Paddle.png'
    return obj

# Trace the fully opaque source silhouette, at denser intervals on the rounded
# shoulders and neck. The long sides need just a few straight segments.
top_pixels = pixels[::-1]
rows = [71,72,74,77,81,86,92,100,110,122,136,152,170,190,214,240,270,
        300,400,500,600,650,675,695,713,730,746,761,775,789,803,817,
        831,845,859,873,887,901,914,925,935,945]
left, right = [], []
for row in rows:
    opaque = np.flatnonzero(top_pixels[row,:,3] > .975)
    lo, hi = int(opaque.min()), int(opaque.max())
    left.append((lo+1,row))
    right.append((hi-1,row))
outline = left + right[::-1]  # counterclockwise in XY
n = len(outline)
planar = [Vector(xy(x,y)) for x,y in outline]
# An inset band follows the photo's raised rim, while the dark carbon panel sits
# slightly lower. Back and real sidewalls make this an actual solid 3D paddle.
inner = []
for i,p in enumerate(planar):
    prev, nxt = planar[i-1], planar[(i+1)%n]
    a=(p-prev).normalized(); b=(nxt-p).normalized()
    na=Vector((-a.y,a.x)); nb=Vector((-b.y,b.x))
    bisector=(na+nb).normalized()
    inset=.019/max(.35, bisector.dot(na))
    inner.append(p+bisector*inset)
verts=[];uvs=[]
for ring,z in [(planar,-.026),(planar,.052),(inner,.057)]:
    for p in ring:
        verts.append((p.x,p.y,z))
        px=p.x/SCALE+CENTER_X;py=(TOP-p.y)/SCALE+71
        uvs.append(uv(px,py))
for i in range(n):
    u,v=uvs[i]
    uvs[i]=(2*(CENTER_X-x0)/(x1-x0)-u,v)
faces=[tuple(range(n-1,-1,-1))];indices=[0];smooth=set()
for i in range(n):
    j=(i+1)%n
    faces.append((i,j,n+j,n+i));indices.append(1);smooth.add(len(faces)-1)
    faces.append((n+i,n+j,2*n+j,2*n+i));indices.append(0)
faces.append(tuple(range(2*n,3*n)));indices.append(0)
head=create_mesh('Paddle_PickleVerse_CarbonHead',verts,faces,[front,edge],indices,uvs,smooth)

def grip_section(name, rings, side_material):
    # Elliptical cross-section: front UV projects the photo without generating
    # any modeled micro-detail. The rear reuses the same single texture.
    segs=24;verts=[];uvs=[]
    for y,left,right,depth,center_z in rings:
        mid=(left+right)*.5;radius=(right-left)*.5
        for j in range(segs):
            a=math.tau*j/segs
            px=mid+radius*math.sin(a)
            x,yy=xy(px,y)
            verts.append((x,yy,center_z+depth*math.cos(a)))
            uvs.append(uv(2*mid-px if math.cos(a)<0 else px,y))
    faces=[];indices=[];smooth=set()
    for r in range(len(rings)-1):
        for j in range(segs):
            faces.append((r*segs+j,r*segs+(j+1)%segs,(r+1)*segs+(j+1)%segs,(r+1)*segs+j))
            # Use photographic front/rear and clean PBR side edges.
            indices.append(0 if abs(math.cos(math.tau*(j+.5)/segs))>.24 else 1)
            smooth.add(len(faces)-1)
    faces.extend([tuple(range(segs-1,-1,-1)),tuple(range((len(rings)-1)*segs,len(rings)*segs))])
    indices.extend([1,1])
    return create_mesh(name,verts,faces,[front,side_material],indices,uvs,smooth)

collar=grip_section('Paddle_PickleVerse_BlackCollar',[
    (937,456,627,.052,.019),(944,459,625,.057,.019),
    (968,470,615,.055,.019),(985,478,608,.049,.019),
    (992,481,604,.047,.019)],black)
handle=grip_section('Paddle_PickleVerse_WhiteWrap',[
    (984,481,604,.048,.019),(1005,482,603,.051,.019),
    (1070,483,602,.053,.019),(1160,483,602,.053,.019),
    (1250,482,602,.053,.019),(1335,479,605,.053,.019),
    (1368,474,611,.055,.019)],white)
cap=grip_section('Paddle_PickleVerse_WhiteEndCap',[
    (1363,475,609,.053,.019),(1378,471,614,.057,.019),
    (1390,473,612,.055,.019),(1400,484,600,.041,.019),
    (1404,500,584,.028,.019)],white)

# Actual supplied GLB mesh, untouched topology/material. Normalize only its local
# transform to the old ball's 0.65 radius, under the existing animated parent.
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=str(BALL_SOURCE))
imported=list(set(bpy.data.objects)-before)
meshes=[o for o in imported if o.type=='MESH']
if len(meshes)!=1 or len(imported)!=1:
    raise RuntimeError('Ball source structure changed; expected its one original mesh.')
ball=meshes[0]
ball.name='Ball_Pickleball_SourceGLB'
corners=[ball.matrix_world@Vector(c) for c in ball.bound_box]
lo=Vector(tuple(min(c[k] for c in corners) for k in range(3)))
hi=Vector(tuple(max(c[k] for c in corners) for k in range(3)))
center=(lo+hi)*.5
radius=max(hi-lo)*.5
ball.parent=ball_pivot
ball.scale=(.65/radius,)*3
ball.location=Vector((0,0,.07))-center*(.65/radius)
ball['lobby_equipment']='original_glb_ball'
ball['source_model']='//../../Assets/Models/ball/pickleball_ball_cartoon.glb'
ball['source_topology_unchanged']=True

# Remove only zero-user leftovers belonging to the two assets we replaced.
for name in ['Carbon face','Match ball','Ball holes']:
    m=bpy.data.materials.get(name)
    if m is not None and m.users==0:
        bpy.data.materials.remove(m)
scene.frame_set(1)
for o in (head,collar,handle,cap,ball):
    o.hide_render=False
    o.hide_set(False)
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='MATERIAL'
        area.spaces.active.region_3d.view_perspective='CAMERA'
        area.spaces.active.overlay.show_overlays=False
        area.spaces.active.region_3d.view_camera_zoom=5
for o in bpy.context.selected_objects:o.select_set(False)
head.select_set(True)
bpy.context.view_layer.objects.active=head
scene['equipment_reference']='Paddle.png native texture and original pickleball_ball_cartoon.glb'
scene['equipment_coordinates']='XY UI plane; +Z front; 1.72-unit paddle height; existing animated pivots retained'
scene['equipment_texture']='One packed 640x1352 lossless RGBA image; native reference pixels, no upscaling'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
report={
    'source':str(SOURCE),'backup':str(backup),'bytes':SOURCE.stat().st_size,
    'removed_objects':removed,'new_paddle_objects':[o.name for o in (head,collar,handle,cap)],
    'packed_texture':{'name':texture.name,'size':list(texture.size),'packed':bool(texture.packed_file)},
    'ball':{'name':ball.name,'vertices':len(ball.data.vertices),'polygons':len(ball.data.polygons),'source':str(BALL_SOURCE)},
    'paddle_vertices':sum(len(o.data.vertices) for o in (head,collar,handle,cap)),
    'paddle_triangles':sum(len(p.vertices)-2 for o in (head,collar,handle,cap) for p in o.data.polygons),
    'actions':[a.name for a in bpy.data.actions]
}
with open(WORK/'replacement-report.json','w',encoding='utf-8') as f:
    json.dump(report,f,indent=2,ensure_ascii=False)
print('REPLACEMENT_COMPLETE',json.dumps(report,ensure_ascii=False),flush=True)
