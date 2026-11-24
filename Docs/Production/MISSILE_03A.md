# MISSILE-03a — Tiger missile evasion stress test

Base `97a7438`: WEAPON-04 accepted. Production map only. Player weapons, dual-slot controls, flight, AI steering, labs and prefab defaults unchanged.

The temporary test policy `EnemyMissileStressTest` is bound by `Air01Encounter`. Each Tiger can fire unlimited missiles with independent 2–3.2 s intervals between a launch and the next eligible warning. No shared cooldown or in-flight cap. Acquisition begins 90–300 world units away and within a broad 75-degree forward half-angle; committed launch follows a .8 s warning even if the aircraft turns. The warning is a short triple distress chirp; nearly coincident warnings share a cue. No continuous flight alarm. Both Tigers can launch simultaneously or staggered. Each launched missile is fire-and-forget and retains its target when the shooter dies, until the encounter ends.

| Enemy AAM | Value |
|---|---:|
| Speed | 140 |
| Turn rate | 95 degrees/s |
| Total yaw budget | 120 degrees |
| Lifetime | 4 s |
| Hit radius | 5 |
| Damage | 6 HP |

Same arcade projectile, swept target collision, layered exhaust, mesh shadow and HP feedback as player AAM. No flares, artificial evasion chance or guaranteed hit. Tune via Air01Encounter > MISSILE-03a inspector. Missile Only defaults on to isolate missile damage; turn it off to restore enemy cannon fire. Player guns remain enabled. H overlay shows live missiles and accepted hits / launches while encounter is active; final hits / launches remain visible after defeat or victory until R. R clears missiles, warnings, counters and effects; victory, defeat and teardown cancel outstanding test attacks. No radar or edge icons in this atom.

## Play Mode gate

- Fly without shooting. Compare straight flight, W escape, S+A/D tight turns, early/late reactions, simultaneous and staggered threats. Note whether near misses are reproducible and whether multiple missiles are fun. Check 6 HP damage, warning before every launch (shared cue for coincident launches), trail and shadow.
- R while warning or missiles are active. Confirm no old missiles hit new sortie. Confirm both player weapons and legacy labs unchanged. Kill one Tiger; already launched missiles continue until encounter end. Toggle Missile Only off for a combined test.
- Next: hostile missile radar and offscreen flash indicators; then tune the evasion envelope and replace temporary unlimited policy with normal encounter-level pacing. Q/E reserved for future per-slot cycling. No flares yet.
