"""Extract and rig the complete warrior from the supplied multi-part GLB.

Run with Blender in background mode. This is a blocking rig for the prototype;
skin weights and clips should be refined before final character art.
"""
import bpy
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(r"C:\Users\SSAFY\pjt\bindraid")
SOURCE = Path(r"C:\Users\SSAFY\Downloads\ancient warrior 3d model.glb")
OUT = ROOT / "Assets/ThirdParty/AncientWarriorPrototype"
OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
mesh = bpy.data.objects.get("tripo_part_2")
if mesh is None or mesh.type != "MESH":
    raise RuntimeError("The complete warrior mesh tripo_part_2 is missing.")
for obj in list(bpy.data.objects):
    if obj != mesh:
        bpy.data.objects.remove(obj, do_unlink=True)
mesh.name = "AncientWarriorMesh"
mesh.parent = None

# The GLB is a sheet of character variants and loose equipment. This complete
# figure occupies one small panel, so normalize it to a 1.8 m player.
points = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
minimum = Vector(tuple(min(p[i] for p in points) for i in range(3)))
maximum = Vector(tuple(max(p[i] for p in points) for i in range(3)))
center_x = (minimum.x + maximum.x) * .5
center_y = (minimum.y + maximum.y) * .5
scale = 1.8 / (maximum.z - minimum.z)
world = mesh.matrix_world.copy()
for vertex in mesh.data.vertices:
    point = world @ vertex.co
    vertex.co = ((point.x - center_x) * scale,
                 (point.y - center_y) * scale,
                 (point.z - minimum.z) * scale)
mesh.matrix_world.identity()

# Keep the recognizable silhouette while reducing the very dense source mesh.
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
decimate = mesh.modifiers.new("Prototype decimation", "DECIMATE")
decimate.ratio = .38
bpy.ops.object.modifier_apply(modifier=decimate.name)
mesh.select_set(False)

arm_data = bpy.data.armatures.new("WarriorArmature")
arm = bpy.data.objects.new("WarriorRig", arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")

def bone(name, head, tail, parent=None):
    item = arm_data.edit_bones.new(name)
    item.head, item.tail = head, tail
    if parent:
        item.parent = arm_data.edit_bones[parent]
    return item

bone("Hips", (0, 0, .78), (0, 0, 1.03))
bone("Spine", (0, 0, 1.03), (0, 0, 1.44), "Hips")
bone("Head", (0, 0, 1.44), (0, 0, 1.76), "Spine")
for side, sign in (("L", 1), ("R", -1)):
    bone("UpperArm" + side, (sign * .20, 0, 1.38), (sign * .36, 0, 1.13), "Spine")
    bone("Forearm" + side, (sign * .36, 0, 1.13), (sign * .42, 0, .88), "UpperArm" + side)
    bone("UpperLeg" + side, (sign * .13, 0, .82), (sign * .14, 0, .43), "Hips")
    bone("LowerLeg" + side, (sign * .14, 0, .43), (sign * .14, 0, .07), "UpperLeg" + side)
bpy.ops.object.mode_set(mode="OBJECT")

groups = {b.name: mesh.vertex_groups.new(name=b.name) for b in arm_data.bones}

def smooth(low, high, value):
    t = max(0.0, min(1.0, (value - low) / (high - low)))
    return t * t * (3.0 - 2.0 * t)

for vertex in mesh.data.vertices:
    x, _, z = vertex.co
    left = smooth(-.04, .04, x)
    leg = 1.0 - smooth(.66, .99, z)
    lower_leg = 1.0 - smooth(.34, .58, z)
    arm_region = smooth(.18, .34, abs(x)) * smooth(.82, 1.08, z) * (1.0 - smooth(1.38, 1.57, z))
    forearm = 1.0 - smooth(1.00, 1.20, z)
    head = smooth(1.39, 1.57, z)
    weights = {
        "Hips": .45 + leg * .2,
        "Spine": 1.0,
        "Head": head * 7.0,
        "UpperLegL": leg * left * (1.0 - lower_leg) * 8.0,
        "UpperLegR": leg * (1.0 - left) * (1.0 - lower_leg) * 8.0,
        "LowerLegL": leg * left * lower_leg * 8.0,
        "LowerLegR": leg * (1.0 - left) * lower_leg * 8.0,
        "UpperArmL": arm_region * left * (1.0 - forearm) * 8.0,
        "UpperArmR": arm_region * (1.0 - left) * (1.0 - forearm) * 8.0,
        "ForearmL": arm_region * left * forearm * 8.0,
        "ForearmR": arm_region * (1.0 - left) * forearm * 8.0,
    }
    total = sum(weights.values())
    for name, weight in weights.items():
        if weight / total > .003:
            groups[name].add([vertex.index], weight / total, "REPLACE")

mesh.parent = arm
skin = mesh.modifiers.new("Warrior skin", "ARMATURE")
skin.object = arm

def action(name, length, poses):
    clip = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = clip
    for frame, values in poses:
        for pose_bone in arm.pose.bones:
            pose_bone.rotation_mode = "XYZ"
            angles = values.get(pose_bone.name, (0, 0, 0))
            pose_bone.rotation_euler = tuple(math.radians(v) for v in angles)
            pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=pose_bone.name)
    clip.use_fake_user = True
    track = arm.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, clip)
    strip.action_frame_start = 1
    strip.action_frame_end = length
    track.mute = True

neutral = (1, {})
action("Idle", 49, [neutral, (25, {"Spine": (2, 0, 0), "Head": (-2, 0, 0)}), (49, {})])
action("Walk", 33, [(1, {"UpperLegL": (24, 0, 0), "UpperLegR": (-24, 0, 0), "UpperArmL": (-13, 0, 0), "UpperArmR": (13, 0, 0)}),
    (17, {"UpperLegL": (-24, 0, 0), "UpperLegR": (24, 0, 0), "UpperArmL": (13, 0, 0), "UpperArmR": (-13, 0, 0)}),
    (33, {"UpperLegL": (24, 0, 0), "UpperLegR": (-24, 0, 0), "UpperArmL": (-13, 0, 0), "UpperArmR": (13, 0, 0)})])
action("Sprint", 25, [(1, {"Spine": (10, 0, 0), "UpperLegL": (38, 0, 0), "UpperLegR": (-38, 0, 0), "UpperArmL": (-25, 0, 0), "UpperArmR": (25, 0, 0)}),
    (13, {"Spine": (10, 0, 0), "UpperLegL": (-38, 0, 0), "UpperLegR": (38, 0, 0), "UpperArmL": (25, 0, 0), "UpperArmR": (-25, 0, 0)}),
    (25, {"Spine": (10, 0, 0), "UpperLegL": (38, 0, 0), "UpperLegR": (-38, 0, 0), "UpperArmL": (-25, 0, 0), "UpperArmR": (25, 0, 0)})])
action("WeakAttack", 19, [neutral, (7, {"Spine": (3, 0, -10), "UpperArmR": (-75, 0, 0), "ForearmR": (-35, 0, 0)}), (13, {"Spine": (8, 0, 12), "UpperArmR": (55, 0, 0)}), (19, {})])
action("StrongAttack", 29, [neutral, (10, {"Spine": (-12, 0, -18), "UpperArmR": (-115, 0, 0), "ForearmR": (-35, 0, 0)}), (20, {"Spine": (16, 0, 25), "UpperArmR": (70, 0, 0)}), (29, {})])
action("Dodge", 16, [neutral, (8, {"Spine": (38, 0, 0), "Head": (-14, 0, 0), "UpperLegL": (-30, 0, 0), "UpperLegR": (-30, 0, 0)}), (16, {})])
action("HitReaction", 13, [neutral, (6, {"Spine": (-22, 0, 0), "Head": (18, 0, 0), "UpperArmL": (20, 0, 0), "UpperArmR": (20, 0, 0)}), (13, {})])
action("Knockdown", 25, [neutral, (13, {"Hips": (-45, 0, 0), "Spine": (-50, 0, 0), "Head": (20, 0, 0)}), (25, {"Hips": (-60, 0, 0), "Spine": (-65, 0, 0)})])
action("Incapacitated", 31, [neutral, (16, {"Hips": (-65, 0, 0), "Spine": (-70, 0, 0)}), (31, {"Hips": (-65, 0, 0), "Spine": (-70, 0, 0)})])
action("Grabbed", 25, [neutral, (13, {"Spine": (10, 0, 15), "UpperArmL": (-35, 0, 0), "UpperArmR": (-35, 0, 0), "UpperLegL": (20, 0, 0), "UpperLegR": (-20, 0, 0)}), (25, {})])
action("GetUp", 23, [(1, {"Hips": (-65, 0, 0), "Spine": (-70, 0, 0)}), (12, {"Hips": (-28, 0, 0), "Spine": (-25, 0, 0)}), (23, {})])
action("Brace", 25, [neutral, (13, {"Hips": (8, 0, 0), "Spine": (14, 0, 0), "UpperArmL": (-28, 0, 0), "UpperArmR": (-28, 0, 0)}), (25, {"Hips": (8, 0, 0), "Spine": (14, 0, 0), "UpperArmL": (-28, 0, 0), "UpperArmR": (-28, 0, 0)})])
action("Pull", 25, [(1, {"Spine": (16, 0, 0), "UpperArmL": (-38, 0, 0), "UpperArmR": (-38, 0, 0)}),
    (13, {"Spine": (-6, 0, 0), "UpperArmL": (-55, 0, 0), "UpperArmR": (-55, 0, 0)}),
    (25, {"Spine": (16, 0, 0), "UpperArmL": (-38, 0, 0), "UpperArmR": (-38, 0, 0)})])
action("Rescue", 25, [neutral, (12, {"Spine": (30, 0, 0), "UpperArmL": (-70, 0, 0), "UpperArmR": (-70, 0, 0)}), (25, {})])

arm.animation_data.action = bpy.data.actions["Idle"]
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "warrior-rig.blend"))
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
mesh.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=str(OUT / "ancient-warrior-rigged.fbx"),
    use_selection=True, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
    bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
    axis_forward="-Z", axis_up="Y", path_mode="AUTO")
print("WARRIOR_RIG_DONE", len(mesh.data.vertices), len(mesh.data.polygons),
      [bone.name for bone in arm_data.bones], [clip.name for clip in bpy.data.actions])
