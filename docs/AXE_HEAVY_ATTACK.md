# Two-handed axe heavy attack

Original authored motion using Dark Souls III Battle Axe as the style reference: gather the off hand, lift the axe, commit to a downward diagonal strike, follow through and recover. This is not an extracted game animation or a frame-identical reproduction.

Blender source: `Builds/AnimationReview/AxeHeavy/spartan-axe-heavy.blend`. Authored at 24 fps, frames 0–36 (1.5 seconds), on the existing Tripo skeleton. The previous FBX and Blender scene are backed up in the same directory. Only the StrongAttack take is replaced; the existing locomotion and roll tracks are retained.

The right hand drives the axe. The left hand blends onto the weapon's support grip during frames 0–6, follows it through frame 29, then releases through frame 36. A two-bone rotational solve maintains support contact in Unity; it never changes joint translations or scales. Finger curl and the support grip are weapon settings. The standalone Blender action contains the authored two-hand pose, while final grip alignment is applied in Unity.

Gameplay timing in CommonWeaponSettings: windup 0.625 s, active 0.167 s, recovery 0.708 s. Free movement input is suppressed during the heavy attack. The authored lunge moves the controller up to 1.1 m along the committed attack heading from 0.3333 to 0.75 s, using a smooth distance curve. Each physics step adds only its interval of that curve; the attack state timer also supports prediction replay. The displacement passes through the existing chain constraint and CharacterController collision handling, so walls/constraints can shorten it. The short airborne hop is presentation motion, not obstacle-clearing physical jumping.

The revised legs crouch, lift into a brief bound and land in a staggered stance; the torso twists farther into the swing before the axe is recovered. Follow-up input opens at 1.25 s; dodge cancellation opens at 1.3 s. Damage and stamina cost remain 55 base power, 45 part power, 15 stamina. Hit detection remains the existing forward capsule, not an axe-mesh collision trace.

Review: `Builds/AnimationReview/AxeHeavy/Unity` contains samples across six clips, plus rear views. Validation checks clip/gameplay duration agreement, recovery, bone translations, support-hand error, carry pose regression and replacement/removal. Combat and movement rule checks run through WeaponAssetSetup.ReviewOnly. Full mesh-intersection coverage and live network playtesting are not claimed.

The creator script is `tools/create_axe_heavy.py`; the Unity review uses the same runtime attachment methods as gameplay. Preview GIF: `Builds/AnimationReview/AxeHeavy/strong-attack.gif`.

Lunge revision log: `Builds/axe-heavy-lunge.log`. Checks include integrated distance at 30/50/100 simulation steps per second and a CharacterController wall collision. Preview captures use a stationary camera, ground and the same distance curve to expose forward travel instead of following the character. Live multiplayer prediction/reconciliation under latency has not been exercised.

## Torso penetration correction

The previous grip constraint preserved contact but did not prevent the arms from crossing the torso. The authored palm orientation put the wrists on opposing sides, and rotation interpolation during release swept the left forearm inward. Corrected palm orientation, forward grip positions, reduced torso flexion, and outward/forward elbow poles now keep the arms in front. Gather/release interpolate wrist targets along a forward arc rather than blending the arm joint rotations across the body; the support-hand solve uses a matching forward transition arc.

The follow-up captures include front, rear and side views. `Unity/torso-clearance.csv` records wrist/elbow distances to the Hips–Spine2 centre segment plus character-local coordinates. This is a diagnostic approximation, not mesh-level collision detection. `Builds/axe-heavy-clearance.log` contains the matching review/build result. The lunge distance and recovery timings are unchanged by this correction.
