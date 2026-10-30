"""Build a prototype deformation rig and FBX clips for the supplied mammoth GLB.

Run inside Blender. The original is one combined mesh, so weights are spatial
approximations intended for gameplay blocking, not final production skinning.
"""
import bpy
import math
from pathlib import Path

ROOT = Path(r"C:\Users\SSAFY\pjt\bindraid")
SOURCE = Path(r"C:\Users\SSAFY\Downloads\fantasy mammoth 3d model.glb")
OUT = ROOT / "Assets/ThirdParty/MammothPrototype"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
mesh = next(o for o in bpy.context.scene.objects if o.type == "MESH")
mesh.name = "MammothMesh"

arm_data = bpy.data.armatures.new("MammothArmature")
arm = bpy.data.objects.new("MammothRig", arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = arm_data.edit_bones[parent]
    return b

bone("Root", (0, 0, .04), (0, 0, .19))
bone("Body", (0, .02, .21), (0, .02, .42), "Root")
bone("Head", (0, -.23, .35), (0, -.39, .32), "Body")
bone("Trunk", (0, -.39, .29), (0, -.43, .10), "Head")
for side, x in (("L", .14), ("R", -.14)):
    for end, y in (("Front", -.28), ("Back", .29)):
        bone(f"{end}{side}", (x, y, .20), (x, y, .025), "Body")
bpy.ops.object.mode_set(mode="OBJECT")

groups = {b.name: mesh.vertex_groups.new(name=b.name) for b in arm_data.bones}

def smooth(a, b, x):
    t = max(0., min(1., (x-a)/(b-a)))
    return t*t*(3.-2.*t)

for v in mesh.data.vertices:
    x, y, z = v.co
    weights = {"Body": 1.0}
    # Keep the heavy back armor on the body; articulate the face and trunk.
    head = 1.-smooth(-.34, -.15, y)
    head *= smooth(.19, .31, z)
    trunk = (1.-smooth(-.46, -.34, y)) * (1.-smooth(.24, .34, z))
    weights["Head"] = head * 2.8
    weights["Trunk"] = trunk * 3.6
    leg = 1.-smooth(.13, .27, z)
    front = 1.-smooth(-.08, .12, y)
    back = smooth(.12, .32, y)
    side = smooth(-.035, .035, x)
    weights["FrontL"] = leg*front*side*4.
    weights["FrontR"] = leg*front*(1.-side)*4.
    weights["BackL"] = leg*back*side*4.
    weights["BackR"] = leg*back*(1.-side)*4.
    total = sum(weights.values())
    for name, weight in weights.items():
        if weight/total > .002:
            groups[name].add([v.index], weight/total, "REPLACE")

mesh.parent = arm
modifier = mesh.modifiers.new("Mammoth Skin", "ARMATURE")
modifier.object = arm

def action(name, length, poses, loop=False):
    a = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = a
    for frame, values in poses:
        for p in arm.pose.bones:
            p.rotation_mode = "XYZ"
            rx, ry, rz = values.get(p.name, (0, 0, 0))
            p.rotation_euler = tuple(math.radians(q) for q in (rx, ry, rz))
            p.keyframe_insert(data_path="rotation_euler", frame=frame, group=p.name)
    a.use_fake_user = True
    # A strip per action gives FBX/Unity named takes.
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, a)
    strip.action_frame_start = 1
    strip.action_frame_end = length
    track.mute = True
    return a

action("Idle", 49, [
    (1, {}), (13, {"Body": (1, 0, 0), "Head": (-2, 0, 0), "Trunk": (4, 0, 2)}),
    (25, {"Body": (0, 0, 0), "Head": (0, 0, 0), "Trunk": (0, 0, 0)}),
    (37, {"Body": (-1, 0, 0), "Head": (2, 0, 0), "Trunk": (-4, 0, -2)}),
    (49, {})])
action("Walk", 33, [
    (1, {"FrontL": (15, 0, 0), "BackR": (13, 0, 0), "FrontR": (-12, 0, 0), "BackL": (-10, 0, 0)}),
    (17, {"FrontL": (-12, 0, 0), "BackR": (-10, 0, 0), "FrontR": (15, 0, 0), "BackL": (13, 0, 0), "Body": (2, 0, 0)}),
    (33, {"FrontL": (15, 0, 0), "BackR": (13, 0, 0), "FrontR": (-12, 0, 0), "BackL": (-10, 0, 0)})])
action("Telegraph", 25, [(1, {}), (15, {"Body": (-7, 0, 0), "Head": (11, 0, 0), "Trunk": (12, 0, 0), "FrontL": (-7, 0, 0)}), (25, {"Body": (-7, 0, 0), "Head": (11, 0, 0), "Trunk": (12, 0, 0), "FrontL": (-7, 0, 0)})])
action("Stomp", 31, [(1, {}), (9, {"FrontL": (0, 0, 18), "Body": (-4, 0, 0)}), (16, {"FrontL": (0, 0, -18), "Body": (9, 0, 0), "Head": (-9, 0, 0)}), (31, {})])
action("TrunkSweep", 37, [(1, {}), (10, {"Head": (0, 0, -14), "Trunk": (-8, 0, -20)}), (23, {"Head": (0, 0, 14), "Trunk": (12, 0, 27)}), (37, {})])
action("Charge", 29, [(1, {}), (9, {"Body": (-10, 0, 0), "Head": (13, 0, 0)}), (19, {"Body": (10, 0, 0), "Head": (-16, 0, 0), "Trunk": (-9, 0, 0)}), (29, {})])
action("Stagger", 41, [(1, {}), (10, {"Body": (0, 0, 12), "Head": (0, 0, -12), "FrontL": (0, 0, -9)}), (23, {"Body": (0, 0, -9), "Head": (0, 0, 10), "FrontR": (0, 0, 9)}), (41, {})])

arm.animation_data.action = bpy.data.actions["Idle"]
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "mammoth-rig.blend"))
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=str(OUT / "fantasy-mammoth-rigged.fbx"),
    use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
    bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
    axis_forward="-Z", axis_up="Y", path_mode="AUTO")
print("RIG_DONE", len(mesh.data.vertices), [b.name for b in arm_data.bones],
      [a.name for a in bpy.data.actions])
