"""Blender 5.1, metres/Z-up. Shared opaque leaf/blade geometry with three LODs.
Replaces only the generated garden in the two approved venue sources. Existing
court/net vertices, stadium structures, entry anchors and lamps are untouched.
"""
import bpy,bmesh,math,random,json,struct,sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\Github\PickleVerse');WORK=ROOT/'BlenderWork'/'NaturalPark20261026'
ASSETS=ROOT/'My project'/'Assets'/'EnvironmentContent'/'NaturalPark'
TEX=ASSETS/'Textures';OUT=WORK/'Exports';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'ArtSource'/'Stadiums'
MATS={};REPORT={'prototypes':[],'venues':[]}

def material(name,file):
    m=bpy.data.materials.new(name);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.9
    bs.inputs['Specular IOR Level'].default_value=.17
    im=bpy.data.images.load(str(TEX/file),check_existing=True)
    node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
    m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    m.diffuse_color=(.25,.35,.13,1) if name!='Bark' else (.3,.28,.21,1)
    MATS[name]=m;return m

class Mesh:
    def __init__(self):self.verts=[];self.faces=[];self.uv=[];self.colors=[];self.mat=[]
    def vertex(self,p,uv,wind,phase,ao=1,var=.85):
        i=len(self.verts);self.verts.append(tuple(p));self.uv.append(uv)
        self.colors.append((max(0,min(1,wind)),phase%1,ao,var));return i
    def face(self,inds,mat):self.faces.append(tuple(inds));self.mat.append(mat)
    def branch(self,path,radii,sides=7,phase=.1,height=4.5,wind=.11):
        rings=[]
        for i,(p,r) in enumerate(zip(path,radii)):
            p=Vector(p);t=Vector(path[min(i+1,len(path)-1)])-Vector(path[max(i-1,0)])
            t.normalize();a=t.cross(Vector((0,1,0)))
            if a.length<.01:a=t.cross(Vector((1,0,0)))
            a.normalize();b=t.cross(a);ring=[]
            for k in range(sides):
                q=p+r*(a*math.cos(k*math.tau/sides)+b*math.sin(k*math.tau/sides))
                ring.append(self.vertex(q,(k/(sides-1),p.z/1.2),min(.2,max(0,p.z/height)**2*wind),phase,.65+.35*min(1,p.z/.6),.86))
            rings.append(ring)
        for i in range(len(rings)-1):
            for k in range(sides):
                j=(k+1)%sides;self.face((rings[i][k],rings[i][j],rings[i+1][j],rings[i+1][k]),0)
        self.face(tuple(reversed(rings[0])),0)
    def leaf(self,base,direction,length,width,roll,phase,lod=0,ao=.88,var=.9):
        d=Vector(direction).normalized();s=d.cross(Vector((0,0,1)))
        if s.length<.1:s=d.cross(Vector((0,1,0)))
        s.normalize();n=s.cross(d).normalized();s=(s*math.cos(roll)+n*math.sin(roll)).normalized();n=s.cross(d).normalized()
        # Stem, shoulders, central ridge and a naturally pointed/curling tip.
        if lod==0:
            points=[(0,0,0),(-.45,.28,.01),(0,.47,.075),(.45,.28,.01),(-.34,.70,-.01),(.34,.70,-.01),(0,1,-.13)]
            faces=[(0,1,2),(0,2,3),(1,4,2),(2,4,6),(2,6,5),(2,5,3)]
        elif lod==1:
            points=[(0,0,0),(-.46,.43,.015),(0,.48,.07),(.46,.43,.015),(0,1,-.12)]
            faces=[(0,1,2),(0,2,3),(1,4,2),(2,4,3)]
        else:
            points=[(0,0,0),(-.47,.42,0),(.47,.42,0),(0,1,-.10)];faces=[(0,1,3),(0,3,2)]
        ids=[]
        for x,y,z in points:
            p=Vector(base)+s*(x*width)+d*(y*length)+n*(z*length)
            ids.append(self.vertex(p,(.5+x,y),.55+.45*y,phase,ao,var))
        for f in faces:self.face([ids[i] for i in f],1)
    def blade(self,base,height,width,angle,lean,phase,lod):
        right=Vector((math.cos(angle),math.sin(angle),0));forward=Vector((-math.sin(angle),math.cos(angle),0))
        if lod==0:
            points=[(-.5,0,0),(.5,0,0),(-.35,.54,.28),(.35,.54,.28),(0,1,1)]
            faces=[(0,1,2),(1,3,2),(2,3,4)]
        elif lod==1:
            points=[(-.5,0,0),(.5,0,0),(0,.62,.40),(0,1,1)];faces=[(0,1,2),(0,2,3)]
        else:points=[(-.5,0,0),(.5,0,0),(0,1,1)];faces=[(0,1,2)]
        ids=[]
        for x,z,bend in points:
            p=Vector(base)+right*x*width+Vector((0,0,height*z))+forward*(lean*bend)
            ids.append(self.vertex(p,(x+.5,z),z,phase,.46+.54*z,.76+.19*phase))
        for f in faces:self.face([ids[i] for i in f],2)
    def object(self,name):
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.verts,[],self.faces);mesh.update()
        for m in MATS.values():mesh.materials.append(m)
        uv=mesh.uv_layers.new(name='UVMap');color=mesh.color_attributes.new(name='WindAO',type='FLOAT_COLOR',domain='CORNER')
        for p,slot in zip(mesh.polygons,self.mat):
            p.material_index=slot;p.use_smooth=True
            for i in p.loop_indices:
                index=mesh.loops[i].vertex_index;uv.data[i].uv=self.uv[index];color.data[i].color=self.colors[index]
        mesh.color_attributes.active_color=color;mesh.color_attributes.render_color_index=0
        mesh.validate();o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o)
        mesh.calc_loop_triangles();REPORT['prototypes'].append({'name':name,'triangles':len(mesh.loop_triangles),'vertices':len(mesh.vertices)})
        return o

def tree(species,lod):
    mesh=Mesh();r=random.Random(947+species*103);h=4.6;count=7 if species==0 else 6
    trunk=[Vector((math.sin(i*.7)*.09,math.cos(i*.8)*.06,i*h/6)) for i in range(7)]
    mesh.branch(trunk,[.18,.145,.115,.086,.06,.035,.008],7 if lod==0 else 5,height=h)
    for i in range(count):
        angle=i*2.40+species*.63+r.uniform(-.18,.18);radial=Vector((math.cos(angle),math.sin(angle),0));side=Vector((-radial.y,radial.x,0))
        start=Vector((0,0,h*(.28+i*.073)));reach=r.uniform(1.12,1.65)*(1 if species==0 else .86)
        end=start+radial*reach+Vector((0,0,r.uniform(1.05,1.55)))
        mesh.branch([start,start+radial*reach*.48+Vector((0,0,.42)),end],[.055,.027,.006],6 if lod==0 else 4,phase=i*.153,height=h)
        second_count=5 if lod<2 else 3
        for j in range(second_count):
            t=.35+j*.14 if lod<2 else .42+j*.24
            root=start.lerp(end,t);direction=side*((-1)**j)*r.uniform(.38,.75)+radial*.25+Vector((0,0,r.uniform(.25,.5)))
            tip=root+direction
            mesh.branch([root,root.lerp(tip,.55)+Vector((0,0,.06)),tip],[.020,.010,.0025],5 if lod==0 else 3,phase=(i+j)*.173,height=h)
            twig_count=4 if lod<2 else 2
            for k in range(twig_count):
                twig_root=root.lerp(tip,.30+k*.21 if lod<2 else .4+k*.5)
                twig_dir=(radial*(1 if k%2 else -.2)+side*((-1)**k)*.42+Vector((0,0,.35))).normalized()
                twig_tip=twig_root+twig_dir*r.uniform(.35,.56)
                if lod<2:mesh.branch([twig_root,twig_tip],[.006,.0015],3,phase=(k+j)*.17,height=h,wind=.18)
                pairs=4 if lod==0 else 3 if lod==1 else 2
                for q in range(pairs):
                    node=twig_root.lerp(twig_tip,.18+q*.22 if lod<2 else .35+q*.6)
                    for sign in [-1,1]:
                        if lod==1 and (i+j+k+q)%3==0 and sign<0:continue
                        leaf_dir=(side*sign*.65+twig_dir*.45+Vector((0,0,r.uniform(-.25,.25)))).normalized()
                        mesh.leaf(node,leaf_dir,r.uniform(.17,.25),r.uniform(.10,.14),r.uniform(-.55,.55),r.random(),lod,ao=.73+.24*r.random(),var=.72+.26*r.random())
    # Low buttress roots provide an attached, tapering trunk rather than a pole.
    if lod<2:
        for i in range(5):
            a=i*math.tau/5;v=Vector((math.cos(a),math.sin(a),0))
            mesh.branch([Vector((0,0,.22)),v*.25+Vector((0,0,.07)),v*.48],[.075,.045,.008],5,phase=.1)
    return mesh.object(('Broadleaf_A' if species==0 else 'Broadleaf_B')+'_LOD'+str(lod))

def grass(lod):
    mesh=Mesh();r=random.Random(32891);count=[230,40,12][lod]
    for i in range(count):
        x=r.uniform(-1.85,1.85);y=r.uniform(-1.85,1.85);phase=r.random()
        for j in range([8,6,5][lod]):
            a=r.random()*math.tau;offset=Vector((math.cos(a),math.sin(a),0))*r.uniform(.005,.045)
            mesh.blade(Vector((x,y,0))+offset,r.uniform(.065,.17),r.uniform(.007,.016),a,r.uniform(.025,.085),phase,lod)
    return mesh.object('GrassPatch_LOD'+str(lod))

def shrub(lod):
    mesh=Mesh();r=random.Random(72815)
    for i in range([19,11,6][lod]):
        a=r.random()*math.tau;root=Vector((math.cos(a),math.sin(a),0))*r.uniform(0,.25);tip=root+Vector((math.cos(a)*.25,math.sin(a)*.25,r.uniform(.3,.6)))
        mesh.branch([root,tip],[.008,.001],4 if lod==0 else 3,phase=r.random(),height=1)
        for j in range([9,5,3][lod]):
            base=root.lerp(tip,.20+j/(12 if lod==0 else 7 if lod==1 else 4));direction=Vector((math.cos(a+j*2.3),math.sin(a+j*2.3),r.uniform(-.1,.5)))
            mesh.leaf(base,direction,r.uniform(.10,.17),.08,r.uniform(-.7,.7),r.random(),lod,ao=.7+.25*r.random())
    return mesh.object('Shrub_LOD'+str(lod))

def export(path,objects,colors=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True);parent=o.parent
        while parent:parent.select_set(True);parent=parent.parent
    options=dict(filepath=str(path),export_format='GLB',use_selection=True,export_animations=False,export_cameras=False,export_lights=False,export_tangents=False,export_yup=True)
    props=bpy.ops.export_scene.gltf.get_rna_type().properties
    if colors:
        if 'export_vertex_color' in props:options['export_vertex_color']='ACTIVE'
        if 'export_all_vertex_colors' in props:options['export_all_vertex_colors']=True
    bpy.ops.export_scene.gltf(**options)
    if colors:
        # Wind masks need only 8-bit precision. Strip duplicate embedded maps:
        # Unity uses the separately compressed, shared botanical textures.
        raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary=raw[28+n:]
        color_accessors={p['attributes']['COLOR_0'] for m in doc['meshes'] for p in m['primitives']}
        replaced={}
        for index in color_accessors:
            a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
            if a['componentType']==5123:
                values=struct.unpack_from('<'+'H'*(a['count']*4),binary,view.get('byteOffset',0)+a.get('byteOffset',0))
                replaced[a['bufferView']]=bytes(round(v/257) for v in values);a['componentType']=5121;a['byteOffset']=0
        for key in ['images','textures','samplers']:doc.pop(key,None)
        for m in doc['materials']:
            m.get('pbrMetallicRoughness',{}).pop('baseColorTexture',None)
            m.get('pbrMetallicRoughness',{})['baseColorFactor']=[1,1,1,1]
            m.pop('extensions',None)
        doc.pop('extensionsUsed',None);doc.pop('extensionsRequired',None)
        used=sorted({a['bufferView'] for a in doc['accessors']});views=[];mapping={};packed=bytearray()
        for old in used:
            view=dict(doc['bufferViews'][old]);offset=view.get('byteOffset',0)
            data=replaced.get(old,binary[offset:offset+view['byteLength']]);packed.extend(b'\0'*((-len(packed))%4))
            view['byteOffset']=len(packed);view['byteLength']=len(data);mapping[old]=len(views);views.append(view);packed.extend(data)
        for a in doc['accessors']:a['bufferView']=mapping[a['bufferView']]
        doc['bufferViews']=views;doc['buffers'][0]['byteLength']=len(packed)
        encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4);packed.extend(b'\0'*((-len(packed))%4))
        body=struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(packed),0x004e4942)+packed
        path.write_bytes(struct.pack('<III',0x46546c67,2,12+len(body))+body)

def wind_preview(objects):
    for o in objects:
        if not o.data.color_attributes:continue
        weights={}
        attr=o.data.color_attributes.active_color
        for loop in o.data.loops:weights[loop.vertex_index]=attr.data[loop.index].color[0]
        o.shape_key_add(name='Basis');key=o.shape_key_add(name='Wind_Preview');key.slider_min=-1
        for i,p in enumerate(key.data):
            w=weights.get(i,0);phase=math.sin(p.co.x*.8+p.co.y*.65+p.co.z*1.1)
            p.co.x+=.075*w*phase;p.co.y+=.035*w*math.cos(p.co.y*1.4+p.co.z)
        for frame,value in [(1,0),(45,1),(90,0),(135,-1),(180,0)]:key.value=value;key.keyframe_insert('value',frame=frame)
        if o.data.shape_keys.animation_data and o.data.shape_keys.animation_data.action:
            action=o.data.shape_keys.animation_data.action
            if hasattr(action,'fcurves'):
                curves=list(action.fcurves)
            else:
                slot=o.data.shape_keys.animation_data.action_slot;curves=[]
                for layer in action.layers:
                    for strip in layer.strips:
                        if hasattr(strip,'channelbag'):
                            bag=strip.channelbag(slot)
                            if bag:curves.extend(bag.fcurves)
            for curve in curves:curve.modifiers.new('CYCLES')

def pack_save(path):
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    for im in bpy.data.images:
        if im.source=='FILE' and not im.packed_file:im.pack()
    bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
material('Bark','Bark_Fibres.png');material('Leaf','Leaf_Venation.png');material('Grass','Grass_Fibres.png')
PROTOTYPES=[]
for species in [0,1]:
    for lod in range(3):PROTOTYPES.append(tree(species,lod))
for lod in range(3):PROTOTYPES.append(grass(lod));PROTOTYPES.append(shrub(lod))
export(ASSETS/'NaturalVegetation.glb',PROTOTYPES,True)
wind_preview(PROTOTYPES)
for i,o in enumerate(PROTOTYPES):o.location=(i//3*6,(i%3)*7,0)
bpy.context.scene.frame_end=180;bpy.context.scene.frame_set(35)
pack_save(SOURCE/'NaturalVegetation.blend')

for key,name in [('Professional','ProfessionalStadium'),('LowPoly','PickleballCourt_LowPoly')]:
    previous=WORK/'Previous'/'ArtSource'/'Stadiums'/(name+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(previous if previous.exists() else SOURCE/(name+'.blend')))
    # These objects were created in the preceding garden pass. Stadium/court
    # geometry has different names and is retained byte-for-byte in Blender.
    remove=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith(('Refined_Garden_Bark','Refined_Garden_Leaf_','Refined_Garden_Soil','Broadleaf_','GrassPatch_','Shrub_'))]
    for o in remove:bpy.data.objects.remove(o,do_unlink=True)
    for o in bpy.context.scene.objects:
        if o.type=='MESH' and o.name=='Refined_Garden_Grass':
            m=o.data.materials[0];nodes=m.node_tree.nodes
            for node in nodes:
                if node.type=='TEX_IMAGE':node.image=bpy.data.images.load(str(TEX/'Ground_Organic.png'),check_existing=True)
                if node.type=='MIX_RGB':node.inputs[2].default_value=(1,1,1,1)
                if node.type=='MIX' and node.data_type=='RGBA':node.inputs[7].default_value=(1,1,1,1)
            m.diffuse_color=(1,1,1,1)
    palette=json.loads((SOURCE/'CourtPalette.json').read_text())
    for mat in bpy.data.materials:
        if mat.name not in palette:continue
        mat.diffuse_color=palette[mat.name]
        if mat.use_nodes:
            for node in mat.node_tree.nodes:
                if node.type=='MIX' and node.data_type=='RGBA' and node.blend_type=='MULTIPLY':node.inputs[7].default_value=palette[mat.name]
                if node.type=='MIX_RGB' and node.blend_type=='MULTIPLY':node.inputs[2].default_value=palette[mat.name]
    core=[o for o in bpy.context.scene.objects if o.type=='MESH']
    export(OUT/(name+'.glb'),core)
    # Preserve the color factors explicitly; Blender material previews are not
    # a substitute for verifying the actual glTF baseColorFactor after export.
    raw=(OUT/(name+'.glb')).read_bytes();size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);binary=raw[28+size:]
    for mat in doc['materials']:
        bm=bpy.data.materials.get(mat['name'])
        if bm and mat['name'] not in ['Net_Weave','ChainLink']:
            factor=list(bm.diffuse_color);mat.setdefault('pbrMetallicRoughness',{})['baseColorFactor']=factor
    data=json.dumps(doc,separators=(',',':')).encode();data+=b' '*((-len(data))%4)
    body=struct.pack('<II',len(data),0x4e4f534a)+data+struct.pack('<II',len(binary),0x004e4942)+binary
    (OUT/(name+'.glb')).write_bytes(struct.pack('<III',0x46546c67,2,12+len(body))+body)

    pro=key=='Professional';ground=-.478 if pro else -.158
    layout=[(-16,-13,4.3),(-19,-5,4.8),(-18,5.5,4.1),(-16,15,4.5),(16,-13.5,4.1),(19,-4.5,4.6),(18,5,4.3),(16,15.5,4.7),(-7,22,3.8),(6.3,22,4.2)] if pro else [(-12,-10,4),(-15,-2,4.7),(-14,6,4.1),(-12,16,4.2),(12,-11,4.3),(15,-3,4.6),(14,6.8,4),(12,17,4.4)]
    patches=set()
    if pro:
        for x in [-20,-16,16,20]:
            for y in range(-22,23,4):patches.add((x,y))
        for y in [-26,-22,22,26]:
            for x in [-10,-6,-2,2,6,10]:patches.add((x,y))
    else:
        for x in [-16,-12,12,16]:
            for y in range(-18,19,4):patches.add((x,y))
        for y in [-22,-18,18,22]:
            for x in [-6,-2,2,6]:patches.add((x,y))
    record={'key':key,'ground':ground,'trees':[{'species':i%2,'position':[x,ground,-y],'scale':h/4.6,'angle':(i*137)%360} for i,(x,y,h) in enumerate(layout)],
        'grass':[{'position':[x,ground+.003,-y],'angle':((i*71)%4)*90} for i,(x,y) in enumerate(sorted(patches))],
        'shrubs':[{'position':[x,ground,-y],'angle':i*59} for i,(x,y) in enumerate([(-16,-18),(16,-18),(-18,14),(18,14)])]}
    REPORT['venues'].append(record)
    # Build the editable complete venue from linked prototype meshes. Only
    # LOD0 is displayed in this authoring scene; Unity chooses LOD at runtime.
    with bpy.data.libraries.load(str(SOURCE/'NaturalVegetation.blend'),link=False) as (available,loaded):
        loaded.objects=[n for n in available.objects if n.endswith('_LOD0')]
    proto={o.name:o for o in loaded.objects if o}
    for o in proto.values():o.location=(0,0,0)
    for row in record['trees']:
        src=proto['Broadleaf_A_LOD0' if row['species']==0 else 'Broadleaf_B_LOD0'];o=src.copy();o.data=src.data
        bpy.context.collection.objects.link(o);o.location=(row['position'][0],-row['position'][2],ground+(.45 if pro else 0));o.scale=(row['scale'],)*3;o.rotation_euler.z=math.radians(row['angle'])
    for category,source in [('grass','GrassPatch_LOD0'),('shrubs','Shrub_LOD0')]:
        for row in record[category]:
            o=proto[source].copy();o.data=proto[source].data;bpy.context.collection.objects.link(o)
            o.location=(row['position'][0],-row['position'][2],ground+(.45 if pro else 0)+(.003 if category=='grass' else 0));o.rotation_euler.z=math.radians(row['angle'])
    bpy.context.scene.frame_end=180;bpy.context.scene.frame_set(35)
    pack_save(SOURCE/(name+'.blend'))

(WORK/'natural-park-manifest.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
(SOURCE/'NaturalParkLayout.json').write_text(json.dumps(REPORT,indent=2),encoding='utf-8')
print('NATURAL_PARK_COMPLETE',json.dumps(REPORT['prototypes']),flush=True)
