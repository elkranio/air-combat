# Frontier art authoring — working playbook

This is the **practical source of truth** for making new environment assets for Unity Air Combat. Read it before writing a new Blender generator or importing an FBX. For directory ownership and cleanup rules see [ART_PIPELINE.md](ART_PIPELINE.md); for scene commands and history see [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md). The actual V18–V20 generator scripts remain executable examples, not universal templates.

**Status:** proven workflow from V18 Bake Truth and V19 Practical Bake; stylistic calibration from V20. A successful technical bake does **not** imply an accepted art style. Scope is environment/hero props, not a mandated approach for every aircraft or VFX asset.

## 1. Visual target and evaluation camera

- Readable, slightly stylized **gritty arcade air combat**, not hyperrealism, generic glossy mobile art, noisy photogrammetry or deliberately faceted low-poly cliffs. Warm muted ochre terrain, weathered industry, cool blue aircraft. Open negative space is important: the aircraft and action must win the contrast hierarchy.
- Judge first in **Play Mode at the actual orthographic size 80**, using pitch 90/75/65 degrees, moving aircraft and dynamic shadows. Close-up previews are debugging tools. Shapes surviving at gameplay scale are more valuable than beautiful details in Blender.
- The environment study is approximately 1200 x 1200 world units. The flight study holds the aircraft near Y=48. Keep authored terrain formations safely below the flight plane; V17 terrain validation used a top-height limit below 40. Do not apply this limit blindly to different scenes or asset categories.
- **Geometry owns:** silhouette, meaningful overhangs, large roof breaks, distinct boulders, occlusion and actual cast shadows. **Normal maps own:** visible-but-not-silhouette surface depth (panels, bevels, vents, chips, strata). **Albedo owns:** material color, dirt, rust and wear. **AO owns:** optional local crevice modulation. Avoid baking sunlight, cast shadows, gradients suggesting a fixed sun angle or world ambient into base color.
- Avoid excessive visual frequency. Repeated parallel wrinkles, radial summit fans and long winding fractures looked artificial. V20's finite, directional sandstone shelves worked better; **V20 Beds 0.45 (B mode 4)** is the current preferred *single-mesa* look; **V19 0.40 (mode 3)** is a softer fallback. Neither is yet a blanket setting for all rocks.

## 2. Decide whether a bake is justified

1. Block out the **game mesh (low-poly)** in metres; inspect silhouette from size 80. If a large feature needs to change the outline or cast a real shadow, model it in low-poly. Do not hide missing silhouette in a normal map.
2. Make a **high-poly / high-detail source** that really differs from low-poly. V14A mostly added ~6–9 cm bevels: little was visible at gameplay distance. V18's deep industrial recesses, raised rails and large fasteners were clearly visible even on a low-poly quad. Design the *frequency and depth* for screen-space visibility, not for polygon count.
3. Keep low and high **aligned, unrotated, same coordinate basis** during the bake. Plan clean UVs without overlaps for unique projected detail; protect borders/seams from leakage. Normal maps cannot fix misaligned UVs or missing tangent data.
4. Start with **base color + normal**. Add AO only when comparing two otherwise identical materials shows a useful result. In V18/V19 AO-only was subtle, while normal-only was decisive. That is observed behavior under our lighting, not proof AO can never help.
5. If the asset is just a distant silhouette, a well-designed low-poly mesh and albedo may be cheaper and better than a complex bake.

## 3. Reproducible Blender workflow

Source code under `Tools/Blender/`; editable `.blend` under `Resources/Blender/`; exported Unity-facing FBX/PNG under a dedicated `Assets/_Project/Art/Environment/<AssetSet>/` folder. Keep high-poly **out** of the game FBX unless explicitly needed. Prefer distinct, reusable models; do not export many redundant whole-world meshes.

**Reference implementations:**

- `Tools/Blender/generate_bake_truth_test.py` — simplest proven high-to-low projection and diagnostic controls. `truth.bake` uses Cycles selected-to-active, high selected + low active; `NORMAL` (tangent space) or `EMIT` after wiring local AO into emission. PNG utility and device selection live here.
- `Tools/Blender/generate_frontier_practical_bake.py` — practical 30 x 20 refinery roof and BrokenMesa detail. Produces base color, normal, optional AO, source `.blend`, roof FBX and a completion report.
- `Tools/Blender/generate_frontier_mesa_polish.py` — **one** restrained, directional normal map on existing BrokenMesa geometry; no AO bake.

Run from the **repository root**, for example:

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_practical_bake.py -- --device OPTIX
```

Use explicit `--device CUDA` or `--device CPU` if OptiX is unavailable. The V19 script defaults to 2048 maps / 48 samples; V20 defaults to 1024 / 48 samples and accepts `--size 2048`. These are **tested starting values**, not mandatory sizes for every module. Bake at production-like settings before judging whether texture detail survives compression and mipmaps.

**Technical checklist:**

- Export normals in **tangent space** with a consistent axis convention. Keep high/low transforms aligned and a valid UV/tangent basis on the low mesh. The Unity FBX importer will calculate Mikk tangents in the tested pipeline.
- Select-to-active ray distance / cage extrusion must cover high-only relief without hitting unrelated parts. V18 uses cage extrusion 1.25 and max ray distance 3.0 for its flat panels; **recalibrate for a different shape**, especially complex overhangs. Do not blindly copy these values into a mesh that can self-project.
- For projected AO, use a **local** Ambient Occlusion node feeding emission on the high source (as in V18); do not accidentally use a fixed directional light or final beauty render as the AO map. AO texture should be non-sRGB in Unity.
- Print useful diagnostics: geometry bounds, normal XY strength percentiles, AO range if baked, expected outputs and a completion marker. Inspect raw normal RGB (purple/green/cyan relief) and raw AO if a mode appears ineffective. Check for reversed bumps, seams, dark projection streaks or empty maps.
- Complete marker is written **only after all exports succeed**. Windows newline handling matters: write explicit UTF-8 LF bytes or use `File.ReadAllLines`/equivalent in Unity. V18 failed before the CRLF fix; never bypass file validation.
- No sunlight baked into textures. Keep Blender source editable and regenerate the entire asset set deterministically; avoid manual undocumented edits to generated Unity outputs.

## 4. Unity 6.6 / URP import contract

The current editor patterns are in `Tools/Unity/ConfigureFrontierPracticalBake.cs` and `ConfigureFrontierMesaPolish.cs`. Use a **new scene copy** of the latest accepted base; do not regenerate or destructively edit `Sandbox.unity`, `FrontierStrata.unity`, `ScorchedFrontier.unity` or the old hero kit to test an iteration.

| Map / asset | Tested import / material behavior |
|---|---|
| FBX low mesh | Import mesh normals, `ModelImporterTangents.CalculateMikk`, disable animation/cameras/lights where not needed; verify UV/tangents and physically correct bounds/axes |
| Albedo PNG | Texture type Default, sRGB **on**, mipmaps **on**, trilinear sampling, GPU compression; URP Lit `_BaseMap` |
| Tangent normal PNG | Texture type NormalMap, sRGB **off**, `convertToNormalmap=false`, mipmaps **on**, trilinear, production `CompressedHQ`; `_BumpMap`, `_NORMALMAP`, `_BumpScale` |
| AO PNG (optional) | Texture type Default, sRGB **off**, mipmaps on; `_OcclusionMap`, `_OCCLUSIONMAP`, `_OcclusionStrength` |
| Material | URP Lit, neutral metallic/roughness appropriate to the surface. Duplicate materials for comparison; **never mutate shared production materials** when cycling modes |

V18 diagnostic uses **uncompressed** maps on purpose to isolate shading/import issues; **do not copy that setting into production by default**. Verify normal map compression visually before committing. The baseline V19 import uses `CompressedHQ` and max texture size 2048; V20 normal uses a 1024 source by default.

**Special case: V17 integrated terrain.** BrokenMesa is **one material submesh within one shared terrain mesh**, not a separate boulder. Its world-to-local UV scale and offset must be reproduced on normal/AO maps (`_BumpMap` / `_OcclusionMap`) as well as `_BaseMap`. V19/V20 explicitly validate BrokenMesa's UV transform and set identical map scale/offset; otherwise the bake appears in the wrong place. Do not replace the whole 4K ground map or terrain mesh to test one rock. Existing slopes already have geometric normals: V19/V20 bake **additive local detail on a planar target**, fading to zero toward talus and patch boundaries, rather than baking the entire slope a second time. This is a pragmatic visual technique, **not** a general-purpose correct high-to-low bake of arbitrary terrain.

Validate dependencies/scene state before changing anything: Play Mode off, scenes saved, completed marker and files present, expected source scene and specific renderers/material slots found, UV/mesh bounds valid. Preserve assets' `.meta` GUIDs across moves. Use real dynamic shadows; no baked world shadow blobs.

## 5. Visual acceptance test (mandatory)

A bake is **technically valid** only if its raw maps contain signal and the effect visibly toggles under controlled lighting. It is **artistically accepted** only if it improves the scene from normal flight distance.

1. **Identical comparison:** show base only vs normal only; optionally AO only / full. Keep the same mesh, albedo, lighting, angle and camera. A label changing is not proof the renderer changed.
2. **Close and gameplay:** inspect once close-up for bake faults, then at orthographic size 80 with moving aircraft. Check 90/75/65-degree pitch and look at how small the detail becomes, including mip/texture compression.
3. **Silhouette & composition:** the detail should support geological/industrial structure without stealing attention from the jet. Watch for vein-like long cracks, excessive noisy stipple, contour rings, over-contrast and an obvious join with the ground.
4. **Shading:** compare actual terrain shadows with and without the detail; do not confuse cast-shadow quality with normal map quality. Keep material color constant during A/B.
5. **Scene safety:** roads, yards, utility structures, other rocks, flight altitude, camera and build scenes stay unchanged in a focused art atom. Check asset sizes and Git status after generation.
6. Record **accepted / rejected / unresolved** results in `HANDOFF.md`; push a tested green before using the result as a new baseline. Old study scripts and generated intermediate assets can be retired after dependency-checked cleanup, not by blind deletion.

### When the result is invisible

Check in this order: (a) texture generated and meaningful raw RGB/AO, (b) Unity import types/sRGB, (c) mesh UVs and tangent space, (d) material texture slots and keywords, (e) variant actually assigned to the renderer, (f) lighting, camera and object size, (g) whether high-only relief was too small. V18 isolated these issues using a 2-triangle plane, obvious high-only geometry, raw-map modes and synthetic control textures; reproduce that test before rewriting the asset pipeline.

## 6. Still open — do not mistake for standards

- **Shadow degradation at screen edges** was observed in V20; the precise cause is **not diagnosed**. Test cascades, splits, shadow distance, resolution, shadow bias and camera framing using identical screenshots and a stable GPU profile. Increasing distance alone can lower texel density. No final cascade/resolution preset is accepted yet.
- **Global clarity / texture filtering**, lighting palettes and atmospheric haze need controlled same-camera A/B experiments. Do not compensate for soft input art by adding indiscriminate sharpening or excessive normal detail.
- **V20 Beds 0.45** is preferred for BrokenMesa and **V19 0.40** remains the softer alternative, but the pattern is not propagated to all geological masses. Building bake is technically functional; a final refinery art language is not yet locked.

## Quick copy-forward checklist

- [ ] Silhouette readable at gameplay size 80; hero aircraft remains primary.
- [ ] Low vs high difference is purposeful and high-detail is not exported into the game mesh.
- [ ] UV orientation, axis convention, Mikk tangents and bake projection verified.
- [ ] Base / normal / optional AO maps separate; no sun shadows baked.
- [ ] Blender script exports FBX/PNG, editable `.blend`, statistics and a completed marker.
- [ ] Unity uses sRGB only for albedo, normal import for normals, correct keywords, mips and production compression.
- [ ] Compare identical meshes at gameplay distance; retain raw-map debugging path.
- [ ] New scene/asset set only; preserve shared materials, terrain, roads, meta GUIDs, original scenes.
- [ ] Log art verdict, asset size and tested green in `HANDOFF.md`.
