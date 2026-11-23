# V2A4 — Hand-authored hero gorge (first model-and-bake playtest)

Status: **first pass, needs Blender export and Unity Play Mode approval**. Baseline: green V2A; V2A2/V2A3 were rejected as soft/stair-stepped erosion studies. **Do not re-run V2A/V2A2/V2A3 scene creators.** This is a NEW independent study.

## Visual rule (changed from original V2 plan)

We embrace **arcade low-altitude scale**: buildings, roads, mountains, canyon banks and aircraft must be readable in the same camera. Geology is a designed landmark and a flight hazard, **not** a satellite photograph. We author major forms by coordinates and explicit cross-sections; no Perlin ground, no world-covering blurry albedo. Static AO for local rock crevices is optional polish; cast shadows always remain realtime. Do not bake sun direction into base colors.

## Actual authored asset

Blender script: `Tools/Blender/generate_v2a4_hero_landscape.py`.

- One physically deep S-shaped gorge across 1200 x 1200 units, built from **10 hand-placed river stations** and hand-authored lateral rock-wall profiles. **Rim ~18–24, bed -30…-42**: roughly **50–65 units** of relief (the wall is largely concentrated on the inner band). A wide flat channel bed is a legible combat landmark. No three repeated parallel gullies.
- Three deliberately asymmetrical polygonal mountain landmarks: **WestMassif ~112**, **EastMassif ~106**, **NorthSpine ~77** top altitude. The jet study flies at **Y=48**. Physical rock masses therefore project above the plane and become meaningful obstacles.
- Each rock is one editable low-poly mesh with 3 semantic material bands (cap, face, talus), uniquely UV-unwrapped for the optional 1024 Cycles **local AO** bake. One gorge mesh uses 4 explicit colored bands (open ground, wall, bed, apron). Both shadows and silhouettes come from real geometry, not a height-gradient albedo trick.
- Authored FBX, AO PNGs and editable `.blend` produced in `Assets/_Project/Art/Environment/FrontierV2Hero/` and `Resources/Blender/FrontierV2Hero.blend`. Generated Unity study materials in `Assets/_Project/Generated/EnvironmentV2Hero/`. Scene is `Assets/_Project/Scenes/FrontierV2Hero.unity`.

**Scope limit:** the ground is intentionally not yet photorealistic. No roads, buildings, material noise, normal-map detail, fog, heavy post or finished collision/damage feedback. We must confirm *shape and scale* first. If the new gorge reads as a flat painted stripe or an artificial cut, reject the form and adjust the authored stations, not the blur/sharpening.

## Build — two explicit steps

1. Install/open Blender on your machine (the established project pipeline uses Blender). From **repo root**, with Unity closed:

   ```sh
   blender -b --python-exit-code 1 --python Tools/Blender/generate_v2a4_hero_landscape.py -- --device OPTIX
   ```

   GPU selection is best-effort: if OptiX is unavailable the script falls back to CPU. To isolate GPU trouble only, add `--no-bake` and then re-run without it once the geometry is accepted. Blender writes **4 FBX**, **3 AO PNG**, source `.blend`, and `V2A4.ready.txt` only after successful export. This models real cliffs; Blender AO is supplementary, not a substitute for geometry.

2. Open Unity 6.6 (Edit Mode, save any current scene), allow model import. Run:

   **Air Combat > Art V2 > V2A4 Assemble Hero Gorge**

   This copies **V17 only** to a brand-new scene, removes V17 art **only from the copy**, imports the authored FBX modules and builds real `MeshCollider`s, local URP materials, real shadows and an art-only collision reset. The menu refuses to overwrite an existing `FrontierV2Hero.unity`. No changes to the accepted combat, Sandbox or V17 scenes, and no modifications to their shared prefabs.

## Playtest / pass-fail

- Play `FrontierV2Hero.unity`. **WASD** flies as in existing art studies, **Space** boosts, **1/2/3** changes camera 90/75/65°, **H** hides legend, **R** resets. Test at **720p and 1080p** ortho size 80.
- Start over the central canyon, see **both banks** at gameplay zoom. Real sun shadows should separate walls, ledges, open ground and deep bed. Fly along the S bend; edge shapes should not become three parallel stripes.
- Turn left/right out of the gorge toward the modeled mountains. **Flying into rock must reset the aircraft** and show a brief collision indication. This is an art-study height probe against real `MeshCollider`s (centre + two wing probes), **not yet game crash physics**. It does not modify flight or combat scripts.
- A/B with V2A and reference image: do we now read gorge, cliffs, mountains without looking at the legend? Are they in the *same perceived scale* as future buildings? Are rocks too faceted or too generic?
- Accept/revise/reject based on Unity screenshots from **same camera** and a short flythrough. Record failures and asset sizes. Only on green do we move to material/road/ruin detail. Blender + Unity Play Mode validation needs the local machines; a successful patch/application is not proof of either.

## Known limitations

- The Blender generator has been Python syntax-checked and its geometry preview exported as OBJ, **but Blender/Unity are not available in the patch creation environment**. If Blender API or FBX material layout differs on the local version, report the Console/error; do not silently change the already committed V17 scene.
- AO is not directional sunlight; it may barely show at flight distance. If AO does not help, omit it rather than increasing dark blotches. Current terrain uses flat URP colors deliberately to make macro shape failures visible.
- Collision is a cheap ray-down height probe; it will detect low overflight or mountain overlap but not side-on wing/hull mesh penetration perfectly. A future actual gameplay pass can use moving hitboxes/sweeps only when the art shape is accepted.
- Previous V2A2/V2A3 files can remain locally while testing; **V2A4 adds new paths only**, so no destructive overwrite or dependence on uncommitted scene authorship.
