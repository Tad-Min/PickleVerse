"""Blender 5.x -> one transparent UI sprite sheet + authored UI motion.
Generated in a fresh factory-startup file. Never opens or modifies user Blender scenes.
Coordinates: XY is screen plane, +Z toward orthographic camera. Unity uses baked
sprites, not a 3D camera per button. .blend stays outside Assets to avoid auto-import.
"""
import bpy, math, json, os, sys
from mathutils import Vector
from array import array
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),"../.."))
OUT=os.path.join(ROOT,"Assets/FrontEndContent/LobbyRefresh")
SRC=os.path.dirname(__file__)
os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=16
scene.cycles.use_denoising=True
scene.render.resolution_x=256;scene.render.resolution_y=256;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.render.film_transparent=True
scene.world.color=(.22,.22,.22)
scene.view_settings.view_transform='Standard'
def mat(name,color,metal=0,rough=.35):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get("Principled BSDF");p.inputs["Base Color"].default_value=(*color,1)
    p.inputs["Metallic"].default_value=metal;p.inputs["Roughness"].default_value=rough
    return m
navy=mat("Midnight enamel",(.027,.063,.105),.35)
gold=mat("Champagne alloy",(.86,.71,.44),.55)
ivory=mat("Warm ceramic",(.96,.91,.73),.1)
mint=mat("Sea glass",(.21,.87,.76),.25)
white=mat("Icon ceramic",(.90,.96,1),.25)
carbon=mat("Carbon face",(.016,.07,.09),.3)
yellow=mat("Match ball",(.81,.98,.15),.02)
dark=mat("Ball holes",(.06,.12,.05))
# Actual repeating carbon relief, baked into the paddle tile.
nodes=carbon.node_tree.nodes;links=carbon.node_tree.links
tex=nodes.new("ShaderNodeTexChecker");tex.inputs["Color1"].default_value=(.01,.055,.065,1);tex.inputs["Color2"].default_value=(.025,.09,.10,1);tex.inputs["Scale"].default_value=34
links.new(tex.outputs["Color"],nodes.get("Principled BSDF").inputs["Base Color"])
def mesh(name,pts,depth,material):
    verts=[(x,y,z) for z in (0,depth) for x,y in pts];n=len(pts)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.data.materials.append(material)
    bevel=ob.modifiers.new("Small light catching bevel","BEVEL");bevel.width=.018;bevel.segments=3
    ob.modifiers.new("Weighted normals","WEIGHTED_NORMAL");return ob
def plate(name,sx,sy,material,z=0):
    c=.10
    pts=[(-sx+c,-sy),(sx-c,-sy),(sx,-sy+c),(sx,sy-c),(sx-c,sy),(-sx+c,sy),(-sx,sy-c),(-sx,-sy+c)]
    ob=mesh(name,pts,.035,material);ob.location.z=z;return ob
def line(name,points,material=white,r=.032,z=.10):
    curve=bpy.data.curves.new(name,"CURVE");curve.dimensions='3D';curve.bevel_depth=r;curve.bevel_resolution=3
    p=curve.splines.new('POLY');p.points.add(len(points)-1)
    for v,xy in zip(p.points,points):v.co=(xy[0],xy[1],z,1)
    ob=bpy.data.objects.new(name,curve);scene.collection.objects.link(ob);ob.data.materials.append(material);return ob
def circle(name,x,y,r,material=white,stroke=.03,z=.10):
    return line(name,[(x+math.cos(i*math.tau/64)*r,y+math.sin(i*math.tau/64)*r) for i in range(65)],material,stroke,z)
def disc(name,x,y,r,material,z=.1):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=r,location=(x,y,z))
    ob=bpy.context.object;ob.name=name;ob.scale.z=.20;ob.data.materials.append(material)
    for p in ob.data.polygons:p.use_smooth=True
    return ob
bpy.ops.object.camera_add(location=(0,0,6));cam=bpy.context.object;cam.name="UI orthographic bake";cam.data.type='ORTHO';cam.data.ortho_scale=2.2;scene.camera=cam
for name,loc,power,size in [("Soft key",(-2,3,5),390,4),("Cool rim",(3,0,3),180,3)]:
    bpy.ops.object.light_add(type='AREA',location=loc);l=bpy.context.object;l.name=name;l.data.energy=power;l.data.shape='DISK';l.data.size=size
    l.rotation_euler=(Vector((0,0,0))-l.location).to_track_quat('-Z','Y').to_euler()
tiles=[]
def tile(name,fn):
    before=set(bpy.data.objects)
    fn()
    obs=list(set(bpy.data.objects)-before)
    for o in obs:o.hide_render=True
    tiles.append((name,obs))
tile("Panel",lambda:(plate("Alloy rim",1,1,gold),plate("Enamel inset",.985,.985,navy,.035)))
tile("Play",lambda:(plate("Play rim",1,1,gold),plate("Play ceramic",.975,.975,ivory,.035),line("Mint trim",[(-.7,-.88),(.7,-.88)],mint,.016,.09)))
tile("Home",lambda:(line("roof",[(-.76,.02),(0,.72),(.76,.02)]),line("house",[(-.54,.18),(-.54,-.65),(.54,-.65),(.54,.18)]),line("door",[(-.15,-.65),(-.15,-.15),(.15,-.15),(.15,-.65)],gold)))
tile("Progress",lambda:([line("bar"+str(i),[(x,-.65),(x,h)],white,.07) for i,(x,h) in enumerate([(-.55,-.10),(0,.25),(.55,.65)])],line("growth",[(-.7,.15),(-.15,.55),(.65,.85)],gold,.025)))
tile("Career",lambda:(line("cup",[(-.48,.65),(.48,.65),(.32,-.06),(-.32,-.06),(-.48,.65)],gold,.045),line("stem",[(0,-.06),(0,-.65),(-.38,-.65),(.38,-.65)],white),line("handleL",[(-.46,.48),(-.75,.48),(-.65,.02),(-.32,-.06)]),line("handleR",[(.46,.48),(.75,.48),(.65,.02),(.32,-.06)])))
tile("Missions",lambda:(plate("clipboard",.54,.72,white),plate("clip",.23,.13,gold,.07),line("check",[(-.27,.07),(-.08,-.12),(.31,.28)],navy,.052,.14),line("text",[(-.28,-.40),(.28,-.40)],navy,.025,.14)))
tile("Collection",lambda:(line("cards",[(-.48,-.72),(-.48,.53),(.49,.53),(.49,-.72),(-.48,-.72)]),line("back",[(-.25,.78),(.73,.78),(.73,-.46)],gold),line("gem",[(0,.26),(.26,-.02),(0,-.30),(-.26,-.02),(0,.26)],gold)))
tile("Shop",lambda:(line("bag",[(-.62,.22),(.62,.22),(.73,-.68),(-.73,-.68),(-.62,.22)]),line("handle",[(math.cos(i*math.pi/32)*.33,.22+math.sin(i*math.pi/32)*.45) for i in range(33)],gold)))
tile("Settings",lambda:(circle("gear",0,0,.56),circle("core",0,0,.23,gold),[line("tooth"+str(i),[(math.cos(i*math.tau/8)*.54,math.sin(i*math.tau/8)*.54),(math.cos(i*math.tau/8)*.78,math.sin(i*math.tau/8)*.78)],white,.065) for i in range(8)]))
tile("Arrow",lambda:line("arrow",[(-.30,.6),(.30,0),(-.30,-.6)],gold,.065))
def paddle():
    pts=[(-.28,-.18),(.28,-.18),(.53,.08),(.50,.62),(.30,.85),(-.30,.85),(-.50,.62),(-.53,.08)]
    mesh("Paddle alloy",pts,.08,gold)
    face=mesh("Carbon face",[(x*.92,y*.95+.025) for x,y in pts],.09,carbon)
    line("Face detail",[(-.30,.18),(.30,.18)],mint,.013,.105)
    grip=plate("Grip",.105,.34,navy,.01);grip.location.y=-.53
    for i in range(6):line("Grip band"+str(i),[(-.10,-.30-i*.09),(.10,-.34-i*.09)],gold,.015,.08)
tile("Paddle",paddle)
def ball():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48,ring_count=24,radius=.65,location=(0,0,.07));ob=bpy.context.object;ob.name="Perforated match ball";ob.data.materials.append(yellow)
    for p in ob.data.polygons:p.use_smooth=True
    # Concave dark insets bake perforation readability without runtime geometry.
    for i,(x,y) in enumerate([(0,0),(-.28,.25),(.28,.25),(-.28,-.25),(.28,-.25),(0,.48),(0,-.48),(-.49,0),(.49,0)]):
        z=.07+math.sqrt(max(.01,.65*.65-x*x-y*y))+.005
        disc("Hole"+str(i),x,y,.078,dark,z)
tile("Ball",ball)
tile("Impact",lambda:(mesh("Flash",[(0,.94),(.12,.16),(.70,.48),(.22,.06),(.94,0),(.20,-.10),(.48,-.65),(.06,-.20),(0,-.94),(-.12,-.18),(-.68,-.5),(-.20,-.07),(-.94,0),(-.20,.10),(-.5,.68),(-.06,.20)],.02,ivory),circle("Shock ring",0,0,.60,mint,.022)))
tile("Sweep",lambda:mesh("Light streak",[(-.75,-1),(-.48,-1),(.75,1),(.48,1)],.005,ivory))
tile("AvatarRing",lambda:(circle("Ring outer",0,0,.86,gold,.028),circle("Ring inner",0,0,.77,white,.012),[line("Notch"+str(i),[(math.cos(i*math.tau/8)*.9,math.sin(i*math.tau/8)*.9),(math.cos(i*math.tau/8)*.96,math.sin(i*math.tau/8)*.96)],gold,.018) for i in range(8)]))
tile("Social",lambda:(circle("Friend head",-.2,.30,.24),circle("Team head",.39,.19,.19,gold),line("Shoulders",[(-.72,-.56),(-.62,-.17),(.15,-.17),(.31,-.56)]),line("Team shoulders",[(.21,-.13),(.66,-.13),(.76,-.56)],gold)))
# Author a short strike action. Export normalized keyframes for 2D playback in Unity.
motion={"duration":.40,"paddle":[[0,22],[.06,40],[.15,-32],[.24,-12],[.40,22]],"paddleX":[[0,0],[.06,-.02],[.15,.05],[.24,.08],[.40,0]],"ballX":[[0,0],[.15,0],[.24,.50],[.40,1.8]],"ballY":[[0,0],[.15,0],[.24,.15],[.40,.46]],"impactTime":.15}
paddle_objs=tiles[10][1];pivot=bpy.data.objects.new("Play_Strike_Pivot",None);scene.collection.objects.link(pivot)
for ob in paddle_objs:ob.parent=pivot
for t,angle in motion["paddle"]:
    pivot.rotation_euler.z=math.radians(angle);pivot.keyframe_insert("rotation_euler",frame=1+t*60)
for t,x in motion["paddleX"]:
    pivot.location.x=x*6.6;pivot.keyframe_insert("location",frame=1+t*60)
scene.render.fps=60
scene.frame_set(1)
# Keep each sprite neutral; motion applies to the 2D sprite in Unity.
pivot.rotation_euler.z=0
atlas=array('f',[0])*(1024*1024*4)
manifest=[]
for index,(name,obs) in enumerate(tiles):
    for ob in obs:ob.hide_render=False
    scene.render.filepath=os.path.join(SRC,"tile-"+name+".png")
    bpy.ops.render.render(write_still=True)
    im=bpy.data.images.load(scene.render.filepath,check_existing=False)
    pixels=array('f',[0])*(256*256*4);im.pixels.foreach_get(pixels)
    col=index%4;row=index//4
    for y in range(256):
        start=((row*256+y)*1024+col*256)*4;atlas[start:start+1024]=pixels[y*1024:(y+1)*1024]
    bpy.data.images.remove(im)
    os.remove(scene.render.filepath)
    for ob in obs:ob.hide_render=True
    manifest.append({"name":name,"x":col*256,"y":row*256,"size":256})
im=bpy.data.images.new("Lobby_UI_Atlas",width=1024,height=1024,alpha=True);im.pixels.foreach_set(atlas)
im.filepath_raw=os.path.join(OUT,"Lobby_UI_Atlas.png");im.file_format='PNG';im.save()
with open(os.path.join(SRC,"lobby-ui-manifest.json"),"w",encoding="utf-8") as f:json.dump({"sprites":manifest,"motion":motion,"atlasSize":1024},f,indent=2)
for index,(_,objects) in enumerate(tiles):
    for ob in objects:
        ob.hide_render=index not in (10,11,12)
        ob.hide_set(index not in (10,11,12))
# Keep the full contact choreography editable in the Blender source as well as JSON.
ball_root=bpy.data.objects.new("Play_Ball_Flight",None);scene.collection.objects.link(ball_root)
for ob in tiles[11][1]:ob.parent=ball_root
ball_root.scale=(.24,.24,.24)
for x_key,y_key in zip(motion["ballX"],motion["ballY"]):
    ball_root.location=(.75+x_key[1]*.6,.30+y_key[1]*.6,.18)
    ball_root.keyframe_insert("location",frame=1+x_key[0]*60)
flash_root=bpy.data.objects.new("Play_Contact_VFX",None);scene.collection.objects.link(flash_root)
for ob in tiles[12][1]:ob.parent=flash_root
flash_root.location=(.72,.28,.22)
for t,scale in [(0,0),(.14,0),(.17,.4),(.25,.7),(.32,0),(.4,0)]:
    flash_root.scale=(scale,)*3;flash_root.keyframe_insert("scale",frame=1+t*60)
nav_root=bpy.data.objects.new("Nav_Hover_Feedback",None);scene.collection.objects.link(nav_root)
for ob in tiles[2][1]:ob.parent=nav_root
for t,scale in [(0,1),(.12,1.045),(.24,1)]:
    nav_root.scale=(scale,)*3;nav_root.keyframe_insert("scale",frame=1+t*60)
scene.frame_end=25
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC,"Lobby_UI_Source.blend"))
print("LOBBY_UI_COMPLETE",os.path.getsize(os.path.join(OUT,"Lobby_UI_Atlas.png")))


