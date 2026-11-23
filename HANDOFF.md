# Air Combat — current handoff

Updated 2026-11-23 for E1 first-flight production and vivid stylized arcade art direction. **Read this file and [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) before modifying gameplay.** This cleanup does not change flight, ballistics, weapons, enemies, HUD, scene objects or gameplay prefabs.

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
- **Camera verdict (2026-11-23):** rotating C1b 50° is visually richer but harms combat and causes mild motion sickness. **C2 fixed-world 50° is GREEN**: yaw -45°, pitch 50°, ortho 80; aircraft rotates onscreen, environment does not. Radar already aligns with the actual camera axes. Gameplay comfort is the priority.
- **Camera C3 GREEN (2026-11-23):** fixed-world yaw **-45°**, pitch **50°**, ortho 80; heading-based forward lead **48 world units**, **+14** on active afterburner, independent **0.42s** smoothing and **20%** viewport-safe margin. C1b rotating-world camera was visually attractive but made combat less comfortable and caused mild motion sickness. `C` remains an optional comparison. Radar follows actual camera axes. [Camera notes](Docs/Art/CAMERA_C1_COMBAT.md).
- **Scene safety:** `FrontierCameraCombatC1.unity` is already authored/committed; never rerun its one-time builder. C3 and E1a operate within that scene; do not alter aircraft/combat prefabs, lighting or other labs. Microfix disables automatic out-of-bounds resets in this study; `R` still resets manually.
- **E1a/flat conclusion:** ground should be visually quiet, **muted and mid-light rather than dark or saturated**. The vivid first E1 pass `#DDA44D` was tested and rejected for its eye-catching saturation. Let aircraft, buildings, threats and VFX provide controlled color accents.
- **E1b result (user Play Mode):** flat muted `#B8A993` ground **accepted**, but the 18 rock chunks read like miniature props; light wings merged into the ground; Y78/shadow relationship harmed scale. **Active E1c:** same existing 2×2 km `ArcadeDesertE1.unity` (not a rebuild). One **430×275m / ~30m** authored low plateau, old E1/E1b rocks disabled; dark blue-grey Kestrel scene-only paint `#31536B` / `#19364B`, orange `#E07940`; initial **Y150**, gameplay-only `7/8/9` compare Y78/Y110/Y150 with instant camera snap. **E1c Unity verdict pending**. One-time menu `Air Combat > Art > E1c - Install Big Plateau + Altitude Test`; existing E1/E1b/E1c builders must not be rerun. [E1c instructions](Docs/Art/ARCADE_DESERT_E1C.md).
- **Visual direction:** Stylized Arcade Military, chunky faceted but softened rock silhouettes (neither round toys nor spikes), quiet ground, clearer/larger geometry and slightly higher aircraft. Aircraft, buildings and later effects carry saturation. Combat systems are proof-of-concept; later high-impact audio, tracers, breakup/debris, smoke and explosions form a separate juice pass. [Art pillars](Docs/Art/ARCADE_VISUAL_DIRECTION.md).
- **After E1c playtest:** choose height/plateau proportion first; then isolate a **longer, more aggressive Kestrel silhouette** test (no shared prefab changes before approval). Only then add hills/canyon, roads/bases and eventually the VFX/audio juice pass. Combat mechanical proofs remain intact.

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
