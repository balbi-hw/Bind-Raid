# Locomotion stutter investigation — 2026-11-02

## Findings

Movement advances at 50 Hz. The old visual code derived animation speed from each render frame's position delta and capped it at 8 m/s. Fixed-step movement produced alternating zero/large samples: a true 5 m/s movement averaged 2.778 m/s at 144 fps and 1.667 m/s at 240 fps in the reproduction. Local and host presentation also lacked interpolation between physics steps.

The supplied Sprint clip had an endpoint mismatch of approximately 13 cm at a foot (14 cm at a hand). Walk's seam was already small. Planted-foot velocity measurements in Blender estimated source gait speeds of 1.0054 m/s (Walk) and 3.2441 m/s (Sprint).

## Changes

- Measure horizontal speed from actual controller displacement per simulation step, excluding vertical motion. Remote players use snapshot displacement over simulation ticks.
- Interpolate local/host presentation between physics samples; the authoritative controller is unchanged. Camera and model share the existing presentation offset. This adds one physics step (20 ms) of presentation latency.
- Advance Walk/Sprint with a shared gait phase and measured source-speed references. At the unchanged gameplay speeds of 5 and 7 m/s, Sprint is used at approximately 1.54x and 2.16x respectively.
- Correct Sprint endpoint residuals in Blender, export the existing FBX, and preserve the other clips. Original input GLB is untouched. Backups are under Builds/AnimationReview/LocomotionFix.

## Validation

Unity batch validation: 30/60/144/240 fps simulated presentation retains 5.000 m/s; maximum interpolated position error is below 0.00006 m. Teleport reset, reconciliation rebase and exclusion of vertical movement passed. Existing movement motor regression checks passed. Gait poses agree within 0.1 degrees after two seconds at 30/60/144 fps.

Imported clip seam gaps: Walk 0.000911 m, Sprint 0.000085 m. Blender frame inspection covered Sprint frames 27, 29, 0 and 2. See Builds/AnimationReview/LocomotionFix/validation.txt and unity-validation.log for test/build output.

These checks reproduce and address animation/presentation timing problems. They are not a runtime GPU/CPU frame-time or network-latency profile, nor a full multiplayer reconciliation playtest. A remaining hitch in actual play requires profiling that session. Faster Sprint playback matches displacement but does not constitute a newly authored high-speed sprint motion.

## Follow-up refinement

After the user confirmed a substantial improvement with some remaining stutter, inspection found that rotation still inherited the controller's discrete 50 Hz updates. The model now interpolates rotation over the same physics interval as position. This preserves gameplay facing and attack rules while smoothing presentation, including transitions across 360 degrees. Procedural model rotation is stored separately so the interpolation correction cannot feed back into its smoothing. Reset and reconciliation rebase also handle rotation.

Camera obstruction queries now reuse a hit buffer instead of allocating a new array every rendered frame. A full buffer is expanded and queried again to preserve all obstruction results. This removes that recurring allocation; its contribution to observed hitches has not been measured.

The follow-up validation adds a 90 degrees/second turn at 30/60/144/240 fps, including wraparound, and checks rotation reset/rebase. The follow-up Unity log is Builds/AnimationReview/LocomotionFix/unity-refinement.log. Actual play testing remains separate from these deterministic checks.
