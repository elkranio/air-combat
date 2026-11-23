# Gameplay architecture — current contracts

This is the **live architecture**, not a change log. Implementation chronology is in [`Docs/Archive`](Docs/Archive/README.md); gameplay data and playtest notes are in [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md).

## Scenes and ownership

- `Assets/_Project/Scenes/WeaponLab.unity`: player jet, enemy fighter, combat camera and shared lighting. `WeaponLabSession` binds air combat weapons, target lock, fighter behavior and diagnostics.
- `Assets/_Project/Scenes/GroundAttackLab.unity`: same reusable player/camera/lighting prefabs, four military depots, one AA1 and one SAM1. `GroundAttackLabSession` attaches/configures lab-only `BombRack`, `CabrageManeuver`, `NursRocketRack`, `DirectNursRack`, `PlayerHitPoints`, `AircraftRadar`, `NursCameraCue` and bomb nadir; it binds existing scene AA/SAM targets and the shared CombatHUD.
- `Sandbox.unity`: legacy flight/shadow and input comparison. **Do not migrate it implicitly.** `FrontierStrata`/bake scenes are art references.

## Modules

| Folder | Components / responsibility |
| --- | --- |
| `Scripts/Aircraft` | `JetAssembly`, `JetChassis`, `CombatCameraRig`, `AircraftRadar`, `IRadarContact` — reusable aircraft/camera roots and scan sensor |
| `Scripts/Flight` | `FlightController`, `AircraftMotor`, `Afterburner`, `BreakTurn`, `CabrageManeuver` — movement, fuel, evasive and cabrage states |
| `Scripts/Enemy` | `EnemyFlightController`, `AntiAircraftTurret`, `SurfaceToAirLauncher` — hostile aircraft and ground-site behavior |
| `Scripts/Weapons` | air weapons, `BombRack`, `NursRocketRack`, `DirectNursRack`, `NursProjectile`, `SurfaceToAirMissile`, `MissileProjectile` — firing schedule, physical flight and blast |
| `Scripts/Combat` | `DamageableTarget`, `CombatTargetDomain`, `GroundBlastDamage`, `GroundDepotTarget`, `PlayerHitPoints` — domains, HP and ground-only splash |
| `Scripts/Targeting` | `BoundedAim`, `TargetLock`, air lock eligibility |
| `Scripts/Camera` | follow/lead and `NursCameraCue`; **no NURS impact predictor** |
| `Scripts/UI` | `CombatHud`, `FlightHud`, `RadarHud`, lock HUD; no hostile AI/gameplay logic in UI |
| `Scripts/Environment` | shared `CombatLightingRig`/URP and actual shadows |
| `Scripts/Scenes` | scene composition only; isolate experiments from reusable prefabs |

## Combat contracts

- `WeaponSelection` exposes slots 1–6, but **only GroundAttackLab equips slots 4–6**. `WeaponController` routes individual weapon behavior; the shared player-aircraft prefab must not own GroundAttackLab-specific loadouts.
- `DamageableTarget.Domain` distinguishes **Air** from **Ground**. Player gun/A2A attacks are for air targets; bombs and both kinds of NURS use `GroundBlastDamage` against ground receivers. AA and SAM deal damage to the ground-lab player HP receiver only. Keep blast and projectile motion independent.
- AA1 uses a tracked yaw/pitch ballistic autocannon and finite physical tracers. SAM1 is a **fixed vertical launch site** with range-only acquisition. `SurfaceToAirMissile` performs vertical clearance, a free pitch-over and finite-budget pursuit. Do not merge it with the player's `MissileProjectile`.
- `AircraftRadar` owns sensor range, explicit contact registration and X/Z filtering; `RadarHud` consumes detections in a north-up circular screen UI. SAM missiles dynamically register/unregister only when inbound. No scene-wide scan on every frame.
- `CombatHud` prefab owns radar display and bottom HP/fuel status, while the scene binds camera, sensor and health. Shared `CombatLightingRig.prefab` is the only combat lighting authority. `AircraftGroundShadow` belongs to legacy Sandbox.
- The existing `GroundDepotTarget` destruction visual uses temporary runtime mesh clones; preserve original meshes/FBXs. Do not duplicate it into a generic destruction system without an explicit design task.

## Safe iteration rules

Maintain stable Unity `.meta` GUIDs. Tune values in **committed prefabs first** when components are serialized (e.g., the SAM missile prefab uses 150 / 140 / 180 and its script initializer now matches). Do not rerun historical one-time AA1/HUD/SAM1 authoring commands; their source is outside Unity compilation in the archive. No speculative mission router, broad service abstraction, uncontrolled scene regeneration or unwanted weapon balance changes. Verify `WeaponLab`, `GroundAttackLab` and `Sandbox` independently before calling a patch green.
