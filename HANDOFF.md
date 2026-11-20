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

## Current atom — cleanup (pending local migration / smoke test)

Base green `3aae3d12b84e5276a333060fdd1480828aa17f2c`. Move accepted V13 scene/output to stable ScorchedFrontier paths. Move the V8 lighting asset and V11 aircraft material copies out of retired generated folders, preserving GUIDs. Refactor generator to use Sandbox plus those retained materials and pipeline, no dependency on V11. Keep current tested scene rather than rebuilding it during cleanup. Remove V1–V12 obsolete scenes, generated folders and their editor generators/runtime preview scripts after checking retained asset dependencies. Preserve authored models, Blender sources, Sandbox/Battlefield and active third-party dependencies. No render tuning or gameplay changes. Run `CleanupArtLabs.Preview`, then `CleanupArtLabs.Configure` only on clean preflight. Smoke-test both scenes; check git status and push green. The generator rebuild path should be separately tested before further generator changes. Archive remains in Git, no history rewrite.

## Lessons from the visual search

- V1–V5: palette/composition, ground anti-tiling, individual tree groves. V6/V7 monolithic canopies looked like tumors; rejected.
- V8/V9: real dynamic directional shadows were the breakthrough; lower strength .88 -> .75 accepted. Relief hidden by trees.
- V10: large flight playground exposed readability issue. Combined forest meshes created ~627 MiB raw generated output (525 MiB trees, 96 MiB texture); avoid duplicating geometry. Heavy snapshot `3d8ddf6`.
- V11/V12: instancing and PNG compression fixed output blowup, but forest still competed with small aircraft; palette muting, fill, outline/scale and texture softness were not enough. No individual V12 accent accepted. Screen-space blur not implemented.
- V13: new post-apocalyptic/badlands mockup drove composition/palette, not literal camera/UI. First V13 flight test accepted. Full pre-cleanup snapshot `3aae3d12`; recover past experiments there.

## Potential next visual treats (not yet ordered)

Slightly desaturate environment (roughly 10–15% test, not an accepted setting), improve clay/dust surface detail, worn roads, authored modular ruin kit in Blender, larger rocky escarpments, sparse rubble/vehicle wrecks and localized dust/smoke. Introduce one visual hypothesis at a time; keep aircraft readable. No need to polish everything now, and no architectural refactor just for its own sake.
