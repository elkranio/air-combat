# Air Combat — current handoff

2026-11-26 | End of day, CA05 committed; animation review pending. Unity 6000.6.10f1 / URP. Work on `master` in small verified atoms. Open existing scenes, never regenerate them.

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
| [CA05](Docs/CA_05_DESTRUCTION_PREVIEW.md) | Committed prototype, NOT visually accepted. User has animation notes to discuss next session; details not recorded yet. |

### Resume here next session

1. Play the existing airfield-side `M02 / CA01 flight preview`. Enable `Start Airborne` on `Mission02RouteTrial` for quick Game view passes; Scene view can follow the entire sequence. L cycles Pickup → Empty hover → Loaded hover; K triggers destruction at the current pose; R resets the selected mode and sortie; Tab pauses. No new installer needed. If the preview is missing, use the existing CA02 install menu in saved M02; the comparison preview is not needed.
2. Review the user's animation notes before changing CA05. Validate unloaded and loaded crashes, loss of thrust, falling motion, terrain impact, wreck presentation and separate cargo fall; iterate until accepted. Current CA05 keeps a charred intact hull and intact landed CG01 as visual placeholders. No nacelle separation or real hull/cargo collision. Do not assume the final gameplay cargo outcome from this art test.
3. Only after both destruction variants are accepted: design and integrate actual Carryall pickup/extraction at C1/C2, targetability, damage and cargo/task outcomes. Remove the temporary airfield preview before final runtime composition. Preserve the accepted assets and socket contract. Empty Carryall destruction gives no reward; the two cargo sites are one optional task. Timing, final cargo destruction/recovery rules and mission/debrief consequences remain design work.
4. Balance and playtest the combined convoy / cargo route; return to campaign completion, debrief and progression later.

### Regression gate

- Compile and check Console; inspect all three existing scenes for missing references. For CA05 test K before attachment and during loaded lift/depart, L in both hover modes, R during fall/after impact, and Tab pause. Check there are no duplicate cargo/rotors, residual FX or shared material changes; real C1/C2 and convoy remain untouched.
- M02: launch/Start Airborne, movement, AC-01/NURS, convoy progress/deployment, independent Wardens, radar, map and R; check V06 routes and boundary/terrain safety.
- M01: normal sortie with debug shortcuts off, three Watchers, optional Overlord kill AND escape, RTB/landing, failure and R. Sandbox: all five modes and their independent loadouts, radar, damage and R.
- Review git diff and commit a green after local Play Mode checks; preserve intentional development overrides. Do not treat a committed visual prototype as accepted art.

[Mission01](Docs/MISSION_01.md) | [Architecture](GAMEPLAY_ARCHITECTURE.md) | [Combat](GAMEPLAY_COMBAT.md) | [History](Docs/Archive/README.md)
