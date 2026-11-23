# Air Combat — current handoff

## Baseline

- **Green base:** AA1 turret (12-round bursts, 230 detection, .45s lead refresh) and boosted PlayerJet pushed on `master` (2026-11-23). **R1 radar is the new Unity-test-required atom.**
- Unity 6.6 / URP. **WeaponLab** is the prefab-composed air-combat regression. **GroundAttackLab** is the ground-combat regression. **Sandbox** remains the only Build Settings entry and is a legacy comparison.
- **F1 flight green:** 70 constant cruise, W boost to 140 (2.5s tank), A/D steering, Q/E Break Turn. Shared `FlightController` enables this with `useFixedCruiseWBoost` on PlayerJet, not globally; Sandbox retains W/S throttle and Space afterburner.

## Green mechanics

- **Air:** BURST (four-shot), GAUSS, guided MISSILE with cursor lock and reacquisition, enemy fighter pursuit/evasion and respawn; see [architecture](GAMEPLAY_ARCHITECTURE.md).
- **G2 depot:** four prefab instances with independently destructible bunker (3 HP), two tanks (1 HP each) and crates (2 HP). The runtime per-part mesh clipping is a temporary visual solution, not authored fracture geometry.
- **G3 bombs / slot 4:** four gravity bombs at 0.25s spacing, 3s cooldown after release, afterburner scatter, Q/E cancellation, ground-only blast damage (radius 13). Nadir line/ring shows altitude and ground directly below, **not** predicted impact. No area brackets.
- **G4 cabrage + NURS / slot 5:** 2.5s climb and 2.5s return, steering/boost/selection lock, Q/E emergency break-and-return. Six unguided physical rockets during climb, blast radius 6, ground-only damage. Normal cooldown 12s, emergency 20s, starting after return.
- **G4c camera:** extra lead 24 when slot 5 selected, 70 while committed, constrained to keep the jet in viewport (20% margin). NURS ground-impact marker was removed and its preview calculation deleted; R1 radar is a separate aircraft instrument.
- **L1 lighting:** shared `CombatLightingRig` prefab across the two combat labs, Day/Sunset/NearNight and true URP shadows from aircraft, ordnance and terrain. `AircraftGroundShadow` remains only for legacy Sandbox/prototypes. No second shadow projection system.

## AA1 green + R1 pending test

The installed stationary AA prefab is green: real yaw/pitch servos, stale-velocity ballistic lead, 12-round tracer bursts and 2.5s rest. Detection range 230; incoming damage uses a lab-only aircraft hitbox, 20 player HP. `GroundAttackLabSession` binds player at runtime; **do not rerun the AA1 one-time installer**.

**R1** equips the GroundAttackLab player with a range-limited `AircraftRadar` (460) and creates a compact heading-up `RadarHud` above the fuel bar. The AA1 turret implements `IRadarContact`; its radar ring reads the live turret detection range, and disappears when destroyed. No aim warning beam or enemy AI change. Debug HUD in both combat labs starts hidden; H toggles it. See [radar test plan](GAMEPLAY_RADAR_R1.md).

## Code boundaries

- `Assets/_Project/Scripts/Scenes/`: `WeaponLabSession` and `GroundAttackLabSession` own scene wiring, diagnostics and scene-only runtime components; they do not belong in shared prefabs.
- `Scripts/Flight`, `Weapons`, `Combat`, `Targeting`, `Camera`: reusable behavior. Ground blast damage is domain-filtered and shared between bombs and NURS; each weapon still owns its own ballistics and blast radius.
- `Scripts/Environment/CombatLightingRig.cs` + `Prefabs/Environment/CombatLightingRig.prefab`: single source of truth for combat light/time presets and real shadows. Legacy `Visual/AircraftGroundShadow.cs` must not be attached to these combat scenes.
- Preserve existing `.meta` GUIDs when moving Unity scripts or authored assets. Do **not** rerun old `ConfigureWeaponLab*`, G1 or L1 one-shot scene installers.

## Regression check before the next green

1. With Unity **closed**, apply the patch to clean `master`; inspect `git status --short` and confirm moved `.cs` files have their original `.meta` GUIDs.
2. Open **WeaponLab**: no Missing Scripts or broken prefab/lighting references. Test BURST, GAUSS, missile lock/reacquire, enemy evasion, Q/E, W, R/H, look-ahead, day/sunset shadows.
3. Open **GroundAttackLab**: radar visible but small/transparent, AA contact visible inside 460 and invisible outside, ring showing 230-unit range; the dot/ring rotate around the fixed player arrow during turns. Verify radar fits above the fuel bar and does not block the combat view. H toggles initially hidden diagnostics; R resets turret/jet without duplicating radar UI.
4. Test slot 4 four-bomb cadence, altitude-only guide, bomb shadows, four-zone depot destruction; slot 5 six NURS, camera and Q/E abort; AA1 tracer streams, A/D/W/Q/E avoidance, player HP, reset and bomb/NURS suppression. No impact predictor or aim warning.
5. Turn the shared lighting prefab's Day/Sunset/NearNight in Play Mode; ensure aircraft/ordnance cast genuine shadows on ground, slope and roofs. Exit Play Mode; verify lighting settings are restored.
6. Open **Sandbox**: legacy input, missiles and old decorative shadow unchanged. Confirm Build Settings still only lists Sandbox. No scene/prefab/URP asset should have been rewritten by this cleanup.

## Not yet built

Coordinated AA/multiple emplacements, dynamic radar contact discovery, hostile aircraft weapons, finished explosion/destruction VFX, mission flow and alternative chassis/loadout system. The R1 radar registers the one lab AA contact; it does not survey the whole world. Do not change flight/weapon feel while testing the radar.

**Workflow:** one approved atom → `.patch` → `git apply --check --verbose` and `git apply` → Unity playtest → green push. Older migration notes are in [Docs/Archive](Docs/Archive).
