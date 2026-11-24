# Air Combat

Unity 6.6 / URP. **Stylized arcade air combat**: strong aircraft silhouettes, direct weapons, overwhelming air/ground combat, loud sound and impactful destruction. Fun and readability take priority over flight simulation realism. The main map has a live two-fighter encounter; combined air/ground missions are not yet integrated.

## Start here (current state)

**Current playtest:** [FLIGHT-02 — variable maneuverability](Docs/Production/FLIGHT_02.md), replacing Q/E with powered W/S modes on the main map. [HUD-04](Docs/Production/HUD_04.md) retained. [GUN-03 — AC-02 continuous cannon](Docs/Production/GUN_03.md) accepted; [AIR-02b pair encounter](Docs/Production/AIR_02.md) retained.

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
| `Assets/_Project/Scenes/ArcadeDesertE1.unity` | **Production map / live air combat**: 2×2 km Terrain and plateau, B1 barracks/HQ/roads/prepared lot, B1e runway/apron/tower/original hangar, B1f arch-hangar style trial, parked Kestrels and static EF-01 Tiger trial. Y78 flight and fixed-world `ArcadeCameraRig`; AIR-02b two live Tigers, player AC-01 and GUN-03 AC-02 playtest. |
| `Assets/_Project/Scenes/WeaponLab.unity` | **Regression lab**: enemy fighter, player air weapons 1–3 and legacy camera. |
| `Assets/_Project/Scenes/GroundAttackLab.unity` | **Regression lab**: player ground weapons 4–6, AA/SAM, radar/HUD and legacy camera. |

## Current gameplay direction

AC-02 continuous fire improved the dogfight. The FLIGHT-01 reversal improved combat; FLIGHT-02 now tests continuous W/S speed and turn-rate control using the same tank. Wheel switches weapons; LMB fires the selected mount. Next separate atom: reduce synchronization and overlap between enemy pilots. Keep land combat and full polish out of this immediate atom.

## Working process

One approved atom → `.patch` → `git apply --check --verbose` → `git apply` → Unity Play Mode → green push. Preserve `.meta` GUIDs and existing scene/prefab references. **Never regenerate the hand-authored main scene** or rerun obsolete installation experiments. Keep historical proposals in Git history; the current state lives in this index and `HANDOFF.md`.
