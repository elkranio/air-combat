# Art pipeline

Sandbox is the gameplay reference. FrontierStrata (V17) is the active visual playground; Scorched Frontier (V13) is the fallback; gameplay and environment iteration remain separate until the environments are deliberately integrated.

## Practical authoring and bake guide

**Before making a new model or baking a map, read [ART_AUTHORING.md](ART_AUTHORING.md).** It contains the V18–V20 tested low/high-poly recipe, Cycles selected-to-active settings, URP import contract, gameplay-distance A/B checklist and unresolved issues. This file remains the reference for folder ownership and asset lifecycle.

## Layout

- `Resources/Blender/`: editable Blender sources (not imported by Unity).
- `Resources/References/`: raw visual references, not shipped with the game.
- `Tools/Blender/`: reproducible authored asset generators; run from the repo root with `blender -b --python-exit-code 1 --python ...`.
- `Tools/Unity/`: editor-only import, prefab and scene assembly tools. See [ARTLAB.md](Tools/Unity/ARTLAB.md).
- `Assets/_Project/Art/`: authored FBX meshes, textures, materials and prefabs, grouped by Aircraft, Buildings, Nature, Terrain, Weapons and VFX. Frontier-specific aircraft materials live in `Aircraft/FrontierMaterials/`; do not alter Sandbox materials implicitly.
- `Assets/_Project/Art/Environment/FrontierHeroKit/`: V14A reproducible authored FBX modules, base color/local AO/normal PNGs, materials and prefabs. Editable source: `Resources/Blender/FrontierHeroKit.blend`. Keep AO and normal maps separate; world shadows remain dynamic. Kit is used by V17; old showcase is historical. Do not rerun the old full kit in place.
- `Assets/_Project/Generated/WeaponLab/`: separate low-cost 12 km test range scene outputs (small repeat PNG, shared ground mesh, training materials). Generator and controls: [WEAPON_LAB.md](Tools/Unity/WEAPON_LAB.md). Not a replacement for accepted V17 terrain.
- `Assets/_Project/Generated/Battlefield/`: current gameplay landscape; keep until its replacement is tested.
- `Assets/_Project/Generated/ScorchedFrontier/`: reproducible terrain, textures and V0 environment modules. Do not edit a generated prefab by hand without changing its generator, or move a useful authored asset into a disposable output folder.
- `Assets/_Project/Generated/FrontierStrata/`: active V17 terrain, local geology maps, shared ground and road assets consolidated by the V18 cleanup utility. Preserve GUIDs and geometry for V19.
- `Assets/_Project/Art/Environment/FrontierBakeTruth/` and `Generated/FrontierBakeTruth/`: isolated test assets. V18 proved meaningful high-only normal detail is visible at size 80; optional AO. Source `Resources/Blender/FrontierBakeTruth.blend`. Diagnostic import settings are not production defaults.
- `Assets/_Project/Scenes/`, `Scripts/`, `Shaders/`: Unity scenes, runtime components and shaders. Use stable names for accepted baselines; version new experimental scenes only when necessary.
- `Assets/Settings/ScorchedFrontier_URP.asset`: accepted study-local dynamic-shadow settings, temporarily selected in Play Mode. Not the global Sandbox pipeline.
- `Assets/ThirdParty/`: existing dependencies; see [THIRD_PARTY_ASSETS.md](THIRD_PARTY_ASSETS.md). Do not remove until actual replacements are installed.

## Asset rules

Prefer reusable mesh and material references, GPU instancing for dense vegetation, and PNG textures with mipmaps and GPU compression. Avoid per-instance combined mesh exports and uncompressed full-map native texture assets. Inspect generated folder size before pushing. Move Unity assets via AssetDatabase with their `.meta` GUIDs, and check dependencies before deleting retired output. Keep raw models and Blender generators for later reuse.

Commit generated assets and matching scene only after playtest. Retired experiments are recoverable from Git; do not copy huge archived files back into Assets or rewrite Git history.
