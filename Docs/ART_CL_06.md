# CL06 — reusable solid rock library (accepted v0)

Eight closed meshes: bastion, pillar, fracture, buttress, crown, shoulder, boulder, slab. CL06B established overlapping wall assemblies; C tested three camera-relative orientations; D/E added localized cuts, F compacted debris, G calmed the plateau tops. Use the current committed models rather than rebuilding earlier walls.

- Prefabs: `Assets/_Project/Prefabs/Environment/Cliffs/CL06_*.prefab`.
- OBJ: `Assets/_Project/Art/Environment/Mission01_FirstSortie/CL06_RockBarrier/`.
- The three `CL05_Stone_*.mat` materials and `build_cl05_test_rock.py` solid builder are shared dependencies, not retired trial assets.
- Model sources: `Tools/Blender/build_cl06_rock_barrier.py` and `cl06_fracture_geometry.py`. The Art > Build CL06 Rock Library command builds missing prefabs only, not scene layouts.
- Assemble overlapping compact blocks with coherent outer contours and moderate height variations. Check the solid union at cruise Y78; avoid blind pockets and seams. Ground debris has no radar or collision component, sits tight against the wall and remains below flight height.
- Radar/terrain collision and projectile queries use the marked closed meshes; avoid moving enabled obstacle solids. Test CPU cost on a real sortie, including the initial union build.

Mission 01 uses the committed [v3 layout](MISSION_01_ROCK_LAYOUT_V3.md). Its JSON recipe and SVG remain useful as design/authoring data; no legacy scene installer is active. Prior experimental layouts are recoverable from Git.
