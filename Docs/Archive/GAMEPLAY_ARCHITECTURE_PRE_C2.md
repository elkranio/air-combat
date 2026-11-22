# Gameplay architecture — live composition (post-C1)

**Status:** A1–A4c tested green through `e60a5c3` (2026-11-22). `WeaponLab.unity` is the prefab-composed air combat reference formerly called `WeaponLabPrefabs.unity`. `Sandbox.unity` is still the sole Build Settings entry. C1 changes file organization only, not combat behavior.

## Composition

```text
WeaponLab scene
  WeaponLabSession (scene-only orchestration and diagnostic UI)
    PlayerJet prefab instance                     EnemyJet prefab instance
      FlightController                              EnemyFlightController
      Afterburner / BreakTurn                        Afterburner / BreakTurn
      WeaponSelection / WeaponController             WeaponLabEvasion
      BoundedAim / GuidedMissileLauncher             WeaponLabEnemy (5 HP, respawn)
      JetAssembly                                    JetAssembly
        JetVisual / AfterburnerVfx                     JetVisual / AfterburnerVfx
          Kestrel prefab (JetChassis + hardpoints)        Kestrel prefab (same model)
    CombatCameraRig prefab instance
      Camera / TopDownCameraFollow / CameraLookAhead
      TargetLock / TargetLockHud / CombatCameraRig
```

**Scene-only links:** `WeaponLabSession` configures player/burst/boost, enemy AI target and missile lock, connects incoming missile notifications to the enemy evasion component, and configures Break Turn. `CombatCameraRig.BindPlayer/BindLockTarget` wire the scene targets. The prefab assets must not serialize references to scene objects.

**Chassis contract:** `JetChassis` is visual/geometry data: mesh, animation root, Autocannon_L / Autocannon_R, Gauss, Missile, Engine_L / Engine_R sockets. `JetAssembly` links role root to the nested visual. Flight, AI, weapon timing and damage do **not** belong inside the chassis. New chassis must supply equivalent sockets; avoid copying muzzle/nozzle offsets to gameplay code. Legacy numeric fallbacks remain for Sandbox/old non-composed objects.

## Runtime ownership

| Capability | Owner | Notes |
| --- | --- | --- |
| Pilot flight | `FlightController` / `AircraftMotor` | Keyboard input and bank; separate AI flight controller |
| Enemy pursuit | `EnemyFlightController` | Enemy's current sparring-flight logic; scene target injected |
| Boost and maneuver | `Afterburner`, `BreakTurn`, `AfterburnerVfx` | Shared tank, 90-degree turn; visual sockets from chassis |
| Weapon mode | `WeaponSelection`, `WeaponController` | 1/2/3 selection, aim and trigger routing, counters |
| Gun cadence | `Autocannon`, `GaussCannon` | Independent burst/cooldown schedulers |
| Gun emission | `AutocannonEmitter`, `GaussEmitter`, `BallisticProjectile` | Physical rounds, audio and effects; air targets only |
| Homing missiles | `GuidedMissileLauncher`, `MissileProjectile`, `MissileImpact` | Launch event consumed by scene AI; one active missile |
| Lock and reticle | `BoundedAim`, `TargetLock`, `ITargetLockEligibility`, `TargetLockHud` | Targeting is camera/scene neutral |
| Damage | `DamageableTarget`, `CombatTargetDomain` | Air/Ground classification; current guns/missiles air-only |
| Lab fighter life cycle | `WeaponLabEnemy` | Still Lab-specific: damage tint, 5 HP, death, respawn |
| Lab AI evasion | `WeaponLabEvasion` | Still Lab-specific and tuned for single opponent |
| Camera | `TopDownCameraFollow`, `CameraLookAhead`, `CombatCameraRig` | 65-degree Lab pitch, smoothing and look-ahead |
| Lab orchestration | `WeaponLabSession` | Temporary scene-specific composition, reset and diagnostics |
| Sandbox | `MissileLauncher`, `FlightHud`, `EnemyIndicator`, etc. | Legacy comparison still actively used; no replacement yet |

No new loadout framework, chassis ScriptableObject, scene router or modular HUD has been approved. Add such systems only when a second chassis or GroundAttackLab genuinely needs them.

## Approved WeaponLab gameplay baseline

- BURST: 4 rounds at 14/s, 0.45-second inter-burst pause, bounded +/-12-degree aim, MG02 sound; mode switch or reselect cancels remaining burst rounds without resetting cooldown.
- GAUSS: forward only, 5 damage, speed 300, max range 250, click to fire, 1.5-second cooldown.
- MISSILE: 1.0-second cursor lock, 0.5-second off-target grace, one active rocket, reacquisition after launch, lethal to the 5-HP Lab fighter. Existing physical flight and AI notification.
- Break Turn: Q/E for player, enemy reaction to sector/missile, 90-degree yaw over 0.55 seconds, 1.0-second powered phase, 70-degree bank, consumes a **full** afterburner tank. No invulnerability or teleport.
- Enemy: 5 HP and respawn after 2 seconds in the Lab; weapon shot/hit stats and enemy kill counter shown in diagnostics.
- Camera: pitch fixed at 65 degrees, automatic look-ahead, no Lab camera pitch-hotkeys. Sandbox retains 1/2/3 camera presets and its own missile path.

## Invariants and migration rules

1. **Never regenerate** the current combat scene from retired `ConfigureWeaponLab*` or `Author*` tooling. Those scripts were removed by C1 after the assets were committed.
2. Change gameplay in small patches; preserve feel and inspector values unless a mechanic is explicitly approved. Validate WeaponLab and Sandbox before a green push.
3. Keep scene-level targets/references in scene composition. Keep chassis-unique visuals and sockets in `JetChassis`, not in the controller.
4. Ground-target damage, bombs and NURS are separate future atoms. Do not enable current anti-air guns against ground objects accidentally.
5. `WeaponLabSession`, `WeaponLabEnemy`, `WeaponLabEvasion`, and Sandbox-specific components are **live**. Do not remove them as part of historical cleanup.
6. `FrontierStrata` and the bake experiments are visual assets/diagnostics, not dead gameplay. Clean them separately with dependency checks.

## Next architectural gate

Construct an independent `GroundAttackLab.unity` that instantiates the role and camera prefabs with explicit scene wiring. Start with a minimal playable flight loop before ground targets, payloads, HUD changes or any additional chassis. Decide a future replacement for Lab-only enemy/mission components only after that proof.

For the detailed history and old setup commands see [Docs/Archive](Docs/Archive); historical guides are **not** current operating instructions.

## G1 independent-scene composition (pending Unity check)

`GroundAttackLabSession` is scene-specific binding for a `PlayerJet` prefab and `CombatCameraRig` prefab. It configures flight Break Turn, bounded aim, weapon selection, and camera follow/lead, with no `WeaponLabSession` or enemy dependencies. The scene contains only a non-damageable flat ground plane and light. This is a prefab reuse proof, not an approved ground-combat system. [G1 test checklist](GAMEPLAY_CORE_G1.md).
