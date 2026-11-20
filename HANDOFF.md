# Air Combat — handoff

## Current direction (accepted)

**Scorched Frontier (V13)** is the accepted visual baseline. User flight test: dramatically better than the forest variants; aircraft reads well in motion, including over ruins. Foundation works; future art polish is optional and incremental. Keep cold blue aircraft over warmer ochre clay/dust, large open spaces, ridges and localized abandoned infrastructure. Avoid dense all-over detail, unnecessary blur/outline and excessive orange saturation.

Current session: user wants relaxed visual experiments for enjoyment after project housekeeping (not a gameplay sprint yet). Do not silently switch to combat work or overhaul the accepted style.

## Workflow (required)

- Deliver every code atom as an actual downloadable `.patch` attachment, not inline diff or instructions to save one manually. Generate with Git; validate `git apply --check` against exact touched source blobs from the green commit before sending.
- User runs Git Bash on Windows. Give exactly two apply commands with the filename: `git apply --check --verbose ~/Downloads/<name>.patch` and `git apply ~/Downloads/<name>.patch`. Give any Unity generator command separately.
- Stop Play Mode, save open scenes and let Unity refresh/compile before invoking generators. User playtests, then pushes green. Base next atom on that commit; do not write directly to master.
- Keep this file updated in each atom. Record accepted results separately from hypotheses and pending tests. Preserve source assets and keep generated outputs small; never rewrite Git history for cleanup.

## Active project

- Unity 6.6 / URP. `Sandbox.unity`: working flight, bank, afterburner/VFX, enemy pursuit, target lock and missiles/respawn. Only Sandbox is in build settings. No gameplay changes in the visual track.
- `ScorchedFrontier.unity`: visual flight playground, formerly ArtLab V13. Static 1200 × 1200 badlands, size 80 ortho camera (default 65 degrees), test-only fixed altitude 48, existing Kestrel and flight tuning. WASD/Space, 1/2/3 camera, R recenter (also automatic near edge), H legend. No combat.
- Real directional shadows (38-degree sun, strength .75, lighter neutral tint), study-local URP asset. Preserve these settings; no baked/blob shadows for Frontier.
- Terrain: elongated ridges, basins, craters, road corridors and settlement pads. 2048 PNG color map, mipmaps and GPU compression. Five sites: refinery, village, industrial yard, transmission station, checkpoint; main highway and spur. V0 reusable prefab modules: ruined factory, house, steel frame, tank, chimney, pylon, barrier, stump, three rocks. Shared meshes/materials. No destruction/collisions/target logic.
- Read [ART_PIPELINE.md](ART_PIPELINE.md) and [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md) for exact layout and generator commands.

## Cleanup — tested / green

User pushed green after the Scorched Frontier cleanup (`bbd67fe38ed051cba868629d709e0363ef5d8ceb`). Stable `ScorchedFrontier.unity`, dedicated URP asset and Frontier aircraft materials are now independent of retired studies. Old forest scenes are recoverable in Git; no history rewrite. Sandbox remains unchanged and the only build scene.

## V14A Hero Refinery + Rock Kit — tested / green

The user wants enjoyable art experiments rather than gameplay work today. Focus on a single better refinery and rock formations, not the whole map. Keep the accepted V13 palette/camera/flight and real dynamic lighting. No world/terrain shadow baking.

- `Tools/Blender/generate_frontier_hero_kit.py`: deterministic kit of nine rock variants (three large rocks, two boulders, two ridge chunks, two cliff walls) and eleven refinery modules (main block, steel frame, chimney, large/small tanks, two pipe racks, control hut, barriers, concrete/metal debris). Models have chunky stratified shapes, major damaged silhouettes, actual roof openings and bevels, broad wear. Source file `Resources/Blender/FrontierHeroKit.blend` keeps editable low and bevel-derived high meshes. Export one FBX submesh per module with uniquely unwrapped UVs and per-module PNGs. No baked sun, world AO or directional shadows.
- Default Blender full bake uses Cycles OptiX on the user's GPU; 1024 maps, 2048 for four hero modules, 48 samples; local AO and tangent-space normals are separate from base color. `--quick` generates 512 base only; `--device CUDA` / `--device CPU` are explicit alternatives. User completed the GPU bake and Unity import.
- `Tools/Unity/ConfigureFrontierHeroKit.cs`: imports maps with correct color space/mips/compression; reusable materials/prefabs under `Assets/_Project/Art/Environment/FrontierHeroKit/`. Copies tested `ScorchedFrontier.unity` to `FrontierHeroShowcase.unity`, replaces refinery only in the copy, adds three rock formations nearby. Start above the refinery approach; fly north to rocks. Existing field, camera and aircraft unchanged; no combat or collisions. B switches base only / AO / normal / both, H toggles legends, controls and real shadows unchanged. Neither Sandbox nor stable Frontier is edited.
- User test: new refinery geometry is an improvement; AO/normal modes show almost no discernible difference at gameplay distance. Tank surfaces exhibit possible bake/texture stipple; cause unverified. Geometry and composition matter more than additional high-poly baking. Do not rebake or promote any material mode yet. V14A green `cc8b3dd7`.

## V14B Surface & Composition — tested / green

Copy green `FrontierHeroShowcase.unity` (not V13), keeping new refinery, rocks, camera/flight, bake comparison and dynamic shadows. Improve the previously blurry ground and sterile roads; do not add more small props or change terrain geometry. Create `FrontierSurfacePass.unity` with scene-local output in `Generated/FrontierSurfacePass/`.

- Upsample the existing ground color map to a 4096 PNG and add restrained world-scale crust/grit, 13% desaturation, concrete slab seams and localized oil/dust wear at the actual refinery footprint; compacted dirt pads at other sites. No painted lighting or floating surface patch meshes.
- Keep original road ribbons and gaps; clone only four small road/shoulder meshes to add strip UVs. Worn asphalt and dust shoulder PNGs with mipmaps and GPU compression. Existing road markings and junction geometry unchanged. No edits to shared materials or V14A/Sandbox/Frontier.
- Existing B comparison remains available; compare ground and roads at the same bake mode and camera angle. No Blender re-run. Generate after saving scenes and stopping Play Mode:

`unity command script run --file Tools/Unity/ConfigureFrontierSurfacePass.cs --entry ConfigureFrontierSurfacePass.Configure`

User test: ground and worn roads noticeably improved; refinery now sits on a coherent pad. Palette and detail accepted for continued work. Aircraft still reads well. Concentric height-driven stripes on the smooth hills are the main outstanding problem. V14B green `e22e7022`. Keep scope visual only.

## V15 Badlands terrain — rejected, cleaned locally

Global procedural height replacement caused floating rocks (moved parent groups, not individual objects), a black terrain/rendering artifact of unconfirmed cause, and loss of the V14B ground detail. Native raw color texture also violated the PNG workflow. User cleaned the untracked V15 files; no commit or push. Do not regenerate or use V15 as a source.

## V16 Rocky escarpments — technically green, visual study not accepted

Start from green `FrontierSurfacePass.unity`. Four localized replacements rather than another global heightfield: a broken mesa north of the refinery, an eastern escarpment, a southern split ridge and a western butte. Real closed Blender meshes with broad eroded silhouettes, steep walls, buried footings and subtle shared albedo swatches; no AO/normal/sun bake. Keep the 48-unit flight plane; each cliff top stays below 40.

- `Tools/Blender/generate_frontier_escarpments.py` creates four FBXs, one small palette PNG and editable `Resources/Blender/FrontierEscarpments.blend`. No Cycles run needed; prior bake comparison showed negligible gameplay-distance benefit.
- `Tools/Unity/ConfigureFrontierEscarpments.cs` makes `FrontierEscarpments.unity`. Clone V14B terrain with exact UVs/indices; flatten only hill cores and protect road and settlement elevations using masks and actual renderer bounds. Validate footprints, roads and infrastructure before copying the scene. Ground texture is V14B 4K PNG with local correction of the old height-periodic slope tint; unchanged outside the replacement masks. Remove conflicting loose props and re-ground retained nature individually; leave buildings and road meshes in place. No gameplay, lighting or camera changes.
- Generate Blender, then Unity using commands in [Tools/Unity/ARTLAB.md](Tools/Unity/ARTLAB.md). Check cliff silhouettes, transitions, flight clearance, remaining loose nature, roads and surface continuity. Remaining untouched hills may still show old bands; do not assume V16 resolves the whole map. If acceptable, expand geology in another atom.

V16 local generation hit a MissingReferenceException at the second nature-group lookup: the cached hierarchy array included children destroyed while removing rocks in the first group. Hotfix resolves both group owners before deletion and skips invalid child references. Discard the partially generated scene in the editor, reopen V14B, then rerun the Unity generator; Blender output is reusable. V16 generated and flew successfully after the hotfix; user pushed green `92acd080`. Visual test: the large bare low-poly rock meshes looked foreign to the textured world, especially radial summit fans, oversized flat facets and abrupt footprints. Small V14A rocks read better. Keep V16 as a technical reference, not an accepted environmental look.

## V17 Integrated Rock Strata — generated / green

Start from green `92acd080` / `FrontierEscarpments.unity`, preserving its ground, roads, yards, refinery and flight settings. Replace the four isolated FBX landforms with continuous terrain relief. Do not redo the entire map or increase clutter.

- `Tools/Blender/generate_frontier_strata.py`: four asymmetric compound sandstone profiles, local erosion cuts, irregular steep walls and a broad talus apron. A deterministic 1.25-unit height grid per site (zero added relief at boundaries) is saved as `Resources/Blender/FrontierStrata.json`; editable geometry preview `FrontierStrata.blend` uses the same samples. No FBX import or lighting/normal/AO baking needed. Blender backup versions disabled for this file. Plain Python `--data-only` can export the samples without Blender.
- `Tools/Unity/ConfigureFrontierStrata.cs`: copy V16 to `FrontierStrata.unity`, remove the four detached V16 meshes only in the copy, refine selected original 5-unit terrain cells by 4x per axis and stitch neighboring cells into the same UInt32-indexed mesh. Existing planes outside patches are unchanged. Validate upward winding, projected area, interior edge incidence, infrastructure clearance and maximum height below 40. Existing road meshes and buildings are not moved. Only remove loose nature if its footprint clashes with raised relief.
- Keep the original V16 4K ground PNG; four small local albedos (512/1024 dimensions) sample the exact underlying ground and blend towards textured sandstone on elevated terrain, with spatially varying mineral deposits, no periodic height rings or baked illumination. One continuous terrain mesh with five material sections and real dynamic shadows. No new entire-map PNG. Keep B material comparison for previously baked hero assets.
- Generate from repo root (stop Play Mode and save scenes first):

`blender -b --python-exit-code 1 --python Tools/Blender/generate_frontier_strata.py`

`unity command script run --file Tools/Unity/ConfigureFrontierStrata.cs --entry ConfigureFrontierStrata.Configure`

Run a flight at size 80, compare V16 with V17, inspect cliff/talus silhouettes, texture continuity at patch boundaries, shadow readability and aircraft clearance. Prior scenes and Sandbox untouched. See the user test result below.

V17 local CLI failed on `JsonUtility` manifest validation before modifying the scene. The exported Python JSON contract is valid in offline tests; the exact native deserialization failure is not confirmed. Hotfix reads the same ordered JSON schema with managed .NET parsing and invariant-culture numbers, with explicit field/sample diagnostics and all previous validation intact. Existing Blender output is reusable; no regeneration required. User completed the Unity run successfully after this fix.

## V17 result — tested / green

User generated V17 after the manifest hotfix and accepted the integrated strata as a working direction. Screenshot shows a better terrain connection than V16, but some smooth surfaces and dark cliff grooves remain. Green `789a48af`; further strata polish is deferred until the material diagnostic below. B on the old showcase only affects V14A kit renderers, not V17 terrain. The user reports no visible difference between modes. Static inspection confirmed serialized renderer/material arrays, AO/normal texture assignments, importer types and runtime keywords; not proof of a correct GPU result. Earlier V14A AO was baked on the low mesh and the high-poly normal source differed mainly by small bevels, so lack of a visible effect is plausible.

## Current atom — V18 Bake Truth Test (pending local execution)

Separate diagnostic scene, not a terrain iteration. Two identical flat 30 × 18 industrial panels with the same neutral base color. Left always base-only; right selects explicit URP Lit material variants. Identical geometry and fixed camera make side-by-side comparison reproducible. Blender high source contains broad physical recessed bays, raised ribs, bolts and vent channels that exist only on the high mesh. Selected-to-active tangent normal and projected local AO bake from this source. No baked sun, lightmaps or environment shadow bake. The editable `.blend`, a low FBX, six diagnostic PNGs and completion/report text are in dedicated paths; no existing kit is modified. Diagnostic textures use uncompressed import intentionally, not a production recommendation.

Run from root:

```sh
blender -b --python-exit-code 1 --python Tools/Blender/generate_bake_truth_test.py -- --device OPTIX
```

After Unity refresh/compile, with Play Mode stopped and scenes saved:

```sh
unity command script run --file Tools/Unity/ConfigureFrontierBakeTruth.cs --entry ConfigureFrontierBakeTruth.Configure
```

Opens `FrontierBakeTruth.unity`. In Play Mode: 1 base (both should match), 2 AO, 3 normal, 4 full (default), 5 stronger AO via contrast map, 6 normal ×4, 7 both stronger; 8 raw AO, 9 raw normal RGB; 0 synthetic positive-control maps, T cycles combined/AO-only/normal-only; C toggles close/gameplay size 80; L cycles mixed/ambient-only/sun-only; H hides labels. Light is dynamic. The installed Frontier URP pipeline is overridden only during Play Mode and restored on disable. No shared material mutations or changes to maps, gameplay, build scenes or prior studies.

Check Blender printed percentile data, raw map visibility, positive controls (AO under ambient-only and normal under sun-only), then actual baked maps at close and gameplay distance. If controls work but real maps do not, investigate bake output/projection; if controls do not work, investigate Unity shader/import/lighting before judging bake value. If all work close but not at size 80, stop spending production effort on invisible detail. This diagnostic has not been run on Blender or Unity locally; only offline geometry, PNG controls and Git patch validation are possible here.

V18 local Unity CLI reported a missing completed bake. The original marker check required a literal LF after the header; Python text-mode output on Windows can write CRLF. Hotfix reads the header as a line (LF or CRLF), separates missing-marker and invalid-marker diagnostics, and makes future Python marker writes use explicit LF bytes. Existing complete bake outputs should be reusable; the actual local marker contents are not yet confirmed. Do not bypass the completion check or rebake unnecessarily.

## Lessons from the visual search

- V1–V5: palette/composition, ground anti-tiling, individual tree groves. V6/V7 monolithic canopies looked like tumors; rejected.
- V8/V9: real dynamic directional shadows were the breakthrough; lower strength .88 -> .75 accepted. Relief hidden by trees.
- V10: large flight playground exposed readability issue. Combined forest meshes created ~627 MiB raw generated output (525 MiB trees, 96 MiB texture); avoid duplicating geometry. Heavy snapshot `3d8ddf6`.
- V11/V12: instancing and PNG compression fixed output blowup, but forest still competed with small aircraft; palette muting, fill, outline/scale and texture softness were not enough. No individual V12 accent accepted. Screen-space blur not implemented.
- V13: new post-apocalyptic/badlands mockup drove composition/palette, not literal camera/UI. First V13 flight test accepted. Full pre-cleanup snapshot `3aae3d12`; recover past experiments there.

## Potential next visual treats (not yet ordered)

Slightly desaturate environment (roughly 10–15% test, not an accepted setting), improve clay/dust surface detail, worn roads, authored modular ruin kit in Blender, larger rocky escarpments, sparse rubble/vehicle wrecks and localized dust/smoke. Introduce one visual hypothesis at a time; keep aircraft readable. No need to polish everything now, and no architectural refactor just for its own sake.
