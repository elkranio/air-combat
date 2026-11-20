# Air Combat — visual handoff

## Current focus

Search for the map/environment visual style. Do not change Sandbox or flight/combat while on this track. Use separate ArtLab scenes for meaningful visual experiments. Unity 6.6 / URP; see ART_PIPELINE.md for art layout and Tools/Unity for reproducible scene generators.

## Workflow (required)

- Deliver each code atom as an actual downloadable `.patch` attachment, not an inline diff or instructions to save one manually.
- User runs Git Bash on Windows. Provide exactly the two apply commands with the actual filename: `git apply --check --verbose ~/Downloads/<name>.patch` and `git apply ~/Downloads/<name>.patch`. Then give any required Unity generation command separately.
- Stop Play Mode and save scenes before running a generator. User playtests, then pushes green; base the next atom on that commit. Do not write directly to master.
- Keep this handoff updated in every atom: hypothesis, result/decision, current implementation, next step. Distinguish pending visual tests from accepted results.

## Visual studies so far

- V1/V2: palette and composition, shallow foothills, fixed art references.
- V3/anti-tiling: textured ground and less obvious repetition.
- V4: individual trees; V5: dense groves of individual tree prefabs are more promising.
- V6/V7: monolithic canopy/forest masses and hybrids felt like shapeless tumors; do not pursue for now.
- V8: real directional soft shadows using a study-local URP asset. Contrast sun (38 degrees, mode 3) preferred over the flatter lighting in user screenshots. Dynamic shadows accepted; painted blobs not the production direction. User wants contrast shadow strength reduced a little (0.88 -> 0.75).
- River/banks temporary. Ground feels too flat. Relief should carry composition; trees provide localized detail and lighting unifies it.

## V9 terrain — tested

V9 retained the V8 forest and real directional lighting, reduced contrast shadow strength to 0.75, added protected terrain relief and re-grounded props. User screenshot: overall improved, shadows now good; relief still too subtle and much of it hidden by trees. V5 individual-tree groves and V9 contrast lighting remain the working baseline. Stop iterating exclusively in a static composition.

## V10 Landscape Flight — tested / superseded pending V11

Static 1200 x 1200 hills and clustered trees, camera follow and Kestrel from Sandbox. User screenshot: flight works, but high-contrast forest competes with the dark aircraft; relief remains work in progress. Do not blur everything; buildings/targets should remain clear. V9 dynamic directional shadow baseline remains accepted (38 degrees, strength 0.75).

V10 generated output was ~627 MiB uncompressed (525 MiB duplicated tree meshes, 96 MiB raw color texture, 6.6 MiB terrain mesh), pushed in `3d8ddf6`. Do not rerun the retired heavy V10 generator. Keep authored models and materials unchanged; avoid baking repeated geometry or uncompressed full-map native texture assets into Git. Keep prior iterations where practical, but retire V10 scene with its generated folder after V11 passes. Do not rewrite Git history.

## V11 Readability — tested

- Separate scene copied from Sandbox, same 1200 x 1200 heightfield and 48-unit flight altitude, size 80 camera, default 65 degrees, same flight/VFX settings and V9 lighting. No combat or buildings; no Sandbox changes.
- Preserve existing three tree meshes and shadows. Muted scene-local tree palette, small fill to reduce dark crown contrast, lower density with open crests. Deterministic layout seed; placements are not pixel-identical to V10. No blur, no new tree geometry.
- Existing Kestrel hull/upper/panel materials cloned locally and brightened; keep trim, glass and engines. Quieter grass composite baked to a 2048 PNG with mipmaps/GPU compression on import. Slope tint retained.
- GPU instancing via sector/model batches of up to 256 matrices, shared imported FBX mesh assets, padded camera culling and real shadows. Forest rendered during Play Mode, not visible in Edit Mode. No generated per-tree geometry, no importer changes to source FBXs. Verify instancing and tree shadows in Frame Debugger; FPS not measured yet.
- WASD, Space afterburner, 1/2/3 camera 90/75/65, H legend, R recenter. Compare aircraft silhouette over forest versus meadow and shadow stability; no new flight mechanics.

Generate after scripts compile, with Play Mode stopped:

`unity command script run --file Tools/Unity/ConfigureArtLabReadability.cs --entry ConfigureArtLabReadability.Configure`

After successful visual test, stop Play Mode and keep V11 open; remove the superseded V10 scene and huge generated assets with:

`unity command script run --file Tools/Unity/ConfigureArtLabReadability.cs --entry ConfigureArtLabReadability.RetireV10`

Commit V11 generated assets and V10 deletions after the test. V10 remains recoverable at `3d8ddf6`; removing files from HEAD does not shrink earlier Git history. V11 runs successfully. The user screenshot confirms the jet still blends with forest crowns even after recoloring. Palette adjustment alone is insufficient.


## V12 Focus Study — tested

- Copy the green V11 scene, retain its terrain, forest transforms, compressed textures, instancing, existing flight/camera controls and real shadows. V12 depends on V11 generated assets; do not delete them. No map regeneration or duplicate terrain/tree geometry.
- B cycles V11 baseline, softened nature (box-filtered ground texture, quieter tree palette and modest tree-only fill), and aircraft focus (soft nature plus 18% visual scale, thin inverted-hull outline, brighter hull materials, more neutral sun/ambient). All changes are scene-local; baseline is the actual V11 look.
- Ground texture softness is selective but this is not screen-space background blur. The aircraft and future targets remain sharp. If this comparison is insufficient, test actual selective render-layer blur separately.
- Retain H, R, WASD, Space, and 1/2/3. No gameplay changes. New generated color maps are small PNGs.

Generate after Unity has refreshed and finished compiling runtime scripts:

`unity command script run --file Tools/Unity/ConfigureArtLabFocusStudy.cs --entry ConfigureArtLabFocusStudy.Configure`

V12 flew successfully in the user test. Aircraft is a little easier to track in motion, but the forest still competes for attention. Do not assume that any of the V12 accents were individually accepted. Pause forest iteration and test a substantially different setting.


## Current atom — V13 Scorched Frontier (pending visual test)

The user approved a post-apocalyptic/badlands mockup as a direction: cold blue aircraft on warmer ochre terrain, rocky ridges, abandoned roads, industrial ruins and large empty spaces. The generated mockup is an atmosphere/composition reference, not a literal target for camera, HUD, detail density or assets. Avoid noisy all-over rubble, strong orange oversaturation, forest-style clutter, painted shadows and background blur.

- Copy green V11 (not V12) for the same brightened Kestrel, actual flight/boost/VFX, camera presets, 48-unit altitude, size 80 and temporary dynamic-shadow URP pipeline. No combat, changes to Sandbox, outlines or post-processing. V11/V12 remain intact. Slightly more neutral key/ambient tint; 38-degree sun and shadow strength 0.75 retained.
- New static 1200 x 1200 terrain, elongated ridges, basins, crater depressions, warm clay/dust/rock color field. Flatten road corridors and actual settlement pads. Unique 2048 PNG color map with mipmaps and GPU compression on import; not a large native color .asset.
- Five composed sites: abandoned refinery, roadside village, industrial yard, transmission station and checkpoint. Main damaged highway and industrial spur, sparse rocks and burnt trees. Reusable Unity-generated prefab kit: ruined factory, house, steel frame, tank, chimney, pylon, barrier, stump, three faceted rock variants. Shared primitive meshes/materials; no per-instance combined meshes or Blender import step this atom. Modular shapes are a V0 silhouette study; no destruction, targeting or physics.
- Keep previous workflow; generate from repository root after saving scenes and stopping Play Mode:

`unity command script run --file Tools/Unity/ConfigureArtLabScorchedFrontier.cs --entry ConfigureArtLabScorchedFrontier.Configure`

WASD / Space, 1/2/3 camera, R recenter, H legend. Inspect over open clay, over concrete/roof silhouettes, around ridge edges and over the first refinery/village. Judge aircraft readability, ground saturation, visual identity and frame time before adding detail. Check generated folder size; no new massive meshes or raw texture assets. No gameplay changes. Future if direction works: turn the most successful V0 modules into authored Blender assets and test building damage/targets later, not during this study.
