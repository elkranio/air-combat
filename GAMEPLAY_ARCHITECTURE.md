# Gameplay architecture — active migration map

**Status:** A1/A2/A3a tested green (`c993e04`, `072b53e`, `c354c28`); A3b firing cadence extraction pending Unity test. Incremental extraction from the tested WeaponLab prototype. No behavior redesign. The accepted combat scene is still `Assets/_Project/Scenes/WeaponLab.unity`, and `Sandbox.unity` remains the only build scene. Keep both intact while migrating. Source base: `master` `03e1781`.

## Ownership and dependencies

| Area | Reusable runtime owner | Remaining Lab-specific wiring / work |
| --- | --- | --- |
| Flight | `FlightController`, `EnemyFlightController`, `AircraftMotor`, `Afterburner`, `AfterburnerVfx`, **`BreakTurn`** | `WeaponLabSession` temporarily installs player/enemy `BreakTurn` and links controllers; `WeaponLabEvasion` is still test AI |
| Camera | `TopDownCameraFollow`, **`CameraLookAhead`** | Session enables look-ahead and disables pitch hotkeys (Lab uses authored 65-degree pitch); Sandbox retains pitch keys |
| Guns | `WeaponLabCannon`, `WeaponLabProjectile` (not migrated yet) | WeaponSelection owns 1/2/3; `Autocannon` / `GaussCannon` own independent firing schedules; legacy WeaponLabCannon still renders, plays audio and spawns Lab-specific projectiles |
| Aim and lock | `BoundedAim`, `TargetLock`, `ITargetLockEligibility`, `TargetLockHud` | Aim and lock are scene-neutral; WeaponLabSession supplies target eligibility and timings; lock still supports one target |
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

1. **Weapons A3c:** complete BURST/GAUSS projectile/VFX/audio ownership and then missile/damage contract extraction; A3b first moves independent firing schedules.
2. **Scene prefabs:** after core components are verified, create player/enemy aircraft, combat camera rig and HUD prefabs; prefab references live in the prefab, scene references are injected by small composition code. No hard-coded scene paths for runtime gameplay.
3. **HUD:** reuse Sandbox afterburner bar in WeaponLab, retain diagnostic overlay separately, verify camera viewport/pixelRect aim alignment. No radar until designed.
4. **Blank scene proof:** assemble `GroundAttackLab` with shared prefabs and no duplicated gameplay scripts; only then prototype ground attacks.

**Test discipline:** one migration slice per patch → compile and both scenes in Play Mode → user green → next slice. `git apply --check --verbose` before applying. Current green does not imply the extracted code has been Unity-tested.

## Gameplay Core A2 — targeting extraction (pending local Unity test)

- Move `WeaponLabFreeAim` → `Scripts/Targeting/BoundedAim.cs` and `TargetLock` → `Scripts/Targeting/TargetLock.cs`, each with its original `.meta` GUID. `BoundedAim` keeps the exact 12-degree default sector, smoothing, world-space reticle and mouse projection. The current `WeaponLabCannon` simply changes the type it calls; **gun code and selection logic are not split in this atom**.
- Remove the `WeaponLabEnemy` type dependency from shared `TargetLock`: the scene now calls `ConfigureTarget(target, 1.0f, 0.5f, fighter)` and the target supplies `ITargetLockEligibility.CanBeLocked` based on its down state. Sandbox continues to use its serialized target and original 1.5 s/1.0 s values without configuring the optional status contract.
- Keep lab lock sound, pointer viewport requirements, cursor radius, visibility, 1-active-missile rule and respawn semantics unchanged. No changes to missiles, damage, AI, flight, camera, UI, prefab contents or scenes. This is a compile + functional gate, not a balancing pass.
- Test WeaponLab 1/2/3 switching, aiming wedge while holding LMB, BURST/Gauss hits, MISSILE acquisition with 0.5 s grace, target down/respawn resetting lock; test Sandbox 1/2/3 camera and its original missile target lock. Run no scene generators.

## Gameplay Core A3a — weapon selection, first weapon slice (pending Unity test)

- New `Scripts/Weapons/WeaponSelection.cs` is the authoritative 1/2/3 selector. It is configurable via `Select(WeaponMode)` and emits a revision even for reselecting the same mode, preserving the old *cancel unfinished BURST but never reset cooldown* behavior. `LateUpdate` order 120 remains ahead of `WeaponLabCannon` at 130 and `TargetLock` at 200.
- The current `WeaponLabCannon` remains the authoritative BURST/GAUSS shooter for now. It reads the selected mode/revision, retains its shot cadence, sound, tracers, damage, aim, and switches the missile lock as before. The scene temporarily installs/wires selection at Play Mode entry. No change to Sandbox or serialized weapon references.
- **Next A3b:** separate `Autocannon` and `GaussCannon` from the legacy combined component and define the missile launch/impact boundary. That step should only follow a local green for A3a, because it will touch projectile callbacks and firing order.
- Test WeaponLab 1/2/3 switching and same-slot reselect during BURST, GAUSS holding/reclick, missile lock/launch and the two scenes' controls. No new prefabs, HUD or ground weapons in A3a.

## Gameplay Core A3b — cadence modules (pending Unity test)

- `Autocannon` owns burst scheduling and cancellation, `GaussCannon` owns single-click readiness. They take explicit frame time and an emission callback; no scene dependency or input polling.
- Legacy `WeaponLabCannon` retains all serialized tuning, audio/VFX and projectile spawning until A3c, and delegates scheduling to the two new modules. The old scene GUID, launcher, aim, lock and Sandbox remain untouched.
- Full migration and reusable prefab components are **not yet complete**. See [GAMEPLAY_CORE_A3B.md](GAMEPLAY_CORE_A3B.md) for exact regression steps.
