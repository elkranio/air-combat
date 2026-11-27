# Air Combat — current handoff

2026-11-27 | CA05 hull crash accepted; CA05B cargo breakup trial pending local acceptance. Unity 6000.6.10f1 / URP. Work on `master` in small verified atoms. Open existing scenes, never regenerate them.

## Where we are

- Mission01: authored Lynx sortie with takeoff, terrain/boundary failure, tactical map, six live Pickets, three required Watchers, optional escaping Overlord, RTB and assisted landing. `Landed` exists; full debrief/reward/progression is not implemented. Debug overrides are development-only; turn them off for normal regression.
- CombatSandbox: isolated Dogfight, GroundAttack, Interception, Warden and Convoy modes. Shared flight, weapon, radar and target feedback regression remains here.
- Mission02 is the active development scene, not registered in Build Settings. M01 and CombatSandbox remain the two build scenes.
- Shared flight session, scripted takeoff/landing, safety, objective state, target effects and HUD presentation are separated. CLEAN-03/04 cleanup is historical; see [art pipeline](ART_PIPELINE.md). No automatic deletion policy.

## Mission02 — current work

Open `Assets/_Project/Scenes/Mission02_ConvoyInterdiction.unity`. Current route trial uses the existing airfield, northern convoy route from A to B, six mobile Pickets, two independent Warden escorts, AC-01 and NURS, and visual depot deployment. V06 asymmetric canyon pockets C1/C2 contain one armored CG01 each. Both belong to one optional task; neither the cargo extraction deadlines nor campaign scoring are implemented. See [V06 layout](Docs/MISSION_02_LAYOUT_V06.md), [CG01](Docs/CG_01_SPECIAL_CARGO.md). The original route graybox and sandbox CV docs are historical setup/iteration records, not current installation instructions.

| Atom | Status |
| --- | --- |
| [CA01](Docs/CA_01_CARRYALL.md) | Accepted V8 shape + V9A+ Heavy Duty paint. Frozen for v1.0; comparison stands removed. |
| [CA02](Docs/CA_02_FLIGHT_PREVIEW.md) | Accepted counter-rotating blades and basic flight presentation; original circle superseded by CA03. |
| [CA03](Docs/CA_03_CARGO_PICKUP.md) | Accepted v0: Approach → Align → Descend → Clamp → Lift → Depart. Rigid four-socket capture. Timing may be tuned in mission context. |
| [CA04](Docs/CA_04_PICKUP_VFX.md) | Accepted through C: thrust, subtle unified ground wash and clamp pulse. Dust polish deferred. |
| [CA05](Docs/CA_05_DESTRUCTION_PREVIEW.md) | Hull crash accepted as good enough; polish deferred. CA05B: CG01 tips, breaks on impact and leaves temporary debris. Cargo breakup pending Play Mode acceptance. |

### Current work / next atoms

1. Play the existing airfield-side `M02 / CA01 flight preview`. Enable `Start Airborne` on `Mission02RouteTrial` for quick Game view passes; Scene view can follow the entire sequence. L cycles Pickup → Empty hover → Loaded hover; K triggers destruction at the current pose; R resets the selected mode and sortie; Tab pauses. No new installer needed. If the preview is missing, use the existing CA02 install menu in saved M02; the comparison preview is not needed.
2. Accept CA05B loaded cargo breakup and verify unloaded kill leaves grounded cargo intact; test R mid-fall and during debris cleanup. Hull crash is already good enough; do not reopen hull animation polish now. No nacelle separation or physical hull/cargo collision.
3. CA06: opt-in stationary firing stand near the airport. Freeze Lynx at a suitable firing position and heading (keep real weapon controls and projectiles); CA01 holds above CG01 at cruise altitude, manual descent command, R repeat. Tune actual AC01 hitboxes and NURS impact/overlap against cargo across phases. Before attachment, cargo destruction aborts pickup and the empty carrier leaves. After attachment, cargo destruction also kills the carrier; AC01 kill of a loaded carrier causes separate cargo fall and breakup. Avoid invulnerability gaps or unlimited vertical ground blast; establish actual weapon/height envelope on this stand. CA06 remains unimplemented.
4. Only after these outcomes are accepted: integrate real pickup/extraction at C1/C2, targetability and mission task outcomes. Remove temporary airport preview before final composition. Preserve accepted assets and socket contract. Empty carrier destruction gives no reward; both cargo sites are one optional task.
5. Balance and playtest the combined convoy / cargo route; return to campaign completion, debrief and progression later.

### Regression gate

- TEST01 (planned): introduce Unity Edit Mode / Play Mode regression tests and local runner; map integrity, socket/prefab checks, reset and phase outcomes first. No test infrastructure added by CA05B.

- Compile and check Console; inspect all three existing scenes for missing references. For CA05 test K before attachment and during loaded lift/depart, L in both hover modes, R during fall/after impact, and Tab pause. Check there are no duplicate cargo/rotors, residual FX or shared material changes; real C1/C2 and convoy remain untouched.
- M02: launch/Start Airborne, movement, AC-01/NURS, convoy progress/deployment, independent Wardens, radar, map and R; check V06 routes and boundary/terrain safety.
- M01: normal sortie with debug shortcuts off, three Watchers, optional Overlord kill AND escape, RTB/landing, failure and R. Sandbox: all five modes and their independent loadouts, radar, damage and R.
- Review git diff and commit a green after local Play Mode checks; preserve intentional development overrides. Do not treat a committed visual prototype as accepted art.

[Mission01](Docs/MISSION_01.md) | [Architecture](GAMEPLAY_ARCHITECTURE.md) | [Combat](GAMEPLAY_COMBAT.md) | [History](Docs/Archive/README.md)
