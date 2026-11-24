# Air Combat — current handoff

2026-11-24 | CLEANUP-02c, based on accepted green `eddf8ba`. Production player prefab composition; pending Play Mode regression. Earlier iterations are archived. Inspector values on committed assets take precedence over code defaults.

## Accepted baseline

- Main map: `ArcadeDesertE1`, authored 2 km desert, fixed-world oblique camera, flight and radar/HUD. WASD variable maneuverability shares one boost resource between fast and tight turns.
- Air test: AC-01 BURST on LMB and AAM on RMB, two independently behaving Tigers. Strong opponents accepted; imperfect gun aim and bounded pressure, independent missile cycles (main scene currently overrides cooldown to 2–3.2s; no tuning change in 02b). Player AAM evasion and Short Vector cue accepted. AC-02 retained but not equipped; powerful, needs a later resource/cooldown tradeoff.
- Ground test (default): AC-01 + direct NURS, one mobile AA patrol. NURS ballistic ground footprint, afterburner spread penalty and tight-turn accuracy reward accepted. Ground destructibility and impact feedback not finished.
- Mobile AA: single silent cold-launch rocket, ejection, visible attitude jets, smooth ignition acceleration, finite horizontal correction. Accepted as an arcade threat, not an exact real-world analogue. Old Mantis tracer and Flak experiments retired. Temporary canister, smoke, launch, ignition and explosion effects need an art/juice pass. Offscreen rise readability remains a known issue.
- Patrol authoring: movable group containing route and unit; draggable points, rounded path, start preview, obstacle check. Existing route and transforms preserved. `AA02_Mantis_Combat.prefab` now owns the motor, launcher and nested visual chassis; the main-map instance keeps its scene route override. New instances need only a scene route assignment (or no route for a stationary unit).

- Tiger: `EF01_Tiger_Fighter.prefab` owns `ArcadeFighterUnit` and the attack settings formerly serialized on the encounter. The unit prepares its flight/health and owns its independent pilot. The encounter still schedules flight decisions before movement and fire after movement; AAM scheduling and registrations remain shared.
- Player: `PlayerKestrel_Combat.prefab` owns the flight, boost, chassis, team, health, radar, trigger and mounted weapon modules. The scene selects and binds one secondary; AC-02 remains disabled. The original `PlayerJet.prefab` is retained for the labs. Spawn pose, camera and flight test settings stay on the scene/session.

## Next atoms (in order)

1. ART-AA-01 — dedicated mobile missile launcher model with authored launch socket, no obsolete gun-turret visuals. Preserve Mantis movement and rocket behavior. First verify CLEANUP-02c in both main-map modes.
2. JUICE-AA-01 — launch smoke, attitude jets, distinct ignition burst, strong smoke trail, explosion and appropriate sound; evaluate in gameplay camera.
3. GROUND-01 — NURS damage and destruction of ground vehicles; then JUICE-NURS-01 for meaningful hit/impact feedback.
4. CONVOY-01 — first mixed mission: moving targets, mobile AA and optional fighter escort. Test the precision-versus-exposure tradeoff; mission completion need not require killing every fighter.

Deferred: enemy aircraft placement/spawn authoring (wait for mission requirements), Attack Intent, AC-02 balance, Q/E selection, wingtip trails, unified visual/audio direction. The two-Tiger test still spawns automatically. No new AI or weapon tuning in cleanup.

## Regression gate

Open main map: no missing scripts/materials, no automatic scene modifications or obsolete install menus. Ground mode: move/rotate patrol, edit points and start preview; launch one rocket per cycle, verify correction and physical hit, reset, NURS and HUD. Air mode: two Tigers, independent guns/AAM, player HP, dual weapons, radar warning, hit feedback and reset. Smoke/FX unchanged in cleanup. Both lab scenes still open and function; no unused-asset deletion without dependency proof. Commit a green only after Play Mode passes.

[Combat rules](GAMEPLAY_COMBAT.md) | [Architecture](GAMEPLAY_ARCHITECTURE.md) | [History](Docs/Archive/README.md)
