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

## Current atom — V14B Surface & Composition (pending visual test)

Copy green `FrontierHeroShowcase.unity` (not V13), keeping new refinery, rocks, camera/flight, bake comparison and dynamic shadows. Improve the previously blurry ground and sterile roads; do not add more small props or change terrain geometry. Create `FrontierSurfacePass.unity` with scene-local output in `Generated/FrontierSurfacePass/`.

- Upsample the existing ground color map to a 4096 PNG and add restrained world-scale crust/grit, 13% desaturation, concrete slab seams and localized oil/dust wear at the actual refinery footprint; compacted dirt pads at other sites. No painted lighting or floating surface patch meshes.
- Keep original road ribbons and gaps; clone only four small road/shoulder meshes to add strip UVs. Worn asphalt and dust shoulder PNGs with mipmaps and GPU compression. Existing road markings and junction geometry unchanged. No edits to shared materials or V14A/Sandbox/Frontier.
- Existing B comparison remains available; compare ground and roads at the same bake mode and camera angle. No Blender re-run. Generate after saving scenes and stopping Play Mode:

`unity command script run --file Tools/Unity/ConfigureFrontierSurfacePass.cs --entry ConfigureFrontierSurfacePass.Configure`

Judge surface detail in motion, natural transition around refinery, road wear and aircraft readability. Inspect generated PNG sizes before pushing. If the pass works, later promote selected surfaces and hero kit to the stable Frontier generator. Keep scope visual only.

## Lessons from the visual search

- V1–V5: palette/composition, ground anti-tiling, individual tree groves. V6/V7 monolithic canopies looked like tumors; rejected.
- V8/V9: real dynamic directional shadows were the breakthrough; lower strength .88 -> .75 accepted. Relief hidden by trees.
- V10: large flight playground exposed readability issue. Combined forest meshes created ~627 MiB raw generated output (525 MiB trees, 96 MiB texture); avoid duplicating geometry. Heavy snapshot `3d8ddf6`.
- V11/V12: instancing and PNG compression fixed output blowup, but forest still competed with small aircraft; palette muting, fill, outline/scale and texture softness were not enough. No individual V12 accent accepted. Screen-space blur not implemented.
- V13: new post-apocalyptic/badlands mockup drove composition/palette, not literal camera/UI. First V13 flight test accepted. Full pre-cleanup snapshot `3aae3d12`; recover past experiments there.

## Potential next visual treats (not yet ordered)

Slightly desaturate environment (roughly 10–15% test, not an accepted setting), improve clay/dust surface detail, worn roads, authored modular ruin kit in Blender, larger rocky escarpments, sparse rubble/vehicle wrecks and localized dust/smoke. Introduce one visual hypothesis at a time; keep aircraft readable. No need to polish everything now, and no architectural refactor just for its own sake.
