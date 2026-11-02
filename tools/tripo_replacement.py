"""Preserve supplied Tripo skin and locomotion; author roll on its existing bones."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion, Euler
ROOT=Path('C:/Users/SSAFY/pjt/bindraid')
OUT=ROOT/'Builds/AnimationReview/TripoReplacement'
ASSET=ROOT/'Assets/ThirdParty/SpartanTripo'
ASSET.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
for obj in scene.objects:
    if obj.name=='Icosphere':obj.hide_render=True;obj.hide_set(True)
arm=bpy.data.objects['Armature']
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('tripo_part_')]
# The supplied cape was weighted to upper arms, producing long spikes during a tuck.
cape=bpy.data.objects['tripo_part_0']
for v in cape.data.vertices:
    weights={cape.vertex_groups[g.group].name:g.weight for g in v.groups}
    merged={}
    for name,w in weights.items():
        dest='mixamorig:Spine2' if any(s in name for s in ['Shoulder','Arm','Neck']) else name
        merged[dest]=merged.get(dest,0)+w
    for g in cape.vertex_groups:g.remove([v.index])
    for name,w in merged.items():cape.vertex_groups[name].add([v.index],w,'REPLACE')
def activate(a):
    arm.animation_data.action=a
    if a.slots:arm.animation_data.action_slot=a.slots[0]
def curves(a):
    return [fc for l in a.layers for s in l.strips for bag in s.channelbags for fc in bag.fcurves]
def reset():
    for p in arm.pose.bones:
        p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
def rot(name,x=0,y=0,z=0):
    p=arm.pose.bones['mixamorig:'+name];r=p.bone.matrix_local.to_quaternion()
    q=Euler(tuple(math.radians(v) for v in (x,y,z)),'XYZ').to_quaternion()
    p.rotation_quaternion=r.inverted()@q@r
def key(f):
    for p in arm.pose.bones:
        p.keyframe_insert('rotation_quaternion',frame=f,group=p.name)
        p.keyframe_insert('location',frame=f,group=p.name)
def aim(name,direction,body_delta):
    p=arm.pose.bones['mixamorig:'+name];b=p.bone
    target=body_delta@Vector(direction).normalized()
    world_q=(b.tail_local-b.head_local).normalized().rotation_difference(target)@b.matrix_local.to_quaternion()
    parent_q=p.parent.matrix.to_quaternion() if p.parent else Quaternion()
    local_rest=(b.parent.matrix_local.inverted()@b.matrix_local).to_quaternion() if b.parent else b.matrix_local.to_quaternion()
    p.rotation_quaternion=local_rest.inverted()@parent_q.inverted()@world_q
    bpy.context.view_layer.update()
def floor_correct():
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    lowest=min((o.matrix_world@v.co).z for o in meshes for v in o.evaluated_get(dg).data.vertices)
    p=arm.pose.bones['mixamorig:Hips']
    p.location+=p.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,.006-lowest))
for t in arm.animation_data.nla_tracks:t.mute=True
scene.render.fps=24
# Source curves copied intact apart from a constant horizontal recentering.
for src,name in [('walk','Walk'),('run','Sprint')]:
    if name in bpy.data.actions:continue
    a=bpy.data.actions[src].copy();a.name=name;a.use_fake_user=True;activate(a);scene.frame_set(0)
    hips=arm.pose.bones['mixamorig:Hips'];h=hips.head.copy()
    offset=hips.bone.matrix_local.to_quaternion().inverted()@Vector((h.x,h.y,-.013))
    for fc in curves(a):
        if fc.data_path=='pose.bones["mixamorig:Hips"].location':
            delta=offset[fc.array_index]
            for k in fc.keyframe_points:k.co.y-=delta;k.handle_left.y-=delta;k.handle_right.y-=delta
    a.use_fake_user=True
# A full roll in 0.75 s: anticipation, push, tuck, shoulder/back contact, feet, recovery.
for name in ['Dodge','Idle']:
    old=bpy.data.actions.get(name)
    if old:bpy.data.actions.remove(old)
roll=bpy.data.actions.new('Dodge');roll.use_fake_user=True;activate(roll)
events=[(0,0,-8,18,4,-10,25),(3,18,-65,100,22,-55,40),
        (5,65,-110,135,45,-80,70),(8,155,-125,145,60,-65,105),
        (11,250,-120,140,50,-40,100),(14,330,-80,110,18,-35,65),
        (18,360,-8,18,4,-10,25)]
def sample(f):
    for a,b in zip(events,events[1:]):
        if a[0]<=f<=b[0]:
            t=(f-a[0])/(b[0]-a[0]);t=t*t*(3-2*t)
            return [a[i]+(b[i]-a[i])*t for i in range(1,len(a))]
previous={}
for f in range(19):
    scene.frame_set(f);reset();pitch,hip,knee,spine,reach,elbow=sample(f)
    rot('Hips',pitch,20*math.sin(math.pi*f/18)**2);rot('Spine',spine*.45);rot('Spine1',spine*.35);rot('Spine2',spine*.2);rot('Neck',25*math.sin(math.pi*f/18));rot('Head',10*math.sin(math.pi*f/18))
    for side,sgn in [('Left',1),('Right',-1)]:
        rot(side+'UpLeg',hip+(4 if side=='Left' else -4)*math.sin(math.pi*f/18),0,-sgn*3)
        rot(side+'Leg',knee);rot(side+'Foot',-knee*.25)
        rot(side+'Arm',reach,sgn*72,0);rot(side+'ForeArm',0,0,-sgn*elbow)
    bpy.context.view_layer.update()
    torso=arm.pose.bones['mixamorig:Spine2']
    body_delta=torso.matrix.to_quaternion()@torso.bone.matrix_local.to_quaternion().inverted()
    reach_weight=math.sin(math.pi*f/18)**.6
    for side,s in [('Left',1),('Right',-1)]:
        aim(side+'Arm',(s*.16,-.15-.8*reach_weight,-1+.65*reach_weight),body_delta)
        aim(side+'ForeArm',(-s*.12,-.25-.25*reach_weight,-.7+1.5*reach_weight),body_delta)
    floor_correct()
    for p in arm.pose.bones:
        if p.name in previous and p.rotation_quaternion.dot(previous[p.name])<0:p.rotation_quaternion.negate()
        previous[p.name]=p.rotation_quaternion.copy()
    key(f)
for fc in curves(roll):
    for k in fc.keyframe_points:k.interpolation='LINEAR'
idle=bpy.data.actions.new('Idle');idle.use_fake_user=True;activate(idle)
for f in [0,12,24,36,48]:
    scene.frame_set(f);reset();rot('Spine',2+math.sin(f*math.pi/24));
    for side,s in [('Left',1),('Right',-1)]:
        rot(side+'Arm',-8,s*72);rot(side+'ForeArm',0,0,-s*25);rot(side+'UpLeg',-8);rot(side+'Leg',18);rot(side+'Foot',-5)
    floor_correct();key(f)
activate(roll);scene.frame_start=0;scene.frame_end=18;scene.frame_set(5)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spartan-tripo-work.blend'))
print('TRIPO_READY',len(arm.data.bones),'bones',sum(len(o.data.polygons) for o in meshes),'faces')
