# Gameplay architecture — current boundaries

## Scene composition

| Scene | Owner | Responsibility |
| --- | --- | --- |
| Mission01_FirstSortie | FlightSession + Mission01LaunchSequence | Campaign phase, takeoff/landing, objectives, failure/retry |
| Mission02_ConvoyInterdiction | FlightSession + Mission02RouteTrial | Sortie clock, flight/safety, convoy/escort/cargo composition, diagnostic modes/reset |
| CombatSandbox | FlightSession + CombatSandboxEncounter | Selectable isolated combat trials, loadout, enemies, retry and cleanup |

M01/Sandbox in Build Settings, M02 editor-only; M03 not built. FlightSession (-80) binds player, camera, health, radar, flight tuning and HUD; owns common pose/camera reset. `IFlightSessionEncounter` provides bind/restart/altitude/status without dependency on concrete mission or sandbox. Campaign owns its retry lifecycle. Do not regenerate authored scenes.

## Campaign lifecycle

- `Mission01LaunchSequence`: Intro/Ready/Takeoff/Flight/Crashed/BoundaryFailed/Landing/Landed, gating and mission retry. `ScriptedTakeoff`, `ScriptedLanding` own pose changes; `Mission01RunwayGeometry` adapts active runway to `LandingRunway`.
- `MissionFlightSafety`, `MissionBoundaryGuard`, `TerrainFlightCollision`: movement segment and bounds checks. `RadarTerrainObstacle`, `TerrainSectionUnion`: shared solid terrain sections for projectiles/radar; decoration is not automatically collidable. Mission chooses failure response.
- `MissionPause` owns exclusive tactical-map/failure pause and time/audio restoration. `MissionRetry` registers owner and publishes accepted retry. No competing time-scale writers or duplicate R paths.
- `Mission01ReconEncounter` binds `WatcherTarget` and `AirTargetCombatEffects`; `DestroyTargetsObjective` reads `IObjectiveTarget`, exposes `ObjectiveProgress` via `IPrimaryObjectiveSource`. Missing/unready targets cannot silently complete. `OverlordTarget` uses `IAirTargetFlightContext` / `IAirTargetEffects`; escape != destruction.
- `Mission02RouteTrial` owns clock and composition; `ConvoyMarchPreview` shares convoy motion/slot/deployment across M02 and sandbox, validates depot route, manages mobile SAM arming and destruction. `Mission02ConvoyEscort` composes two independent `WardenCombat` units. `Mission02CargoEvacuation` owns two sites and optional outcome; `CarryallEvacuationSite` owns individual dispatch/reserve/escape and damage linkage, reusing `CarryallPickupSequence` and crash/breakup visuals. No finished M02 mission-end controller yet. See [M02](Docs/MISSION_02.md).
- `SurfaceToAirLauncher` owns arming, warning, rail/vertical launch profile and owned projectiles. Mission owns when sites are armed. Picket mobile controller uses existing template, PicketHealth owns damage.

## Aircraft, weapons and enemies

- `JetAssembly` / `JetChassis` configured visual and sockets; `FlightController` + `AircraftMotor` player movement, `Afterburner` shared resource, `AfterburnerVfx` presentation. `CombatAircraftFlight` takes enemy pilot commands. No legacy player Q/E break turn.
- `PlayerWeaponTrigger` dispatches independent `ITriggeredWeapon` slots; `IWeaponHudStatus` reports readiness. `AutocannonWeapon` uses `AutocannonPreset`, `AutocannonEmitter`, `BallisticProjectile`; `ArcadeMissileLauncher`, `TargetDesignation`, `WeaponLock`, `TargetingCursor`, `MissileProjectile` implement AAM; `ArcadeNursLauncher`, `NursProjectile`, `NursImpactMarker` implement ground salvos. No legacy WeaponController/TargetLock.
- `ArcadeFighterUnit` owns Tiger flight/health/weapon/pilot binding, `ArcadeFighterPilot` owns individual decisions. `EnemyMissileStressTest` owns hostile AAM launch scheduling, warning and live radar contacts (historical name). IN02 adds manual two-shot salvo alongside unchanged Tiger cooldown policy.
- `InterceptorPassTrial` is a sandbox-owned single-aircraft composition with phase/motion, damage, launch props and explicit contact registration. It uses the IN01 visual-only prefab; not a Tiger pilot. Reuse or extract reusable encounter-independent behavior before M03, rather than making campaign depend on Sandbox.
- `GroundPatrolRoute` / `GroundPatrolPath` authored route/rounded geometry, `GroundPatrolMotor` motion, `MantisGun` / `MantisVerticalRocket` mobile rocket AA. `DamageableTarget` separates air/ground; `PlayerHitPoints` and ground receivers own health. `CombatHitFeedback` / `AirTargetCombatEffects` own effects, not objectives.

## AAM asset ownership (MAINT02b)

- `Prefabs/Weapons/AAM_Player.prefab`, `AAM_Tiger.prefab`, and `AAM_Interceptor.prefab` own their projectile flight values and hit damage. All use `MissileProjectile` and reference the same visual mesh/material resources; no launch-time tuning override.
- Both player aircraft reference `AAM_Player` directly on `ArcadeMissileLauncher`. The aircraft still owns lock, grace, magazine and reload policy.
- `Settings/AAM_Tiger_Loadout.asset` and `Settings/AAM_Interceptor_Loadout.asset` own enemy attack policy plus the projectile reference. Sandbox and M03 bind the same interceptor loadout. Scenes do not own enemy AAM tuning. Runtime warning, radar, cancellation and salvo behavior remain in `EnemyMissileStressTest`.
- MAINT02b preserves the values from `503875b`; the original IN02 balance restoration is deferred to MAINT03. The legacy `Art/Weapons/Missile.prefab` is retained pending the dependency sweep in MAINT02d.

## Presentation

`CombatCameraRig` binds one `ArcadeCameraFollow` (fixed world yaw, heading/boost lead). `AircraftRadar` receives explicit `IRadarContact` sources. CombatHud, RadarHud, FlightHud, WeaponHud, TargetMarkerHud, EnemyMissileThreatHud and TerrainCollisionWarning display runtime state. Mission01ObjectiveHud/Mission01StatusHud are runtime views. TacticalMapView owns UI/input; objective state remains in controllers. `Mission01EngineAudio` retains its historical name/GUID but reads `IPlayerEngineAudioContext` from M01/M02; shared mixer reuses Lynx cruise/boost loops. `CockpitWarningAudio` handles warnings. Future radio/debrief systems not implemented.

## Reusable authoring

Paths relative to `Assets/_Project/Prefabs/`:

| Prefab | Use |
| --- | --- |
| Aircraft/PlayerLynx_Combat.prefab | Campaign player |
| Aircraft/PlayerKestrel_Combat.prefab | Sandbox player |
| Aircraft/EF01_Tiger_Fighter.prefab | Fighter with authored pilot/flight/weapon settings |
| Aircraft/IN01_Interceptor.prefab | Visual-only IN01 V2; IN02 attaches combat components at runtime |
| Aircraft/RE01_Watcher_Combat.prefab, Aircraft/AE01_Overlord_Combat.prefab | Objective aircraft |
| Aircraft/CM01_Sparrow.prefab | Cruise missile target |
| Ground/AA02_Mantis_Combat.prefab | Mobile AA, motor, launcher and rocket carrier visual |
| Environment/SAM01_Picket.prefab | Stationary SAM and mobile weapon tuning template |
| Environment/Base/Airfield_B.prefab | Base composition |
| Camera/ArcadeCameraRig.prefab, UI/CombatHUD.prefab | Shared camera/HUD |

For new mobile AA, place AA02_Mantis_Combat in Sandbox and assign authored `GroundPatrolMotor` route (Air Combat > Mission > Create Patrol Route). No route = stationary. Move/yaw route group, not vehicle scale. Ground Attack binds scene Mantis units, including inactive. Scene references stay on scene instances. Use current M02 scene for Carryall/CG01/Warden/mobile Picket/base prefab links rather than old installer notes.

Keep generic controllers free of mission coordinates. Scene-scoped ownership, explicit binding, controller/view separation. Bake tactical map after terrain/geography changes. Preserve GUIDs and actual dependency closure before removing assets. Historical setup notes remain in [Git snapshots](Docs/Archive/README.md).
