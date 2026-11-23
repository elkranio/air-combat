# AIR-01 — live Tiger assembly and random encounter

HUD-02 and AC-01 are green at `28a7eeb3`. AIR-01 is the first combat setup, not the combat AI.

- One scene-neutral `EF01_Tiger_Fighter.prefab`: D1 chassis, shared `AircraftMotor`, matching player cruise/turn/acceleration/bank tuning, afterburner and BreakTurn copied from the accepted main-map player, same nested `AC01_Autocannon.prefab`, 20 HP and aircraft radar contact. Weapon is installed but **not triggered**.
- Main-map `Air01Encounter`: 550-unit random off-screen ring around the player's spawn, same altitude, bounded within map. Brief initial boost demonstrates the shared tank; then a passive fly-through, with boundary return steering only. No pursuit/attack/evasion yet. The parked D1 trial remains art but is no longer registered as an enemy on this radar.
- Player and enemy get live HP and hitboxes; modular projectiles ignore their own owner and matching team. Legacy labs retain their rules. Tiger damage is real; at zero HP, radar/contact/aircraft disappear with no respawn or death FX. R (and the existing player map-boundary reset) starts a fresh encounter, clears its old rounds, and resets health. Altitude comparison keeps the two aircraft at the same height.

## Install once

Apply patch and import. Open and save `ArcadeDesertE1.unity`, outside Play Mode. Menu **Air Combat > Combat > AIR-01 - Install Tiger Flight Test** creates the reusable fighter prefab and binds the encounter to E1. Commit the generated prefab, its meta, and the changed scene after testing. Do not rerun over edited assets.

## Play Mode gate

1. Play: a single hostile rim blip should appear outside 460 units, then enter radar as a heading triangle. Watch the real Tiger fly through; verify scale, bank, and initial boost. R several times: different approach bearings, always one active fighter, no parked radar ghost.
2. Follow the Tiger and hit it with AC-01; accepted hits use the existing tracer impact effect. 20 accepted one-damage hits remove it and the radar contact. No timed respawn. Verify the player cannot hit itself. Player HP remains 20 while Tiger is passive.
3. R restarts; camera, HUD, controls and altitude comparisons work. Legacy WeaponLab and GroundAttackLab still compile and play. Enemy steering/flight tuning is editable on the fighter prefab; radius on encounter.

## Approved next atoms

AIR-02: arcade attack passes (approach / attack / disengage), real shared weapon, reciprocal damage, no endless pursuit. AIR-03: delayed/probabilistic threat reactions and break-turn evasion; tune combat rhythm. AIR-04: hit/death juice, sparks, fire/smoke/explosion and possible falling wreck/debris. No VFX overhaul before combat is enjoyable.

AIR-02 implementation and current test gate: [AIR-02](AIR_02.md).
