# Air Combat

Unity 6.6 / URP. **Stylized arcade air combat**: strong aircraft silhouettes, direct weapons, overwhelming air/ground combat, loud sound and impactful destruction. Fun and readability take priority over flight simulation realism. The main map has a live two-fighter encounter; combined air/ground missions are not yet integrated.

## Start here (current state)

**Current playtest:** [AI-04a — powered gun pursuit](Docs/Production/AI_04A.md). [MISSILE-04b](Docs/Production/MISSILE_03B.md#missile-04b--combined-combat-pacing) missile tuning retained. Short Vector and existing missile evasion tuning accepted; Tigers fire AC-01 and unlimited AAM with independent 8–15 s cooldowns; missile-only mode removed. Production missile shadows disabled. [WEAPON-04 — dual weapon slots](Docs/Production/WEAPON_04.md), [MISSILE-02](Docs/Production/MISSILE_02.md) 150 ms lock and [JUICE-02c](Docs/Production/JUICE_02C.md) retained. Tigers now use shared-fuel tight turns and closer-range boost, maintain useful gun pursuit and allow a wider gun firing angle. Next compare gun pressure, then attack intent. Unified art direction and wingtip trails deferred.

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
| `Assets/_Project/Scenes/ArcadeDesertE1.unity` | **Production map / live air combat**: 2×2 km Terrain and plateau, B1 barracks/HQ/roads/prepared lot, B1e runway/apron/tower/original hangar, B1f arch-hangar style trial, parked Kestrels and static EF-01 Tiger trial. Y78 flight and fixed-world `ArcadeCameraRig`; AIR-02b two live Tigers, player AC-01 / AAM dual-slot playtest. |
| `Assets/_Project/Scenes/WeaponLab.unity` | **Regression lab**: enemy fighter, player air weapons 1–3 and legacy camera. |
| `Assets/_Project/Scenes/GroundAttackLab.unity` | **Regression lab**: player ground weapons 4–6, AA/SAM, radar/HUD and legacy camera. |

## Current gameplay direction

FLIGHT-02 W/S control and AIR-03 independent Tigers are accepted. LMB fires AC-01 BURST; RMB fires AAM on full lock. Both slots are independent and visible on the HUD. AC-02 continuous is preserved but unequipped; mouse wheel is unbound on the production map. JUICE-01/02 effects retained.

## Working process

One approved atom → `.patch` → `git apply --check --verbose` → `git apply` → Unity Play Mode → green push. Preserve `.meta` GUIDs and existing scene/prefab references. **Never regenerate the hand-authored main scene** or rerun obsolete installation experiments. Keep historical proposals in Git history; the current state lives in this index and `HANDOFF.md`.
