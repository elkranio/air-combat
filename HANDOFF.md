# Air Combat — current handoff

## Baseline

- **Read from `master` at `115e3377` (2026-11-23):** G4c marker removed after a successful camera test. This document describes that code baseline; cleanup changes made after it require their own Unity smoke test.
- Unity 6.6 / URP. **WeaponLab** is the prefab-composed air-combat regression. **GroundAttackLab** is the ground-combat regression. **Sandbox** remains the only Build Settings entry and is a legacy comparison.
- **F1 flight green:** 70 constant cruise, W boost to 110, A/D steering, Q/E Break Turn. Shared `FlightController` enables this with `useFixedCruiseWBoost` on PlayerJet, not globally; Sandbox retains W/S throttle and Space afterburner.

## Green mechanics

- **Air:** BURST (four-shot), GAUSS, guided MISSILE with cursor lock and reacquisition, enemy fighter pursuit/evasion and respawn; see [architecture](GAMEPLAY_ARCHITECTURE.md).
- **G2 depot:** four prefab instances with independently destructible bunker (3 HP), two tanks (1 HP each) and crates (2 HP). The runtime per-part mesh clipping is a temporary visual solution, not authored fracture geometry.
- **G3 bombs / slot 4:** four gravity bombs at 0.25s spacing, 3s cooldown after release, afterburner scatter, Q/E cancellation, ground-only blast damage (radius 13). Nadir line/ring shows altitude and ground directly below, **not** predicted impact. No area brackets.
- **G4 cabrage + NURS / slot 5:** 2.5s climb and 2.5s return, steering/boost/selection lock, Q/E emergency break-and-return. Six unguided physical rockets during climb, blast radius 6, ground-only damage. Normal cooldown 12s, emergency 20s, starting after return.
- **G4c camera:** extra lead 24 when slot 5 selected, 70 while committed, constrained to keep the jet in viewport (20% margin). NURS ground-impact marker was removed and its preview calculation deleted; planned radar is a separate future feature.
- **L1 lighting:** shared `CombatLightingRig` prefab across the two combat labs, Day/Sunset/NearNight and true URP shadows from aircraft, ordnance and terrain. `AircraftGroundShadow` remains only for legacy Sandbox/prototypes. No second shadow projection system.

## Code boundaries

- `Assets/_Project/Scripts/Scenes/`: `WeaponLabSession` and `GroundAttackLabSession` own scene wiring, diagnostics and scene-only runtime components; they do not belong in shared prefabs.
- `Scripts/Flight`, `Weapons`, `Combat`, `Targeting`, `Camera`: reusable behavior. Ground blast damage is domain-filtered and shared between bombs and NURS; each weapon still owns its own ballistics and blast radius.
- `Scripts/Environment/CombatLightingRig.cs` + `Prefabs/Environment/CombatLightingRig.prefab`: single source of truth for combat light/time presets and real shadows. Legacy `Visual/AircraftGroundShadow.cs` must not be attached to these combat scenes.
- Preserve existing `.meta` GUIDs when moving Unity scripts or authored assets. Do **not** rerun old `ConfigureWeaponLab*`, G1 or L1 one-shot scene installers.

## Regression check before the next green

1. With Unity **closed**, apply the patch to clean `master`; inspect `git status --short` and confirm moved `.cs` files have their original `.meta` GUIDs.
2. Open **WeaponLab**: no Missing Scripts or broken prefab/lighting references. Test BURST, GAUSS, missile lock/reacquire, enemy evasion, Q/E, W, R/H, look-ahead, day/sunset shadows.
3. Open **GroundAttackLab**: slot 4 four-bomb cadence, altitude-only guide, bomb shadows, four-zone depot destruction and repeated destruction; slot 5 six NURS, impact counters, camera retaining jet, Q/E abort, cooldown and **no marker**. Switch to 1/2/3 to confirm normal camera.
4. Turn the shared lighting prefab's Day/Sunset/NearNight in Play Mode; ensure aircraft/ordnance cast genuine shadows on ground, slope and roofs. Exit Play Mode; verify lighting settings are restored.
5. Open **Sandbox**: legacy input, missiles and old decorative shadow unchanged. Confirm Build Settings still only lists Sandbox. No scene/prefab/URP asset should have been rewritten by this cleanup.

## Not yet built

Radar, hostile AA, enemy offensive weapons, finished explosion/destruction VFX, mission flow and alternative chassis/loadout system. Do not add frameworks or alter flight/weapon feel opportunistically during cleanup.

**Workflow:** one approved atom → `.patch` → `git apply --check --verbose` and `git apply` → Unity playtest → green push. Older migration notes are in [Docs/Archive](Docs/Archive).
