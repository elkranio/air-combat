# Air Combat

Unity 6.6 / URP. **Stylized arcade air combat**: strong aircraft silhouettes, direct weapons, overwhelming air/ground combat, loud sound and impactful destruction. Fun and readability take priority over flight simulation realism. Playable prototypes exist; the **main map is not yet a combined combat mission**.

## Start here (current state)

1. [HANDOFF.md](HANDOFF.md) — authoritative current green, immediate next atom and regression rules.
2. [GAMEPLAY_COMBAT.md](GAMEPLAY_COMBAT.md) / [GAMEPLAY_ARCHITECTURE.md](GAMEPLAY_ARCHITECTURE.md) — current weapons, controls, radar and scene ownership.
3. [Docs/Production/TIGER_D1.md](Docs/Production/TIGER_D1.md) — EF-01 Tiger enemy visual chassis, status and intended integration.
4. [Docs/Production/BASE_DESIGN.md](Docs/Production/BASE_DESIGN.md) / [Docs/Production/AIRFIELD_B1E.md](Docs/Production/AIRFIELD_B1E.md) — current base, airfield and B1f hangar.
5. [Docs/Production/MAP_DESIGN.md](Docs/Production/MAP_DESIGN.md) / [Docs/Production/ART_DIRECTION.md](Docs/Production/ART_DIRECTION.md) — world scale, camera and look.
6. [ART_PIPELINE.md](ART_PIPELINE.md) / [ART_AUTHORING.md](ART_AUTHORING.md) — safe asset authoring and Unity import.
7. [LIGHTING_L1.md](LIGHTING_L1.md) / [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md) — lighting caveats and licenses.

## Scenes

| Scene | Role |
| --- | --- |
| `Assets/_Project/Scenes/ArcadeDesertE1.unity` | **Production map / flight only**: 2×2 km Terrain and plateau, B1 barracks/HQ/roads/prepared lot, B1e runway/apron/tower/original hangar, B1f arch-hangar style trial, parked Kestrels and static EF-01 Tiger trial. Y78 flight and fixed-world `ArcadeCameraRig`; no fight session yet. |
| `Assets/_Project/Scenes/WeaponLab.unity` | **Regression lab**: enemy fighter, player air weapons 1–3 and legacy camera. |
| `Assets/_Project/Scenes/GroundAttackLab.unity` | **Regression lab**: player ground weapons 4–6, AA/SAM, radar/HUD and legacy camera. |

## Current gameplay direction

**First** test the player's BURST/autocannon firing **straight along aircraft heading** on LMB, without mouse-directed angular aim. The existing bounded-aim behavior is the baseline to replace/test, **not yet a completed change**. Evaluate it with the **production camera** before committing to further targeting aids. Then build a single-Tiger dogfight with a minimal, useful radar contact; only after it is fun move to several enemy fighters. Keep land combat and full polish out of this immediate atom. Further 2–3-atom details will be decided after this documentation pass.

## Working process

One approved atom → `.patch` → `git apply --check --verbose` → `git apply` → Unity Play Mode → green push. Preserve `.meta` GUIDs and existing scene/prefab references. **Never regenerate the hand-authored main scene** or rerun obsolete installation experiments. Keep historical proposals in Git history; the current state lives in this index and `HANDOFF.md`.
