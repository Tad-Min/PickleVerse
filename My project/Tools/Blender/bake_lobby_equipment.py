"""Bake just the two lobby equipment tiles from the authored blend.

Other atlas pixels, source geometry, animation and sprite rectangles are preserved.
Run with Blender --background --factory-startup --python this_script.py.
"""
import bpy, json, math, hashlib, os, struct, zlib
import numpy as np
from pathlib import Path

HERE=Path(__file__).resolve().parent
PROJECT=HERE.parent.parent
WORK=PROJECT.parent/'UnityWork/LobbyPlayEquipment20261016'
SOURCE=HERE/'Lobby_UI_Source.blend'
WORK.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene=bpy.context.scene
paddle=bpy.data.objects.get('Play_Strike_Pivot')
ball=bpy.data.objects.get('Play_Ball_Flight')
if not paddle or not ball:
    raise RuntimeError('Authored paddle/ball pivots missing')
if not bpy.data.objects.get('Paddle_PickleVerse_CarbonHead') or not bpy.data.objects.get('Ball_Pickleball_SourceGLB'):
    raise RuntimeError('Expected new reference paddle and original GLB ball, not legacy UI models')
for o in scene.objects:
    if o.type not in {'CAMERA','LIGHT'}:
        o.hide_render=True
for root in (paddle,ball):
    root.animation_data_clear()
    root.location=(0,0,0)
    root.rotation_euler=(0,0,0)
    root.scale=(1,1,1)
scene.camera.location=(0,0,6)
scene.camera.rotation_euler=(0,0,0)
scene.camera.data.type='ORTHO'
scene.camera.data.ortho_scale=2.2
scene.render.resolution_x=256
scene.render.resolution_y=256
scene.render.resolution_percentage=100
scene.render.engine='CYCLES'
scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.render.film_transparent=True
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'
report={}
for name,root in [('Paddle',paddle),('Ball',ball)]:
    if name=='Paddle':root.rotation_euler.z=math.radians(22)
    for o in [root]+list(root.children_recursive):o.hide_render=False
    scene.render.filepath=str(WORK/('tile-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
    report[name]=dict(source_objects=[o.name for o in root.children_recursive],tile=scene.render.filepath)
    for o in [root]+list(root.children_recursive):o.hide_render=True
report['source_sha256']=hashlib.sha256(SOURCE.read_bytes()).hexdigest()
report['note']='Only temporary render state changed. Blender source file was not saved.'
atlas_path=PROJECT/'Assets/FrontEndContent/LobbyRefresh/Lobby_UI_Atlas.png'
atlas=bpy.data.images.load(str(atlas_path),check_existing=False)
if tuple(atlas.size)!=(1024,1024):raise RuntimeError('Unexpected atlas dimensions')
pixels=np.asarray(atlas.pixels[:],dtype=np.float32).reshape(1024,1024,4)
original=pixels.copy()
manifest=json.loads((HERE/'lobby-ui-manifest.json').read_text(encoding='utf8'))
mask=np.zeros((1024,1024),dtype=bool)
for name in ('Paddle','Ball'):
    rect=next(r for r in manifest['sprites'] if r['name']==name)
    x,y,size=rect['x'],rect['y'],rect['size']
    if size!=256:raise RuntimeError('Unexpected equipment sprite rectangle')
    tile=bpy.data.images.load(str(WORK/('tile-'+name+'.png')),check_existing=False)
    pixels[y:y+size,x:x+size]=np.asarray(tile.pixels[:],dtype=np.float32).reshape(size,size,4)
    mask[y:y+size,x:x+size]=True
assert np.array_equal(pixels[~mask],original[~mask])
atlas.pixels.foreach_set(pixels.reshape(-1))
atlas.filepath_raw=str(WORK/'Lobby_UI_Atlas.baked.png')
atlas.file_format='PNG'
atlas.save()
# Lossless PNG stream compression; write atomically so Unity never sees half a file.
png=Path(atlas.filepath_raw).read_bytes();offset=8;chunks=[];idat=[]
while offset<len(png):
    length,kind=struct.unpack_from('>I4s',png,offset)
    data=png[offset+8:offset+8+length]
    (idat if kind==b'IDAT' else chunks).append(data if kind==b'IDAT' else (kind,data))
    offset+=length+12
def chunk(kind,data):
    return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
optimized=png[:8]+b''.join(chunk(k,d) for k,d in chunks if k!=b'IEND')+chunk(b'IDAT',zlib.compress(zlib.decompress(b''.join(idat)),9))+chunk(b'IEND',b'')
temp=WORK/'Lobby_UI_Atlas.ready.png'
temp.write_bytes(optimized)
os.replace(temp,atlas_path)
report['atlas_bytes']=atlas_path.stat().st_size
report['unchanged_tiles']=14
report['atlas_size']=[1024,1024]
with open(WORK/'bake-report.json','w',encoding='utf8') as f:json.dump(report,f,indent=2)
print('EQUIPMENT_BAKE_COMPLETE',flush=True)
