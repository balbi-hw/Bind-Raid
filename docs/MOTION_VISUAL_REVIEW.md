# Visual motion review — 2026-11-01

## Decision

NOT APPROVED for game integration. The current production animations and the new locomotion study do not meet the requested visual standard. A successful build or finite mesh bounds is not an artistic acceptance test.

## Reference

Path of Exile 2, Warrior early-game play: https://www.youtube.com/watch?v=OGGZqTTkuzo (opened at 2:00).
The earlier endgame showcase https://www.youtube.com/watch?v=sRxhJv4wFzA was rejected as the primary motion reference because combat effects and editing obscure the body.
No exact PoE2 stride lengths or motion durations have been measured. Do not describe the current study as a reproduction of PoE2 motion.

## Verified baseline limitations

- Production rig has 17 bones, no IK constraints, no clavicles, one spine segment, no separate toe articulation, and no independent world/root control.
- Original locomotion is generated from periodic rotation functions rather than measured foot contacts.
- Dodge is 9 frame intervals at 30 fps (0.3 seconds); its main mechanism is a full pelvis rotation and per-frame height correction. That is not an authored shoulder roll with anticipation, tuck, support, and recovery.
- Gameplay reference speeds are 5 m/s walking and 7 m/s sprinting. These must be reconciled with the physical stride of a 1.8 m character. Slowing a clip alone cannot solve sliding.
- Reducing 1,989,254 triangles to 89,497 triangles via Decimate is not animation retopology.
- Split limb boundaries and shoulder volume have not passed a seam/strain acceptance test.
- The user's live Sculpt-mode session was preserved to a separate copy before review; it was not replaced.

## Isolated experiment

Builds/AnimationReview/VisualRework/locomotion-study.blend adds non-stretch leg IK, knee poles and foot rotation targets. WalkStudy.gif and RunStudy.gif show continuous cycles at 30 fps. Contact and swing phases are distinct. An initial incorrect knee-pole angle was found in rendered frames and corrected.

Rejected visually: upper-body stiffness, insufficient running drive/flight, and unvalidated shoulder/cloth behavior. The study is a diagnostic artifact, not a finished animation. It has NOT been exported into Assets or installed into the game.

## Required acceptance gates

1. Rig: root/pelvis/chest/clavicles, arm and leg controls, toe/heel control; anatomically aligned joints; scale locked to one; no stretch-enabled IK.
2. Skin: continuous arm raising and elbow flexion, squat and twist; inspect shoulder/armpit volume, wrist rigidity, knee shape, split seams and cape interference. Inspect both neutral and lit views.
3. Locomotion: inspect side/front/game camera, normal speed and half speed. Support foot must stay fixed in moving-world playback; heel/toe contact, pelvis transfer and arm counter-rotation must be visible. Run must have convincing support and flight rather than faster walking.
4. Dodge: anticipation, shoulder-led tuck, protected head, foot plant and recovery; no floor penetration or floating pivot. Determine an aesthetically acceptable duration before fitting gameplay timing.
5. Transitions: idle/walk/run start-stop, turns, dodge entry/exit, Q/F stance transitions. Test in the actual game camera using live inputs, not isolated clips alone.
6. Integration only after visual review passes; then verify speed/stride matching and multiplayer presentation. Keep comparison renders and explain remaining limitations.

## Remaining work

### Leg correction follow-up

The prior statement that the knee-pole direction was corrected was insufficient: both legs used the same angle despite mirrored rest-bone axes. A measured sweep found forward-facing pole angles near 5 degrees (L) and 175 degrees (R). The isolated LegFix study applies those independently and checks the forward knee-plane direction on every walk/run frame. Front and side renders and regenerated GIFs are stored under VisualRework/LegFix. This fixes the observed reverse-bending direction in the study; it does not approve the complete locomotion or change the game assets.

Reference frame analysis is incomplete; a complete rebuilt rig, acceptable walk/run/roll, and live-game visual validation remain outstanding. Do not report this review as task completion.
