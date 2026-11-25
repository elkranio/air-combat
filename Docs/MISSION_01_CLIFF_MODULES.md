# MISSION-01H2A — Reusable cliff modules (WIP)

Base `3e9f828`. Model-only atom following the accepted cliff trial and proposed map layout v4 (wide triangular southern base, north-running central barrier). Existing scene, TerrainData, cliff trial, lighting, radar code and combat are unchanged. No final placements yet.

## Modules

| Asset | Nominal ground footprint X × Z | Height | Role |
| --- | ---: | ---: | --- |
| CL01_Fan | 985 × 782 | 168 | Broad southern base, narrows northwards |
| CL02_Spur | 540 × 561 | 128 | Asymmetric lateral shoulder / buttress |
| CL03_Ridge | 570 × 854 | 184 | Elongated central spine |
| CL04_Citadel | 525 × 575 | 205 | Distinct northern head / summit |

Each has an independently authored outline, varying steep-wall profile, uneven shelf, coplanar plateau, flat normals and closed combined topology. Three coincident-boundary material meshes reuse the accepted Foot/Wall/Crown materials in `CliffTrial`; retain those materials when the old scene trial is removed. Ground footprint is wider than the radar section at Y78. No random texture or baked light. Reusable source is `Tools/Blender/build_cliff_modules.py`; run in Python to regenerate OBJ geometry or with `blender --background --factory-startup --python Tools/Blender/build_cliff_modules.py` to also save `Resources/Blender/CliffModules.blend`. The editable .blend is not included in the patch. Each local origin is at ground level; Blender lineup offsets are for authoring only.

## Local review

1. Apply patch and let Unity import and compile.
2. Run **Air Combat > Art > Build Cliff Module Prefabs**. This creates four prefabs in `Assets/_Project/Prefabs/Environment/Cliffs`, reusing existing materials and adding one `RadarTerrainObstacle` to each root. Existing prefabs are never overwritten; running again only creates missing ones. No mission changes.
3. Drag one prefab at a time onto a flat part of `Mission01_FirstSortie` at Y0 for preview, inspect at flight altitude Y78 from both sides. Avoid the existing experimental Terrain ridge. Check shape, shadows, radar and caution arc. Remove the temporary preview instances before committing the model-only atom.
4. Commit generated prefab assets and their .meta files along with the patch files after local approval.

## Next atom

Place and rotate modules to fit the v4 map's Y78 contour, disable or flatten the superseded ridge in a separate explicitly scoped edit. Use individual radar components per closed module, not one component over a group of overlapping modules: the existing even-odd slice logic would cancel overlapping fills inside a single obstacle. Separate components can still draw interior overlap outlines, so check the assembled radar shape and address union visualization if needed. Check continuity of the barrier at flight height and at the southern map boundary, the generous west route and the east optional objective approach. Physical collision/crash and SAM composition remain deferred. Unity import, compilation and Play Mode have not been run in the patch environment.
