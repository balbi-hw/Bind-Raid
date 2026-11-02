# Ancient warrior prototype player

The project owner supplied their own `ancient warrior 3d model.glb` on
2026-10-31 and confirmed that it may be included in tester builds shared
outside the project. This does not grant others a separate right to reuse it.

The GLB is a sheet of 22 meshes: several complete miniature figures and loose
equipment. `tools/rig_warrior.py` extracts the complete `tripo_part_2` figure,
normalizes it to 1.8 m, reduces it from 68,514 to about 26,000 triangles,
adds an 11-bone prototype rig and 14 blocking animation clips, and exports
`ancient-warrior-rigged.fbx`. `warrior-rig.blend` is the editable source.

The source GLB has no texture images or animation rig. The two players use
different Unity materials for quick identification. The current weights and
animation clips are spatial approximations; refine skinning, cloth movement,
hands, attack silhouettes and equipment attachment before final art.

The rig and visible mesh are children of the original network player prefab.
Server movement, collision, hitboxes, chain anchors and camera logic remain
on the existing gameplay root.
