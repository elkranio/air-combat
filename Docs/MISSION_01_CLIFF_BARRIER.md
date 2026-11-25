# MISSION-01H2B — Cliff barrier layout v4 (WIP)

Base `bf00784`. With the saved `Mission01_FirstSortie` scene open, run **Air Combat > Missions > Place Mission 01 Cliff Barrier v4** once. The command uses the four committed cliff prefabs, creates six independently editable instances and keeps the accepted sand surface and 3200 x 3200 map extent. It sets the mission-only TerrainData heights to zero and rebakes the existing facet mesh using the established palette rules. The old `Mission01 / Cliff mesh trial` scene root is removed (Undo supported); its shared materials, meshes and generator remain. No asset deletions, no runtime generation, no changes to other scenes or to camera/flight/combat settings. Do not run the superseded Terrain ridge/profile or cliff trial placement tools on this layout. Re-running the new command with its group present does not duplicate or move anything.

| Instance | X | Z | Yaw | Scale X / Y / Z |
| --- | ---: | ---: | ---: | --- |
| CL01 Fan | 650 | -800 | 0 | 1.48 / 1 / 1.15 |
| CL02 East | 1160 | -960 | 25 | 1.35 / 1.08 / .85 |
| CL02 West | 300 | -900 | -27 | .95 / 1.10 / .9 |
| CL03 Lower | 650 | -240 | -8 | 1.23 / 1 / 1.10 |
| CL03 Upper | 700 | 390 | 8 | 1.24 / 1.02 / 1.10 |
| CL04 North | 730 | 1010 | -9 | 1.03 / 1 / 1.24 |

All origins are at Y0. The broad south is deliberately extended past the map edge so the Y78 section reaches the boundary. No mission boundary enforcement or collision is implemented yet. Calculated from the source triangle geometry, the union of Y78 sections is one connected polygon with positive-area joins and an unobstructed proposed v4 west/east route; Unity import, shadows, seam visibility, radar outlines at overlapping modules and actual flight clearances require local review. Keep one radar obstacle per prefab; the grouping root has none.

Save the scene and the modified `Mission01_Terrain.asset` and `Mission01_Facets.asset`; commit these with the patch. Inspect from the west and east sides at flight altitude, check the old ridge and trial are absent, radar and caution work, and R/takeoff/AC-01 are unchanged. No SAM, recon or optional target marker positions are changed in H2B; their old layout markers remain provisional until a separate v4 objective placement pass. Physical collision/crash and actual SAM behavior are later atoms.
