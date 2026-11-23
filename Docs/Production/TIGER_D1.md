# D1 — EF-01 Tiger enemy fighter (visual chassis)

**Repository state:** `master` `645081ba` contains the new **EF-01 Tiger D1** fighter art, inspired by the recognizable F-5 Tiger II class. It is intended as the **first-wave / entry-level enemy fighter**, with future aircraft allowed to become more advanced and striking. Stylized, not a tiny weak-looking toy or a flight-simulator fidelity exercise.

## What exists

- `Resources/Blender/EF01_Tiger_D1.blend` — editable source.
- `Tools/Blender/build_enemy_tiger_d1.py` — reproducible OBJ-section generator.
- `Assets/_Project/Art/Aircraft/EF01_Tiger_D1/` — 10 model sections and 6 matte URP materials, preserving Unity GUIDs.
- `Assets/_Project/Prefabs/Aircraft/EF01_Tiger_D1_Chassis.prefab` — **visual** `JetChassis` with gun L/R, unused Gauss, missile and engine L/R sockets for future compatibility.
- `Assets/_Project/Editor/InstallEnemyTigerD1.cs` — editor-only guarded initial installer/fallback; a static trial exists in `ArcadeDesertE1.unity` under **`D1 / EF-01 Tiger - VISUAL TRIAL (toggle)`**, placed between B1e parked Kestrels for scale comparison. Do not rerun unless specifically repairing a missing installation.

## Explicitly NOT done

Tiger is **not** currently attached to the existing `EnemyJet` flight/AI/weapons and the main scene has **no live dogfight**. Visual comparison is committed but still needs an explicit Game-view silhouette/scale/material verdict. Do not infer behavior from a `JetChassis` component or a parked scene instance.

## When combat starts

First test the player's BURST/autocannon **fixed straight ahead** under the accepted production camera. Then, if Tiger is visually acceptable, use the existing enemy logic with the Tiger **as its appearance**, one opponent first and several later. Preserve the existing lab EnemyJet/Kestrel regression until new main-map combat is demonstrated. Radar contacts should reflect actual detection, not reveal all enemies automatically; precise radar UX and detection rules will be set before implementation.
