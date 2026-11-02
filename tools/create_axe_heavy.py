import bpy, math, shutil, json
from pathlib import Path
from mathutils import Vector, Quaternion, Matrix
ROOT=Path('C:/Users/SSAFY/pjt/bindraid');OUT=ROOT/'Builds/AnimationReview/AxeHeavy';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes['Tripo Replacement'];bpy.context.window.scene=scene
arm=bpy.data.objects['Armature'];scale=arm.scale.x
meshes=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('tripo_part_')]
source=ROOT/'Assets/ThirdParty/SpartanTripo/spartan-tripo.fbx'
if not (OUT/'before.fbx').exists():shutil.copy2(source,OUT/'before.fbx')
if not (OUT/'before.blend').exists():bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'before.blend'),copy=True)
def activate(a):
 arm.animation_data.action=a
 if a.slots:arm.animation_data.action_slot=a.slots[0]
for track in arm.animation_data.nla_tracks:track.mute=True
activate(bpy.data.actions['Idle']);scene.frame_set(0)
base={p.name:(p.location.copy(),p.rotation_quaternion.copy(),p.scale.copy()) for p in arm.pose.bones}
def bone(n):return arm.pose.bones['mixamorig:'+n]
def pos(n):return arm.matrix_world@bone(n).head
feet={s:(pos(s+'Foot'),bone(s+'Foot').matrix.to_quaternion().copy()) for s in ['Left','Right']}
def setrot(p,q):
 m=p.matrix.copy();loc=m.translation.copy();p.matrix=Matrix.Translation(loc)@q.to_matrix().to_4x4();bpy.context.view_layer.update()
def aim(n,child,target):
 p=bone(n);v=pos(child)-pos(n);d=Vector(target)-pos(n)
 if v.length>1e-6 and d.length>1e-6:setrot(p,v.rotation_difference(d)@p.matrix.to_quaternion())
def ik(a,b,c,target,pole):
 root=pos(a);end=Vector(target);l1=(pos(b)-root).length;l2=(pos(c)-pos(b)).length
 d=end-root;dist=min(max(d.length,abs(l1-l2)+.001),l1+l2-.001);axis=d.normalized()
 side=Vector(pole)-root;side=(side-axis*side.dot(axis)).normalized()
 along=(l1*l1-l2*l2+dist*dist)/(2*dist);height=math.sqrt(max(0,l1*l1-along*along))
 elbow=root+axis*along+side*height
 aim(a,b,elbow);aim(b,c,root+axis*dist)
def smooth(t):return t*t*(3-2*t)
# frame, grip XYZ, shaft direction, spine bend/twist, hip drop, two-hand weight
keys=[
 (0,(-.15,-.25,1.12),(0,-.3,1),0,0,0,0),
 (6,(0,-.32,1.26),(-.1,-.2,1),-5,-8,.015,1),
 (13,(-.02,-.23,1.59),(-.4,.65,.8),-10,-30,.035,1),
 (15,(-.02,-.22,1.6),(-.4,.65,.8),-10,-32,.04,1),
 (18,(.02,-.43,1.18),(.1,-.95,-.2),18,8,.06,1),
 (20,(.08,-.42,1.15),(.5,-.65,-.6),20,25,.10,1),
 (24,(.08,-.40,1.18),(.6,-.6,-.5),18,23,.08,1),
 (29,(.04,-.35,1.23),(.35,-.65,.6),8,12,.04,1),
 (36,(-.15,-.25,1.12),(0,-.3,1),0,0,0,0)]
old=bpy.data.actions['StrongAttack'];old.name='StrongAttack_PreTwoHand'
act=bpy.data.actions.new('StrongAttack');act.use_fake_user=True;activate(act)
for track in list(arm.animation_data.nla_tracks):
 if track.name=='StrongAttack':arm.animation_data.nla_tracks.remove(track)
report=[];prev={}
for frame in range(37):
 scene.frame_set(frame)
 for p in arm.pose.bones:
  p.rotation_mode='QUATERNION';p.location,p.rotation_quaternion,p.scale=base[p.name]
 for i in range(len(keys)-1):
  if keys[i][0]<=frame<=keys[i+1][0]:break
 a,b=keys[i],keys[i+1];t=smooth((frame-a[0])/(b[0]-a[0]))
 grip=Vector(a[1]).lerp(Vector(b[1]),t);shaft=Vector(a[2]).lerp(Vector(b[2]),t).normalized()
 bend,twist,drop,w=[a[j]*(1-t)+b[j]*t for j in range(3,7)]
 for n,factor in [('Spine',.35),('Spine1',.3),('Spine2',.35)]:
  p=bone(n);q=p.bone.matrix_local.to_quaternion();world=Quaternion((1,0,0),math.radians(bend*factor))@Quaternion((0,0,1),math.radians(twist*factor))
  p.rotation_quaternion=q.inverted()@world@q@base[p.name][1]
 # Brief forward bound: airborne compression/extension, then a staggered landing.
 legkeys=[(0,0,0,0,0,0),(6,-.045,.06,0,.06,0),(10,.10,-.18,.18,.17,.08),(14,.15,-.25,.20,.22,.18),(18,0,-.27,0,.22,.05),(23,0,-.23,0,.18,0),(29,0,-.10,0,.08,0),(36,0,0,0,0,0)]
 for j in range(len(legkeys)-1):
  if legkeys[j][0]<=frame<=legkeys[j+1][0]:break
 la,lb=legkeys[j],legkeys[j+1];lt=smooth((frame-la[0])/(lb[0]-la[0]));lift,ly,lz,ry,rz=[la[k]*(1-lt)+lb[k]*lt for k in range(1,6)]
 grip.z+=lift
 p=bone('Hips');p.location+=p.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,(lift-drop)/scale))
 bpy.context.view_layer.update()
 if frame>0 and frame<36:
  for side,sign in [('Right',-1),('Left',1)]:
   dy,dz=(ly,lz) if side=='Left' else (ry,rz)
   ik(side+'UpLeg',side+'Leg',side+'Foot',feet[side][0]+Vector((0,dy,dz)),(sign*.2,-1,.5));setrot(bone(side+'Foot'),feet[side][1])
 # FBX handedness conversion maps the equipment shaft onto Blender hand +X.
 # Keep each wrist on its own side of the handle; mirrored palm axes crossed the forearms.
 x=shaft;y=Vector((1,0,0));y=(y-x*y.dot(x)).normalized();z=x.cross(y).normalized()
 rightrot=Matrix((x,y,z)).transposed().to_quaternion()
 leftrot=Matrix((-x,-y,z)).transposed().to_quaternion()
 for side,q,center,sign in [('Right',rightrot,grip,-1),('Left',leftrot,grip-shaft*.085,1)]:
  wrist=center-q@Vector((0,.07,.015))
  names=[side+'Arm',side+'ForeArm',side+'Hand']
  initial_hand=pos(side+'Hand');initial_elbow=pos(side+'ForeArm');initial_rotation=bone(side+'Hand').matrix.to_quaternion().copy()
  if w>0:
   # Move through open space; blending arm rotations can sweep the wrist through the torso.
   wrist=initial_hand.lerp(wrist,w)+Vector((0,-.18*math.sin(math.pi*w),0))
   pole=initial_elbow.lerp(Vector((sign*.6,-.45,1.1)),w)
   ik(*names,wrist,pole);setrot(bone(side+'Hand'),initial_rotation.slerp(q,w))
  bpy.context.view_layer.update()
 for p in arm.pose.bones:
  if p.name in prev and p.rotation_quaternion.dot(prev[p.name])<0:p.rotation_quaternion.negate()
  prev[p.name]=p.rotation_quaternion.copy()
  for path in ['rotation_quaternion','location','scale']:p.keyframe_insert(path,frame=frame,group=p.name)
 report.append({'frame':frame,'hand_separation':(pos('LeftHand')-pos('RightHand')).length})
for layer in act.layers:
 for strip in layer.strips:
  for bag in strip.channelbags:
   for c in bag.fcurves:
    for k in c.keyframe_points:k.interpolation='LINEAR'
arm.animation_data.action=None
track=arm.animation_data.nla_tracks.new();track.name='StrongAttack';strip=track.strips.new('StrongAttack',0,act);strip.name='StrongAttack';strip.action_slot=act.slots[0]
for track in arm.animation_data.nla_tracks:track.mute=False
with bpy.context.temp_override(selected_objects=[arm]+meshes,selected_editable_objects=[arm]+meshes):
 bpy.ops.export_scene.fbx(filepath=str(source),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_nla_strips=True,bake_anim_use_all_actions=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',apply_scale_options='FBX_SCALE_UNITS')
for track in arm.animation_data.nla_tracks:track.mute=True
activate(act);scene.frame_start=0;scene.frame_end=36;scene.frame_set(13)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'spartan-axe-heavy.blend'))
(OUT/'authoring.json').write_text(json.dumps(report,indent=2))
print('HEAVY_AUTHORED',act.frame_range[:])
