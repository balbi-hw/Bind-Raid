# Tripo Spartan replacement — 2026-11-02

## Input and preservation

- Supplied `C:/Users/SSAFY/Downloads/spartan warrior 3d model with additional work.glb`.
- 19 character mesh parts, 14,770 polygons, 65 Mixamo-named bones, `walk` and `run` actions.
- The original file is unchanged. A copy and pre-replacement Unity configuration are in `Builds/AnimationReview/TripoReplacement` (`original-supplied.glb`, `BeforeReplacement/`).
- Previous model remains in `Assets/ThirdParty/SpartanWarrior`. Active source is now `Assets/ThirdParty/SpartanTripo/spartan-tripo.fbx`.

## Changes

- Retained the supplied skeleton and body skin. No replacement skeleton or paid generation.
- Copied walk/run joint curves as Walk/Sprint; recentered horizontal placement and added 0.013 model units of ground clearance. Source timing remains 24 fps (2.333 s walk / 1.25 s run).
- Cape weights connected to arms/shoulders/neck were consolidated onto Spine2 to prevent arm-driven stretching. The cape has no cloth simulation and remains relatively stiff.
- Added a 0.75 s forward roll (Dodge): anticipation, tucked rotation, recovery, ground-height correction. Arms aim forward; spine/neck flex and slight lateral tilt are included. This is authored keyframe animation, not captured PoE2 motion.
- Added breathing Idle. Retargeted ten previous secondary actions for compatibility. Those secondary actions are not newly supplied Tripo clips and should not be considered artistically approved.
- Preserved 19 separate basecolor/normal maps and assigned URP materials. Variant B changes cape tint only.
- Explicit four-bone skin quality prevents lower quality tiers from dropping influences. Thin material surfaces render both sides. Unity calculates smooth normals/tangents. Normal maps are retained but disabled in the current matte material (metallic 0, smoothness 0.1), since the detailed lit surface did not match the Blender preview. Diagnostic UV inversion and unlit-material experiments were removed. Atlas mipmaps/compression are disabled to avoid color bleeding between densely packed UV islands; this alone did not eliminate the visual difference.
- Disabled legacy procedural body bob/lean for this presentation to avoid adding motion on top of authored clips.
- FBX units are exported as metadata (`FBX_SCALE_UNITS`) and Unity uses file units. The first validation caught an object-scale animation mismatch; this export change fixes it.
- Dodge duration changes from 0.3 to 0.75 s; speed changes from 9 to 3.6 m/s, preserving the nominal 2.7 m travel distance. This also changes the time spent in the gameplay Dodge state; combat balance needs user review.

## Review artifacts

In `Builds/AnimationReview/TripoReplacement/`:

- `spartan-tripo-final.blend`: editable rig and all actions; open on Dodge frames 0–18.
- `Walk.gif`, `Sprint.gif`, `Dodge.gif`: fixed-camera previews.
- `roll-contact-sheet.jpg`: sampled roll poses.
- `validation.json`: full-frame Blender checks. Weight sum errors: 0. Maximum bone-length deviation under 0.000005 relative. Minimum surface height: Walk 0.00215 m; Sprint 0.00131 m; Dodge 0.01110 m.
- `Unity/`: captures and independent imported-clip validation.
- Unity captures render CPU-baked sampled meshes. Repeated `Camera.Render` calls in a single editor callback had reused stale GPU skin matrices, showing incorrect arm poses; this was a preview capture issue, and the corrected captures show the tucked arms. These are imported-pose captures, not gameplay screenshots.
- `unity-final.log`: final Unity application, validation, and build output.

## Limits

Bone-length and finite-geometry checks do not establish artistic quality. The roll is a reviewable first authored version. Cloth dynamics, final weapon-hand contact, the Unity surface appearance, and all secondary combat actions require further visual refinement. Imported motion was inspected using Blender MCP frame views and rendered frames; Unity captures check the game import. These are distinct from a full interactive multiplayer gameplay review.
