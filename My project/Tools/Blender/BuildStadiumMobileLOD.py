import bpy,json,os,sys
from pathlib import Path
folder=str(Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else Path(__file__).resolve().parents[3]/'UnityWork'/'MobileStadiumLOD')
rows=json.load(open(os.path.join(folder,'input.json'),encoding='utf-8'))
result=[]
for row in rows:
    mesh=bpy.data.meshes.new(row['name'])
    faces=[row['triangles'][i:i+3] for i in range(0,len(row['triangles']),3)]
    mesh.from_pydata(row['vertices'],[],faces);mesh.update()
    if row['uv']:
        layer=mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:layer.data[loop.index].uv=row['uv'][loop.vertex_index]
    obj=bpy.data.objects.new(row['name'],mesh);bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    modifier=obj.modifiers.new('Mobile background LOD','DECIMATE');modifier.ratio=.22;modifier.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    data=obj.data;data.calc_loop_triangles();vertices=[];uv=[];triangles=[];unique={}
    for tri in data.loop_triangles:
        for loop_index in tri.loops:
            loop=data.loops[loop_index];p=data.vertices[loop.vertex_index].co
            tex=data.uv_layers.active.data[loop_index].uv if data.uv_layers.active else (0.,0.)
            key=tuple(p)+tuple(tex)
            if key not in unique:unique[key]=len(vertices);vertices.append(list(p));uv.append(list(tex))
            triangles.append(unique[key])
    result.append(dict(name=row['name'],vertices=vertices,uv=uv,triangles=triangles))
    print(row['name'],len(row['triangles'])//3,'->',len(triangles)//3,flush=True)
    bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(data)
with open(os.path.join(folder,'output.json'),'w',encoding='utf-8') as file:json.dump(result,file,separators=(',',':'))
print('MOBILE_LOD_COMPLETE',flush=True)
