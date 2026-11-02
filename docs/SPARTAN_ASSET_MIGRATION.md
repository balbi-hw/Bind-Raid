# Spartan player migration

The previous AncientWarriorPrototype assets remain in their original directory as legacy sources. The player prefab and both presentation profiles now use ThirdParty/SpartanWarrior/spartan-rigged.fbx. The boss is unchanged by this migration.

Source: user-provided Downloads/spartan warrior 3d model.glb. The source GLB is not modified.

The Blender build script is tools/rig_spartan.py. It creates a separate scene, reduces the 19 meshes from 1,989,254 to 89,497 triangles, and assigns normalized part-aware weights to an independent skeleton with hand, foot, and cape bones. The 14 named clips retain the game's existing presentation contract. Existing combat timing and authoritative movement are unchanged.

Review files: Builds/AnimationReview/Spartan-production.blend and Spartan-animation-review.blend. In the review file, timeline markers identify each action; Space plays the sequence.

Validation: Blender walk and strong-attack pose inspection; Unity ValidateSpartan samples every exported action at five times and checks baked mesh bounds for invalid/explosive deformation. This is not a replacement for artistic review: fingers are currently driven as one hand, cape motion is bone-based rather than cloth simulation, and foot contacts are authored without terrain IK. Mesh reduction is a prototype optimization, not manual animation retopology.

Apply and build: ChainboundRaid.Editor.PrototypeAssetSetup.ApplySpartanAndBuild.
