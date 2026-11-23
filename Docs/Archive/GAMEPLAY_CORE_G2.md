# G2 — Military Depot (model, Cycles bake, 4 prefab instances)

**Status: green, imported and placed.** The G2 generation/import instructions below are historical provenance. Do not rerun the importer on accepted GroundAttackLab; G3 and G4 have since added damage and weapons. Current state: [HANDOFF.md](HANDOFF.md).

## Actual 3D asset

- `Tools/Blender/military_depot_geometry.py`: deterministic 76 x 54 x 10.16 metre low-poly object. Eight render groups (~409 mostly quad polygons), including concrete bunker, roof trim and equipment, two large fuel tanks, service pipeline, supply crates, blast walls and a UV-mapped roof quad.
- `Tools/Blender/generate_military_depot.py`: builds the low game mesh and aligned physically raised/recessed HIGH roof surface. Uses the existing **V18** `generate_bake_truth_test.truth.bake` implementation for **Cycles selected-to-active tangent-space normals** and **local ambient occlusion projected via emission**. The high object is excluded from the FBX. The maps do not contain baked directional sunlight.
- Output directory `Assets/_Project/Art/Environment/MilitaryDepot/`: `MilitaryDepot.fbx`, `DepotRoof_BaseColor.png`, `DepotRoof_Normal.png`, `DepotRoof_AO.png`, `MilitaryDepot.ready.txt` (marker written **only** after successful bake/FBX/export). Editable source `Resources/Blender/MilitaryDepot.blend`. High roof relief is for the bake only.
- The roof has unique UVs, while other structural details exist as actual low-poly silhouette geometry. Normal/AO are for the roof top **only**; the 8 material groups are not pretending to share an unwrapped whole-model bake.

## Build asset (run from repo root in Git Bash)

```bash
blender -b --python-exit-code 1 --python Tools/Blender/generate_military_depot.py -- --device OPTIX
```

Use `--device CUDA` or `--device CPU` if OptiX is unavailable. Default: 1024 x 1024 maps, 48 Cycles samples; optional `--size 2048 --samples 64`. This is a real local Blender render/bake; a `.patch` alone cannot serialize `.blend` and `.fbx` without running Blender. Do not claim a successful bake unless the marker exists and raw images have signal.

## Import and scene placement (Unity 6.6 / URP)

1. Apply patch, run Blender command, wait for successful completion and Unity AssetDatabase import/compilation.
2. Open **existing** `Assets/_Project/Scenes/GroundAttackLab.unity` in Edit Mode. Save any unrelated changes so the scene is clean.
3. Menu: **Air Combat > Ground Attack > Import Military Depot G2**. Script is inside `Assets/_Project/Editor/` (not under Tools, so menu compiles). It validates the bake marker, FBX render groups, upward roof normals, UV/Mikk tangents and world bounds; imports roof albedo as sRGB, normal as NormalMap/non-sRGB and AO as non-sRGB, with mips and CompressedHQ.
4. The command creates `Assets/_Project/Prefabs/Targets/MilitaryDepot.prefab`, materials in `.../MilitaryDepot/Materials/`, and 4 prefab instances under `Military Depots - G2` in **GroundAttackLab only**. Positions (X,Z): `(0,250)`, `(-310,625)`, `(430,950)`, `(-220,1300)`; distinct headings. Prefab has a broad structural collider but no HP, damage logic or weapon changes.
5. Inspect scene at camera orthographic size **80** and compare the roof's `G2_Roof_BakedPanel` material against `G2_Roof_BaseOnly_AB` using the *same* view, lighting and albedo. Check visible normal detail, no UV seams, no excessive grunge, shadows and identifiable tank/depot silhouettes. Also check at pitch 90/75/65 if useful.
6. Test G1 flight, then WeaponLab and Sandbox. Stage the generated `.fbx`, `.png`, `.blend`, `.ready.txt`, `.prefab`, `.mat` and **all Unity-generated `.meta` files**. Commit only after green. The command refuses to overwrite an existing depot prefab/target group. To iterate, use a **separate new atom**, not casual re-running of the authoring command.

## Deferred to G3

No bombs, NURS, destruction, ground HP, explosions or radar. G3 will attach a generic `DamageableTarget`/ground-domain receiver and implement a gravity bomb + projected impact cue, without requiring the depot model to change.
