"""Build the segmented Spartan in an isolated scene; source and legacy rigs stay intact."""
import bpy, math, ast, json
from pathlib import Path
from mathutils import Vector, Euler
ROOT=Path(r'C:/Users/SSAFY/pjt/bindraid')
OUT=ROOT/'Assets/ThirdParty/SpartanWarrior'
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('SpartanProduction')
bpy.context.window.scene=scene
bpy.ops.import_scene.gltf(filepath=r'C:/Users/SSAFY/Downloads/spartan warrior 3d model.glb')
meshes=[o for o in scene.objects if o.type=='MESH']
# Import can suffix names when the inspection scene is present.
parts={int(o.name.split('tripo_part_')[1].split('.')[0]):o for o in meshes}
scale=1.8/.979
for i,o in parts.items():
    world=o.matrix_world.copy();o.parent=None;o.matrix_world.identity()
    for v in o.data.vertices:v.co=(world@v.co)*scale
    bpy.context.view_layer.objects.active=o
    modifier=o.modifiers.new('Game mesh reduction','DECIMATE');modifier.ratio=.045
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    o.name=f'Spartan_Part_{i:02}'
data=bpy.data.armatures.new('SpartanSkeleton');arm=bpy.data.objects.new('SpartanRig',data)
scene.collection.objects.link(arm);bpy.context.view_layer.objects.active=arm;arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(n,h,t,p=None):
    b=data.edit_bones.new(n);b.head=Vector(h)*scale;b.tail=Vector(t)*scale
    if p:b.parent=data.edit_bones[p]
bone('Hips',(0,0,.44),(0,0,.53));bone('Spine',(0,0,.53),(0,0,.69),'Hips')
bone('Head',(0,0,.69),(0,0,.9),'Spine')
bone('Cape',(0,.05,.71),(0,.1,.42),'Spine');bone('CapeTip',(0,.1,.42),(0,.13,.2),'Cape')
for side,s in [('L',1),('R',-1)]:
    bone('UpperArm'+side,(s*.12,-.025,.71),(s*.25,-.025,.70),'Spine')
    bone('Forearm'+side,(s*.25,-.025,.70),(s*.345,-.025,.70),'UpperArm'+side)
    bone('Hand'+side,(s*.345,-.025,.70),(s*.405,-.025,.70),'Forearm'+side)
    bone('UpperLeg'+side,(s*.075,0,.45),(s*.09,0,.27),'Hips')
    bone('LowerLeg'+side,(s*.09,0,.27),(s*.105,0,.075),'UpperLeg'+side)
    bone('Foot'+side,(s*.105,0,.075),(s*.105,-.11,.035),'LowerLeg'+side)
bpy.ops.object.mode_set(mode='OBJECT')
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for i,o in parts.items():
    groups={b.name:o.vertex_groups.new(name=b.name) for b in data.bones}
    for v in o.data.vertices:
        x,y,z=v.co/scale;s='L' if x>0 else 'R';ax=abs(x)
        if i==0:
            lower=1-smooth(.35,.5,z);upper=smooth(.63,.75,z)
            w={'Spine':upper,'Cape':(1-upper)*(1-lower),'CapeTip':(1-upper)*lower}
        elif i==2:w={'Head':1}
        elif i in (5,6,9,10):
            shoulder=1-smooth(.10,.16,ax);elbow=smooth(.225,.275,ax);hand=smooth(.325,.36,ax)
            w={'Spine':shoulder,'UpperArm'+s:(1-shoulder)*(1-elbow),'Forearm'+s:(1-shoulder)*elbow*(1-hand),'Hand'+s:(1-shoulder)*elbow*hand}
        elif i in (7,8,12,13,15,16,17,18):w={'Foot'+s:1}
        elif i in (3,4):
            hip=smooth(.40,.48,z);knee=1-smooth(.235,.30,z);foot=1-smooth(.07,.12,z)
            w={'Hips':hip,'UpperLeg'+s:(1-hip)*(1-knee),'LowerLeg'+s:(1-hip)*knee*(1-foot),'Foot'+s:(1-hip)*knee*foot}
        else:
            spine=smooth(.48,.59,z);w={'Hips':1-spine,'Spine':spine}
        for name,weight in w.items():
            if weight>0:groups[name].add([v.index],weight,'REPLACE')
    o.parent=arm;mod=o.modifiers.new('Spartan skin','ARMATURE');mod.object=arm
    assert all(abs(sum(g.weight for g in v.groups)-1)<1e-5 for v in o.data.vertices)
arm.animation_data_create()
created_actions=[]
def action(name,length,poses):
    clip=bpy.data.actions.new(name);arm.animation_data.action=clip
    for frame,values in poses:
        for p in arm.pose.bones:
            angles=values.get(p.name,(0,0,0));x,y,z=angles
            # Convert world-axis pose deltas into each T-pose bone's local frame.
            q=Euler(tuple(math.radians(v) for v in (x,y,z)),'XYZ').to_quaternion()
            if p.name.startswith('UpperArm'):
                lower=70 if p.name.endswith('L') else -70
                q=Euler((math.radians(x),0,math.radians(z)),'XYZ').to_quaternion() @ Euler((0,math.radians(lower+y),0),'XYZ').to_quaternion()
            elif p.name.startswith('Forearm'):
                q=Euler((0,0,math.radians(x*(1 if p.name.endswith('L') else -1))),'XYZ').to_quaternion()
            rest=p.bone.matrix_local.to_quaternion();p.rotation_mode='QUATERNION'
            p.rotation_quaternion=rest.inverted()@q@rest
            p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
    clip.use_fake_user=True
    created_actions.append(clip)
    return clip
# Retain the game's named action contract; author locomotion with bent knees.
tree=ast.parse((ROOT/'tools/rig_warrior.py').read_text(encoding='utf-8'))
for node in tree.body:
    if isinstance(node,ast.Expr) and isinstance(node.value,ast.Call) and getattr(node.value.func,'id','')=='action':
        args=[eval(compile(ast.Expression(a),'<pose>','eval'),{'neutral':(1,{})}) for a in node.value.args]
        name,length,poses=args
        if name in ('Walk','Sprint'):
            poses=[];run=name=='Sprint'
            for f in range(1,length+1):
                phase=2*math.pi*(f-1)/(length-1);v={'Spine':(6 if run else 1,0,2*math.sin(phase))}
                for side,offset in [('L',0),('R',math.pi)]:
                    p=phase+offset;lift=max(0,math.sin(p))**2
                    v['UpperLeg'+side]=((32 if run else 22)*math.cos(p),0,0)
                    v['LowerLeg'+side]=(-6-(48 if run else 32)*lift,0,0)
                    v['Foot'+side]=(6+12*lift,0,0)
                    v['UpperArm'+side]=(-(22 if run else 12)*math.cos(p),0,0)
                v['Cape']=(3*math.sin(phase-.4),0,1*math.sin(phase));poses.append((f,v))
        if name=='Incapacitated':poses=[(1,poses[-1][1]),(length,poses[-1][1])]
        action(name,length,poses)
# Restrict export actions to this skeleton by using named NLA strips.
own=created_actions
assert len(own)==14
arm.animation_data.action=None
for a in own:
    name=a.name.split('.')[0];track=arm.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,1,a);strip.name=name
    track.mute=True
scene.render.fps=30;scene.frame_set(1)
project=ROOT/'Builds/AnimationReview';project.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'Spartan-production.blend'))
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for o in meshes:o.select_set(True)
with bpy.context.temp_override(selected_objects=[arm]+meshes,selected_editable_objects=[arm]+meshes):
    for t in arm.animation_data.nla_tracks:t.mute=False
    bpy.ops.export_scene.fbx(filepath=str(OUT/'spartan-rigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,axis_forward='-Z',axis_up='Y')
    for t in arm.animation_data.nla_tracks:t.mute=True
arm.animation_data.action=next(a for a in own if a.name.split('.')[0]=='Walk')
scene.frame_start=1;scene.frame_end=33;scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'Spartan-production.blend'))
print('SPARTAN_DONE',sum(len(o.data.polygons) for o in meshes),'faces',[(a.name,tuple(a.frame_range)) for a in own])

