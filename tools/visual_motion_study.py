"""Isolated visual study. Never exports to Assets or changes gameplay."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Euler
ROOT=Path(r'C:/Users/SSAFY/pjt/bindraid')
OUT=ROOT/'Builds/AnimationReview/VisualRework/LegFix';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Builds/AnimationReview/Spartan-production.blend'))
scene=bpy.context.scene;arm=bpy.data.objects['SpartanRig']
for t in list(arm.animation_data.nla_tracks):arm.animation_data.nla_tracks.remove(t)
arm.animation_data.action=None
for p in arm.pose.bones:p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0);p.location=(0,0,0);p.scale=(1,1,1)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
for side,sign in [('L',1),('R',-1)]:
    foot=arm.data.edit_bones['Foot'+side]
    b=arm.data.edit_bones.new('FootTarget'+side);b.head=foot.head;b.tail=foot.head+Vector((0,0,.15));b.use_deform=False
    b=arm.data.edit_bones.new('KneePole'+side);b.head=(sign*.17,-1,.5);b.tail=b.head+Vector((0,0,.12));b.use_deform=False
bpy.ops.object.mode_set(mode='OBJECT')
for side in ['L','R']:
    c=arm.pose.bones['LowerLeg'+side].constraints.new('IK');c.target=arm;c.subtarget='FootTarget'+side;c.chain_count=2;c.use_stretch=False
    # Mirrored rest-bone axes require different pole angles. Measured against
    # the forward-projected hip-to-ankle plane, rather than guessed symmetrically.
    c.pole_target=arm;c.pole_subtarget='KneePole'+side;c.pole_angle=math.radians(5 if side=='L' else 175)
    c=arm.pose.bones['Foot'+side].constraints.new('COPY_ROTATION');c.target=arm;c.subtarget='FootTarget'+side;c.target_space='WORLD';c.owner_space='WORLD'
    # Foot target orientation follows the original shoe, not the vertical control bone.
    target=arm.pose.bones['FootTarget'+side]
    target.rotation_mode='QUATERNION'
    target.rotation_quaternion=target.bone.matrix_local.to_quaternion().inverted()@arm.data.bones['Foot'+side].matrix_local.to_quaternion()
def rot(name,x=0,y=0,z=0):
    p=arm.pose.bones[name];rest=p.bone.matrix_local.to_quaternion()
    q=Euler(tuple(math.radians(v) for v in (x,y,z))).to_quaternion()
    if name.startswith('UpperArm'):q=q@Euler((0,math.radians(70 if name.endswith('L') else -70),0)).to_quaternion()
    p.rotation_quaternion=rest.inverted()@q@rest
actions=[]
for name,frames,stance,lift,reach in [('WalkStudy',37,.62,.10,.24),('RunStudy',25,.38,.21,.32)]:
    action=bpy.data.actions.new(name);arm.animation_data.action=action;actions.append(action)
    for f in range(1,frames+1):
        t=(f-1)/(frames-1);phase=2*math.pi*t
        rot('Hips',0,0,2*math.sin(phase));rot('Spine',5 if name=='WalkStudy' else 12,0,-3*math.sin(phase));rot('Head',-3)
        arm.pose.bones['Hips'].location=(0,-.075+.014*math.cos(2*phase),0)
        for side,offset in [('L',0),('R',.5)]:
            u=(t+offset)%1
            if u<stance:y=-reach+2*reach*u/stance;z=0
            else:
                v=(u-stance)/(1-stance);ease=v*v*(3-2*v);y=reach-2*reach*ease;z=lift*math.sin(math.pi*v)**2
            p=arm.pose.bones['FootTarget'+side]
            p.location=p.bone.matrix_local.to_quaternion().inverted()@Vector((0,y,z))
            rot('UpperArm'+side,-15*math.cos(2*math.pi*u))
            rot('Forearm'+side,0,0,(-25 if side=='L' else 25))
        rot('Cape',2*math.sin(phase-.5));rot('CapeTip',4*math.sin(phase-.9))
        for p in arm.pose.bones:
            p.keyframe_insert('rotation_quaternion',frame=f,group=p.name);p.keyframe_insert('location',frame=f,group=p.name)
    action.use_fake_user=True
arm.animation_data.action=actions[0];scene.frame_start=1;scene.frame_end=37;scene.frame_set(1)
# Neutral floor and two repeatable review cameras.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.02));floor=bpy.context.object;floor.name='ReviewFloor'
floor.color=(.12,.14,.17,1)
for obj in scene.objects:
    if obj.type=='MESH' and obj!=floor:obj.color=(.62,.65,.69,1)
bpy.ops.object.camera_add(location=(3,-4,2.3));camera=bpy.context.object
camera.rotation_euler=(Vector((0,0,.9))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.7;scene.camera=camera
scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.light='STUDIO';scene.display.shading.color_type='OBJECT';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.fps=30
scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'locomotion-study.blend'))
report=[]
for action in actions:
    arm.animation_data.action=action
    for frame in range(1,int(action.frame_range[1])+1):
        scene.frame_set(frame);bpy.context.view_layer.update()
        for side in ['L','R']:
            h=arm.pose.bones['UpperLeg'+side].head.copy();k=arm.pose.bones['LowerLeg'+side].head.copy();f=arm.pose.bones['LowerLeg'+side].tail.copy()
            axis=(f-h).normalized();bend=k-h-axis*(k-h).dot(axis)
            forward=Vector((0,-1,0));forward=(forward-axis*forward.dot(axis)).normalized()
            direction=bend.normalized().dot(forward)
            target=arm.pose.bones['FootTarget'+side].head
            error=(f-target).length
            report.append({'clip':action.name,'frame':frame,'side':side,'knee_forward':direction,'ankle_error':error})
            assert direction>.95,(action.name,frame,side,direction)
(OUT/'leg-validation.json').write_text(json.dumps(report,indent=2))
for action in actions:
    arm.animation_data.action=action;scene.frame_end=int(action.frame_range[1]);scene.render.filepath=str(OUT/action.name/'frame_');bpy.ops.render.render(animation=True)
arm.animation_data.action=actions[0];scene.frame_set(10)
for view,position in [('front',(0,-4,1)),('side',(4,0,1))]:
    camera.location=position;camera.rotation_euler=(Vector((0,0,.85))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/(view+'.png'));bpy.ops.render.render(write_still=True)
print('VISUAL_STUDY_DONE')
