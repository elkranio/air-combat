# AE-01A — Overlord visual trial v0

Fictional Dominion AEW aircraft. Visual-only prefab, Y-up +Z nose, 23.22 x 28 units, 1204 triangles, gray olive hull, gold tips, long nacelles, twin canted fins, 9-unit rotating radar disc. The nine OBJ paint/animation groups use matte URP materials. `RadarPivot` and independent `GlowL`/`GlowR` renderers and `EscapeExhaustL/R` transforms are available for the next atom. No target collider, radar contact, patrol, escape logic, damage, or objective changes.

After patch/import, open `Mission01_FirstSortie` and run **Air Combat > Art > Preview AE01 Overlord v0**. It builds/rebuilds the prefab and places one temporary visual trial near the debug airborne endpoint. Select/frame it (F) in Scene view; with debug air start, Play to compare it with Lynx and see disc spin. Stop and run **Remove AE01 Overlord Preview** before saving/committing the mission scene. Alternatively **Build AE01 Overlord Prefab v0** builds only the prefab. Commit generated prefab/materials/metas after accepting the visual; no permanent scene instance is needed yet. Do not add the trial to the mission objective registry.

Reproducible generator: `python Tools/Blender/build_ae01_overlord.py` (no Blender required for OBJ export). Running with Blender also saves an editable `.blend`. OBJs included in patch; no need to run the generator for normal import. Review proportions, camera readability, shadows, disc rotation and intake/nozzle shape before implementing flight and escape.

## AE-01A2 — silhouette polish

Increase the exposed fin height from 2.74 to 3.29 units and cant the tops slightly farther outward. Add one subtle trailing-edge panel per outer wing, and replace the dome's radial paint wedges with a straight flush dark band. The rotating pivot, materials, mesh asset names, exhaust sockets, overall length and span are unchanged. The revised model has 1292 triangles. Reimport the OBJs and use the existing Preview menu to review at gameplay scale; remove the trial before saving the mission scene. No gameplay changes.
