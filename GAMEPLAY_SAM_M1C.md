# SAM1c — launch visibility and failure diagnosis

Base: green SAM1b (vertical SAM install/model), no one-time authoring required.

- Still 350-unit X/Z acquisition, 1.4-second lock, 7-second reload, 112 speed, 145-degree pursuit budget and the independent `SurfaceToAirMissile` prefab. No A2A missile or radar changes.
- **H** now shows both `Missiles N` (launches counted) and `SAM last:` with the latest flight result (spawned, blocked by named collider, hit player, or expired). A launch also writes a single Unity Console `Log` with launch height and target coordinates. An obstacle termination writes a `Warning` with its collider and flight age. There should be no silent launch-frame disappearances.
- Cold-launch clearance: physical world collision tests on SAM missiles begin after the initial vertical ejection (0.34s), avoiding immediate interception by nearby world geometry. Owner colliders remain excluded. Player impact is still checked during launch; obstacles are checked during pitch-over and pursuit.

## Test

1. Enter GroundAttackLab. Open debug with **H**, then approach the SAM and remain within its 350-unit ring for at least 1.4s. Watch `Lock` progress and `Missiles` count. If `Missiles` remains 0, the lock never completed. If the count increments, Unity Console logs the actual launch coordinates.
2. On launch, watch the amber incoming glyph on radar and the physical missile rising from the silo. If it disappears, read `SAM last:` and any Console warning; this isolates early collision vs lifetime vs player impact.
3. Repeat with 70 cruise, W, and Q/E. Check AA1, HP/fuel bar, bombs, NURS, reset R, WeaponLab and Sandbox regression.

No Unity Play Mode run was available during patch authoring.
