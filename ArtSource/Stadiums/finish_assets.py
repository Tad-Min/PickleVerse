"""Preserve explicit glTF baseColorFactor and compact the editable authoring files."""
import bpy,json,struct,bmesh
from pathlib import Path

ROOT=Path(r'D:\Github\PickleVerse');WORK=ROOT/'BlenderWork'/'VenueRefinement20261025'
SOURCE=ROOT/'ArtSource'/'Stadiums';SOURCE.mkdir(parents=True,exist_ok=True)

def factors(path,colors):
    raw=path.read_bytes();offset=12;chunks=[]
    while offset<len(raw):
        size,kind=struct.unpack_from('<II',raw,offset);data=raw[offset+8:offset+8+size];offset+=8+size
        if kind==0x4e4f534a:
            obj=json.loads(data)
            for m in obj['materials']:
                if m['name'] in colors:m.setdefault('pbrMetallicRoughness',{})['baseColorFactor']=colors[m['name']]
            data=json.dumps(obj,ensure_ascii=True,separators=(',',':')).encode('utf-8');data+=b' '*((-len(data))%4)
        chunks.append(struct.pack('<II',len(data),kind)+data)
    body=b''.join(chunks);path.write_bytes(struct.pack('<III',0x46546c67,2,len(body)+12)+body)

for key,name in [('Professional','ProfessionalStadium'),('LowPoly','PickleballCourt_LowPoly')]:
    bpy.ops.wm.open_mainfile(filepath=str(WORK/(name+'_Refined.blend')))
    used={m for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m}
    report=json.loads((WORK/(key+'-manifest.json')).read_text(encoding='utf-8'))
    # Only replace the materials authored in this pass. Keep the imported alpha net.
    colors={m.name:list(m.diffuse_color) for m in used if m.name in report['textures']}
    # Use the current Blender exporter node arrangement, including when the
    # compact source is exported manually in a later editing session.
    for m in used:
        if not m.use_nodes:continue
        for old in list(m.node_tree.nodes):
            if old.type!='MIX_RGB':continue
            links=list(old.outputs[0].links);input_links=list(old.inputs[1].links)
            new=m.node_tree.nodes.new('ShaderNodeMix');new.data_type='RGBA';new.blend_type='MULTIPLY';new.inputs[0].default_value=1
            new.inputs[7].default_value=old.inputs[2].default_value
            for link in input_links:m.node_tree.links.new(link.from_socket,new.inputs[6])
            for link in links:m.node_tree.links.new(new.outputs[2],link.to_socket)
            m.node_tree.nodes.remove(old)
    # Mount the principal banner at the far end visible to Unity's lobby camera;
    # Unity +Z corresponds to Blender -Y. Keep the entrance opening clear.
    signage=bpy.data.objects['Refined_Venue_Signage']
    if key=='Professional':
        for v in signage.data.vertices:
            if abs(v.co.x)<2.81 and abs(v.co.y-10.59)<.015:v.co.x=-v.co.x;v.co.y=-10.79
    else:
        if sum(v.co.y for v in signage.data.vertices)>0:
            for v in signage.data.vertices:v.co.x=-v.co.x;v.co.y=-v.co.y
        metal=bpy.data.objects['Refined_Venue_Metal']
        for v in metal.data.vertices:
            if v.co.y>9.2:v.co.x=-v.co.x;v.co.y=-v.co.y
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type=='MESH':
            o.select_set(True)
            parent=o.parent
            while parent:
                parent.select_set(True);parent=parent.parent
    bpy.ops.export_scene.gltf(filepath=str(WORK/'Exports'/(name+'.glb')),export_format='GLB',use_selection=True,
        export_animations=False,export_cameras=False,export_lights=False,export_yup=True,
        export_tangents=False,export_image_format='AUTO')
    factors(WORK/'Exports'/(name+'.glb'),colors)
    report['colors']=colors;report['bytes']=(WORK/'Exports'/(name+'.glb')).stat().st_size
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for o in meshes:o.data.calc_loop_triangles()
    report['triangles']=sum(len(o.data.loop_triangles) for o in meshes)
    report['objects']=[{'name':o.name,'triangles':len(o.data.loop_triangles),'materials':[m.name for m in o.data.materials]} for o in meshes]
    (WORK/(key+'-manifest.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for im in bpy.data.images:
        if im.source=='FILE' and not im.packed_file:im.pack()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
    print('FINISHED_ASSET',key,report['bytes'],(SOURCE/(name+'.blend')).stat().st_size,flush=True)
