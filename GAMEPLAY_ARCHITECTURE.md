# Gameplay architecture — current

Based on green C2 `master` `8ae843` (2026-11-23). AA1 is authored against it, pending Unity smoke test.

## Composition

- `WeaponLab.unity`: `PlayerJet`, `EnemyJet`, `CombatCameraRig` and `CombatLightingRig` prefab instances. `WeaponLabSession` connects the player, fighter AI, missile lock and diagnostics.
- `GroundAttackLab.unity`: the same player, camera and lighting prefabs, plus four `MilitaryDepot` instances. `GroundAttackLabSession` equips bombs/NURS, binds cabrage/camera, and installs depot hit zones. AA1's optional turret prefab gets its target via the same scene binder.
- `Sandbox.unity`: legacy input, missile and flat decorative shadow comparison. The only Build Settings scene; do not silently migrate it.
- `FrontierStrata` and bake scenes are art baselines, not current combat levels.

## Reusable ownership

| Folder | Components | Contract |
| --- | --- | --- |
| `Aircraft` | `JetAssembly`, `JetChassis`, `CombatCameraRig` | Role roots and visual sockets; no serialized cross-scene targets |
| `Flight` | `FlightController`, `AircraftMotor`, `Afterburner`, `BreakTurn`, `CabrageManeuver` | Movement and maneuvers; cabrage equipped by the ground lab |
| `Enemy` | `EnemyFlightController`, `AntiAircraftTurret` | Flight pursuit and stationary AA tracking/firing; scene-neutral behaviors |
| `Weapons` | `WeaponSelection`, `WeaponController`, air weapons, `BombRack`, `NursRocketRack`, `AntiAircraftProjectile`, projectiles/ballistics | Launch/cadence and physical flight; AA rounds are air-only |
| `Targeting` | `BoundedAim`, `TargetLock`, `ITargetLockEligibility` | Air lock and bounded gun aim |
| `Combat` | `DamageableTarget`, `CombatTargetDomain`, `GroundBlastDamage`, `GroundDepotPart`, `GroundDepotTarget`, `PlayerHitPoints` | Shared domain-filtered blast; depot remains a lab-specific geometry adapter |
| `Camera` | `TopDownCameraFollow`, `CameraLookAhead`, `NursCameraCue` | Common follow; bounded NURS lead only when the scene equips it |
| `Environment` | `CombatLightingRig` | Shared light/time-of-day and URP shadow policy |
| `Scenes` | `WeaponLabSession`, `GroundAttackLabSession` | Scene composition, diagnostics and reset, **not** gameplay data models |
| `Visual`, `UI` | VFX, bomb nadir helper, HUD | Presentation; `AircraftGroundShadow` is legacy Sandbox-only |

## Physics, damage and shadows

- AA1 incoming tracer rounds are **air-only** and collide with any solid world geometry; its stationary turret is a `Ground` damage receiver. `PlayerHitPoints` is attached to GroundAttackLab only.
- Player guns/guided missile remain **air-only**. Bombs and NURS use separate physical integration and distinct blast radii (13 / 6). `GroundBlastDamage` applies each blast once per ground-domain damage receiver and returns actual hit count; it does not change projectile motion or target HP.
- `GroundDepotTarget` temporarily deletes mesh triangles for destroyed zones using **runtime mesh clones**. These are not source meshes or authored breakable meshes. Current live clones replace older ones; original FBX remains immutable.
- The `Prefabs/Environment/CombatLightingRig.prefab` is the **only combat sun/shadow authority**; it references the accepted shared URP asset. Aircraft/ordnance/depot meshes use real shadow maps. No extra procedural ground shadow for combat scenes.
- `Visual/AircraftGroundShadow.cs` is a flat legacy projection for Sandbox and early art experiments. Do not use it as a general terrain shadow system.

## Accepted weapon/camera numbers

- Cruise 70, W boost to 110; Q/E real 90-degree Break Turn with full tank required. Sandbox has different legacy input.
- BURST: 4 shots at 14/s, .45s between bursts, bounded ±12°; GAUSS: fixed heading, 5 damage, speed 300, max range 250, 1.5s cooldown.
- MISSILE: cursor lock 1s, .5s target-loss grace, one active missile, reacquire after launch; Lab fighter has 5 HP and respawns.
- Bombs: 4 × .25s, cooldown 3s from last shot, gravity 80 and drag .045, W scatter; the slot-4 nadir helper is **not** an impact prediction.
- NURS: 6 rockets during 2.5s climb, 170 launch kick and ballistic gravity 80; 2.5s return, Q/E emergency return, cooldown 12/20 after recovery. Slot-5 camera lead is 24/70 and aircraft-clamped; **no NURS aim marker**.

## Rules

Keep role, visual chassis and scene binding independent. Preserve `.meta` GUIDs when moving scripts; don't regenerate accepted scenes. No speculative loadout framework, radar, AA coordination, mission router or universal HUD until an actual second use case. Test WeaponLab, GroundAttackLab and Sandbox before green.
