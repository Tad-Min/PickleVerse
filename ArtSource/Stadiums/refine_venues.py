"""Blender 5.1: preserve regulation geometry, refine surfaces, add a sparse garden.
Authoring coordinates: Blender Z up, metres; glTF export Y up. Unity applies the
existing Professional -0.45m offset. Only decorative meshes are simplified.
"""
import bpy, math, random, json, bmesh, sys
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\Github\PickleVerse');WORK=ROOT/'BlenderWork'/'VenueRefinement20261025'
TEX=ROOT/'My project'/'Assets'/'LocalMatchContent'/'Stadiums'/'RefinedTextures'
OUT=WORK/'Exports';OUT.mkdir(exist_ok=True)
random.seed(4719)
images={};materials={};generated=[]

def image(name):
    if name not in images:images[name]=bpy.data.images.load(str(TEX/name),check_existing=True)
    return images[name]

def mat(name,color,rough=.8,texture=None):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled')
    m.node_tree.links.new(bs.outputs['BSDF'],out.inputs[0]);bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=0
    bs.inputs['Specular IOR Level'].default_value=.25
    if texture:
        t=n.new('ShaderNodeTexImage');t.image=image(texture)
        # glTF stores texture multiplied by baseColorFactor; export recognises this.
        mix=n.new('ShaderNodeMix');mix.data_type='RGBA';mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
        mix.inputs[7].default_value=(*color,1);m.node_tree.links.new(t.outputs['Color'],mix.inputs[6]);m.node_tree.links.new(mix.outputs[2],bs.inputs['Base Color'])
    m.diffuse_color=(*color,1);materials[name]=m
    return m

def use(o,m):
    o.data.materials.clear();o.data.materials.append(m);generated.append(o);return o

def cube(name,loc,size,m,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for p in o.data.polygons:p.use_smooth=False
    return use(o,m)

def cylinder(name,a,b,r,m,vertices=8):
    v=Vector(b)-Vector(a);mid=(Vector(a)+Vector(b))*.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=v.length,location=mid)
    o=bpy.context.object;o.name=name;o.rotation_euler=v.to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for p in o.data.polygons:p.use_smooth=True
    return use(o,m)

def project_uv(o,scale=.7):
    if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
    uv=o.data.uv_layers.active.data
    for p in o.data.polygons:
        axis=max(range(3),key=lambda k:abs(p.normal[k]))
        axes=[(1,2),(0,2),(0,1)][axis]
        for i in p.loop_indices:
            v=o.matrix_world@o.data.vertices[o.data.loops[i].vertex_index].co
            uv[i].uv=(v[axes[0]]*scale,v[axes[1]]*scale)

def canopy(loc,scale,m,seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=loc)
    o=bpy.context.object;o.name='Garden_Canopy';local=random.Random(seed)
    for v in o.data.vertices:
        r=local.uniform(.87,1.12);v.co.x*=scale[0]*r;v.co.y*=scale[1]*r;v.co.z*=scale[2]*r
    for p in o.data.polygons:p.use_smooth=True
    use(o,m);return o

def tree(x,y,z,h,seed):
    local=random.Random(seed)
    cylinder('Garden_Trunk',(x,y,z),(x+.10,y-.08,z+h*.72),.105 if h<4 else .135,materials['Garden_Bark'],7)
    for i,(dx,dy) in enumerate([(-.65,.25),(.55,.35),(.10,-.60)]):
        cylinder('Garden_Branches',(x,y,z+h*.46),(x+dx,y+dy,z+h*.80),.047,materials['Garden_Bark'],6)
        canopy((x+dx,y+dy,z+h*.78),(h*.25,h*.23,h*.28),materials['Garden_Leaf_A' if i%2==0 else 'Garden_Leaf_B'],seed+i)
    canopy((x+.05,y,z+h*.91),(h*.22,h*.22,h*.25),materials['Garden_Leaf_A'],seed+12)
    # Flat, opaque low mound = contact shadow without another shadow light.
    canopy((x,y,z-.11),(.6,.6,.15),materials['Garden_Soil'],seed+33)

def sign(name,center,width,height,row,orientation):
    # Text is rasterised in an atlas so Vietnamese letters cost only two triangles.
    c=Vector(center);right=Vector(orientation);up=Vector((0,0,1))
    vertices=[c-right*width*.5-up*height*.5,c+right*width*.5-up*height*.5,c+right*width*.5+up*height*.5,c-right*width*.5+up*height*.5]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],[(0,1,2,3)]);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);use(o,materials['Venue_Signage'])
    uv=mesh.uv_layers.new(name='UVMap');v0=1-(row+1)*.25+.008;v1=1-row*.25-.008
    for l,coord in zip(uv.data,[(.006,v0),(.994,v0),(.994,v1),(.006,v1)]):l.uv=coord
    return o

def garden(pro,z):
    outer= (38,44);inner=(10.6,15.2) if pro else (7.1,12)
    # A ring of solid grass with a paved strip between it and the venue.
    mesh=bpy.data.meshes.new('Garden_Lawn_Mesh');verts=[];faces=[]
    for x0,x1,y0,y1 in [(-outer[0],-inner[0],-outer[1],outer[1]),(inner[0],outer[0],-outer[1],outer[1]),(-inner[0],inner[0],inner[1],outer[1]),(-inner[0],inner[0],-outer[1],-inner[1])]:
        n=len(verts);verts.extend([(x0,y0,z),(x1,y0,z),(x1,y1,z),(x0,y1,z)]);faces.append((n,n+1,n+2,n+3))
    mesh.from_pydata(verts,[],faces);mesh.update();o=bpy.data.objects.new('Garden_Lawn',mesh);bpy.context.collection.objects.link(o);use(o,materials['Garden_Grass']);project_uv(o,1.25)
    layout=[(-13.8,-12.8,4.3),(-16,-5,4.8),(-15,5.5,4.1),(-13.4,14.5,4.5),(13.5,-13.5,4.1),(15.5,-4.5,4.6),(15,5,4.3),(13.6,14.8,4.7),(-7,19,3.8),(6.3,19.8,4.2)] if pro else [(-10,-10,4),(-12,-2,4.7),(-11,6.2,4.1),(-8.8,13.8,4.2),(9.5,-11,4.3),(11.5,-3,4.6),(10,6.8,4),(8.8,14,4.4)]
    for i,(x,y,h) in enumerate(layout):tree(x,y,z,h,125+i)
    for x,y in [(-11,16),(11,16),(-16,11),(16,11),(-13,-17),(13,-17)]:
        for i in range(3):canopy((x+(i-1)*.52,y,z+.32),(.7,.6,.48),materials['Garden_Leaf_B'],731+i)

def props(pro,z):
    # Keep every decorative prop outside the playable run-off and entrance paths.
    x=5.35 if pro else 6.2
    for side in [-1,1]:
        cx=side*x;cy=2.7 if pro else 3.5
        for leg in [-1,1]:cube('Timber_Bench_Leg',(cx+side*.03,cy+leg*.68,z+.25),(.35,.10,.48),materials['Venue_Metal'],.018)
        for plank in [-1,0,1]:cube('Timber_Bench_Slat',(cx+plank*.13,cy,z+.51),(.115,1.8,.08),materials['Venue_Timber'],.012)
        cube('Timber_Bench_Back',(cx+side*.22,cy,z+.85),(.08,1.8,.45),materials['Venue_Timber'],.018)
        # Insulated drinks cooler + towel, readable during entry shots.
        cube('Players_Cooler',(cx,cy+1.55,z+.28),(.45,.52,.52),materials['Venue_Polymer'],.035)
        cube('Players_Cooler_Lid',(cx,cy+1.55,z+.57),(.49,.56,.07),materials['Venue_OffWhite'],.018)
        cube('Players_Towel',(cx,cy-.25,z+.56),(.32,.43,.025),materials['Venue_OffWhite'],.006)
        cylinder('Drinking_Bottle',(cx,cy+.33,z+.56),(cx,cy+.33,z+.82),.055,materials['Venue_Polymer'],8)
        cylinder('Drinking_Bottle_Cap',(cx,cy+.33,z+.82),(cx,cy+.33,z+.85),.035,materials['Venue_OffWhite'],8)

def simplify(o):
    # Weld split normals first; only collapse bevel-heavy non-gameplay guardrails.
    before=len(o.data.polygons)
    if o.name!='Guardrails_And_Balusters':return
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00003);bm.to_mesh(o.data);bm.free()
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Background rail budget','DECIMATE');mod.ratio=.43;mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    o.data.validate(clean_customdata=False)
    print('GUARDRAILS_BUDGET',before,len(o.data.polygons),flush=True)

def render(key,z):
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    scene.world=bpy.data.worlds.new('Soft daylight');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.60,.72,.84,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
    bpy.ops.object.light_add(type='SUN',rotation=(math.radians(25),math.radians(-24),math.radians(-25)))
    bpy.context.object.name='Preview_Sun';bpy.context.object.data.energy=1.6;bpy.context.object.data.angle=math.radians(7)
    bpy.ops.object.camera_add(location=(19,-26,18) if key=='Professional' else (17,-23,16))
    cam=bpy.context.object;cam.name='Preview_Cinematic';cam.data.lens=39;cam.rotation_euler=(Vector((0,1,z+.5))-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    scene.render.filepath=str(WORK/(key+'-refined.png'));bpy.ops.render.render(write_still=True)
    cam.location=(0,-12,z+3.3);cam.data.lens=24;cam.rotation_euler=(Vector((0,6,z+1.7))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(WORK/(key+'-courtside.png'));bpy.ops.render.render(write_still=True)

def build(key,filename):
    global images,materials,generated
    bpy.ops.wm.read_factory_settings(use_empty=True);images={};materials={};generated=[]
    bpy.ops.import_scene.gltf(filepath=str(WORK/'Original'/(filename+'.glb')))
    pro=key=='Professional';z=.45 if pro else 0
    original=[o for o in bpy.context.scene.objects if o.type=='MESH']
    protected={o.name:[list(v.co) for v in o.data.vertices] for o in original if o.name.startswith(('Court_','PortableNet_'))}
    # Muted tournament palette, high roughness, no baked glare or emissive paint.
    colors={'Court_Blue':(.105,.285,.355),'Court_Green':(.185,.28,.235),'Apron_Navy':(.09,.15,.17),'Barrier_Navy':(.06,.105,.13),
        'Seat_Blue':(.105,.255,.31),'Concrete':(.39,.415,.405),'White':(.82,.84,.79),'Line_White':(.82,.84,.79),
        'Bench_Plastic_White':(.63,.68,.65),'Silver':(.30,.35,.36),'Steel_Highlight':(.27,.33,.34),
        'Leaf_Light':(.18,.28,.105),'Leaf_Green':(.075,.19,.085),'Petal_White':(.79,.80,.69),'Slab_Edge':(.33,.35,.34),
        'Rail_Black':(.055,.075,.08),'Camera_Charcoal':(.07,.085,.095),'Planter_Charcoal':(.13,.16,.16),
        'Soil':(.10,.073,.04),'Net_Tape_White':(.78,.80,.74),'Net_Binding':(.78,.80,.74),
        'Net_Frame':(.055,.075,.08),'Fence_Steel':(.07,.09,.09)}
    colors.update({name:tuple(value[:3]) for name,value in json.loads((ROOT/'ArtSource'/'Stadiums'/'CourtPalette.json').read_text()).items()})
    textures={}
    for old in list(bpy.data.materials):
        if not old.use_nodes:continue
        if old.name in ['Net_Weave','ChainLink']:continue
        bs=next((n for n in old.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        color=colors.get(old.name,tuple(bs.inputs['Base Color'].default_value[:3]) if bs else (.1,.1,.1))
        name=old.name;old.name='Original_'+name
        replacement=mat(name,color,.88 if 'Court' in name or 'Apron' in name else .72,'Venue_Micrograin.png')
        for o in original:
            for slot in o.material_slots:
                if slot.material==old:slot.material=replacement
        textures[name]='Venue_Micrograin.png'
    for name,color,rough,tex in [('Garden_Bark',(.30,.23,.16),.96,'Venue_Bark.png'),('Garden_Leaf_A',(.15,.29,.11),.92,'Venue_Micrograin.png'),
        ('Garden_Leaf_B',(.095,.22,.105),.92,'Venue_Micrograin.png'),('Garden_Soil',(.15,.17,.105),.98,None),
        ('Garden_Grass',(.29,.39,.25),.97,'Venue_Lawn.png'),('Venue_Paving',(.39,.43,.42),.95,'Venue_Paving.png'),
        ('Venue_Metal',(.13,.18,.19),.65,'Venue_Micrograin.png'),('Venue_Timber',(.39,.26,.135),.78,'Venue_Bark.png'),
        ('Venue_Polymer',(.10,.29,.32),.68,'Venue_Micrograin.png'),('Venue_OffWhite',(.75,.77,.70),.78,'Venue_Micrograin.png'),
        ('Venue_Signage',(1,1,1),.9,'Venue_Signage.png')]:
        mat(name,color,rough,tex);textures[name]=tex
    for o in original:
        if o.name=='Players_Benches_And_Broadcast_Cameras':
            bm=bmesh.new();bm.from_mesh(o.data)
            remove=[v for v in bm.verts if (o.matrix_world@v.co).y>-2.0]
            bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free();o.data.update()
        simplify(o)
        if not any(s.material and any(n.type=='TEX_IMAGE' and n.image and n.image.name.startswith(('ChainLink','NetWeave')) for n in s.material.node_tree.nodes) for s in o.material_slots):
            project_uv(o,1.7 if o.name.startswith('Court_') else .8)
    # A narrow paved apron and a planted park; large central opening stays clear.
    ground_level=-.01 if pro else -.14
    cube('Garden_Paved_Apron',(0,0,ground_level-.06),(27 if pro else 19,36 if pro else 28,.12),materials['Venue_Paving'])
    garden(pro,ground_level-.018);props(pro,z)
    if pro:
        # Avoid the south gate and frame the far side visible from the lobby.
        sign('Brand_Far',(0,-10.79,z+.56),5.6,.70,0,(-1,0,0))
        sign('Cheer_Far_Left',(-4.5,10.59,z+.56),2.65,.54,1,(1,0,0))
        sign('Cheer_Far_Right',(4.5,10.59,z+.56),2.65,.54,3,(1,0,0))
        sign('Cheer_West',(-6.07,3.5,z+.56),4.5,.63,2,(0,1,0))
        sign('Cheer_East',(6.07,3.5,z+.56),4.5,.63,1,(0,-1,0))
        sign('Brand_West',(-6.07,-3.3,z+.56),4.5,.63,0,(0,1,0))
        sign('Brand_East',(6.07,-3.3,z+.56),4.5,.63,0,(0,-1,0))
    else:
        cube('Community_Sign_Frame',(0,-9.33,z+1.50),(4.2,.05,.65),materials['Venue_Metal'],.018)
        sign('Community_Brand',(0,-9.292,z+1.50),4.10,.56,0,(-1,0,0))
        for x in [-4.65,4.65]:
            cylinder('Court_Litter_Bin',(x,6,z),(x,6,z+.63),.22,materials['Venue_Metal'],10)
            cylinder('Court_Bin_Lid',(x,6,z+.64),(x,6,z+.68),.24,materials['Venue_OffWhite'],10)
    # Join only generated pieces by shared material: small renderer count and no per-leaf scripts.
    for m in list(materials.values()):
        group=[o for o in generated if o.name in bpy.context.scene.objects and o.data.materials and o.data.materials[0]==m]
        if not group:continue
        for o in group:
            if m.name!='Venue_Signage':project_uv(o,.8 if m.name!='Garden_Grass' else 1.25)
        generated=[o for o in generated if o not in group]
        bpy.ops.object.select_all(action='DESELECT')
        for o in group:o.select_set(True)
        bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();group[0].name='Refined_'+m.name
        # Bake transforms so Unity can attach the mesh at the existing venue offset.
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Verify authoring never moves the regulatory surfaces or net vertices.
    for name,verts in protected.items():
        obj=bpy.data.objects[name];assert verts==[list(v.co) for v in obj.data.vertices],name
    # Collapse repeated seat/rail/stand material submissions, while preserving
    # individual court/net objects and the interactive gate used by the prefab.
    structure=[o for o in original if not o.name.startswith(('Court_','PortableNet_','Gate_'))]
    if len(structure)>1:
        bpy.ops.object.select_all(action='DESELECT')
        for o in structure:o.select_set(True)
        bpy.context.view_layer.objects.active=structure[0];bpy.ops.object.join()
        structure[0].name='Refined_Venue_Structure'
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for o in objects:
        o.data.calc_loop_triangles()
        o.data.name=o.name+'_RefinedMesh'
    report={'venue':key,'source':filename,'triangles':sum(len(o.data.loop_triangles) for o in objects),
        'renderers':len(objects),'protectedGeometry':list(protected),'textures':textures,
        'objects':[{'name':o.name,'triangles':len(o.data.loop_triangles),'materials':[m.name for m in o.data.materials]} for o in objects]}
    # Export only meshes, without preview lights/cameras. No Draco dependency.
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
        parent=o.parent
        while parent:
            parent.select_set(True);parent=parent.parent
    bpy.ops.export_scene.gltf(filepath=str(OUT/(filename+'.glb')),export_format='GLB',use_selection=True,
        export_animations=False,export_cameras=False,export_lights=False,export_yup=True,export_apply=False,
        export_image_format='AUTO',export_texcoords=True,export_normals=True,export_tangents=False,export_attributes=False)
    report['bytes']=(OUT/(filename+'.glb')).stat().st_size
    render(key,z)
    # Source is outside Assets and contains only the current two source venues individually.
    for im in bpy.data.images:
        if im.source=='FILE' and not im.packed_file:im.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(filename+'_Refined.blend')),compress=True)
    (WORK/(key+'-manifest.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print('VENUE_COMPLETE',key,report['triangles'],report['bytes'],flush=True)

for k,f in [('Professional','ProfessionalStadium'),('LowPoly','PickleballCourt_LowPoly')]:build(k,f)
