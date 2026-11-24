# Gameplay architecture — active boundaries

## Scene ownership

| Scene | Owner | Responsibility |
| --- | --- | --- |
| `ArcadeDesertE1` | `E1MapFlightSession` + `Air01Encounter` | Persistent authored world, production camera/flight, player/HUD binding, isolated air or ground test |
| `WeaponLab` | `WeaponLabSession` | Legacy weapon/aircraft regression |
| `GroundAttackLab` | `GroundAttackLabSession` | Legacy ground weapons, depot, AA/SAM and camera regression |

Only the main map is enabled in the build. `E1MapFlightSession` configures variable maneuverability, camera, radar, health, HUD and reset. `Air01Encounter` owns the runtime test composition; Ground Nurs Test selects NURS and scene-authored mobile AA, otherwise it equips AAM and spawns two Tigers. It binds, resets and tears down the active weapons/enemies. This is not a general mission system yet. Do not regenerate the map or rewrite shared lab prefabs for production changes.

## Component boundaries

- Aircraft: `JetAssembly`, `FlightController`, `Afterburner`, `PlayerHitPoints` and `CombatAircraftFlight`; flight, resource, health and chassis sockets remain separate. `ArcadeFighterPilot` owns Tiger pursuit and gun decisions; `EnemyMissileStressTest` (historical class name) owns independent enemy AAM cycles.
- Weapons: `PlayerWeaponTrigger` dispatches independent primary/secondary slots. `AutocannonWeapon`, AAM launcher/projectile and `ArcadeNursLauncher` own their mechanics; `NursImpactMarker` handles the world-space footprint. Legacy `WeaponSelection` remains for labs.
- Ground: `GroundPatrolRoute` is scene-authored waypoint data and shared rounded-path geometry; `GroundPatrolMotor` owns vehicle motion. `MantisGun` owns the accepted mobile missile launcher, radar registration and projectile lifecycle; `MantisVerticalRocket` owns launch phases and limited correction. Temporary runtime canister still depends on authored `turretYaw` and `gunPitch` visual mounts. Keep component GUID and serialized accepted tuning until prefabization/art migration is deliberately done.
- Damage: `DamageableTarget.Domain` separates air and ground; `PlayerHitPoints` provides health and damage events; `GroundBlastDamage` applies ground splash. No cross-domain magic screen-hit test. `CombatHitFeedback` owns air combat feedback. Vehicle destruction still to implement.
- Radar/HUD: `AircraftRadar` samples explicit `IRadarContact`s; `CombatHud` displays camera-relative radar, health, fuel and equipped weapons. Threat warning and physical hit are distinct.

## Authoring and assets

`Air Combat > Mission > Create Patrol Route` is the persistent mission tool. Group/point editing, obstacle check and start preview are in the Inspector. A route anchor is independent of the moving child; place and yaw the group, edit waypoints, do not scale the vehicle. Terrain-only path projection; no pathfinding. The old one-time installers are no longer active editor code.

Existing `Prefabs/Aircraft` (including Tiger fighter and chassis), `Prefabs/Ground/AA02_Mantis`, `Prefabs/Camera`, `Prefabs/UI` and environment prefabs are retained. The Mantis prefab is still a visual chassis; the scene wrapper carries the motor and combat component. CLEANUP-02 will formalize reusable battle-ready prefab composition without putting scene references in prefab assets. All labs, their prefabs, and raw art sources stay available. See [asset policy](ART_PIPELINE.md).

## Future boundary

Mission scripts should compose reusable units, routes and objective/trigger data. Do not add mission-specific coordinates or dependencies inside generic weapon/flight code. Prefabize existing behavior before new features. Keep runtime asset lookup and serialized GUID references in mind during resource cleanup; a lack of scene references alone is not proof of disuse.
