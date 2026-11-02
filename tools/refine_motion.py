"""Refine existing prototype actions without changing take names or rig topology.
Run using Blender --background --python tools/refine_motion.py.
"""
import bpy
import math
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BACKUP = ROOT / 'Builds/motion-backup'
BACKUP.mkdir(parents=True, exist_ok=True)

def curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves

for folder, blend, fbx, rig in [
    ('AncientWarriorPrototype', 'warrior-rig.blend', 'ancient-warrior-rigged.fbx', 'WarriorRig'),
    ('MammothPrototype', 'mammoth-rig.blend', 'fantasy-mammoth-rigged.fbx', 'MammothRig'),
]:
    directory = ROOT / 'Assets/ThirdParty' / folder
    for filename in (blend, fbx):
        backup = BACKUP / filename
        if not backup.exists():
            shutil.copy2(directory / filename, backup)
    # Always derive from the original, so repeated runs are deterministic.
    bpy.ops.wm.open_mainfile(filepath=str(BACKUP / blend))
    arm = bpy.data.objects[rig]
    warrior = rig == 'WarriorRig'
    for action in list(bpy.data.actions):
        arm.animation_data.action = action
        if action.slots:
            arm.animation_data.action_slot = action.slots[0]
        end = int(action.frame_range[1])
        if action.name in ('Walk', 'Sprint'):
            for curve in list(curves(action)):
                curve.keyframe_points.clear()
            for frame in range(1, end + 1):
                phase = 2 * math.pi * (frame - 1) / (end - 1)
                for bone in arm.pose.bones:
                    bone.rotation_mode = 'XYZ'
                    bone.rotation_euler = (0, 0, 0)
                def rotate(name, x=0, y=0, z=0):
                    arm.pose.bones[name].rotation_euler = tuple(math.radians(v) for v in (x, y, z))
                if warrior:
                    run = action.name == 'Sprint'
                    for side, offset in [('L', 0), ('R', math.pi)]:
                        p = phase + offset
                        swing = math.cos(p)
                        lift = max(0, math.sin(p)) ** 2
                        rotate('UpperLeg' + side, (34 if run else 22) * swing)
                        rotate('LowerLeg' + side, -(8 + (58 if run else 38) * lift))
                        rotate('UpperArm' + side, -(24 if run else 12) * swing)
                        rotate('Forearm' + side, -(48 if run else 18) - 8 * lift)
                    rotate('Hips', z=3 * math.sin(phase))
                    rotate('Spine', 8 if run else 2, z=-4 * math.sin(phase))
                    rotate('Head', -4 if run else -1)
                else:
                    # Four staggered contacts instead of synchronized diagonal rocking.
                    for name, offset in [('BackL', 0), ('FrontL', .5), ('BackR', 1), ('FrontR', 1.5)]:
                        rotate(name, 11 * math.cos(phase + math.pi * offset))
                    rotate('Body', .6 * math.sin(2 * phase), z=.8 * math.sin(phase))
                    rotate('Head', -.8 * math.sin(2 * phase - .3))
                    rotate('Trunk', 2 * math.sin(phase - .5))
                for bone in arm.pose.bones:
                    bone.keyframe_insert('rotation_euler', frame=frame, group=bone.name)
        for curve in curves(action):
            for key in curve.keyframe_points:
                key.interpolation = 'BEZIER'
                key.handle_left_type = 'AUTO_CLAMPED'
                key.handle_right_type = 'AUTO_CLAMPED'
        assert all(math.isfinite(k.co.y) for c in curves(action) for k in c.keyframe_points)
        print('REFINED', rig, action.name, tuple(action.frame_range))
    arm.animation_data.action = bpy.data.actions['Idle']
    bpy.context.scene.frame_set(1)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(directory / blend))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        if obj.type in {'MESH', 'ARMATURE'}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=str(directory / fbx), use_selection=False,
        object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        axis_forward='-Z', axis_up='Y', path_mode='AUTO')
    print('REFINEMENT_DONE', rig)
