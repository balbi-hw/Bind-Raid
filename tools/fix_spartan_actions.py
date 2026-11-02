"""Repair brace/pull posing and author a full forward roll on the production rig."""
import bpy, math
from mathutils import Euler, Vector
from pathlib import Path
ROOT=Path(r'C:/Users/SSAFY/pjt/bindraid')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Builds/AnimationReview/Spartan-production.blend'))
arm=bpy.data.objects['SpartanRig'];scene=bpy.context.scene
# Forearm pieces must not retain upper-arm weights past their split boundary.
# Otherwise elbow flex stretches the separate gauntlet/hand mesh.
for obj in scene.objects:
    if obj.type!='MESH' or obj.name not in ['Spartan_Part_06','Spartan_Part_09']:continue
    side='L' if obj.name.endswith('06') else 'R'
    for v in obj.data.vertices:
        x=abs(v.co.x)/(1.8/.979)
        t=max(0,min(1,(x-.325)/.035));t=t*t*(3-2*t)
        for g in obj.vertex_groups:g.remove([v.index])
        obj.vertex_groups['Forearm'+side].add([v.index],1-t,'REPLACE')
        obj.vertex_groups['Hand'+side].add([v.index],t,'REPLACE')
for t in arm.animation_data.nla_tracks:t.mute=True
actions={t.name:t.strips[0].action for t in arm.animation_data.nla_tracks}
def rotation(p,x=0,y=0,z=0):
    rest=p.bone.matrix_local.to_quaternion()
    q=Euler(tuple(math.radians(v) for v in (x,y,z)),'XYZ').to_quaternion()
    if p.name.startswith('UpperArm'):
        q=Euler((math.radians(x),0,0)).to_quaternion()@Euler((0,math.radians(70 if p.name.endswith('L') else -70),0)).to_quaternion()
    elif p.name.startswith('Forearm'):
        q=Euler((0,0,math.radians(x*(1 if p.name.endswith('L') else -1)))).to_quaternion()
    p.rotation_mode='QUATERNION';p.rotation_quaternion=rest.inverted()@q@rest
for name in ['Brace','Pull','Dodge']:
    a=actions[name];arm.animation_data.action=a
    if a.slots:arm.animation_data.action_slot=a.slots[0]
    for layer in a.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in list(bag.fcurves):bag.fcurves.remove(curve)
    end=10 if name=='Dodge' else 25
    for f in range(1,end+1):
        t=(f-1)/(end-1);tuck=math.sin(math.pi*t)**.6
        for p in arm.pose.bones:
            rotation(p);p.location=(0,0,0);p.scale=(1,1,1)
        if name=='Dodge':
            rotation(arm.pose.bones['Hips'],360*t)
            rotation(arm.pose.bones['Spine'],35*tuck)
            rotation(arm.pose.bones['Head'],-20*tuck)
            # Vertical compensation stays on the skeleton; gameplay owns translation.
            arm.pose.bones['Hips'].location=arm.data.bones['Hips'].matrix_local.to_quaternion().inverted()@Vector((0,0,-.22*tuck))
            for side in ['L','R']:
                rotation(arm.pose.bones['UpperLeg'+side],-65*tuck)
                rotation(arm.pose.bones['LowerLeg'+side],105*tuck)
                rotation(arm.pose.bones['UpperArm'+side],-55*tuck)
                rotation(arm.pose.bones['Forearm'+side],-85*tuck)
        else:
            cycle=(1-math.cos(2*math.pi*t))*.5 if name=='Pull' else 0
            rotation(arm.pose.bones['Spine'],4-8*cycle)
            for side in ['L','R']:
                rotation(arm.pose.bones['UpperArm'+side],-25+10*cycle)
                rotation(arm.pose.bones['Forearm'+side],-65-20*cycle)
                rotation(arm.pose.bones['UpperLeg'+side],-8)
                rotation(arm.pose.bones['LowerLeg'+side],12)
        for p in arm.pose.bones:
            p.keyframe_insert('rotation_quaternion',frame=f,group=p.name)
            p.keyframe_insert('location',frame=f,group=p.name)
            p.keyframe_insert('scale',frame=f,group=p.name)
        if name=='Dodge':
            scene.frame_set(f)
            deps=bpy.context.evaluated_depsgraph_get()
            minimum=100.0
            for obj in scene.objects:
                if obj.type!='MESH':continue
                evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
                minimum=min(minimum,min((evaluated.matrix_world@v.co).z for v in mesh.vertices))
                evaluated.to_mesh_clear()
            hips=arm.pose.bones['Hips']
            hips.location+=hips.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,.015-minimum))
            hips.keyframe_insert('location',frame=f,group='Hips')
    track=next(t for t in arm.animation_data.nla_tracks if t.name==name)
    track.strips[0].action_frame_end=end;track.strips[0].frame_end=end
arm.animation_data.action=None
meshes=[o for o in scene.objects if o.type=='MESH']
with bpy.context.temp_override(selected_objects=[arm]+meshes,selected_editable_objects=[arm]+meshes):
    for t in arm.animation_data.nla_tracks:t.mute=False
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'Assets/ThirdParty/SpartanWarrior/spartan-rigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,axis_forward='-Z',axis_up='Y')
    for t in arm.animation_data.nla_tracks:t.mute=True
arm.animation_data.action=actions['Dodge'];scene.frame_start=1;scene.frame_end=10;scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Builds/AnimationReview/Spartan-production.blend'))
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Builds/AnimationReview/Spartan-action-fixes.blend'))
