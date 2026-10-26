"""Change only court paint; preserve every GLB binary chunk and Blender mesh.

Run in Blender 5.1. Colours are linear glTF/Principled factors. Unity's shared
materials use the corresponding sRGB colour property via the Editor API.
"""
import bpy,json,struct,hashlib
from pathlib import Path

ROOT=Path(r'D:\Github\PickleVerse');SOURCE=ROOT/'ArtSource'/'Stadiums'
WORK=ROOT/'BlenderWork'/'CourtColor20261026';WORK.mkdir(parents=True,exist_ok=True)
PALETTE=json.loads((SOURCE/'CourtPalette.json').read_text())
rows=[]
for name in ['ProfessionalStadium','PickleballCourt_LowPoly']:
    blend=SOURCE/(name+'.blend');bpy.ops.wm.open_mainfile(filepath=str(blend))
    modified=[]
    for material in bpy.data.materials:
        if material.name not in PALETTE:continue
        color=PALETTE[material.name];material.diffuse_color=color
        if material.use_nodes:
            nodes=material.node_tree.nodes;painted=False
            for node in nodes:
                if node.type=='MIX' and node.data_type=='RGBA' and node.blend_type=='MULTIPLY':node.inputs[7].default_value=color;painted=True
                if node.type=='MIX_RGB' and node.blend_type=='MULTIPLY':node.inputs[2].default_value=color;painted=True
            if not painted:
                bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
                if bs and not bs.inputs['Base Color'].is_linked:bs.inputs['Base Color'].default_value=color
        modified.append(material.name)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)

    path=ROOT/'My project'/'Assets'/'Models'/'Stadium_Enhanced'/(name+'.glb')
    raw=path.read_bytes();offset=12;chunks=[];changed=[];binary_before=[];binary_after=[]
    while offset<len(raw):
        length,kind=struct.unpack_from('<II',raw,offset);data=raw[offset+8:offset+8+length];offset+=8+length
        if kind==0x4e4f534a:
            doc=json.loads(data);before=json.loads(data)
            for mat in doc['materials']:
                if mat['name'] in PALETTE:
                    old=mat['pbrMetallicRoughness']['baseColorFactor']
                    mat['pbrMetallicRoughness']['baseColorFactor']=PALETTE[mat['name']]
                    changed.append({'material':mat['name'],'before':old,'after':PALETTE[mat['name']]})
            allowed=json.loads(json.dumps(doc))
            for i,mat in enumerate(before['materials']):
                if mat['name'] in PALETTE:allowed['materials'][i]['pbrMetallicRoughness']['baseColorFactor']=mat['pbrMetallicRoughness']['baseColorFactor']
            assert allowed==before,'Non-palette glTF metadata changed'
            data=json.dumps(doc,separators=(',',':')).encode();data+=b' '*((-len(data))%4)
        else:
            binary_before.append(hashlib.sha256(data).hexdigest());binary_after.append(hashlib.sha256(data).hexdigest())
        chunks.append(struct.pack('<II',len(data),kind)+data)
    assert binary_before==binary_after
    body=b''.join(chunks);path.write_bytes(struct.pack('<III',0x46546c67,2,len(body)+12)+body)
    rows.append({'model':name,'blendMaterials':modified,'changes':changed,'geometryAndTextureBinaryUnchanged':True,'binarySha256':binary_after,'bytesBefore':len(raw),'bytesAfter':path.stat().st_size})
(WORK/'source-palette-changes.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print('COURT_PALETTE_UPDATED',json.dumps(rows))
