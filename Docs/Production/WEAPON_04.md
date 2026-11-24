# WEAPON-04 — independent primary / secondary slots

Base `0675463`: player AAM proof accepted, acquisition tuned to 150 ms. No flight, damage, cannon cadence or missile balance changes.

- Main encounter explicitly equips the authored AC-01 BURST primary (LMB) and runtime AAM launcher secondary (RMB). No wheel selection. Both slots tick independently each frame, including released trigger state. Continuous AC-02 is disabled only on this live player instance; its assets and implementation are unchanged. Legacy lab input paths stay unchanged.
- AAM acquisition is always active while ammo is available, even while the cannon fires. Fresh RMB press with full lock fires once; LMB cannot launch. Same 150 ms acquisition, 0.5/1s grace, beep, two rounds, 20s full-magazine rearm only after the second shot, independent in-flight missiles. No lock or beep while empty. Both slots cease on player trigger disable/defeat; R resets the encounter as before.
- The existing bottom status bar expands at runtime in dual mode. HP/BOOST stay left; equally wide primary and secondary blocks show LMB/RMB and actual weapon state. AAM launcher and guided missile are separate labels with 2/2 or 1/2 green magazine fill; 0/2 shows amber rearm progress and seconds remaining. The magazine HUD reads the weapon's own state, not an estimated timer. No scene/prefab edits.
- Future slot selection/loadout UI and gamepad mapping are deferred. Cannon finite resource is a hypothesis, not an implemented balance change.

## Play Mode gate

1. Open ArcadeDesertE1 and Play: both weapon blocks visible, HP/BOOST readable at your resolution. LMB fires bursts only, RMB with full lock launches only; wheel does nothing. No AC-02 fire. Lock while firing cannon, launch with RMB while holding LMB, verify both remain independent.
2. Hover a Tiger: 150 ms lock and beep; move away/offscreen: existing grace, RMB can launch during full grace. Empty magazine: no target frame or beep, amber countdown. One round remaining after >20s stays 1/2; second launch begins 20s rearm, restores both together. Holding RMB must not automatically launch on a later lock.
3. R during lock, missile flight and rearm; check full magazine, reset sounds/particles, no orphan projectile. Check defeat disables both triggers. Inspect Console and regress WeaponLab / GroundAttackLab. Next: enemy AAM pacing, warning and evasion.
