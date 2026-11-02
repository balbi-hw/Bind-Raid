import bpy, json, shutil
from pathlib import Path
from mathutils import Quaternion, Vector
ROOT=Path('C:/Users/SSAFY/pjt/bindraid');OUT=ROOT/'Builds/AnimationReview/LocomotionFix';OUT.mkdir(exist_ok=True)
scene=bpy.data.scenes['Tripo Replacement'];bpy.context.window.scene=scene
arm=bpy.data.objects['Armature'];meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('tripo_part_')]
source=ROOT/'Assets/ThirdParty/SpartanTripo/spartan-tripo.fbx'
if not (OUT/'before.fbx').exists():shutil.copy2(source,OUT/'before.fbx')
if not (OUT/'before.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'before.blend'),copy=True)
act=bpy.data.actions['Sprint'];arm.animation_data.action=act;arm.animation_data.action_slot=act.slots[0]
for t in arm.animation_data.nla_tracks:t.mute=True
end=int(act.frame_range.y);poses=[]
for f in range(end+1):
    scene.frame_set(f);poses.append({p.name:(p.rotation_quaternion.copy(),p.location.copy(),p.scale.copy()) for p in arm.pose.bones})
# Correct the small end-to-start drift across the whole take, without a held last frame.
for layer in act.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in list(bag.fcurves):bag.fcurves.remove(curve)
prev={}
for f in range(end+1):
    scene.frame_set(f);t=f/end
    for p in arm.pose.bones:
        q,loc,scale=poses[f][p.name];first=poses[0][p.name];last=poses[-1][p.name]
        residual=last[0].inverted()@first[0]
        if residual.w<0:residual.negate()
        p.rotation_mode='QUATERNION';p.rotation_quaternion=q@Quaternion().slerp(residual,t)
        if p.name in prev and p.rotation_quaternion.dot(prev[p.name])<0:p.rotation_quaternion.negate()
        prev[p.name]=p.rotation_quaternion.copy()
        p.location=loc+(first[1]-last[1])*t;p.scale=scale
    bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
    bottom=min((o.matrix_world@v.co).z for o in meshes for v in o.evaluated_get(dg).data.vertices)
    hips=arm.pose.bones['mixamorig:Hips']
    hips.location+=hips.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,max(0,.002-bottom)/arm.scale.z))
    for p in arm.pose.bones:
        for path in ['rotation_quaternion','location','scale']:p.keyframe_insert(path,frame=f,group=p.name)
for layer in act.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in bag.fcurves:
                for point in curve.keyframe_points:point.interpolation='LINEAR'
arm.animation_data.action=None
for t in arm.animation_data.nla_tracks:t.mute=False
with bpy.context.temp_override(selected_objects=[arm]+meshes,selected_editable_objects=[arm]+meshes):
    bpy.ops.export_scene.fbx(filepath=str(source),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',apply_scale_options='FBX_SCALE_UNITS')
for t in arm.animation_data.nla_tracks:t.mute=True
arm.animation_data.action=act;arm.animation_data.action_slot=act.slots[0]
positions=[]
for f in [0,end]:
    scene.frame_set(f);positions.append({p.name:arm.matrix_world@p.head for p in arm.pose.bones})
gap=max((positions[0][n]-positions[1][n]).length for n in positions[0])
scene.frame_start=0;scene.frame_end=end;scene.frame_set(0)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spartan-locomotion-fixed.blend'))
(OUT/'loop-validation.json').write_text(json.dumps({'max_joint_loop_gap_m':gap,'fps':scene.render.fps,'frames':end},indent=2))
print('LOOP_FIXED max joint gap',gap)
