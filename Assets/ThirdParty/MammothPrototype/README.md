# Fantasy mammoth prototype model

Source: user supplied `C:/Users/SSAFY/Downloads/fantasy mammoth 3d model.glb`.
The original GLB contains one mesh, three textures, no skeleton, and no
animation clips. `fantasy-mammoth.fbx` was converted with Blender 4.5.15 and
decimated from 501,124 to 125,280 triangles for this prototype. The base
color and normal textures were extracted without modification. The
metallic/roughness texture is retained but is not currently used by the URP
material.

The source file did not include license metadata. Confirm its usage rights
before distributing this art outside the project team.

## Prototype rig and animation

`mammoth-rig.blend` is the editable Blender 5.1 rig. The active Unity model is
`fantasy-mammoth-rigged.fbx`. It has eight bones (root, body, head, trunk, and
four legs) and seven clips: Idle, Walk, Telegraph, Stomp, TrunkSweep, Charge,
and Stagger. The scene uses Idle, Telegraph, attack clips, and Stagger; Walk is
prepared for later boss locomotion. ChainSnatch and SpinGrab currently reuse
TrunkSweep as a temporary visual.

The original GLB is a single combined mesh without separate armor, cloth, or
body parts. Spatial weights are approximate. The rig is suitable for blocking
gameplay and timing, but feet can slide and armor/cloth can deform during larger
motions. Collision, hit timing, and attack ranges still come from game logic,
not animation events. Refine the skinning and make dedicated ChainSnatch and
SpinGrab clips before final art.

The reproducible build script is `tools/rig_mammoth.py` at the repository root.
It reads the original user-provided GLB from Downloads; update `SOURCE` if it
has moved. Run it through Blender MCP or Blender's Python console to regenerate
the `.blend` and FBX files. The Unity editor menu **Chainbound Raid > Apply
Temporary Characters** reconnects the model and animator in the scene.
