# Air Combat — current handoff

2026-11-27 | Code baseline `dbbb8a1b7a56d3836f7d17a5ec57ab45ca8d62ad` (IN02 committed). Work on master in small verifiable atoms; review/apply patches locally, compile and Play Mode, then commit green. Unity 6000.6.10f1 / URP. Open existing scenes, never regenerate them.

## Resume here: IN02

1. Open `CombatSandbox.unity`, Play. Saved `CombatSandboxEncounter.Mode = Interceptor` (5). One IN01 V2 interceptor vs existing Kestrel with AC01 and two regenerating AAM; deterministic 600-unit head-on start at Y78. R retries. See [IN02 implementation and playtest](Docs/IN_02_INTERCEPTOR_PASS.md).
2. Test straight flight, early left/right turn, boost and S-turn. Can a novice evade the two missiles and still counterattack during the breakaway? Can the interceptor acquire a maneuvering player without fighter-like agility or unfair snap aim? Record behavior and adjust one thing at a time. Do not add a full enemy airfield before this combat loop is fun.
3. Visual selection: user prefers C, dark graphite, from the four IN01 V2 liveries. **Not yet applied to the production prefab/generator** in the reviewed commit; A slate remains the original. Temporary comparison exists in Sandbox. Apply C coherently to the main palette and editable generator, preserve GUIDs, then remove temporary comparisons/materials only after checking references. Do not remodel accepted V2 just for paint.
4. IN02 is a single pass, no rearm/landing/respawn. Next design after acceptance: real enemy sortie cycle and M03 layout. [M03 working design](Docs/MISSION_03.md).

No direct local Play Mode report for IN02 yet in the available conversation; committed is not the same as gameplay-accepted. No final tuning claims.

## Campaign status

- M01: playable through assisted landing. Three required Watchers, optional escaping Overlord, six Pickets, AC01 only. Final debrief/reward/progression deferred. [Mission01](Docs/MISSION_01.md).
- M02: gameplay design accepted by developer after full-sortie and distributed base assault testing. Keep six Pickets and current BD07 geometry/tuning; no immediate nerfs. V07B organic canyons, real C1/C2 evacuation and convoy deployment. Full sortie victory/RTB/debrief presentation still not finished. Saved Base Assault Trial ON; disable for a normal sortie. [Mission02](Docs/MISSION_02.md).
- M03: design only; no scene. Two waves of hostile strike UAVs, two persistent missile interceptors as proposed optional objective, protected operating enemy airfield, AC01 + AAM. Prototype and balance first.

## Design decisions

One aircraft, one sortie; no extra lives or intermediate checkpoints in current design. Optional objectives may be harder and compete naturally with primary timing. Player can skip optional tasks to complete primary objectives. Numerical evacuation timers stay diagnostic; eventual in-flight tactical radio from a communications/tactical-control character gives actionable warnings rather than exact countdowns. Keep lines short, event-based, non-repetitive; preserve critical warnings when banter is disabled. No radio system implemented here.

Campaign identity: arcade controls, tactical sorties, authored missions, a world with character. Target readable low-poly art and authored reactive details rather than realism. Trident Elite vs Dominion. Final result UI, progression, scoring, audio polish, mission balancing and wider campaign systems deferred.

## Regression gates

Compile and inspect Console; no missing references. IN02: kill before/mid/after salvo, surviving missiles, radar/target marker/props, R during warning and after death/escape; previous Dogfight and Convoy modes remain available. M02: full sortie and Base Assault, C1/C2 loaded/empty outcomes, reserve, SAM arming and pause/R. M01: normal no-debug launch, objectives, optional escape and kill, boundary/terrain, landing, retry. Automated regression suite is still planned, not installed by this doc pass.

Use current scene Inspector values and code over historical documents. [Architecture](GAMEPLAY_ARCHITECTURE.md) | [Combat](GAMEPLAY_COMBAT.md) | [History](Docs/Archive/README.md).
