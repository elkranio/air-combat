# Gameplay architecture — current boundaries

## Scene ownership

| Scene | Composition owner | Responsibility |
| --- | --- | --- |
| `Mission01_FirstSortie` | `FlightSession` + `Mission01LaunchSequence` | Authored player/world, campaign phase, takeoff/landing, failures, retry, objective gating |
| `CombatSandbox` | `FlightSession` + `CombatSandboxEncounter` | Authored world, player/HUD, selectable regression composition, enemy/weapon setup, restart and cleanup |

Both scenes are committed and enabled in Build Settings. `FlightSession` (execution order -80) binds the configured player, `ArcadeCameraFollow`, flight tuning, health, radar and HUD. It owns the shared pose/camera reset. An optional `IFlightSessionEncounter` supplies sandbox-specific bind, restart, altitude and debug status; the campaign owns its own retry lifecycle. No dependency from the shared session to a concrete sandbox class.

## Campaign lifecycle

- `Mission01LaunchSequence` owns phase (`Intro`, `Ready`, `Takeoff`, `Flight`, `Crashed`, `BoundaryFailed`, `Landing`, `Landed`), input gates, SAM arming, mission retry and completion gating. `ScriptedTakeoff` and `ScriptedLanding` implement scripted pose changes; `Mission01RunwayGeometry` adapts the authored runway to reusable `LandingRunway` data.
- `MissionFlightSafety` checks the movement segment against the operation bounds and terrain; `MissionBoundaryGuard` and `TerrainFlightCollision` provide geometry queries. The launch sequence decides the response. `RadarTerrainObstacle` and `TerrainSectionUnion` provide shared solid terrain sections; decorative debris is not automatically collidable.
- `MissionPause` owns exclusive tactical-map / boundary-failure pause and time/audio restoration. `MissionRetry` registers the scene's owner and publishes an accepted same-frame retry. Target controllers and camera respect these contracts; do not add competing global time-scale writers or duplicate campaign R handling.
- `Mission01ReconEncounter` binds the three `WatcherTarget`s and shared `AirTargetCombatEffects`. Targets own their motion, health and state. `DestroyTargetsObjective` reads `IObjectiveTarget` and provides `ObjectiveProgress` via `IPrimaryObjectiveSource`; destroyed required targets alone count. Missing/not-ready targets cannot silently complete the objective.
- `OverlordTarget` is an autonomous optional objective with its own patrol/charge/boost/terminal states. It consumes `IAirTargetFlightContext` and `IAirTargetEffects`, not concrete mission controllers for those behaviors. Escaped is distinct from Destroyed.
- `SurfaceToAirLauncher` owns Picket launch profile, arming, prelaunch warning and missiles. Mission chooses when sites are armed; warning timing and audio presentation are separate helpers.

## Aircraft, weapons, damage

- `JetAssembly` / `JetChassis`: configured aircraft and authored sockets. `FlightController` and `AircraftMotor`: player flight; `Afterburner`: shared resource; `AfterburnerVfx`: presentation. Enemy `CombatAircraftFlight` accepts pilot commands. `BreakTurn` remains part of the enemy flight contract; the player has no old Q/E branch.
- `PlayerWeaponTrigger` dispatches independent slots via `ITriggeredWeapon`; `IWeaponHudStatus` exposes readiness. `AutocannonWeapon` uses an `AutocannonPreset`, burst or continuous cadence and `AutocannonEmitter`; rounds are physical `BallisticProjectile`s. `ArcadeMissileLauncher`, `TargetDesignation`, `WeaponLock`, `TargetingCursor` and `MissileProjectile` implement current AAM. `ArcadeNursLauncher`, `NursProjectile` and `NursImpactMarker` implement ground salvos. No legacy WeaponController/TargetLock path.
- `ArcadeFighterUnit` owns the prefab's fighter settings and pilot lifetime. `ArcadeFighterPilot` owns individual decisions. Sandbox schedules shared encounter work and enemy AAM via `EnemyMissileStressTest` (historical name). Do not make a new campaign depend on a sandbox scene just to spawn fighters.
- `GroundPatrolRoute` / `GroundPatrolPath`: authored route and rounded geometry. `GroundPatrolMotor`: motion. `MantisGun` / `MantisVerticalRocket`: mobile rocket AA. `DamageableTarget` separates air and ground domains; `PlayerHitPoints` and ground damage helpers own health/damage. `CombatHitFeedback` and `AirTargetCombatEffects` own effects, not mission rules.

## Presentation

`CombatCameraRig` binds one `ArcadeCameraFollow` (fixed world yaw; heading/boost lead). `AircraftRadar` receives explicit `IRadarContact` sources. `CombatHud`, `RadarHud`, `FlightHud`, `WeaponHud`, `TargetMarkerHud`, `EnemyMissileThreatHud` and `TerrainCollisionWarning` display runtime state. `Mission01ObjectiveHud` and `Mission01StatusHud` are attached runtime views. `TacticalMapView` owns its UI and input; mission state and objective progress stay in the controllers. `Mission01EngineAudio` and `CockpitWarningAudio` handle sound layers and warning cues.

## Authoring

Paths below are relative to `Assets/_Project/Prefabs/`:

| Prefab | Use |
| --- | --- |
| `Aircraft/PlayerLynx_Combat.prefab` | Campaign player |
| `Aircraft/PlayerKestrel_Combat.prefab` | Sandbox player |
| `Aircraft/EF01_Tiger_Fighter.prefab` | Fighter with authored flight/health/weapon/pilot settings |
| `Aircraft/RE01_Watcher_Combat.prefab`, `Aircraft/AE01_Overlord_Combat.prefab` | Reusable objective aircraft |
| `Aircraft/CM01_Sparrow.prefab` | Interception target |
| `Ground/AA02_Mantis_Combat.prefab` | Mobile AA with motor, launcher and nested rocket-carrier visual |
| `Environment/SAM01_Picket.prefab` | Campaign SAM |
| `Environment/Base/Airfield_B.prefab` | Authored base composition |
| `Camera/ArcadeCameraRig.prefab`, `UI/CombatHUD.prefab` | Shared camera and HUD |

### Add mobile AA

In CombatSandbox, place `AA02_Mantis_Combat.prefab`. Use **Air Combat > Mission > Create Patrol Route** or an existing route. Assign the scene route on `GroundPatrolMotor`; use Group with assigned route and start preview in the Inspector. Move/yaw the route group, not the vehicle scale. Without a route it stays stationary. Ground Attack discovers the scene's Mantis units (including inactive ones) and binds them to player health/radar. Do not apply scene references to a prefab asset.

### Rules for further missions

Compose existing prefabs and scene-owned bindings; keep generic flight/weapons free from mission coordinates. Do not regenerate hand-authored scenes, replace GUIDs casually, or add an installer for already-authored content. Build map geography with the persistent baker after terrain edits. Use the scene reachability audit before removing assets; it is not proof of dynamic or authoring disuse.
