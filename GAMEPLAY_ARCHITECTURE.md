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

- `Prefabs/Aircraft/{PlayerJet,EnemyJet}`, `Prefabs/Chassis/Kestrel`: chassis sockets + motor/boost/break-turn and visual composition. Never install scene-specific weapons on the shared player prefab without explicit approval.
- `Prefabs/Camera/{ArcadeCameraRig,CombatCameraRig}`: new main framing vs old lab framing. Keep lab camera until old labs are deliberately retired.
- `Prefabs/Enemies/{AA1Autocannon,SAM1Launcher,SAM1Missile}`, `Prefabs/Targets/MilitaryDepot`, `Prefabs/UI/CombatHUD`, `Prefabs/Environment/CombatLightingRig`: existing tested components and materials.
- `Scripts/Flight`, `Weapons`, `Enemy`, `Combat`, `Targeting`, `UI`: gameplay mechanics, independent of future map art. `WeaponSelection` supports six slots; only the ground lab currently populates 4–6. `DamageableTarget.Domain` separates Air and Ground. SAM missile is distinct from player A2A missile.
- `Scripts/Camera/{ArcadeCameraFollow,TopDownCameraFollow,CameraLookAhead,NursCameraCue}`: main and lab controllers. Camera study scripts and one-time builder menus are retired.

## Next integration gate (after base art direction)

Create one **persistent main mission composition** using existing scene-neutral prefabs and mission bindings for player, fighter, AA/SAM and six weapons. Avoid copying the whole old lab scene or merging `WeaponLabSession` and `GroundAttackLabSession` into a monolith. Regression-check all slots and target domains before removing labs or legacy camera code. Keep lighting/profile differences deliberate and explicitly compared.

For any build/asset change, check Unity compilation, missing script/material references, collider setup, reset bounds, shadows and scene-specific state cleanup. Never rerun retired installers. Keep serialized Inspector values as source of truth.
