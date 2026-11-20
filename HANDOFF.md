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

## Current atom — V9 terrain (pending visual test)

Copy V8; retain its forest and lighting. Add broad hills and a shallow depression to the existing terrain mesh. Protect the airfield, road, riverbed and static aircraft reference areas. Re-ground individual forest trees, rocks and turrets to the changed surface. Keep the V8 scene and settings unchanged; V9 uses contrast shadow strength 0.75. Only real-shadow modes in V9. No Blender bake required for this test.

Generate from the repository root:

`unity command script run --file Tools/Unity/ConfigureArtLabTerrain.cs --entry ConfigureArtLabTerrain.Configure`

Inspect V9 in Play Mode (2/3 lighting, L cycle, G ground, H legend). Compare to V8 at the same camera settings. Judge relief silhouette, runway/river clearance, tree bases, aircraft readability and shadow contrast. Next change based on screenshots; don't assume acceptance yet.
