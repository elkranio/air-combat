# Gameplay architecture — active migration map

**Status:** incremental extraction from the tested WeaponLab prototype. No behavior redesign. The accepted combat scene is still `Assets/_Project/Scenes/WeaponLab.unity`, and `Sandbox.unity` remains the only build scene. Keep both intact while migrating. Source base: `master` `03e1781`.

## Ownership and dependencies

| Area | Reusable runtime owner | Remaining Lab-specific wiring / work |
| --- | --- | --- |
| Flight | `FlightController`, `EnemyFlightController`, `AircraftMotor`, `Afterburner`, `AfterburnerVfx`, **`BreakTurn`** | `WeaponLabSession` temporarily installs player/enemy `BreakTurn` and links controllers; `WeaponLabEvasion` is still test AI |
| Camera | `TopDownCameraFollow`, **`CameraLookAhead`** | Session enables look-ahead and disables pitch hotkeys (Lab uses authored 65-degree pitch); Sandbox retains pitch keys |
| Guns | `WeaponLabCannon`, `WeaponLabProjectile` (not migrated yet) | Single Lab component still owns BURST/GAUSS cadence, selection, audio and guides; extract next, without changing values |
| Aim and lock | `WeaponLabFreeAim`, `TargetLock`, `TargetLockHud` (partial) | Lab aim and lock configuration still reference `WeaponLabEnemy`; split generic targeting from opponent-specific validation next |
| Missiles | `MissileProjectile`, `MissileLauncher`, `WeaponLabMissileLauncher` | Shared projectile exists; Lab launcher adapts one-hit damage to 5-HP sparring enemy |
| HUD | `FlightHud` (Sandbox), `TargetLockHud`, `EnemyIndicator` (Sandbox), Lab diagnostic `OnGUI` | Status bar and bearing arrow are not yet general scene prefabs; Radar is **not** specified |
| Scene orchestration | `WeaponLabSession` | Owns test-only respawn, stats, range and runtime scene setup; will shrink as prefab wiring replaces dynamic setup |

**Call direction:** Scene composition → gameplay components (Flight/Camera/Targeting/Weapons/UI) → Unity and shared data. Weapon-specific code must not depend on `WeaponLabSession`; scene-only opponent HP/respawn logic must not leak into generic targeting or flight.

## Gameplay Core A1 — included in this patch

- Preserve exact running behavior; move Break Turn from `Weapons/WeaponLabBreakTurn.cs` to `Flight/BreakTurn.cs`, remove direct `WeaponLabEnemy` and keyboard dependencies. Player input now belongs to `FlightController`, enemy decision remains in `WeaponLabEvasion`. Death/respawn continues to cancel the maneuver through the Lab encounter controller.
- Move `Visual/WeaponLabCameraLead.cs` to `Camera/CameraLookAhead.cs`. Its smoothing, boost modifier, execution order and the authored scene camera remain the same.
- Make `TopDownCameraFollow` pitch-hotkey toggle neutral (`SetPitchHotkeysEnabled`), without changing the Sandbox default or Lab value. Keep `.meta` GUIDs on moved scripts.
- **Not in this atom:** new prefabs, independent weapon scripts, broad folder rearrangement, HUD redesign, radar, bombs, NURS, loadouts, enemy AI balance, scene edits.

## Locked feel-test baseline

- Camera: WeaponLab pitch 65°, always-on look-ahead, unchanged position smoothing and orthographic framing.
- BURST: 4 shots at 14/s, 0.45 s pause after final round, ±12° bounded aim, MG02.
- GAUSS: forward-only, 5 damage, 300 speed, 250 range, 1.5 s cooldown, no hold-repeat.
- MISSILE: 1.0 s cursor lock, 0.5 s grace, no minimum range, one active rocket; existing physical pursuit and hit behavior.
- Break Turn: Q/E player, shared enemy ability, real 90° yaw in 0.55 s, 1.0 s powered phase, 70° bank and full Afterburner charge; enemy missile and sector decision probabilities unchanged.
- Sandbox is a behavior reference; do not regenerate WeaponLab from old V0/V4/V5 editor tools or overwrite scene-authored values.

## Remaining migration gates

1. **Targeting + weapons:** separate player input/selection from BURST, GAUSS and missile firing. Make bounded aim and lock reusable with a scene-independent target eligibility rule. Preserve projectile and VFX semantics.
2. **Scene prefabs:** after core components are verified, create player/enemy aircraft, combat camera rig and HUD prefabs; prefab references live in the prefab, scene references are injected by small composition code. No hard-coded scene paths for runtime gameplay.
3. **HUD:** reuse Sandbox afterburner bar in WeaponLab, retain diagnostic overlay separately, verify camera viewport/pixelRect aim alignment. No radar until designed.
4. **Blank scene proof:** assemble `GroundAttackLab` with shared prefabs and no duplicated gameplay scripts; only then prototype ground attacks.

**Test discipline:** one migration slice per patch → compile and both scenes in Play Mode → user green → next slice. `git apply --check --verbose` before applying. Current green does not imply the extracted code has been Unity-tested.
