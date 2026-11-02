import bpy, json, math
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/SSAFY/pjt/bindraid/Builds/AnimationReview/TripoReplacement')
scene=bpy.context.scene;arm=bpy.data.objects['Armature'];meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('tripo_part_')]
cam=bpy.data.objects.get('ReviewCamera')
if not cam:
    cam=bpy.data.objects.new('ReviewCamera',bpy.data.cameras.new('ReviewCamera'));scene.collection.objects.link(cam)
scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.65
cam.location=(3,-4,2.15);cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD'
scene.world=bpy.data.worlds.new('ReviewWorld') if not scene.world else scene.world;scene.world.color=(.07,.07,.07)
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
floor=bpy.data.objects.get('ReviewGround')
if not floor:
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01));floor=bpy.context.object;floor.name='ReviewGround';floor.color=(.18,.2,.23,1)
report={'bones':len(arm.data.bones),'faces':sum(len(o.data.polygons) for o in meshes),'clips':{},'weight_errors':0}
for o in meshes:
    for v in o.data.vertices:
        if abs(sum(g.weight for g in v.groups)-1)>.002:report['weight_errors']+=1
for name in ['Walk','Sprint','Dodge']:
    a=bpy.data.actions[name];arm.animation_data.action=a;arm.animation_data.action_slot=a.slots[0]
    end=round(a.frame_range.y);folder=OUT/name;folder.mkdir(exist_ok=True);stats=[]
    for f in range(end+1):
        scene.frame_set(f);bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
        verts=[o.matrix_world@v.co for o in meshes for v in o.evaluated_get(dg).data.vertices]
        minz=min(v.z for v in verts);maxz=max(v.z for v in verts)
        stretch=max(abs((p.tail-p.head).length/p.bone.length-1) for p in arm.pose.bones if p.bone.length>1e-6)
        stats.append({'frame':f,'min_z':minz,'max_z':maxz,'bone_stretch':stretch})
        if name=='Dodge' or f%2==0:
            scene.render.filepath=str(folder/f'{f:03d}.png');bpy.ops.render.render(write_still=True)
    report['clips'][name]={'seconds':end/24,'max_bone_stretch':max(s['bone_stretch'] for s in stats),'min_ground':min(s['min_z'] for s in stats),'samples':stats}
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
arm.animation_data.action=bpy.data.actions['Dodge'];arm.animation_data.action_slot=bpy.data.actions['Dodge'].slots[0];scene.frame_start=0;scene.frame_end=18;scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spartan-tripo-final.blend'))
print('REVIEW_DONE',[(k,v['max_bone_stretch'],v['min_ground']) for k,v in report['clips'].items()])
