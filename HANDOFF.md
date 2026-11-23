# Air Combat — current handoff

## Baseline

- **Green base:** AA1, boosted PlayerJet, north-up R1 radar and installed H1 CombatHUD on `master` (2026-11-23). **SAM1b vertical-launch upgrade is pending Unity playtest.**
- Unity 6.6 / URP. **WeaponLab** is the prefab-composed air-combat regression. **GroundAttackLab** is the ground-combat regression. **Sandbox** remains the only Build Settings entry and is a legacy comparison.
- **F1 flight green:** 70 constant cruise, W boost to 140 (2.5s tank), A/D steering, Q/E Break Turn. Shared `FlightController` enables this with `useFixedCruiseWBoost` on PlayerJet, not globally; Sandbox retains W/S throttle and Space afterburner.

## Green mechanics

- **Air:** BURST (four-shot), GAUSS, guided MISSILE with cursor lock and reacquisition, enemy fighter pursuit/evasion and respawn; see [architecture](GAMEPLAY_ARCHITECTURE.md).
- **G2 depot:** four prefab instances with independently destructible bunker (3 HP), two tanks (1 HP each) and crates (2 HP). The runtime per-part mesh clipping is a temporary visual solution, not authored fracture geometry.
- **G3 bombs / slot 4:** four gravity bombs at 0.25s spacing, 3s cooldown after release, afterburner scatter, Q/E cancellation, ground-only blast damage (radius 13). Nadir line/ring shows altitude and ground directly below, **not** predicted impact. No area brackets.
- **G4 cabrage + NURS / slot 5:** 2.5s climb and 2.5s return, steering/boost/selection lock, Q/E emergency break-and-return. Six unguided physical rockets during climb, blast radius 6, ground-only damage. Normal cooldown 12s, emergency 20s, starting after return.
- **G4c camera:** extra lead 24 when slot 5 selected, 70 while committed, constrained to keep the jet in viewport (20% margin). NURS ground-impact marker was removed and its preview calculation deleted; R1 radar is a separate aircraft instrument.
- **L1 lighting:** shared `CombatLightingRig` prefab across the two combat labs, Day/Sunset/NearNight and true URP shadows from aircraft, ordnance and terrain. `AircraftGroundShadow` remains only for legacy Sandbox/prototypes. No second shadow projection system.

## AA1, R1 and H1 green

The installed stationary AA prefab is green: real yaw/pitch servos, stale-velocity ballistic lead, 12-round tracer bursts and 2.5s rest. Detection range 230; incoming damage uses a lab-only aircraft hitbox, 20 player HP. `GroundAttackLabSession` binds player at runtime; **do not rerun the AA1 one-time installer**.

**R1/H1** equip GroundAttackLab with a range-limited `AircraftRadar` (460), north-up top-right `RadarHud` and the bottom HP/fuel CombatHUD strip. The AA1 turret implements `IRadarContact`; its radar ring reads the live turret detection range, and disappears when destroyed. No aim warning beam or enemy AI change. Debug HUD in both combat labs starts hidden; H toggles it. See [radar test plan](GAMEPLAY_RADAR_R1.md).

## SAM1b — pending Unity test

The authored SAM1 prefab stays at (-320,0,215), but must be **upgraded in place once** via **Air Combat > SAM1b > Upgrade Existing Launcher to Vertical** after patch application. SAM no longer tracks the jet mechanically: range-only 1.4s audible acquisition launches a separate rocket vertically (0.34s), then a quick pitch-over (up to 0.75s) before 72°/s homing with a fresh 145° budget. Radar shows launcher ring (350) and incoming missile only. Do not rerun original installer; player air-to-air missile unchanged. See [SAM1b playtest](GAMEPLAY_SAM_M1B.md).

## Code boundaries

- `Assets/_Project/Scripts/Scenes/`: `WeaponLabSession` and `GroundAttackLabSession` own scene wiring, diagnostics and scene-only runtime components; they do not belong in shared prefabs.
- `Scripts/Flight`, `Weapons`, `Combat`, `Targeting`, `Camera`: reusable behavior. Ground blast damage is domain-filtered and shared between bombs and NURS; each weapon still owns its own ballistics and blast radius.
- `Scripts/Environment/CombatLightingRig.cs` + `Prefabs/Environment/CombatLightingRig.prefab`: single source of truth for combat light/time presets and real shadows. Legacy `Visual/AircraftGroundShadow.cs` must not be attached to these combat scenes.
- Preserve existing `.meta` GUIDs when moving Unity scripts or authored assets. Do **not** rerun old `ConfigureWeaponLab*`, G1 or L1 one-shot scene installers.

## Regression check before the next green

1. With Unity **closed**, apply the patch to clean `master`; inspect `git status --short` and confirm moved `.cs` files have their original `.meta` GUIDs.
2. Open **WeaponLab**: no Missing Scripts or broken prefab/lighting references. Test BURST, GAUSS, missile lock/reacquire, enemy evasion, Q/E, W, R/H, look-ahead, day/sunset shadows.
3. Open **GroundAttackLab**: H1 top-right north-up radar and bottom fuel/HP bar, no duplicate radar, H toggles hidden diagnostics. Check red AA ring (230) and red SAM ring (350) within the aircraft sensor (460); the player arrow rotates without spinning the world. R resets both sites and player HP.
4. Test SAM1 audible acquisition, finite physical missile, distinct amber inbound radar glyph and meaningful Q/E/W avoidance. Test site destroyed during missile flight, R cleanup and bomb/NURS suppression. Then test slots 4/5, AA1 tracer streams and camera regressions.
5. Turn the shared lighting prefab's Day/Sunset/NearNight in Play Mode; ensure aircraft/ordnance cast genuine shadows on ground, slope and roofs. Exit Play Mode; verify lighting settings are restored.
6. Open **Sandbox**: legacy input, missiles and old decorative shadow unchanged. Confirm Build Settings still only lists Sandbox. No scene/prefab/URP asset should have been rewritten by this cleanup.

## Not yet built

Coordinated AA/multiple emplacements, dynamic radar contact discovery, hostile aircraft weapons, finished destruction VFX, countermeasures, mission flow and alternative chassis/loadout system. Missile warning audio is intentionally per-site in SAM1; generalize only after multiple overlapping threats exist. Preserve current player and air-to-air balance during SAM1 tests.

**Workflow:** one approved atom → `.patch` → `git apply --check --verbose` and `git apply` → Unity playtest → green push. Older migration notes are in [Docs/Archive](Docs/Archive).
