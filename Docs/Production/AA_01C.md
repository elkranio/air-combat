# AA-01c — Mantis combat

## AA-01e1 — cold launch (current)

Base `f24ee9a`. Keep Attack Mode = Vertical Unguided; no scene migration. One centered temporary canister, one rocket per 2.5s cycle. Flak and Tracers unchanged. New Inspector timings replace old rise speed and alignment fields (no stale serialized .16s alignment): ejection .48s, turn .32s, ignition acceleration .22s. Fixed launch-time flight plane, 240 units/s final speed, 310 range, 3 HP physical hit. Silent until dedicated rocket audio pass.

- Ejection decelerates vertically with a short launch smoke puff, no sustained main exhaust. The turn completes the climb while the body rotates with visible side jets and a small horizontal drift. Ignition snapshots current player XZ, accelerates smoothly along that course, and enables the main exhaust/smoke. No further tracking, no lead, no damage before ignition, no splash damage.
- Play gate: inspect continuity at climb/turn/ignition, one launch per cycle, no Flak audio, fixed straight heading after ignition, range expiry, reset and mode switching. Compare multiple independently placed units after the single-unit read is accepted. NURS, radar and patrol unaffected.

## AA-01e — vertical unguided experiment (historical)

Base `53f907f`. Flak was mechanically playable but the stationary airborne markers read as ground objects under the oblique camera. Preserve Flak and Tracers; in Edit Mode select the existing Mantis vehicle wrapper (MantisGun), set **Attack Mode = Vertical Unguided**, save and Play. No installer or scene migration. New components default to Vertical Unguided; existing serialized mode is deliberately preserved until changed. Runtime-only twin vertical pod replaces the gun meshes in this mode; no art/prefab changes.

- Two independent launches, .35s apart, 2.5s pause after the second. Each rises vertically at 180 units/s to the launch-time aircraft height; turns over .16s, aiming at the player's current XZ position until commitment. No lead. Then 240 units/s, fixed horizontal heading, 310 units of travel. Only the committed phase deals damage (3 HP on swept physical hit); expiry/impact puff is harmless. No UI markers or seeking after commitment.
- Visible hull, smoke trail, distinct boost ignition, launch and expiry sound placeholders. Existing radar ground contact, movement, reset/unbind and NURS remain unchanged. No extra missile contact on radar in this experiment. Test on a fixed altitude; changing flight altitude mid-launch is outside the current test.
- Play gate: verify both rockets originate from the pod, rise to the aircraft plane, turn independently, fly straight after ignition, expire without splash damage, and clear on encounter reset/mode change. Try different approach angles and speeds; tune readability before lethality. Flak/Tracers remain selectable.

## AA-01d — Flak experiment (current)

Base `9c7d514`. No reinstall or scene changes needed. Existing MantisGun defaults to Attack Mode = Flak; select Tracers before Play for the original AA-01c. Same patrol, servo, ground mode, radar, HUD and reset lifecycle.

- Two charges from left/right sockets, .15 s apart. Snapshot a pair of fixed points at player flight height using the initial velocity estimate; separation 18 units across the flight path. Second charge does not retarget. .25 s visible rise, .5 s stationary fuse, 2.5 s pause after second launch. All exposed on MantisGun.
- Glowing charge and fixed-radius world-space aura, not HUD. Radius 12 units. At detonation test player center against the exact 3D sphere once, 2 HP per charge. No contact damage in transit, no lingering damage, no homing. This experimental effect does not test terrain obstruction.
- Flash and exact-radius fading ring, radial sparks, dark fading smoke and procedural shot/blast sounds. Existing player damage feedback remains active. Fired/hit counters count charges/damaging detonations. Reset/unbind clears all charges and their audio.
- Play gate: observe rise and warning, evade both charges in cruise and S/W, verify no hits outside aura at detonation, verify R and mode switch, compare Tracers. Tune fuse/radius/separation before damage/destruction work.

## AA-01c (historical tracer mode)

Base `fb379b5`: rounded patrol turns accepted. Mantis keeps its route and existing visual sockets. This atom tests the threat, not ground damage. The legacy AA1 lab is unchanged.

In Edit Mode open `ArcadeDesertE1`, choose **Air Combat > Mission > AA-01c - Arm Mantis patrol**, save scene. The command equips each Mantis patrol motor with a `MantisGun`, binds the existing turret/pitch/muzzle transforms, preserves settings on rerun and selects the unit. Ground Nurs Test remains enabled. In Play, Air01Encounter binds scene guns only in that ground mode; the optional Tiger/AAM mode does not activate the Mantis guns. Existing NURS and patrol authoring are unchanged.

## Defaults (all tuning on MantisGun)

- Horizontal detection and radar danger radius: 210 units. Independent chassis/servos, yaw 240 deg/s and elevation 180 deg/s; max elevation 85 degrees. Track predicted player position between bursts, no continuous target correction within a burst. Actual barrels fire in pairs; snapshot barrel axis, then coherent drift up to 3 degrees yaw and 1.05 degrees pitch, plus small per-round spread .65 degrees. A sharp chassis movement outside the firing corridor interrupts the burst instead of emitting wildly misdirected rounds.
- Seven pairs (14 physical rounds), .085 s nominal interval, ~.51 s burst at sufficiently high framerate, random 1.25–1.8 s pause. Speed 190, maximum travel 340, tracer length 13. Straight arcade trajectories, no homing. Swept raycast against real colliders; only the bound player's damage receiver takes damage (1 HP). Obstructions stop a round. No area damage. No damage to Mantis yet.
- Orange tracer with readable halo while below combat height; brightens to warm white within 13 units of player flight height. Rising round crossing player height +5 expires after collision check; short .22 s expanding air puff (also on impact/range expiry). Harmless expiry, no proximity damage.
- H debug shows AA hits/fired and currently bursting gun count. Ground mode now handles player death via existing flight disable/wreck flow without declaring victory simply because Tigers are absent. R and map reset restore player HP, patrol start poses, AA stats, gun cooldowns and clear all their in-flight rounds. No ground damage or custom sound in this atom.

Play gate: compare yaw responsiveness during W/S/A/D, watch two nearly parallel streams and pauses, dodge a committed burst, verify near-height brightness and expiry, take a real hit, check HUD/radar, lose and R restart. Check no AA fires when ground mode is disabled. Test obstruction and route turn. Next AA-01d tuning then NURS ground damage and destruction juice.
