# V2A5 — Single authored escarpment / first art-language test

**Status:** built as a Blender/Unity pipeline atom; **awaiting local Blender and Unity Play Mode check**. V2A4 is green in Git but **visually rejected**: synthetic smooth trench + pyramid-like hills. Its high/low altitude scale is still useful. This atom **does not** remake the entire map.

## Why this exists

A convincing low-altitude arcade rock formation needs discontinuous planes, an intentional cliff silhouette, unique cuts, big shadow-throwing projections and irregular transitions to ground. Detail noise and AO cannot rescue uniform extrusions. Test **one** rock asset against the real aircraft before building a modular mountain kit.

## Asset content

- `Tools/Blender/generate_v2a5_cliff.py` holds **14 manually placed silhouette stations**, distinct uneven face heights, 4 projecting buttresses, 4 *short* isolated ledges and 9 authored talus blocks. The rock is about **380 across, 120 high**, considerably higher than the aircraft study flight height of 48.
- Distinct **Cap / WarmFace / CoolFace / Fracture / Talus** material families. Flat-shaded large facets and cast shadows, not painted sunlight. The cliff includes several useful potential cliff-module prototypes but **is still one large unique hero asset**; do not repeat it across the map.
- A **1024 local Cycles AO map** can be baked on the rock's UV unwrap (no directional sun). AO is optional; high-poly normal baking is intentionally **not** attempted until the form is accepted. The real mesh owns the silhouette and collisions.
- Editable source `Resources/Blender/FrontierV2Cliff.blend`, Unity FBX/AO under `Assets/_Project/Art/Environment/FrontierV2Cliff/`, materials under `Assets/_Project/Generated/EnvironmentV2Cliff/`.
- Dedicated scene `Assets/_Project/Scenes/FrontierV2Cliff.unity`: **copy of green V2A ground**, adds only the single rock and its collider. V17, V2A4, WeaponLab, GroundAttackLab and Sandbox remain independent.

## Build (once)

With Unity closed, from repository root:

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_v2a5_cliff.py -- --device OPTIX
```

Device falls back to CPU if OptiX is unavailable. For **geometry-only** diagnosis, add `--no-bake`, then redo the full build if the form is accepted. The successful run creates `V2A5_Cliff.fbx`, optional `V2A5_Cliff_AO.png`, source `.blend` and `V2A5.ready.txt`. Do not rename material slots: Unity expects the five material groups in source order.

In **Unity Edit Mode**, with saved scenes, run:

**Air Combat > Art V2 > V2A5 Assemble Single Cliff**

This creates a new scene and material assets; the menu **refuses overwriting** an existing V2A5 scene. Do not rerun old V2A4 or V2A scene authoring. Save and commit generated `.blend` / `.fbx` / `.png` / `.mat` / `.unity` and all Unity `.meta` files only after the playtest.

The patch also corrects the **already-committed V2A4** player start Z `-425 -> -300`, and its camera Z by the same +125 so the camera-margin auto-reset no longer loops. No old geometry or material is regenerated.

## Play Mode: pass / fail

1. Launch `FrontierV2Cliff.unity`: player starts at (0,48,-235), within flight boundaries, facing the rock. It should move immediately on WASD; not reset every frame.
2. Use camera **1 / 2 / 3** (90°, 75°, 65°), orbit/fly along the rock, judge at **16:9 / orthographic 80**, then at 720p. Evaluate *real silhouette and rock planes*, not close-up Blender screenshots.
3. Fly straight into the cliff: its `MeshCollider` and `ArtLabTerrainCollision` should show **TERRAIN COLLISION — RESET**, then return to start. If this occurs on spawn, inspect actual rock position/bounds.
4. A/B with previous V2A4. Can one identify a cliff with a deep fractured face, distinct ledges and talus without being told? Does the jet feel low and the cliff physically dangerous? Does the shape look like an irregular natural rock face rather than a flat curtain, repetitive stripes or a collection of cones?
5. If silhouette is still poor, **do not build roads or increase texture detail**. Edit the explicit silhouette/rock panel cages, not the entire world or random Perlin values.

## Known limitations / decision gate

- The mesh is hand-authored by fixed station and cage coordinates but generated/exported by a Blender Python script for reproducibility. It is **not** an artist-sculpted production mesh yet. The next decision is whether its **facet/ledge language** is worth sculpting/refining or whether we need a different rock grammar altogether.
- AO is an enhancement, not proof of art quality; the test still uses V2A's soft ground, so only judge the **cliff**, not the landscape finish.
- Blender and Unity are not installed in the patch-generation environment; scripts/OBJ geometry and patch applicability can be tested here, but FBX import, Cycles AO and Play Mode need local verification.

**Result:** pending screenshot and user verdict (accept / revise / reject).
