# TERRAIN-01 — faceted terrain playground

Base: `143259e`. Visual trial only, separate from the authored `ArcadeDesertE1`. Inspired by the mobile Jet Attack reference: large matte faces, coherent terrain forms, no surface noise or props. No collision, enemies, weapons, canyon, or mission rules.

## First run

After applying the patch and Unity script reload, choose **Air Combat > Terrain > Create or Open TERRAIN-01**. The explicit editor command makes a separate `Assets/_Project/Scenes/Terrain01Playground.unity` and terrain assets under `Assets/_Project/Art/Environment/Terrain01/`. It copies the main scene's existing player/camera/lighting instance overrides and flight session, removes all other scene roots and the encounter from the copy only, and disables player firing. It never regenerates or saves over the main scene; the existing trial is opened, not rebuilt. No build settings are changed.

Press Play. A/D steer, W boost, S tight turn, R reset, H debug; 7/8/9 compare Y78/Y110/Y150 using the existing height test. No combat HUD. Fly and compare with the production view at Y78 first. The visible surface extends 2400 x 2400; automatic player boundary reset is disabled. Initial local relief is bounded between 3 and 68 units. The committed playground has both source and surface at Y-30 (world relief -27 to 38). The flight camera and player speed are unchanged.

## Authoring

The source is an editable Unity Terrain (257 x 257 height samples), hidden from rendering by its Draw Heightmap setting. The mesh samples a deterministic mildly irregular 84 x 84 grid (roughly 28.6-unit cells; 14112 triangles after rebaking, versus the original 3528), duplicates each triangle's vertices and recalculates flat normals. No random face colors; one matte URP Lit sand material with actual scene lighting/shadows. The source TerrainCollider is disabled for play, and the mesh has no collider in this trial.

To edit: stop Play, hide `TERRAIN-01 / Faceted surface`, select `TERRAIN-01 / Editable source`, enable Draw Heightmap and the source TerrainCollider, then sculpt with Unity Terrain tools. Then choose **Air Combat > Terrain > Rebake TERRAIN-01 Facets**. This updates the existing mesh asset, hides the source rendering, disables its collider and restores the surface. Save the scene. Preserve the generated source and mesh assets and their .meta files with the scene if the trial is accepted. Adjust the Sand material directly if needed. No automatic rebaking. For the finer-facet trial, run this command once after applying the patch; the existing heightmap, material and Y-30 placement are preserved. The mesh asset must be saved/committed after rebaking.

## Acceptance

- No missing components or errors during scene creation or Play.
- Kestrel stays readable, shadows visible, broad facets show motion without visual noise; inspect Y78 first, then 110/150 if useful.
- WASD, boost, reset and camera work like the existing scene; no combat spawns or firing.
- Source terrain edits survive rebake; mesh has no cracks, upside-down triangles or smooth normals.
- Main scene and existing lab scenes are untouched; the main map still runs the current interception test.

Next, if the visual style is accepted: TERRAIN-02 (wide canyon with large turns and open combat space), then COLLISION-01. Do not implement altitude gameplay or terrain collision in this trial.
