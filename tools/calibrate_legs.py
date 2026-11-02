import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(r'C:/Users/SSAFY/pjt/bindraid/Builds/AnimationReview/VisualRework')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'locomotion-study.blend'))
a=bpy.data.objects['SpartanRig'];s=bpy.context.scene
s.frame_set(10)
result={}
for side in ['L','R']:
    c=a.pose.bones['LowerLeg'+side].constraints[0]
    scores=[]
    for deg in range(-180,181,5):
        c.pole_angle=math.radians(deg);bpy.context.view_layer.update()
        h=a.pose.bones['UpperLeg'+side].head.copy();k=a.pose.bones['LowerLeg'+side].head.copy();f=a.pose.bones['LowerLeg'+side].tail.copy()
        axis=(f-h).normalized();bend=k-h-axis*(k-h).dot(axis)
        forward=Vector((0,-1,0));forward=(forward-axis*forward.dot(axis)).normalized()
        score=bend.normalized().dot(forward)
        scores.append((score,deg,list(k)))
    best=max(scores);result[side]=best
    c.pole_angle=math.radians(best[1]);bpy.context.view_layer.update()
print('POLE_CALIBRATION',json.dumps(result))
(ROOT/'pole-calibration.json').write_text(json.dumps(result,indent=2))
