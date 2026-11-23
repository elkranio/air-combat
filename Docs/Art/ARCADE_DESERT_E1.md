# E1 — Vibrant Desert first-flight map

Status: **original E1 scene created and committed**. The bright initial ground and small rocks described below were superseded for the active art pass by [E1b muted ground / chunky outcrops](ARCADE_DESERT_E1B.md). These are historical setup instructions: do **not** rerun the E1 builder on the existing map. Read [visual direction](ARCADE_VISUAL_DIRECTION.md) and [map plan](ENVIRONMENT_MAP_PLAN.md).

## Create once

On a clean committed master, apply the E1 patch, allow Unity to compile, **stop Play Mode and save/discard dirty scenes**. Run:

**Air Combat > Art > E1 - Create Vibrant Desert Map**

It writes the persistent scene `Assets/_Project/Scenes/ArcadeDesertE1.unity` and the accompanying editable TerrainData, TerrainLayer, tiny flat ground PNG, three rock mesh assets and three rock materials under `Assets/_Project/Art/Environment/ArcadeDesertE1/`.

**Do not rerun the builder** once created: it intentionally refuses to overwrite the scene or asset folder, because the next E1/E2 passes must build on the same hand-edited map. Commit the new scene, generated assets **and all Unity `.meta` files** after the Unity playtest. The source C1/C1b, WeaponLab, GroundAttackLab, T1 and V19 scenes/prefabs are untouched.

## What is in the map

- **Terrain:** Unity-native editable TerrainData **2000×2000 world units**, height resolution **513²**, initial geometry **Y=0**. Bounds from -1000 to +1000 X/Z; TerrainData offset -40 and vertical range 180 leave future room for shallow valleys and hills. A single completely uniform RGB ground texture (`#DDA44D`), no noise/height detail or photo maps, matte URP Terrain/Lit, one TerrainCollider. Do not confuse *flat color* with a permanent flat-height constraint.
- **Rocks:** 3 deliberately low-detail angular hand-defined mesh forms (low, upright, slab), 3 warm matte color variants (`#C76D42`, `#E18D48`, `#AC5540`). Just **12 sparse editable groups** across the map (1–3 stones each), no radial procedural scatter or fields of rubble. Meshes have real shadows and MeshColliders, but the Transform-based flight motor does **not** yet crash into geometry; collision/crash handling belongs to the canyon phase.
- **Current PlayerJet:** the **same authored visual/chassis**; no aircraft remodel/paint yet. Boost and Q/E Break Turn work, but no enemies, weapons combat session or HUD are installed in this first art test. Gameplay prefabs are not modified.
- **C3 camera:** existing `HeadingLockedObliqueCameraStudy` in **fixed-world-only** mode, yaw **-45°**, pitch **50°**, ortho **80**, cruise lead **48**, boost extra lead **14** with smoothing and viewport-safe clamp. `C` rotation comparison intentionally disabled in E1 so the world never spins unexpectedly. No old `TopDownCameraFollow`/`CameraLookAhead` double-drive.
- **Lighting:** exactly one authored `CombatLightingRig.prefab`, existing URP shadows, standard Day preset. No second directional sun, old V19 local pipeline/ambient override or postprocess mask. No baked fake shadows.
- **Bounds:** player starts `(0,48,-220)`. Auto reset after `|X|` or `|Z|` reaches **850** (before the map edge); **R** also resets. The camera snaps on reset instead of sweeping across the 2 km map.

## Flight controls / first visual gate

**A/D** turn, **W** afterburner, **Q/E** Break Turn, **R** reset, **H** hide basic overlay. `J/K/L` still control optional 50/58/65-degree camera tilt in the shared C3 study component; default is **50°**. There is deliberately no combat in E1; return to GroundAttackLab/C1b for weapons testing.

Check these from the **Game view while flying**, not from a distant Scene view: saturated ground without gloss/blur, matte rock faces and believable shadows, jet visibility against ochre, shadows at near/far rock groups, space before the aircraft at cruise/boost, and absence of camera roll/yaw-induced nausea. Test map edges and R once. Keep/refine/reject the colors and each rock form **before** increasing dressing density.

## Next after first E1 green

Use this same scene for **aircraft color/silhouette comparisons**, not an isolated Blender beauty render. Later sculpt macro hills/lowlands directly in the saved TerrainData, build actual wall/canyon obstacles, roads and structures. Art/lighting trials must not silently change weapons, flight or shared prefab Inspector values.
