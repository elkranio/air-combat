# Gameplay architecture — active boundaries

The **main art/flight map is not a finished combat scene**. Keep usable combat labs until the full six-weapon game can be regression-tested in the main scene. Gameplay numbers: [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md).

## Scene ownership

| Scene | Owner | Camera | Purpose |
| --- | --- | --- | --- |
| `ArcadeDesertE1.unity` | `E1MapFlightSession` | `ArcadeCameraRig.prefab` / `ArcadeCameraFollow` | Growing authored world, flight, height comparison; no weapons/enemies session |
| `WeaponLab.unity` | `WeaponLabSession` | Legacy `CombatCameraRig.prefab` | Fighter pursuit, BURST/GAUSS/MISSILE regression |
| `GroundAttackLab.unity` | `GroundAttackLabSession` | Legacy `CombatCameraRig.prefab` + `NursCameraCue` | Ground weapons, AA/SAM, radar/HP regression |

Main map is **never regenerated**. Both camera prefabs are independent and have no cross-scene references in prefab assets. `CombatCameraRig.BindPlayer()` supports both the accepted arcade follow and the lab legacy follow. `CombatHud` binds radar direction to a supplied camera when appropriate; `RadarHud` does not depend on retired C1 components.

## Reused gameplay assets

- `Prefabs/Aircraft/{PlayerJet,EnemyJet}`, `Prefabs/Chassis/Kestrel`: reusable flight/weapon sockets and visual composition. Never add scene-specific weapons to the shared player prefab without approval.
- `Prefabs/Aircraft/EF01_Tiger_D1_Chassis.prefab`: new **visual-only** Tiger + `JetChassis` sockets, currently **not** the `EnemyJet` AI. [Details](Docs/Production/TIGER_D1.md).
- `Prefabs/Camera/{ArcadeCameraRig,CombatCameraRig}`: new production fixed-world camera vs old lab framing. Keep the legacy lab camera for regression.
- `Prefabs/Enemies/{AA1Autocannon,SAM1Launcher,SAM1Missile}`, `Prefabs/Targets/MilitaryDepot`, `Prefabs/UI/CombatHUD`, `Prefabs/Environment/CombatLightingRig`: existing tested components.
- `Scripts/Flight`, `Weapons`, `Enemy`, `Combat`, `Targeting`, `UI`: scene-neutral mechanics. `WeaponSelection` supports six slots; ground lab populates 4–6. `DamageableTarget.Domain` distinguishes Air and Ground. Hostile SAM missiles differ from player A2A missiles.
- `Scripts/Camera/{ArcadeCameraFollow,TopDownCameraFollow,CameraLookAhead,NursCameraCue}`: production and legacy lab components. Retired studies must not be reinstalled.

## Next integration gate — progressive, air combat first

1. **Single atom:** evaluate player BURST with **fixed forward aim** (no cursor-angle deviation) on the **production camera**, preserving regression for GAUSS, missile and ground weapons.
2. **One fighter:** after approving Tiger D1 visually, wire the existing fighter AI to the Tiger chassis and test 1v1. Add the minimum radar/HUD needed to indicate a **detected** enemy contact; do not invent detection mechanics before discussing them.
3. **Several fighters:** test readable, fun and survivable arcade 1vN combat. Later mission composition integrates AA/SAM and other weapons without merging lab session classes into one monolith.

`ArcadeDesertE1` still has **no combined combat session**. Do not delete either lab or alter shared prefab/scene ownership until tested. On each atom check compilation, missing scripts/materials, colliders, reset bounds, shadows and clean scene state. Inspector serialization is authoritative over C# defaults.
