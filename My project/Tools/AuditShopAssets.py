from pathlib import Path
import struct,json,io
from PIL import Image

root=Path(__file__).resolve().parents[1]
for name in ['paddle-skin-gemstone-model.glb','paddle-skin-thot-model.glb']:
    path=root/'Assets'/'Models'/'Paddle'/name
    raw=path.read_bytes();jlen=struct.unpack_from('<I',raw,12)[0];j=json.loads(raw[20:20+jlen]);offset=20+jlen;blob=raw[offset+8:]
    textures=[]
    for i,item in enumerate(j.get('images',[])):
        v=j['bufferViews'][item['bufferView']];content=blob[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];im=Image.open(io.BytesIO(content))
        textures.append(dict(index=i,name=item.get('name'),bytes=len(content),size=im.size,format=im.format,mode=im.mode))
    print(json.dumps(dict(name=name,bytes=len(raw),meshes=[dict(name=m.get('name'),primitives=[dict(vertices=j['accessors'][p['attributes']['POSITION']]['count'],triangles=j['accessors'][p['indices']]['count']//3 if 'indices' in p else None,material=p.get('material')) for p in m['primitives']]) for m in j.get('meshes',[])],materials=j.get('materials'),images=textures,nodes=[dict(name=n.get('name'),mesh=n.get('mesh'),scale=n.get('scale'),translation=n.get('translation')) for n in j.get('nodes',[])]),indent=2))
for name in ['Tokenpad.png','Elite Token.png']:
    im=Image.open(root/'Assets'/'Models'/'coin'/name)
    print(name,im.size,im.mode,im.getextrema())
