# Player weapon visuals

The supplied textured axe is converted from `C:/Users/SSAFY/Downloads/ancient-warrior-axe.glb.glb` to `Assets/ThirdParty/AncientAxe/ancient-axe.fbx`. Its handle is aligned vertically, the grip is the model origin, and its full height is 0.65 m. Base color and normal textures are preserved as separate PNG assets. The source file is unchanged.

Both player appearances carry a `RaidWeaponAttachment` on their character root. Its `hand` references `mixamorig:RightHand`; the weapon is a rigid child of that bone and follows all animation without changing skin weights. Imported armature scale is compensated so weapon dimensions and grip offsets use model metres.

Default equipment: `Assets/Settings/AncientAxeWeapon.asset`. Model/material prefab: `Assets/ThirdParty/AncientAxe/AncientAxe.prefab`.

To add a weapon, create a prefab with the grip at its origin, then use Create > Chainbound Raid > Weapon Visual. Assign a stable weapon ID, prefab, grip position, grip rotation and scale. `RaidWeaponAttachment.Equip(definition)` replaces the visual; `Equip(null)` removes it. The player asset setup reapplies the default axe when regenerating character appearances.

This is a visual equipment layer, not an inventory or weapon-stat system. The existing server-authoritative damage, hit detection and attack animations remain unchanged. Future runtime loadout selection needs an authoritative replicated weapon ID/catalog, plus weapon-specific attacks and ranges. Calling Equip on one client alone does not replicate a selection to other clients.

The weapon definition also sets a finger curl applied after character animation, with the thumb left animated. Finger transforms are cached to avoid allocating an array each rendered frame. The prefab wrapper preserves the FBX axis conversion independently of the editable grip rotation.

Review output is under `Builds/AnimationReview/WeaponEquipment`; Unity setup/build log is `Builds/weapon-equipment.log`. Both appearances, rigid attachment across 18 samples in six clips, and replacement/removal passed, followed by a successful Windows build. Idle, attack, sprint and dodge images were inspected. These checks do not establish full collision-free weapon motion, and the original animations were not replaced with weapon-specific motion.

## Locomotion carry correction

The initial Sprint pose rotated the axe into/behind the torso. The axe definition now enables a carry pose: after the locomotion graph evaluates, the right upper arm rotates to keep the hand farther out and forward, and the wrist rotates the weapon shaft toward the character-space carry direction. Rotations preserve bone lengths, animated elbow bend and all lower-body animation. The pose blends by displayed speed between 0.2 and 3 m/s; it is disabled for attack, roll, brace, pull, rescue and get-up clips. Finger curl follows the carry adjustment.

`useCarryPose`, `carryDirection`, `minimumHandSide` and `minimumHandForward` are per-weapon settings. The current correction assumes the right hand's parent is the forearm, its grandparent is the upper arm, and the weapon prefab's +Y axis follows its shaft. It is suitable for the present one-handed axe; two-handed weapons need their own pose/IK design.

Follow-up output: `Builds/AnimationReview/WeaponCarry`, with front and rear locomotion previews. `Builds/weapon-carry.log` records the successful Windows build and 72 sampled poses across six clips. Checks confirm unchanged joint translations, untouched unrelated rotations, stable grip, and the desired Sprint shaft direction. This is not a full mesh-intersection proof or a live multiplayer playtest.
