import bpy, math, json, shutil
from pathlib import Path
from mathutils import Vector, Quaternion
ROOT=Path('C:/Users/SSAFY/pjt/bindraid');OUT=ROOT/'Builds/AnimationReview/TripoReplacement';ASSET=ROOT/'Assets/ThirdParty/SpartanTripo'
scene=bpy.context.scene;arm=bpy.data.objects['Armature']
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('tripo_part_')]
def activate(obj,a):
    obj.animation_data_create();obj.animation_data.action=a
    if a.slots:obj.animation_data.action_slot=a.slots[0]
def reset(obj):
    for p in obj.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
def key(obj,f):
    for p in obj.pose.bones:
        p.keyframe_insert('rotation_quaternion',frame=f,group=p.name);p.keyframe_insert('location',frame=f,group=p.name)
# Preserve secondary gameplay actions by retargeting their rotation deltas, never their weights.
with bpy.data.libraries.load(str(ROOT/'Builds/AnimationReview/Spartan-action-fixes.blend'),link=False) as (src,dst):
    dst.objects=['SpartanRig']
old=dst.objects[0];scene.collection.objects.link(old)
for t in old.animation_data.nla_tracks:t.mute=True
old_actions={t.name:t.strips[0].action for t in old.animation_data.nla_tracks}
mapping={'Hips':'Hips','Spine':'Spine','Head':'Head','UpperArmL':'LeftArm','ForearmL':'LeftForeArm','HandL':'LeftHand','UpperArmR':'RightArm','ForearmR':'RightForeArm','HandR':'RightHand','UpperLegL':'LeftUpLeg','LowerLegL':'LeftLeg','FootL':'LeftFoot','UpperLegR':'RightUpLeg','LowerLegR':'RightLeg','FootR':'RightFoot'}
secondary=['WeakAttack','StrongAttack','HitReaction','Knockdown','Incapacitated','Grabbed','GetUp','Brace','Pull','Rescue']
for name in secondary:
    source=old_actions[name];a=bpy.data.actions.new(name);a.use_fake_user=True;activate(arm,a);activate(old,source)
    end=round(source.frame_range.y);duration=end-source.frame_range.x
    for f in range(end+1):
        scene.frame_set(f);reset(arm)
        for sn,dn in mapping.items():
            p=old.pose.bones[sn];target=arm.pose.bones['mixamorig:'+dn]
            rs=p.bone.matrix_local.to_quaternion();rt=target.bone.matrix_local.to_quaternion()
            target.rotation_quaternion=rt.inverted()@rs@p.rotation_quaternion@rs.inverted()@rt
        # Old clips use a 30fps timeline. Preserve their duration in this 24fps scene.
        key(arm,f*24/30)
old.hide_set(True);old.hide_render=True;scene.collection.objects.unlink(old)
textures=ASSET/'Textures';textures.mkdir(exist_ok=True)
for obj in meshes:
    for slot in obj.material_slots:
        mat=slot.material;mat.name=obj.name+'_material'
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                kind='normal' if 'normal' in node.image.name else 'basecolor'
                im=node.image;im.filepath_raw=str(textures/(obj.name+'_'+kind+'.png'));im.file_format='PNG';im.save()
# Normalize size through the armature root; original weights/rest bone transforms stay intact.
arm.parent=None;arm.scale=(1.85,)*3
for o in meshes:
    # glTF skin meshes are children of this armature; preserve that hierarchy.
    assert o.parent==arm,(o.name,o.parent)
for t in list(arm.animation_data.nla_tracks):arm.animation_data.nla_tracks.remove(t)
arm.animation_data.action=None
names=['Idle','Walk','Sprint','Dodge']+secondary
for name in names:
    a=bpy.data.actions[name];track=arm.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,0,a);strip.name=name
    if a.slots:strip.action_slot=a.slots[0]
bpy.ops.object.select_all(action='DESELECT');arm.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=arm
with bpy.context.temp_override(selected_objects=[arm]+meshes,selected_editable_objects=[arm]+meshes):
    bpy.ops.export_scene.fbx(filepath=str(ASSET/'spartan-tripo.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',apply_scale_options='FBX_SCALE_UNITS')
for t in arm.animation_data.nla_tracks:t.mute=True
activate(arm,bpy.data.actions['Dodge']);scene.frame_start=0;scene.frame_end=18;scene.frame_set(0)
shutil.copy2('C:/Users/SSAFY/Downloads/spartan warrior 3d model with additional work.glb',OUT/'original-supplied.glb')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spartan-tripo-final.blend'))
print('EXPORTED',[(n,list(bpy.data.actions[n].frame_range)) for n in names])

