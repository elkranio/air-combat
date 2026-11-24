# JUICE-02b — burning falling aircraft

Base `84d59e3` / JUICE-02a-c accepted for prototype. Keep the existing live damage trail, hit feedback, fatal shake, AI, weapons and controls unchanged.

- `AircraftWreck` copies the registered authored rigid hull mesh hierarchy and material references into a separate presentation-only root. No AI, weapons, colliders, radar contact or physics body is copied. The original Tiger is removed immediately. On player defeat the original visual root is hidden and restored at reset; the camera still follows the stationary player root, not the wreck.
- The wreck starts at the real pose and current flight speed, decelerates horizontally, banks/tumbles with randomized signed roll/yaw, and pitches downward while falling with 30 units/s² acceleration. Presentation-only RNG. Initial vertical speed 7 units/s. At Y78 over flat Y0 terrain the fall lasts roughly 2 seconds; higher terrain shortens it.
- Preserve the procedural damage point on the clone; choose a surface anchor on fatal damage if not selected already. Reuse both shared world-space smoke layers with 1.6x alpha intensity and larger particles, plus 1.8x fire rate. No new particle shadows, lights or materials. Model shadows remain as before. Unreadable meshes may be copied for visuals but are never used for procedural surface sampling.
- Small movement substeps test the terrain height at the wreck position, with 1.5 units of contact clearance. On terrain contact the wreck records the surface point and disappears; existing trail particles fade. Off-terrain fallback is Y0; no building collision for v0. Ground explosion, debris, sound and distance-sensitive shake are JUICE-02c.
- Wrecks continue stepping after victory/defeat. R and map reset clear all wrecks and residual particles, restoring the player visual. Cleanup also runs when the FX owner is disabled/destroyed. No prefab, scene or lab edits.

## Play Mode gate

1. Kill Tiger during a straight pass, turn and boost: wreck retains its pose, flies onward, tumbles and burns. Verify radar/AI disappear immediately and survivor continues combat.
2. Kill both Tigers close together; victory does not freeze the falling wrecks. Test over the plateau and open sand; wreck should disappear at terrain height, not remain underground.
3. Lose the player: confirm visual-only wreck, existing defeat message, camera stays at death location. R mid-fall and after impact restores player model, controls and clean effects. No stale wrecks after repeated R or map reset.
4. Verify normal critical trails and hit feedback remain visually unchanged; no errors or extra live aircraft from wrecks. Labs unchanged. Next JUICE-02c ground explosion.
