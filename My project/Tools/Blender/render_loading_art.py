"""Render compact, authentic loading artwork and a ball turntable from game GLBs.
Blender 5.x; run in background. Source game models and existing blend files stay intact.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector

PROJECT=Path(__file__).resolve().parents[2]
OUT=PROJECT/'Assets/FrontEndContent/Loading/Artwork'
WORK=PROJECT.parent/'UnityWork/Loading20261019'
OUT.mkdir(parents=True,exist_ok=True);WORK.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.render.engine='CYCLES';scene.cycles.samples=96;scene.cycles.use_denoising=False
REVISION='--revise' in sys.argv
if REVISION:scene.cycles.samples=32;scene.cycles.use_denoising=True
# Leave the laptop GPU available to Unity while the offline render runs.
scene.cycles.device='CPU';scene.render.threads_mode='FIXED';scene.render.threads=8
scene.cycles.denoiser='OPENIMAGEDENOISE'
if hasattr(scene.cycles,'denoising_use_gpu'):scene.cycles.denoising_use_gpu=False
scene.render.resolution_x=2048;scene.render.resolution_y=1152;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='JPEG';scene.render.image_settings.quality=88
scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=-.25
scene.world.color=(.11,.15,.22)

def load(rel,name):
 before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(PROJECT/'Assets/Models'/rel))
 objects=list(set(bpy.data.objects)-before);root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
 for o in objects:
  if o.parent is None:o.parent=root
 return root,objects
def material(name,color,metal=0,rough=.5,emission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 if emission:p.inputs['Emission Color'].default_value=(*color,1);p.inputs['Emission Strength'].default_value=emission
 return m
def aim(obj,point):obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
def light(name,location,energy,color,size=5,kind='AREA'):
 data=bpy.data.lights.new(name,kind);data.energy=energy;data.color=color
 if kind=='AREA':data.shape='DISK';data.size=size
 obj=bpy.data.objects.new(name,data);scene.collection.objects.link(obj);obj.location=location;aim(obj,(0,0,0));return obj
def group_bounds(objects):
 points=[o.matrix_world@Vector(v) for o in objects if o.type=='MESH' for v in o.bound_box]
 return Vector(tuple(min(v[i] for v in points) for i in range(3))),Vector(tuple(max(v[i] for v in points) for i in range(3)))

stadium,stadium_objects=load('Stadium_Enhanced/ProfessionalStadium.glb','Loading_ProfessionalStadium');stadium.location.z=-.45
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.03));bpy.context.object.name='CinematicGround';bpy.context.object.data.materials.append(material('Ground_Navy',(.055,.09,.13)))
sun=light('Golden_Sun',(0,0,12),3.5,(1,.78,.5),kind='SUN');sun.rotation_euler=(math.radians(58),math.radians(-24),math.radians(-32));sun.data.angle=.12
light('Cool_Stadium_Fill',(-5,-3,9),1200,(.5,.71,1),9)
light('Warm_Rim',(7,7,7),2600,(1,.78,.45),6)
light('White_Court_Fill',(0,-2,8),1800,(.9,.95,1),8)
world=scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs['Color'].default_value=(.09,.15,.28,1);world.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
camera_data=bpy.data.cameras.new('Cinematic_Camera');camera=bpy.data.objects.new('Cinematic_Camera',camera_data);scene.collection.objects.link(camera);scene.camera=camera
focus=bpy.data.objects.new('Focus',None);scene.collection.objects.link(focus);camera_data.dof.use_dof=True;camera_data.dof.focus_object=focus

actors=[]
for gender,position,angle in [('Female',(-1.35,-2.9,0),math.pi),('Male',(1.15,2.4,0),0)]:
 root,objects=load('Characters/'+gender+'.glb','Loading_'+gender)
 for o in objects:
  if o.animation_data:o.animation_data_clear()
 bpy.context.view_layer.update();lo,hi=group_bounds(objects);root.scale=Vector((1,1,1))*(1.85/max(.1,hi.z-lo.z));bpy.context.view_layer.update();lo,hi=group_bounds(objects)
 root.location=Vector(position)-Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z));root.rotation_euler.z=angle
 for arm in [o for o in objects if o.type=='ARMATURE']:
  for b in arm.pose.bones:
   name=b.name.lower().replace('mixamorig:','');b.rotation_mode='XYZ'
   if name.endswith('rightarm'):b.rotation_euler=(.3,-.25,-.75)
   elif name.endswith('leftarm'):b.rotation_euler=(.15,.1,.7)
   elif name.endswith('rightforearm'):b.rotation_euler=(.1,.1,-.65)
   elif name.endswith('leftforearm'):b.rotation_euler=(0,.1,.55)
   elif name.endswith('spine'):b.rotation_euler=(.12,.1,-.12)
   elif name.endswith('rightupleg'):b.rotation_euler=(.1,-.1,-.13)
   elif name.endswith('leftupleg'):b.rotation_euler=(-.1,.1,.1)
  # Solve hands into an athletic ready pose instead of relying on Euler axes
  # that differ between source armatures. Forearm tails meet the grip targets.
  for side,dx in [('Right',.34),('Left',-.34)]:
   target=bpy.data.objects.new(gender+'_'+side+'_Grip',None);scene.collection.objects.link(target)
   forward=1 if gender=='Female' else -1
   target.location=(position[0]+dx*(1 if gender=='Female' else -1),position[1]+forward*.30,1.10)
   bone=next((b for b in arm.pose.bones if b.name.lower().endswith(side.lower()+'forearm')),None)
   if bone:
    constraint=bone.constraints.new('IK');constraint.target=target;constraint.chain_count=2;constraint.use_stretch=False
   if side=='Right':grip=target;grip_arm=arm
 bpy.context.view_layer.update()
 hand=next((b for b in grip_arm.pose.bones if b.name.lower().endswith('righthand')),None)
 grip_point=grip_arm.matrix_world@hand.head if hand else grip.location.copy()
 paddle,paddle_objects=load('Paddle/Paddle.glb','Loading_'+gender+'_Paddle');bpy.context.view_layer.update();a,z=group_bounds(paddle_objects)
 paddle.scale=Vector((1,1,1))*(.52/max(z-a));paddle.rotation_euler=(math.radians(-22),math.radians(12),-.25+angle)
 local_grip=Vector(((a.x+z.x)/2,(a.y+z.y)/2,a.z+(z.z-a.z)*.12))
 paddle.location=grip_point-paddle.rotation_euler.to_matrix()@(local_grip*paddle.scale.x)
 actors.append((root,objects,paddle,paddle_objects))
ball,ball_objects=load('ball/pickleball_ball_cartoon.glb','Loading_Original_Pickleball');bpy.context.view_layer.update();lo,hi=group_bounds(ball_objects);ball_scale=.25/max(hi-lo);ball.scale=Vector((1,1,1))*ball_scale;ball.location=(.2,-.45,1.45)
for o in ball_objects:
 if o.type=='MESH':
  for poly in o.data.polygons:poly.use_smooth=True

def render(name,pos,look,lens,fstop):
 camera.location=pos;aim(camera,look);focus.location=look;camera_data.lens=lens;camera_data.dof.aperture_fstop=fstop;scene.render.filepath=str(OUT/(name+'.jpg'));bpy.ops.render.render(write_still=True)
for root,objects,paddle,paddle_objects in actors:
 for o in objects+paddle_objects:o.hide_render=True
render('01_Stadium_Dusk',(9.4,-13.5,5.2),(0,0,.7),32,5.6)
for root,objects,paddle,paddle_objects in actors:
 for o in objects+paddle_objects:o.hide_render=False
render('02_Rivalry',(3.4,-7.3,2.25),(.1,.1,1.05),33,3.2)
if REVISION:
 print('LOADING_ART_REVISED');sys.exit(0)

# Close-up: retain the original holes/geometry; a shallow focus and restrained
# streaks communicate speed without painting detail onto the ball.
ball.location=(.15,-2.9,1.55);ball.scale=Vector((1,1,1))*ball_scale*1.35
streak_material=material('Flight_Champagne',(.9,.7,.3),emission=2)
streaks=[]
for i in range(5):
 curve=bpy.data.curves.new('Flight_Streak_'+str(i),'CURVE');curve.dimensions='3D';curve.bevel_depth=.0025;curve.bevel_resolution=2
 spline=curve.splines.new('BEZIER');spline.bezier_points.add(2)
 for k,p in enumerate(spline.bezier_points):p.co=(.2+(k-2)*.7,-2.8+.15*i,1.42+i*.055);p.handle_left_type=p.handle_right_type='AUTO'
 obj=bpy.data.objects.new(curve.name,curve);scene.collection.objects.link(obj);obj.data.materials.append(streak_material);streaks.append(obj)
render('03_Ball_In_Flight',(1.05,-4.8,1.95),ball.location,62,2)

# A 16-frame turntable is cheaper than a camera/render texture during loading.
# It is the requested GLB, lit once offline and rotated through the rolling axis.
for obj in list(bpy.data.objects):
 if obj not in ball_objects and obj not in {ball,camera,focus} and obj.type!='LIGHT':obj.hide_render=True
for obj in streaks:obj.hide_render=True
ball.location=(0,0,0);ball.scale=Vector((1,1,1))*ball_scale
scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.resolution_x=128;scene.render.resolution_y=128;scene.cycles.samples=24
camera.location=(0,-1.3,.35);aim(camera,(0,0,0));focus.location=(0,0,0);camera_data.type='ORTHO';camera_data.ortho_scale=.30;camera_data.dof.use_dof=False
for i in range(16):
 ball.rotation_euler=(i*math.tau/16,.15,.25);scene.render.filepath=str(WORK/('ball-roll-%02d.png'%i));bpy.ops.render.render(write_still=True)
report={'artwork':[str(p) for p in OUT.glob('*.jpg')],'ball_source':'Assets/Models/ball/pickleball_ball_cartoon.glb','ball_frames':16,'view_transform':'AgX','resolution':[2048,1152],'notes':'Original source models preserved; the rolling ball is an offline 128px turntable.'}
(WORK/'artwork-manifest.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('LOADING_ART_COMPLETE',json.dumps(report))
