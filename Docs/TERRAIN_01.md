# TERRAIN-01 — faceted terrain playground

Base: `143259e`. Visual trial only, separate from the authored `ArcadeDesertE1`. Inspired by the mobile Jet Attack reference: large matte faces, coherent terrain forms, no surface noise or props. No collision, enemies, weapons or mission rules. Later trials add canyon geography.

## First run

After applying the patch and Unity script reload, choose **Air Combat > Terrain > Create or Open TERRAIN-01**. The explicit editor command makes a separate `Assets/_Project/Scenes/Terrain01Playground.unity` and terrain assets under `Assets/_Project/Art/Environment/Terrain01/`. It copies the main scene's existing player/camera/lighting instance overrides and flight session, removes all other scene roots and the encounter from the copy only, and disables player firing. It never regenerates or saves over the main scene; the existing trial is opened, not rebuilt. No build settings are changed.

Press Play. A/D steer, W boost, S tight turn, R reset, H debug; 7/8/9 compare Y78/Y110/Y150 using the existing height test. No combat HUD. Fly and compare with the production view at Y78 first. The visible surface extends 2400 x 2400; automatic player boundary reset is disabled. Initial local relief is bounded between 3 and 68 units. The committed playground has both source and surface at Y-30 (world relief -27 to 38). The flight camera and player speed are unchanged.

## Authoring

The source is an editable Unity Terrain (257 x 257 height samples), hidden from rendering by its Draw Heightmap setting. The mesh samples a deterministic mildly irregular 84 x 84 grid (roughly 28.6-unit cells; 14112 triangles after rebaking, versus the original 3528), duplicates each triangle's vertices and recalculates flat normals. No random face colors; one matte URP Lit sand material with actual scene lighting/shadows. The source TerrainCollider is disabled for play, and the mesh has no collider in this trial.

To edit: stop Play, hide `TERRAIN-01 / Faceted surface`, select `TERRAIN-01 / Editable source`, enable Draw Heightmap and the source TerrainCollider, then sculpt with Unity Terrain tools. Then choose **Air Combat > Terrain > Rebake TERRAIN-01 Facets**. This updates the existing mesh asset, hides the source rendering, disables its collider and restores the surface. Save the scene. Preserve the generated source and mesh assets and their .meta files with the scene if the trial is accepted. Adjust the Sand material directly if needed. No automatic rebaking. For the finer-facet trial, run this command once after applying the patch; the existing heightmap, material and Y-30 placement are preserved. The mesh asset must be saved/committed after rebaking.

## TERRAIN-04 — Macro relief (trial)

With Terrain01Playground open in Edit Mode, choose **Air Combat > Terrain > Trial Macro Relief** and confirm. This explicitly replaces the existing trial heightmap and rebakes the same mesh asset; ordinary Rebake remains non-destructive to sculpted heights. The four unequal, rotated and warped landforms leave broad connected lowlands, with a quiet floor and restrained local noise. The 84 x 84 facet grid, material, terrain transform (Y-30), camera and flight remain unchanged. The main map is untouched. Save and commit the updated source and mesh assets if accepted; use Undo or restore the previous green to compare.

## TERRAIN-05 — Relief contrast (trial)

Open Terrain01Playground in Edit Mode and choose **Air Combat > Terrain > Trial Relief Contrast**. Confirm replacing the trial heightmap. The four warped landform footprints and 84 x 84 facet grid stay the same; vertical mass is multiplied by 1.8, the source height range expands to 180 units to avoid clipping summits, and local upland detail increases from 6 to 10 units. Lowlands have a quieter 3-unit variation. Both terrain transforms remain at Y-30. No camera, lighting, flight or main-map changes.

The existing URP Lit material uses a generated 64 x 1 linear albedo palette (`Terrain01_ReliefPalette.asset`) with constant UVs per triangle. Elevation brightens the surface and steep slopes darken it slightly; there is no random face color, shader replacement or baked sunlight. Normal Rebake preserves current sculpting, height range and palette. **Trial Macro Relief** restores the original procedural relief and removes the palette from the material (the palette asset can remain for reuse). Both trial generation commands overwrite the trial heightmap explicitly; Undo or restore the previous green to compare.

This is still a visual-only test: some peaks can exceed Y78 or Y110, and the aircraft can intersect the surface because terrain collision is not implemented. Use 9 (Y150) to inspect the full relief safely, then 7/8 to compare readability at gameplay height. Save and commit the changed terrain source, mesh, material, new palette asset and its .meta if accepted. Check that the original main map is unaffected.

## TERRAIN-06 — Canyon Run (trial)

Open `Terrain01Playground` in Edit Mode and choose **Air Combat > Terrain > Trial Canyon Run**, then confirm. This replaces only the trial heightmap with a continuous S-shaped valley cut through a high plateau: open mouths, varying width, a wider mid-route basin, independently irregular banks and coherent steep slopes. It reuses the 84 x 84 faceted mesh, the existing URP Lit sand material and relief palette, the 180-unit height range, and the committed Y-30 terrain placement. At that placement the floor is approximately world Y-15 to -8 and the plateau Y126 to Y141; the Y78 combat plane passes through the walls, not over them. There is deliberately no collision detection yet.

The command moves the trial Kestrel to the southern mouth at Y78, facing along the route. Camera, controls, lighting and flight tuning are unchanged. R returns to the new start. 9 (Y150) is useful for an overhead comparison. Ordinary Rebake preserves the current heightmap and player placement. The old relief trial commands remain available, but do not restore the previous player pose; restore the previous green for an exact comparison. Save the trial scene and commit the terrain data and mesh if accepted. The authored main map and labs are untouched.

## Acceptance

- No missing components or errors during scene creation or Play.
- Kestrel stays readable, shadows visible, broad facets show motion without visual noise; inspect Y78 first, then 110/150 if useful.
- WASD, boost, reset and camera work like the existing scene; no combat spawns or firing.
- Source terrain edits survive rebake; mesh has no cracks, upside-down triangles or smooth normals.
- Main scene and existing lab scenes are untouched; the main map still runs the current interception test.

Next: evaluate the Canyon Run at Y78, including wall readability and whether the corridor feels comfortable at speed; then consider COLLISION-01. Do not implement altitude gameplay or terrain collision in this trial.
