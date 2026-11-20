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

## Current atom — V10 Landscape Flight (pending visual test)

- Separate scene copied from Sandbox solely to reuse the configured Player, Kestrel model, bank pivot, flight/afterburner/VFX and follow camera. Sandbox and prior ArtLabs are untouched. Remove other roots except camera and light; remove combat and painted aircraft shadow components.
- Static 1200 x 1200 mesh landscape: broad hills, ridges and valleys, stronger height range, open crests and clustered trees on lower slopes. Existing tree assets are combined by material in 100-unit sectors to keep renderer counts reasonable; no runtime streaming or tree collisions.
- Existing GrassBase/GrassDry source textures blended into a new scene-local anti-repetition color map with modest slope tint. No Blender bake or shared asset changes.
- V9 contrast light settings (38 degrees, 0.75 shadow strength) and temporary V8 study URP pipeline; real dynamic tree, terrain and aircraft shadows only.
- Actual Sandbox flight tuning retained. Test-only fixed flight altitude 48; camera starts at 65 degrees with size 80. WASD, Space boost; 1/2/3 camera 90/75/65 degrees; H legend; R recenter. Automatic recenter before reaching map edge (not a gameplay boundary mechanic).
- No buildings, roads, rivers, enemies or combat UI. No new mechanics.

Generate from repo root after scripts compile:

`unity command script run --file Tools/Unity/ConfigureArtLabLandscapeFlight.cs --entry ConfigureArtLabLandscapeFlight.Configure`

In Play Mode compare open hills and wooded valleys while flying, camera angles, shadow stability and frame time. V10 is a visual experiment, not a production map or an accepted result yet. Next: tune relief scale, vegetation and ground color based on flight footage/screenshots; reduce altitude/relief if aircraft readability suffers.
