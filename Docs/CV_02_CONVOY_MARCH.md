# CV02 — straight convoy sandbox

Base `f05931d`. CombatSandbox now defaults to Convoy (enum 4; existing mode IDs unchanged). Six instances of the accepted SAM01_Picket_Mobile visual, no launcher or damage components. March pose only; no firing or deployment. Existing authored Warden and Mantis trials are hidden at runtime in this mode. No Mission01 changes.

- Play directly, no scene install step. Player uses AC-01 only. H shows convoy progress, R resets the six vehicles, pose and travel without accumulating contacts or instances.
- Inspector on CombatSandboxEncounter: Convoy Start is the initial head position (XZ), Convoy Yaw is world yaw, Convoy Spacing is center-to-center, Convoy Speed and Convoy Distance control straight travel. Initial values (-290, -100), 0 degrees (+Z), 30 units, 12 units/s, 650 units. Vehicles stop together at the end without deploying. Each samples the authored terrain, not separate plateau/building meshes; route is a provisional open-ground test and may need repositioning after visual inspection.
- Radar shows six ground contacts with zero threat radius. The vehicles are intentionally indestructible for now. No wheel rotation (wheel geometry is baked). Next atom adds two independent Warden escorts following the convoy, then ground damage/NURS and deployment/arming.
- Local gate: compile, open CombatSandbox and Play, find the convoy using radar (initial head is north-east of spawn), inspect spacing, ground contact and march pose. Verify no duplicate vehicles or radar contacts after R and after stopping. Switch back to Warden, Dogfight, GroundAttack and Interception before Play and check their original loadouts/behavior. Verify Mission01 is unchanged. Unity Play Mode has not been run in the patch environment.

## CV02B — wheel dust

Mobile Picket has a reusable `GroundMovementDust` component: two rear-wheel emission points, short world-space dust puffs, distance-based density with independent random variation, no emission while stationary. R clears particles after repositioning. Tune Meters Per Puff, Puff Size, Puff Lifetime, Dust Color (including alpha) on the mobile prefab. Surface Dust defaults to 1 (sand); road detection is deferred, and `SetSurfaceDust` supports reducing it later. Verify movement, stop/fade, R, six overlapping trails and readability at actual camera zoom. No scene, old Picket, Warden or weapon changes.
