from pathlib import Path
import struct,json,io,hashlib,shutil
from PIL import Image

root=Path(__file__).resolve().parents[1]
work=root.parent/'UnityWork'/'Shop';backup=work/'BeforeModels';backup.mkdir(parents=True,exist_ok=True)
reports=[]
for name in ['paddle-skin-gemstone-model.glb','paddle-skin-thot-model.glb']:
    path=root/'Assets'/'Models'/'Paddle'/name
    original=backup/name
    if not original.exists():shutil.copy2(path,original)
    raw=original.read_bytes();jlen=struct.unpack_from('<I',raw,12)[0];j=json.loads(raw[20:20+jlen]);blob=raw[28+jlen:]
    before=json.loads(json.dumps(j));image_views={x['bufferView']:i for i,x in enumerate(j['images'])};base_indices={m['pbrMetallicRoughness']['baseColorTexture']['index'] for m in j['materials']}
    base_images={j['textures'][i]['source'] for i in base_indices};rough_indices={m['pbrMetallicRoughness']['metallicRoughnessTexture']['index'] for m in j['materials']};rough_images={j['textures'][i]['source'] for i in rough_indices}
    rebuilt=bytearray();details=[];geometry_identical=True
    for index,view in enumerate(j['bufferViews']):
        content=blob[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        if index in image_views:
            number=image_views[index];im=Image.open(io.BytesIO(content));out=io.BytesIO();old_size=im.size
            if number in rough_images and max(im.size)>2048:im=im.resize((2048,2048),Image.Resampling.LANCZOS)
            if number in base_images and im.mode=='RGB':im.save(out,format='JPEG',quality=95,subsampling=0,optimize=True);j['images'][number]['mimeType']='image/jpeg'
            else:im.save(out,format='PNG',optimize=True);j['images'][number]['mimeType']='image/png'
            encoded=out.getvalue();details.append(dict(image=number,before_bytes=len(content),after_bytes=len(encoded),before_size=old_size,after_size=im.size));content=encoded
        else:
            geometry_identical=geometry_identical and hashlib.sha256(content).digest()==hashlib.sha256(blob[before['bufferViews'][index].get('byteOffset',0):before['bufferViews'][index].get('byteOffset',0)+before['bufferViews'][index]['byteLength']]).digest()
        rebuilt.extend(b'\0'*((-len(rebuilt))%4));view['byteOffset']=len(rebuilt);view['byteLength']=len(content);rebuilt.extend(content)
    rebuilt.extend(b'\0'*((-len(rebuilt))%4));j['buffers'][0]['byteLength']=len(rebuilt)
    metadata=json.dumps(j,separators=(',',':'),ensure_ascii=False).encode();metadata+=b' '*((-len(metadata))%4)
    packed=struct.pack('<III',0x46546c67,2,28+len(metadata)+len(rebuilt))+struct.pack('<II',len(metadata),0x4e4f534a)+metadata+struct.pack('<II',len(rebuilt),0x004e4942)+rebuilt
    # All non-image data and the topology/rig/UV metadata must match before overwrite.
    for field in ['meshes','accessors','nodes','skins','animations']:assert j.get(field)==before.get(field),field
    assert geometry_identical
    path.write_bytes(packed)
    reports.append(dict(file=name,before_bytes=len(raw),after_bytes=len(packed),reduction_percent=round((1-len(packed)/len(raw))*100,2),all_geometry_buffers_unchanged=geometry_identical,mesh_metadata_unchanged=True,images=details))
(work/'optimization.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
print(json.dumps(reports,indent=2))
