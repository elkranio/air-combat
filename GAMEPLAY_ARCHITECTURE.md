# Gameplay architecture — active boundaries

## Scene ownership

| Scene | Owner | Responsibility |
| --- | --- | --- |
| `ArcadeDesertE1` | `E1MapFlightSession` + `Air01Encounter` | Persistent authored world, production camera/flight, player/HUD binding, isolated air or ground test |
| `WeaponLab` | `WeaponLabSession` | Legacy weapon/aircraft regression |
| `GroundAttackLab` | `GroundAttackLabSession` | Legacy ground weapons, depot, AA/SAM and camera regression |

Only the main map is enabled in the build. `E1MapFlightSession` configures variable maneuverability, camera, radar, health, HUD and reset. `Air01Encounter` owns the runtime test composition; Ground Nurs Test selects NURS and scene-authored mobile AA, otherwise it equips AAM and spawns two Tigers. It binds, resets and tears down the active weapons/enemies. This is not a general mission system yet. Do not regenerate the map or rewrite shared lab prefabs for production changes.

## Component boundaries

- Aircraft: `JetAssembly`, `FlightController`, `Afterburner`, `PlayerHitPoints` and `CombatAircraftFlight`; flight, resource, health and chassis sockets remain separate. `ArcadeFighterUnit` on the Tiger prefab owns its serialized attack settings, pilot lifetime, flight/health initialization and delegates to `ArcadeFighterPilot` for pursuit and gun decisions; `EnemyMissileStressTest` (historical class name) owns independent enemy AAM cycles.
- Weapons: `PlayerWeaponTrigger` dispatches independent primary/secondary slots. `AutocannonWeapon`, AAM launcher/projectile and `ArcadeNursLauncher` own their mechanics; `NursImpactMarker` handles the world-space footprint. Legacy `WeaponSelection` remains for labs.
- Ground: `GroundPatrolRoute` is scene-authored waypoint data and shared rounded-path geometry; `GroundPatrolMotor` owns vehicle motion. `MantisGun` owns the accepted mobile missile launcher, radar registration and projectile lifecycle; `MantisVerticalRocket` owns launch phases and limited correction. The rocket carrier visual supplies a fixed authored `LaunchSocket`; MantisGun binds that transform and no longer constructs a temporary pod or hides gun barrels. Component GUID and accepted tuning are unchanged.
- Damage: `DamageableTarget.Domain` separates air and ground; `PlayerHitPoints` provides health and damage events; `GroundBlastDamage` applies ground splash. No cross-domain magic screen-hit test. `CombatHitFeedback` owns air combat feedback. Vehicle destruction still to implement.
- Radar/HUD: `AircraftRadar` samples explicit `IRadarContact`s; `CombatHud` displays camera-relative radar, health, fuel and equipped weapons. Threat warning and physical hit are distinct.

## Authoring and assets

`Air Combat > Mission > Create Patrol Route` is the persistent mission tool. Group/point editing, obstacle check and start preview are in the Inspector. A route anchor is independent of the moving child; place and yaw the group, edit waypoints, do not scale the vehicle. Terrain-only path projection; no pathfinding. The old one-time installers are no longer active editor code.

Existing `Prefabs/Aircraft` (including Tiger fighter and chassis), `Prefabs/Camera`, `Prefabs/UI` and environment prefabs are retained. `Prefabs/Ground/AA02_Mantis_Combat.prefab` is the battle-ready unit: root motor and launcher, nested `AA02_Mantis_Rocket` visual-only prefab, internal launch socket reference, no scene route in the asset. The main-map instance retains its route as an override. `Air01Encounter` discovers scene-authored Mantis units in Ground Nurs Test and binds each to player health and radar; no installer or registration list is required. The original `AA02_Mantis` gun visual is retained as a historical asset until the new art is accepted; the combat prefab GUID and scene overrides remain unchanged. See [ART-AA-01](Docs/ART_AA_01.md). All labs, their prefabs, and raw art sources stay available. See [asset policy](ART_PIPELINE.md).

### Add mobile AA to the main map

Drag `AA02_Mantis_Combat.prefab` into the scene. Create a route with `Air Combat > Mission > Create Patrol Route` or use an existing route. Assign it in the unit’s `GroundPatrolMotor` Inspector; the start pose previews immediately. Use `Group with assigned route` to move the route and child as one group. Different units may use independent routes or different start points on a shared route. Without a route the unit stays where placed. Tune the individual motor and launcher via prefab instance overrides; Play in Ground Nurs Test. Do not apply scene route references to the prefab asset.

### Player combat prefab

The main-map player uses `Prefabs/Aircraft/PlayerKestrel_Combat.prefab`, independently of the legacy `PlayerJet.prefab` used by the labs. The main-map session reads the authored radar and health instead of adding them at runtime. `Air01Encounter` selects and binds the authored AAM or NURS child (both inactive in the asset); the existing AC-01 primary is referenced by the trigger, and AC-02 remains unequipped. Scene-specific camera, targeting, feedback, spawn pose and flight-test settings remain in the session. Weapon modules no longer need to be created and destroyed by the encounter.

### Tiger combat prefab

`Prefabs/Aircraft/EF01_Tiger_Fighter.prefab` contains the chassis, flight motor, boost, health, radar, AC-01 and `ArcadeFighterUnit`. Tune the Attack settings on that prefab (not on Air01Encounter). The encounter instantiates the prefab, prepares it, registers health/targeting/AAM/radar, and binds the player and map boundary. Each unit creates its own randomized pilot variant. The encounter deliberately schedules both flight decisions before aircraft movement and guns after movement, retaining wingman context and the existing shared AAM scheduler. A future mission can use the same unit API; simply dropping an unbound Tiger in the scene does not assign a target. No balance or timing changes in this atom.

## Future boundary

Mission scripts should compose reusable units, routes and objective/trigger data. Do not add mission-specific coordinates or dependencies inside generic weapon/flight code. Prefabize existing behavior before new features. Keep runtime asset lookup and serialized GUID references in mind during resource cleanup; a lack of scene references alone is not proof of disuse.

## Campaign scene

`Mission01_FirstSortie` owns the authored v3 geography and `Mission01LaunchSequence` owns takeoff, flight, terrain crash and retry. `RadarTerrainObstacle` and `TerrainSectionUnion` provide shared terrain geometry for radar/proximity; projectile queries use marked rock surfaces. Mission data and tactical map are the next separate systems, not new responsibilities of the flight controller. See [Mission 01](Docs/MISSION_01.md).
