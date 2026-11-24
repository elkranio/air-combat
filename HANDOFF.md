# Air Combat — current handoff

2026-11-24 | CLEANUP-01, based on accepted green `7881719` (AA-01e2). This file replaces the chronological handoff; earlier notes are archived. Inspector values on committed assets take precedence over code defaults.

## Accepted baseline

- Main map: `ArcadeDesertE1`, authored 2 km desert, fixed-world oblique camera, flight and radar/HUD. WASD variable maneuverability shares one boost resource between fast and tight turns.
- Air test: AC-01 BURST on LMB and AAM on RMB, two independently behaving Tigers. Strong opponents accepted; imperfect gun aim and bounded pressure, independent missile cycles 8–15s. Player AAM evasion and Short Vector cue accepted. AC-02 retained but not equipped; powerful, needs a later resource/cooldown tradeoff.
- Ground test (default): AC-01 + direct NURS, one mobile AA patrol. NURS ballistic ground footprint, afterburner spread penalty and tight-turn accuracy reward accepted. Ground destructibility and impact feedback not finished.
- Mobile AA: single silent cold-launch rocket, ejection, visible attitude jets, smooth ignition acceleration, finite horizontal correction. Accepted as an arcade threat, not an exact real-world analogue. Old Mantis tracer and Flak experiments retired. Temporary canister, smoke, launch, ignition and explosion effects need an art/juice pass. Offscreen rise readability remains a known issue.
- Patrol authoring: movable group containing route and unit; draggable points, rounded path, start preview, obstacle check. Existing route and transforms preserved.

## Next atoms (in order)

1. CLEANUP-02 — prefabization: reusable player, Tiger and mobile AA composition; scene-specific references bound by mission/session; route groups authorable with minimal setup. Do not change balance or invent a large mission framework.
2. ART-AA-01 — dedicated mobile missile launcher model with authored launch socket, no obsolete gun-turret visuals. Preserve Mantis movement and rocket behavior.
3. JUICE-AA-01 — launch smoke, attitude jets, distinct ignition burst, strong smoke trail, explosion and appropriate sound; evaluate in gameplay camera.
4. GROUND-01 — NURS damage and destruction of ground vehicles; then JUICE-NURS-01 for meaningful hit/impact feedback.
5. CONVOY-01 — first mixed mission: moving targets, mobile AA and optional fighter escort. Test the precision-versus-exposure tradeoff; mission completion need not require killing every fighter.

Deferred: Attack Intent, AC-02 balance, Q/E selection, wingtip trails, unified visual/audio direction. No new AI or weapon tuning in cleanup.

## Regression gate

Open main map: no missing scripts/materials, no automatic scene modifications or obsolete install menus. Ground mode: move/rotate patrol, edit points and start preview; launch one rocket per cycle, verify correction and physical hit, reset, NURS and HUD. Air mode: two Tigers, independent guns/AAM, player HP, dual weapons, radar warning, hit feedback and reset. Smoke/FX unchanged in cleanup. Both lab scenes still open and function; no unused-asset deletion without dependency proof. Commit a green only after Play Mode passes.

[Combat rules](GAMEPLAY_COMBAT.md) | [Architecture](GAMEPLAY_ARCHITECTURE.md) | [History](Docs/Archive/README.md)
