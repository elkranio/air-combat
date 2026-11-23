# Air Combat — current handoff

Updated 2026-11-23 after the documentation/retired-editor-tools cleanup. **Read this file and [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) before modifying gameplay.** This cleanup does not change flight, ballistics, weapons, enemies, HUD, scene objects or gameplay prefabs.

## Current playable baseline

- **Unity 6.6 / URP**. `WeaponLab` is the fighter-combat regression; `GroundAttackLab` is the ground-combat regression. `Sandbox` is legacy and the only Build Settings scene; `FrontierStrata` is an art reference.
- Player constant cruise **70**, W boost **140** with a **2.5s** fuel tank, A/D steering, Q/E 90° Break Turn requiring a full tank; R reset, H diagnostics. WeaponLab slots 1–3; GroundAttackLab slots 1–6.
- **GroundAttackLab**: four independently damageable military depots; four-bomb run (4); six-rockets-during-cabrage attack (5); forward/down direct six-NURS salvo (6); AA1 autocannon and vertical-launch SAM1; north-up radar at top-right, HP + fuel strip at bottom. No NURS predicted-impact marker. No flares/decoys.
- **SAM tuning in the committed missile prefab**, after manual green playtest: speed **150**, homing turn **140°/s**, total homing turn budget **180°**. This cleanup aligns the script initializers with that prefab; **serialized Inspector values remain authoritative**. SAM lock 1.4s within 350, vertical start -> pitch-over -> homing, three quick launch beeps then silence. Site and inbound missile appear on radar. Player A2A missile is a separate class/prefab.
- **Direct NURS G5 (slot 6)**: independently playable, 6 rockets / 0.15s, down-angle 10°, radius-8 ground splash, 4s cooldown; normal steering during release. Boost spreads shots; Q/E and weapon switch cancel only pending shots. No targeting reticle.

## Known issues / next design decisions

1. **Direct NURS range is too long for the desired arcade feel.** Tune muzzle speed / down-angle / ballistics separately after another visual playtest. Current mechanic is approved; don't "fix" by adding automatic aiming. Use `DirectNursRack` and `NursBallistics` to find the relevant controls.
2. Cabrage is deliberately demanding and still needs refinement. It should remain a different attack option; don't silently replace slot 5 with slot 6. Future cabrage tuning may trade precision for a denser area salvo.
3. Continue testing multiple AA/SAM sites and mobile ground threats in small missions, but do not build mission systems or countermeasures prematurely. Initial enemy SAM is dangerous but dodgeable with the player's current maneuvers.
4. `GroundDepotTarget` still has a temporary runtime mesh-cutting destruction implementation; don't turn it into an art-pipeline refactor during combat tuning.

## Environment plan / next work (2026-11-23)

- **Current approved direction:** one expandable **stylized hybrid** low-flight map with broad hills/lowlands, crisp ground/rocks/buildings, roads and one genuinely flyable canyon that reaches aircraft altitude and can cause wall crashes. Unify screen-space detail and materials; avoid V19's sharp-prop / blurry-ground mismatch, toy styling and texture stretching. New map uses the accepted `CombatLightingRig.prefab`, then restrained post/shader grading. Plan: [Docs/Art/ENVIRONMENT_MAP_PLAN.md](Docs/Art/ENVIRONMENT_MAP_PLAN.md).
- **C1/C1b verdict (2026-11-23):** 50° heading-locked 3/4 looks richer and the V19 world feels more three-dimensional, but with six weapons, fighter, AA1 and SAM1 combat becomes harder and the rotating world causes **noticeable mild motion sickness**. Comfort and player control outrank visual novelty. **Active C2:** fixed-world yaw **-45°**, pitch **50°**, ortho 80, same combat/terrain; plane rotates on screen, ground does not. Existing scene `FrontierCameraCombatC1.unity` now defaults to C2. **C** compares to old rotating-world C1b at *the same* pitch; J/K/L optionally tune pitch. Radar uses actual camera axes (jet arrow turns in fixed mode, points upper-right in rotating mode). **C2 VERDICT PENDING**; then E1 map development. See [camera test](Docs/Art/CAMERA_C1_COMBAT.md).
- **C1b already authored/committed; do NOT rerun its one-time menu.** Reuse the existing combat study with all six weapon slots (keys 1–6), AA1, SAM1, four depots and fighter. C2 is a focused camera/radar software change, not a new scene or asset rebuild; no relighting, enemy tuning or production terrain in this atom. See [C1b/C2 test](Docs/Art/CAMERA_C1_COMBAT.md).
- **Next after C2:** E1 new persistent environment scene and shared-light foundation based on V19 composition; then E2 sharpness/material coherence, E3 real collision canyon, E4 buildings/dressing, E5 grading/map expansion. No gigantic one-shot procedural rebuild.

## Art checkpoint

- `FrontierStrata` V17 remains the terrain/art baseline; the V18/V19 selected-to-active bake workflow works technically, but this is **not** automatic artistic acceptance. See [ART_AUTHORING.md](ART_AUTHORING.md) before making new environmental assets.
- V20 **Beds 0.45** is preferred only for the studied BrokenMesa; V19 **0.40** is a softer fallback. Still unresolved: screen-edge shadow degradation, overall image clarity / atmosphere, and final refinery art language.
- **V2A–V2A5 art reset (2026-11-23):** all failed V2 live scenes/meshes/textures/one-time generators and their Unity `.meta` files were culled. V17 and earlier proven bake references remain. The one-rock and free-pack fallbacks were **tried** (RockKitR1 and BrokenVector cliff pack), but neither won visually. Native T1 is technically usable but too blurry/stretched on steep slopes. **Do not treat these older fallback steps as the next task**; see the approved hybrid map plan. See [art reset](ENVIRONMENT_ART_RESTART.md) for the reasons behind previous rejections.
- For each new visual atom, add **accepted / rejected / unresolved**, tested camera/size and generated asset size to this handoff, then archive the iteration after its conclusions are folded into the live art guides.

## Code ownership / safe boundaries

- `GroundAttackLabSession` owns lab-only setup: bombs, cabrage, direct NURS, health, radar registrations, scene targets and HUD binding. `WeaponLabSession` owns the other lab. Shared `PlayerJet` and air-combat prefabs should **not** be modified to wire in ground-only experiments.
- Existing AA1, SAM1, missile and HUD prefabs are **authored and committed**. Do not rerun their one-shot editor installers; those scripts have been moved to `Docs/Archive/EditorInstallers/` as noncompiled historical source.
- `AircraftRadar` detects explicit `IRadarContact`s within 460 X/Z units. `RadarHud` only renders the detections: ground threat circles and hostile incoming-missile glyphs; never display our own projectiles as incoming threats.
- Keep player `MissileProjectile` (air-to-air) and `SurfaceToAirMissile` (SAM) independent. Lighting is owned by the shared `CombatLightingRig.prefab`; `AircraftGroundShadow` is legacy Sandbox-only.

## Quick regression after each combat change

1. **GroundAttackLab**: slots 1–6, normal flight/boost/Q/E, bombs, cabrage recovery and camera, direct NURS spread/cancel/cooldown, target damage, AA1 tracers, SAM lock/launch/impact, radar/HP/fuel, H and R. Watch SAM diagnostic `LastFlightReport` if a rocket disappears unexpectedly.
2. **WeaponLab**: fighter pursuit/evasion, BURST, GAUSS, lock-on A2A missile, W/Q/E and R, no ground-attack weapons accidentally equipped.
3. **Sandbox**: legacy movement and original HUD/shadow still work. Check light presets/shadows in both combat labs if modifying URP.
4. **No scene regeneration or asset GUID churn** during code-only patches. Unity Play Mode is a separate validation step; a successful `git apply --check` is not a Unity compile/gameplay test.

## Handoff discipline

For each atom, create a short `GAMEPLAY_*.md` with **actual numbers, files, intended behavior, testing and known limits**. Add a one-line link under README's working notes only while the atom is active. At a cleanup checkpoint, reconcile the true committed prefabs/code into this handoff and [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md), then move superseded atom docs to [Docs/Archive](Docs/Archive/README.md). Keep this handoff updated even if a conversation ends suddenly.
