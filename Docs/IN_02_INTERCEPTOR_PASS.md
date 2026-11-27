# IN02 — Interceptor one-pass sandbox

Current committed implementation at `dbbb8a1b`; local behavioral acceptance pending. Open CombatSandbox, Play (saved mode Interceptor = 5). Other enum IDs unchanged. Existing Kestrel (70 cruise / 140 boost), AC01 + AAM, NURS off. Runtime hides IN01 comparison, Mantis/Warden/SAM trial actors only in this mode. Start/R: player `(0,Y78,-300)` northbound, IN01 `(0,Y78,300)` southbound. R resets player pose, health, ammo, boost, actors and warnings. Change modes before Play for other scenarios.

## IN01 visual state

V2 Long Dart: 17.64 length, 10.10 span, pointed fuselage, swept wings, two aft nacelles and canted fins. Four liveries: A slate blue-grey (original), B light titanium/dark wing, C dark graphite (user preference), D dust olive. C is **not yet applied** to main prefab/materials or generator in reviewed commit. Production model `Assets/_Project/Prefabs/Aircraft/IN01_Interceptor.prefab`, art `Assets/_Project/Art/Aircraft/Dominion/IN01_Interceptor/`, temporary `LiveryTrials` and scene root `IN01 / Livery comparison (temporary)` retained. IN02 currently instantiates original prefab. Do not rerun old visual installer blindly. Next visual atom: synchronize C from actual trial materials/authoring palette into original materials, JSON and Blender generator, preserve GUIDs; remove temporary comparisons after verifying references. No need to change accepted silhouette. Exact lost chat wording is unavailable; decision recorded from developer's explicit recall.

Editable source `Tools/Blender/build_in01_interceptor.py`, deterministic `Assets/_Project/Editor/IN01Geometry.json`; generates `Resources/Blender/IN01_Interceptor.blend`. Use Blender's own Python, factory-empty background session; script clears current Blender scene. Unity `IN01InterceptorAuthoring` builds/refreshes linked visual assets. No runtime AI on visual prefab. Nose +Z, up +Y. Existing visual sockets for IN02 derived from `Visual/L missile body` and `Visual/R missile body` mesh centers; fired props hide.

## Current code and tuning

`CombatSandboxEncounter` owns composition and retry; `Scripts/Enemy/InterceptorPassTrial.cs` owns one actor/phase, limited steering and motion. Uses existing `EnemyMissileStressTest` manual salvo API plus existing `MissileProjectile`, radar, warning HUD, targeting and `CombatHitFeedback`. Runtime health/hitbox/team/radar registered on actor, no prefab edits.

| Setting | Value |
| --- | --- |
| Approach / breakaway / return speed | 96 / 55 / 125 |
| Approach / breakaway turn rate | 30 / 32 deg/s |
| Speed response | 45 units/s² |
| Acquisition | 110–300 range, 45° half-angle |
| Approach timeout | 9 s (also abort if close crossing/near edge) |
| Warning / shot gap | .65 / .38 s |
| Minimum breakaway | 2.2 s, until northbound |
| Interceptor HP | 5 |
| AAM speed / turn / budget | 180 / 70°/s / 100° |
| AAM lifetime / hit radius / damage | 4 s / 5 / 5 HP |

Approach steers toward current player position. If acquisition fails, breakaway without fire. Once warning/commit begins, two shots scheduled even if player maneuvers; no new lock required. Second launch then breakaway, slow turn toward north, accelerate and escape beyond test edge. No snap yaw, no invulnerability, no additional salvo/rearm/landing yet. Killing actor cancels pending shot, not airborne missiles. Departing removes actor but leaves live missiles; R restarts. Defeat cleans encounter. Settings above are scene defaults, not final balance.

## Next playtest

- Straight head-on first; then R, vary early A/D, W, S. Log NO LOCK, shots/hits, distance and phase; test whether early turn trivially defeats the approach.
- Evade the salvo and counterattack during slowing turn. Test player AAM and AC01, also before/after turn and chasing on boost. Is the window readable and achievable for a novice? Adjust acquisition/commit/motion only after observing.
- Kill during warning and after shot one; live shot remains dangerous, remaining shot cancelled. R during warning, salvo, escape, death; check radar, target marks, missile props and resource reset. Keep altitude fixed per run. Regress Dogfight/Convoy modes; do not change Tiger tuning.
- No M03 scene or enemy sortie logistics until the combat cycle is accepted. [M03 working design](MISSION_03.md).
