# Air Combat — handoff

## Current direction (accepted)

**FrontierStrata (V17)** is the current visual baseline, evolved from accepted Scorched Frontier (V13). V13 is retained as fallback; V18 is the bake diagnostic. User flight test: dramatically better than the forest variants; aircraft reads well in motion, including over ruins. Foundation works; future art polish is optional and incremental. Keep cold blue aircraft over warmer ochre clay/dust, large open spaces, ridges and localized abandoned infrastructure. Avoid dense all-over detail, unnecessary blur/outline and excessive orange saturation.

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

## Cleanup after V18 — tested / green

Green base `a4ad17979ed9ebc222600625b2517919c10a3bbd` contains all tested V14–V18 studies. Preserve it as the recovery point. Keep Sandbox (sole build scene), ScorchedFrontier (V13 fallback), FrontierStrata (V17 active map) and FrontierBakeTruth (V18 bake reference). Do not regenerate any of them during cleanup.

- `Tools/Unity/CleanupFrontierStudies.cs` offers `Preview` and `Configure`; see [ARTLAB.md](Tools/Unity/ARTLAB.md). It moves the eight V14B road assets and two V16 ground assets into `Generated/FrontierStrata/` with preserved GUIDs. After checking dependencies of every retained imported asset, it deletes the three intermediate scenes, obsolete V14B ground texture/material, V16 terrain/rock mesh kit/material, obsolete V16 Blender source/backup and retired scene generators. It refuses any unexpected files in retired folders, build scene conflicts or retained dependencies. No broad orphan sweep, no Git history rewrite, no gameplay or art change. Existing hero assets, reusable Blender sources, V17 height manifest and both bake diagnostic generators stay. V17's full historical Unity generator chain is recoverable at the green base, not a live rebuild path after cleanup.
- User applied the cleanup and pushed green `b21a56669b42454dd51f1a01c0ffad5941eb5f5d`. The retained V17 and V18 scene blobs are unchanged from pre-cleanup. Do not rerun cleanup casually; never force-delete on a failed dependency preflight.

## Accepted visual progression (historical)

V13 (`bbd67fe3`) established the palette, static 1200 x 1200 map, dynamic directional shadows and aircraft readability. V14A (`cc8b3dd7`) improved the refinery and small rock kit, reusable FBX/PNG/material/prefab assets in `Art/Environment/FrontierHeroKit/` and editable `Resources/Blender/FrontierHeroKit.blend`. V14B (`e22e7022`) added the 4K ground grit, reduced saturation, concrete yards and worn roads. V15 rejected: changed global heightfield, floating props, black artifact and blurry ground. V16 (`92acd080`) worked technically but detached giant low-poly rocks looked foreign. V17 (`789a48af`) replaced four isolated masses with locally refined, watertight continuous terrain, textured sandstone and talus. User accepted V17 as the direction; smooth surfaces/dark grooves are still candidates for polish. V17 source height data and editable preview stay in Resources; old intermediate scenes and generators are historical after cleanup.

## V18 Bake Truth Test — tested / green

Separate `FrontierBakeTruth.unity`: two identical flat 30 x 18 industrial panels; left base only, right controls. High source has deliberately broad details absent from the low mesh. Selected-to-active tangent-space normal and local AO baked from high. No baked sun/world shadows. Both Blender/Unity generation succeeded after the Windows CRLF marker hotfix. User tested close and actual gameplay orthographic size 80, with obvious normal-driven relief at ordinary strength 1. AO only is subtle, AO+normal versus normal alone changes little. Exaggerated and raw/control maps also worked. The previous V14A kit's high poly mainly added 6–9 cm bevels, and AO was baked on low; that explains the plausible lack of visible detail there, not a proven V14A runtime bug. The V18 result confirms this diagnostic pipeline, not all future assets.

Keep normal maps for purposeful surface detail, optional AO, broad shapes designed for gameplay camera. Real silhouette/large volumes and shadows must remain geometry. Never bake directional sunlight into albedo. V18 diagnostic uses uncompressed maps deliberately; production should use mipmaps and compression. Rebuild commands and controls in [ARTLAB.md](Tools/Unity/ARTLAB.md). No changes to V18 needed for now.

## Current atom — V19 Practical Bake Test (pending local generation / visual test)

Base: cleanup green `b21a5666`. Copy `FrontierStrata.unity` to `FrontierPracticalBake.unity`. No changes to V17, V18, Sandbox, V13, existing kit assets, ground/roads, gameplay or lighting. Test two assets at the real flight camera, orthographic size 80.

- Replace only the original 34 x 24 refinery main block at (65,30) with a simple industrial process building on the existing foundation footprint. The 30 x 20 roof has a two-triangle mesh with purposeful high-only recessed bays, vent channels, raised fasteners and a damaged seam. Select-to-active Cycles normals and local AO use the proven V18 bake routine; roof albedo is separate material color/weathering with no directional lighting.
- For the north BrokenMesa (-125..20 X, 145..225 Z), **keep the integrated V17 terrain geometry and albedo**. Generate local height-guided bedding shelves and fractures on a separate planar high source; bake their tangent-space detail normals and optional AO, using the existing material's world-to-local UV transform. Fade detail to zero at the apron and all patch edges. This is an approximate detail-normal layer over the existing slope normals, not a rebake of the full terrain or a geometry displacement. No additional detached rock mesh. Other three strata materials are unchanged.
- `Tools/Blender/generate_frontier_practical_bake.py` uses reusable V18 `bake`, `setup_device` and PNG helpers. Two normal/AO sets and roof color at 2048 with 48 Cycles samples default, one roof FBX, editable `Resources/Blender/FrontierPracticalBake.blend`. CRLF-safe ready marker includes per-bake strength reports; refuse weak normal signals. Production texture imports use mips and compressed HQ, not V18's diagnostic uncompressed settings.
- `Tools/Unity/ConfigureFrontierPracticalBake.cs`: validate marker/maps, roof geometry/UVs, original building bounds and single BrokenMesa terrain slot/UV transform. Clone scene and material variants; never modify the shared terrain mesh or existing materials. New V19-only `FrontierPracticalBakePreview` switches B modes on the roof and BrokenMesa slot only; remove old hero preview in the copy, not from V17. B: base / AO / normal / both (default). Fly north from refinery to BrokenMesa. H hides legends. Real shadows always on.

Commands in [ARTLAB.md](Tools/Unity/ARTLAB.md). Check B from identical positions above roof and mesa; evaluate ordinary strength 1 at size 80, not only close-up. Judge silhouette, material readability, terrain integration and whether texture compression preserves normal detail. The original roof silhouette is intentionally replaced by a simpler closed hall; new art needs visual acceptance. AO optional if marginal. Offline Python syntax, geometry/range, boundary fade and base-color checks passed; Cycles/Unity execution not available here. No V19 local results yet.

## Lessons from the visual search

- V1–V5: palette/composition, ground anti-tiling, individual tree groves. V6/V7 monolithic canopies looked like tumors; rejected.
- V8/V9: real dynamic directional shadows were the breakthrough; lower strength .88 -> .75 accepted. Relief hidden by trees.
- V10: large flight playground exposed readability issue. Combined forest meshes created ~627 MiB raw generated output (525 MiB trees, 96 MiB texture); avoid duplicating geometry. Heavy snapshot `3d8ddf6`.
- V11/V12: instancing and PNG compression fixed output blowup, but forest still competed with small aircraft; palette muting, fill, outline/scale and texture softness were not enough. No individual V12 accent accepted. Screen-space blur not implemented.
- V13: new post-apocalyptic/badlands mockup drove composition/palette, not literal camera/UI. First V13 flight test accepted. Full pre-cleanup snapshot `3aae3d12`; recover past experiments there.

## Other potential visual treats (not yet ordered)

Slightly desaturate environment (roughly 10–15% test, not an accepted setting), improve clay/dust surface detail, worn roads, authored modular ruin kit in Blender, larger rocky escarpments, sparse rubble/vehicle wrecks and localized dust/smoke. Introduce one visual hypothesis at a time; keep aircraft readable. No need to polish everything now, and no architectural refactor just for its own sake.
